using System.Text.Json;
using FezEditor.Structure;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class AppStorageService : IDisposable
{
    public static readonly string BaseDir = Path.Combine(AppContext.BaseDirectory, "EditorData");

    private static readonly ILogger Logger = Logging.Create<AppStorageService>();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const int MaxRecentPaths = 10;

    public IReadOnlyList<Settings.RecentProvider> RecentProviders => _data.RecentProviders;

    public IReadOnlyDictionary<string, List<string>> RecentFiles => _data.RecentFiles;

    private Settings _data = new();

    private readonly Game _game;

    public AppStorageService(Game game)
    {
        _game = game;
        Load();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SaveWindowState();
        Save();
    }

    public void AddRecentProvider(string path, string kind)
    {
        _data.RecentProviders.RemoveAll(rp => string.Equals(rp.Path, path, StringComparison.OrdinalIgnoreCase));
        _data.RecentProviders.Insert(0, new Settings.RecentProvider(path, kind));

        if (_data.RecentProviders.Count > MaxRecentPaths)
        {
            _data.RecentProviders.RemoveRange(MaxRecentPaths, _data.RecentProviders.Count - MaxRecentPaths);
        }
    }

    public void AddRecentFile(string provider, string path)
    {
        if (!_data.RecentFiles.TryGetValue(provider, out var list))
        {
            list = new List<string>();
            _data.RecentFiles[provider] = list;
        }

        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);

        if (list.Count > MaxRecentPaths)
        {
            list.RemoveRange(MaxRecentPaths, list.Count - MaxRecentPaths);
        }
    }

    public void ClearRecentPaths()
    {
        _data.RecentProviders.Clear();
    }

    public void SaveWindowState()
    {
        _data = _data with
        {
            Window = new Settings.WindowSize(_game.Window.ClientBounds.Width, _game.Window.ClientBounds.Height)
        };
    }

    public void LoadWindowState(GraphicsDeviceManager gdm)
    {
        gdm.PreferredBackBufferWidth = _data.Window.Width;
        gdm.PreferredBackBufferHeight = _data.Window.Height;
    }

    public void Save()
    {
        try
        {
            using var file = new FileStream(Settings.FilePath, FileMode.Create);
            JsonSerializer.Serialize(file, _data, JsonOptions);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to save application data");
        }
    }

    private void Load()
    {
        if (!File.Exists(Settings.FilePath))
        {
            Logger.Information("No settings file found, using defaults");
            return;
        }

        try
        {
            using var file = new FileStream(Settings.FilePath, FileMode.Open);
            _data = JsonSerializer.Deserialize<Settings>(file, JsonOptions)!;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to load application data, using defaults");
            _data = new Settings();
        }
    }
}