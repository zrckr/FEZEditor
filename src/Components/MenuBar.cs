using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class MenuBar : DrawableGameComponent
{
    private static readonly float[] ScalePresets = new[] { 1.00f, 1.25f, 1.50f, 1.75f, 2.00f };

    private Texture2D _logoTexture = null!;

    private AboutWindow? _aboutWindow;

    private readonly ConfirmWindow _confirmWindow;

    private readonly ReferencesWindow _referencesWindow;

    private readonly HistoryWindow _historyWindow;

    private readonly EditorService _editorService;

    private readonly ResourceService _resourceService;

    private readonly InputService _inputService;

    private readonly AppStorageService _storageService;

    private readonly ImGuiService _imguiService;

    private readonly StatusService _statusService;

    private string? _lastModResolutionNoticeRoot;

    private MainLayout _mainLayout = null!;

    private FileBrowser _fileBrowser = null!;

    public MenuBar(Game game) : base(game)
    {
        game.AddComponent(_confirmWindow = new ConfirmWindow(game));
        game.AddComponent(_referencesWindow = new ReferencesWindow(game));
        game.AddComponent(_historyWindow = new HistoryWindow(game));
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        _inputService = game.GetService<InputService>();
        _storageService = game.GetService<AppStorageService>();
        _imguiService = game.GetService<ImGuiService>();
        _statusService = game.GetService<StatusService>();
        _resourceService.ProviderChanged += OnProviderChanged;
        _resourceService.ModOpenedFirstTime += OnModOpenedFirstTime;
    }

    protected override void Dispose(bool disposing)
    {
        _resourceService.ProviderChanged -= OnProviderChanged;
        _resourceService.ModOpenedFirstTime -= OnModOpenedFirstTime;
        _confirmWindow.Dispose();
        _referencesWindow.Dispose();
        _historyWindow.Dispose();
        _aboutWindow?.Dispose();
    }

    private void OnProviderChanged()
    {
        if (_resourceService.HasNoProvider)
        {
            _lastModResolutionNoticeRoot = null;
            return;
        }

        _storageService.PruneRecentFiles(_resourceService.RootPath, _resourceService.Exists);
        var resolution = _resourceService.ModResolution;
        if (resolution == null || _lastModResolutionNoticeRoot == _resourceService.RootPath)
        {
            return;
        }

        _lastModResolutionNoticeRoot = _resourceService.RootPath;
        if (resolution is ModDirectoryResolution.Redirected)
        {
            ShowStatusNotice("Mod root selected: opened the Assets directory inside the selected mod");
        }
        else if (resolution is ModDirectoryResolution.Created)
        {
            ShowStatusNotice("Assets directory created and opened inside the selected mod");
        }
    }

    private void OnModOpenedFirstTime()
    {
        ShowStatusNotice("Mod assets opened. Manage references via Editor > Manage References");
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Media/Icon");
        _mainLayout = Game.GetComponent<MainLayout>();
        _fileBrowser = Game.GetComponent<FileBrowser>();
    }

    public override void Update(GameTime gameTime)
    {
        if (_inputService.IsActionJustPressed(InputActions.UiClose))
        {
            ShowCloseDialog();
        }
        else if (_inputService.IsActionJustPressed(InputActions.UiQuitToWelcome))
        {
            ShowCloseAllDialog();
        }
        else if (_inputService.IsActionJustPressed(InputActions.UiQuit))
        {
            ShowQuitDialog();
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                if (!_resourceService.HasNoProvider)
                {
                    _storageService.RecentFiles.TryGetValue(_resourceService.RootPath, out var recentFiles);
                    if (ImGui.BeginMenu("Open Recent", recentFiles?.Count > 0))
                    {
                        foreach (var path in recentFiles!.ToArray())
                        {
                            var extension = _resourceService.GetExtension(path);
                            var icon = EditorService.GetFileIcon(extension);
                            if (ImGui.MenuItem($"{icon} {path}"))
                            {
                                _editorService.OpenEditorFor(path);
                            }
                        }

                        ImGui.EndMenu();
                    }
                }

                ImGui.Separator();

                var enabled = _editorService.Flags.HasFlag(EditorFlags.SaveFile);
                var shortcut = _inputService.GetActionBinding(InputActions.UiSave);
                if (ImGui.MenuItem("Save File", shortcut, false, enabled))
                {
                    _editorService.SaveActiveEditorChanges();
                }

                enabled = _editorService.Flags.HasFlag(EditorFlags.CloseFile);
                shortcut = _inputService.GetActionBinding(InputActions.UiSaveAs);
                if (ImGui.MenuItem("Save File As...", shortcut, false, enabled))
                {
                    _editorService.SaveActiveEditorChangesAs();
                }

                enabled = _editorService.Flags.HasFlag(EditorFlags.SaveAll);
                shortcut = _inputService.GetActionBinding(InputActions.UiSaveAll);
                if (ImGui.MenuItem("Save All Files", shortcut, false, enabled))
                {
                    foreach (var editor in _editorService.Editors)
                    {
                        _editorService.SaveEditorChanges(editor);
                    }
                }

                ImGui.Separator();

                enabled = _editorService.Flags.HasFlag(EditorFlags.Undo);
                shortcut = _inputService.GetActionBinding(InputActions.UiUndo);
                if (ImGui.MenuItem("Undo", shortcut, false, enabled))
                {
                    _editorService.UndoActiveEditorChanges();
                }

                enabled = _editorService.Flags.HasFlag(EditorFlags.Redo);
                shortcut = _inputService.GetActionBinding(InputActions.UiRedo);
                if (ImGui.MenuItem("Redo", shortcut, false, enabled))
                {
                    _editorService.RedoActiveEditorChanges();
                }

                ImGui.Separator();

                enabled = _editorService.Flags.HasFlag(EditorFlags.CloseFile);
                shortcut = _inputService.GetActionBinding(InputActions.UiClose);
                if (ImGui.MenuItem("Close File", shortcut, false, enabled))
                {
                    ShowCloseDialog();
                }

                enabled = _editorService.Flags.HasFlag(EditorFlags.QuitToWelcome);
                shortcut = _inputService.GetActionBinding(InputActions.UiQuitToWelcome);
                var provider = _resourceService.IsReadonly ? "PAK" : "Directory";
                if (ImGui.MenuItem($"Close {provider}", shortcut, false, enabled))
                {
                    ShowCloseAllDialog();
                }

                shortcut = _inputService.GetActionBinding(InputActions.UiQuit);
                if (ImGui.MenuItem("Quit", shortcut))
                {
                    ShowQuitDialog();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Editor"))
            {
                ImGui.SeparatorText("History");
                if (ImGui.MenuItem("History", null, _historyWindow.IsOpen))
                {
                    _historyWindow.Toggle();
                }

                if (_resourceService.GetModReferencePaths().Count > 0)
                {
                    ImGui.SeparatorText("Mod");
                    if (ImGui.MenuItem("Manage references"))
                    {
                        _referencesWindow.Show();
                    }
                }

                ImGui.SeparatorText("HAT");
                if (ImGui.MenuItem("Locate HAT launcher..."))
                {
                    FileDialog.Show(FileDialog.Type.OpenFile, SetHatLauncherPath, new FileDialog.Options
                    {
                        Title = "Locate HAT launcher...",
                        DefaultLocation = _storageService.HatLauncherPath,
                        Filters =
                        [
                            new FileDialog.Filter("Executable files", "exe"),
                            new FileDialog.Filter("All files", "*")
                        ]
                    });
                }

                ImGui.SeparatorText("Thumbnails");
                var hasProvider = !_resourceService.HasNoProvider;
                if (ImGui.MenuItem("Regenerate", null, false, hasProvider))
                {
                    _fileBrowser.RegenerateThumbnails();
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("View"))
            {
                if (ImGui.BeginMenu("Display Scale"))
                {
                    var current = _storageService.DisplayScale;
                    var autoScale = _imguiService.AutoDisplayScale;

                    if (ImGui.MenuItem($"Auto ({autoScale * 100f:F0}%)", null, current == null))
                    {
                        if (current != null)
                        {
                            _storageService.DisplayScale = null;
                            _imguiService.SetDisplayScale(autoScale);
                        }
                    }

                    ImGui.Separator();

                    foreach (var preset in ScalePresets)
                    {
                        var label = $"{preset * 100f:F0}%";
                        var selected = current.HasValue && MathF.Abs(current.Value - preset) < 0.01f;
                        if (ImGui.MenuItem(label, null, selected))
                        {
                            if (!selected)
                            {
                                _storageService.DisplayScale = preset;
                                _imguiService.SetDisplayScale(preset);
                            }
                        }
                    }

                    ImGui.EndMenu();
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Rendering Stats", null, _storageService.ShowRenderingStats))
                {
                    _storageService.ShowRenderingStats = !_storageService.ShowRenderingStats;
                }

                if (ImGui.MenuItem("Input Hints", null, _storageService.ShowInputHints))
                {
                    _storageService.ShowInputHints = !_storageService.ShowInputHints;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("File Browser", null, _mainLayout.ShowFileBrowser))
                {
                    _mainLayout.ShowFileBrowser = !_mainLayout.ShowFileBrowser;
                }

                ImGui.EndMenu();
            }

            if (ImGui.BeginMenu("Help"))
            {
                ImGuiX.Image(_logoTexture, new Vector2(16, 16));
                ImGui.SameLine();
                if (ImGui.MenuItem("About FEZEditor..."))
                {
                    ShowAboutWindow();
                }

                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
    }

    private void ShowAboutWindow()
    {
        if (_aboutWindow == null)
        {
            _aboutWindow = new AboutWindow(Game);
            _aboutWindow.Disposed += (_, _) => { _aboutWindow = null; };
            Game.AddComponent(_aboutWindow);
        }
    }

    private void ShowCloseDialog()
    {
        if (!_editorService.HasAnyEditorUnsavedChanges())
        {
            _editorService.CloseActiveEditor();
            return;
        }

        _confirmWindow.Text = "You have unsaved changes. Close the file?";
        _confirmWindow.Title = "Confirm Closing";
        _confirmWindow.ConfirmButtonText = "Yes";
        _confirmWindow.DenyButtonText = "No";
        _confirmWindow.Confirmed = () => _editorService.CloseActiveEditor();
        _confirmWindow.Denied = null;
        _confirmWindow.Closed = null;
    }

    private void ShowCloseAllDialog()
    {
        if (!_editorService.HasAnyEditorUnsavedChanges())
        {
            _resourceService.CloseProvider();
            _editorService.CloseAllEditors();
            _editorService.OpenEditor(new WelcomeSplash(Game));
            return;
        }

        _confirmWindow.Text = "You have unsaved changes. Close all files?";
        _confirmWindow.Title = "Confirm Closing All";
        _confirmWindow.ConfirmButtonText = "Yes";
        _confirmWindow.DenyButtonText = "No";
        _confirmWindow.Confirmed = () =>
        {
            _resourceService.CloseProvider();
            _editorService.CloseAllEditors();
            _editorService.OpenEditor(new WelcomeSplash(Game));
        };
        _confirmWindow.Denied = null;
        _confirmWindow.Closed = null;
    }

    private void ShowQuitDialog()
    {
        if (!_editorService.HasAnyEditorUnsavedChanges())
        {
            Game.Exit();
            return;
        }

        _confirmWindow.Text = "You have unsaved changes. Quit the editor?";
        _confirmWindow.Title = "Confirm Quitting";
        _confirmWindow.ConfirmButtonText = "Yes";
        _confirmWindow.DenyButtonText = "No";
        _confirmWindow.Confirmed = () => Game.Exit();
        _confirmWindow.Denied = null;
        _confirmWindow.Closed = null;
    }

    private void SetHatLauncherPath(string[] files)
    {
        var path = files.FirstOrDefault();
        if (!string.IsNullOrEmpty(path))
        {
            _storageService.HatLauncherPath = path;
        }
    }

    private void ShowStatusNotice(string text)
    {
        _statusService.ShowMessage(text, TimeSpan.FromSeconds(5));
    }
}