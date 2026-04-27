using System.Diagnostics;
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
    private static readonly ILogger Logger = Logging.Create<ResourceService>();

    public event Action? ProviderChanged;

    public event Action? ProviderReset;

    public event Action? ThumbnailsReady;

    public event Action? ModOpenedFirstTime;

    public bool HasNoProvider => _provider == null;

    public bool IsReadonly => _provider?.IsReadonly ?? true;

    public string Root => _provider?.Root ?? string.Empty;

    public IEnumerable<string> Files => _provider?.Files ?? Enumerable.Empty<string>();

    private IResourceProvider? _provider;

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
        if (_provider != null)
        {
            _provider.Refresh();
            ProviderChanged?.Invoke();
        }
    }

    public void OpenProvider(IResourceProvider provider)
    {
        CloseProvider();
        _provider = provider;
        ProviderReset?.Invoke();
        ProviderChanged?.Invoke();
        Logger.Information("Opened {0} at {1} with {2} file(s)",
            provider.GetType().Name,
            _provider.GetFullPath(string.Empty),
            provider.Files.Count()
        );
    }

    public void CloseProvider()
    {
        ProviderReset?.Invoke();
        ProviderChanged?.Invoke();
        _cache.Clear();
        _provider?.Dispose();
        _provider = null;
        Logger.Information("Provider closed");
    }

    public Stream OpenStream(string path, string extension)
    {
        return _provider!.OpenStream(path, extension);
    }

    public bool Exists(string path)
    {
        return _provider?.Exists(path) ?? false;
    }

    public string GetExtension(string path)
    {
        return _provider?.GetExtension(path) ?? string.Empty;
    }

    public string GetFullPath(string path)
    {
        return _provider?.GetFullPath(path) ?? string.Empty;
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return _provider?.GetLastWriteTimeUtc(path) ?? DateTime.MinValue;
    }

    public string GetRelativePath(string absolutePath)
    {
        var root = GetFullPath(string.Empty);
        return absolutePath.WithoutBaseDirectory(root).Replace('\\', '/');
    }

    public object Load(string path)
    {
        if (path.Contains("SaveSlot", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = _provider!.OpenStream(path, string.Empty);
            var saveData = SaveData.Read(stream);
            Logger.Information("Loaded save data - {0}", path);
            return saveData;
        }

        if (_provider!.GetExtension(path) == ".ogg")
        {
            var stream = _provider.OpenStream(path, ".ogg");
            var oggContainer = new VorbisSoundContainer(stream, leaveOpen: false);
            Logger.Information("Loaded *.ogg file as SoundEffect - {0}", path);
            return oggContainer;
        }

        path = path.Replace('\\', '/');
        if (_cache.TryGetValue(path, out var weakRef) && weakRef.TryGetTarget(out var cached))
        {
            Logger.Debug("Cache hit - {0} ({1})", path, cached.GetType().Name);
            return cached;
        }

        var @object = _provider!.Load<object>(path);
        _cache[path] = new WeakReference<object>(@object);
        Logger.Information("Loaded - {0} ({1})", path, @object.GetType().Name);
        return @object;
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
        if (_provider!.GetExtension(path) == ".fezts.glb")
        {
            var trileSet = _provider!.Load<TrileSet>(path);
            return trileSet.Triles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);
        }

        return new Dictionary<int, string>();
    }

    public Dictionary<string, RAnimatedTexture> LoadAnimations(string path)
    {
        if (_cache.TryGetValue(path, out var weakRef) && weakRef.TryGetTarget(out var cached))
        {
            Logger.Debug("Cache hit animations - {0}", path);
            return (Dictionary<string, RAnimatedTexture>)cached;
        }

        var animations = new Dictionary<string, RAnimatedTexture>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in _provider!.Files)
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

        _provider!.Save(path, asset);
        _cache.Remove(path);
        _provider.Refresh();
        ProviderChanged?.Invoke();
        Logger.Information("Saved - {0}", path);
    }

    public void Duplicate(string path)
    {
        _provider!.Duplicate(path);
        _provider.Refresh();
        ProviderChanged?.Invoke();
        Logger.Information("Duplicated - {0}", path);
    }

    public void Move(string path, string newPath)
    {
        _provider!.Move(path, newPath);
        _cache.Remove(path);
        _provider.Refresh();
        ProviderChanged?.Invoke();
        Logger.Information("Moved - {0} -> {1}", path, newPath);
    }

    public void Delete(string path)
    {
        _provider!.Remove(path);
        _cache.Remove(path);
        _provider.Refresh();
        ProviderChanged?.Invoke();
        Logger.Information("Deleted - {0}", path);
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
        if (_provider is ModResourceProvider mod)
        {
            return mod.References
                .Select(r => r.GetFullPath(string.Empty))
                .ToList();
        }

        return new List<string>();
    }

    public void UpdateModReferences(IEnumerable<string> paths)
    {
        if (_provider is ModResourceProvider mod)
        {
            mod.UpdateReferences(paths);
            ProviderChanged?.Invoke();
        }
    }

    public void CopyFromReference(string path)
    {
        if (_provider is ModResourceProvider mod)
        {
            mod.CopyToMod(path);
            ProviderChanged?.Invoke();
            Logger.Information("Copied from reference - {0}", path);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cache.Clear();
        _provider?.Dispose();
        _game.Activated -= OnGameActivated;
    }
}