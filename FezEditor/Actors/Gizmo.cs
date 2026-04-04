using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class Gizmo : ActorComponent
{
    public Camera Camera { get; set; } = null!;

    public Vector2 Viewport { get; set; }

    public bool DragStarted { get; private set; }

    public bool DragEnded { get; private set; }

    private readonly RenderingService _rendering;

    private readonly StatusService _status;

    public Gizmo(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _status = game.GetService<StatusService>();
    }

    public bool Translate(ref Vector3 origin)
    {
        _status.AddHints(
            ("LMB Drag", "Move X or Z"),
            ("Alt+LMB Drag", "Move Y"),
            ("Hold Shift", "Snap")
        );

        origin = Vector3.Zero;
        return false;
    }

    public bool Rotate(Vector3 origin, ref Quaternion rotation)
    {
        _status.AddHints(
            ("LMB", "Rotate 90°")
        );

        rotation = Quaternion.Identity;
        return false;
    }

    public bool Scale(Vector3 origin, ref Vector3 scale)
    {
        _status.AddHints(
            ("LMB Drag", "Scale")
        );

        scale = Vector3.One;
        return false;
    }

    public bool ScaleFace(Vector3 origin, FaceOrientation face, out float delta)
    {
        _status.AddHints(
            ("LMB Drag", "Add / Remove Trile")
        );

        delta = 0;
        return false;
    }

    private enum Handle
    {
        Axis,
        Plane,
        Ring,
        Scale,
        Face
    }

    private readonly record struct Hit(
        Handle Handle,
        Vector3 Direction,
        FaceOrientation? Face = null
    );
}