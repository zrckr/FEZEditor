using FezEditor.Structure;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public partial class RenderingService : IDisposable
{
    private static readonly ILogger Logger = Logging.Create<RenderingService>();

    public GraphicsDevice GraphicsDevice { get; }

    private readonly Queue<Rid> _instanceTraversal = new();

    private uint _nextRid = 1;

    private int _drawCalls;

    private int _primitives;

    private float _fps;

    private float _frameTimeMs;

    private int _fpsFrameCount;

    private TimeSpan _fpsElapsed;

    public RenderingService(Game game)
    {
        GraphicsDevice = game.GraphicsDevice;
        _backbufferRid = CreateBackbuffer();
        Logger.Debug("Initialized with backbuffer {0}x{1}",
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight);
    }

    public void Draw(GameTime gameTime)
    {
        _drawCalls = 0;
        _primitives = 0;
        _frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        _fpsElapsed += gameTime.ElapsedGameTime;
        _fpsFrameCount++;
        if (_fpsElapsed >= TimeSpan.FromSeconds(1))
        {
            _fps = _fpsFrameCount / (float)_fpsElapsed.TotalSeconds;
            _fpsFrameCount = 0;
            _fpsElapsed = TimeSpan.Zero;
        }

        foreach (var rt in _renderTargets.Values)
        {
            if (!TryGetResource(_worlds, rt.World, out var world))
            {
                continue;
            }

            // Bind render target.
            GraphicsDevice.SetRenderTarget(rt.IsBackbuffer ? null : rt.Target);
            GraphicsDevice.Clear(rt.ClearColor);

            // Build matrices pack
            var view = Matrix.Identity;
            var projection = Matrix.Identity;
            if (TryGetResource(_cameras, world!.Camera, out var cam))
            {
                view = cam!.View;
                projection = cam.Projection;
            }

            var viewProjection = view * projection;

            // Draw instances in tree traversal order.
            _instanceTraversal.Clear();
            _instanceTraversal.Enqueue(world.Root);

            while (_instanceTraversal.Count > 0)
            {
                var instanceRid = _instanceTraversal.Dequeue();
                var instance = GetResource(_instances, instanceRid);
                if (!instance.Visible)
                {
                    continue;
                }

                var worldMatrix = ComputeWorldMatrix(instance);
                var matrices = new InstanceMatrices(worldMatrix, view, projection, viewProjection);
                switch (instance.Type)
                {
                    case InstanceType.None:
                        // Draw nothing
                        break;
                    case InstanceType.Mesh:
                        DrawMesh(rt, world, instance.Internal, matrices);
                        break;
                    case InstanceType.MultiMesh:
                        DrawMultiMesh(rt, world, instance.Internal, matrices);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("Not supported instance type:  " + instance.Type);
                }

                foreach (var child in instance.Children)
                {
                    _instanceTraversal.Enqueue(child);
                }
            }

            // Unbind render target.
            if (!rt.IsBackbuffer)
            {
                GraphicsDevice.SetRenderTarget(null);
            }
        }
    }

    public Dictionary<string, string> GetStats()
    {
        var instances = _multiMeshes.Values.Sum(mm => mm.VisibleInstances < 0 ? mm.InstanceCount : mm.VisibleInstances);
        var textures = _materials.Values.Select(md => md.Texture).Where(t => t is { IsDisposed: false }).Distinct().Count();

        return new Dictionary<string, string>
        {
            ["FPS"] = $"{_fps:F2}",
            ["Frame Time"] = $"{_frameTimeMs:F1} ms",
            ["Draw Calls"] = _drawCalls.ToString(),
            ["Primitives"] = _primitives.ToString(),
            ["Meshes"] = _meshes.Count.ToString(),
            ["Materials"] = _materials.Count.ToString(),
            ["Textures"] = textures.ToString(),
            ["Instances"] = instances.ToString()
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var rt in _renderTargets.Values)
        {
            DisposeRenderTarget(rt);
        }

        foreach (var mesh in _meshes.Values)
        {
            DisposeBuffers(mesh);
        }

        foreach (var mm in _multiMeshes.Values)
        {
            mm.InstanceBuffer?.Dispose();
            mm.InstanceDeclaration?.Dispose();
        }

        _cameras.Clear();
        _worlds.Clear();
        _instances.Clear();
        _meshes.Clear();
        _materials.Clear();
        _multiMeshes.Clear();
        _renderTargets.Clear();
        Logger.Debug("Disposed");
    }

    public void FreeRid(Rid rid)
    {
        if (rid.Type == typeof(RenderTargetData))
        {
            if (RemoveResource(_renderTargets, rid, out var rt))
            {
                DisposeRenderTarget(rt!);
                Logger.Debug("Freed RenderTarget {0}", rid);
            }
        }
        else if (rid.Type == typeof(WorldData))
        {
            if (RemoveResource(_worlds, rid, out var world))
            {
                DisposeInstanceTree(world!.Root);
                Logger.Debug("Freed World {0}", rid);
            }
        }
        else if (rid.Type == typeof(InstanceData))
        {
            if (_instances.ContainsKey(rid))
            {
                DisposeInstanceTree(rid);
                Logger.Debug("Freed Instance {0}", rid);
            }
        }
        else if (rid.Type == typeof(CameraData))
        {
            RemoveResource(_cameras, rid, out _);
            Logger.Debug("Freed Camera {0}", rid);
        }
        else if (rid.Type == typeof(MeshData))
        {
            if (RemoveResource(_meshes, rid, out var mesh))
            {
                DisposeBuffers(mesh!);
                InvalidateMultiMesh(rid);
                Logger.Debug("Freed Mesh {0}", rid);
            }
        }
        else if (rid.Type == typeof(MaterialData))
        {
            if (RemoveResource(_materials, rid, out var material))
            {
                material!.Effect?.Dispose();
                InvalidateMaterial(rid);
                Logger.Debug("Freed Material {0}", rid);
            }
        }
        else if (rid.Type == typeof(MultiMeshData))
        {
            if (RemoveResource(_multiMeshes, rid, out var mm))
            {
                mm!.InstanceBuffer?.Dispose();
                mm.InstanceDeclaration?.Dispose();
                Logger.Debug("Freed MultiMesh {0}", rid);
            }
        }
    }

    private Rid AllocateRid(Type type)
    {
        return new Rid(_nextRid++, type);
    }

    private static T GetResource<T>(Dictionary<Rid, T> store, Rid rid) where T : class
    {
        if (!rid.IsValid || !store.TryGetValue(rid, out var resource))
        {
            throw new InvalidOperationException($"Invalid RID: {rid}");
        }

        return resource;
    }

    private static bool TryGetResource<T>(Dictionary<Rid, T> store, Rid rid, out T? resource) where T : class
    {
        if (rid.IsValid && store.TryGetValue(rid, out var data))
        {
            resource = data;
            return true;
        }

        resource = null;
        return false;
    }

    private static bool RemoveResource<T>(Dictionary<Rid, T> store, Rid rid, out T? resource) where T : class
    {
        if (rid.IsValid && store.Remove(rid, out var data))
        {
            resource = data;
            return true;
        }

        resource = null;
        return false;
    }

    private record InstanceMatrices(Matrix World, Matrix View, Matrix Projection, Matrix ViewProjection);
}