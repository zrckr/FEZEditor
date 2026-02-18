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

    private InputService _input = null!;

    private Transform _transform = null!;

    public override void Initialize()
    {
        _input = Game.GetService<InputService>();
        _transform = Actor.GetComponent<Transform>();
    }

    public override void Update(GameTime gameTime)
    {
        var scroll = _input.GetScrollWheelDelta();
        if (scroll != 0)
        {
            Distance -= scroll * ZoomSensitivity;
            Distance = MathHelper.Clamp(Distance, MinDistance, MaxDistance);
        }
        
        _transform.Position = new Vector3(0f, 0f, Distance);
    }
}
