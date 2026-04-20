using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class OrbitControl : ActorComponent
{
    private const float MouseSensitivity = 0.005f;

    public float Yaw { get; set; } = 0f;

    public float Pitch
    {
        get => _pitch;
        set => _pitch = MathHelper.Clamp(value, PitchClamp.X + 0.01f, PitchClamp.Y - 0.01f);
    }

    public Vector2 PitchClamp { get; set; } = new Vector2(-1f, 1f) * MathHelper.PiOver2;

    private readonly InputService _input;

    private readonly StatusService _status;

    private readonly Transform _transform;

    private float _pitch;

    internal OrbitControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _status = game.GetService<StatusService>();
        _transform = actor.GetComponent<Transform>();
    }

    public override void Update(GameTime gameTime)
    {
        _status.AddHints(("MMB", "Orbit"));
        if (_input.CaptureMiddleMouseDelta(out var delta))
        {
            Yaw -= delta.X * MouseSensitivity;
            Pitch -= delta.Y * MouseSensitivity;
        }

        _transform.Rotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
    }
}