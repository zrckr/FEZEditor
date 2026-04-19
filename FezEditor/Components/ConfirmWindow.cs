using FezEditor.Structure;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class ConfirmWindow : DrawableGameComponent
{
    public Dirty<string> Title { get; set; } = new("Alert!");

    public Dirty<string> Text { get; set; } = new("Are you sure?");

    public Dirty<string> ConfirmButtonText { get; set; } = new("Yes");

    public Dirty<string> DenyButtonText { get; set; } = new("No");

    public Dirty<string> CancelButtonText { get; set; } = new("Cancel");

    public Action? Confirmed { get; set; }

    public Action? Denied { get; set; }

    public Action? Closed { get; set; }

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
               DenyButtonText.IsDirty;
    }

    private void Clear()
    {
        _isDirty = false;
        Title = Title.Clean();
        Text = Text.Clean();
        ConfirmButtonText = ConfirmButtonText.Clean();
        DenyButtonText = DenyButtonText.Clean();
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
                ClosePopup();
            }

            var showCancelButton = Denied != null && !string.IsNullOrEmpty(CancelButtonText);

            if (!string.IsNullOrEmpty(DenyButtonText))
            {
                ImGui.SameLine();
                if (ImGui.Button(DenyButtonText) || (!showCancelButton && ImGui.IsKeyPressed(ImGuiKey.Escape)))
                {
                    Denied?.Invoke();
                    ClosePopup();
                }
            }

            if (showCancelButton)
            {
                ImGui.SameLine();
                if (ImGui.Button(CancelButtonText) || ImGui.IsKeyPressed(ImGuiKey.Escape))
                {
                    ClosePopup();
                }
            }

            ImGui.EndPopup();
        }
    }

    private void ClosePopup()
    {
        ImGui.CloseCurrentPopup();
        Closed?.Invoke();
    }
}