namespace FezEditor.Tools;

public static class PathExtensions
{
    extension(Path)
    {
        public static string Normalize(string path)
        {
            return path.Replace('\\', '/').ToLowerInvariant();
        }
    }
}