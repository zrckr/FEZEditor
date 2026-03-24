using System.Text.Json.Serialization;
using FezEditor.Services;

namespace FezEditor.Structure;

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public record Settings
{
    public static readonly string FilePath = Path.Combine(AppStorageService.BaseDir, "Settings.json");

    public List<RecentProvider> RecentProviders { get; init; } = new();

    public Dictionary<string, List<string>> RecentFiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public WindowSize Window { get; init; } = new(1280, 720);

    public record RecentProvider(string Path, string Kind);

    public record WindowSize(int Width, int Height);
}