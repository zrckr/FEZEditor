using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class StarsMesh : ActorComponent
{
    private const int Stars = 49;

    private const float Size = 4f;

    private const float Spacing = 200f;

    private const float OrthogonalSize = 20f;

    private static readonly Vector4[] Colors =
    [
        new Color(20, 1, 28).ToVector4(),
        new Color(108, 27, 44).ToVector4(),
        new Color(225, 125, 53).ToVector4(),
        new Color(246, 231, 108).ToVector4(),
        new Color(155, 226, 177).ToVector4(),
        new Color(67, 246, 255).ToVector4(),
        new Color(100, 154, 224).ToVector4(),
        new Color(214, 133, 180).ToVector4(),
        new Color(189, 63, 117).ToVector4(),
        new Color(98, 21, 88).ToVector4(),
        new Color(255, 255, 255).ToVector4()
    ];

    public Camera? Camera { private get; set; }

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _multiMesh;

    private readonly Rid _material;

    private readonly Rid _rt;

    public StarsMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _multiMesh = _rendering.MultiMeshCreate();
        _material = _rendering.MaterialCreate();
        _rt = _rendering.WorldGetRenderTarget(_rendering.InstanceGetWorld(actor.InstanceRid));
        _rendering.MultiMeshSetMesh(_multiMesh, _mesh);
        _rendering.InstanceSetMultiMesh(actor.InstanceRid, _multiMesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/StarsMesh");
        var texture = content.Load<Texture2D>("Default");
        var surface = MeshSurface.CreateQuad(Vector3.One);

        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
        _rendering.MaterialAssignEffect(_material, effect);
        _rendering.MaterialAssignBaseTexture(_material, texture);
        _rendering.MaterialSetBlendMode(_material, BlendMode.Additive);
        _rendering.MaterialSetCullMode(_material, CullMode.None);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialShaderSetParam(_material, "Colors", Colors);
        _rendering.MaterialShaderSetParam(_material, "Size", Size);

        const float half = Stars / 2f;
        var random = new Random(42);
        var index = 0;

        _rendering.MultiMeshAllocate(_multiMesh, Stars * Stars * Stars, MultiMeshDataType.Vector4);
        for (var i = 0; i < Stars; i++)
        {
            for (var j = 0; j < Stars; j++)
            {
                for (var k = 0; k < Stars; k++)
                {
                    _rendering.MultiMeshSetInstanceVector4(_multiMesh, index++, new Vector4
                    {
                        X = (i - half) * Spacing,
                        Y = (j - half) * Spacing,
                        Z = (k - half) * Spacing,
                        W = random.Next(0, Colors.Length)
                    });
                }
            }
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (Camera is not { Projection: Camera.ProjectionType.Orthographic })
        {
            return;
        }

        var scale = OrthogonalSize / Camera.Size;
        var view = Matrix.CreateScale(scale) * Camera.View;

        var (width, height) = _rendering.RenderTargetGetSize(_rt);
        var projection = Matrix.CreatePerspectiveFieldOfView(
            fieldOfView: MathHelper.ToRadians(75f),
            aspectRatio: (float)width / height,
            nearPlaneDistance: 0.1f,
            farPlaneDistance: 1000f);

        _rendering.MaterialShaderSetParam(_material, "Projection", view * projection);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_multiMesh);
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}
