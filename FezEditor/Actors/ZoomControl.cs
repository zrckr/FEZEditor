using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class ZoomControl : ActorComponent
{
    public float ZoomSensitivity { get; set; } = 0.001f;

    public float MinDistance { get; set; } = 0.5f;

    public float MaxDistance { get; set; } = 500.0f;

    public float Distance { get; set; } = 10.0f;

    private readonly InputService _input;

    private readonly StatusService _status;

    private readonly Camera _camera;

    internal ZoomControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _status = game.GetService<StatusService>();
        _camera = actor.GetComponent<Camera>();
    }

    public override void Update(GameTime gameTime)
    {
        _status.AddHints(("Scroll Wheel", "Zoom"));
        if (_input.CaptureScrollWheelDelta(out var scroll))
        {
            Distance -= scroll * ZoomSensitivity;
            Distance = MathHelper.Clamp(Distance, MinDistance, MaxDistance);
        }

        _camera.Offset = new Vector3(0f, 0f, Distance);
    }
}