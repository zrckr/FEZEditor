using FezEditor.Components;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.MapTree;
using FEZRepacker.Core.Definitions.Game.TrackedSong;
using FEZRepacker.Core.Definitions.Game.TrileSet;

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
            ArtObject ao => new ChrisEditor(_game, path, ao),
            TrileSet ts => new ChrisEditor(_game, path, ts),
            MapTree tree => new JadeEditor(_game, path, tree),
            Level level => new EddyEditor(_game, path, level),
            _ => new NotSupportedComponent(_game, path, asset.GetType())
        };
    }
}