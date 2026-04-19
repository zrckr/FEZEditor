using System.Text.Json;
using FezEditor.Structure;
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
        Directory.CreateDirectory(CacheDir);
        Load();
        game.Window.ClientSizeChanged += OnClientSizeChanged;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _game.Window.ClientSizeChanged -= OnClientSizeChanged;
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

    private void OnClientSizeChanged(object? sender, EventArgs e)
    {
        var flags = SDL.SDL_GetWindowFlags(_game.Window.Handle);
        if ((flags & SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) == 0)
        {
            SDL.SDL_GetWindowSize(_game.Window.Handle, out var width, out var height);
            _data = _data with
            {
                Window = _data.Window with
                {
                    Width = width,
                    Height = height
                }
            };
        }
    }

    private void SaveWindowState()
    {
        var maximized = (SDL.SDL_GetWindowFlags(_game.Window.Handle) & SDL.SDL_WindowFlags.SDL_WINDOW_MAXIMIZED) != 0;
        _data = _data with
        {
            Window = _data.Window with
            {
                IsMaximized = maximized
            }
        };
    }

    public void LoadWindowState()
    {
        SDL.SDL_SetWindowSize(_game.Window.Handle, _data.Window.Width, _data.Window.Height);
        if (_data.Window.IsMaximized)
        {
            SDL.SDL_MaximizeWindow(_game.Window.Handle);
        }
    }

    public static bool HasCacheFile(string filename)
    {
        return File.Exists(Path.Combine(CacheDir, filename));
    }

    public static void DeleteCacheFile(string filename)
    {
        var path = Path.Combine(CacheDir, filename);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void SaveToCache(string filename, Stream stream)
    {
        try
        {
            using var file = new FileStream(Path.Combine(CacheDir, filename), FileMode.OpenOrCreate, FileAccess.Write);
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