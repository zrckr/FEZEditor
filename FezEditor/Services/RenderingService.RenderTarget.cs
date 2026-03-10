using FezEditor.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Services;

public partial class RenderingService
{
    private class RenderTargetData
    {
        public bool IsBackbuffer;
        public RenderTarget2D? Target;
        public Rid World;
        public Color ClearColor = Color.CornflowerBlue;
        public int Width;
        public int Height;
    }

    private readonly Dictionary<Rid, RenderTargetData> _renderTargets = new();

    private readonly Rid _backbufferRid;

    public Rid RenderTargetCreate()
    {
        var rid = AllocateRid(typeof(RenderTargetData));
        var w = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var h = GraphicsDevice.PresentationParameters.BackBufferHeight;

        _renderTargets[rid] = new RenderTargetData
        {
            Width = w,
            Height = h,
            Target = new RenderTarget2D(GraphicsDevice, w, h,
                false,
                GraphicsDevice.PresentationParameters.BackBufferFormat,
                GraphicsDevice.PresentationParameters.DepthStencilFormat)
        };

        Logger.Debug("RenderTarget created {0} ({1}x{2})", rid, w, h);
        return rid;
    }

    public Rid RenderTargetGetBackbuffer()
    {
        return _backbufferRid;
    }

    public Texture2D? RenderTargetGetTexture(Rid rt)
    {
        var data = GetResource(_renderTargets, rt);
        return data.IsBackbuffer ? null! : data.Target;
    }

    public (int width, int height) RenderTargetGetSize(Rid rt)
    {
        var data = GetResource(_renderTargets, rt);
        return (data.Width, data.Height);
    }

    public void RenderTargetSetWorld(Rid rt, Rid world)
    {
        GetResource(_renderTargets, rt).World = world;
    }

    public void RenderTargetSetSize(Rid rt, int width, int height)
    {
        var data = GetResource(_renderTargets, rt);
        if (data.IsBackbuffer || (data.Width == width && data.Height == height))
        {
            return;
        }

        data.Width = width;
        data.Height = height;
        data.Target?.Dispose();
        data.Target = new RenderTarget2D(GraphicsDevice, width, height, false,
            GraphicsDevice.PresentationParameters.BackBufferFormat,
            GraphicsDevice.PresentationParameters.DepthStencilFormat);
        Logger.Debug("RenderTarget {0} resized to {1}x{2}", rt, width, height);
    }

    public void RenderTargetSetClearColor(Rid rt, Color color)
    {
        GetResource(_renderTargets, rt).ClearColor = color;
    }

    private Rid CreateBackbuffer()
    {
        var rid = AllocateRid(typeof(RenderTargetData));
        var w = GraphicsDevice.PresentationParameters.BackBufferWidth;
        var h = GraphicsDevice.PresentationParameters.BackBufferHeight;
        _renderTargets[rid] = new RenderTargetData
        {
            IsBackbuffer = true,
            Width = w,
            Height = h
        };
        Logger.Debug("Backbuffer created {0} ({1}x{2})", rid, w, h);
        return rid;
    }

    private static void DisposeRenderTarget(RenderTargetData rt)
    {
        if (!rt.IsBackbuffer)
        {
            rt.Target?.Dispose();
        }
    }
}