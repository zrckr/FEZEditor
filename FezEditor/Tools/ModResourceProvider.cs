using FezEditor.Services;

namespace FezEditor.Tools;

internal class ModResourceProvider : IResourceProvider
{
    private const string ReferencesPrefix = "References/";

    public bool IsReadonly => false;

    public string Root => _inner.Root;

    public IEnumerable<string> Files => _inner.Files.Concat(_referenceFiles);

    public IReadOnlyList<IResourceProvider> References => _references;

    private readonly DirResourceProvider _inner;

    private readonly AppStorageService _storage;

    private readonly List<IResourceProvider> _references = new();

    private Dictionary<string, IResourceProvider> _index = new(StringComparer.OrdinalIgnoreCase);

    private List<string> _referenceFiles = new();

    public ModResourceProvider(DirectoryInfo dir, AppStorageService storage)
    {
        _inner = new DirResourceProvider(dir);
        _storage = storage;

        var savedPaths = storage.GetReferenceProviders(dir.FullName);
        if (savedPaths.Count > 0)
        {
            LoadReferences(SortPakPaths(savedPaths.Where(p => File.Exists(p) || Directory.Exists(p))));
        }
    }

    public void UpdateReferences(IEnumerable<string> paths)
    {
        var validPaths = SortPakPaths(paths.Where(p => File.Exists(p) || Directory.Exists(p))).ToList();
        var rootPath = _inner.GetFullPath(string.Empty);
        _storage.SetReferenceProviders(rootPath, validPaths);
        LoadReferences(validPaths);
    }

    private void LoadReferences(IEnumerable<string> paths)
    {
        foreach (var r in _references)
        {
            r.Dispose();
        }

        _references.Clear();
        foreach (var path in paths)
        {
            IResourceProvider provider = Directory.Exists(path)
                ? new DirResourceProvider(new DirectoryInfo(path))
                : new PakResourceProvider(new FileInfo(path));
            _references.Add(provider);
        }

        RebuildIndex();
    }

    private void RebuildIndex()
    {
        var index = new Dictionary<string, IResourceProvider>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refFiles = new List<string>();

        foreach (var reference in _references)
        {
            foreach (var path in reference.Files)
            {
                index[path] = reference;
                if (seen.Add(path))
                {
                    refFiles.Add(ReferencesPrefix + path);
                }
            }
        }

        foreach (var path in _inner.Files)
        {
            index[path] = _inner;
        }

        _index = index;
        _referenceFiles = refFiles;
    }

    public bool Exists(string path)
    {
        return _index.ContainsKey(StripReferencesPrefix(path));
    }

    public string GetExtension(string path)
    {
        var resolved = StripReferencesPrefix(path);
        return _index.TryGetValue(resolved, out var rp)
            ? rp.GetExtension(resolved)
            : string.Empty;
    }

    public Stream OpenStream(string path, string extension)
    {
        var resolved = StripReferencesPrefix(path);
        return _index.TryGetValue(resolved, out var rp)
            ? rp.OpenStream(resolved, extension)
            : throw new FileNotFoundException(path);
    }

    public T Load<T>(string path) where T : class
    {
        var resolved = StripReferencesPrefix(path);
        return _index.TryGetValue(resolved, out var rp)
            ? rp.Load<T>(resolved)
            : throw new FileNotFoundException(path);
    }

    public string GetFullPath(string path)
    {
        var resolved = StripReferencesPrefix(path);
        return _index.TryGetValue(resolved, out var rp)
            ? rp.GetFullPath(resolved)
            : _inner.GetFullPath(resolved);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        var resolved = StripReferencesPrefix(path);
        return _index.TryGetValue(resolved, out var rp)
            ? rp.GetLastWriteTimeUtc(resolved)
            : DateTime.MinValue;
    }

    public void Save<T>(string path, T asset) where T : class
    {
        if (path.StartsWith(ReferencesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Save(path, asset);
        RebuildIndex();
    }

    public void Move(string path, string newPath)
    {
        if (path.StartsWith(ReferencesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Move(path, newPath);
        RebuildIndex();
    }

    public void Duplicate(string path)
    {
        if (path.StartsWith(ReferencesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Duplicate(path);
        RebuildIndex();
    }

    public void Remove(string path)
    {
        if (path.StartsWith(ReferencesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Remove(path);
        RebuildIndex();
    }

    public void Refresh()
    {
        _inner.Refresh();
        RebuildIndex();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _inner.Dispose();
        foreach (var r in _references)
        {
            r.Dispose();
        }

        _references.Clear();
    }

    public void CopyToMod(string path)
    {
        var relativePath = StripReferencesPrefix(path);
        if (!_index.TryGetValue(relativePath, out var source))
        {
            throw new FileNotFoundException(path);
        }

        var sourceFullPath = source.GetFullPath(relativePath);
        var destFullPath = _inner.GetFullPath(relativePath);

        if (File.Exists(sourceFullPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destFullPath)!);
            File.Copy(sourceFullPath, destFullPath, overwrite: true);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destFullPath)!);
            var asset = source.Load<object>(relativePath);
            _inner.Save(relativePath, asset);
        }

        _inner.Refresh();
        RebuildIndex();
    }

    private static string StripReferencesPrefix(string path)
    {
        return path.StartsWith(ReferencesPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[ReferencesPrefix.Length..]
            : path;
    }

    private static IEnumerable<string> SortPakPaths(IEnumerable<string> paths)
    {
        return paths.OrderBy(p =>
        {
            var name = Path.GetFileNameWithoutExtension(p);
            return name switch
            {
                "Essentials" => 0,
                "Music" => 1,
                "Other" => 2,
                "Updates" => 3,
                _ => 2
            };
        });
    }
}