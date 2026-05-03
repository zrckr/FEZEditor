using FezEditor.Services;

namespace FezEditor.Tools;

internal class ModResourceProvider : IResourceProvider
{
    private const string ReferencesVirtualPathPrefix = "References/";

    public bool IsReadonly => false;

    public string Root => _inner.Root;

    public IEnumerable<string> Files => _inner.Files.Union(_referenceFiles);
    public IEnumerable<string> VirtualFiles => _inner.Files.Concat(_referenceVirtualFiles);

    public IReadOnlyList<IResourceProvider> References => _references;

    private readonly DirResourceProvider _inner;

    private readonly AppStorageService _storage;

    private readonly List<IResourceProvider> _references = new();

    private readonly Dictionary<string, IResourceProvider> _referenceLookup = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _referenceFiles = new();

    private readonly List<string> _referenceVirtualFiles = new();

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

        RebuildReferenceLookup();
    }

    private void RebuildReferenceLookup()
    {
        _referenceLookup.Clear();
        _referenceFiles.Clear();
        _referenceVirtualFiles.Clear();

        foreach (var reference in _references)
        {
            foreach (var path in reference.Files)
            {
                if (!_referenceLookup.ContainsKey(path))
                {
                    _referenceFiles.Add(path);
                    _referenceVirtualFiles.Add(ReferencesVirtualPathPrefix + path);
                }
                _referenceLookup[path] = reference;
            }
        }
    }

    private bool TryGetProviderForPath(string path, out IResourceProvider provider, out string resolvedPath)
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            resolvedPath = StripReferencesVirtualPrefix(path);
            if (_referenceLookup.TryGetValue(resolvedPath, out provider!))
            {
                return true;
            }
        }

        resolvedPath = path;

        if (_inner.Exists(resolvedPath))
        {
            provider = _inner;
            return true;
        }

        return _referenceLookup.TryGetValue(resolvedPath, out provider!);
    }

    public bool Exists(string path)
    {
        return TryGetProviderForPath(path, out _, out _);
    }

    public string GetExtension(string path)
    {
        return TryGetProviderForPath(path, out var rp, out var resolved)
            ? rp.GetExtension(resolved)
            : string.Empty;
    }

    public Stream OpenStream(string path, string extension)
    {
        return TryGetProviderForPath(path, out var rp, out var resolved)
            ? rp.OpenStream(resolved, extension)
            : throw new FileNotFoundException(path);
    }

    public T Load<T>(string path) where T : class
    {
        return TryGetProviderForPath(path, out var rp, out var resolved)
            ? rp.Load<T>(resolved)
            : throw new FileNotFoundException(path);
    }

    public string GetFullPath(string path)
    {
        return TryGetProviderForPath(path, out var rp, out var resolved)
            ? rp.GetFullPath(resolved)
            : _inner.GetFullPath(resolved);
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return TryGetProviderForPath(path, out var rp, out var resolved)
            ? rp.GetLastWriteTimeUtc(resolved)
            : DateTime.MinValue;
    }

    public void Save<T>(string path, T asset) where T : class
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Save(path, asset);
        RebuildReferenceLookup();
    }

    public void Move(string path, string newPath)
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Move(path, newPath);
        RebuildReferenceLookup();
    }

    public void Duplicate(string path)
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Duplicate(path);
        RebuildReferenceLookup();
    }

    public void Remove(string path)
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Remove(path);
        RebuildReferenceLookup();
    }

    public void Refresh()
    {
        _inner.Refresh();
        RebuildReferenceLookup();
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
        var relativePath = StripReferencesVirtualPrefix(path);
        if (!_referenceLookup.TryGetValue(relativePath, out var source))
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
        RebuildReferenceLookup();
    }

    private static string StripReferencesVirtualPrefix(string path)
    {
        return path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[ReferencesVirtualPathPrefix.Length..]
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