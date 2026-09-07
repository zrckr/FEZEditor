using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public sealed class CursorMesh : ActorComponent
{
    private const float CursorDepthBias = -0.000001f;

    private const float CollisionDepthBias = -0.0005f;

    private static readonly Matrix[] IdentityInstance = [CreateInstanceMatrix(Vector3.Zero)];

    private readonly RenderingService _rendering;

    private readonly Overlay _selection;

    private readonly Overlay _hover;

    private HologramInstance _hologram = new();

    internal CursorMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _hover = new Overlay(_rendering, actor.InstanceRid);
        _selection = new Overlay(_rendering, actor.InstanceRid);
        _hologram.Mesh = _rendering.MeshCreate();
        _hologram.Instance = _rendering.InstanceCreate(actor.InstanceRid);
        _rendering.InstanceSetMesh(_hologram.Instance, _hologram.Mesh);
        _rendering.InstanceSetVisibility(_hologram.Instance, false);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/CursorMesh");
        _hover.Load(effect);
        _selection.Load(effect);

        _hologram.Material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_hologram.Material, _rendering.BasicEffectTextured);
        _rendering.MaterialSetCullMode(_hologram.Material, CullMode.None);
        _rendering.MaterialSetBlendMode(_hologram.Material, BlendMode.AlphaBlend);
        _rendering.MaterialSetDepthWrite(_hologram.Material, false);
        _rendering.MaterialSetAlbedo(_hologram.Material, Color.White with { A = 120 });
        _rendering.MaterialSetDepthBias(_hologram.Material, CursorDepthBias, 0.0f);

        foreach (var collision in Enum.GetValues<CollisionType>())
        {
            var material = _rendering.MaterialCreate();
            _rendering.MaterialAssignEffect(material, _rendering.BasicEffectTextured);
            _rendering.MaterialSetCullMode(material, CullMode.None);
            _rendering.MaterialSetBlendMode(material, BlendMode.AlphaBlend);
            _rendering.MaterialSetDepthWrite(material, false);
            _rendering.MaterialSetAlbedo(material, Color.White with { A = 120 });
            _rendering.MaterialAssignBaseTexture(material, content.Load<Texture2D>($"Textures/{collision}"));
            _rendering.MaterialSetDepthBias(material, CollisionDepthBias, 0.0f);
            _hologram.CollisionMaterials[collision] = material;
        }
    }

    public void SetHoverSurfaces(IEnumerable<(MeshSurface, PrimitiveType)> surfaces, Color color)
    {
        foreach (var (surface, primitive) in surfaces)
        {
            UploadHoverMesh(surface, primitive, color);
            SetHoverInstances(IdentityInstance);
        }
    }

    public void UploadHoverMesh(MeshSurface surface, PrimitiveType primitive, Color color)
    {
        _hover.UploadMesh(surface, primitive, color);
    }

    public void SetHoverInstances(IEnumerable<Matrix> instances)
    {
        _hover.SetInstances(instances);
    }

    public void ClearHover()
    {
        // Keep template meshes resident while removing their active instances
        _hover.Clear();
    }

    public void SetSelectionSurfaces(IEnumerable<(MeshSurface, PrimitiveType)> surfaces, Color color)
    {
        foreach (var (surface, primitive) in surfaces)
        {
            UploadSelectionMesh(surface, primitive, color);
            SetSelectionInstances(IdentityInstance);
        }
    }

    public void UploadSelectionMesh(MeshSurface surface, PrimitiveType primitive, Color color)
    {
        _selection.UploadMesh(surface, primitive, color);
    }

    public void SetSelectionInstances(IEnumerable<Matrix> instances)
    {
        _selection.SetInstances(instances);
    }

    public void ClearSelection()
    {
        // Keep template meshes resident while removing their active instances
        _selection.Clear();
    }

    public void UpdateHologram(TrileSet trileSet, int trileId)
    {
        _hologram.Texture?.Dispose();
        _rendering.MeshClear(_hologram.Mesh);

        if (trileSet.Triles.TryGetValue(trileId, out var trile) &&
            !trile.Geometry.IsNullOrEmpty() &&
            trileSet.TextureAtlas != null)
        {
            _hologram.Texture = RepackerExtensions.ExtractColorToTexture2D(trileSet.TextureAtlas);
            _rendering.MaterialAssignBaseTexture(_hologram.Material, _hologram.Texture);
            var surface = RepackerExtensions.ConvertToMesh(trile.Geometry.Vertices, trile.Geometry.Indices);
            _rendering.MeshAddSurface(_hologram.Mesh, PrimitiveType.TriangleList, surface, _hologram.Material);
        }
        else
        {
            var size = trile?.Size.ToXna() ?? Vector3.One;
            foreach (var (face, collision) in trile?.Faces ?? new Dictionary<FaceOrientation, CollisionType>())
            {
                var surface = MeshSurface.CreateFaceQuad(size, face);
                surface.Translate((trile?.Offset.ToXna() ?? Vector3.Zero) / 2f);
                _rendering.MeshAddSurface(_hologram.Mesh, PrimitiveType.TriangleList, surface,
                    _hologram.CollisionMaterials[collision]);
            }
        }
    }

    public void SetHologramPose(Vector3 worldPosition, Quaternion rotation)
    {
        _rendering.InstanceSetPosition(_hologram.Instance, worldPosition);
        _rendering.InstanceSetRotation(_hologram.Instance, rotation);
        if (!_hologram.Visible)
        {
            _hologram.Visible = true;
            _rendering.InstanceSetVisibility(_hologram.Instance, true);
        }
    }

    public void ClearHologram()
    {
        if (_hologram.Visible)
        {
            _hologram.Visible = false;
            _rendering.InstanceSetVisibility(_hologram.Instance, false);
        }
    }

    public override void Dispose()
    {
        _selection.Dispose();
        _hover.Dispose();
        _hologram.Texture?.Dispose();
        _rendering.FreeRid(_hologram.Instance);
        _rendering.FreeRid(_hologram.Mesh);
        _rendering.FreeRid(_hologram.Material);
        foreach (var material in _hologram.CollisionMaterials.Values)
        {
            _rendering.FreeRid(material);
        }
    }

    private struct HologramInstance()
    {
        public Rid Instance = Rid.Invalid;
        public Rid Mesh = Rid.Invalid;
        public Rid Material = Rid.Invalid;
        public Texture2D? Texture = null!;
        public readonly Dictionary<CollisionType, Rid> CollisionMaterials = new();
        public bool Visible = false;
    }

    private static Matrix CreateInstanceMatrix(Vector3 position)
    {
        return new Matrix(
            position.X, position.Y, position.Z, 0f,
            0f, 0f, 0f, 1f,
            1f, 1f, 1f, 0f,
            0f, 0f, 0f, 0f
        );
    }

    private sealed class Overlay : IDisposable
    {
        private readonly RenderingService _rendering;

        private readonly Rid _parent;

        private readonly List<Batch> _batches = new();

        private Effect? _effect;

        private int _activeBatchIndex = -1;

        public Overlay(RenderingService rendering, Rid parent)
        {
            _rendering = rendering;
            _parent = parent;
        }

        public void Load(Effect effect)
        {
            _effect = effect;
            foreach (var batch in _batches)
            {
                batch.Load(effect);
            }
        }

        public void UploadMesh(MeshSurface surface, PrimitiveType primitive, Color color)
        {
            var batchIndex = ++_activeBatchIndex;
            while (_batches.Count <= batchIndex)
            {
                _batches.Add(new Batch(_rendering, _parent));
            }

            var batch = _batches[batchIndex];
            if (_effect != null)
            {
                batch.Load(_effect);
            }

            batch.UploadMesh(surface, primitive, color);
        }

        public void SetInstances(IEnumerable<Matrix> instances)
        {
            if (_activeBatchIndex < 0)
            {
                throw new InvalidOperationException("Upload a cursor mesh before setting its instances.");
            }

            _batches[_activeBatchIndex].SetInstances(instances);
        }

        public void Clear()
        {
            foreach (var batch in _batches)
            {
                batch.Clear();
            }

            _activeBatchIndex = -1;
        }

        public void Dispose()
        {
            foreach (var batch in _batches)
            {
                batch.Dispose();
            }
        }
    }

    private sealed class Batch : IDisposable
    {
        private readonly RenderingService _rendering;

        private readonly Rid _mesh;

        private readonly Rid _material;

        private readonly Rid _multiMesh;

        private readonly Rid _instance;

        private MeshSurface? _surface;

        private PrimitiveType _primitive;

        private bool _loaded;

        public Batch(RenderingService rendering, Rid parent)
        {
            _rendering = rendering;
            _mesh = _rendering.MeshCreate();
            _material = _rendering.MaterialCreate();
            _multiMesh = _rendering.MultiMeshCreate();
            _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
            _instance = _rendering.InstanceCreate(parent);
            _rendering.InstanceSetMultiMesh(_instance, _multiMesh);
            _rendering.InstanceSetVisibility(_instance, false);
        }

        public void Load(Effect effect)
        {
            if (!_loaded)
            {
                _rendering.MaterialAssignEffect(_material, effect);
                _rendering.MaterialSetCullMode(_material, CullMode.None);
                _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);
                _rendering.MaterialSetDepthBias(_material, CursorDepthBias, 0.0f);
                _loaded = true;
            }
        }

        public void UploadMesh(MeshSurface surface, PrimitiveType primitive, Color color)
        {
            _rendering.MaterialSetAlbedo(_material, color);
            if (!ReferenceEquals(_surface, surface) || _primitive != primitive)
            {
                // Replace only the template geometry; instance data stays separate
                _rendering.MeshClear(_mesh);
                _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
                _rendering.MeshAddSurface(_mesh, primitive, surface, _material);
                _surface = surface;
                _primitive = primitive;
            }
        }

        public void SetInstances(IEnumerable<Matrix> instances)
        {
            var count = _rendering.MultiMeshSetInstances(_multiMesh, instances);
            _rendering.InstanceSetVisibility(_instance, count > 0);
        }

        public void Clear()
        {
            _rendering.MultiMeshSetVisibleInstances(_multiMesh, 0);
            _rendering.InstanceSetVisibility(_instance, false);
        }

        public void Dispose()
        {
            _rendering.FreeRid(_instance);
            _rendering.FreeRid(_multiMesh);
            _rendering.FreeRid(_mesh);
            _rendering.FreeRid(_material);
        }
    }
}