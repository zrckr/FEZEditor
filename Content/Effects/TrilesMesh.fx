#include "BaseEffect.fxh"

static const float EMPLACEMENT_CENTER = 0.5;

DECLARE_TEXTURE(BaseTexture);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float InstanceIndex : TEXCOORD1;
    float3 InstancePosition : TEXCOORD2;
    float4 InstanceQuaternion : TEXCOORD3;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float3 Normal : TEXCOORD0;
    float Fog : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float3x3 basis = QuaternionToMatrix(input.InstanceQuaternion);
    float4x4 instanceMatrix = CreateTransform(input.InstancePosition + EMPLACEMENT_CENTER, basis);
    float4 worldPos = mul(input.Position, instanceMatrix);

    output.Position = TransformPositionToClip(worldPos);
    output.Normal = mul(input.Normal, (float3x3)instanceMatrix);
    output.TexCoord = input.TexCoord;
    output.Fog = saturate(1.0 - ApplyFog(output.Position.w));

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);

    float3 color = texColor.rgb;
    color *= ComputeLight(input.Normal, 0.0);
    color = lerp(color, Fog_Color, input.Fog);

    return float4(color, 1.0);
}

technique TSM2
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}