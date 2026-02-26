using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Actors;

public class OrientationGizmo : ActorComponent
{
    private static readonly (FaceOrientation positive, uint color, string xyzPos)[] Axes =
    [
        (FaceOrientation.Right, 0xFF4444FF, "X"),
        (FaceOrientation.Top, 0xFF44FF44, "Y"),
        (FaceOrientation.Front, 0xFFFF4444, "Z"),
    ];
    
    private const float Radius = 40f;

    private const float LineLength = 28f;

    private const float LabelOffset = 10f;

    public bool UseFaceLabels { private get; set; }

    private readonly RenderingService _rendering;

    private readonly Rid _cameraRid;

    public OrientationGizmo(Game game, Actor actor) : base(game, actor)
    {
        _rendering = game.GetService<RenderingService>();
        _cameraRid = _rendering.WorldGetCamera(_rendering.InstanceGetWorld(actor.InstanceRid));
    }

    public void Draw(Vector2 screenCenter)
    {
        var center = screenCenter.ToNumerics() + new NVector2(-Radius, Radius);
        var view = _rendering.CameraGetView(_cameraRid);

        var dl = ImGui.GetWindowDrawList();
        dl.AddCircleFilled(center, Radius, 0x44000000);

        var sorted = Axes
            .Select(a => (a.positive, a.color, a.xyzPos, proj: Project(view, a.positive.AsVector())))
            .OrderBy(a => Vector3.Dot(a.positive.AsVector(), new Vector3(view.M13, view.M23, view.M33)))
            .ToArray();

        foreach (var (positive, color, xyz, proj) in sorted)
        {
            var posTip = center + proj;
            var negTip = center - proj;
            var dimColor = (color & 0x00FFFFFF) | 0x44000000;

            dl.AddLine(center, negTip, dimColor, 1.5f);
            dl.AddLine(center, posTip, color, 2f);
            dl.AddCircleFilled(negTip, 4f, dimColor);
            dl.AddCircleFilled(posTip, 5f, color);

            var projLen = proj.Length();
            if (projLen > 0.001f)
            {
                var dir = proj / projLen;
                var positiveText = UseFaceLabels ? positive.ToString()[..1] : xyz;
                var negativeText = UseFaceLabels ? positive.GetOpposite().ToString()[..1] : "-" + xyz;
                var posTextSize = ImGui.CalcTextSize(positiveText);
                var negTextSize = ImGui.CalcTextSize(negativeText);
                dl.AddText((posTip + dir * LabelOffset - posTextSize * 0.5f), color, positiveText);
                dl.AddText((negTip - dir * LabelOffset - negTextSize * 0.5f), dimColor, negativeText);
            }
        }
    }

    private static NVector2 Project(Matrix view, Vector3 dir)
    {
        var x = Vector3.Dot(dir, new Vector3(view.M11, view.M21, view.M31));
        var y = Vector3.Dot(dir, new Vector3(view.M12, view.M22, view.M32));
        return new NVector2(x, -y) * LineLength;
    }
}