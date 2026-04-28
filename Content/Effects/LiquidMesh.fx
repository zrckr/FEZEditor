#include "BaseEffect.fxh"

struct VS_INPUT
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float Fog : TEXCOORD0;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 worldViewPos = TransformPositionToClip(input.Position);
    output.Position = ApplyTexelOffset(worldViewPos);
    output.Color = input.Color;
    output.Fog = saturate(1.0 - ApplyFog(output.Position.w));

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float3 color = input.Color.rgb * Material_Diffuse;
    float alpha = input.Color.a * Material_Opacity;
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
