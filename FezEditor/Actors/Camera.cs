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

    public float FieldOfView { get; set; } = 75.0f;

    public float Size { get; set; } = 1.0f;

    public float Near { get; set; } = 0.05f;
    
    public float Far { get; set; } = 1000.0f;

    public float AspectRatio { get; set; } = 1.0f;

    private RenderingService _rendering = null!;

    private Rid _camera;

    private Rid _world;
    
    public override void Initialize()
    {
        _rendering = Game.GetService<RenderingService>();
        _camera = _rendering.CameraCreate();
        _world = _rendering.InstanceGetWorld(Actor.InstanceRid);
        _rendering.WorldSetCamera(_world, _camera);
    }

    public override void Update(GameTime gameTime)
    {
        var world = _rendering.InstanceGetWorldMatrix(Actor.InstanceRid);
        var viewMatrix = Matrix.CreateLookAt(world.Translation, world.Translation + world.Forward, world.Up);
        var projectionMatrix = Projection switch
        {
            ProjectionType.Perspective => Matrix
                .CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfView), AspectRatio, Near, Far),
            
            ProjectionType.Orthographic => Matrix
                .CreateOrthographic(AspectRatio * Size, Size, Near, Far),
            
            _ => Matrix.Identity
        };

        _rendering.CameraSetView(_camera, viewMatrix);
        _rendering.CameraSetProjection(_camera, projectionMatrix);
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_camera);
    }
}