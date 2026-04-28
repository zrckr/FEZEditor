#include "BaseEffect.fxh"

float2 ViewportSize;
float2 TextureSize;
float PixelsPerTrixel;
float3 CubeOffset;

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float3 Normal : TEXCOORD0;
    float4 ProjTC : TEXCOORD1;
    float4 CenterTC : TEXCOORD2;
};

float NodeShading(float3 normal)
{
    float shade = saturate(BaseAmbient).x;
    float remainder = (1.0 - BaseAmbient).x;

    shade += saturate(dot(normal, 1.0)) * remainder;
    if (normal.z < -0.01)
    {
        shade += abs(normal.z) * remainder * 0.6;
    }
    else if (normal.x > 0.01)
    {
        shade -= abs(normal.x) * remainder * 0.2;
    }
    if (normal.x < -0.01)
    {
        shade += abs(normal.x) * remainder * 0.4;
    }
    if (normal.y > 0.01)
    {
        shade -= abs(normal.y) * remainder * 0.1;
    }

    return saturate(shade);
}

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4 position = TransformPositionToClip(input.Position);
    output.Normal = TransformNormalToWorld(input.Normal);
    output.ProjTC = position;
    output.CenterTC = TransformWorldToClip(float4(CubeOffset, 1.0));
    output.Position = position;

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    // Project texture coords
    float2 texCoord = float2(input.ProjTC.x, -input.ProjTC.y) / input.ProjTC.w;
    float2 centerTexCoord = float2(input.CenterTC.x, -input.CenterTC.y) / input.CenterTC.w;

    // Calculate sample coord from projected coords
    texCoord *= PixelsPerTrixel;
    texCoord -= centerTexCoord * PixelsPerTrixel;
    texCoord *= ViewportSize / TextureSize / 3.0;
    texCoord = 0.5 * texCoord + 0.5;
    texCoord += 0.5 / ViewportSize;

    float3 color = SAMPLE_TEXTURE(BaseTexture, texCoord).rgb;
    color *= pow(NodeShading(input.Normal), 0.75);

    return float4(color, Material_Opacity);
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}