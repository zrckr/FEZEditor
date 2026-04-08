using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class ArtObjectMesh : ActorComponent, IPickable
{
    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private Texture2D? _texture;

    private Vector3 _size;

    internal ArtObjectMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/ArtObjectMesh");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public void Visualize(ArtObject ao)
    {
        _texture?.Dispose();
        _texture = RepackerExtensions.ConvertToTexture2D(ao.Cubemap);
        _rendering.MaterialAssignBaseTexture(_material, _texture);
        _size = ao.Size.ToXna();

        var surface = RepackerExtensions.ConvertToMesh(ao.Geometry.Vertices, ao.Geometry.Indices);
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        yield return Mathz.ComputeBoundingBox(
            Actor.Transform.Position, Actor.Transform.Rotation, Actor.Transform.Scale, _size);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        var dist = ray.Intersects(box);
        return dist.HasValue ? new PickHit(dist.Value, 0) : null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _texture?.Dispose();
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}