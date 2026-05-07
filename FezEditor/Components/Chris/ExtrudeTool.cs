using FezEditor.Structure;
using FezEditor.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Chris;

internal class ExtrudeTool : BaseTool
{
    private ExtrudeState _extrude = new();

    private IDisposable? _extrudeScope;

    public ExtrudeTool(Game game, IChrisEditor chris) : base(game, chris)
    {
    }

    protected override void TestConditions()
    {
        if (Chris.CurrentTool != ChrisTool.Extrude)
        {
            return;
        }

        var cancelEsc = ImGui.IsKeyPressed(ImGuiKey.Escape);
        var cancelClick = !Chris.Gizmo.IsActive && Chris.IsViewportHovered &&
                          ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !Chris.Hit.HasValue;
        var nothingSelected = Chris.SelectedFaces.Count < 1 || !Chris.SelectionOrientation.HasValue;
        if (cancelEsc || cancelClick || nothingSelected)
        {
            Chris.SelectedFaces.Clear();
            Chris.SelectionOrientation = null;
            Chris.CurrentTool = ChrisTool.Select;
        }
    }

    protected override void Act()
    {
        StatusService.AddHints(
            ("LMB Drag", "Extrude / Intrude")
        );

        var emps = Chris.SelectedFaces.Select(tf => tf.Emplacement).ToList();
        var orientation = Chris.SelectionOrientation!.Value;

        var meshOffset = Vector3.Zero - (Chris.Obj.Size / 2f);
        var centroid = emps.Aggregate(Vector3.Zero, (s, e) => s + e.ToVector3()) / emps.Count;
        var origin = centroid * Mathz.TrixelSize + meshOffset
                                                 + orientation.AsVector() * Mathz.TrixelSize;

        if (Chris.Gizmo.ScaleFace(origin, orientation, out var delta))
        {
            var steps = (int)MathF.Round(delta / Mathz.TrixelSize);
            if (steps != _extrude.PreviousSteps)
            {
                var dir = steps > _extrude.PreviousSteps ? 1 : -1;
                for (var s = _extrude.PreviousSteps; s != steps; s += dir)
                {
                    _extrude.Ops.Enqueue(new ExtrudeOp(dir > 0 ? s + dir : s, dir > 0));
                }

                _extrude.PreviousSteps = steps;
            }
        }

        var size = new Vector3I(Chris.Obj.Width, Chris.Obj.Height, Chris.Obj.Depth);
        var dirty = false;

        while (_extrude.Ops.Count > 0)
        {
            var op = _extrude.Ops.Dequeue();
            var targets = _extrude.Snapshot
                .Select(emp => emp + _extrude.Delta * op.Step)
                .Where(t => t >= Vector3I.Zero && t < size)
                .ToList();

            if (!op.Add)
            {
                var totalTrixels = Chris.Trixels.Faces.Select(tf => tf.Emplacement).Distinct().Count();
                if (totalTrixels - targets.Count < 1)
                {
                    targets.RemoveAt(targets.Count - 1);
                }
            }

            foreach (var target in targets)
            {
                Chris.Obj.SetMissing(target, !op.Add);
                dirty = true;
            }
        }

        if (dirty)
        {
            Chris.Trixels.Visualize(Chris.Obj);
            Chris.SelectedFaces.Clear();
            foreach (var emp in _extrude.Snapshot)
            {
                var next = emp + _extrude.Delta * _extrude.PreviousSteps;
                Chris.SelectedFaces.Add(new TrixelFace(next, orientation));
            }
        }

        if (Chris.Gizmo.DragStarted)
        {
            _extrudeScope?.Dispose();
            _extrudeScope = Chris.History.BeginScope("Extrude Trixels");
            _extrude = new ExtrudeState
            {
                Delta = new Vector3I(orientation.AsVector())
            };

            foreach (var trixelFace in Chris.SelectedFaces)
            {
                _extrude.Snapshot.Add(trixelFace.Emplacement);
            }
        }

        if (Chris.Gizmo.DragEnded)
        {
            _extrude = new ExtrudeState();
            _extrudeScope?.Dispose();
            _extrudeScope = null;
        }
    }

    protected override bool IsToolAllowed(ChrisTool tool)
    {
        return tool == ChrisTool.Extrude;
    }

    private record struct ExtrudeOp(int Step, bool Add);

    private struct ExtrudeState()
    {
        public readonly List<Vector3I> Snapshot = new();
        public readonly Queue<ExtrudeOp> Ops = new();
        public int PreviousSteps;
        public Vector3I Delta;
    }
}