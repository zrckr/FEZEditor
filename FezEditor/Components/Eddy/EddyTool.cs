namespace FezEditor.Components.Eddy;

internal enum EddyTool
{
    Select,
    Translate,
    Rotate,
    Scale,
    Paint,
    Pick
}

internal static class EddyToolExtensions
{
    public static string GetLabel(this EddyTool tool)
    {
        return tool switch
        {
            EddyTool.Select => "Select",
            EddyTool.Translate => "Translate",
            EddyTool.Rotate => "Rotate",
            EddyTool.Scale => "Scale",
            EddyTool.Paint => "Paint",
            EddyTool.Pick => "Pick",
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
        };
    }
}
