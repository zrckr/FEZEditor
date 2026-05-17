using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PaintTool : BaseTool
{
    public PaintTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool != ChrisTool.Paint)
        {
            return;
        }

        var cancelEsc = ImGui.IsKeyPressed(ImGuiKey.Escape);
        var clickOnEmpty = Chris.IsViewportHovered &&
                           ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !Chris.Hit.HasValue;
        if (cancelEsc || clickOnEmpty)
        {
            Chris.SelectedFaces.Clear();
            Chris.SelectionOrientation = null;
            Chris.CurrentTool = ChrisTool.Select;
        }
    }

    private IDisposable? _paintScope;

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB", "Paint")
        );

        if (!Chris.Hit.HasValue || !Chris.IsViewportHovered)
        {
            return;
        }

        if (Chris.SelectedFaces.Count > 0 &&
            Chris.SelectedFaces.Contains(Chris.Hit.Value) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            using (Chris.History.BeginScope("Paint Trixel Selection"))
            {
                foreach (var face in Chris.SelectedFaces)
                {
                    PaintTrixel(face);
                }
            }

            return;
        }

        if (Chris.SelectedFaces.Count < 1 &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
            ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            _paintScope ??= Chris.History.BeginScope("Paint Single Trixels");
            PaintTrixel(Chris.Hit.Value);
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _paintScope?.Dispose();
            _paintScope = null;
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Paint;
    }

    private void PaintTrixel(TrixelFace face)
    {
        var obj = Chris.Obj;
        var color = Chris.PaintColor;

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

        if (Chris.CurrentPaintMode is PaintMode.Color)
        {
            obj.Texture.TextureData[idx + 0] = color.R;
            obj.Texture.TextureData[idx + 1] = color.G;
            obj.Texture.TextureData[idx + 2] = color.B;
        }
        else if (Chris.CurrentPaintMode is PaintMode.Emission)
        {
            obj.Texture.TextureData[idx + 3] = color.A;
        }

        Chris.Trixels.UpdateTextureDataFrom(obj.Texture);
    }
}