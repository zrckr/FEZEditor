using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrimitiveType = Microsoft.Xna.Framework.Graphics.PrimitiveType;

namespace FezEditor.Actors;

public class BackgroundPlaneMesh : ActorComponent, IPickable
{
    private const float DepthBiasOrthographic = -1e-7f;

    private const float SlopeScaleDepthBias = -0.1f;

    private const float PerspectiveDividend = -0.0001f;

    public Vector3 PlaneSize { get; private set; }

    public Camera Camera { get; set; } = null!;

    public bool Animated { get; private set; }

    public bool DoubleSided { get; set; }

    public bool Billboard { get; set; }

    public Color Color { get; set; } = Color.White;

    public float Opacity { get; set; } = 1.0f;

    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _camera;

    private readonly Transform _transform;

    private List<FrameContent> _frames = [];

    private Texture2D? _texture;

    private TimeSpan _frameElapsed = TimeSpan.Zero;

    private int _frameCounter;

    internal BackgroundPlaneMesh(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _mesh = _rendering.MeshCreate();
        _material = _rendering.MaterialCreate();
        _camera = _rendering.WorldGetCamera(_rendering.InstanceGetWorld(actor.InstanceRid));
        _rendering.InstanceSetMesh(actor.InstanceRid, _mesh);
        _transform = actor.GetComponent<Transform>();
    }

    public override void LoadContent(IContentManager content)
    {
        var effect = content.Load<Effect>("Effects/SpritePlaneMesh");
        _rendering.MaterialAssignEffect(_material, effect);
    }

    public void Visualize(object asset)
    {
        _texture?.Dispose();
        switch (asset)
        {
            case RAnimatedTexture animatedTexture:
                _texture = RepackerExtensions.ConvertToTexture2D(animatedTexture);
                _frames = animatedTexture.Frames;
                PlaneSize = new Vector3(animatedTexture.FrameWidth, animatedTexture.FrameHeight, 2f) * Mathz.TrixelSize;
                break;

            case RTexture2D texture:
                _texture = RepackerExtensions.ConvertToTexture2D(texture);
                _frames = new List<FrameContent>();
                PlaneSize = new Vector3(texture.Width, texture.Height, 2f) * Mathz.TrixelSize;
                break;

            default:
                throw new NotSupportedException("Unsupported asset type: " + asset.GetType());
        }

        _rendering.MaterialAssignBaseTexture(_material, _texture);
        _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);

        var surface = MeshSurface.CreateQuad(PlaneSize);
        _rendering.MeshClear(_mesh);
        _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);

        _frameCounter = 0;
        Animated = _frames.Count > 0;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _texture?.Dispose();
        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        yield return Mathz.ComputeBoundingBox(_transform.Position, _transform.Rotation, _transform.Scale, PlaneSize);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        var dist = ray.Intersects(box);
        return dist.HasValue ? new PickHit(dist.Value, 0) : null;
    }

    public override void Update(GameTime gameTime)
    {
        if (Animated)
        {
            var currentFrame = _frames[_frameCounter];
            _frameElapsed += gameTime.ElapsedGameTime;

            if (_frameElapsed >= currentFrame.Duration)
            {
                var textureSize = new Vector2(_texture!.Width, _texture!.Height);
                var transform = Mathz.CreateTextureTransform(currentFrame.Rectangle.ToXna(), textureSize);
                _rendering.MaterialSetTextureTransform(_material, transform);
                _frameCounter = (_frameCounter + 1) % _frames.Count;
                _frameElapsed = TimeSpan.Zero;
            }
        }

        if (Billboard)
        {
            var viewMatrix = _rendering.CameraGetView(_camera);
            _transform.Rotation = Mathz.CreateYBillboard(viewMatrix, _transform.Position);
        }

        var depthBias = DepthBiasOrthographic;
        if (Camera.Projection == Camera.ProjectionType.Perspective)
        {
            depthBias = PerspectiveDividend / (Camera.Far - Camera.Near);
        }

        _rendering.MaterialSetDepthBias(_material, depthBias, SlopeScaleDepthBias);
        _rendering.MaterialSetAlbedo(_material, Color * Opacity);
        _rendering.MaterialShaderSetParam(_material, "DoubleSided", DoubleSided ? 1f : 0f);
        _rendering.MaterialSetCullMode(_material, DoubleSided ? CullMode.None : CullMode.CullClockwiseFace);
    }
}