using FezEditor.Services;
using FezEditor.Tools;
using FEZRepacker.Core.Conversion;
using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;
using ImGuiNET;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Components;

[UsedImplicitly]
public class ResourceExtractor : DrawableGameComponent
{
    private static readonly ILogger Logger = Logging.Create<ResourceExtractor>();

    private readonly HashSet<string> _expectedPaks = ["Essentials.pak", "Music.pak", "Other.pak", "Updates.pak"];

    private readonly Dictionary<string, string> _contentListing = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _pakFiles;

    private readonly string _directoryPath;

    private float _progress;

    private string _currentFile = "";

    private int _filesProcessed;

    private int _totalFiles;

    private string _status = "";

    private string _popup = "";

    private State _state = State.CanExtract;

    private State _previousState = State.Disposed;

    private CancellationTokenSource? _cts;

    private TimeSpan _disposeAfter = TimeSpan.Zero;

    public event Action? Competed;

    public ResourceExtractor(Game game, string[] paks, string directory) : base(game)
    {
        if (paks.Length < 1)
        {
            throw new ArgumentException("Pak files must be specified.");
        }

        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Target directory must be specified.");
        }

        _pakFiles = paks.Where(p => _expectedPaks.Any(p.EndsWith)).ToList();
        _directoryPath = directory;
    }

    protected override void LoadContent()
    {
        var content = Game.GetService<ContentService>().Get(this);
        var listing = content.LoadJson<string[]>("ContentListing");
        foreach (var file in listing)
        {
            _contentListing.Add(file, file);
        }
    }

    public override void Update(GameTime gameTime)
    {
        if (_state == State.Disposed)
        {
            Game.RemoveComponent(this);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        if (_state != _previousState)
        {
            _popup = _state switch
            {
                State.CanExtract => "Asset Extraction##Confirm",
                State.Extracting => "Asset Extraction##Process",
                State.Complete => "Asset Extraction##Complete",
                _ => _popup
            };
            ImGuiX.SetNextWindowCentered();
            ImGui.OpenPopup(_popup);
            _previousState = _state;
        }

        var isOpen = true;
        if (ImGui.BeginPopupModal(_popup, ref isOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            switch (_state)
            {
                case State.CanExtract:
                    {
                        ImGui.Text("This will extract all game assets to:");
                        ImGuiX.TextColored(new Color(0.5f, 0.8f, 1f, 1f), _directoryPath);
                        ImGui.Spacing();
                        ImGui.Text("Do you want to continue?");
                        ImGui.Spacing();

                        if (ImGuiX.Button("Yes", new Vector2(120, 0)))
                        {
                            _state = State.Extracting;
                            _ = ExtractAsync();
                        }

                        ImGui.SameLine();

                        if (ImGuiX.Button("No", new Vector2(120, 0)))
                        {
                            _state = State.Disposed;
                            ImGui.CloseCurrentPopup();
                        }

                        break;
                    }

                case State.Extracting:
                    {
                        ImGui.Text(_status);
                        ImGui.Text($"File: {_currentFile}");
                        ImGui.Text($"Progress: {_filesProcessed} / {_totalFiles}");

                        ImGuiX.ProgressBar(_progress, new Vector2(400, 0), $"{_progress * 100:F1}%");
                        if (ImGui.Button("Cancel"))
                        {
                            _cts?.Cancel();
                            ImGui.CloseCurrentPopup();
                        }

                        break;
                    }

                case State.Complete:
                    {
                        ImGuiX.TextColored(
                            _status.Contains("Error") ? Color.Red :
                            _status.Contains("complete") ? Color.Green :
                            Color.White,
                            _status);

                        _disposeAfter -= gameTime.ElapsedGameTime;
                        if (_disposeAfter < TimeSpan.Zero)
                        {
                            if (_status.Contains("complete"))
                            {
                                Competed?.Invoke();
                            }

                            _state = State.Disposed;
                            ImGui.CloseCurrentPopup();
                        }

                        break;
                    }
            }

            ImGui.EndPopup();
        }

        if (!isOpen)
        {
            _state = State.Disposed;
        }
    }

    private async Task ExtractAsync()
    {
        _cts = new CancellationTokenSource();
        _state = State.Extracting;
        _status = "Counting files...";
        _progress = 0f;

        try
        {
            await Task.Run(() => ExtractInternal(_cts.Token), _cts.Token);
            _status = "Extraction complete! Opening the directory...";
            _progress = 1.0f;
        }
        catch (OperationCanceledException)
        {
            _status = "Extraction cancelled";
        }
        catch (Exception ex)
        {
            _status = $"Error: {ex.Message}";
        }
        finally
        {
            _state = State.Complete;
            _disposeAfter = TimeSpan.FromSeconds(3);
        }
    }

    private void ExtractInternal(CancellationToken ct)
    {
        _totalFiles = 0;
        foreach (var file in _pakFiles)
        {
            using var pakStream = File.OpenRead(file);
            using var pakReader = new PakReader(pakStream);

            foreach (var pakFile in pakReader.ReadFiles())
            {
                ct.ThrowIfCancellationRequested();

                var path = file.EndsWith("Music.pak")
                    ? Path.Combine("music", pakFile.Path)
                    : pakFile.Path;

                if (_contentListing.ContainsKey(path))
                {
                    _totalFiles += 1;
                }
                else
                {
                    Logger.Warning("Missing listed asset - {0}", path);
                }
            }
        }

        _filesProcessed = 0;
        if (_totalFiles == 0)
        {
            _status = "No files to extract";
            Logger.Warning("No files to extract");
            return;
        }

        Logger.Information("Files to extract - {0}", _totalFiles);

        foreach (var file in _pakFiles)
        {
            ct.ThrowIfCancellationRequested();

            var outputDir = _directoryPath;
            if (file.EndsWith("Music.pak"))
            {
                outputDir = Path.Combine(_directoryPath, "Music");
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var pakStream = File.OpenRead(file);
            using var pakReader = new PakReader(pakStream);

            foreach (var pakFile in pakReader.ReadFiles())
            {
                ct.ThrowIfCancellationRequested();

                _currentFile = file.EndsWith("Music.pak")
                    ? _contentListing[Path.Combine("music", pakFile.Path)]
                    : _contentListing[pakFile.Path];

                _status = $"Extracting: {file}";
                var extension = pakFile.FindExtension();
                using var fileStream = pakFile.Open();
                var initialStreamPosition = fileStream.Position;

                FileBundle bundle;
                try
                {
                    var outputData = XnbSerializer.Deserialize(fileStream)!;
                    bundle = FormatConversion.Convert(outputData);
                }
                catch (Exception)
                {
                    fileStream.Seek(initialStreamPosition, SeekOrigin.Begin);
                    bundle = FileBundle.Single(fileStream, extension);
                }

                var pakFilePathNormalized = Path.Combine(_currentFile.Split('/', '\\'));
                bundle.BundlePath = Path.Combine(outputDir, pakFilePathNormalized);
                Directory.CreateDirectory(Path.GetDirectoryName(bundle.BundlePath) ?? "");

                foreach (var outputFile in bundle.Files)
                {
                    var fileName = bundle.BundlePath + bundle.MainExtension + outputFile.Extension;
                    using var fileOutputStream = File.Open(fileName, FileMode.Create);
                    outputFile.Data.CopyTo(fileOutputStream);
                }

                bundle.Dispose();

                _filesProcessed++;
                _progress = (float)_filesProcessed / _totalFiles;

                Logger.Information("Asset extracted ({0}/{1}) - {2}",
                    _filesProcessed,  _totalFiles, bundle.BundlePath);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        _cts?.Dispose();
        Game.GetService<ContentService>().Unload(this);
        base.Dispose(disposing);
    }

    private enum State
    {
        Disposed,
        CanExtract,
        Extracting,
        Complete
    }
}