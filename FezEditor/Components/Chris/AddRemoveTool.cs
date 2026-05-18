using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class AddRemoveTool : BaseTool
{
    private LmbState _lmb;

    public AddRemoveTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB / Drag", Chris.CurrentTool == ChrisTool.Add ? "Add" : "Remove")
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
            if (_lmb == LmbState.Pressed && Chris is { IsViewportHovered: true, Hit: not null } ||
                _lmb == LmbState.Dragging && Chris.SelectedFaces.Count > 0)
            {
                ApplyChanges();
            }

            _lmb = LmbState.Idle;
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool is ChrisTool.Add or ChrisTool.Remove;
    }

    private void ApplyChanges()
    {
        if (Chris.CurrentTool == ChrisTool.Add)
        {
            var toAdd = Chris.SelectedFaces
                .Select(face => face.Emplacement + new Vector3I(face.Face.AsVector()))
                .Where(emp => Chris.Obj.SizeContains(emp))
                .ToList();

            if (toAdd.Count > 0)
            {
                using (Chris.History.BeginScope("Add Trixels"))
                {
                    foreach (var emp in toAdd)
                    {
                        Chris.Obj.SetMissing(emp, false);
                    }
                }
            }
        }
        else
        {
            var toRemove = Chris.SelectedFaces.Select(tf => tf.Emplacement).ToHashSet();
            var totalTrixels = Chris.Obj.VisibleFaces.Select(tf => tf.Emplacement).Distinct().Count();
            if (totalTrixels - toRemove.Count < 1)
            {
                toRemove.Remove(toRemove.First());
            }

            if (toRemove.Count > 0)
            {
                using (Chris.History.BeginScope("Remove Trixels"))
                {
                    foreach (var emp in toRemove)
                    {
                        Chris.Obj.SetMissing(emp, true);
                    }
                }
            }
        }

        Chris.SelectedFaces.Clear();
        Chris.Trixels.Visualize(Chris.Obj);
    }
}