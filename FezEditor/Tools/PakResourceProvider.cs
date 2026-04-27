using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;

namespace FezEditor.Tools;

internal class PakResourceProvider : IResourceProvider
{
    public bool IsReadonly => true;

    public string Root => _pakFile.Name;

    public IEnumerable<string> Files => _records.Keys;

    private readonly Dictionary<string, string> _records = new(StringComparer.OrdinalIgnoreCase);

    private readonly FileInfo _pakFile;

    private readonly string _musicPrefix;

    public PakResourceProvider(FileInfo info)
    {
        if (info is not { Extension: ".pak", Exists: true })
        {
            throw new FileNotFoundException(info.FullName);
        }

        _pakFile = info;
        _musicPrefix = info.Name.StartsWith("Music") ? "music/" : "";
        Refresh();
    }

    public bool Exists(string path)
    {
        return _records.ContainsKey(path);
    }

    public string GetExtension(string path)
    {
        return _records.GetValueOrDefault(path, "");
    }

    public string GetFullPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return _pakFile.FullName;
        }

        return _records.TryGetValue(path, out var extension)
            ? Path.Combine(_pakFile.FullName, path + extension)
            : "";
    }

    public Stream OpenStream(string path, string extension)
    {
        if (!(Exists(path) && _records.ContainsValue(extension)))
        {
            throw new FileNotFoundException(path);
        }

        var pakPath = StripMusicPrefix(path);
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);

        var record = reader.ReadFiles().FirstOrDefault(r =>
            r.Path.Equals(pakPath, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException(path);

        return record.Open();
    }

    public T Load<T>(string path) where T : class
    {
        if (!Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        var pakPath = StripMusicPrefix(path);
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);

        var record = reader.ReadFiles().FirstOrDefault(r =>
                         r.Path.Replace('\\', '/').Equals(pakPath, StringComparison.OrdinalIgnoreCase))
                     ?? throw new FileNotFoundException(path);

        using var xnbStream = record.Open();
        var initialPosition = xnbStream.Position;
        try
        {
            return (T)XnbSerializer.Deserialize(xnbStream)!;
        }
        catch
        {
            xnbStream.Seek(initialPosition, SeekOrigin.Begin);
            throw;
        }
    }

    public void Save<T>(string path, T asset) where T : class
    {
        throw new NotSupportedException();
    }

    public void Move(string path, string newPath)
    {
        throw new NotSupportedException();
    }

    public void Duplicate(string path)
    {
        throw new NotSupportedException();
    }

    public void Remove(string path)
    {
        throw new NotSupportedException();
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return _pakFile.LastWriteTimeUtc;
    }

    public void Refresh()
    {
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);

        _records.Clear();
        foreach (var record in reader.ReadFiles())
        {
            var normalizedPath = record.Path.Replace('\\', '/');
            var key = string.IsNullOrEmpty(_musicPrefix) ? normalizedPath : $"{_musicPrefix}{normalizedPath}";
            _records[key] = record.FindExtension();
        }
    }

    private string StripMusicPrefix(string path)
    {
        if (string.IsNullOrEmpty(_musicPrefix))
        {
            return path;
        }

        return path.StartsWith(_musicPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[_musicPrefix.Length..]
            : path;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _records.Clear();
    }
}