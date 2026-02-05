using FezEditor.Tools;
using FEZRepacker.Core.Conversion;
using FEZRepacker.Core.FileSystem;
using FEZRepacker.Core.XNB;
using JetBrains.Annotations;

namespace FezEditor.Services;

[UsedImplicitly]
public class DirResourceService : IResourceService
{
    public bool IsReadonly => false;
    
    public string Root => _directory.Name;

    public IEnumerable<string> Files => _files.Keys;

    private readonly Dictionary<string, FileInfo> _files = new(StringComparer.OrdinalIgnoreCase);

    private DirectoryInfo _directory = null!;

    public void Initialize(FileSystemInfo info)
    {
        if (info is not DirectoryInfo directoryInfo)
        {
            throw new DirectoryNotFoundException(info.FullName);
        }

        _directory = directoryInfo;
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

    public void Refresh()
    {
        _files.Clear();
        foreach (var file in _directory.EnumerateFiles("*", SearchOption.AllDirectories))
        {
            var path = file.FullName.WithoutBaseDirectory(_directory.FullName);
            var filePath = path.Replace(path.GetExtension(), "");
            _files[filePath] = file;
        }
    }
}