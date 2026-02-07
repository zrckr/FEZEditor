using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

[UsedImplicitly]
public class MainLayout : DrawableGameComponent
{
    private const float DefaultLeftPaneWidth = 250f;

    private FileBrowser? FileBrowser => Game.TryGetComponent<FileBrowser>();
    
    private StatusBar? StatusBar => Game.TryGetComponent<StatusBar>();

    public MainLayout(Game game) : base(game)
    {
        DrawOrder = -1;
    }

    public override void Draw(GameTime gameTime)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(viewport.WorkSize, ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        if (ImGui.Begin("##MainLayout",
                ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoSavedSettings))
        {
            var statusBarHeight = ImGui.GetFrameHeightWithSpacing();

            // Top region: Left pane + Right pane
            {
                // Left pane - File Browser (resizable horizontally)
                {
                    ImGuiX.BeginChild("LeftPane", new Vector2(DefaultLeftPaneWidth, -statusBarHeight), 
                        ImGuiChildFlags.Border | ImGuiChildFlags.ResizeX);
                    FileBrowser?.Draw();
                    ImGui.EndChild();
                    ImGui.SameLine();
                }
                
                // Right pane - Editor tabs
                {
                    ImGuiX.BeginChild("RightPane", new Vector2(0, -statusBarHeight));
                    if (ImGui.BeginTabBar("##EditorTabs"))
                    {
                        if (ImGui.BeginTabItem("Welcome"))
                        {
                            ImGui.TextDisabled("Select a file to edit.");
                            ImGui.EndTabItem();
                        }
                        ImGui.EndTabBar();
                    }
                    ImGui.EndChild();
                }
            }

            // Full width, bottom
            StatusBar?.Draw();
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
    }
}
