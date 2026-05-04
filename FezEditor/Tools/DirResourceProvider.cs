using FEZRepacker.Core.Conversion;
using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;

namespace FezEditor.Tools;

internal class DirResourceProvider : IResourceProvider
{
    public bool IsReadonly => false;

    public string RootPath => _directory.FullName;

    public IEnumerable<string> Files => _files.Keys;

    private readonly Dictionary<string, FileInfo> _files = new(StringComparer.OrdinalIgnoreCase);

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
        return _files.ContainsKey(path);
    }

    public string GetExtension(string path)
    {
        return _files.GetValueOrDefault(path)?.FullName.GetExtension() ?? "";
    }

    public string GetFullPath(string path)
    {
        return _files.TryGetValue(path, out var fileInfo)
            ? fileInfo.FullName
            : Path.Combine(_directory.FullName, path);
    }

    public Stream OpenStream(string path, string extension)
    {
        var info = _files.GetValueOrDefault(path);
        if (info is not { Exists: true })
        {
            throw new FileNotFoundException(path);
        }

        var bundles = FileBundle.BundleFilesAtPath(info.FullName);
        foreach (var bundle in bundles)
        {
            if (bundle.MainExtension.Equals(extension, StringComparison.OrdinalIgnoreCase) && bundle.Files.Count == 1)
            {
                var file = bundle.Files[0];
                return file.Data;
            }
        }

        throw new FileNotFoundException(path);
    }

    public T Load<T>(string path) where T : class
    {
        var info = _files.GetValueOrDefault(path);
        if (info is not { Exists: true })
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
        if (bundles.Count == 0)
        {
            throw new FileNotFoundException(info.FullName);
        }

        return (T)FormatConversion.Deconvert(bundles.First())!;
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

    public void Move(string path, string newPath)
    {
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
        foreach (var file in GetBundleFiles(path))
        {
            File.Delete(file.FullName);
        }
    }

    private IEnumerable<FileInfo> GetBundleFiles(string path)
    {
        var absolutePath = GetFullPath(path);
        var dir = Path.GetDirectoryName(absolutePath)!;
        var fileName = Path.GetFileName(absolutePath);
        var prefix = fileName[..fileName.IndexOf('.')];
        return new DirectoryInfo(dir).EnumerateFiles(prefix + ".*");
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        return _files.TryGetValue(path, out var info) ? info.LastWriteTimeUtc : DateTime.MinValue;
    }

    public void Refresh()
    {
        _files.Clear();
        foreach (var file in _directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var path = file.FullName.WithoutBaseDirectory(_directory.FullName);
            if (Path.HasExtension(path))
            {
                path = path.Replace(path.GetExtension(), "");
            }

            var normalizedPath = path.Replace('\\', '/');
            _files[normalizedPath] = file;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _files.Clear();
    }
}