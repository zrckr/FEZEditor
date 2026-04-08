using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class NpcMesh : ActorComponent, IPickable
{
    public Dirty<string> CurrentAnimation { get; set; } = new("");

    public bool Pickable { get; set; } = true;

    private readonly RenderingService _rendering;

    private readonly Rid _mesh;

    private readonly Rid _material;

    private readonly Rid _camera;

    private readonly Transform _transform;

    private readonly Dictionary<string, AnimationData> _animations = new(StringComparer.OrdinalIgnoreCase);

    private TimeSpan _frameElapsed = TimeSpan.Zero;

    private int _frameCounter;

    internal NpcMesh(Game game, Actor actor) : base(game, actor)
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
        _rendering.MaterialSetCullMode(_material, CullMode.CullClockwiseFace);
    }

    public void Visualize(Dictionary<string, RAnimatedTexture> animations, string? currentAnimation = null)
    {
        foreach (var data in _animations.Values)
        {
            data.Texture.Dispose();
        }

        CurrentAnimation = currentAnimation ?? (animations.ContainsKey("Idle") ? "Idle" : "Walk");
        foreach (var (name, animatedTexture) in animations)
        {
            var texture = RepackerExtensions.ConvertToTexture2D(animatedTexture);
            var scale = new Vector3(animatedTexture.FrameWidth, animatedTexture.FrameHeight, 16f) * Mathz.TrixelSize;
            _animations[name] = new AnimationData(texture, scale, animatedTexture.Frames);
        }
    }

    public IEnumerable<BoundingBox> GetBounds()
    {
        var scale = _animations.TryGetValue(CurrentAnimation.Value, out var data) ? data.Scale : Vector3.One;
        var offset = Vector3.Transform(Vector3.UnitY * scale.Y / 2f, _transform.Rotation);
        yield return Mathz.ComputeBoundingBox(_transform.Position + offset, _transform.Rotation, scale, Vector3.One);
    }

    public PickHit? Pick(Ray ray)
    {
        var box = GetBounds().First();
        var dist = ray.Intersects(box);
        return dist.HasValue ? new PickHit(dist.Value, 0) : null;
    }

    public override void Update(GameTime gameTime)
    {
        if (!_animations.TryGetValue(CurrentAnimation.Value, out var data))
        {
            return;
        }

        if (CurrentAnimation.IsDirty)
        {
            _rendering.MaterialAssignBaseTexture(_material, data.Texture);
            _rendering.MaterialSetBlendMode(_material, BlendMode.AlphaBlend);

            var surface = MeshSurface.CreateQuad(Vector3.One, Vector3.UnitY / 2f);
            _rendering.MeshClear(_mesh);
            _rendering.MeshAddSurface(_mesh, PrimitiveType.TriangleList, surface, _material);

            _frameCounter = 0;
            CurrentAnimation = CurrentAnimation.Clean();
        }

        var currentFrame = data.Frames[_frameCounter];
        _frameElapsed += gameTime.ElapsedGameTime;

        if (_frameElapsed >= currentFrame.Duration)
        {
            var textureSize = new Vector2(data.Texture.Width, data.Texture.Height);
            var transform = Mathz.CreateTextureTransform(currentFrame.Rectangle.ToXna(), textureSize);
            _rendering.MaterialSetTextureTransform(_material, transform);
            _frameCounter = (_frameCounter + 1) % data.Frames.Count;
            _frameElapsed = TimeSpan.Zero;
        }

        var viewMatrix = _rendering.CameraGetView(_camera);
        _transform.Rotation = Mathz.CreateYBillboard(viewMatrix, _transform.Position);
        _transform.Scale = data.Scale;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var data in _animations.Values)
        {
            data.Texture.Dispose();
        }

        _rendering.FreeRid(_mesh);
        _rendering.FreeRid(_material);
    }

    private readonly record struct AnimationData(Texture2D Texture, Vector3 Scale, List<FrameContent> Frames);
}