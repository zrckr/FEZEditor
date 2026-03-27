using System.Text.Json;
using System.Text.Json.Serialization;
using FEZRepacker.Core.Definitions.Game.Level;

namespace FezEditor.Tools;

public sealed class TrileEmplacementConverter : JsonConverter<TrileEmplacement>
{
    public override TrileEmplacement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = reader.GetString()!.Split(',');
        return new TrileEmplacement(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    public override void Write(Utf8JsonWriter writer, TrileEmplacement value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value.X},{value.Y},{value.Z}");
    }

    public override TrileEmplacement ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var parts = reader.GetString()!.Split(',');
        return new TrileEmplacement(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TrileEmplacement value, JsonSerializerOptions options)
    {
        writer.WritePropertyName($"{value.X},{value.Y},{value.Z}");
    }
}
