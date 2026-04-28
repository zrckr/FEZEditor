#ifndef BASE_EFFECT_FXH
#define BASE_EFFECT_FXH

//------------------------------------------------------------------------------
// MACROS
//------------------------------------------------------------------------------

#define DECLARE_TEXTURE(Name) \
    texture2D Name; \
    sampler Name##Sampler = sampler_state { Texture = (Name); }

#define SAMPLE_TEXTURE(Name, texCoord) \
    tex2D(Name##Sampler, texCoord)

#define RGB(hex) float3( \
    ((hex >> 16) & 0xFF) / 255.0, \
    ((hex >> 8) & 0xFF) / 255.0, \
    (hex & 0xFF) / 255.0 \
)

#define RGBA(hex) float4( \
    ((hex >> 24) & 0xFF) / 255.0, \
    ((hex >> 16) & 0xFF) / 255.0, \
    ((hex >> 8) & 0xFF) / 255.0, \
    (hex & 0xFF) / 255.0 \
)

//------------------------------------------------------------------------------
// CONSTANTS
//------------------------------------------------------------------------------

static const float PI = 3.14159274;

static const float TAU = 2.0 * PI;

static const float ALPHA_THRESHOLD = 1.0 / 256.0;

//------------------------------------------------------------------------------
// FOG SEMANTICS
//------------------------------------------------------------------------------

static const float FOG_TYPE_NONE = 0.0;
static const float FOG_TYPE_EXP = 1.0;
static const float FOG_TYPE_EXP_SQR = 2.0;
static const float FOG_TYPE_LINEAR = 3.0;

float Fog_Type;

float3 Fog_Color;

float Fog_Density;

float Exp2Fog(float distance, float density)
{
    return 1.0 / exp(pow(distance * density, 3.0));
}

float ApplyFog(float distance)
{
    if (Fog_Type == FOG_TYPE_NONE)
    {
        return 1.0;
    }
    
    if (Fog_Type == FOG_TYPE_EXP_SQR)
    {
        return Exp2Fog(distance, Fog_Density);
    }
    
    // NOTE: FOG_TYPE_EXP and FOG_TYPE_LINEAR not implemented
    return 1.0;
}

//------------------------------------------------------------------------------
// MATRIX SEMANTICS (XNA ROW-MAJOR CONVENTION)
//------------------------------------------------------------------------------

static const float4x4 MATRIX_IDENTITY = float4x4(
    1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1
);

float4x4 Matrices_WorldViewProjection;

float4x4 Matrices_WorldInverseTranspose;

float4x4 Matrices_World;

float3x3 Matrices_Texture;

float4x4 Matrices_ViewProjection;

float4 TransformPositionToClip(float4 position)
{
    return mul(position, Matrices_WorldViewProjection);
}

float3 TransformNormalToWorld(float3 normal)
{
    return mul(normal, (float3x3)Matrices_WorldInverseTranspose);
}

float4 TransformNormalToWorld(float4 normal)
{
    return mul(normal, (float3x4)Matrices_WorldInverseTranspose);
}

float4 TransformPositionToWorld(float4 position)
{
    return mul(position, Matrices_World);
}

float2 TransformTexCoord(float2 texCoord)
{
    return mul(float3(texCoord, 1.0), Matrices_Texture).xy;
}

float2 TransformTexCoord(float2 texCoord, float3x3 matricesTexture)
{
    return mul(float3(texCoord, 1.0), matricesTexture).xy;
}

float4 TransformWorldToClip(float4 worldPosition)
{
    return mul(worldPosition, Matrices_ViewProjection);
}

//------------------------------------------------------------------------------
// BASE TEXTURE
//------------------------------------------------------------------------------

DECLARE_TEXTURE(BaseTexture);

//------------------------------------------------------------------------------
// MATERIAL SEMANTICS
//------------------------------------------------------------------------------

float3 Material_Diffuse;

float Material_Opacity;

void ApplyAlphaTest(float alpha)
{
    clip(alpha - ALPHA_THRESHOLD);
}

//------------------------------------------------------------------------------
// BASE EFFECT SEMANTICS
//------------------------------------------------------------------------------

float AspectRatio;

float2 TexelOffset;

float Time;

float3 BaseAmbient;

float3 Eye;

float3 DiffuseLight;

float4 ApplyTexelOffset(float4 position)
{
    return float4(position.xy + (TexelOffset * position.w), position.zw);
}

float4 ApplyTexelOffset(float4 position, float2 offset)
{
    return float4(position.xy + (offset * position.w), position.zw);
}

float3 PerAxisShading(float3 normal, float emissive)
{
    float3 shade = saturate(BaseAmbient + emissive);
    float3 remainder = 1.0 - BaseAmbient;
    
    // Front lighting for surfaces lit directly
    shade += saturate(dot(normal, 1.0)) * remainder;
    
    // Back lighting for surfaces facing away (60% contribution)
    if (normal.z < -0.01)
    {
        shade += abs(normal.z) * remainder * 0.6;
    }
    
    // Side lighting for surfaces facing left/right (30% contribution)
    if (normal.x < -0.01)
    {
        shade += abs(normal.x) * remainder * 0.3;
    }
    
    return saturate(shade);
}

float3 ComputeLight(float3 normal, float emissive)
{
    float3 ambient = PerAxisShading(normal, emissive);
    return ambient * DiffuseLight + emissive * (1.0 - DiffuseLight);
}

float ApplySpecular(float3 normal)
{
    float3 eyeDir = Eye - float3(0.0, 0.25, 0.0);
    float specular = dot(eyeDir, normal);
    return saturate(pow(specular, 8));
}

//------------------------------------------------------------------------------
// UTILITY FUNCTIONS
//------------------------------------------------------------------------------

float3x3 CreateTransform2D(float2 position, float2 scale)
{
    return float3x3(
        scale.x, 0, 0,
        0, scale.y, 0,
        position, 1
    );
}

float4x4 CreateTransform(float3 position)
{
    return float4x4(
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        position, 1
    );
}

float4x4 CreateTransform(float3 position, float3x3 rotation)
{
    return float4x4(
        rotation[0], 0,
        rotation[1], 0,
        rotation[2], 0,
        position, 1
    );
}

float4x4 CreateTransform(float3 position, float3 scale)
{
    return float4x4(
        scale.x, 0, 0, 0,
        0, scale.y, 0, 0,
        0, 0, scale.z, 0,
        position, 1
    );
}

float4x4 CreateTransform(float3 position, float3x3 rotation, float3 scale)
{
    return float4x4(
        rotation[0] * scale.x, 0,
        rotation[1] * scale.y, 0,
        rotation[2] * scale.z, 0,
        position, 1
    );
}

float3x3 PhiToMatrix(float phi)
{
    // Y-up, left-handed
    float s, c;
    sincos(phi, s, c);
    return float3x3(
        c,  0,  -s,
        0,  1,  0,
        s,  0,  c
    );
}

float3x3 QuaternionToMatrix(float4 quaternion)
{
    float xx = quaternion.x * quaternion.x;
    float yy = quaternion.y * quaternion.y;
    float zz = quaternion.z * quaternion.z;
    float xy = quaternion.x * quaternion.y;
    float xz = quaternion.x * quaternion.z;
    float xw = quaternion.x * quaternion.w;
    float yz = quaternion.y * quaternion.z;
    float yw = quaternion.y * quaternion.w;
    float zw = quaternion.z * quaternion.w;

    return float3x3(
        1 - 2 * (yy + zz), 2 * (xy + zw), 2 * (xz - yw),
        2 * (xy - zw), 1 - 2 * (xx + zz), 2 * (yz + xw),
        2 * (xz + yw), 2 * (yz - xw), 1 - 2 * (xx + yy)
    );
}

float3 HSV_RGB(float hue, float saturation, float value)
{
    float h = hue * 6.0;
    float f = frac(h);
    int i = (int)floor(h) % 6;

    float v = value;
    float p = value * (1.0 - saturation);
    float q = value * (1.0 - f * saturation);
    float t = value * (1.0 - (1.0 - f) * saturation);

    if (i == 0) return float3(v, t, p);
    else if (i == 1) return float3(q, v, p);
    else if (i == 2) return float3(p, v, t);
    else if (i == 3) return float3(p, q, v);
    else if (i == 4) return float3(t, p, v);
    else return float3(v, p, q);
}

#endif // BASE_EFFECT_FXH