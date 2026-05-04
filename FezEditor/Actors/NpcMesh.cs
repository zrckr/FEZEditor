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

    public bool Billboard { get; set; } = true;

    public Color Tint { get; set; } = Color.Transparent;

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
        if (!ray.Intersects(box).HasValue)
        {
            return null;
        }

        var scale = _animations.TryGetValue(CurrentAnimation.Value, out var data) ? data.Scale : Vector3.One;
        var hw = scale.X / 2f;
        var h = scale.Y;
        var localRay = _transform.TransformRay(ray);

        // Quad corners in local space (matches CreateQuad with pivot Vector3.UnitY / 2f)
        var v0 = new Vector3(-hw, 0f, 0f);
        var v1 = new Vector3(hw, 0f, 0f);
        var v2 = new Vector3(hw, h, 0f);
        var v3 = new Vector3(-hw, h, 0f);

        var t0 = localRay.IntersectsTriangle(v0, v1, v2);
        var t1 = localRay.IntersectsTriangle(v0, v2, v3);
        var t = t0.HasValue && t1.HasValue ? MathF.Min(t0.Value, t1.Value) : t0 ?? t1;

        return t.HasValue ? new PickHit(t.Value, 0) : null;
    }

    public override void Update(GameTime gameTime)
    {
        _rendering.MaterialShaderSetParam<Vector4>(_material, "Tint", Tint.ToVector4());
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

        if (Billboard)
        {
            var viewMatrix = _rendering.CameraGetView(_camera);
            _transform.Rotation = Mathz.CreateYBillboard(viewMatrix, _transform.Position);
        }

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