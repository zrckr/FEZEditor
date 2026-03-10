using FezEditor.Services;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class WelcomeComponent : EditorComponent
{
    private const float ContentWidth = 250f;

    private const float ContentHeight = 230f;

    private ResourceExtractor? _resourceExtractor;

    private Texture2D _logoTexture = null!;

    private readonly EditorService _editorService;

    private readonly ResourceService _resourceService;

    public WelcomeComponent(Game game) : base(game, "Welcome!")
    {
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
    }

    public override void LoadContent()
    {
        _logoTexture = ContentManager.Load<Texture2D>("Icon");
    }

    public override void Draw()
    {
        var regionSize = ImGuiX.GetContentRegionAvail();
        var offsetX = Math.Max(0, (regionSize.X - ContentWidth) / 2);
        var offsetY = Math.Max(0, (regionSize.Y - ContentHeight) / 2);

        ImGuiX.SetCursorPos(ImGuiX.GetCursorPos() + new Vector2(offsetX, offsetY));
        ImGui.BeginGroup();

        ImGuiX.Image(_logoTexture);
        ImGui.NewLine();
        ImGui.Text("Welcome to FEZEDITOR!");
        ImGui.NewLine();

        if (ImGui.Button("Open PAK file"))
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

        if (ImGui.Button("Open assets directory"))
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
                Title = "Select PAK files to extract",
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

        if (ImGui.Button("Open SaveSlot file to edit"))
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
            {
                Title = "Choose FEZ application directory...",
                DefaultLocation = Path.Combine(path, "FEZ", "")
            });
        }

        if (ImGui.Button("Quit"))
        {
            Game.Exit();
        }

        ImGui.EndGroup();
    }

    private void ExtractPaksAndOpenDirectory(FileDialog.Result source, FileDialog.Result target)
    {
        if (_resourceExtractor == null)
        {
            _resourceExtractor = new ResourceExtractor(Game, source.Files, target.Files[0]);
            _resourceExtractor.Disposed += (_, _) => _resourceExtractor = null;
            _resourceExtractor.Competed += () => OpenDirectory(target);
            Game.AddComponent(_resourceExtractor);
        }
    }

    private void OpenPakFile(FileDialog.Result result)
    {
        var pakPath = result.Files.FirstOrDefault();
        if (!string.IsNullOrEmpty(pakPath))
        {
            _resourceService.OpenProvider(new FileInfo(pakPath));
            _editorService.CloseEditor(this);
        }
    }

    private void OpenDirectory(FileDialog.Result result)
    {
        var dirPath = result.Files.FirstOrDefault();
        if (!string.IsNullOrEmpty(dirPath))
        {
            _resourceService.OpenProvider(new DirectoryInfo(dirPath));
            _editorService.CloseEditor(this);
        }
    }
}