using FezEditor.Structure;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using InstanceType = FezEditor.Services.IRenderingService.InstanceType;

namespace FezEditor.Services;

[UsedImplicitly]
public partial class RenderingService : IRenderingService
{
    private readonly GraphicsDevice _device;
    
    private readonly Queue<Rid> _instanceTraversal = new();

    private uint _nextRid = 1;
    
    public RenderingService(Game game)
    {
        _device = game.GraphicsDevice;
        _backbufferRid = CreateBackbuffer();
    }

    public void Draw(GameTime gameTime)
    {
        foreach (var rt in _renderTargets.Values)
        {
            if (!TryGetResource(_worlds, rt.World, out var world))
            {
                continue;
            }

            // Bind render target.
            _device.SetRenderTarget(rt.IsBackbuffer ? null : rt.Target);
            _device.Clear(rt.ClearColor);

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
                _device.SetRenderTarget(null);
            }
        }
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
        
        foreach(var mm in _multiMeshes.Values)
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
    }

    public void FreeRid(Rid rid)
    {
        if (rid.Type == typeof(RenderTargetData))
        {
            if (RemoveResource(_renderTargets, rid, out var rt))
            {
                DisposeRenderTarget(rt!);
            }
        }
        else if (rid.Type == typeof(WorldData))
        {
            if (RemoveResource(_worlds, rid, out var world))
            {
                DisposeInstanceTree(world!.Root);
            }
        }
        else if (rid.Type == typeof(InstanceData))
        {
            if (_instances.ContainsKey(rid))
            {
                DisposeInstanceTree(rid);
            }
        }
        else if (rid.Type == typeof(CameraData))
        {
            RemoveResource(_cameras, rid, out _);
        }
        else if (rid.Type == typeof(MeshData))
        {
            if (RemoveResource(_meshes, rid, out var mesh))
            {
                DisposeBuffers(mesh!);
                InvalidateMultiMesh(rid);
            }
        }
        else if (rid.Type == typeof(MaterialData))
        {
            if (RemoveResource(_materials, rid, out _))
            {
                InvalidateMaterial(rid);
            }
        }
        else if (rid.Type == typeof(MultiMeshData))
        {
            RemoveResource(_multiMeshes, rid, out _);
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
