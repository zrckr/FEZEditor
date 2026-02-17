using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Tools;

public static class FaceExtensions
{
    public static FaceOrientation OrientationFromDirection(Vector3 direction)
    {
        if (direction == Vector3.Forward) return FaceOrientation.Back;
        if (direction == Vector3.Backward) return FaceOrientation.Front;
        if (direction == Vector3.Up) return FaceOrientation.Top;
        if (direction == Vector3.Down) return FaceOrientation.Down;
        if (direction == Vector3.Left) return FaceOrientation.Left;
        return FaceOrientation.Right;
    }
    
    public static bool IsPositive(this FaceOrientation face)
    {
        return face > FaceOrientation.Back;
    }
    
    public static Vector3 AsVector(this FaceOrientation face)
    {
        return face switch
        {
            FaceOrientation.Back => Vector3.Forward,
            FaceOrientation.Front => Vector3.Backward,
            FaceOrientation.Top => Vector3.Up,
            FaceOrientation.Down => Vector3.Down,
            FaceOrientation.Left => Vector3.Left,
            _ => Vector3.Right
        };
    }

    public static Quaternion AsQuaternion(this FaceOrientation face)
    {
        return face switch
        {
            FaceOrientation.Front => Quaternion.Identity,                                          // +Z, no rotation
            FaceOrientation.Back => Quaternion.CreateFromAxisAngle(Vector3.Up, MathF.PI),          // -Z, 180° around Y
            FaceOrientation.Right => Quaternion.CreateFromAxisAngle(Vector3.Up, MathHelper.PiOver2),  // +X, 90° around Y
            FaceOrientation.Left => Quaternion.CreateFromAxisAngle(Vector3.Up, -MathHelper.PiOver2),  // -X, -90° around Y
            FaceOrientation.Top => Quaternion.CreateFromAxisAngle(Vector3.Right, -MathHelper.PiOver2), // +Y, -90° around X
            FaceOrientation.Down => Quaternion.CreateFromAxisAngle(Vector3.Right, MathHelper.PiOver2), // -Y, 90° around X
            _ => Quaternion.Identity
        };
    }
    
    public static FaceOrientation GetTangent(this FaceOrientation face)
    {
        return face switch
        {
            FaceOrientation.Back => FaceOrientation.Top,
            FaceOrientation.Front => FaceOrientation.Top,
            FaceOrientation.Top or FaceOrientation.Down => FaceOrientation.Right,
            _ => FaceOrientation.Front
        };
    }

    public static FaceOrientation GetBitangent(this FaceOrientation face)
    {
        return face switch
        {
            FaceOrientation.Back or FaceOrientation.Front => FaceOrientation.Right,
            FaceOrientation.Top or FaceOrientation.Down => FaceOrientation.Front,
            _ => FaceOrientation.Top
        };
    }
    
    public static Vector3 UpVector(this FaceOrientation face)
    {
        return face switch
        {
            FaceOrientation.Back => Vector3.Up,
            FaceOrientation.Front => Vector3.Up,
            FaceOrientation.Left => Vector3.Up,
            FaceOrientation.Right => Vector3.Up,
            FaceOrientation.Down or FaceOrientation.Top => Vector3.Backward,
            _ => Vector3.Zero
        };
    }

    public static Vector3 RightVector(this FaceOrientation view)
    {
        return view switch
        {
            FaceOrientation.Back => Vector3.Left,
            FaceOrientation.Front => Vector3.Right,
            FaceOrientation.Left => Vector3.Backward,
            FaceOrientation.Right => Vector3.Forward,
            FaceOrientation.Down => Vector3.Right,
            FaceOrientation.Top => Vector3.Right,
            _ => Vector3.Zero
        };
    }
}