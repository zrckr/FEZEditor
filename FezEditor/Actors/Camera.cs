using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Camera : ActorComponent
{
    public enum ProjectionType
    {
        Perspective,
        Orthographic
    }

    public ProjectionType Projection { get; set; } = ProjectionType.Perspective;

    public float FieldOfView
    {
        get => _fieldOfView;
        set
        {
            if (Projection != ProjectionType.Perspective)
            {
                throw new ArgumentException("Projection type must be perspective!");
            }

            _fieldOfView = value;
        }
    }

    public float Size
    {
        get => _size;
        set
        {
            if (Projection != ProjectionType.Orthographic)
            {
                throw new ArgumentException("Projection type must be orthographic!");
            }

            _size = value;
        }
    }

    public Matrix View => _rendering.CameraGetView(_camera);

    public Matrix ProjectionMatrix => _rendering.CameraGetProjection(_camera);

    public Matrix ViewProjection => View * ProjectionMatrix;

    public Matrix InverseView { get; private set; } = Matrix.Identity;

    public Vector3 Position { get; private set; }

    public Vector3 Offset { get; set; } = Vector3.Zero;

    public float Near { get; set; } = 0.05f;

    public float Far { get; set; } = 1000.0f;

    private readonly RenderingService _rendering;

    private readonly Rid _camera;

    private readonly Rid _rt;

    private float _size = 1.0f;

    private float _fieldOfView = 75.0f;

    internal Camera(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        var world = _rendering.InstanceGetWorld(actor.InstanceRid);
        if (_rendering.WorldHasCamera(world))
        {
            throw new InvalidOperationException("A single camera was already initialized!");
        }

        _camera = _rendering.CameraCreate();
        _rendering.WorldSetCamera(world, _camera);
        _rt = _rendering.WorldGetRenderTarget(world);
    }

    public override void Update(GameTime gameTime)
    {
        var world = _rendering.InstanceGetWorldMatrix(Actor.InstanceRid);
        Position = Vector3.Transform(Offset, world);
        var viewMatrix = Matrix.CreateLookAt(Position, Position + world.Forward, world.Up);
        InverseView = Matrix.Invert(viewMatrix);

        var (width, height) = _rendering.RenderTargetGetSize(_rt);
        var aspectRatio = (float)width / height;
        var projectionMatrix = Projection switch
        {
            ProjectionType.Perspective => Matrix
                .CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfView), aspectRatio, Near, Far),

            ProjectionType.Orthographic => Matrix
                .CreateOrthographic(aspectRatio * Size, Size, Near, Far),

            _ => Matrix.Identity
        };

        _rendering.CameraSetView(_camera, viewMatrix);
        _rendering.CameraSetProjection(_camera, projectionMatrix);
    }

    public Vector3 Project(Vector3 position, Vector2 viewport)
    {
        var clip = Vector4.Transform(new Vector4(position, 1f), ViewProjection);
        if (MathF.Abs(clip.W) > float.Epsilon)
        {
            clip.X /= clip.W;
            clip.Y /= clip.W;
            clip.Z /= clip.W;
        }

        var (width, height) = _rendering.RenderTargetGetSize(_rt);
        var screenX = ((clip.X + 1f) * 0.5f * width) + viewport.X;
        var screenY = ((-clip.Y + 1f) * 0.5f * height) + viewport.Y;

        return new Vector3(screenX, screenY, clip.Z);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_camera);
    }
}