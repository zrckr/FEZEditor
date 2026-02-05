using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;
using JetBrains.Annotations;

namespace FezEditor.Services;

[UsedImplicitly]
public class PakResourceService : IResourceService
{
    public bool IsReadonly => true;

    public string Root => _pakFile.Name;

    public IEnumerable<string> Files => _records.Keys;
    
    private readonly Dictionary<string, string> _records = new(StringComparer.OrdinalIgnoreCase);
    
    private FileInfo _pakFile = null!;

    public void Initialize(FileSystemInfo info)
    {
        if (info is not FileInfo { Extension: ".pak", Exists: true } pakFile)
        {
            throw new FileNotFoundException(info.FullName);
        }

        _pakFile = pakFile;
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
        return _records.TryGetValue(path, out var extension)
            ? Path.Combine(_pakFile.Name, path + extension)
            : "";
    }

    public T Load<T>(string path) where T : class
    {
        if (!Exists(path))
        {
            throw new FileNotFoundException(path);
        }
        
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);
        
        var record = reader.ReadFiles().FirstOrDefault(r => r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (record == null)
        {
            throw new FileNotFoundException(path);
        }

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

    public void Refresh()
    {
        using var stream = _pakFile.OpenRead();
        using var reader = new PakReader(stream);

        _records.Clear();
        foreach (var record in reader.ReadFiles())
        {
            _records[record.Path] = record.FindExtension();
        }
    }
}