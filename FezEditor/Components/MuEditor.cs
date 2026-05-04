using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.NpcMetadata;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class MuEditor : EditorComponent
{
    private const float MaxWidth = 480f;

    private const float TopPadding = 300f;

    public override object Asset => _npcMetadata;

    private readonly EditorService _editorService;

    private readonly NpcMetadata _npcMetadata;

    public MuEditor(Game game, string title, NpcMetadata npcMetadata) : base(game, title)
    {
        _editorService = game.GetService<EditorService>();
        _npcMetadata = npcMetadata;
        History.Track(npcMetadata);
    }

    public override void Draw()
    {
        var avail = ImGui.GetContentRegionAvail();
        var width = MathHelper.Min(avail.X, MaxWidth);
        ImGui.SetCursorPosX((avail.X - width) * 0.5f);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + TopPadding);

        if (ImGuiX.BeginChild("##NpcProperties", new Vector2(width, 0)))
        {
            ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

            var walkSpeed = _npcMetadata.WalkSpeed;
            if (ImGui.DragFloat("Walk Speed", ref walkSpeed, 0.01f))
            {
                using (History.BeginScope("Edit Walk Speed"))
                {
                    _npcMetadata.WalkSpeed = walkSpeed;
                }
            }

            var avoidsGomez = _npcMetadata.AvoidsGomez;
            if (ImGui.Checkbox("Avoids Gomez", ref avoidsGomez))
            {
                using (History.BeginScope("Edit Avoids Gomez"))
                {
                    _npcMetadata.AvoidsGomez = avoidsGomez;
                }
            }

            var soundPath = _npcMetadata.SoundPath;
            if (ImGui.InputText("Sound Path", ref soundPath, 255))
            {
                using (History.BeginScope("Edit Sound Path"))
                {
                    _npcMetadata.SoundPath = soundPath;
                }
            }

            ImGui.SameLine();
            ImGui.BeginDisabled(string.IsNullOrEmpty(_npcMetadata.SoundPath));
            if (ImGui.Button($"{Lucide.Folder}"))
            {
                _editorService.OpenEditorFor($"Sounds/{_npcMetadata.SoundPath}");
            }

            ImGui.EndDisabled();

            var soundActions = _npcMetadata.SoundActions;
            if (ImGuiX.EditableList("Sound Actions", ref soundActions, RenderNpcAction, () => NpcAction.None))
            {
                using (History.BeginScope("Edit Sound Actions"))
                {
                    _npcMetadata.SoundActions = soundActions;
                }
            }

            ImGui.PopStyleVar();
            ImGui.EndChild();
        }
    }

    private static bool RenderNpcAction(int index, ref NpcAction item)
    {
        var action = (int)item;
        var actions = Enum.GetNames<NpcAction>();
        if (ImGui.Combo("##item", ref action, actions, actions.Length))
        {
            item = (NpcAction)action;
            return true;
        }

        return false;
    }

    public static NpcMetadata Create()
    {
        return new NpcMetadata();
    }
}
