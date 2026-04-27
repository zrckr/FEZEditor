using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Serilog;

namespace FezEditor.Components;

public class WelcomeComponent : EditorComponent
{
    private static readonly ILogger Logger = Logging.Create<WelcomeComponent>();

    private const float ContentWidth = 250f;

    private const float ContentHeight = 230f;

    private ResourceExtractor? _resourceExtractor;

    private Texture2D _logoTexture = null!;

    private readonly AppStorageService _appStorageService;

    private readonly EditorService _editorService;

    private readonly ResourceService _resourceService;

    private readonly ConfirmWindow _confirm;

    public WelcomeComponent(Game game) : base(game, "Welcome!")
    {
        _appStorageService = game.GetService<AppStorageService>();
        _editorService = game.GetService<EditorService>();
        _resourceService = game.GetService<ResourceService>();
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void LoadContent()
    {
        _logoTexture = ContentManager.Load<Texture2D>("Icon");
    }

    public override void Draw()
    {
        var regionSize = ImGuiX.GetContentRegionAvail();
        var offset = new Vector2
        {
            X = Math.Max(0, (regionSize.X - ContentWidth) / 2),
            Y = Math.Max(0, (regionSize.Y - ContentHeight) / 2)
        };

        ImGuiX.SetCursorPos(ImGuiX.GetCursorPos() + offset);
        ImGui.BeginGroup();

        ImGuiX.Image(_logoTexture);
        ImGui.NewLine();
        ImGui.Text("Welcome to FEZEDITOR!");
        ImGui.NewLine();

        if (ImGuiX.BeginChild("##recentWrapper", new Vector2(ContentWidth, 0), ImGuiChildFlags.AutoResizeY))
        {
            if (ImGui.CollapsingHeader("Open Recent"))
            {
                var recentPaths = _appStorageService.RecentProviders.ToArray();
                if (recentPaths.Length == 0)
                {
                    ImGui.Indent();
                    ImGui.TextDisabled("No recent files.");
                    ImGui.Unindent();
                }
                else
                {
                    ImGui.Indent();
                    foreach (var entry in recentPaths)
                    {
                        var name = Path.GetFileName(entry.Path.TrimEnd('/', '\\'));
                        if (string.IsNullOrEmpty(name))
                        {
                            name = entry.Path;
                        }

                        var icon = entry.Kind switch
                        {
                            "File" => Icons.Package,
                            "Directory" => Icons.Folder,
                            "Mod" => Icons.FileSubmodule,
                            _ => throw new InvalidOperationException()
                        };
                        if (ImGuiX.Button($"{icon} {name}##recent_{entry.Path}", new Vector2(-1, 0)))
                        {
                            OpenRecentEntry(entry);
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.SetTooltip(entry.Path);
                        }
                    }

                    if (ImGuiX.Button($"{Icons.Trash} Clear recent files", new Vector2(-1, 0)))
                    {
                        _appStorageService.ClearRecentPaths();
                    }
                }
            }

            ImGui.EndChild();
        }

        if (ImGui.Button($"{Icons.Package} Open PAK file"))
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

        if (ImGui.Button($"{Icons.Folder} Open assets directory"))
        {
            FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
            {
                Title = "Choose assets directory..."
            });
        }

        if (ImGui.Button($"{Icons.FileSubmodule} Open mod assets directory"))
        {
            FileDialog.Show(FileDialog.Type.OpenFolder, OpenMod, new FileDialog.Options
            {
                Title = "Choose mod assets directory..."
            });
        }

        if (ImGui.Button($"{Icons.Export} Extract assets and open them..."))
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
                    FileDialog.Show(FileDialog.Type.OpenFolder,
                        target => ExtractPaksAndOpenDirectory(source, target), new FileDialog.Options
                        {
                            Title = "Choose a directory to save assets..."
                        });
                },
                selectOptions);
        }

        if (ImGui.Button($"{Icons.SaveAs} Open SaveSlot file to edit"))
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            FileDialog.Show(FileDialog.Type.OpenFolder, OpenDirectory, new FileDialog.Options
            {
                Title = "Choose FEZ application directory...",
                DefaultLocation = Path.Combine(path, "FEZ", "")
            });
        }

        if (ImGui.Button($"{Icons.CloseAll} Quit"))
        {
            Game.Exit();
        }

        ImGui.EndGroup();
    }

    public override void Dispose()
    {
        base.Dispose();
        Game.RemoveComponent(_confirm);
    }

    private void ExtractPaksAndOpenDirectory(string[] sources, string[] targets)
    {
        if (_resourceExtractor == null)
        {
            _resourceExtractor = new ResourceExtractor(Game, sources, targets[0]);
            _resourceExtractor.Disposed += (_, _) => _resourceExtractor = null;
            _resourceExtractor.Competed += () => OpenDirectory(targets);
            Game.AddComponent(_resourceExtractor);
        }
    }

    private void OpenPakFile(string[] files)
    {
        var pakPath = files.FirstOrDefault();
        if (!string.IsNullOrEmpty(pakPath))
        {
            _appStorageService.AddRecentProvider(pakPath, "File");
            _resourceService.OpenProvider(new PakResourceProvider(new FileInfo(pakPath)));
            _editorService.CloseEditor(this);
        }
    }

    private void OpenDirectory(string[] files)
    {
        var dirPath = files.FirstOrDefault();
        if (!string.IsNullOrEmpty(dirPath))
        {
            _appStorageService.AddRecentProvider(dirPath, "Directory");
            _resourceService.OpenProvider(new DirResourceProvider(new DirectoryInfo(dirPath)));
            _editorService.CloseEditor(this);
        }
    }

    private void OpenMod(string[] files)
    {
        var modPath = files.FirstOrDefault()!;
        var provider = new ModResourceProvider(new DirectoryInfo(modPath), _appStorageService);
        if (provider.References.Count < 1)
        {
            var options = new FileDialog.Options
            {
                Title = "Add reference PAK files...",
                AllowMultiple = true,
                Filters = new[] { new FileDialog.Filter("PAK files", "pak") }
            };

            FileDialog.Show(FileDialog.Type.OpenFile, pakFiles =>
            {
                provider.UpdateReferences(pakFiles);
                OpenModProvider();
                _resourceService.NotifyModOpenedFirstTime();
            }, options);
        }
        else
        {
            OpenModProvider();
        }

        return;

        void OpenModProvider()
        {
            _appStorageService.AddRecentProvider(modPath, "Mod");
            _resourceService.OpenProvider(provider);
            _editorService.CloseEditor(this);
        }
    }

    private void OpenRecentEntry(Settings.RecentProvider provider)
    {
        var exists = provider.Kind == "File"
            ? File.Exists(provider.Path)
            : Directory.Exists(provider.Path);

        if (!exists)
        {
            Logger.Warning("Recent path no longer exists: {Path}", provider.Path);
            return;
        }

        if (provider.Kind == "File")
        {
            OpenPakFile(new[] { provider.Path });
        }
        else if (provider.Kind == "Directory")
        {
            OpenDirectory(new[] { provider.Path });
        }
        else if (provider.Kind == "Mod")
        {
            OpenMod(new[] { provider.Path });
        }
    }
}