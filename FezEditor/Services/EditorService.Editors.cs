using FezEditor.Components;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.TrackedSong;

namespace FezEditor.Services;

public partial class EditorService
{
    private EditorComponent CreateEditorFor(object asset, string path)
    {
        return asset switch
        {
            TrackedSong song => new DiezEditor(_game, path, song),
            TextStorage text => new PoEditor(_game, path, text),
            FezFont font => new ZuEditor(_game, path, font),
            SaveData saveData => new SallyEditor(_game, path, saveData),
            _ => new NotSupportedComponent(_game, path, asset.GetType())
        };
    }
}