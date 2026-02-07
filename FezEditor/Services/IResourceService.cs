using FezEditor.Components;
using FezEditor.Structure;

namespace FezEditor.Services;

public interface IResourceService : IDisposable
{
    IResourceProvider? Provider { get; }

    event Action? ProviderChanged;

    void OpenProvider(FileSystemInfo info);

    void CloseProvider();
    
    EditorComponent CreateEditorFor(string path);
}