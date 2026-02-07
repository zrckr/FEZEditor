using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

[UsedImplicitly]
public class MenuBar : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private AboutWindow? _aboutWindow;
    
    private readonly IEditorService _editorService;
    
    private readonly IResourceService _resourceService;

    public MenuBar(Game game, IEditorService editorService, IResourceService resourceService) : base(game)
    {
        _editorService = editorService;
        _resourceService = resourceService;
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Content/Icon");
    }

    public override void Draw(GameTime gameTime)
    {
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("File"))
            {
                ImGui.Separator();

                if (ImGui.MenuItem("Save File", _editorService.Flags.HasFlag(EditorFlags.SaveFile)))
                {
                    // TODO: saving single modified file
                }
                
                if (ImGui.MenuItem("Save File As...", _editorService.Flags.HasFlag(EditorFlags.SaveFile)))
                {
                    // TODO: saving single modified file to different location
                }
                
                if (ImGui.MenuItem("Save All Files", _editorService.Flags.HasFlag(EditorFlags.SaveFile)))
                {
                    // TODO: saving all modified files
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("Undo", _editorService.Flags.HasFlag(EditorFlags.Undo)))
                {
                    // TODO: History undo
                }
                
                if (ImGui.MenuItem("Redo", _editorService.Flags.HasFlag(EditorFlags.Redo)))
                {
                    // TODO: History redo
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("Close File", _editorService.Flags.HasFlag(EditorFlags.CloseFile)))
                {
                    _editorService.CloseEditor(_editorService.ActiveEditor!);
                }

                if (ImGui.MenuItem("Quit To Welcome", _editorService.Flags.HasFlag(EditorFlags.QuitToWelcome)))
                {
                    // TODO: add safeguard modal
                    _resourceService.CloseProvider();
                    _editorService.CloseAllEditors();
                    _editorService.OpenEditor(new WelcomeComponent(Game));
                }
                
                if (ImGui.MenuItem("Quit"))
                {
                    // TODO: add safeguard modal
                    Game.Exit();
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
            _aboutWindow = Game.CreateComponent<AboutWindow>();
            _aboutWindow.Disposed += (_, _) => { _aboutWindow = null; };
        }
    }
}