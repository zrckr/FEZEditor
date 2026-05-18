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

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB", "Fill")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Chris is { IsViewportHovered: true, Hit: not null })
        {
            using (Chris.History.BeginScope("Fill Paint Trixels"))
            {
                FloodFillTrixels(Chris.Hit.Value);
            }

            FlushPaintChanges();
        }
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

        Vector3I[] floodFillOffsets = [floodAxisX, floodAxisY, floodAxisX * -1, floodAxisY * -1];

        var facesToPaint = new HashSet<TrixelFace>();
        var facesToExpandFrom = new HashSet<TrixelFace> { originFace };

        while (facesToExpandFrom.Count > 0)
        {
            var next = new HashSet<TrixelFace>();
            foreach (var face in facesToExpandFrom)
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

                if (GetTrixelColor(face) != clickedColor)
                {
                    continue;
                }

                facesToPaint.Add(face);
                foreach (var offset in floodFillOffsets)
                {
                    next.Add(new TrixelFace(face.Emplacement + offset, face.Face));
                }
            }

            facesToExpandFrom = next;
        }

        foreach (var face in facesToPaint)
        {
            PaintTrixel(face);
        }
    }
}
