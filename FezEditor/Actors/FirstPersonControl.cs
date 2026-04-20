using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class FirstPersonControl : ActorComponent
{
    public float MovementSpeed { get; set; } = 8.0f;

    public float MouseSensitivity { get; set; } = 0.002f;

    private float _yaw;

    private float _pitch;

    private TimeSpan _ctrlPenalty;

    private readonly InputService _input;

    private readonly StatusService _status;

    private readonly Transform _transform;

    internal FirstPersonControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _status = game.GetService<StatusService>();
        _transform = actor.GetComponent<Transform>();
    }

    public void FocusOn(Vector3 target, Vector3 approachDirection, float distance)
    {
        var lookDir = -approachDirection;
        _yaw = MathF.Atan2(-lookDir.X, -lookDir.Z);
        _pitch = MathF.Asin(MathHelper.Clamp(lookDir.Y, -1f, 1f));
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
        _transform.Position = target + approachDirection * distance;
    }

    public override void Update(GameTime gameTime)
    {
        #region Update Input Hints

        var f = _input.GetActionBinding(InputActions.MoveForward);
        var l = _input.GetActionBinding(InputActions.MoveLeft);
        var b = _input.GetActionBinding(InputActions.MoveBackward);
        var r = _input.GetActionBinding(InputActions.MoveRight);
        _status.AddHints(
            (f+l+b+r, "Movement"),
            ("RMB", "Look around")
        );

        #endregion

        #region Handle mouse input

        if (_input.CaptureRightMouseDelta(out var delta))
        {
            _yaw -= delta.X * MouseSensitivity;
            _pitch -= delta.Y * MouseSensitivity;
            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
        }

        #endregion

        #region Handle key input

        if (ImGuiNET.ImGui.GetIO().KeyCtrl)
        {
            _ctrlPenalty = TimeSpan.FromMilliseconds(500);
        }
        else if (_ctrlPenalty > TimeSpan.Zero)
        {
            _ctrlPenalty -= gameTime.ElapsedGameTime;
        }

        var inputDirection = _ctrlPenalty > TimeSpan.Zero
            ? Vector2.Zero
            : _input.GetActionsVector(
                InputActions.MoveLeft,
                InputActions.MoveRight,
                InputActions.MoveBackward,
                InputActions.MoveForward
            );

        var rotation = _transform.Rotation;
        var forward = Vector3.Transform(Vector3.Forward, rotation);
        var right = Vector3.Transform(Vector3.Right, rotation);
        if (forward.LengthSquared() > 0)
        {
            forward.Normalize();
        }

        if (right.LengthSquared() > 0)
        {
            right.Normalize();
        }

        var direction = (forward * inputDirection.Y) + (right * inputDirection.X);

        #endregion

        #region Apply movement

        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _transform.Position += direction * MovementSpeed * deltaTime;

        #endregion

        #region Update rotation

        var yawQuaternion = Quaternion.CreateFromAxisAngle(Vector3.Up, _yaw);
        var pitchQuaternion = Quaternion.CreateFromAxisAngle(Vector3.Right, _pitch);
        _transform.Rotation = yawQuaternion * pitchQuaternion;

        #endregion
    }
}