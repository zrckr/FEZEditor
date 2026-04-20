using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class MapPanControl : ActorComponent
{
    private const float PanSensitivity = 0.0008f;

    private const float FocusSpeed = 15f;

    public bool Focused => !_focusTarget.HasValue;

    private readonly InputService _input;

    private readonly StatusService _status;

    private readonly Transform _transform;

    private readonly Camera _camera;

    private Vector3? _focusTarget;

    internal MapPanControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _status = game.GetService<StatusService>();
        _transform = actor.GetComponent<Transform>();
        _camera = actor.GetComponent<Camera>();
    }

    public void FocusOn(Vector3 worldPosition)
    {
        _focusTarget = worldPosition;
    }

    public override void Update(GameTime gameTime)
    {
        _status.AddHints(("RMB", "Pan"));
        if (_focusTarget.HasValue)
        {
            var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _transform.Position = Vector3.Lerp(_transform.Position, _focusTarget.Value, FocusSpeed * delta);
            if ((_transform.Position - _focusTarget.Value).LengthSquared() < 0.01f)
            {
                _transform.Position = _focusTarget.Value;
                _focusTarget = null;
            }

            return;
        }

        if (_input.CaptureRightMouseDelta(out var delta1))
        {
            var right = Vector3.Transform(Vector3.Right, _transform.Rotation);
            _transform.Position -= right * delta1.X * PanSensitivity * _camera.Size;
            _transform.Position += Vector3.Up * delta1.Y * PanSensitivity * _camera.Size;
        }
    }
}