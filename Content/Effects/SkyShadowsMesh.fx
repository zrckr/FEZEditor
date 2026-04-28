#include "BaseEffect.fxh"

bool Canopy;

struct VS_INPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : NORMAL0;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Normal : TEXCOORD1;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    output.Position = ApplyTexelOffset(input.Position);
    output.TexCoord = TransformTexCoord(input.TexCoord);
    output.Normal = input.Normal;

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);

    if (Canopy)
    {
        float shadow = texColor.r * Material_Opacity;
        float3 color = 1.0 - shadow * (1.0 - BaseAmbient);
        return float4(color, 1.0);
    }
    else
    {
        float3 shadow = texColor.rrr * Material_Opacity;
        return float4(1.0 - shadow, 1.0);
    }
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}