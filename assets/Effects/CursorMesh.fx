#include "BaseEffect.fxh"

struct VS_INPUT
{
    float4 Position : POSITION0;
    float4 InstancePosition : TEXCOORD1;
    float4 InstanceQuaternion : TEXCOORD2;
    float4 InstanceSize : TEXCOORD3;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float3x3 basis = QuaternionToMatrix(input.InstanceQuaternion);
    float3 worldPosition = (mul(input.Position.xyz, basis) * input.InstanceSize.xyz) + input.InstancePosition.xyz;
    output.Position = TransformPositionToClip(float4(worldPosition, 1.0));

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    return float4(Material_Diffuse, Material_Opacity);
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}