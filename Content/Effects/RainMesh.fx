#include "BaseEffect.fxh"

struct VS_INPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float InstanceIndex : TEXCOORD1;
    float3 InstancePosition : TEXCOORD2;
    float4 InstanceQuaternion : TEXCOORD3;
    float3 InstanceScale : TEXCOORD4;
    float4 InstanceColor : TEXCOORD5;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float4 Color : TEXCOORD1;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float3x3 basis = QuaternionToMatrix(input.InstanceQuaternion);
    float4x4 xform = CreateTransform(input.InstancePosition, basis, input.InstanceScale);
    float4 worldPos = mul(input.Position, xform);
    float4 worldViewPos = TransformPositionToClip(worldPos);
    output.Position = ApplyTexelOffset(worldViewPos);
    output.TexCoord = input.TexCoord;
    output.Color = input.InstanceColor;

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);

    float3 color = texColor.rgb * input.Color.rgb;
    float alpha = texColor.a * input.Color.a;
    ApplyAlphaTest(alpha);

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