using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    private readonly RTexture2D _source;

    private readonly string _thumbPath;

    private readonly string _metaPath;

    private readonly DateTime _lastWrite;

    public Thumbnailer(string path, DateTime lastWrite, ArtObject ao) : this(path, lastWrite)
    {
        var cubemap = ao.Cubemap;
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

    public Thumbnailer(string path, DateTime lastWrite, Trile trile, RTexture2D atlas) : this(path, lastWrite)
    {
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

    public Thumbnailer(string path, DateTime lastWrite, RTexture2D texture) : this(path, lastWrite)
    {
        _source = texture;
    }

    public Thumbnailer(string path, DateTime lastWrite, RAnimatedTexture anim) : this(path, lastWrite)
    {
        var frame = anim.Frames[0].Rectangle.ToXna();
        _source = new RTexture2D
        {
            Width = frame.Width,
            Height = frame.Height,
            TextureData = CropRawRegion(anim.TextureData, anim.AtlasWidth, frame)
        };
    }

    public Thumbnailer(string path, DateTime lastWrite)
    {
        _lastWrite = lastWrite;
        _source = new RTexture2D();
        {
            var normalizedPath = path.ToLowerInvariant().Replace('\\', '/');
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(normalizedPath));
            var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            _thumbPath = $"thumb-{hash}.png";
            _metaPath = Path.ChangeExtension(_thumbPath, ".json");
        }
    }

    public void Delete()
    {
        AppStorageService.DeleteCacheFile(_thumbPath);
        AppStorageService.DeleteCacheFile(_metaPath);
    }

    public bool HasInCache()
    {
        return AppStorageService.HasCacheFile(_thumbPath) && AppStorageService.HasCacheFile(_metaPath);
    }

    public bool TryLoad(out RTexture2D? texture)
    {
        if (!HasInCache())
        {
            texture = null;
            return false;
        }

        ThumbMeta? meta;
        try
        {
            using var stream = AppStorageService.LoadFromCache(_metaPath);
            stream.Seek(0, SeekOrigin.Begin);
            meta = JsonSerializer.Deserialize<ThumbMeta>(stream);
        }
        catch
        {
            meta = null;
        }

        if (meta == null || meta.LastWrite != _lastWrite)
        {
            texture = null;
            return false;
        }

        using var image = Image.Load<Rgba32>(AppStorageService.LoadFromCache(_thumbPath));
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
        #region Thumbnail

        {
            using var image = Image.LoadPixelData<Rgba32>(texture.TextureData, texture.Width, texture.Height);
            using var png = new MemoryStream();
            image.SaveAsPng(png);
            AppStorageService.SaveToCache(_thumbPath, png);
        }

        #endregion

        #region Metadata

        {
            var meta = new ThumbMeta(_lastWrite, texture.Width, texture.Height);
            using var stream = new MemoryStream();
            JsonSerializer.Serialize(stream, meta);
            AppStorageService.SaveToCache(_metaPath, stream);
        }

        #endregion
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
            var srcOffset = ((rect.Y + row) * stride + rect.X) * BytesPerPixel;
            var dstOffset = row * rect.Width * BytesPerPixel;
            Buffer.BlockCopy(data, srcOffset, result, dstOffset, rect.Width * BytesPerPixel);
        }

        return result;
    }

    private record ThumbMeta(DateTime LastWrite, int Width, int Height); // last write datetime of asset
}