using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class BoundsMesh : ActorComponent
{
    private static readonly Color WireColor = Color.White;

    public Vector3 Size { get; set; } = Vector3.Zero;

    private readonly RenderingService _rendering;

    private readonly Rid _instance;

    private readonly Rid _mesh;

    private Rid _material;

    private readonly Transform _transform;

    public BoundsMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        var world = _rendering.InstanceGetWorld(actor.InstanceRid);
        var root = _rendering.WorldGetRoot(world);
        _instance = _rendering.InstanceCreate(root);
        _mesh = _rendering.MeshCreate();
        _rendering.InstanceSetMesh(_instance, _mesh);
        _transform = actor.GetComponent<Transform>();
    }

    public override void LoadContent(IContentManager content)
    {
        _material = _rendering.MaterialCreate();
        _rendering.MaterialAssignEffect(_material, _rendering.BasicEffectVertexColor);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
    }

    public override void Update(GameTime gameTime)
    {
        var surface = MeshSurface.CreateWireframeBox(Size, WireColor);
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface, _material);
        _rendering.InstanceSetPosition(_instance, _transform.Position + Size / 2f);
        _rendering.InstanceSetVisibility(_instance, Actor.Visible);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_instance);
    }
}