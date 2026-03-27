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

    private readonly Rid _hoverMaterial;

    private readonly Rid _selectionMaterial;

    private List<(MeshSurface Surface, PrimitiveType Primitive)>? _hoverSurfaces;

    private Color _hoverColor;

    private List<(MeshSurface Surface, PrimitiveType Primitive)>? _selectionSurfaces;

    private Color _selectionColor;

    private bool _dirty;

    internal CursorMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _hoverMaterial = _rendering.MaterialCreate();
        _selectionMaterial = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _rendering.MaterialAssignEffect(_hoverMaterial, _rendering.BasicEffect);
        _rendering.MaterialSetCullMode(_hoverMaterial, CullMode.None);
        _rendering.MaterialSetBlendMode(_hoverMaterial, BlendMode.AlphaBlend);

        _rendering.MaterialAssignEffect(_selectionMaterial, _rendering.BasicEffect);
        _rendering.MaterialSetCullMode(_selectionMaterial, CullMode.None);
        _rendering.MaterialSetBlendMode(_selectionMaterial, BlendMode.AlphaBlend);
    }

    public void SetHoverSurfaces(IEnumerable<(MeshSurface, PrimitiveType)> surfaces, Color color)
    {
        _hoverSurfaces = surfaces.ToList();
        _hoverColor = color;
        _dirty = true;
    }

    public void ClearHover()
    {
        if (_hoverSurfaces is not { Count: > 0 })
        {
            return;
        }

        _hoverSurfaces = null;
        _dirty = true;
    }

    public void SetSelectionSurfaces(IEnumerable<(MeshSurface, PrimitiveType)> surfaces, Color color)
    {
        _selectionSurfaces = surfaces.ToList();
        _selectionColor = color;
        _dirty = true;
    }

    public void ClearSelection()
    {
        if (_selectionSurfaces is not { Count: > 0 })
        {
            return;
        }

        _selectionSurfaces = null;
        _dirty = true;
    }

    public override void Update(GameTime gameTime)
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        _rendering.MeshClear(_mesh);

        if (_hoverSurfaces is { Count: > 0 })
        {
            _rendering.MaterialSetAlbedo(_hoverMaterial, _hoverColor);
            foreach (var (surface, primitive) in _hoverSurfaces)
            {
                _rendering.MeshAddSurface(_mesh, primitive, surface, _hoverMaterial);
            }
        }

        if (_selectionSurfaces is { Count: > 0 })
        {
            _rendering.MaterialSetAlbedo(_selectionMaterial, _selectionColor);
            foreach (var (surface, primitive) in _selectionSurfaces)
            {
                _rendering.MeshAddSurface(_mesh, primitive, surface, _selectionMaterial);
            }
        }

        var isVisible = (_hoverSurfaces is { Count: > 0 }) || (_selectionSurfaces is { Count: > 0 });
        _rendering.InstanceSetVisibility(Actor.InstanceRid, isVisible);
    }

    public override void Dispose()
    {
        _rendering.FreeRid(_selectionMaterial);
        _rendering.FreeRid(_hoverMaterial);
        _rendering.FreeRid(_mesh);
    }
}
