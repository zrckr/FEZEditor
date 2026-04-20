using FezEditor.Services;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class MapZoomControl : ActorComponent
{
    private static readonly float[] Sizes = [80f, 40f, 20f, 10f, 5f];

    private const float LerpSpeed = 10f;

    private readonly InputService _input;

    private readonly StatusService _status;

    private readonly Camera _camera;

    private int _sizeIndex = 2;

    private float _targetSize = Sizes[2];

    public MapZoomControl(Game game, Actor actor) : base(game, actor)
    {
        _input = game.GetService<InputService>();
        _status = game.GetService<StatusService>();
        _camera = actor.GetComponent<Camera>();
        _camera.Projection = Camera.ProjectionType.Orthographic;
        _camera.Size = _targetSize;
    }


    public void Reset()
    {
        _sizeIndex = 2;
        _targetSize = Sizes[_sizeIndex];
    }

    public override void Update(GameTime gameTime)
    {
        _status.AddHints(("Scroll Wheel", "Cycle Zoom"));
        if (MathF.Abs(_camera.Size - _targetSize) > 0.01f)
        {
            var delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _camera.Size = MathHelper.Lerp(_camera.Size, _targetSize, LerpSpeed * delta);
        }
        else
        {
            _camera.Size = _targetSize;
        }

        if (_input.CaptureScrollWheelDelta(out var scroll))
        {
            _sizeIndex = Math.Clamp(_sizeIndex + Math.Sign(scroll), 0, Sizes.Length - 1);
            _targetSize = Sizes[_sizeIndex];
        }
    }
}