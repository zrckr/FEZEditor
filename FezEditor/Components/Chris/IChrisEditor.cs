using FezEditor.Actors;
using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.Common;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal interface IChrisEditor
{
    bool IsViewportHovered { get; }

    History History { get; }

    TrixelObject Obj { get; }

    TrixelFace? Hit { get; }

    CursorMesh Cursor { get; }

    TrixelsMesh Trixels { get; }

    Color PaintColor { get; set; }

    ChrisTool CurrentTool { get; set; }

    PaintMode CurrentPaintMode { get; set; }

    HashSet<TrixelFace> SelectedFaces { get; }

    FaceOrientation? SelectionOrientation { get; set; }
}
