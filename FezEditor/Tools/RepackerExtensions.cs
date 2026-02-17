using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Tools;

public static class RepackerExtensions
{
    private const int TrileTextureWidth = 108;

    private const int TrileTextureHeight = 18;

    public static GraphicsDevice? Gd { private get; set; }
    
    public static MeshSurface ConvertToMesh(VertexInstance[] vertices, ushort[] indices)
    {
        return new MeshSurface
        {
            Vertices = vertices.Select(i => i.Position.ToXna()).ToArray(),
            Normals = vertices.Select(i => i.Normal.ToXna()).ToArray(),
            TexCoords = vertices.Select(i => i.TextureCoordinate.ToXna()).ToArray(),
            Indices = indices.Select(i => (int)i).ToArray()
        };
    }
    
    public static Texture2D ConvertToTexture2D(RTexture2D texture)
    {
        var tex2D = new Texture2D(Gd, texture.Width, texture.Height, false, SurfaceFormat.Color);
        var data = new byte[texture.TextureData.Length];
        Array.Copy(texture.TextureData, data, texture.TextureData.Length);
        tex2D.SetData(data);
        return tex2D;
    }

    public static Texture2D ConvertToTexture2D(RAnimatedTexture texture)
    {
        var tex2D = new Texture2D(Gd, texture.AtlasWidth, texture.AtlasHeight, false, SurfaceFormat.Color);
        var data = new byte[texture.TextureData.Length];
        Array.Copy(texture.TextureData, data, texture.TextureData.Length);
        tex2D.SetData(data);
        return tex2D;
    }
    
    public static Texture2D ConvertToTextureAtlas(RTexture2D texture, Vector2 atlasOffset)
    {
        var pixelX = (int)MathF.Round(atlasOffset.X * texture.Width);
        var pixelY = (int)MathF.Round(atlasOffset.Y * texture.Height);

        var atlas = new byte[TrileTextureWidth * TrileTextureHeight * 4];
        for (var row = 0; row < TrileTextureHeight; row++)
        {
            var srcOffset = ((pixelY + row) * texture.Width + pixelX) * 4;
            var dstOffset = row * TrileTextureWidth * 4;
            Buffer.BlockCopy(texture.TextureData, srcOffset, atlas, dstOffset, TrileTextureWidth * 4);
        }

        var tex2D = new Texture2D(Gd, TrileTextureWidth, TrileTextureHeight, false, SurfaceFormat.Color);
        tex2D.SetData(atlas);
        return tex2D;
    }
    
    public static Vector2 ToXna(this RVector2 v) => new(v.X, v.Y);
    public static Vector3 ToXna(this RVector3 v) => new(v.X, v.Y, v.Z);
    public static Vector4 ToXna(this RVector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static Quaternion ToXna(this RQuaternion q) => new(q.X, q.Y, q.Z, q.W);
    public static Color ToXna(this RColor c) => new(c.R, c.G, c.B, c.A);
    public static Rectangle ToXna(this RRectangle r) => new(r.X, r.Y, r.Width, r.Height);

    public static RVector2 ToRepacker(this Vector2 v) => new(v.X, v.Y);
    public static RVector3 ToRepacker(this Vector3 v) => new(v.X, v.Y, v.Z);
    public static RVector4 ToRepacker(this Vector4 v) => new(v.X, v.Y, v.Z, v.W);
    public static RQuaternion ToRepacker(this Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    public static RColor ToRepacker(this Color c) => new(c.R, c.G, c.B, c.A);
    public static RRectangle ToRepacker(this Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}