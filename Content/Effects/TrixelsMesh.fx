#include "BaseEffect.fxh"

static const float3 TRIXEL_SIZE = float3(1.0 / 16.0, 1.0 / 16.0, 1.0 / 16.0);

DECLARE_TEXTURE(BaseTexture);

float3 SelectedColor;
float SelectedAlpha;

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float InstanceIndex : TEXCOORD1;
    float4 InstancePosition : TEXCOORD2;
    float4 InstanceQuaternion : TEXCOORD3;
    float4 InstanceTexCoord01 : TEXCOORD4;
    float4 InstanceTexCoord23 : TEXCOORD5;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float3 Normal : TEXCOORD0;
    float2 TexCoord : TEXCOORD1;
    float Selected : TEXCOORD2;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;
    
    float3x3 basis = QuaternionToMatrix(input.InstanceQuaternion);
    float4x4 xform = CreateTransform(input.InstancePosition.xyz, basis, TRIXEL_SIZE);
    float4 worldPos = mul(input.Position, xform);
    
    float4 worldViewPos = TransformPositionToClip(worldPos);
    output.Position = ApplyTexelOffset(worldViewPos);
    output.Normal = mul(input.Normal, basis);
    
    float2 t = input.Position.xy + 0.5;
    float2 bottom = lerp(input.InstanceTexCoord01.xy, input.InstanceTexCoord01.zw, t.x);
    float2 top = lerp(input.InstanceTexCoord23.xy, input.InstanceTexCoord23.zw, t.x);
    output.TexCoord = lerp(bottom, top, t.y);
    
    output.Selected = input.InstancePosition.w;

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);
    
    float3 color = texColor.rgb;
    color *= ComputeLight(input.Normal, 0.0);
    
    if (input.Selected == 1.0)
    {
        color = lerp(color, SelectedColor, SelectedAlpha);
    }
    
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
