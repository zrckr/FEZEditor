using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class NotSupportedComponent : EditorComponent
{
    private readonly Type _type;
    
    public NotSupportedComponent(Game game, string title, Type type) : base(game, title)
    {
        _type = type;
    }

    public override void Draw()
    {
        var text = $"{Icons.Warning} There's no editor for {_type.Name} asset!";
        ImGuiX.SetTextCentered(text);
        ImGui.Text(text);
    }
}