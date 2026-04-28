using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Actors;

public class SceneViewport : IDisposable
{
    private readonly RenderingService _rendering;

    private readonly Rid _world;

    private readonly Rid _rt;

    internal SceneViewport(Game game, Rid worldRid)
    {
        _rendering = game.GetService<RenderingService>();
        _world = worldRid;
        _rt = _rendering.RenderTargetCreate();
        _rendering.RenderTargetSetWorld(_rt, worldRid);
        _rendering.RenderTargetSetClearColor(_rt, Color.Black);
    }

    public Texture2D? GetTexture()
    {
        return _rendering.RenderTargetGetTexture(_rt);
    }

    public (int Width, int Height) GetSize()
    {
        return _rendering.RenderTargetGetSize(_rt);
    }

    public void SetSize(int width, int height)
    {
        _rendering.RenderTargetSetSize(_rt, width, height);
    }

    public void SetClearColor(Color color)
    {
        _rendering.RenderTargetSetClearColor(_rt, color);
    }

    public Ray Unproject(Vector2 mousePos, Vector2 viewportMin)
    {
        var (width, height) = _rendering.RenderTargetGetSize(_rt);
        var camera = _rendering.WorldGetCamera(_world);
        var view = _rendering.CameraGetView(camera);
        var projection = _rendering.CameraGetProjection(camera);

        var local = mousePos - viewportMin;
        var invViewProj = Matrix.Invert(view * projection);
        var nearPoint = UnprojectPoint(new Vector3(local.X, local.Y, 0f), width, height, invViewProj);
        var farPoint = UnprojectPoint(new Vector3(local.X, local.Y, 1f), width, height, invViewProj);
        var direction = Vector3.Normalize(farPoint - nearPoint);

        return new Ray(nearPoint, direction);
    }

    private static Vector3 UnprojectPoint(Vector3 source, int width, int height, Matrix invViewProj)
    {
        source.X = (source.X / width * 2f) - 1f;
        source.Y = -((source.Y / height * 2f) - 1f);

        var result = Vector3.Transform(source, invViewProj);
        var w = (source.X * invViewProj.M14) +
                (source.Y * invViewProj.M24) +
                (source.Z * invViewProj.M34) +
                invViewProj.M44;

        if (MathF.Abs(w - 1f) >= float.Epsilon)
        {
            result /= w;
        }

        return result;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _rendering.FreeRid(_rt);
    }
}