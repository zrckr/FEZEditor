using System.Text.Json;
using FezEditor.Structure;
using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using SDL3;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class AppStorageService : IDisposable
{
    public static readonly string BaseDir = Path.Combine(AppContext.BaseDirectory, "EditorData");

    private static readonly string CacheDir = Path.Combine(BaseDir, "Cache");

    private static readonly string HistoryDir = Path.Combine(BaseDir, "History");

    private static readonly ILogger Logger = Log.ForContext<AppStorageService>();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private const int MaxRecentPaths = 10;

    public IReadOnlyList<Settings.RecentProvider> RecentProviders => _data.RecentProviders;

    public IReadOnlyDictionary<string, List<string>> RecentFiles => _data.RecentFiles;

    public Color[] PaintPalette => _data.PaintPalette;

    public float? DisplayScale
    {
        get => _data.DisplayScale;
        set
        {
            _data = _data with { DisplayScale = value };
            Save();
        }
    }

    public bool ShowRenderingStats
    {
        get => _data.ShowRenderingStats;
        set
        {
            _data = _data with { ShowRenderingStats = value };
            Save();
        }
    }

    public bool ShowInputHints
    {
        get => _data.ShowInputHints;
        set
        {
            _data = _data with { ShowInputHints = value };
            Save();
        }
    }

    public string HatLauncherPath
    {
        get => _data.HatLauncherPath;
        set
        {
            _data = _data with { HatLauncherPath = value };
            Save();
        }
    }

    private Settings _data = new();

    public AppStorageService(FezEditor editor)
    {
        Directory.CreateDirectory(CacheDir);
        ClearAbandonedHistory();
        Load();
        LoadWindowState();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        ThumbnailDatabase.Flush();
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

        Save();
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

        Save();
    }

    public IReadOnlyList<string> GetReferenceProviders(string modPath)
    {
        return _data.ReferenceProviders.TryGetValue(modPath, out var list)
            ? list
            : new List<string>();
    }

    public void SavePaintPalette(Color[] palette)
    {
        _data = _data with { PaintPalette = palette };
        Save();
    }

    public void SetReferenceProviders(string modPath, IEnumerable<string> paths)
    {
        _data.ReferenceProviders[modPath] = paths.ToList();
        Save();
    }

    public void PruneRecentFiles(string provider, Func<string, bool> exists)
    {
        if (_data.RecentFiles.TryGetValue(provider, out var list))
        {
            list.RemoveAll(p => !exists(p));
            Save();
        }
    }

    public void ClearRecentPaths()
    {
        _data.RecentProviders.Clear();
        Save();
    }

    private void SaveWindowState()
    {
        var flags = SDL.SDL_GetWindowFlags(FezEditor.GameWindow.Handle);
        var maximized = (flags & SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0;
        var window = _data.Window;

        if (!maximized)
        {
            var bounds = FezEditor.GameWindow.ClientBounds;
            window.Width = bounds.Width;
            window.Height = bounds.Height;
        }

        _data = _data with
        {
            Window = window,
            IsWindowMaximized = maximized
        };
    }

    private void LoadWindowState()
    {
        FezEditor.DeviceManager.PreferredBackBufferWidth = _data.Window.Width;
        FezEditor.DeviceManager.PreferredBackBufferHeight = _data.Window.Height;
        FezEditor.DeviceManager.ApplyChanges();
        if (_data.IsWindowMaximized)
        {
            SDL.SDL_MaximizeWindow(FezEditor.GameWindow.Handle);
        }
    }

    public static void ClearCache()
    {
        ThumbnailDatabase.Reset();
        foreach (var file in Directory.GetFiles(CacheDir))
        {
            File.Delete(file);
        }
    }

    public static string CreateHistorySessionDirectory()
    {
        Directory.CreateDirectory(HistoryDir);
        var path = Path.Combine(HistoryDir, DateTime.UtcNow.Ticks.ToString());
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool HasCacheFile(string filename)
    {
        return File.Exists(Path.Combine(CacheDir, filename));
    }

    public static void SaveToCache(string filename, Stream stream)
    {
        try
        {
            using var file = new FileStream(Path.Combine(CacheDir, filename), FileMode.Create, FileAccess.Write);
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(file);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to save cache binary data.");
        }
    }

    public static Stream LoadFromCache(string filename)
    {
        var memory = new MemoryStream();
        try
        {
            using var stream = new FileStream(Path.Combine(CacheDir, filename), FileMode.Open, FileAccess.Read);
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(memory);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to read cache binary data.");
        }

        memory.Seek(0, SeekOrigin.Begin);
        return memory;
    }

    public static bool TryLoadCacheJson<T>(string filename, out T? value)
    {
        value = default;
        var path = Path.Combine(CacheDir, filename);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            value = JsonSerializer.Deserialize<T>(file, JsonOptions);
            return value != null;
        }
        catch (Exception e)
        {
            Logger.Warning(e, "Unable to read cache database {0}", filename);
            return false;
        }
    }

    public static bool SaveCacheJson<T>(string filename, T value)
    {
        var path = Path.Combine(CacheDir, filename);
        var temporaryPath = path + ".tmp";
        try
        {
            using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, value, JsonOptions);
            }

            File.Move(temporaryPath, path, true);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error(e, "Unable to save cache database {0}", filename);
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // Preserve the original persistence error.
            }

            return false;
        }
    }

    private void Save()
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

    private static void ClearAbandonedHistory()
    {
        if (Directory.Exists(HistoryDir))
        {
            try
            {
                Directory.Delete(HistoryDir, true);
            }
            catch (Exception e)
            {
                Logger.Warning(e, "Unable to clear abandoned history data.");
            }
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