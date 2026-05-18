using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class PaintTool : TextureTool
{
    public PaintTool(Game game, IChrisEditor chris) : base(game, chris)
    {
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

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            _paintScope ??= Chris.History.BeginScope("Paint Trixels");
            PaintTrixel(Chris.Hit.Value);
            FlushPaintChanges();
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
}