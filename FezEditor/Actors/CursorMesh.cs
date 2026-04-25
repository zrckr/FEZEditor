using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public sealed class CursorMesh : ActorComponent
{
    public const float OverlayOffset = 0.01f;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private MeshInstance _selection = new();

    private MeshInstance _hover = new();

    private HologramInstance _hologram = new();

    private bool _dirty;

    internal CursorMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
        _hologram.Mesh = _rendering.MeshCreate();
        _hologram.Instance = _rendering.InstanceCreate(actor.InstanceRid);
        _rendering.InstanceSetMesh(_hologram.Instance, _hologram.Mesh);
        _rendering.InstanceSetVisibility(_hologram.Instance, false);
    }

    public override void LoadContent(IContentManager content)
    {
        _hover.Material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_hover.Material, _rendering.BasicEffect);
        _rendering.MaterialSetCullMode(_hover.Material, CullMode.None);
        _rendering.MaterialSetBlendMode(_hover.Material, BlendMode.AlphaBlend);

        _selection.Material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_selection.Material, _rendering.BasicEffect);
        _rendering.MaterialSetCullMode(_selection.Material, CullMode.None);
        _rendering.MaterialSetBlendMode(_selection.Material, BlendMode.AlphaBlend);

        _hologram.Empty = content.Load<Texture2D>("Textures/Empty");
        _hologram.Material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_hologram.Material, _rendering.BasicEffectTextured);
        _rendering.MaterialSetCullMode(_hologram.Material, CullMode.None);
        _rendering.MaterialSetBlendMode(_hologram.Material, BlendMode.AlphaBlend);
        _rendering.MaterialSetDepthWrite(_hologram.Material, false);
        _rendering.MaterialSetAlbedo(_hologram.Material, Color.White with { A = 120 });
    }

    public void SetHoverSurfaces(IEnumerable<(MeshSurface, PrimitiveType)> surfaces, Color color)
    {
        _hover.Surfaces.AddRange(surfaces);
        _hover.Color = color;
        _dirty = true;
    }

    public void ClearHover()
    {
        if (_hover.Surfaces.Count > 0)
        {
            _hover.Surfaces.Clear();
            _dirty = true;
        }
    }

    public void SetSelectionSurfaces(IEnumerable<(MeshSurface, PrimitiveType)> surfaces, Color color)
    {
        _selection.Surfaces.AddRange(surfaces);
        _selection.Color = color;
        _dirty = true;
    }

    public void ClearSelection()
    {
        if (_selection.Surfaces.Count > 0)
        {
            _selection.Surfaces.Clear();
            _dirty = true;
        }
    }

    public void UpdateHologram(TrileSet trileSet, int trileId)
    {
        _hologram.Texture?.Dispose();
        _rendering.MeshClear(_hologram.Mesh);

        if (trileSet.Triles.TryGetValue(trileId, out var trile) && trile.Geometry.Indices.Length > 0)
        {
            _hologram.Texture = RepackerExtensions.ConvertToTexture2D(trileSet.TextureAtlas);
            RepackerExtensions.SetAlpha(_hologram.Texture, 1f);
            _rendering.MaterialAssignBaseTexture(_hologram.Material, _hologram.Texture);
            var surface = RepackerExtensions.ConvertToMesh(trile.Geometry.Vertices, trile.Geometry.Indices);
            _rendering.MeshAddSurface(_hologram.Mesh, PrimitiveType.TriangleList, surface, _hologram.Material);
        }
        else
        {
            var size = trile?.Size.ToXna() ?? Vector3.One;
            _rendering.MaterialAssignBaseTexture(_hologram.Material, _hologram.Empty);
            var surface = MeshSurface.CreateTexturedBox(size);
            _rendering.MeshAddSurface(_hologram.Mesh, PrimitiveType.TriangleList, surface, _hologram.Material);
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

    public override void Update(GameTime gameTime)
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        _rendering.MeshClear(_mesh);

        if (_hover.Surfaces.Count > 0)
        {
            _rendering.MaterialSetAlbedo(_hover.Material, _hover.Color);
            foreach (var (surface, primitive) in _hover.Surfaces)
            {
                _rendering.MeshAddSurface(_mesh, primitive, surface, _hover.Material);
            }
        }

        if (_selection.Surfaces.Count > 0)
        {
            _rendering.MaterialSetAlbedo(_selection.Material, _selection.Color);
            foreach (var (surface, primitive) in _selection.Surfaces)
            {
                _rendering.MeshAddSurface(_mesh, primitive, surface, _selection.Material);
            }
        }

        var isVisible = (_hover.Surfaces is { Count: > 0 }) || (_selection.Surfaces is { Count: > 0 });
        _rendering.InstanceSetVisibility(Actor.InstanceRid, isVisible);
    }

    public override void Dispose()
    {
        _rendering.FreeRid(_selection.Material);
        _rendering.FreeRid(_hover.Material);
        _rendering.FreeRid(_mesh);
        _hologram.Texture?.Dispose();
        _rendering.FreeRid(_hologram.Instance);
        _rendering.FreeRid(_hologram.Mesh);
        _rendering.FreeRid(_hologram.Material);
    }

    private struct MeshInstance()
    {
        public Rid Material = Rid.Invalid;
        public Color Color = Color.Transparent;
        public readonly List<(MeshSurface Surface, PrimitiveType Primitive)> Surfaces = new();
    }

    private struct HologramInstance()
    {
        public Rid Instance = Rid.Invalid;
        public Rid Mesh = Rid.Invalid;
        public Rid Material = Rid.Invalid;
        public Texture2D? Texture = null!;
        public Texture2D Empty = null!;
        public bool Visible = false;
    }
}
