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
    float3 size = input.InstanceSize.xyz;

    float3 worldPos = (mul(input.Position.xyz, basis) * size) + input.InstancePosition.xyz;
    output.Position = TransformPositionToClip(float4(worldPos, 1.0));

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 color = float4(1.0, 1.0, 1.0, 1.0);
    color.rgb *= Material_Diffuse;
    color.a *= Material_Opacity;

    return color;
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}