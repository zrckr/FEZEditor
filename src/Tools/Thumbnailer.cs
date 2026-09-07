using System.Security.Cryptography;
using System.Text;
using FezEditor.Services;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace FezEditor.Tools;

public class Thumbnailer
{
    private const int BytesPerPixel = 4;

    private static readonly Lock DirtySync = new();

    private static readonly Dictionary<string, long> DirtyRevisions = new(StringComparer.Ordinal);

    private static long s_revision;

    private readonly RTexture2D _source;

    private readonly string _thumbPath;

    private readonly string _path;

    private readonly long _revision;

    public Thumbnailer(string path, ArtObject ao) : this(path)
    {
        var cubemap = ao.Cubemap;
        if (cubemap == null)
        {
            return;
        }

        var faceWidth = cubemap.Width / 6;
        var faceRect = new Rectangle(0, 0, faceWidth, cubemap.Height);
        var data = CropRawRegion(cubemap.TextureData, cubemap.Width, faceRect);
        SetOpaqueAlpha(data);
        _source = new RTexture2D
        {
            Width = faceWidth,
            Height = cubemap.Height,
            TextureData = data
        };
    }

    public Thumbnailer(string path, Trile trile, RTexture2D? atlas) : this(path)
    {
        if (atlas == null)
        {
            return;
        }

        var px = (int)MathF.Round(trile.AtlasOffset.X * atlas.Width);
        var py = (int)MathF.Round(trile.AtlasOffset.Y * atlas.Height);
        var rect = new Rectangle(px + 1, py + 1, 16, 16);
        var data = CropRawRegion(atlas.TextureData, atlas.Width, rect);
        SetOpaqueAlpha(data);
        _source = new RTexture2D
        {
            Width = 16,
            Height = 16,
            TextureData = data
        };
    }

    public Thumbnailer(string path, RTexture2D texture) : this(path)
    {
        _source = texture;
    }

    public Thumbnailer(string path, RAnimatedTexture anim) : this(path)
    {
        var frame = anim.Frames[0].Rectangle.ToXna();
        _source = new RTexture2D
        {
            Width = frame.Width,
            Height = frame.Height,
            TextureData = CropRawRegion(anim.TextureData, anim.AtlasWidth, frame)
        };
    }

    public Thumbnailer(string path)
    {
        _path = Path.Normalize(path);
        _source = new RTexture2D();
        lock (DirtySync)
        {
            _revision = DirtyRevisions.GetValueOrDefault(_path);
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(_path));
        _thumbPath = Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public static void MarkDirty(string path)
    {
        path = Path.Normalize(path);
        lock (DirtySync)
        {
            DirtyRevisions[path] = ++s_revision;
        }
    }

    public static void ResetDirty()
    {
        lock (DirtySync)
        {
            DirtyRevisions.Clear();
        }
    }

    public bool NeedsGeneration()
    {
        if (!AppStorageService.HasThumb(_thumbPath))
        {
            return true;
        }

        lock (DirtySync)
        {
            return DirtyRevisions.ContainsKey(_path);
        }
    }

    public bool TryLoad(out RTexture2D? texture)
    {
        if (!AppStorageService.HasThumb(_thumbPath))
        {
            texture = null;
            return false;
        }

        using var image = Image.Load<Rgba32>(AppStorageService.LoadThumb(_thumbPath));
        var data = new byte[image.Width * image.Height * BytesPerPixel];
        image.CopyPixelDataTo(data);

        texture = new RTexture2D
        {
            Width = image.Width,
            Height = image.Height,
            TextureData = data
        };
        return true;
    }

    public RTexture2D Generate()
    {
        using var image = Image.LoadPixelData<Rgba32>(_source.TextureData, _source.Width, _source.Height);
        var data = new byte[_source.Width * _source.Height * BytesPerPixel];
        image.CopyPixelDataTo(data);

        return new RTexture2D
        {
            Width = image.Width,
            Height = image.Height,
            TextureData = data
        };
    }

    public void Save(RTexture2D texture)
    {
        using var image = Image.LoadPixelData<Rgba32>(texture.TextureData, texture.Width, texture.Height);
        using var png = new MemoryStream();

        image.SaveAsPng(png);
        if (!AppStorageService.SaveThumb(_thumbPath, png))
        {
            return;
        }

        lock (DirtySync)
        {
            if (DirtyRevisions.GetValueOrDefault(_path) == _revision)
            {
                DirtyRevisions.Remove(_path);
            }
        }
    }

    private static void SetOpaqueAlpha(byte[] data)
    {
        for (var i = 3; i < data.Length; i += BytesPerPixel)
        {
            data[i] = 255;
        }
    }

    private static byte[] CropRawRegion(byte[] data, int stride, Rectangle rect)
    {
        var result = new byte[rect.Width * rect.Height * BytesPerPixel];
        for (var row = 0; row < rect.Height; row++)
        {
            var srcOffset = (((rect.Y + row) * stride) + rect.X) * BytesPerPixel;
            var dstOffset = row * rect.Width * BytesPerPixel;
            Buffer.BlockCopy(data, srcOffset, result, dstOffset, rect.Width * BytesPerPixel);
        }

        return result;
    }
}
