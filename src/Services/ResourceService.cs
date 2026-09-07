using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FezEditor.Components;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Serilog;

namespace FezEditor.Services;

[UsedImplicitly]
public class ResourceService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ResourceService>();

    public event Action? ProviderChanged;

    public event Action? ProviderReset;

    public event Action? ThumbnailsReady;

    public event Action? ModOpenedFirstTime;

    public bool HasNoProvider
    {
        get
        {
            lock (_providerLock)
            {
                return _provider == null;
            }
        }
    }

    public bool IsReadonly
    {
        get
        {
            lock (_providerLock)
            {
                return _provider?.IsReadonly ?? true;
            }
        }
    }

    public string RootPath
    {
        get
        {
            lock (_providerLock)
            {
                return _provider?.RootPath ?? string.Empty;
            }
        }
    }

    public ModDirectoryResolution? ModResolution
    {
        get
        {
            lock (_providerLock)
            {
                return _provider is ModResourceProvider mod ? mod.Resolution : null;
            }
        }
    }

    public IEnumerable<string> Files
    {
        get
        {
            lock (_providerLock)
            {
                return _provider?.Entries.OfType<ResourceEntry.File>().Select(f => f.Path).ToArray() ?? [];
            }
        }
    }

    public IEnumerable<ResourceEntry> Entries
    {
        get
        {
            lock (_providerLock)
            {
                return _provider is ModResourceProvider mod
                    ? mod.VirtualEntries.ToArray()
                    : _provider?.Entries.ToArray() ?? [];
            }
        }
    }

    private AssetPickWindow? _assetPickWindow;

    private IResourceProvider? _provider;

    private readonly Lock _providerLock = new();

    private readonly IContentManager _content;

    private readonly Game _game;

    private readonly Dictionary<string, WeakReference<object>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ResourceService(Game game)
    {
        _game = game;
        _game.Activated += OnGameActivated;
        _content = game.GetService<ContentService>().Global;
    }

    private void OnGameActivated(object? o, EventArgs eventArgs)
    {
        var refreshed = false;
        lock (_providerLock)
        {
            if (_provider != null)
            {
                _provider.Refresh();
                refreshed = true;
            }
        }

        if (refreshed)
        {
            ProviderChanged?.Invoke();
        }
    }

    public void OpenProvider(IResourceProvider provider)
    {
        var rootPath = provider.RootPath;
        var fileCount = provider.Entries.Count(re => re is ResourceEntry.File);
        IResourceProvider? previous;
        lock (_providerLock)
        {
            previous = _provider;
            _provider = provider;
            _cache.Clear();
        }

        previous?.Dispose();
        ProviderReset?.Invoke();
        ProviderChanged?.Invoke();
        Logger.Information("Opened {0} at {1} with {2} file(s)",
            provider.GetType().Name,
            rootPath,
            fileCount
        );
    }

    public void CloseProvider()
    {
        IResourceProvider? provider;
        lock (_providerLock)
        {
            provider = _provider;
            _provider = null;
            _cache.Clear();
        }

        provider?.Dispose();
        ProviderReset?.Invoke();
        ProviderChanged?.Invoke();
        Logger.Information("Provider closed");
    }

    public Stream OpenStream(string path, string extension)
    {
        lock (_providerLock)
        {
            return _provider!.OpenStream(path, extension);
        }
    }

    public bool Exists(string path)
    {
        lock (_providerLock)
        {
            return _provider?.Exists(path) ?? false;
        }
    }

    public string GetExtension(string path)
    {
        lock (_providerLock)
        {
            return _provider?.GetExtension(path) ?? string.Empty;
        }
    }

    public string GetFullPath(string path)
    {
        lock (_providerLock)
        {
            return _provider?.GetFullPath(path) ?? string.Empty;
        }
    }

    public string GetRelativePath(string absolutePath)
    {
        var relative = Path.GetRelativePath(RootPath, absolutePath);
        return relative == "." ? string.Empty : relative;
    }

    public bool IsReadonlyPath(string path)
    {
        lock (_providerLock)
        {
            return _provider?.IsReadonlyPath(path) ?? true;
        }
    }

    public T Load<T>(string path) where T : class
    {
        lock (_providerLock)
        {
            if (path.Contains("SaveSlot", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = _provider!.OpenStream(path, string.Empty);
                var saveData = SaveData.Read(stream);
                Logger.Information("Loaded save data - {0}", path);
                return (saveData as T)!;
            }

            if (_provider!.GetExtension(path) == ".ogg")
            {
                var stream = _provider!.OpenStream(path, ".ogg");
                var oggContainer = new VorbisSoundContainer(stream, leaveOpen: false);
                Logger.Information("Loaded *.ogg file as SoundEffect - {0}", path);
                return (oggContainer as T)!;
            }

            path = path.Replace('\\', '/');
            if (_cache.TryGetValue(path, out var reference) && reference.TryGetTarget(out var cached))
            {
                Logger.Debug("Cache hit - {0} ({1})", path, cached.GetType().Name);
                return (T)cached;
            }

            var asset = _provider!.Load<T>(path);
            _cache[path] = new WeakReference<object>(asset);
            Logger.Information("Loaded - {0} ({1})", path, asset.GetType().Name);
            return asset;
        }
    }

    public bool TryLoad<T>(string path, [NotNullWhen(true)] out T? resource) where T : class
    {
        try
        {
            resource = Load<T>(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            resource = null;
            return false;
        }
    }

    public SaveData LoadSaveDataFromContent(string path)
    {
        using var stream = _content.LoadStream(path);
        var saveData = SaveData.Read(stream);
        Logger.Information("Loaded save data from content - {0}", path);
        return saveData;
    }

    public Dictionary<int, string> GetTrileSetList(string path)
    {
        lock (_providerLock)
        {
            try
            {
                var trileSet = _provider!.Load<TrileSet>(path);
                return trileSet.Triles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }
    }

    public Dictionary<string, RAnimatedTexture> LoadAnimations(string path)
    {
        lock (_providerLock)
        {
            if (_cache.TryGetValue(path, out var weakRef) && weakRef.TryGetTarget(out var cached))
            {
                Logger.Debug("Cache hit animations - {0}", path);
                return (Dictionary<string, RAnimatedTexture>)cached;
            }

            var animations = new Dictionary<string, RAnimatedTexture>(StringComparer.OrdinalIgnoreCase);
            var files = _provider!.Entries.Where(re => re is ResourceEntry.File).Select(f => f.Path);
            foreach (var file in files)
            {
                if (file.StartsWith(path, StringComparison.OrdinalIgnoreCase) &&
                    !file.Contains("Metadata", StringComparison.OrdinalIgnoreCase))
                {
                    var name = file[(path.Length + 1)..];
                    var asset = _provider!.Load<RAnimatedTexture>(file);
                    animations.Add(name, asset);
                }
            }

            _cache[path] = new WeakReference<object>(animations);
            Logger.Information("Loaded animations - {0}", path);
            return animations;
        }
    }

    public bool TryLoadSkyTexture(string sky, string? textureName, [NotNullWhen(true)] out RTexture2D? texture)
    {
        if (string.IsNullOrEmpty(sky) || string.IsNullOrEmpty(textureName))
        {
            texture = null;
            return false;
        }
        return TryLoad($"Skies/{sky}/{textureName}", out texture);
    }

    public void Save(string path, object asset)
    {
        if (asset is SaveData saveData)
        {
            using var stream = SaveData.Write(saveData);
            using var fileStream = new FileStream(path, FileMode.Create);
            stream.CopyTo(fileStream);
            Logger.Information("Saved save data - {0}", path);
            return;
        }

        lock (_providerLock)
        {
            _provider!.Save(path, asset);
            _cache.Remove(path);
            _provider.Refresh();
        }

        ThumbnailGenerator.MarkDirtyForSave(path, asset);
        ProviderChanged?.Invoke();
        Logger.Information("Saved - {0}", path);
    }

    public void Duplicate(string path)
    {
        lock (_providerLock)
        {
            _provider!.Duplicate(path);
            _provider.Refresh();
        }

        ProviderChanged?.Invoke();
        Logger.Information("Duplicated - {0}", path);
    }

    public void CreateDirectory(string path)
    {
        lock (_providerLock)
        {
            _provider!.CreateDirectory(path);
            _provider.Refresh();
        }

        ProviderChanged?.Invoke();
        Logger.Information("Created directory - {0}", path);
    }

    public void Move(string path, string newPath)
    {
        lock (_providerLock)
        {
            _provider!.Move(path, newPath);
            _cache.Remove(path);
            _provider.Refresh();
        }

        ProviderChanged?.Invoke();
        Logger.Information("Moved - {0} -> {1}", path, newPath);
    }

    public void Delete(string path)
    {
        lock (_providerLock)
        {
            _provider!.Remove(path);
            _cache.Remove(path);
            _provider.Refresh();
        }

        ProviderChanged?.Invoke();
        Logger.Information("Deleted - {0}", path);
    }

    public void InvalidateCacheFor(string path)
    {
        lock (_providerLock)
        {
            _cache.Remove(path);
        }
    }

    public void OpenInFileManager(string path)
    {
        var absolutePath = GetFullPath(path);
        var target = File.Exists(absolutePath) ? absolutePath : Path.GetDirectoryName(absolutePath)!;
        Logger.Information("Opening in File Manager - {0}", path);

        if (OperatingSystem.IsWindows())
        {
            Process.Start("explorer.exe", $"/select,\"{target}\"");
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", $"-R \"{target}\"");
        }
        else
        {
            Process.Start("xdg-open", $"\"{Path.GetDirectoryName(target)}\"");
        }
    }

    public void NotifyThumbnailsReady()
    {
        ThumbnailsReady?.Invoke();
    }

    public void NotifyModOpenedFirstTime()
    {
        ModOpenedFirstTime?.Invoke();
    }

    public IReadOnlyList<string> GetModReferencePaths()
    {
        lock (_providerLock)
        {
            if (_provider is ModResourceProvider mod)
            {
                return mod.References
                    .Select(r => r.RootPath)
                    .ToList();
            }

            return [];
        }
    }

    public void UpdateModReferences(IEnumerable<string> paths)
    {
        var updated = false;
        lock (_providerLock)
        {
            if (_provider is ModResourceProvider mod)
            {
                mod.UpdateReferences(paths);
                updated = true;
            }
        }

        if (updated)
        {
            ProviderChanged?.Invoke();
        }
    }

    public void CopyFromReference(string path)
    {
        var copied = false;
        lock (_providerLock)
        {
            if (_provider is ModResourceProvider mod)
            {
                mod.CopyToMod(path);
                copied = true;
            }
        }

        if (copied)
        {
            ProviderChanged?.Invoke();
            Logger.Information("Copied from reference - {0}", path);
        }
    }

    public void RequestAssetPathFromUser(string title, string text, string rootPath, Action<string> onProvided)
    {
        if (_assetPickWindow == null)
        {
            _game.Components.Add(_assetPickWindow = new AssetPickWindow(_game));
        }

        _assetPickWindow.Title = title;
        _assetPickWindow.Text = text;
        _assetPickWindow.RootPath = rootPath;
        _assetPickWindow.MissingAssetsText = "(no assets found)";
        _assetPickWindow.Accepted = onProvided;
    }

    public void Refresh()
    {
        lock (_providerLock)
        {
            _provider!.Refresh();
        }

        ProviderChanged?.Invoke();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        IResourceProvider? provider;
        lock (_providerLock)
        {
            provider = _provider;
            _provider = null;
            _cache.Clear();
        }

        provider?.Dispose();
        _game.Activated -= OnGameActivated;

        if (_assetPickWindow != null)
        {
            _game.Components.Remove(_assetPickWindow);
            _assetPickWindow = null;
        }
    }

    public static string GetProviderDisplayName(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var pathParts = path.TrimEnd('/', '\\').Split('/', '\\');
        if (pathParts.Length > 1 && pathParts[^1].Equals("Assets", StringComparison.OrdinalIgnoreCase))
        {
            return $"{pathParts[^2]}/{pathParts[^1]}";
        }

        return pathParts[^1];
    }
}