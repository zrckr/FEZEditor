namespace FezEditor.Components.Chris;

public enum ChrisTool
{
    Select,
    Extrude,
    Paint,
    Bucket,
    Pick
}

public static class ChrisToolExtensions
{
    public static string GetLabel(this ChrisTool tool)
    {
        return tool switch
        {
            ChrisTool.Select => "Select",
            ChrisTool.Extrude => "Extrude",
            ChrisTool.Paint => "Paint",
            ChrisTool.Bucket => "Bucket",
            ChrisTool.Pick => "Pick",
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
        };
    }
}
