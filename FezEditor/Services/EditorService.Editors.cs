using FezEditor.Components;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.MapTree;
using FEZRepacker.Core.Definitions.Game.NpcMetadata;
using FEZRepacker.Core.Definitions.Game.Sky;
using FEZRepacker.Core.Definitions.Game.TrackedSong;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using FEZRepacker.Core.Definitions.Game.XNA;

namespace FezEditor.Services;

public partial class EditorService
{
    private static readonly Dictionary<string, Type> AssetTypes = new()
    {
        ["Art Object"] = typeof(ArtObject),
        ["Text Storage"] = typeof(TextStorage),
        ["Font"] = typeof(FezFont),
        ["Level"] = typeof(Level),
        ["Map"] = typeof(MapTree),
        ["NPC Metadata"] = typeof(NpcMetadata),
        ["Sky"] = typeof(Sky),
        ["Song"] = typeof(TrackedSong),
        ["Trile Set"] = typeof(TrileSet)
    };

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
            SoundEffect soundEffect => new RichardEditor(_game, path, soundEffect),
            _ => new NotSupportedComponent(_game, path, asset.GetType())
        };
    }

    public static IEnumerable<KeyValuePair<string, Type>> GetAssetTypes()
    {
        return AssetTypes;
    }

    public static string GetExtensionForType(Type assetType)
    {
        if (assetType == typeof(TrackedSong)) return "fezsong.json";
        if (assetType == typeof(TextStorage)) return "feztxt.json";
        if (assetType == typeof(FezFont)) return "fezfont.json";
        if (assetType == typeof(ArtObject)) return "fezao.glb";
        if (assetType == typeof(TrileSet)) return "fezts.glb";
        if (assetType == typeof(MapTree)) return "fezmap.json";
        if (assetType == typeof(Level)) return "fezlvl.json";
        if (assetType == typeof(Sky)) return "fezsky.json";
        if (assetType == typeof(NpcMetadata)) return "feznpc.json";
        throw new InvalidOperationException();
    }

    public static object CreateAssetOfType(Type assetType, string name)
    {
        if (assetType == typeof(TrackedSong)) return DiezEditor.Create(name);
        if (assetType == typeof(TextStorage)) return PoEditor.Create();
        if (assetType == typeof(FezFont)) return ZuEditor.Create();
        if (assetType == typeof(ArtObject)) return ChrisEditor.CreateAo(name);
        if (assetType == typeof(TrileSet)) return ChrisEditor.CreateTs(name);
        if (assetType == typeof(MapTree)) return JadeEditor.Create(name);
        throw new InvalidOperationException();
    }
}