namespace FezEditor.Components.Chris;

public enum ChrisTool
{
    Look,
    Add,
    Remove,
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
            ChrisTool.Look => "Look",
            ChrisTool.Add => "Add",
            ChrisTool.Remove => "Remove",
            ChrisTool.Paint => "Paint",
            ChrisTool.Bucket => "Bucket",
            ChrisTool.Pick => "Pick",
            _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null)
        };
    }

    public static bool IsTextureTool(this ChrisTool tool)
    {
        return tool is ChrisTool.Paint or ChrisTool.Bucket;
    }
}
