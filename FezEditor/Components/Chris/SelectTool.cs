using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
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
        if (!Chris.IsViewportHovered || Chris.CurrentTool is not (ChrisTool.Add or ChrisTool.Remove or ChrisTool.Bucket))
        {
            return;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragStartFace = Chris.Hit;
            if (!Chris.Hit.HasValue)
            {
                Chris.SelectedFaces.Clear();
                return;
            }
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _dragStartFace = null;
        }
        else if (Chris.Hit.HasValue && _dragStartFace.HasValue)
        {
            var min = Vector3I.Min(_dragStartFace.Value.Emplacement, Chris.Hit.Value.Emplacement);
            var max = Vector3I.Max(_dragStartFace.Value.Emplacement, Chris.Hit.Value.Emplacement);
            var result = new HashSet<TrixelFace>();

            foreach (var tf in Chris.Obj.VisibleFaces)
            {
                if ((tf.Face == _dragStartFace.Value.Face || tf.Face == Chris.Hit.Value.Face) &&
                    tf.Emplacement >= min &&
                    tf.Emplacement <= max)
                {
                    result.Add(tf);
                }
            }

            if (!result.SetEquals(Chris.SelectedFaces))
            {
                Chris.SelectedFaces.Clear();
                foreach (var f in result)
                {
                    Chris.SelectedFaces.Add(f);
                }
            }
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return true;
    }

    private MeshSurface BuildTrixelFaceQuad(TrixelFace tf)
    {
        var faceCenter = (tf.Emplacement.ToVector3() + ((Vector3.One + tf.Face.AsVector()) * 0.5f)) * Mathz.TrixelSize - Chris.Obj.Offset;
        var origin = faceCenter + tf.Face.AsVector() * CursorMesh.OverlayOffset * Mathz.TrixelSize;
        return MeshSurface.CreateFaceQuad(Vector3.One * Mathz.TrixelSize, origin, tf.Face);
    }
}
