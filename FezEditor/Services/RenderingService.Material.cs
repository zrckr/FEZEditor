using FezEditor.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Services;

public partial class RenderingService
{
    private static readonly Dictionary<(CullMode, FillMode), RasterizerState> RasterizerStateCache = new();

    private static readonly Dictionary<int, BlendState> BlendStateCache = new();

    private class MaterialData
    {
        public Effect? Effect;
        public Texture2D? Texture;
        public Matrix TextureTransform = Matrix.Identity;
        public Color Albedo = Color.White;
        public BlendMode BlendMode = BlendMode.AlphaBlend;
        public CullMode CullMode = CullMode.CullCounterClockwiseFace;
        public FillMode FillMode = FillMode.Solid;
        public ColorWriteChannels ColorWriteChannels = ColorWriteChannels.All;
        public SamplerState? SamplerState = SamplerState.PointClamp;
        public BlendState BlendState = ResolveBlendState(BlendMode.AlphaBlend, ColorWriteChannels.All);

        public readonly DepthStencilState DepthStencilState = new()
        {
            DepthBufferEnable = true, DepthBufferWriteEnable = true,
            StencilEnable = true,
            StencilFunction = CompareFunction.Always,
            StencilPass = StencilOperation.Replace,
            ReferenceStencil = 1
        };
    }

    private readonly Dictionary<Rid, MaterialData> _materials = new();

    public Rid MaterialCreate()
    {
        var rid = AllocateRid(typeof(MaterialData));
        _materials[rid] = new MaterialData();
        Logger.Debug("Material created {0}", rid);
        return rid;
    }

    public void MaterialReset(Rid material)
    {
        var data = GetResource(_materials, material);
        data.Effect?.Dispose();
        data.Effect = null;
        Logger.Debug("Material {0} reset", material);
    }

    public void MaterialAssignEffect(Rid material, Effect effect)
    {
        var data = GetResource(_materials, material);
        data.Effect?.Dispose();
        data.Effect = effect.Clone();
        Logger.Debug("Material {0} assigned effect {1}", material, effect.GetType().Name);
    }

    public void MaterialAssignBaseTexture(Rid material, Texture2D texture)
    {
        GetResource(_materials, material).Texture = texture;
    }

    public void MaterialSetTextureTransform(Rid material, Matrix transform)
    {
        GetResource(_materials, material).TextureTransform = transform;
    }

    public void MaterialSetAlbedo(Rid material, Color color)
    {
        GetResource(_materials, material).Albedo = color;
    }

    public Matrix MaterialGetTextureTransform(Rid material)
    {
        return GetResource(_materials, material).TextureTransform;
    }

    public Color MaterialGetAlbedo(Rid material)
    {
        return GetResource(_materials, material).Albedo;
    }

    public void MaterialSetBlendMode(Rid material, BlendMode mode)
    {
        var data = GetResource(_materials, material);
        data.BlendMode = mode;
        data.BlendState = ResolveBlendState(mode, data.ColorWriteChannels);
    }

    public void MaterialSetCullMode(Rid material, CullMode mode)
    {
        GetResource(_materials, material).CullMode = mode;
    }

    public void MaterialSetFillMode(Rid material, FillMode mode)
    {
        GetResource(_materials, material).FillMode = mode;
    }

    public FillMode MaterialGetFillMode(Rid material)
    {
        return GetResource(_materials, material).FillMode;
    }

    public void MaterialSetDepthWrite(Rid material, bool enabled)
    {
        var dss = GetResource(_materials, material).DepthStencilState;
        dss.DepthBufferWriteEnable = enabled;
        dss.DepthBufferEnable = dss.DepthBufferWriteEnable || dss.DepthBufferFunction != CompareFunction.Never;
    }

    public void MaterialSetDepthTest(Rid material, CompareFunction func)
    {
        var dss = GetResource(_materials, material).DepthStencilState;
        dss.DepthBufferFunction = func;
        dss.DepthBufferEnable = dss.DepthBufferWriteEnable || dss.DepthBufferFunction != CompareFunction.Never;
    }

    public void MaterialSetStencilTest(Rid material, CompareFunction func, int referenceValue)
    {
        var dss = GetResource(_materials, material).DepthStencilState;
        dss.StencilFunction = func;
        dss.StencilPass = StencilOperation.Keep;
        dss.ReferenceStencil = referenceValue;
    }

    public void MaterialSetColorWriteChannels(Rid material, ColorWriteChannels channels)
    {
        var data = GetResource(_materials, material);
        data.ColorWriteChannels = channels;
        data.BlendState = ResolveBlendState(data.BlendMode, channels);
    }

    public void MaterialSetSamplerState(Rid material, SamplerState state)
    {
        GetResource(_materials, material).SamplerState = state;
    }

    private void ApplyMaterialState(MaterialData mat)
    {
        GraphicsDevice.SamplerStates[0] = mat.SamplerState;
        GraphicsDevice.BlendState = mat.BlendState;
        GraphicsDevice.DepthStencilState = mat.DepthStencilState;
        GraphicsDevice.RasterizerState = ResolveRasterizerState(mat.CullMode, mat.FillMode);
    }

    private static RasterizerState ResolveRasterizerState(CullMode cullMode, FillMode fillMode)
    {
        if (fillMode == FillMode.Solid)
        {
            return cullMode switch
            {
                CullMode.None => RasterizerState.CullNone,
                CullMode.CullClockwiseFace => RasterizerState.CullClockwise,
                _ => RasterizerState.CullCounterClockwise
            };
        }

        var key = (cullMode, fillMode);
        if (!RasterizerStateCache.TryGetValue(key, out var state))
        {
            state = new RasterizerState { CullMode = cullMode, FillMode = fillMode };
            RasterizerStateCache[key] = state;
        }

        return state;
    }

    private static BlendState ResolveBlendState(BlendMode mode, ColorWriteChannels colorWriteChannels)
    {
        BlendFunction colorBlendFunction;
        Blend colorSourceBlend;
        Blend colorDestinationBlend;
        BlendFunction alphaBlendFunction;
        Blend alphaSourceBlend;
        Blend alphaDestinationBlend;

        switch (mode)
        {
            case BlendMode.Opaque:
                colorBlendFunction = BlendState.Opaque.ColorBlendFunction;
                colorSourceBlend = BlendState.Opaque.ColorSourceBlend;
                colorDestinationBlend = BlendState.Opaque.ColorDestinationBlend;
                alphaBlendFunction = BlendState.Opaque.AlphaBlendFunction;
                alphaSourceBlend = BlendState.Opaque.AlphaSourceBlend;
                alphaDestinationBlend = BlendState.Opaque.AlphaDestinationBlend;
                break;

            case BlendMode.AlphaBlend:
                colorBlendFunction = BlendState.AlphaBlend.ColorBlendFunction;
                colorSourceBlend = BlendState.AlphaBlend.ColorSourceBlend;
                colorDestinationBlend = BlendState.AlphaBlend.ColorDestinationBlend;
                alphaBlendFunction = BlendState.AlphaBlend.AlphaBlendFunction;
                alphaSourceBlend = BlendState.AlphaBlend.AlphaSourceBlend;
                alphaDestinationBlend = BlendState.AlphaBlend.AlphaDestinationBlend;
                break;

            case BlendMode.Additive:
                colorBlendFunction = BlendState.Additive.ColorBlendFunction;
                colorSourceBlend = BlendState.Additive.ColorSourceBlend;
                colorDestinationBlend = BlendState.Additive.ColorDestinationBlend;
                alphaBlendFunction = BlendState.Additive.AlphaBlendFunction;
                alphaSourceBlend = BlendState.Additive.AlphaSourceBlend;
                alphaDestinationBlend = BlendState.Additive.AlphaDestinationBlend;
                break;

            case BlendMode.Premultiplied:
                colorBlendFunction = BlendState.NonPremultiplied.ColorBlendFunction;
                colorSourceBlend = BlendState.NonPremultiplied.ColorSourceBlend;
                colorDestinationBlend = BlendState.NonPremultiplied.ColorDestinationBlend;
                alphaBlendFunction = BlendState.NonPremultiplied.AlphaBlendFunction;
                alphaSourceBlend = BlendState.NonPremultiplied.AlphaSourceBlend;
                alphaDestinationBlend = BlendState.NonPremultiplied.AlphaDestinationBlend;
                break;

            case BlendMode.Multiply:
                colorBlendFunction = BlendFunction.Add;
                colorSourceBlend = Blend.DestinationColor;
                colorDestinationBlend = Blend.Zero;
                alphaBlendFunction = BlendFunction.Add;
                alphaSourceBlend = Blend.DestinationAlpha;
                alphaDestinationBlend = Blend.Zero;
                break;

            case BlendMode.Multiply2X:
                colorBlendFunction = BlendFunction.Add;
                colorSourceBlend = Blend.DestinationColor;
                colorDestinationBlend = Blend.SourceColor;
                alphaBlendFunction = BlendFunction.Add;
                alphaSourceBlend = Blend.DestinationAlpha;
                alphaDestinationBlend = Blend.SourceAlpha;
                break;

            case BlendMode.Screen:
                colorBlendFunction = BlendFunction.Add;
                colorSourceBlend = Blend.InverseDestinationColor;
                colorDestinationBlend = Blend.One;
                alphaBlendFunction = BlendFunction.Add;
                alphaSourceBlend = Blend.InverseDestinationAlpha;
                alphaDestinationBlend = Blend.One;
                break;

            case BlendMode.Maximum:
                colorBlendFunction = BlendFunction.Max;
                colorSourceBlend = Blend.One;
                colorDestinationBlend = Blend.One;
                alphaBlendFunction = BlendFunction.Max;
                alphaSourceBlend = Blend.One;
                alphaDestinationBlend = Blend.One;
                break;

            case BlendMode.Minimum:
                colorBlendFunction = BlendFunction.Min;
                colorSourceBlend = Blend.One;
                colorDestinationBlend = Blend.One;
                alphaBlendFunction = BlendFunction.Min;
                alphaSourceBlend = Blend.One;
                alphaDestinationBlend = Blend.One;
                break;

            case BlendMode.Subtract:
                colorBlendFunction = BlendFunction.ReverseSubtract;
                colorSourceBlend = Blend.One;
                colorDestinationBlend = Blend.One;
                alphaBlendFunction = BlendFunction.ReverseSubtract;
                alphaSourceBlend = Blend.One;
                alphaDestinationBlend = Blend.One;
                break;

            case BlendMode.StarsOverClouds:
                colorBlendFunction = BlendFunction.Add;
                colorSourceBlend = Blend.One;
                colorDestinationBlend = Blend.InverseSourceColor;
                alphaBlendFunction = BlendFunction.Add;
                alphaSourceBlend = Blend.One;
                alphaDestinationBlend = Blend.InverseSourceColor;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var hash = (byte)colorBlendFunction
                   | ((byte)colorSourceBlend << 3)
                   | ((byte)colorDestinationBlend << 7)
                   | ((byte)alphaBlendFunction << 11)
                   | ((byte)alphaSourceBlend << 14)
                   | ((byte)alphaDestinationBlend << 18)
                   | ((byte)colorWriteChannels << 22);

        if (!BlendStateCache.TryGetValue(hash, out var state))
        {
            state = new BlendState
            {
                ColorBlendFunction = colorBlendFunction,
                ColorSourceBlend = colorSourceBlend,
                ColorDestinationBlend = colorDestinationBlend,
                AlphaBlendFunction = alphaBlendFunction,
                AlphaSourceBlend = alphaSourceBlend,
                AlphaDestinationBlend = alphaDestinationBlend,
                ColorWriteChannels = colorWriteChannels
            };
            BlendStateCache[hash] = state;
        }

        return state;
    }

    private void RestoreDefaultState()
    {
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }

    private void InvalidateMaterial(Rid material)
    {
        foreach (var surface in _meshes.Values.SelectMany(m => m.Surfaces))
        {
            if (surface.Material == material)
            {
                surface.Material = Rid.Invalid;
            }
        }
    }
}