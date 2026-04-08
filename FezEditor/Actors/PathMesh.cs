using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class PathMesh : ActorComponent, IPickable
{
    private static readonly Color PathColor = new(1f, 0.5f, 0f, 0.8f);

    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private Rid _material;

    internal PathMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_material, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
    }

    public void Visualize(Vector3[] segments)
    {
        if (segments.Length < 2)
        {
            return;
        }

        var vertices = new Vector3[segments.Length];
        var colors = new Color[segments.Length];
        var indices = new int[(segments.Length - 1) * 2];

        for (var i = 0; i < segments.Length; i++)
        {
            vertices[i] = segments[i];
            colors[i] = PathColor;
        }

        for (var i = 0; i < segments.Length - 1; i++)
        {
            indices[i * 2] = i;
            indices[i * 2 + 1] = i + 1;
        }

        var surface = new MeshSurface
        {
            Vertices = vertices,
            Colors = colors,
            Indices = indices
        };

        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface, _material);
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        return Array.Empty<BoundingBox>();
    }

    public PickHit? Pick(Ray ray)
    {
        return null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
    }
}
