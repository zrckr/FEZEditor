using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.MapTree;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class MapNodeMesh : ActorComponent, IPickable
{
    private const float OutlineSize = 1.25f;

    private const float FallbackSize = 50f;

    private const string TexturePath = "Other Textures/map_screens/";

    private static readonly SamplerState PointMipClamp = new()
    {
        AddressU = TextureAddressMode.Clamp,
        AddressV = TextureAddressMode.Clamp,
        Filter = TextureFilter.MinLinearMagPointMipLinear
    };

    public Camera? Camera { private get; set; }

    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly ResourceService _resources;

    private readonly Transform _transform;

    private readonly Rid _mesh;

    private readonly Rid _outlineMaterial;

    private readonly Rid _textureMaterial;

    private readonly Rid _rt;

    private Texture2D? _defaultTexture;

    private Texture2D? _nodeTexture;

    private Vector3 _nodeSize;

    public MapNodeMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _resources = game.GetService<ResourceService>();
        _transform = actor.GetComponent<Transform>();
        _outlineMaterial = _rendering.MaterialCreate();
        _textureMaterial = _rendering.MaterialCreate();
        _mesh = _rendering.MeshCreate();
        _rt = _rendering.WorldGetRenderTarget(_rendering.InstanceGetWorld(actor.InstanceRid));
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
    }

    public override void LoadContent(IContentManager content)
    {
        _rendering.MaterialAssignEffect(_outlineMaterial, _rendering.BasicEffect);
        _rendering.MaterialSetAlbedo(_outlineMaterial, Color.White);
        _rendering.MaterialSetCullMode(_outlineMaterial, CullMode.CullClockwiseFace);

        var effect = content.Load<Effect>("Effects/MapNodeMesh");
        _rendering.MaterialAssignEffect(_textureMaterial, effect);

        _defaultTexture = content.Load<Texture2D>("MapScreens/missing");
    }

    public void Visualize(MapNode node)
    {
        var texturePath = TexturePath + node.LevelName;
        var textureFile = _resources.Files
            .FirstOrDefault(f => f.StartsWith(texturePath, StringComparison.OrdinalIgnoreCase));

        if (_nodeTexture != _defaultTexture)
        {
            _nodeTexture?.Dispose();
        }

        _nodeTexture = _defaultTexture!;

        if (!string.IsNullOrEmpty(textureFile))
        {
            var rTexture = (RTexture2D)_resources.Load(textureFile);
            _nodeTexture = RepackerExtensions.ConvertToTexture2D(rTexture);
        }

        _rendering.MaterialAssignBaseTexture(_textureMaterial, _nodeTexture);
        _rendering.MaterialSetSamplerState(_textureMaterial, PointMipClamp);
        _rendering.MaterialShaderSetParam(_textureMaterial, "TextureSize",
            new Vector2(_nodeTexture.Width, _nodeTexture.Height));
        _rendering.MaterialShaderSetParam(_textureMaterial, "CubeOffset", _transform.Position);

        _nodeSize = Vector3.One * node.NodeType.GetSizeFactor();
        var outlineBox = MeshSurface.CreateBox(_nodeSize * OutlineSize);
        var textureBox = MeshSurface.CreateBox(_nodeSize);

        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, outlineBox, _outlineMaterial);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, textureBox, _textureMaterial);
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        var half = _nodeSize / 2f;
        var pos = _transform.Position;
        yield return new BoundingBox(pos - half, pos + half);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        var dist = ray.Intersects(box);
        return dist.HasValue ? new PickHit(dist.Value, 0) : null;
    }

    public override void Update(GameTime gameTime)
    {
        var (width, height) = _rendering.RenderTargetGetSize(_rt);
        var pixelsPerTrixel = (Camera?.Size ?? FallbackSize) / 45f * 18f;
        _rendering.MaterialShaderSetParam(_textureMaterial, "PixelsPerTrixel", pixelsPerTrixel);
        _rendering.MaterialShaderSetParam(_textureMaterial, "ViewportSize", new Vector2(width, height));
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_nodeTexture != _defaultTexture)
        {
            _nodeTexture?.Dispose();
        }

        _rendering.FreeRid(_textureMaterial);
        _rendering.FreeRid(_outlineMaterial);
        _rendering.FreeRid(_mesh);
    }
}