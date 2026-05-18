using FezEditor.Structure;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

public enum SymmetryMode
{
    None,
    Horizontal,
    Vertical,
    Corners
}

public static class SymmetryExtensions
{
    public static IEnumerable<TrixelFace> GetSymmetricFaces(this SymmetryMode mode, TrixelFace face, TrixelObject obj)
    {
        if (mode == SymmetryMode.None)
        {
            yield break;
        }

        var hFace = mode is SymmetryMode.Horizontal or SymmetryMode.Corners
            ? MirrorAlongUp(face, obj)
            : null;

        var vFace = mode is SymmetryMode.Vertical or SymmetryMode.Corners
            ? MirrorAlongRight(face, obj)
            : null;

        if (hFace.HasValue && hFace.Value != face)
        {
            yield return hFace.Value;
        }

        if (vFace.HasValue && vFace.Value != face)
        {
            yield return vFace.Value;
        }

        if (mode == SymmetryMode.Corners && hFace.HasValue && vFace.HasValue)
        {
            var hvFace = MirrorAlongRight(hFace.Value, obj);
            if (hvFace.HasValue && hvFace.Value != face)
            {
                yield return hvFace.Value;
            }
        }
    }

    private static TrixelFace? MirrorAlongRight(TrixelFace face, TrixelObject obj)
    {
        var r = face.Face.RightVector();
        var emp = face.Emplacement;
        var mirroredEmp = new Vector3I(
            Math.Abs(r.X) > 0.5f ? obj.Width - 1 - emp.X : emp.X,
            Math.Abs(r.Y) > 0.5f ? obj.Height - 1 - emp.Y : emp.Y,
            Math.Abs(r.Z) > 0.5f ? obj.Depth - 1 - emp.Z : emp.Z
        );

        if (!obj.SizeContains(mirroredEmp))
        {
            return null;
        }

        var mirroredFace = Math.Abs(Vector3.Dot(face.Face.AsVector(), r)) > 0.5f
            ? face.Face.GetOpposite()
            : face.Face;

        return new TrixelFace(mirroredEmp, mirroredFace);
    }

    private static TrixelFace? MirrorAlongUp(TrixelFace face, TrixelObject obj)
    {
        var u = face.Face.UpVector();
        var emp = face.Emplacement;
        var mirroredEmp = new Vector3I(
            Math.Abs(u.X) > 0.5f ? obj.Width - 1 - emp.X : emp.X,
            Math.Abs(u.Y) > 0.5f ? obj.Height - 1 - emp.Y : emp.Y,
            Math.Abs(u.Z) > 0.5f ? obj.Depth - 1 - emp.Z : emp.Z
        );

        if (!obj.SizeContains(mirroredEmp))
        {
            return null;
        }

        var mirroredFace = Math.Abs(Vector3.Dot(face.Face.AsVector(), u)) > 0.5f
            ? face.Face.GetOpposite()
            : face.Face;

        return new TrixelFace(mirroredEmp, mirroredFace);
    }
}