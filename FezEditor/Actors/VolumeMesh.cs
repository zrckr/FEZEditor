using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class VolumeMesh : ActorComponent
{
    private static readonly Color WireColor = Color.LimeGreen;
    private const float OverlayOversize = 1.025f;

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
        _rendering.MaterialSetAlbedo(_overlay, Color.White with { A = 102 }); // 40%
        _rendering.MaterialSetSamplerState(_overlay, SamplerState.PointWrap);
        _rendering.MaterialSetDepthWrite(_overlay, false);
    }

    public void Visualize(Vector3 from, Vector3 to)
    {
        var size = to - from;
        Actor.Transform.Position = from + size / 2f;
        _rendering.MeshClear(_mesh);

        var visualSize = size * OverlayOversize;

        var surface2 = MeshSurface.CreateTexturedBox(visualSize);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface2, _overlay);

        var surface1 = MeshSurface.CreateWireframeBox(visualSize, WireColor);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.LineList, surface1, _material);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_overlay);
        _rendering.FreeRid(_material);
        _rendering.FreeRid(_mesh);
    }
}