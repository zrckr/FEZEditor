using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class BucketTool : TextureTool
{
    public BucketTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool != ChrisTool.Bucket)
        {
            return;
        }

        var cancelEsc = ImGui.IsKeyPressed(ImGuiKey.Escape);
        var clickOnUnselected =
            Chris.IsViewportHovered &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            !(Chris.Hit.HasValue && Chris.SelectedFaces.Contains(Chris.Hit.Value));
        if (cancelEsc || clickOnUnselected)
        {
            Chris.SelectedFaces.Clear();
            Chris.SelectionOrientation = null;
        }
    }

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB", "Fill")
        );

        if (!Chris.Hit.HasValue || !Chris.IsViewportHovered)
        {
            return;
        }

        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (Chris.SelectedFaces.Count > 0 &&
            Chris.SelectedFaces.Contains(Chris.Hit.Value))
        {
            using (Chris.History.BeginScope("Paint Trixel Selection"))
            {
                foreach (var face in Chris.SelectedFaces)
                {
                    PaintTrixel(face);
                }
            }
        }
        else
        {
            using (Chris.History.BeginScope("Fill Paint Trixels"))
            {
                FloodFillTrixels(Chris.Hit.Value);
            }
        }

        FlushPaintChanges();
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Bucket;
    }

    private void FloodFillTrixels(TrixelFace originFace)
    {
        var obj = Chris.Obj;
        var clickedColor = GetTrixelColor(originFace);

        if (clickedColor == Chris.PaintColor)
        {
            return;
        }

        var floodAxisX = new Vector3I(originFace.Face.GetTangent().AsVector());
        var floodAxisY = new Vector3I(originFace.Face.GetBitangent().AsVector());
        var floodAxisZ = new Vector3I(originFace.Face.AsVector());

        var facesToPaint = new HashSet<TrixelFace>();

        Vector3I[] floodFillOffsets = [floodAxisX, floodAxisY, floodAxisX * -1, floodAxisY * -1];

        var facesToExpandFrom = new HashSet<TrixelFace>();
        facesToExpandFrom.Add(originFace);

        while (facesToExpandFrom.Count > 0)
        {
            var newFacesToExpandFrom = new HashSet<TrixelFace>();
            foreach(var face in facesToExpandFrom)
            {
                if (facesToPaint.Contains(face))
                {
                    continue;
                }

                if (!obj.SizeContains(face.Emplacement) || obj.IsMissing(face.Emplacement))
                {
                    continue;
                }

                var coveringTrixel = face.Emplacement + floodAxisZ;
                if (obj.SizeContains(coveringTrixel) && !obj.IsMissing(coveringTrixel))
                {
                    continue;
                }

                var faceColor = GetTrixelColor(face);
                if (faceColor != clickedColor)
                {
                    continue;
                }

                facesToPaint.Add(face);
                foreach (var offset in floodFillOffsets)
                {
                    var offsetFace = new TrixelFace(face.Emplacement + offset, face.Face);
                    newFacesToExpandFrom.Add(offsetFace);
                }
            }

            facesToExpandFrom = newFacesToExpandFrom;
        }

        if (facesToPaint.Count == 0)
        {
            return;
        }

        foreach (var face in facesToPaint)
        {
            PaintTrixel(face);
        }
    }
}
