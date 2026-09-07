using FezEditor.Structure;
using FEZRepacker.Core.Conversion;
using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;

namespace FezEditor.Tools;

internal class DirResourceProvider : IResourceProvider
{
    public bool IsReadonly => false;

    public string RootPath => _directory.FullName;

    public IEnumerable<ResourceEntry> Entries => _entries.Select(pair => pair.Value switch
    {
        FileInfo file => (ResourceEntry)new ResourceEntry.File(pair.Key, GetFullExtension(file.FullName)),
        DirectoryInfo => new ResourceEntry.Directory(pair.Key),
        _ => throw new InvalidOperationException()
    });

    private readonly Dictionary<string, FileSystemInfo> _entries = new(StringComparer.OrdinalIgnoreCase);

    private readonly DirectoryInfo _directory;

    public DirResourceProvider(DirectoryInfo info)
    {
        if (info is not { Exists: true })
        {
            throw new DirectoryNotFoundException(info.FullName);
        }

        _directory = info;
        TempTextureTracker.CleanOrphans(_directory.FullName);
        Refresh();
    }

    public bool Exists(string path)
    {
        return _entries.ContainsKey(path);
    }

    public string GetExtension(string path)
    {
        return _entries.GetValueOrDefault(path) is FileInfo file
            ? GetFullExtension(file.FullName)
            : string.Empty;
    }

    public string GetFullPath(string path)
    {
        return _entries.TryGetValue(path, out var entry)
            ? entry.FullName
            : Path.Combine(_directory.FullName, path);
    }

    public bool IsReadonlyPath(string path)
    {
        return false;
    }

    public Stream OpenStream(string path, string extension)
    {
        if (_entries.GetValueOrDefault(path) is not FileInfo { Exists: true } info)
        {
            throw new FileNotFoundException(path);
        }

        var bundles = FileBundle.BundleFilesAtPath(info.FullName);
        try
        {
            foreach (var bundle in bundles)
            {
                if (bundle.MainExtension.Equals(extension, StringComparison.OrdinalIgnoreCase) && bundle.Files.Count == 1)
                {
                    var file = bundle.Files[0];
                    var output = new MemoryStream();
                    file.Data.CopyTo(output);
                    output.Position = 0;
                    return output;
                }
            }

            throw new FileNotFoundException(path);
        }
        finally
        {
            foreach (var bundle in bundles)
            {
                bundle.Dispose();
            }
        }
    }

    public T Load<T>(string path) where T : class
    {
        if (_entries.GetValueOrDefault(path) is not FileInfo { Exists: true } info)
        {
            throw new FileNotFoundException(path);
        }

        if (info.Extension == ".xnb")
        {
            using var xnbStream = info.Open(FileMode.Open);
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

        var bundles = FileBundle.BundleFilesAtPath(info.FullName);
        try
        {
            if (bundles.Count == 0)
            {
                throw new FileNotFoundException(info.FullName);
            }

            try
            {
                return (T)FormatConversion.Deconvert(bundles.First())!;
            }
            catch (FormatConversionException ex)
            {
                throw new NotSupportedException(path, ex);
            }
        }
        finally
        {
            foreach (var bundle in bundles)
            {
                bundle.Dispose();
            }
        }
    }

    public void Save<T>(string path, T asset) where T : class
    {
        using var bundle = FormatConversion.Convert(asset);
        bundle.BundlePath = Path.Combine(_directory.FullName, path);

        foreach (var outputFile in bundle.Files)
        {
            var fileOutputPath = bundle.BundlePath + bundle.MainExtension + outputFile.Extension;
            using var fileOutputStream = new FileInfo(fileOutputPath).Create();
            outputFile.Data.CopyTo(fileOutputStream);
        }
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(Path.Combine(_directory.FullName, path));
    }

    public void Move(string path, string newPath)
    {
        if (_entries.GetValueOrDefault(path) is DirectoryInfo directory)
        {
            var destination = Path.Combine(_directory.FullName, newPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(directory.FullName, destination);
            return;
        }

        foreach (var file in GetBundleFiles(path))
        {
            var suffix = file.Name[file.Name.IndexOf('.')..];
            var dest = Path.Combine(_directory.FullName, newPath.Replace('/', Path.DirectorySeparatorChar) + suffix);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Move(file.FullName, dest);
        }
    }

    public void Duplicate(string path)
    {
        var copyPath = path + " (copy)";
        foreach (var file in GetBundleFiles(path))
        {
            var suffix = file.Name[file.Name.IndexOf('.')..];
            var dest = Path.Combine(_directory.FullName, copyPath.Replace('/', Path.DirectorySeparatorChar) + suffix);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file.FullName, dest, overwrite: false);
        }
    }

    public void Remove(string path)
    {
        if (_entries.GetValueOrDefault(path) is DirectoryInfo directory)
        {
            Directory.Delete(directory.FullName, recursive: true);
            return;
        }

        foreach (var file in GetBundleFiles(path))
        {
            File.Delete(file.FullName);
        }
    }

    private IEnumerable<FileInfo> GetBundleFiles(string path)
    {
        if (_entries.GetValueOrDefault(path) is not FileInfo entry)
        {
            throw new FileNotFoundException(path);
        }

        var absolutePath = entry.FullName;
        var dir = Path.GetDirectoryName(absolutePath)!;
        var fileName = Path.GetFileName(absolutePath);
        var prefix = fileName[..fileName.IndexOf('.')];
        return new DirectoryInfo(dir).EnumerateFiles(prefix + ".*");
    }

    public void Refresh()
    {
        _entries.Clear();
        foreach (var directory in _directory.EnumerateDirectories("*", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(_directory.FullName, directory.FullName);
            _entries[path.Replace('\\', '/')] = directory;
        }

        foreach (var file in _directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var path = Path.GetRelativePath(_directory.FullName, file.FullName);
            if (Path.HasExtension(path))
            {
                path = path.Replace(GetFullExtension(path), "");
            }

            var normalizedPath = path.Replace('\\', '/');
            _entries[normalizedPath] = file;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _entries.Clear();
    }

    private static string GetFullExtension(string path)
    {
        var fileName = Path.GetFileName(path).AsSpan();
        var dot = fileName.IndexOf('.');
        return dot >= 0 ? fileName[dot..].ToString() : string.Empty;
    }
}