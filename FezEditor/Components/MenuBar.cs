using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class MenuBar : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private AboutWindow? _aboutWindow;

    private readonly ConfirmWindow _confirmWindow;

    private readonly ReferencesWindow _referencesWindow;

    private readonly EditorService _editorService;

    private readonly ResourceService _resourceService;

    private readonly InputService _inputService;

    private readonly AppStorageService _storageService;

    private MainLayout _mainLayout = null!;

    private FileBrowser _fileBrowser = null!;

    public MenuBar(Game game) : base(game)
    {
        game.AddComponent(_confirmWindow = new ConfirmWindow(game));
        game.AddComponent(_referencesWindow = new ReferencesWindow(game));
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        _inputService = game.GetService<InputService>();
        _storageService = game.GetService<AppStorageService>();
        _resourceService.ProviderChanged += OnProviderChanged;
        _resourceService.ModOpenedFirstTime += OnModOpenedFirstTime;
    }

    protected override void Dispose(bool disposing)
    {
        _resourceService.ProviderChanged -= OnProviderChanged;
        _resourceService.ModOpenedFirstTime -= OnModOpenedFirstTime;
        _confirmWindow.Dispose();
        _referencesWindow.Dispose();
        _aboutWindow?.Dispose();
    }

    private void OnProviderChanged()
    {
        if (!_resourceService.HasNoProvider)
        {
            _storageService.PruneRecentFiles(_resourceService.Root, _resourceService.Exists);
        }
    }

    private void OnModOpenedFirstTime()
    {
        _confirmWindow.Title = "Mod assets opened";
        _confirmWindow.Text = "You can add or manage references at any time\nvia Editor > Manage References.";
        _confirmWindow.ConfirmButtonText = "Ok";
        _confirmWindow.DenyButtonText = "";
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Icon");
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
                    _storageService.RecentFiles.TryGetValue(_resourceService.Root, out var recentFiles);
                    if (ImGui.BeginMenu("Open Recent", recentFiles?.Count > 0))
                    {
                        foreach (var path in recentFiles!.ToArray())
                        {
                            var name = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
                            if (ImGui.MenuItem(name))
                            {
                                _editorService.OpenEditorFor(path);
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip(path);
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
                if (_editorService.ActiveEditor is EddyEditor eddy)
                {
                    ImGui.SeparatorText("Eddy");
                    if (ImGui.MenuItem("Export Level as Diorama..."))
                    {
                        eddy.ExportAsDiorama();
                    }
                }

                if (_resourceService.GetModReferencePaths().Count > 0)
                {
                    ImGui.SeparatorText("Mod");
                    if (ImGui.MenuItem("Manage references"))
                    {
                        _referencesWindow.Show();
                    }
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
        _confirmWindow.Confirmed = () =>
        {
            _editorService.CloseActiveEditor();
        };
    }

    private void ShowCloseAllDialog()
    {
        if (!_editorService.HasAnyEditorUnsavedChanges())
        {
            _resourceService.CloseProvider();
            _editorService.CloseAllEditors();
            _editorService.OpenEditor(new WelcomeComponent(Game));
            return;
        }

        _confirmWindow.Text = "You have unsaved changes. Close all files?";
        _confirmWindow.Title = "Confirm Closing All";
        _confirmWindow.Confirmed = () =>
        {
            _resourceService.CloseProvider();
            _editorService.CloseAllEditors();
            _editorService.OpenEditor(new WelcomeComponent(Game));
        };
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
        _confirmWindow.Confirmed = () =>
        {
            Game.Exit();
        };
    }
}