namespace FezEditor.Structure;

public enum AssetType
{
    Trile,
    ArtObject,
    BackgroundPlane,
    NonPlayableCharacter
}

public static class AssetTypeExtensions
{
    public static string GetLabel(this AssetType type)
    {
        return type switch
        {
            AssetType.Trile => "Triles",
            AssetType.ArtObject => "Art Objects",
            AssetType.BackgroundPlane => "Planes",
            AssetType.NonPlayableCharacter => "NPCs/Critters",
            _ => throw new InvalidOperationException()
        };
    }
}