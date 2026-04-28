#include "BaseEffect.fxh"

float DoubleSided;      // boolean

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : TEXCOORD1;
    float Fog : TEXCOORD2;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 worldPos = TransformPositionToWorld(input.Position);
    output.Normal = TransformNormalToWorld(input.Normal);

    float4 worldViewPos = TransformWorldToClip(worldPos);
    output.Position = ApplyTexelOffset(worldViewPos);
    output.TexCoord = TransformTexCoord(input.TexCoord);
    output.Fog = saturate(1.0 - ApplyFog(output.Position.w));

    return output;
}

float4 PS(VS_OUTPUT input, float vface : VFACE) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);
    float alpha = texColor.a * Material_Opacity;
    ApplyAlphaTest(alpha);

    float3 color = texColor.rgb * Material_Diffuse;
    float3 normal = (DoubleSided != 0.0 && vface < 0.0) ? -input.Normal : input.Normal;
    color *= ComputeLight(normal, 0.0);
    color = lerp(color, Fog_Color, input.Fog);

    return float4(color, alpha);
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}
