using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

[UsedImplicitly]
public class WelcomeScreen : DrawableGameComponent
{
    private Texture2D _logoTexture = null!;

    private bool _open = true;

    private ResourceExtractor? _resourceExtractor;
    
    private readonly IStateService _stateService;

    public WelcomeScreen(Game game, IStateService stateService) : base(game)
    {
        _stateService = stateService;
    }

    protected override void LoadContent()
    {
        _logoTexture = Game.Content.Load<Texture2D>("Content/Icon");
    }

    public override void Draw(GameTime gameTime)
    {
        ImGui.SetNextWindowCollapsed(!_open, ImGuiCond.FirstUseEver);

        var center = ImGui.GetMainViewport().GetCenter().ToXna();
        ImGuiX.SetNextWindowPos(center, ImGuiCond.Appearing, Vector2.One / 2);

        if (ImGui.Begin("Start", ref _open,
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoMove | ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGuiX.Image(_logoTexture, new Vector2(64, 64));
            ImGui.NewLine();
            ImGui.Text("Welcome to FEZEDITOR!");
            ImGui.NewLine();

            if (ImGui.Button("Open PAK..."))
            {
                FileDialog.Show(FileDialog.Type.OpenFile, OpenPakFile, new FileDialog.Options
                {
                    Title = "Choose PAK file...",
                    Filters = new FileDialog.Filter[]
                    {
                        new("PAK files", "pak")
                    }
                });
            }

            if (ImGui.Button("Open assets directory..."))
            {
                FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
                {
                    Title = "Choose assets directory..."
                });
            }

            if (ImGui.Button("Extract assets and open them..."))
            {
                var selectOptions = new FileDialog.Options
                {
                    Title = "Select PAK files to extract...",
                    AllowMultiple = true,
                    Filters = new FileDialog.Filter[]
                    {
                        new("PAK files", "pak")
                    }
                };

                FileDialog.Show(FileDialog.Type.OpenFile, source =>
                    {
                        if (source.Files.Length > 0)
                        {
                            FileDialog.Show(FileDialog.Type.OpenFolder,
                                target => ExtractPaksAndOpenDirectory(source, target), new FileDialog.Options
                                {
                                    Title = "Choose a directory to save assets..."
                                });
                        }
                    },
                    selectOptions);
            }

            if (ImGui.Button("Quit"))
            {
                _stateService.Quit();
            }
        }

        ImGui.End();
    }

    private void ExtractPaksAndOpenDirectory(FileDialog.Result source, FileDialog.Result target)
    {
        if (source.Files.Length > 0 && target.Files.Length > 0 && _resourceExtractor == null)
        {
            try
            {
                _resourceExtractor = Game.CreateComponent<ResourceExtractor>();
                _resourceExtractor.Disposed += (_, _) => { _resourceExtractor = null; };
                _resourceExtractor.Initialize(source.Files, target.Files[0]);
                _resourceExtractor.Competed += () => OpenDirectory(target);
            }
            catch (Exception ex)
            {
                Game.CreateComponent<ModalWindow>().Show(ex.Message, "Error occured!");
            }
        }
    }

    private void OpenPakFile(FileDialog.Result result)
    {
        if (result.Files.Length > 0)
        {
            try
            {
                var pakService = Game.CreateService<IResourceService, PakResourceService>();
                pakService.Initialize(new FileInfo(result.Files[0]));
                Game.RemoveComponent(this);
            }
            catch (Exception ex)
            {
                Game.CreateComponent<ModalWindow>().Show(ex.Message, "Error occured!");
            }
        }
    }

    private void OpenDirectory(FileDialog.Result result)
    {
        if (result.Files.Length > 0)
        {
            try
            {
                var dirService = Game.CreateService<IResourceService, DirResourceService>();
                dirService.Initialize(new DirectoryInfo(result.Files[0]));
                Game.RemoveComponent(this);
            }
            catch (Exception ex)
            {
                Game.CreateComponent<ModalWindow>().Show(ex.Message, "Error occured!");
            }
        }
    }
}