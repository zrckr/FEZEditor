using FezEditor.Structure;
using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

[UsedImplicitly]
public class ResourceService : IDisposable
{
    public event Action? ProviderChanged;

    public bool HasNoProvider => _provider == null;

    public bool IsReadonly => _provider?.IsReadonly ?? true;

    public string Root => _provider?.Root ?? string.Empty;

    public IEnumerable<string> Files => _provider?.Files ?? Enumerable.Empty<string>();

    private IResourceProvider? _provider;

    private readonly Game _game;

    public ResourceService(Game game)
    {
        _game = game;
        _game.Activated += OnGameActivated;
    }

    private void OnGameActivated(object? o, EventArgs eventArgs)
    {
#if (!DEBUG)
        if (_provider != null)
        {
            _provider.Refresh();
            ProviderChanged?.Invoke();
        }
#endif
    }

    public void OpenProvider(FileSystemInfo info)
    {
        IResourceProvider provider = info switch
        {
            FileInfo file => new PakResourceProvider(file),
            DirectoryInfo dir => new DirResourceProvider(dir),
            _ => throw new ArgumentException("Not supported: " + info)
        };

        CloseProvider();
        _provider = provider;
        ProviderChanged?.Invoke();
    }

    public void CloseProvider()
    {
        ProviderChanged?.Invoke();
        _provider?.Dispose();
        _provider = null;
    }

    public Stream OpenStream(string path, string extension)
    {
        return _provider!.OpenStream(path, extension);
    }

    public object Load(string path)
    {
        if (path.Contains("SaveSlot", StringComparison.OrdinalIgnoreCase))
        {
            using var stream = _provider!.OpenStream(path, string.Empty);
            return SaveData.Read(stream);
        }
        
        return _provider!.Load<object>(path);
    }
    
    public SaveData LoadSaveDataFromContent(string path)
    {
        var bytes = _game.Content.LoadBytes(path);
        using var stream = new MemoryStream(bytes);
        return SaveData.Read(stream);
    }

    public void Save(string path, object asset)
    {
        if (asset is SaveData saveData)
        {
            using var stream = SaveData.Write(saveData);
            using var fileStream = new FileStream(path, FileMode.Create);
            stream.CopyTo(fileStream);
            return;
        }
        
        _provider!.Save(path, asset);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _provider?.Dispose();
        _game.Activated -= OnGameActivated;
    }
}