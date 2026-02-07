using FezEditor.Components;
using FezEditor.Structure;
using FezEditor.Tools;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;

namespace FezEditor.Services;

[UsedImplicitly]
public class ResourceService : IResourceService
{
    public IResourceProvider? Provider { get; private set; }
    
    public event Action? ProviderChanged;
    
    private readonly Game _game;

    public ResourceService(Game game)
    {
        _game = game;
    }
    
    public void OpenProvider(FileSystemInfo info)
    {
        IResourceProvider provider = info switch
        {
            FileInfo file => new PakResourceProvider(file),
            DirectoryInfo dir => new DirResourceProvider(dir),
            _ => throw new ArgumentException("Not supported: " + info)
        };
        
        CloseProvider();
        Provider = provider;
        ProviderChanged?.Invoke();
    }

    public void CloseProvider()
    {
        ProviderChanged?.Invoke();
        Provider?.Dispose();
        Provider = null;
    }

    public EditorComponent CreateEditorFor(string path)
    {
        return new TestComponent(_game, path);
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        Provider?.Dispose();
    }
}