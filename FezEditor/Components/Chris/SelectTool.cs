using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Chris;

internal class SelectTool : BaseTool
{
    private static readonly Color HoverColor = Color.Blue with { A = 85 };

    private static readonly Color SelectionColor = Color.Red with { A = 85 };

    private TrixelFace? _dragStartFace;

    public SelectTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool == ChrisTool.Look)
        {
            return;
        }

        if (Chris.Hit is not null)
        {
            var surface = BuildTrixelFaceQuad(Chris.Hit.Value);
            Chris.Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
        }

        if (Chris.SelectedFaces.Count != 0)
        {
            var surfaces = Chris.SelectedFaces.Select(tf => (BuildTrixelFaceQuad(tf), PrimitiveType.TriangleList));
            Chris.Cursor.SetSelectionSurfaces(surfaces, SelectionColor);
        }
    }

    protected override void Act()
    {
        if (!Chris.IsViewportHovered || Chris.CurrentTool is not (ChrisTool.Add or ChrisTool.Remove or ChrisTool.Paint))
        {
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragStartFace = Chris.Hit;
            if (!Chris.Hit.HasValue)
            {
                ClearSelection();
                return;
            }
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _dragStartFace = null;
        }
        else if (Chris.Hit.HasValue && _dragStartFace.HasValue)
        {
            var orientation = _dragStartFace.Value.Face;
            if (orientation == Chris.Hit.Value.Face)
            {
                var newSelection = BuildRectSelection(orientation, _dragStartFace.Value.Emplacement, Chris.Hit.Value.Emplacement);
                if (orientation != Chris.SelectionOrientation || !newSelection.SetEquals(Chris.SelectedFaces))
                {
                    Chris.SelectionOrientation = orientation;
                    Chris.SelectedFaces.Clear();
                    foreach (var f in newSelection)
                    {
                        Chris.SelectedFaces.Add(f);
                    }
                }
            }
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return true;
    }

    private void ClearSelection()
    {
        Chris.SelectedFaces.Clear();
        Chris.SelectionOrientation = null;
    }

    private HashSet<TrixelFace> BuildRectSelection(FaceOrientation orientation, Vector3I start, Vector3I end)
    {
        var normal = orientation.AsVector();
        var tan = orientation.GetTangent().AsVector();
        var bitan = orientation.GetBitangent().AsVector();

        var startN = (int)((start.X * normal.X) + (start.Y * normal.Y) + (start.Z * normal.Z));
        var startT = (int)((start.X * tan.X) + (start.Y * tan.Y) + (start.Z * tan.Z));
        var startB = (int)((start.X * bitan.X) + (start.Y * bitan.Y) + (start.Z * bitan.Z));
        var endT = (int)((end.X * tan.X) + (end.Y * tan.Y) + (end.Z * tan.Z));
        var endB = (int)((end.X * bitan.X) + (end.Y * bitan.Y) + (end.Z * bitan.Z));

        var minT = Math.Min(startT, endT);
        var maxT = Math.Max(startT, endT);
        var minB = Math.Min(startB, endB);
        var maxB = Math.Max(startB, endB);

        var result = new HashSet<TrixelFace>();
        foreach (var tf in Chris.Obj.VisibleFaces)
        {
            if (tf.Face != orientation)
            {
                continue;
            }

            var n = (int)((tf.Emplacement.X * normal.X) + (tf.Emplacement.Y * normal.Y) + (tf.Emplacement.Z * normal.Z));
            if (n != startN)
            {
                continue;
            }

            var t = (int)((tf.Emplacement.X * tan.X) + (tf.Emplacement.Y * tan.Y) + (tf.Emplacement.Z * tan.Z));
            var b = (int)((tf.Emplacement.X * bitan.X) + (tf.Emplacement.Y * bitan.Y) + (tf.Emplacement.Z * bitan.Z));
            if (t >= minT && t <= maxT && b >= minB && b <= maxB)
            {
                result.Add(tf);
            }
        }

        return result;
    }

    private MeshSurface BuildTrixelFaceQuad(TrixelFace tf)
    {
        var faceCenter = (tf.Emplacement.ToVector3() + ((Vector3.One + tf.Face.AsVector()) * 0.5f)) * Mathz.TrixelSize - Chris.Obj.Offset;
        var origin = faceCenter + tf.Face.AsVector() * CursorMesh.OverlayOffset * Mathz.TrixelSize;
        return MeshSurface.CreateFaceQuad(Vector3.One * Mathz.TrixelSize, origin, tf.Face);
    }
}
