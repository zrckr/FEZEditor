using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class OrbitControl : ActorComponent
{
    public float MouseSensitivity { get; set; } = 0.005f;

    private float _yaw;

    private float _pitch;

    private InputService _input = null!;

    private Transform _transform = null!;

    public override void Initialize()
    {
        _input = Game.GetService<InputService>();
        _transform = Actor.GetComponent<Transform>();
    }

    public override void Update(GameTime gameTime)
    {
        _input.CaptureMouse(false);
        if (_input.IsMiddleMousePressed())
        {
            var delta = _input.GetMouseDelta();
            _yaw -= delta.X * MouseSensitivity;
            _pitch -= delta.Y * MouseSensitivity;
            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
            _input.CaptureMouse(true);
        }

        var yawQ = Quaternion.CreateFromAxisAngle(Vector3.Up, _yaw);
        var pitchQ = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        _transform.Rotation = yawQ * pitchQ;
    }
}
