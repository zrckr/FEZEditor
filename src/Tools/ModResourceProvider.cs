using FezEditor.Services;
using FezEditor.Structure;

namespace FezEditor.Tools;

internal class ModResourceProvider : IResourceProvider
{
    private const string ReferencesVirtualPathPrefix = "References/";

    public bool IsReadonly => false;

    public string RootPath => _inner.RootPath;

    public ModDirectoryResolution Resolution { get; }

    public IEnumerable<ResourceEntry> Entries => _inner.Entries.Union(_referenceEntries);

    public IEnumerable<ResourceEntry> VirtualEntries => _inner.Entries.Concat(_referenceVirtualEntries);

    public IReadOnlyList<IResourceProvider> References => _references;

    private readonly DirResourceProvider _inner;

    private readonly AppStorageService _storage;

    private readonly List<IResourceProvider> _references = new();

    private readonly Dictionary<string, IResourceProvider> _referenceLookup = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ResourceEntry> _referenceEntries = new();

    private readonly List<ResourceEntry> _referenceVirtualEntries = new();

    public ModResourceProvider(DirectoryInfo dir, AppStorageService storage)
    {
        var resolution = ModDirectoryResolution.Resolve(dir);
        if (resolution is ModDirectoryResolution.Invalid invalid)
        {
            throw new DirectoryNotFoundException(invalid.Original.FullName);
        }

        Resolution = resolution;
        _inner = new DirResourceProvider(resolution.AssetsDirectory);
        _storage = storage;

        var savedPaths = storage.GetReferenceProviders(_inner.RootPath);
        if (savedPaths.Count > 0)
        {
            LoadReferences(SortPakPaths(savedPaths.Where(p => File.Exists(p) || Directory.Exists(p))));
        }
    }

    public void UpdateReferences(IEnumerable<string> paths)
    {
        var validPaths = SortPakPaths(paths.Where(p => File.Exists(p) || Directory.Exists(p))).ToList();
        _storage.SetReferenceProviders(_inner.RootPath, validPaths);
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
        _referenceEntries.Clear();
        _referenceVirtualEntries.Clear();

        var entryIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in _references)
        {
            foreach (var entry in reference.Entries)
            {
                ResourceEntry virtualEntry = entry switch
                {
                    ResourceEntry.File file => new ResourceEntry.File(
                        ReferencesVirtualPathPrefix + file.Path, file.Extension),

                    ResourceEntry.Directory directory => new ResourceEntry.Directory(
                        ReferencesVirtualPathPrefix + directory.Path),

                    _ => throw new InvalidOperationException()
                };

                var entryLookupKey = entry.Path + (entry is ResourceEntry.Directory ? "/" : "");

                if (entryIndices.TryGetValue(entryLookupKey, out var index))
                {
                    _referenceEntries[index] = entry;
                    _referenceVirtualEntries[index] = virtualEntry;
                }
                else
                {
                    entryIndices[entryLookupKey] = _referenceEntries.Count;
                    _referenceEntries.Add(entry);
                    _referenceVirtualEntries.Add(virtualEntry);
                }

                _referenceLookup[entry.Path] = reference;
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

    public bool IsReadonlyPath(string path)
    {
        return path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase);
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

    public void Save<T>(string path, T asset) where T : class
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.Save(path, asset);
        RebuildReferenceLookup();
    }

    public void CreateDirectory(string path)
    {
        if (path.StartsWith(ReferencesVirtualPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException();
        }

        _inner.CreateDirectory(path);
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