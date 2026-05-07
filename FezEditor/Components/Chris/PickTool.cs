using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PickTool : BaseTool
{
    public PickTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool != ChrisTool.Pick)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            Chris.CurrentTool = ChrisTool.Select;
        }
    }

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB", "Pick Color")
        );

        if (!Chris.Hit.HasValue || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) || !Chris.IsViewportHovered)
        {
            return;
        }

        var obj = Chris.Obj;
        var face = Chris.Hit!.Value;
        var (lx, y) = face.Face switch
        {
            FaceOrientation.Front => (face.Emplacement.X, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Right => (obj.Depth - 1 - face.Emplacement.Z, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Back => (obj.Width - 1 - face.Emplacement.X, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Left => (face.Emplacement.Z, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Top => (face.Emplacement.X, face.Emplacement.Z),
            FaceOrientation.Down => (face.Emplacement.X, obj.Depth - 1 - face.Emplacement.Z),
            _ => throw new InvalidOperationException()
        };

        var faceIndex = Array.IndexOf(FaceExtensions.NaturalOrder, face.Face);
        var x = faceIndex * obj.Texture.Width / 6 + lx;
        var idx = (y * obj.Texture.Width + x) * 4;

        var data = obj.Texture.TextureData;
        Chris.PaintColor = new Color(data[idx], data[idx + 1], data[idx + 2], 255);
        Chris.CurrentTool = ChrisTool.Paint;
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Pick;
    }
}