namespace FezEditor.Tools;

public static class StringExtensions
{
    public static string GetExtension(this string path)
    {
        var extension= path.IndexOf('.');
        if (extension is -1 or 0) return "";
        return path[extension..];
    }
    
    public static string WithoutBaseDirectory(this string instance, string baseDirectory)
    {
        return baseDirectory.Length <= 0
            ? instance
            : instance[(baseDirectory.Length + 1)..];
    }
}