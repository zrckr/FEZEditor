using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class Mathz
{
    public static readonly Vector3 XzMask = Vector3.UnitX + Vector3.UnitZ;

    public const float TrixelSize = 1f / 16f;

    public static bool IsEqualApprox(float lhs, float rhs)
    {
        return Math.Abs(lhs - rhs) < float.Epsilon;
    }

    public static bool IsZeroApprox(float value)
    {
        return MathF.Abs(value) < float.Epsilon;
    }

    public static float Frac(float value)
    {
        return value - (int)value;
    }

    public static Matrix CreateTextureTransform(Rectangle rectangle, Vector2 size)
    {
        return new Matrix(
            rectangle.Width / size.X, 0f, 0f, 0f,
            0f, rectangle.Height / size.Y, 0f, 0f,
            rectangle.X / size.X,
            rectangle.Y / size.Y, 1f, 0f,
            0f, 0f, 0f, 0f
        );
    }

    public static Quaternion CreateYBillboard(Matrix view, Vector3 position)
    {
        var cameraPos = Matrix.Invert(view).Translation;
        var toCamera = cameraPos - position;
        var angleY = (float)Math.Atan2(toCamera.X, toCamera.Z);
        return Quaternion.CreateFromAxisAngle(Vector3.Up, angleY);
    }

    public static BoundingBox ComputeBoundingBox(Vector3 position, Quaternion rotation, Vector3 scale, Vector3 size)
    {
        var halfExtents = size * 0.5f;
        var worldMatrix = Matrix.CreateScale(scale) *
                          Matrix.CreateFromQuaternion(rotation) *
                          Matrix.CreateTranslation(position);

        var localCorners = new Vector3[]
        {
            new(-halfExtents.X, -halfExtents.Y, -halfExtents.Z), // left-bottom-back
            new(halfExtents.X, -halfExtents.Y, -halfExtents.Z), // right-bottom-back
            new(-halfExtents.X, halfExtents.Y, -halfExtents.Z), // left-top-back
            new(halfExtents.X, halfExtents.Y, -halfExtents.Z), // right-top-back
            new(-halfExtents.X, -halfExtents.Y, halfExtents.Z), // left-bottom-front
            new(halfExtents.X, -halfExtents.Y, halfExtents.Z), // right-bottom-front
            new(-halfExtents.X, halfExtents.Y, halfExtents.Z), // left-top-front
            new(halfExtents.X, halfExtents.Y, halfExtents.Z) // right-top-front
        };

        var worldCorners = new Vector3[8];
        for (var i = 0; i < 8; i++)
        {
            worldCorners[i] = Vector3.Transform(localCorners[i], worldMatrix);
        }

        var min = worldCorners[0];
        var max = worldCorners[0];
        for (var i = 1; i < 8; i++)
        {
            min = Vector3.Min(min, worldCorners[i]);
            max = Vector3.Max(max, worldCorners[i]);
        }

        return new BoundingBox(min, max);
    }

    public static FaceOrientation DetermineFace(BoundingBox box, Ray ray, float distance)
    {
        var point = ray.Position + ray.Direction * distance;
        var center = (box.Min + box.Max) / 2f;
        var bounds = (box.Max - box.Min) / 2f;

        var local = point - center;
        var abs = new Vector3
        {
            X = MathF.Abs(local.X / bounds.X),
            Y = MathF.Abs(local.Y / bounds.Y),
            Z = MathF.Abs(local.Z / bounds.Z)
        };

        Vector3 normal;
        if (abs.X > abs.Y && abs.X > abs.Z)
        {
            normal = new Vector3(MathF.Sign(local.X), 0, 0);
        }
        else if (abs.Y > abs.Z)
        {
            normal = new Vector3(0, MathF.Sign(local.Y), 0);
        }
        else
        {
            normal = new Vector3(0, 0, MathF.Sign(local.Z));
        }

        return FaceExtensions.OrientationFromDirection(normal);
    }

    public static Vector3 Abs(this Vector3 vector)
    {
        return new Vector3(
            Math.Abs(vector.X),
            Math.Abs(vector.Y),
            Math.Abs(vector.Z)
        );
    }

    public static Vector3 Round(this Vector3 vector, int decimals = 10)
    {
        return new Vector3(
            MathF.Round(vector.X, decimals),
            MathF.Round(vector.Y, decimals),
            MathF.Round(vector.Z, decimals)
        );
    }

    public static float Between(this Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    public static Vector3 ToEuler(this Quaternion q)
    {
        var sinRCosP = 2f * (q.W * q.X + q.Y * q.Z);
        var cosRCosP = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        var roll = MathF.Atan2(sinRCosP, cosRCosP);

        var sinP = 2f * (q.W * q.Y - q.Z * q.X);
        var pitch = MathF.Abs(sinP) >= 1f ? MathF.CopySign(MathHelper.PiOver2, sinP) : MathF.Asin(sinP);

        var sinYCosP = 2f * (q.W * q.Z + q.X * q.Y);
        var cosYCosP = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        var yaw = MathF.Atan2(sinYCosP, cosYCosP);

        return new Vector3(
            MathHelper.ToDegrees(roll),
            MathHelper.ToDegrees(pitch),
            MathHelper.ToDegrees(yaw));
    }

    public static Quaternion FromEuler(this Vector3 euler)
    {
        return Quaternion.CreateFromYawPitchRoll(
            MathHelper.ToRadians(euler.Y),
            MathHelper.ToRadians(euler.X),
            MathHelper.ToRadians(euler.Z));
    }

    public static float? IntersectsTriangle(this Ray ray, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var edge1 = v1 - v0;
        var edge2 = v2 - v0;
        var h = Vector3.Cross(ray.Direction, edge2);
        var a = Vector3.Dot(edge1, h);
        if (MathF.Abs(a) < float.Epsilon)
        {
            return null;
        }

        var f = 1f / a;
        var s = ray.Position - v0;
        var u = f * Vector3.Dot(s, h);
        if (u is < 0f or > 1f)
        {
            return null;
        }

        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(ray.Direction, q);
        if (v < 0f || u + v > 1f)
        {
            return null;
        }

        var t = f * Vector3.Dot(edge2, q);
        return t > float.Epsilon ? t : null;
    }
}