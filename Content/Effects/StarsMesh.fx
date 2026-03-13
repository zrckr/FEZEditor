#include "BaseEffect.fxh"

static const float4 FOG_COLOR = float4(0, 0, 0, 1);
static const float FOG_DENSITY = 0.00175;

float4 Colors[11];
float Size;
float4x4 Projection;

DECLARE_TEXTURE(BaseTexture);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float InstanceIndex : TEXCOORD1;
    float4 InstancePositionColorIndex : TEXCOORD2;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
    float Fog : TEXCOORD1;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 starColor = Colors[(int)input.InstancePositionColorIndex.w];
    float4 worldPos = TransformPositionToWorld(float4(input.InstancePositionColorIndex.xyz, 1.0));
    float4 centerClip = mul(worldPos, Projection);

    float2 offset = input.Position.xy * (-TexelOffset * 2.0 * Size);
    output.Position = ApplyTexelOffset(centerClip, offset);
    output.TexCoord = input.TexCoord;
    output.Color = starColor * Material_Opacity * starColor.a;
    output.Fog = saturate(1.0 - Exp2Fog(centerClip.w, FOG_DENSITY));

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);
    float4 fogColor = lerp(input.Color, FOG_COLOR, input.Fog);
    return texColor * fogColor;
}

technique TSM2
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}
