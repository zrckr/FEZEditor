namespace FezEditor.Components.Eddy;

public enum EddyContext
{
    Default,
    Trile,
    ArtObject,
    BackgroundPlane,
    NonPlayableCharacter,
    Gomez,
    Volume,
    Path,
    Script
}

public static class EddyContextExtensions
{
    public static string GetLabel(this EddyContext context)
    {
        return context switch
        {
            EddyContext.Default => "Level",
            EddyContext.Trile => "Trile",
            EddyContext.ArtObject => "Art Object",
            EddyContext.BackgroundPlane => "Background Plane",
            EddyContext.NonPlayableCharacter => "NPC",
            EddyContext.Gomez => "Gomez",
            EddyContext.Volume => "Volume",
            EddyContext.Path => "Path",
            EddyContext.Script => "Script",
            _ => throw new ArgumentOutOfRangeException(nameof(context), context, null)
        };
    }

    public static EddyContext? GetEntity(string typeName) => typeName switch
    {
        "ArtObject" => EddyContext.ArtObject,
        "Group" or "RotatingGroup" or "SuckBlock" or "Switch" or "SpinBlock" => EddyContext.Trile,
        "Npc" => EddyContext.NonPlayableCharacter,
        "Volume" => EddyContext.Volume,
        "Path" => EddyContext.Path,
        "Plane" => EddyContext.BackgroundPlane,
        "Script" => EddyContext.Script,
        _ => null
    };
}