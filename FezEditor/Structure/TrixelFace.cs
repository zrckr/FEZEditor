using FEZRepacker.Core.Definitions.Game.Common;

namespace FezEditor.Structure;

public struct TrixelFace : IEquatable<TrixelFace>
{
    public Vector3I Emplacement;

    public FaceOrientation Face;

    public bool Selected;

    public TrixelFace(Vector3I emplacement, FaceOrientation face)
    {
        Emplacement = emplacement;
        Face = face;
    }

    public override int GetHashCode()
    {
        return Emplacement.GetHashCode() + Face.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is TrixelFace trixelFace)
        {
            return Equals(trixelFace);
        }

        return false;
    }

    public bool Equals(TrixelFace other)
    {
        return other.Emplacement == Emplacement && other.Face == Face;
    }

    public static bool operator ==(TrixelFace left, TrixelFace right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TrixelFace left, TrixelFace right)
    {
        return !(left == right);
    }
}