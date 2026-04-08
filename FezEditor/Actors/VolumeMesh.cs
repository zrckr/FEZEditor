using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class VolumeMesh : ActorComponent, IPickable
{
    private const float OverlayOversize = 1.025f;

    public Vector3 Size { get; set; } = Vector3.One;

    public Color Color { get; set; } = Color.White;

    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _overlay;

    internal VolumeMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _overlay = _rendering.MaterialCreate();
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _rendering.MaterialAssignEffect(_material, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_material, CullMode.None);

        var texture = content.Load<Texture2D>("Textures/Volume");
        _rendering.MaterialAssignEffect(_overlay, _rendering.BasicEffect);
        _rendering.MaterialAssignBaseTexture(_overlay, texture);
        _rendering.MaterialSetCullMode(_overlay, CullMode.None);
        _rendering.MaterialSetSamplerState(_overlay, SamplerState.PointWrap);
        _rendering.MaterialSetDepthWrite(_overlay, false);
    }

    public override void Update(GameTime gameTime)
    {
        var surface2 = MeshSurface.CreateTexturedBox(Size * OverlayOversize);
        var surface1 = MeshSurface.CreateWireframeBox(Size * OverlayOversize, Color);

        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface2, _overlay);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface1, _material);
        _rendering.MaterialSetAlbedo(_overlay, Color with { A = 102 }); // 40%
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        var half = Size / 2f;
        var position = Actor.Transform.Position;
        yield return new BoundingBox(position - half, position + half);
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
        _rendering.FreeRid(_overlay);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
    }
}