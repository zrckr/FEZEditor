namespace FezEditor.Tools;

public interface IResourceProvider : IDisposable
{
    bool IsReadonly { get; }

    string Root { get; }

    IEnumerable<string> Files { get; }

    bool Exists(string path);

    string GetFullPath(string path);

    string GetExtension(string path);

    Stream OpenStream(string path, string extension);

    T Load<T>(string path) where T : class;

    void Save<T>(string path, T asset) where T : class;

    void Move(string path, string newPath);

    void Duplicate(string path);

    void Remove(string path);

    DateTime GetLastWriteTimeUtc(string path);

    void Refresh();
}