#include "BaseEffect.fxh"

float4x4 CameraRotation;
float Billboard;        // boolean

struct VS_INPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float InstanceIndex : TEXCOORD1;
    float3 InstancePosition : TEXCOORD2;
    float3 InstanceScale : TEXCOORD3;
    float4 InstanceTexCoord : TEXCOORD4;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float4x4 rotation = (Billboard != 0.0) ? CameraRotation : MATRIX_IDENTITY;
    float4x4 xform = CreateTransform(input.InstancePosition, (float3x3)rotation, input.InstanceScale);
    float4 worldPos = mul(input.Position, xform);

    float4 worldViewPos = TransformPositionToClip(worldPos);
    output.Position = ApplyTexelOffset(worldViewPos);
    output.TexCoord = input.TexCoord * input.InstanceTexCoord.zw + input.InstanceTexCoord.xy;

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    return SAMPLE_TEXTURE(BaseTexture, input.TexCoord);
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}