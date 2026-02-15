using FezEditor.Tools;
using FEZRepacker.Core.Conversion;
using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;

namespace FezEditor.Services;

internal class DirResourceProvider : IResourceProvider
{
    public bool IsReadonly => false;
    
    public string Root => _directory.Name;

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
        return _files.GetValueOrDefault(path)?.FullName ?? "";
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