#include "BaseEffect.fxh"

#define DECLARE_POINT_TEXTURE(Name) \
    texture2D Name; \
    sampler Name##Sampler = sampler_state { Texture = (Name); MagFilter = Point; MinFilter = Point; MipFilter = Point; }

static const float EMPLACEMENT_CENTER = 0.5;
static const int COLLISION_ALL_SIDES = 0;
static const int COLLISION_TOP_ONLY = 1;
static const int COLLISION_NONE = 2;
static const int COLLISION_IMMATERIAL = 3;

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
    float4 InstancePositionCollision : TEXCOORD1;
    float4 InstanceQuaternion : TEXCOORD2;
    float4 InstanceTint : TEXCOORD3;
    float4 InstanceCollisionTypes : TEXCOORD4;
};

struct VS_OUTPUT
{
    float4 Position : POSITION0;
    float3 Normal : TEXCOORD0;
    float Fog : TEXCOORD1;
    float2 TexCoord : TEXCOORD2;
    float4 Tint : TEXCOORD3;
    float CollisionType : TEXCOORD4;
};

VS_OUTPUT VS(VS_INPUT input)
{
    VS_OUTPUT output;

    float3x3 basis = QuaternionToMatrix(input.InstanceQuaternion);
    float4x4 instanceMatrix = CreateTransform(input.InstancePositionCollision.xyz + EMPLACEMENT_CENTER, basis);
    float4 worldPos = mul(input.Position, instanceMatrix);

    output.Position = TransformPositionToClip(worldPos);
    output.Normal = mul(input.Normal, (float3x3)instanceMatrix);
    output.TexCoord = input.TexCoord;
    output.Fog = saturate(1.0 - ApplyFog(output.Position.w));
    output.Tint = input.InstanceTint;
    output.CollisionType = -1.0;

    if (input.InstancePositionCollision.w > 0.5)
    {
        if (input.Normal.z > 0.5)
        {
            output.CollisionType = input.InstanceCollisionTypes.x;
        }
        else if (input.Normal.x > 0.5)
        {
            output.CollisionType = input.InstanceCollisionTypes.y;
        }
        else if (input.Normal.z < -0.5)
        {
            output.CollisionType = input.InstanceCollisionTypes.z;
        }
        else
        {
            output.CollisionType = input.InstanceCollisionTypes.w;
        }
    }

    return output;
}

float4 PS(VS_OUTPUT input) : COLOR0
{
    float4 texColor;

    if (input.CollisionType >= 0.0)
    {
        int type = (int)(input.CollisionType + 0.5);

        if (type == COLLISION_ALL_SIDES)
        {
            texColor = SAMPLE_TEXTURE(AllSidesTexture, input.TexCoord);
        }
        else if (type == COLLISION_TOP_ONLY)
        {
            texColor = SAMPLE_TEXTURE(TopOnlyTexture, input.TexCoord);
        }
        else if (type == COLLISION_NONE)
        {
            texColor = SAMPLE_TEXTURE(NoneTexture, input.TexCoord);
        }
        else if (type == COLLISION_IMMATERIAL)
        {
            texColor = SAMPLE_TEXTURE(ImmaterialTexture, input.TexCoord);
        }
        else
        {
            texColor = SAMPLE_TEXTURE(TopNoStraightLedgeTexture, input.TexCoord);
        }
    }
    else
    {
        texColor = SAMPLE_TEXTURE(BaseTexture, input.TexCoord);
    }

    float3 color = texColor.rgb;
    color = lerp(color, input.Tint.rgb, input.Tint.a);
    color *= ComputeLight(input.Normal, 0.0);
    color = lerp(color, Fog_Color, input.Fog);

    return float4(color, 1.0);
}

technique Main
{
    pass Main
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PS();
    }
}