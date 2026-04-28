using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

[UsedImplicitly]
public class AboutWindow : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private bool _isOpen = true;

    public AboutWindow(Game game) : base(game)
    {
        DrawOrder = 1000;
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Icon");
    }

    public override void Update(GameTime gameTime)
    {
        if (!_isOpen)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        ImGui.SetNextWindowCollapsed(!_isOpen, ImGuiCond.FirstUseEver);
        ImGuiX.SetNextWindowCentered();

        const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse |
                                       ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove;

        if (ImGui.Begin(nameof(AboutWindow), ref _isOpen, flags))
        {
            ImGuiX.Image(_logoTexture, new Vector2(64, 64));
            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.NewLine();
            ImGui.Text($"FEZEditor, version {FezEditor.Version}");
            ImGui.Text($"Developed by {FezEditor.Authors}");
            ImGui.Text("Powered by ");
            ImGui.SameLine(0, 0);
            ImGuiX.Hyperlink("FNA", "https://github.com/FNA-XNA/FNA");
            ImGui.SameLine(0, 0);
            ImGui.Text(" and ");
            ImGui.SameLine(0, 0);
            ImGuiX.Hyperlink("FEZRepacker", "https://github.com/FEZModding/FEZRepacker");
            ImGui.EndGroup();
            ImGui.NewLine();

            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - 80) / 2);
            if (ImGuiX.Button("OK", new Vector2(80, 0)))
            {
                _isOpen = false;
            }
        }

        ImGui.End();
    }
}