#include "BaseEffect.fxh"

#define DECLARE_POINT_TEXTURE(Name) \
    texture2D Name; \
    sampler Name##Sampler = sampler_state { Texture = (Name); MagFilter = Point; MinFilter = Point; MipFilter = Point; }

static const int COLLISION_ALL_SIDES = 0;
static const int COLLISION_TOP_ONLY = 1;
static const int COLLISION_NONE = 2;
static const int COLLISION_IMMATERIAL = 3;
static const int COLLISION_TOP_NO_STRAIGHT_LEDGE = 4;

DECLARE_POINT_TEXTURE(AllSidesTexture);
DECLARE_POINT_TEXTURE(TopOnlyTexture);
DECLARE_POINT_TEXTURE(NoneTexture);
DECLARE_POINT_TEXTURE(ImmaterialTexture);
DECLARE_POINT_TEXTURE(TopNoStraightLedgeTexture);

struct VS_INPUT
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float InstanceIndex : TEXCOORD1;
    float4 InstancePosition : TEXCOORD2;
    float4 InstanceQuaternion : TEXCOORD3;
    float4 InstanceSizeType : TEXCOORD4;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float CollisionType : TEXCOORD1;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float3x3 basis = QuaternionToMatrix(input.InstanceQuaternion);
    float3 size = input.InstanceSizeType.xyz;
    float3 faceNormal = mul(float3(0, 0, 1), basis);

    float3 worldPos = (mul(input.Position.xyz, basis) * size) + input.InstancePosition.xyz + faceNormal * size / 2.0;
    output.Position = TransformPositionToClip(float4(worldPos, 1.0));
    output.TexCoord = input.TexCoord;
    output.CollisionType = input.InstanceSizeType.w;

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    int type = (int)(input.CollisionType + 0.5); // to ensure proper integer rounding for comparison
    float4 color;

    if (type == COLLISION_ALL_SIDES)
    {
        color = SAMPLE_TEXTURE(AllSidesTexture, input.TexCoord);
    }
    else if (type == COLLISION_TOP_ONLY)
    {
        color = SAMPLE_TEXTURE(TopOnlyTexture, input.TexCoord);
    }
    else if (type == COLLISION_NONE)
    {
        color = SAMPLE_TEXTURE(NoneTexture, input.TexCoord);
    }
    else if (type == COLLISION_IMMATERIAL)
    {
        color = SAMPLE_TEXTURE(ImmaterialTexture, input.TexCoord);
    }
    else
    {
        color = SAMPLE_TEXTURE(TopNoStraightLedgeTexture, input.TexCoord);
    }

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
