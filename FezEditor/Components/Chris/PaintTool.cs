using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PaintTool : TextureTool
{
    private LmbState _lmb;

    public PaintTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB / Drag", "Paint")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _lmb = Chris is { IsViewportHovered: true, Hit: not null } ? LmbState.Pressed : LmbState.Idle;
        }

        if (_lmb != LmbState.Idle && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && Chris.SelectedFaces.Count > 0)
        {
            _lmb = LmbState.Dragging;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            if (_lmb == LmbState.Pressed && Chris is { IsViewportHovered: true, Hit: not null })
            {
                using (Chris.History.BeginScope("Paint Trixels"))
                {
                    PaintTrixel(Chris.Hit.Value);
                }

                FlushPaintChanges();
            }
            else if (_lmb == LmbState.Dragging && Chris.SelectedFaces.Count > 0)
            {
                using (Chris.History.BeginScope("Paint Trixels"))
                {
                    foreach (var face in Chris.SelectedFaces)
                    {
                        PaintTrixel(face);
                    }

                    Chris.SelectedFaces.Clear();
                }

                FlushPaintChanges();
            }

            _lmb = LmbState.Idle;
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Paint;
    }
}