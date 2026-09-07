using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class HistoryWindow : DrawableGameComponent
{
    private readonly EditorService _editorService;

    private bool _isOpen;

    public bool IsOpen => _isOpen;

    public HistoryWindow(Game game) : base(game)
    {
        _editorService = game.GetService<EditorService>();
    }

    public void Toggle()
    {
        _isOpen = !_isOpen;
    }

    public override void Draw(GameTime gameTime)
    {
        if (!_isOpen)
        {
            return;
        }

        ImGuiX.SetNextWindowSize(new Vector2(360, 400), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("History", ref _isOpen))
        {
            ImGui.End();
            return;
        }

        var editor = _editorService.ActiveEditor;
        if (editor == null)
        {
            ImGui.TextDisabled("No active editor.");
        }
        else
        {
            ImGui.TextDisabled(editor.Title);
            ImGui.Separator();

            foreach (var entry in editor.History.GetEntries())
            {
                var state = entry.State;
                var label = state.HasFlag(History.EntryState.Current)
                    ? "Current"
                    : state.HasFlag(History.EntryState.Redo)
                        ? "Redo"
                        : "Undo";

                if (state.HasFlag(History.EntryState.Saved))
                {
                    label += ", Saved";
                }

                var timestamp = entry.Timestamp.ToLocalTime().ToString("HH:mm:ss");
                if (ImGui.Selectable($"{timestamp}  {entry.Name}", state.HasFlag(History.EntryState.Current)))
                {
                    editor.History.JumpToEntry(entry.Index);
                }

                ImGui.SameLine();
                ImGui.TextDisabled(label);
            }
        }

        ImGui.End();
    }
}