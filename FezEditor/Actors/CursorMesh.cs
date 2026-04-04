using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
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

    private bool _dirty;

    internal CursorMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
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
    }

    private struct MeshInstance()
    {
        public Rid Material = Rid.Invalid;
        public Color Color = Color.Transparent;
        public readonly List<(MeshSurface Surface, PrimitiveType Primitive)> Surfaces = new();
    }
}
