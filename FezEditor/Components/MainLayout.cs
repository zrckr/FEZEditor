using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class MainLayout : DrawableGameComponent
{
    private const float DefaultLeftPaneWidth = 250f;

    private readonly EditorService _editorService;

    private readonly FileBrowser _fileBrowser;

    private readonly StatusBar _statusBar;

    private readonly ConfirmWindow _confirm;

    public MainLayout(Game game) : base(game)
    {
        _editorService = Game.GetService<EditorService>();
        _fileBrowser = Game.GetComponent<FileBrowser>();
        _statusBar = Game.GetComponent<StatusBar>();
        Game.AddComponent(_confirm = new ConfirmWindow(game));
        DrawOrder = -1;
    }

    protected override void Dispose(bool disposing)
    {
        Game.RemoveComponent(_confirm);
    }

    public override void Update(GameTime gameTime)
    {
        _editorService.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // Clear previously closed editors
        _editorService.FlushPendingCloses();
        
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
                    _fileBrowser.Draw();
                    ImGui.EndChild();
                    ImGui.SameLine();
                }

                // Right pane - Editor tabs
                ImGuiX.BeginChild("RightPane", new Vector2(0, -statusBarHeight));
                if (_editorService.Editors.Any())
                {
                    if (ImGui.BeginTabBar("##EditorTabs"))
                    {
                        foreach (var editor in _editorService.Editors)
                        {
                            var title = editor.Title;
                            if (_editorService.HasEditorUnsavedChanges(editor))
                            {
                                title = "(*) " + title;
                            }
                            
                            var isOpen = true;
                            var beginTabItem = editor is WelcomeComponent
                                ? ImGui.BeginTabItem(title)
                                : ImGui.BeginTabItem(title, ref isOpen);
                            
                            if (beginTabItem)
                            {
                                DrawEditor(editor);
                                ImGui.EndTabItem();
                            }

                            if (!isOpen)
                            {
                                SaveAndCloseEditor(editor);
                            }
                        }

                        ImGui.EndTabBar();
                    }
                }
                else
                {
                    const string text = $"{Icons.ArrowLeft} Open a file from File Browser";
                    ImGuiX.SetTextCentered(text);
                    ImGui.Text(text);
                }
                ImGui.EndChild();
            }

            // Full width, bottom
            ImGui.Separator();
            _statusBar.Draw();
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private void DrawEditor(EditorComponent editor)
    {
        _editorService.MarkEditorActive(editor);
        if (!_editorService.IsEditorLoading(editor))
        {
            editor.Draw();
            return;
        }
        
        var dotCount = ((int)(ImGui.GetTime() * 2) % 4);
        var dots = new string('.', dotCount);
        var text = $"Loading{dots}";
        ImGuiX.SetTextCentered(text);
        ImGui.Text(text);
    }

    private void SaveAndCloseEditor(EditorComponent editor)
    {
        if (!_editorService.HasEditorUnsavedChanges(editor))
        {
            _editorService.CloseEditor(editor);
            return;
        }

        _confirm.Title = "Before closing the editor...";
        _confirm.Text = "Save current changes?";
        _confirm.Confirmed = () =>
        {
            _editorService.SaveEditorChanges(editor);
            _editorService.CloseEditor(editor);
        };
        _confirm.Canceled = () =>
        {
            _editorService.CloseEditor(editor);
        };
    }
}