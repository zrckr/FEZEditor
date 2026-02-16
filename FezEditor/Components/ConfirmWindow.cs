using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class ConfirmWindow : DrawableGameComponent
{
    public Dirty<string> Title { get; set; } = new("Alert!");

    public Dirty<string> Text { get; set; } = new("Are you sure?");

    public Dirty<string> ConfirmButtonText { get; set; } = new("Yes");

    public Dirty<string> CancelButtonText { get; set; } = new("No");

    public Action? Canceled { get; set; }

    public Action? Confirmed { get; set; }

    private bool _isDirty;

    private readonly int _popupId = Random.Shared.Next();

    public ConfirmWindow(Game game) : base(game)
    {
    }

    public void ForceToShow()
    {
        _isDirty = true;
    }

    private bool IsDirty()
    {
        return _isDirty ||
               Title.IsDirty ||
               Text.IsDirty ||
               ConfirmButtonText.IsDirty ||
               CancelButtonText.IsDirty;
    }

    private void Clear()
    {
        _isDirty = false;
        Title = Title.Clean();
        Text = Text.Clean();
        ConfirmButtonText = ConfirmButtonText.Clean();
        CancelButtonText = CancelButtonText.Clean();
    }

    public override void Draw(GameTime gameTime)
    {
        var strId = $"{Title.Value}##DialogWindow_{_popupId}";
        if (IsDirty())
        {
            if (string.IsNullOrEmpty(Text))
            {
                throw new ArgumentException("Dialog text is empty");
            }

            if (string.IsNullOrEmpty(ConfirmButtonText))
            {
                throw new ArgumentException("Ok button text is empty");
            }

            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup(strId);
            Clear();
        }

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize;
        if (string.IsNullOrEmpty(Title))
        {
            flags |= ImGuiWindowFlags.NoTitleBar;
        }
        
        ImGuiX.SetNextWindowSize(new Vector2(320, 0));
        if (ImGui.BeginPopupModal(strId, flags))
        {
            ImGui.Text(Text);
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            
            if (ImGui.Button(ConfirmButtonText) || ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                Confirmed?.Invoke();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (!string.IsNullOrEmpty(CancelButtonText))
            {
                if (ImGui.Button(CancelButtonText) || ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    Canceled?.Invoke();
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.EndPopup();
        }
    }
}