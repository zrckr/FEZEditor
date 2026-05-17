using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public class TrixelObject
{
    public Vector3 Size { get; private set; }

    [JsonConverter(typeof(Base64Converter))]
    public byte[] MissingTrixels { get; private set; } = Array.Empty<byte>();

    [JsonConverter(typeof(CompressConverter))]
    public RTexture2D Texture { get; set; } = new RTexture2D();

    public int Width => (int)(Size.X / Mathz.TrixelSize);

    public int Height => (int)(Size.Y / Mathz.TrixelSize);

    public int Depth => (int)(Size.Z / Mathz.TrixelSize);

    /// <summary>
    /// Enumerates all visible trixel faces for rendering. A face is visible if the trixel exists
    /// (not missing) and is either on the object boundary or adjacent to a missing trixel.
    /// </summary>
    public IEnumerable<TrixelFace> VisibleFaces
    {
        get
        {
            var w = Width;
            var h = Height;
            var d = Depth;

            // Boundary faces: trixels on the 6 outer surfaces of the bounding box.
            // These are always visible (no neighbor on the outside), so only check if trixel exists.

            // Front (z = d-1, normal = -Z) / Back (z = 0, normal = +Z)
            for (var x = 0; x < w; x++)
            {
                for (var y = 0; y < h; y++)
                {
                    if (!IsMissing(new Vector3I(x, y, d - 1)))
                    {
                        yield return new TrixelFace(new Vector3I(x, y, d - 1), FaceOrientation.Front);
                    }

                    if (!IsMissing(new Vector3I(x, y, 0)))
                    {
                        yield return new TrixelFace(new Vector3I(x, y, 0), FaceOrientation.Back);
                    }
                }
            }

            // Right (x = w-1, normal = +X) / Left (x = 0, normal = -X)
            for (var y = 0; y < h; y++)
            {
                for (var z = 0; z < d; z++)
                {
                    if (!IsMissing(new Vector3I(w - 1, y, z)))
                    {
                        yield return new TrixelFace(new Vector3I(w - 1, y, z), FaceOrientation.Right);
                    }

                    if (!IsMissing(new Vector3I(0, y, z)))
                    {
                        yield return new TrixelFace(new Vector3I(0, y, z), FaceOrientation.Left);
                    }
                }
            }

            // Top (y = h-1, normal = +Y) / Down (y = 0, normal = -Y)
            for (var x = 0; x < w; x++)
            {
                for (var z = 0; z < d; z++)
                {
                    if (!IsMissing(new Vector3I(x, h - 1, z)))
                    {
                        yield return new TrixelFace(new Vector3I(x, h - 1, z), FaceOrientation.Top);
                    }

                    if (!IsMissing(new Vector3I(x, 0, z)))
                    {
                        yield return new TrixelFace(new Vector3I(x, 0, z), FaceOrientation.Down);
                    }
                }
            }

            // Inner faces: exposed when a missing trixel is adjacent to an existing one.
            // For each missing trixel, check all 6 neighbors - if the neighbor exists and
            // is within bounds, emit a face on that neighbor pointing toward the void.
            // The face orientation is the direction FROM the neighbor TOWARD the missing trixel
            for (var x = 0; x < w; x++)
            {
                for (var y = 0; y < h; y++)
                {
                    for (var z = 0; z < d; z++)
                    {
                        var emplacement = new Vector3I(x, y, z);
                        if (!IsMissing(emplacement))
                        {
                            continue;
                        }

                        if (z + 1 < d && !IsMissing(new Vector3I(x, y, z + 1)))
                        {
                            yield return new TrixelFace(new Vector3I(x, y, z + 1), FaceOrientation.Back);
                        }

                        if (z - 1 >= 0 && !IsMissing(new Vector3I(x, y, z - 1)))
                        {
                            yield return new TrixelFace(new Vector3I(x, y, z - 1), FaceOrientation.Front);
                        }

                        if (x + 1 < w && !IsMissing(new Vector3I(x + 1, y, z)))
                        {
                            yield return new TrixelFace(new Vector3I(x + 1, y, z), FaceOrientation.Left);
                        }

                        if (x - 1 >= 0 && !IsMissing(new Vector3I(x - 1, y, z)))
                        {
                            yield return new TrixelFace(new Vector3I(x - 1, y, z), FaceOrientation.Right);
                        }

                        if (y + 1 < h && !IsMissing(new Vector3I(x, y + 1, z)))
                        {
                            yield return new TrixelFace(new Vector3I(x, y + 1, z), FaceOrientation.Down);
                        }

                        if (y - 1 >= 0 && !IsMissing(new Vector3I(x, y - 1, z)))
                        {
                            yield return new TrixelFace(new Vector3I(x, y - 1, z), FaceOrientation.Top);
                        }
                    }
                }
            }
        }
    }

    public TrixelObject(Vector3 size)
    {
        Size = size;
        var needed = ((Width * Height * Depth) + 7) / 8;
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
        var needed = ((w * h * d) + 7) / 8;

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
                    var oldI = x + (y * oldW) + (z * oldW * oldH);
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
        return emplacement.X + (emplacement.Y * Width) + (emplacement.Z * Width * Height);
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

    private class CompressConverter : JsonConverter<RTexture2D>
    {
        public override RTexture2D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var base64 = reader.GetString();
            if (string.IsNullOrEmpty(base64))
            {
                return new RTexture2D();
            }

            var compressed = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(compressed);
            using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return JsonSerializer.Deserialize<RTexture2D>(output.ToArray())!;
        }

        public override void Write(Utf8JsonWriter writer, RTexture2D value, JsonSerializerOptions options)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(value);
            using var ms = new MemoryStream();
            using (var deflate = new DeflateStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            {
                deflate.Write(json);
            }

            writer.WriteStringValue(Convert.ToBase64String(ms.ToArray()));
        }
    }
}