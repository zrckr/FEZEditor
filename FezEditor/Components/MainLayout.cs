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

    private readonly StatusService _statusService;

    private readonly FileBrowser _fileBrowser;

    private readonly ConfirmWindow _confirm;

    private bool _loadNextUpdate;

    public MainLayout(Game game) : base(game)
    {
        _editorService = Game.GetService<EditorService>();
        _statusService = Game.GetService<StatusService>();
        _fileBrowser = Game.GetComponent<FileBrowser>();
        Game.AddComponent(_confirm = new ConfirmWindow(game));
        DrawOrder = -1;
    }

    protected override void Dispose(bool disposing)
    {
        _editorService.CloseAllEditors();
        _editorService.FlushPendingCloses();
        Game.RemoveComponent(_confirm);
    }

    public override void Update(GameTime gameTime)
    {
        _editorService.Update(gameTime);
    }

    public override void Draw(GameTime gameTime)
    {
        // Flush loading editors on next frame
        if (_loadNextUpdate)
        {
            _loadNextUpdate = false;
            _editorService.FlushPendingLoads();
        }

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
            // Region: Left pane + Right pane
            {
                // Left pane - File Browser (resizable horizontally)
                {
                    ImGuiX.BeginChild("LeftPane", new Vector2(DefaultLeftPaneWidth, 0),
                        ImGuiChildFlags.Border | ImGuiChildFlags.ResizeX);
                    _fileBrowser.Draw();
                    ImGui.EndChild();
                    ImGui.SameLine();
                }

                // Right pane - Editor tabs + Status bar
                ImGuiX.BeginChild("RightPane", Vector2.Zero);
                {
                    var hasHints = _statusService.Hints.Count > 0;
                    var statusBarHeight = hasHints
                        ? ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y + 1
                        : 0f;

                    ImGuiX.BeginChild("EditorArea", new Vector2(0, -statusBarHeight));
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

                                var tabFlags = _editorService.ShouldFocusEditor(editor)
                                    ? ImGuiTabItemFlags.SetSelected
                                    : ImGuiTabItemFlags.None;

                                var tabLabel = title + "###" + editor.Title;
                                var isOpen = true;
                                var beginTabItem = editor is WelcomeComponent
                                    ? ImGui.BeginTabItem(tabLabel)
                                    : ImGui.BeginTabItem(tabLabel, ref isOpen, tabFlags);

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

                    DrawStatusBar();
                }

                ImGui.EndChild();
            }
        }

        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private void DrawEditor(EditorComponent editor)
    {
        if (!_editorService.IsEditorLoading(editor))
        {
            _editorService.MarkEditorActive(editor);
            editor.Draw();
            return;
        }

        const string text = "Loading...";
        ImGuiX.SetTextCentered(text);
        ImGui.Text(text);
        _loadNextUpdate = true;
    }

    private void DrawStatusBar()
    {
        var hints = _statusService.Hints;
        if (hints.Count == 0)
        {
            return;
        }

        ImGui.Separator();
        if (ImGuiX.BeginChild("StatusBar", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar))
        {
            for (var i = 0; i < hints.Count; i++)
            {
                var (binding, label) = hints[i];
                ImGui.Text(binding + " - " + label);
                if (i < hints.Count - 1)
                {
                    ImGui.SameLine(0, 32);
                    ImGui.TextDisabled("|");
                    ImGui.SameLine(0, 8);
                }
            }
        }

        ImGui.EndChild();
    }

    private bool _confirmPending;

    private void SaveAndCloseEditor(EditorComponent editor)
    {
        if (!_editorService.HasEditorUnsavedChanges(editor))
        {
            _editorService.CloseEditor(editor);
            return;
        }

        if (_confirmPending)
        {
            return;
        }

        _confirmPending = true;
        _confirm.Title = "Before closing the editor...";
        _confirm.Text = "Save current changes?";
        _confirm.Confirmed = () =>
        {
            _editorService.SaveEditorChanges(editor);
            _editorService.CloseEditor(editor);
            _confirmPending = false;
        };
        _confirm.Canceled = () =>
        {
            _editorService.CloseEditor(editor);
            _confirmPending = false;
        };
    }
}