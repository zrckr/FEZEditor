using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrimitiveType = Microsoft.Xna.Framework.Graphics.PrimitiveType;

namespace FezEditor.Actors;

public class SkyBackgroundMesh : ActorComponent
{
    private static readonly SamplerState CustomSamplerState = new()
    {
        Filter = TextureFilter.Linear,
        AddressU = TextureAddressMode.Wrap,
        AddressV = TextureAddressMode.Clamp
    };

    public float Opacity { get; set; } = 1f;

    public Clock? Clock { private get; set; }

    private readonly RenderingService _rendering;

    private readonly ResourceService _resources;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _world;

    private Texture2D? _texture;

    private Color[] _fogColors = [];

    private Matrix _textureMatrix = Matrix.Identity;

    internal SkyBackgroundMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _resources = game.GetService<ResourceService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _world = _rendering.InstanceGetWorld(actor.InstanceRid);
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/SkyBackgroundMesh");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public void Visualize(string name, string background)
    {
        var texture = (RTexture2D)_resources.Load($"Skies/{name}/{background}");
        _texture?.Dispose();
        _texture = RepackerExtensions.ConvertToTexture2D(texture);

        _fogColors = new Color[texture.Width];
        for (var x = 0; x < texture.Width; x++)
        {
            var offset = (texture.Height / 2 * texture.Width + x) * 4;
            _fogColors[x] = new Color(
                texture.TextureData[offset],
                texture.TextureData[offset + 1],
                texture.TextureData[offset + 2],
                texture.TextureData[offset + 3]
            );
        }

        _rendering.MaterialAssignBaseTexture(_material, _texture);
        _rendering.MaterialSetBlendMode(_material, BlendMode.Screen);
        _rendering.MaterialSetDepthWrite(_material, false);
        _rendering.MaterialSetDepthTest(_material, CompareFunction.Always);
        _rendering.MaterialSetSamplerState(_material, CustomSamplerState);
        _rendering.MaterialSetCullMode(_material, CullMode.None);

        var surface = MeshSurface.CreateFaceQuad(Vector3.One * 2f, FaceOrientation.Front);
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);
    }

    public override void Update(GameTime gameTime)
    {
        if (Clock == null)
        {
            throw new InvalidOperationException("Clock is missing");
        }

        _textureMatrix.M11 = 0.0001f;
        _textureMatrix.M31 = Clock.DayFraction;
        _rendering.MaterialSetTextureTransform(_material, _textureMatrix);
        _rendering.MaterialSetAlbedo(_material, new Color(Opacity, Opacity, Opacity, 1f));

        var index = Clock.DayFraction * _fogColors.Length;
        if (Mathz.IsEqualApprox(index, _fogColors.Length))
        {
            index = 0f;
        }

        var color1 = _fogColors[Math.Max((int)Math.Floor(index), 0)];
        var color2 = _fogColors[Math.Min((int)Math.Ceiling(index), _fogColors.Length - 1)];
        var amount = Mathz.Frac(index);

        var fogColor = Color.Lerp(color1, color2, amount);
        _rendering.WorldSetFogColor(_world, fogColor);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _texture?.Dispose();
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }
}