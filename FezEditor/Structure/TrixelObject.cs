using System.Text.Json;
using System.Text.Json.Serialization;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public class TrixelObject
{
    public Vector3 Size { get; private set; }

    [JsonConverter(typeof(Base64Converter))]
    public byte[] MissingTrixels { get; private set; } = Array.Empty<byte>();

    public int Width => (int)(Size.X / Mathz.TrixelSize);

    public int Height => (int)(Size.Y / Mathz.TrixelSize);

    public int Depth => (int)(Size.Z / Mathz.TrixelSize);

    public TrixelObject(Vector3 size)
    {
        Size = size;
        var needed = (Width * Height * Depth + 7) / 8;
        if (MissingTrixels.Length != needed)
        {
            MissingTrixels = new byte[needed];
        }
    }

    public void Resize(Vector3 newSize)
    {
        if (Size != newSize)
        {
            var oldW = Width;
            var oldH = Height;
            var oldD = Depth;
            Size = newSize;
            ReallocateBitset(oldW, oldH, oldD);
        }
    }

    public bool IsMissing(Vector3I emplacement)
    {
        var i = BitIndex(emplacement);
        return (MissingTrixels[i >> 3] & (1 << (i & 7))) != 0;
    }

    public void SetMissing(Vector3I emplacement, bool missing)
    {
        var i = BitIndex(emplacement);
        if (missing)
        {
            MissingTrixels[i >> 3] |= (byte)(1 << (i & 7));
        }
        else
        {
            MissingTrixels[i >> 3] &= (byte)~(1 << (i & 7));
        }
    }

    private void ReallocateBitset(int oldW, int oldH, int oldD)
    {
        var w = Width;
        var h = Height;
        var d = Depth;
        var needed = (w * h * d + 7) / 8;

        if (MissingTrixels.Length == needed)
        {
            return;
        }

        var oldBytes = MissingTrixels;
        MissingTrixels = new byte[needed];

        var copyW = Math.Min(w, oldW);
        var copyH = Math.Min(h, oldH);
        var copyD = Math.Min(d, oldD);

        for (var x = 0; x < copyW; x++)
        {
            for (var y = 0; y < copyH; y++)
            {
                for (var z = 0; z < copyD; z++)
                {
                    var oldI = x + y * oldW + z * oldW * oldH;
                    if ((oldBytes[oldI >> 3] & (1 << (oldI & 7))) != 0)
                    {
                        SetMissing(new Vector3I(x, y, z), true);
                    }
                }
            }
        }
    }

    private int BitIndex(Vector3I emplacement)
    {
        return emplacement.X + emplacement.Y * Width + emplacement.Z * Width * Height;
    }

    private class Base64Converter : JsonConverter<byte[]>
    {
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var base64 = reader.GetString();
            return string.IsNullOrEmpty(base64)
                ? Array.Empty<byte>()
                : Convert.FromBase64String(base64);
        }

        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(Convert.ToBase64String(value));
        }
    }
}