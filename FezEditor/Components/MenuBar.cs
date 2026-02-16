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
    
    private readonly EditorService _editorService;
    
    private readonly ResourceService _resourceService;
    
    private readonly InputService _inputService;

    public MenuBar(Game game) : base(game)
    {
        game.AddComponent(_confirmWindow = new ConfirmWindow(game));
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        _inputService = game.GetService<InputService>();
    }

    protected override void Dispose(bool disposing)
    {
        _confirmWindow.Dispose();
        _aboutWindow?.Dispose();
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Icon");
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
                ImGui.Separator();

                var enabled = _editorService.Flags.HasFlag(EditorFlags.SaveFile);
                var shortcut = _inputService.GetActionBinding(InputActions.UiSave);
                if (ImGui.MenuItem("Save File", shortcut, false, enabled))
                {
                    _editorService.SaveActiveEditorChanges();
                }
                
                enabled = _editorService.Flags.HasFlag(EditorFlags.SaveFile);
                shortcut = _inputService.GetActionBinding(InputActions.UiSaveAs);
                if (ImGui.MenuItem("Save File As...", shortcut, false, enabled))
                {
                    _editorService.SaveActiveEditorChangesAs();
                }
                
                enabled = _editorService.Flags.HasFlag(EditorFlags.SaveFile);
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
                if (ImGui.MenuItem("Close All", shortcut, false, enabled))
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