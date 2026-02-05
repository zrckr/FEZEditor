namespace FezEditor.Services;

public interface IResourceService
{
    bool IsReadonly { get; }
    
    string Root { get; }
    
    IEnumerable<string> Files { get; }

    void Initialize(FileSystemInfo info);

    bool Exists(string path);
    
    string GetFullPath(string path);
    
    string GetExtension(string path);
    
    T Load<T>(string path) where T : class;
    
    void Refresh();
}