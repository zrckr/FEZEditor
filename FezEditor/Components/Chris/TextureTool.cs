using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal abstract class TextureTool : BaseTool
{
    private bool _dirty;

    protected TextureTool(Game game, IChrisEditor chris) : base(game, chris) { }

    protected Color GetTrixelColor(TrixelFace face)
    {
        var textureData = Chris.Obj.Texture.TextureData;
        var idx = GetTrixelFaceTextureDataIndex(face);

        return new Color(
            textureData[idx + 0],
            textureData[idx + 1],
            textureData[idx + 2],
            textureData[idx + 3]
        );
    }

    protected void PaintTrixel(TrixelFace face)
    {
        PaintFace(face);
        foreach (var mirrored in Chris.SymmetryMode.GetSymmetricFaces(face, Chris.Obj))
        {
            PaintFace(mirrored);
        }
    }

    private void PaintFace(TrixelFace face)
    {
        var textureData = Chris.Obj.Texture.TextureData;
        var color = Chris.PaintColor;

        var idx = GetTrixelFaceTextureDataIndex(face);

        if (Chris.PaintMode is PaintMode.Color)
        {
            textureData[idx + 0] = color.R;
            textureData[idx + 1] = color.G;
            textureData[idx + 2] = color.B;
        }
        else if (Chris.PaintMode is PaintMode.Emission)
        {
            textureData[idx + 3] = color.A;
        }

        _dirty = true;
    }

    protected void FlushPaintChanges()
    {
        if (_dirty)
        {
            _dirty = false;
            Chris.Trixels.UpdateTextureDataFrom(Chris.Obj.Texture);
        }
    }

    private int GetTrixelFaceTextureDataIndex(TrixelFace face)
    {
        var obj = Chris.Obj;

        var (lx, y) = face.Face switch
        {
            FaceOrientation.Front => (face.Emplacement.X, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Right => (obj.Depth - 1 - face.Emplacement.Z, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Back => (obj.Width - 1 - face.Emplacement.X, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Left => (face.Emplacement.Z, obj.Height - 1 - face.Emplacement.Y),
            FaceOrientation.Top => (face.Emplacement.X, face.Emplacement.Z),
            FaceOrientation.Down => (face.Emplacement.X, obj.Depth - 1 - face.Emplacement.Z),
            _ => throw new InvalidOperationException()
        };

        var faceIndex = Array.IndexOf(FaceExtensions.NaturalOrder, face.Face);
        var x = faceIndex * obj.Texture.Width / 6 + lx;
        var idx = (y * obj.Texture.Width + x) * 4;

        return idx;
    }
}