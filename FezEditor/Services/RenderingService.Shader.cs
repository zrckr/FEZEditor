using FezEditor.Structure;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Services;

public partial class RenderingService
{
    private static readonly HashSet<Type> AllowedParameterTypes = new()
    {
        typeof(bool),
        typeof(int),
        typeof(float),
        typeof(Vector2),
        typeof(Vector3),
        typeof(Vector4),
        typeof(Quaternion),
        typeof(Matrix),
        typeof(Color),
        typeof(Vector4[]),
        typeof(Matrix[])
    };

    private static readonly Dictionary<Type, Func<EffectParameter, int, object>> GetParameterFunctions = new()
    {
        [typeof(bool)] = (ep, _) => ep.GetValueBoolean(),
        [typeof(int)] = (ep, _) => ep.GetValueInt32(),
        [typeof(float)] = (ep, _) => ep.GetValueSingle(),
        [typeof(Vector2)] = (ep, _) => ep.GetValueVector2(),
        [typeof(Vector3)] = (ep, _) => ep.GetValueVector3(),
        [typeof(Vector4)] = (ep, _) => ep.GetValueVector4(),
        [typeof(Quaternion)] = (ep, _) => ep.GetValueQuaternion(),
        [typeof(Matrix)] = (ep, _) => ep.GetValueMatrix(),
        [typeof(Color)] = (ep, _) => { var v = ep.GetValueVector3(); return new Color(v.X, v.Y, v.Z); },
        [typeof(Vector4[])] = (ep, i) => ep.GetValueVector4Array(i),
        [typeof(Matrix[])] = (ep, i) => ep.GetValueMatrixArray(i)
    };
    
    private static readonly Dictionary<Type, Action<EffectParameter, object>> SetParameterFunctions = new()
    {
        [typeof(bool)] = (ep, v) => ep.SetValue((bool)v),
        [typeof(int)] = (ep, v) => ep.SetValue((int)v),
        [typeof(float)] = (ep, v) => ep.SetValue((float)v),
        [typeof(Vector2)] = (ep, v) => ep.SetValue((Vector2)v),
        [typeof(Vector3)] = (ep, v) => ep.SetValue((Vector3)v),
        [typeof(Vector4)] = (ep, v) => ep.SetValue((Vector4)v),
        [typeof(Quaternion)] = (ep, v) => ep.SetValue((Quaternion)v),
        [typeof(Matrix)] = (ep, v) => ep.SetValue((Matrix)v),
        [typeof(Color)] = (ep, v) => ep.SetValue(((Color)v).ToVector3()),
        [typeof(Vector4[])] = (ep, v) => ep.SetValue((Vector4[])v),
        [typeof(Matrix[])] = (ep, v) => ep.SetValue((Matrix[])v)
    };
    
    public T MaterialShaderGetParam<T>(Rid material, string name, int count = 0)
    {
        var effect = GetResource(_materials, material).Effect;
        var parameter = GetEffectParameter<T>(effect, name);
        var getter = GetParameterFunctions[typeof(T)];
        return (T)getter(parameter, count);
    }

    public void MaterialShaderSetParam<T>(Rid material, string name, T value)
    {
        var effect = GetResource(_materials, material).Effect;
        var parameter = GetEffectParameter<T>(effect, name);
        var setter = SetParameterFunctions[typeof(T)];
        setter(parameter, value!);
    }

    private static EffectParameter GetEffectParameter<T>(Effect effect, string name)
    {
        if (!AllowedParameterTypes.Contains(typeof(T)))
        {
            throw new ArgumentException($"Parameter of type {typeof(T)} does not supported");
        }

        var parameter = effect.Parameters[name];
        if (parameter == null)
        {
            throw new ArgumentException($"Parameter of name {name} does not exist");
        }

        return parameter;
    }

    private static void UpdateBasicEffect(MaterialData material, InstanceMatrices matrices)
    {
        var effect = (BasicEffect)material.Effect;
        effect.World = matrices.World;
        effect.View = matrices.View;
        effect.Projection = matrices.Projection;
        effect.DiffuseColor = material.Diffuse;
        effect.Alpha = material.Opacity;
        effect.TextureEnabled = false;
        if (material.Texture != null)
        {
            effect.TextureEnabled = true;
            effect.Texture = material.Texture;
        }
    }

    private static void UpdateBaseEffect(RenderTargetData rt, WorldData world, MaterialData material, InstanceMatrices matrices)
    {
        var parameters = material.Effect.Parameters;
        var worldViewProjection = matrices.World * matrices.ViewProjection;
        var worldInverseTranspose = Matrix.Transpose(Matrix.Invert(matrices.World));
        
        // Matrices
        parameters["Matrices_WorldViewProjection"].SetValue(worldViewProjection);
        parameters["Matrices_WorldInverseTranspose"].SetValue(worldInverseTranspose);
        parameters["Matrices_World"].SetValue(matrices.World);
        parameters["Matrices_ViewProjection"].SetValue(matrices.ViewProjection);
        parameters["Matrices_Texture"].SetValue(material.TextureTransform);

        // Material
        parameters["Material_Diffuse"].SetValue(material.Diffuse);
        parameters["Material_Opacity"].SetValue(material.Opacity);
        parameters["BaseTexture"].SetValue(material.Texture);

        // Lighting
        parameters["BaseAmbient"].SetValue(world.AmbientLight);
        parameters["DiffuseLight"].SetValue(world.DiffuseLight);

        // Fog
        parameters["Fog_Type"].SetValue((float)world.FogType);
        parameters["Fog_Color"].SetValue(world.FogColor.ToVector3());
        parameters["Fog_Density"].SetValue(world.FogDensity);

        // Render Target
        parameters["AspectRatio"].SetValue((float)rt.Width / rt.Height);
        parameters["TexelOffset"].SetValue(new Vector2(-0.5f / rt.Width, 0.5f / rt.Height));

        // Camera
        var invView = Matrix.Invert(matrices.View);
        parameters["Eye"].SetValue(invView.Forward);
    }
}