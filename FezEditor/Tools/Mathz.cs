using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class Mathz
{
    public const float TrixelSize = 1f / 16f;

    public static Vector3 XzMask { get; set; } = new(1f, 0f, 1f);

    public static float Frac(float number)
    {
        return number - (int)number;
    }

    public static bool IsEqualApprox(float lhs, float rhs)
    {
        return Math.Abs(lhs - rhs) < float.Epsilon;
    }

    public static bool IsZeroApprox(float value)
    {
        return MathF.Abs(value) < float.Epsilon;
    }

    public static int Clamp(int value, int min, int max)
    {
        value = value > max ? max : value;
        value = value < min ? min : value;
        return value;
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
}