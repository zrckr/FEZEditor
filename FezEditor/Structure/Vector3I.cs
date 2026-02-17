using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public struct Vector3I : IEquatable<Vector3I>, IComparable<Vector3I>
{
    public int X;

    public int Y;

    public int Z;
    
    public Vector3I(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    
    public Vector3I(float x, float y, float z)
    {
        X = (int)MathF.Round(x);
        Y = (int)MathF.Round(y);
        Z = (int)MathF.Round(z);
    }

    public Vector3I(Vector3I vector)
    {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
    }
    
    public Vector3I(Vector3 vector)
    {
        X = (int)MathF.Round(vector.X);
        Y = (int)MathF.Round(vector.Y);
        Z = (int)MathF.Round(vector.Z);
    }
    
    public readonly Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Vector3I vector3I)
        {
            return Equals(vector3I);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return X ^ (Y << 10) ^ (Z << 20);
    }

    public override string ToString()
    {
        var sb = new StringBuilder(32);
        sb.Append("{X:");
        sb.Append(X);
        sb.Append(" Y:");
        sb.Append(Y);
        sb.Append(" Z:");
        sb.Append(Z);
        sb.Append('}');
        return sb.ToString();
    }

    public bool Equals(Vector3I other)
    {
        if (other.X == X && other.Y == Y)
        {
            return other.Z == Z;
        }

        return false;
    }

    public int CompareTo(Vector3I other)
    {
        var num = X.CompareTo(other.X);
        if (num == 0)
        {
            num = Y.CompareTo(other.Y);
            if (num == 0)
            {
                num = Z.CompareTo(other.Z);
            }
        }

        return num;
    }

    public static bool operator ==(Vector3I lhs, Vector3I rhs)
    {
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Vector3I lhs, Vector3I rhs)
    {
        return !(lhs == rhs);
    }

    public static Vector3I operator +(Vector3I lhs, Vector3I rhs)
    {
        return new Vector3I(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
    }

    public static Vector3I operator -(Vector3I lhs, Vector3I rhs)
    {
        return new Vector3I(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
    }

    public static Vector3I operator +(Vector3I lhs, Vector3 rhs)
    {
        return new Vector3I(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);
    }

    public static Vector3I operator -(Vector3I lhs, Vector3 rhs)
    {
        return new Vector3I(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);
    }

    public static Vector3I operator /(Vector3I lhs, float rhs)
    {
        return new Vector3I(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);
    }

    public static bool operator <(Vector3I lhs, Vector3I rhs)
    {
        return lhs.CompareTo(rhs) < 0;
    }

    public static bool operator >(Vector3I lhs, Vector3I rhs)
    {
        return lhs.CompareTo(rhs) > 0;
    }

    public static bool operator <(Vector3I lhs, Vector3 rhs)
    {
        return lhs.CompareTo(new Vector3I(rhs)) < 0;
    }

    public static bool operator >(Vector3I lhs, Vector3 rhs)
    {
        return lhs.CompareTo(new Vector3I(rhs)) > 0;
    }
    
    public static Vector3I Zero { get; } = new(0, 0, 0);

    public static Vector3I One { get; } = new(1, 1, 1);

    public static Vector3I Up { get; } = new(0, 1, 0);

    public static Vector3I Down { get; } = new(0, -1, 0);

    public static Vector3I Right { get; } = new(1, 0, 0);

    public static Vector3I Left { get; } = new(-1, 0, 0);

    public static Vector3I Forward { get; } = new(0, 0, -1);

    public static Vector3I Backward { get; } = new(0, 0, 1);
}