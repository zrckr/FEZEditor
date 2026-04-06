namespace FezEditor.Components.Eddy;

public enum EddyTool
{
    Select,
    Translate,
    Rotate,
    Scale,
    Paint,
    Pick
}

public static class EddyToolExtensions
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
