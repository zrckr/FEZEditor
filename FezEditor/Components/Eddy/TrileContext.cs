using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal sealed class TrileContext : BaseContext
{
    private static readonly string[] Rotations = FaceExtensions.NaturalOrder
        .Where(fo => fo.IsSide())
        .Select(fo => fo.ToString())
        .ToArray();

    private readonly Dictionary<int, Actor> _trileActors = new();

    private Actor? _collisionMapActor;

    private TrileSet? _set;

    private CursorState _hoveredCursor = new();

    private CursorState _selectedCursor = new();

    private SelectState _select = new();

    private ScaleState _scale = new();

    private IDisposable? _translateScope;

    private IDisposable? _scaleScope;

    private readonly HashSet<PaintOp> _paintOps = new();

    private readonly Dictionary<TrileEmplacement, int> _emplacementGroups = new();

    private readonly Dictionary<int, HashSet<TrileEmplacement>> _groupEmplacements = new();

    private BoundingBox _levelBounds;

    private Vector3 _previousPositionDrag;

    public TrileContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        _hoveredCursor.Reset();
        if (Eddy.Visuals.IsDirty)
        {
            if (_collisionMapActor != null)
            {
                var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.CollisionMap);
                var collisionMesh = _collisionMapActor.GetComponent<TrileCollisionMesh>();
                collisionMesh.Visible = visible;
            }

            var triles = Eddy.Visuals.Value.HasFlag(EddyVisuals.Triles);
            var emptyTriles = Eddy.Visuals.Value.HasFlag(EddyVisuals.EmptyTriles);

            foreach (var actor in _trileActors.Values)
            {
                var trilesMesh = actor.GetComponent<TrilesMesh>();
                var visible = trilesMesh.HasGeometry ? triles : emptyTriles;
                actor.Visible = visible;
                trilesMesh.Pickable = visible;
            }
        }

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
        {
            var index = Eddy.Hit.Value.Index;
            if (index >= mesh.InstanceCount)
            {
                return;
            }

            var emplacement = mesh.GetEmplacement(index);
            if (Level.Triles.ContainsKey(emplacement) &&
                Eddy.Tool is EddyTool.Select or EddyTool.Pick or EddyTool.Paint)
            {
                var box = mesh.GetBounds().ElementAt(index);
                var distance = Eddy.Hit.Value.Distance;
                _hoveredCursor.Emplacements.Clear();
                _hoveredCursor.Emplacements.Add(emplacement);
                _hoveredCursor.Face = Mathz.DetermineFace(box, Eddy.Ray, distance);
                _hoveredCursor.GroupId = _emplacementGroups.TryGetValue(emplacement, out var gid) ? gid : null;
                Eddy.HoveredContext = EddyContext.Trile;
                return;
            }
        }

        if (_hoveredCursor.Emplacement == null && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            Eddy.SelectedContext == EddyContext.Trile &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedCursor.Reset();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedCursor.Emplacements.Count > 0)
        {
            Eddy.SelectedContext = EddyContext.Trile;
        }

        if (Eddy.AssetBrowser.Select(AssetType.Trile))
        {
            Eddy.Tool = EddyTool.Paint;
            Eddy.SelectedContext = EddyContext.Trile;
        }

        if (Eddy.InstanceBrowser.Select(out var sel) && sel.context == EddyContext.Trile)
        {
            Eddy.InstanceBrowser.Consume();
            if (_groupEmplacements.TryGetValue(sel.id, out var emps) && emps.Count > 0)
            {
                var centroid = emps.Aggregate(Vector3.Zero,
                    (sum, e) => sum + new Vector3(e.X, e.Y, e.Z)) / emps.Count + new Vector3(0.5f);
                Eddy.FocusOn(centroid);
            }
        }
    }

    protected override void Act()
    {
        Eddy.AllowedTools.UnionWith(Enum.GetValues<EddyTool>());

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selectedCursor.Reset();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        switch (Eddy.Tool)
        {
            case EddyTool.Select: UpdateSelect(); break;
            case EddyTool.Translate: UpdateTranslate(); break;
            case EddyTool.Rotate: UpdateRotate(); break;
            case EddyTool.Paint: UpdatePaint(); break;
            case EddyTool.Pick: UpdatePick(); break;
            case EddyTool.Scale: UpdateScale(); break;
            default: throw new InvalidOperationException();
        }

        if (_hoveredCursor.Emplacement != null)
        {
            // When hovering a grouped trile with no current selection, highlight the whole group as boxes
            if (_hoveredCursor.GroupId != null && _selectedCursor.Emplacements.Count == 0
                                               && _groupEmplacements.TryGetValue(_hoveredCursor.GroupId.Value,
                                                   out var groupSet))
            {
                Eddy.Cursor.SetHoverSurfaces(BuildBoxSurfaces(groupSet, HoverColor), HoverColor);
            }
            else if (Level.Triles.TryGetValue(_hoveredCursor.Emplacement, out var hoveredInstance))
            {
                var face = _hoveredCursor.Face ?? FaceOrientation.Front;
                var center = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
                var origin = center + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
                Eddy.Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
            }
        }

        if (_selectedCursor.Emplacements.Count > 0)
        {
            // Group selection: show boxes around each trile in group
            if (_selectedCursor.GroupId != null)
            {
                var surfaces = BuildBoxSurfaces(_selectedCursor.Emplacements, SelectionColor);
                Eddy.Cursor.SetSelectionSurfaces(surfaces, SelectionColor);
                return;
            }

            if (_selectedCursor.Face.HasValue)
            {
                var normal = _selectedCursor.Face.Value.AsVector();
                var faceSurfaces = _selectedCursor.Emplacements
                    .Where(e => Level.Triles.ContainsKey(e))
                    .Select(e =>
                    {
                        var trileCenter = Level.Triles[e].Position.ToXna() + new Vector3(0.5f);
                        var origin = trileCenter + normal * (0.5f + CursorMesh.OverlayOffset);
                        var s = MeshSurface.CreateFaceQuad(Vector3.One, origin, _selectedCursor.Face.Value);
                        return (s, PrimitiveType.TriangleList);
                    });
                Eddy.Cursor.SetSelectionSurfaces(faceSurfaces, SelectionColor);
            }
        }
    }

    private void UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
            ("Shift+LMB", "Add to Selection"),
            ("Alt+LMB", "Select Overlapped"),
            ("LMB Drag", "Select Multiple")
        );

        if (_selectedCursor.Emplacements.Count > 0)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );

            if (_selectedCursor.GroupId != null)
            {
                StatusService.AddHints(("Ctrl+Shift+G", "Ungroup"));
            }
            else if (_selectedCursor.Emplacements.Count > 1)
            {
                StatusService.AddHints(("Ctrl+G", "Group"));
            }
        }

        if (_select.Clipboard.Count > 0 && _hoveredCursor.Emplacement != null)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selectedCursor.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (Eddy.History.BeginScope("Delete Triles"))
            {
                RemoveSelected();
            }

            _selectedCursor.Reset();
        }

        if (ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.G) &&
            _selectedCursor.GroupId != null)
        {
            using (Eddy.History.BeginScope("Remove Trile Group"))
            {
                Level.Groups.Remove(_selectedCursor.GroupId.Value);
            }

            _selectedCursor.GroupId = null;
        }

        if (ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.G) &&
            _selectedCursor.Emplacements.Count > 1 && _selectedCursor.GroupId == null)
        {
            using (Eddy.History.BeginScope("Create Trile Group"))
            {
                var groupId = Level.Groups.Count > 0 ? Level.Groups.Keys.Max() + 1 : 0;
                var group = new TrileGroup();

                foreach (var emp in _selectedCursor.Emplacements)
                {
                    if (Level.Triles.TryGetValue(emp, out var instance))
                    {
                        group.Triles.Add(instance);
                        _emplacementGroups[emp] = groupId;
                    }
                }

                if (!_groupEmplacements.TryGetValue(groupId, out var set))
                {
                    set = new HashSet<TrileEmplacement>();
                    _groupEmplacements[groupId] = set;
                }

                set.UnionWith(_selectedCursor.Emplacements);
                Level.Groups[groupId] = group;
                _selectedCursor.GroupId = groupId;
            }
        }

        if (ImGui.GetIO().KeyCtrl)
        {
            if (_selectedCursor.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                BuildClipboard();
            }

            if (_selectedCursor.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                BuildClipboard();
                using (Eddy.History.BeginScope("Cut Triles"))
                {
                    RemoveSelected();
                }

                _selectedCursor.Emplacements.Clear();
                _selectedCursor.Face = null;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (Eddy.History.BeginScope("Paste Triles"))
                {
                    PasteClipboard();
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _select.RectOrigin = _hoveredCursor.Emplacement;
            _select.WasDrag = false;
            _select.PressedOnViewport = Eddy.IsViewportHovered;
        }

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _select.RectOrigin != null &&
            _hoveredCursor.Emplacement != null)
        {
            var min = _select.RectOrigin.Min(_hoveredCursor.Emplacement);
            var max = _select.RectOrigin.Max(_hoveredCursor.Emplacement);

            _selectedCursor.Emplacements.Clear();
            _selectedCursor.GroupId = null;
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        var emplacement = new TrileEmplacement(x, y, z);
                        if (!Level.Triles.ContainsKey(emplacement))
                        {
                            continue;
                        }

                        // Expand grouped triles to their full group
                        if (_emplacementGroups.TryGetValue(emplacement, out var gid) &&
                            _groupEmplacements.TryGetValue(gid, out var gset))
                        {
                            _selectedCursor.Emplacements.UnionWith(gset);
                        }
                        else
                        {
                            _selectedCursor.Emplacements.Add(emplacement);
                        }
                    }
                }
            }

            _selectedCursor.Face = _hoveredCursor.Face;
            _select.WasDrag = true;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && _select.PressedOnViewport)
        {
            var wasDrag = _select.WasDrag;
            _select.SelectionAnchor = _select.RectOrigin;
            _select.RectOrigin = null;
            _select.WasDrag = false;
            _select.PressedOnViewport = false;

            if (!wasDrag)
            {
                var shift = ImGui.GetIO().KeyShift;
                if (!shift)
                {
                    _selectedCursor.Emplacements.Clear();
                    _selectedCursor.GroupId = null;
                }

                if (_hoveredCursor.Emplacement != null)
                {
                    // If hovering a grouped trile, select the whole group
                    if (_hoveredCursor.GroupId != null &&
                        _groupEmplacements.TryGetValue(_hoveredCursor.GroupId.Value, out var groupSet))
                    {
                        if (shift && _selectedCursor.Emplacements.IsSupersetOf(groupSet))
                        {
                            _selectedCursor.Emplacements.ExceptWith(groupSet);
                        }
                        else
                        {
                            _selectedCursor.Emplacements.UnionWith(groupSet);
                        }

                        _selectedCursor.GroupId =
                            _selectedCursor.Emplacements.Count > 0 ? _hoveredCursor.GroupId : null;
                    }
                    else
                    {
                        if (shift && _selectedCursor.Emplacements.Contains(_hoveredCursor.Emplacement))
                        {
                            _selectedCursor.Emplacements.Remove(_hoveredCursor.Emplacement);
                        }
                        else
                        {
                            _selectedCursor.Emplacements.Add(_hoveredCursor.Emplacement);
                        }

                        if (shift)
                        {
                            _selectedCursor.GroupId = null;
                        }
                    }
                }
            }

            _selectedCursor.Face = _hoveredCursor.Face;
            if (_selectedCursor.Emplacements.Count > 0)
            {
                Eddy.SelectedContext = EddyContext.Trile;
            }
        }
    }

    private void UpdateTranslate()
    {
        StatusService.AddHints(
            ("R", "Reset")
        );

        if (_selectedCursor.Emplacements.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();
        if (Eddy.Gizmo.Translate(ref centroid))
        {
            var delta = centroid - ComputeSelectionCentroid();
            foreach (var emplacement in _selectedCursor.Emplacements.ToList())
            {
                if (Level.Triles.TryGetValue(emplacement, out var instance))
                {
                    var position = instance.Position.ToXna() + delta;
                    instance.Position = position.ToRepacker();
                    if (_trileActors.TryGetValue(instance.TrileId, out var actor))
                    {
                        var mesh = actor.GetComponent<TrilesMesh>();
                        mesh.SetInstanceData(emplacement, position, instance.PhiLight);
                    }
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate Trile");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _translateScope?.Dispose();
            _translateScope = null;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedCursor.Emplacements.Count > 0 && _translateScope == null)
        {
            using (Eddy.History.BeginScope("Reset Translate Trile"))
            {
                foreach (var emplacement in _selectedCursor.Emplacements)
                {
                    if (Level.Triles.TryGetValue(emplacement, out var instance))
                    {
                        var position = new Vector3(emplacement.X, emplacement.Y, emplacement.Z);
                        instance.Position = position.ToRepacker();
                        if (_trileActors.TryGetValue(instance.TrileId, out var actor))
                        {
                            var mesh = actor.GetComponent<TrilesMesh>();
                            mesh.SetInstanceData(emplacement, position, instance.PhiLight);
                        }
                    }
                }
            }
        }
    }

    private Vector3 ComputeSelectionCentroid()
    {
        if (_selectedCursor.Emplacements.Count == 0)
        {
            return Vector3.Zero;
        }

        var sum = _selectedCursor.Emplacements
            .Select(emplacement => Level.Triles[emplacement])
            .Select(instance => instance.Position.ToXna())
            .Aggregate(Vector3.Zero, (current, position) => current + position);

        return sum / _selectedCursor.Emplacements.Count;
    }

    private void UpdateRotate()
    {
        if (_selectedCursor.Emplacements.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();

        if (Eddy.Gizmo.Rotate(centroid))
        {
            using (Eddy.History.BeginScope("Rotate Trile(s)"))
            {
                foreach (var emplacement in _selectedCursor.Emplacements)
                {
                    var instance = Level.Triles[emplacement];
                    instance.PhiLight = (byte)((instance.PhiLight + 1) % 4);
                    if (_trileActors.TryGetValue(instance.TrileId, out var actor))
                    {
                        var mesh = actor.GetComponent<TrilesMesh>();
                        mesh.SetInstanceData(emplacement, instance.Position.ToXna(), instance.PhiLight);
                    }
                }
            }
        }
    }

    private void UpdateScale()
    {
        if (!_selectedCursor.Face.HasValue)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();
        var faceVec = _selectedCursor.Face.Value.AsVector();
        var selectionMin = _selectedCursor.Emplacements.Aggregate(
            new Vector3(float.MaxValue),
            (min, e) => Vector3.Min(min, new Vector3(e.X, e.Y, e.Z)));
        var disabled = faceVec.X < 0 && selectionMin.X <= 0 ||
                       faceVec.Y < 0 && selectionMin.Y <= 0 ||
                       faceVec.Z < 0 && selectionMin.Z <= 0;

        if (Eddy.Gizmo.ScaleFace(centroid, _selectedCursor.Face.Value, out var delta, disabled))
        {
            var steps = (int)MathF.Round(delta);
            if (steps != _scale.PreviousSteps)
            {
                var dir = steps > _scale.PreviousSteps ? 1 : -1;
                for (var s = _scale.PreviousSteps; s != steps; s += dir)
                {
                    _scale.Ops.Enqueue(new ScaleOp(dir > 0 ? s + dir : s, dir > 0));
                }

                _scale.PreviousSteps = steps;
            }
        }

        while (_scale.Ops.Count > 0)
        {
            var op = _scale.Ops.Dequeue();
            if (op.Add)
            {
                foreach (var entry in _scale.Snapshot)
                {
                    var target = new TrileEmplacement(
                        entry.Emp.X + _scale.Dx * op.Step,
                        entry.Emp.Y + _scale.Dy * op.Step,
                        entry.Emp.Z + _scale.Dz * op.Step);

                    if (target.X < 0 || target.Y < 0 || target.Z < 0)
                    {
                        continue;
                    }

                    if (Level.Triles.ContainsKey(target))
                    {
                        continue;
                    }

                    var instance = new TrileInstance
                    {
                        Position = new Vector3(target.X, target.Y, target.Z).ToRepacker(),
                        TrileId = entry.TrileId,
                        PhiLight = entry.PhiLight
                    };

                    Level.Triles[target] = instance;

                    var v = new Vector3(target.X, target.Y, target.Z);
                    var min = Vector3.Min(_levelBounds.Min, v);
                    var max = Vector3.Max(_levelBounds.Max, v);
                    ComputeLevelBounds(min, max);

                    if (_selectedCursor.GroupId != null)
                    {
                        AddToGroup(_selectedCursor.GroupId.Value, target, instance);
                    }

                    EnsureTrileActor(entry.TrileId).GetComponent<TrilesMesh>()
                        .SetInstanceData(target, instance.Position.ToXna(), entry.PhiLight);
                }
            }
            else
            {
                foreach (var entry in _scale.Snapshot)
                {
                    var target = new TrileEmplacement(
                        entry.Emp.X + _scale.Dx * op.Step,
                        entry.Emp.Y + _scale.Dy * op.Step,
                        entry.Emp.Z + _scale.Dz * op.Step);
                    if (!Level.Triles.Remove(target, out var removedInst))
                    {
                        continue;
                    }

                    RemoveFromGroup(target, removedInst);
                    if (_trileActors.TryGetValue(removedInst.TrileId, out var actor))
                    {
                        var mesh = actor.GetComponent<TrilesMesh>();
                        mesh.RemoveInstance(target);
                        CleanupEmptyActor(removedInst.TrileId, mesh);
                    }
                }
            }

            _selectedCursor.Emplacements.Clear();
            foreach (var entry in _scale.Snapshot)
            {
                var target = new TrileEmplacement(
                    entry.Emp.X + _scale.Dx * _scale.PreviousSteps,
                    entry.Emp.Y + _scale.Dy * _scale.PreviousSteps,
                    entry.Emp.Z + _scale.Dz * _scale.PreviousSteps);
                if (Level.Triles.ContainsKey(target))
                {
                    _selectedCursor.Emplacements.Add(target);
                }
            }

            UpdateCollisionMesh();
            EnsurePlaceholder();
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _scaleScope?.Dispose();
            _scaleScope = Eddy.History.BeginScope("Scale Triles");
            _scale = new ScaleState();

            var faceVec1 = _selectedCursor.Face.Value.AsVector();
            _scale.Dx = (int)faceVec1.X;
            _scale.Dy = (int)faceVec1.Y;
            _scale.Dz = (int)faceVec1.Z;

            foreach (var emplacement in _selectedCursor.Emplacements)
            {
                if (Level.Triles.TryGetValue(emplacement, out var inst))
                {
                    _scale.Snapshot.Add(new TrileEntry(emplacement, inst.TrileId, inst.PhiLight));
                }
            }
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _scale = new ScaleState();
            _scaleScope?.Dispose();
            _scaleScope = null;
        }
    }

    public override void DrawOverlay()
    {
        if (Eddy.Tool != EddyTool.Paint || !Eddy.IsViewportHovered)
        {
            return;
        }

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.Trile);
        if (string.IsNullOrEmpty(entry))
        {
            return;
        }

        var thumb = Eddy.AssetBrowser.GetThumbnail(AssetType.Trile, entry);
        if (thumb != null)
        {
            var mousePos = ImGui.GetMousePos();
            var drawMin = mousePos + new NVector2(12f, 12f);
            var drawMax = drawMin + new NVector2(32f, 32f);
            ImGui.GetForegroundDrawList(ImGui.GetMainViewport())
                .AddImage(ImGuiX.Bind(thumb), drawMin, drawMax);
        }
    }

    private void UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Paint"),
            ("Shift+LMB", "Append"),
            ("Ctrl+LMB", "Erase")
        );

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.Trile);
        var trileId = !string.IsNullOrEmpty(entry)
            ? _set!.FindByName(entry).Id
            : InvalidId;

        #region Selected triles

        if (_selectedCursor.Emplacements.Count > 0 &&
            _selectedCursor.Emplacements.Contains(_hoveredCursor.Emplacement ?? new TrileEmplacement()) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var emplacements = _selectedCursor.Emplacements.ToList();
            if (ImGui.GetIO().KeyShift)
            {
                foreach (var emplacement in emplacements)
                {
                    AppendNewTrile(emplacement);
                }

                UpdateCollisionMesh();
            }
            else if (ImGui.GetIO().KeyCtrl)
            {
                foreach (var emplacement in emplacements)
                {
                    EraseTrile(emplacement);
                }

                UpdateCollisionMesh();
                EnsurePlaceholder();
            }
            else
            {
                foreach (var emplacement in emplacements)
                {
                    ChangeTrile(emplacement);
                }

                UpdateCollisionMesh();
            }
        }

        #endregion

        #region Single hovered trile

        if (_hoveredCursor is { Emplacement: not null, Face: not null } &&
            _selectedCursor.Emplacements.Count == 0 &&
            (ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
             ImGui.IsMouseDragging(ImGuiMouseButton.Left)))
        {
            Eddy.SelectedContext = EddyContext.Trile;
            if (ImGui.GetIO().KeyShift)
            {
                AppendNewTrile(_hoveredCursor.Emplacement!);
                UpdateCollisionMesh();
            }
            else if (ImGui.GetIO().KeyCtrl)
            {
                EraseTrile(_hoveredCursor.Emplacement!);
                UpdateCollisionMesh();
                EnsurePlaceholder();
            }
            else
            {
                ChangeTrile(_hoveredCursor.Emplacement!);
                UpdateCollisionMesh();
            }
        }

        #endregion

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && _paintOps.Count != 0)
        {
            using (Eddy.History.BeginScope("Paint Triles"))
            {
                foreach (var op in _paintOps)
                {
                    switch (op)
                    {
                        case PaintOp.Added(var emplacement, var id):
                            {
                                Level.Triles[emplacement] = new TrileInstance
                                {
                                    TrileId = id,
                                    PhiLight = 0,
                                    Position = new Vector3(emplacement.X, emplacement.Y, emplacement.Z).ToRepacker()
                                };
                                break;
                            }

                        case PaintOp.Changed(var emplacement, var newId):
                            {
                                if (Level.Triles.TryGetValue(emplacement, out var instance))
                                {
                                    instance.TrileId = newId;
                                }

                                break;
                            }

                        case PaintOp.Erased(var emplacement):
                            {
                                Level.Triles.Remove(emplacement);
                                break;
                            }
                    }
                }
            }

            _paintOps.Clear();
        }

        return;

        void AppendNewTrile(TrileEmplacement emplacement)
        {
            var faceNormal = _hoveredCursor.Face!.Value.AsVector();
            var newEmp = new TrileEmplacement(
                emplacement.X + (int)faceNormal.X,
                emplacement.Y + (int)faceNormal.Y,
                emplacement.Z + (int)faceNormal.Z
            );

            var position = new Vector3(newEmp.X, newEmp.Y, newEmp.Z);
            var mesh = EnsureTrileActor(trileId).GetComponent<TrilesMesh>();
            mesh.SetInstanceData(newEmp, position, 0);

            _paintOps.Add(new PaintOp.Added(newEmp, trileId));
            if (_selectedCursor.Emplacements.Remove(emplacement))
            {
                _selectedCursor.Emplacements.Add(newEmp);
            }
        }

        void EraseTrile(TrileEmplacement emplacement)
        {
            var removedId = Level.Triles[emplacement].TrileId;
            if (_trileActors.TryGetValue(removedId, out var actor))
            {
                var mesh = actor.GetComponent<TrilesMesh>();
                mesh.RemoveInstance(emplacement);
                CleanupEmptyActor(removedId, mesh);
            }

            _paintOps.Add(new PaintOp.Erased(emplacement));
            _selectedCursor.Emplacements.Remove(emplacement);
        }

        void ChangeTrile(TrileEmplacement emplacement)
        {
            var instance = Level.Triles[emplacement];
            var oldTrileId = instance.TrileId;
            if (oldTrileId == trileId || trileId == InvalidId)
            {
                return;
            }

            if (_trileActors.TryGetValue(oldTrileId, out var oldActor))
            {
                var oldMesh = oldActor.GetComponent<TrilesMesh>();
                oldMesh.RemoveInstance(emplacement);
                CleanupEmptyActor(oldTrileId, oldMesh);
            }

            var mesh = EnsureTrileActor(trileId).GetComponent<TrilesMesh>();
            mesh.SetInstanceData(emplacement, instance.Position.ToXna(), instance.PhiLight);

            _paintOps.Add(new PaintOp.Changed(emplacement, trileId));
        }
    }

    private void UpdatePick()
    {
        StatusService.AddHints(
            ("LMB", "Pick Trile")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredCursor.Emplacement != null)
        {
            var pickedName = GetHoveredName();
            if (!string.IsNullOrEmpty(pickedName))
            {
                Eddy.AssetBrowser.Pick(pickedName, AssetType.Trile);
                Eddy.Tool = EddyTool.Paint;
            }
        }
    }

    public override void Revisualize(bool partial = false)
    {
        if (Level.Triles.Count != 0)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var emp1 in Level.Triles.Keys)
            {
                min = Vector3.Min(min, new Vector3(emp1.X, emp1.Y, emp1.Z));
                max = Vector3.Max(max, new Vector3(emp1.X, emp1.Y, emp1.Z));
            }

            ComputeLevelBounds(min, max);
        }
        else
        {
            _levelBounds = new BoundingBox(Vector3.Zero, Vector3.Zero);
        }

        if (partial)
        {
            if (Eddy.SelectedContext != EddyContext.Trile)
            {
                return;
            }

            _emplacementGroups.Clear();
            _groupEmplacements.Clear();
            foreach (var (id, group) in Level.Groups.Where(kv => kv.Key != InvalidId))
            {
                foreach (var instance in group.Triles)
                {
                    var emp = new TrileEmplacement(instance.Position);
                    _emplacementGroups[emp] = id;

                    if (!_groupEmplacements.TryGetValue(id, out var set))
                    {
                        set = new HashSet<TrileEmplacement>();
                        _groupEmplacements[id] = set;
                    }

                    set.Add(emp);
                }
            }

            if (_selectedCursor.GroupId != null && !Level.Groups.ContainsKey(_selectedCursor.GroupId.Value))
            {
                _selectedCursor.GroupId = null;
            }

            var presentIds = Level.Triles.Values
                .Where(ti => ti.TrileId != InvalidId)
                .Select(ti => ti.TrileId)
                .ToHashSet();

            foreach (var id in _trileActors.Keys.ToList())
            {
                if (id != InvalidId && !presentIds.Contains(id))
                {
                    Eddy.Scene.DestroyActor(_trileActors[id]);
                    _trileActors.Remove(id);
                }
            }

            foreach (var id in presentIds)
            {
                EnsureTrileActor(id);
            }

            foreach (var actor in _trileActors.Values)
            {
                actor.GetComponent<TrilesMesh>().ClearInstances();
            }

            _selectedCursor.Emplacements.RemoveWhere(e => !Level.Triles.ContainsKey(e));
            if (_hoveredCursor.Emplacement != null && !Level.Triles.ContainsKey(_hoveredCursor.Emplacement))
            {
                _hoveredCursor.Reset();
            }
        }
        else
        {
            TeardownVisualization(force: false);

            #region Trile Set

            var path = $"Trile Sets/{Level.TrileSetName}";
            _set = (TrileSet)ResourceService.Load(path);
            Eddy.AssetBrowser.SetTrileSet(path, _set);

            #endregion

            #region Triles

            var trileIds = Level.Triles.Values
                .Where(ti => ti.TrileId != InvalidId)
                .Select(ti => ti.TrileId)
                .Distinct();

            foreach (var id in trileIds)
            {
                var actor = CreateSubActor();
                actor.Name = _set.Triles.TryGetValue(id, out var trile) ? $"{id}: {trile.Name}" : $"{id}";
                _trileActors[id] = actor;

                var mesh = actor.AddComponent<TrilesMesh>();
                mesh.Visualize(_set, id);
                if (!mesh.HasGeometry)
                {
                    var emptyTriles = Eddy.Visuals.Value.HasFlag(EddyVisuals.EmptyTriles);
                    actor.Visible = emptyTriles;
                    mesh.Pickable = emptyTriles;
                }
            }

            #endregion

            #region Trile Groups

            _emplacementGroups.Clear();
            _groupEmplacements.Clear();
            foreach (var (id, group) in Level.Groups.Where(kv => kv.Key != InvalidId))
            {
                foreach (var instance in group.Triles)
                {
                    // Grouped emplacements are stored in TrileInstance objects.
                    // Check FEZRepacker.Core.Definitions.Json.TrileGroupJsonModel

                    var emp = new TrileEmplacement(instance.Position);
                    _emplacementGroups[emp] = id;

                    if (!_groupEmplacements.TryGetValue(id, out var set))
                    {
                        set = new HashSet<TrileEmplacement>();
                        _groupEmplacements[id] = set;
                    }

                    set.Add(emp);
                }
            }

            _selectedCursor.GroupId = null;

            #endregion

            _selectedCursor.Reset();
            _hoveredCursor.Reset();
        }

        foreach (var (emplacement, instance) in Level.Triles.Where(kv => kv.Value.TrileId != InvalidId))
        {
            if (_trileActors.TryGetValue(instance.TrileId, out var actor))
            {
                var mesh = actor.GetComponent<TrilesMesh>();
                mesh.SetInstanceData(emplacement, instance.Position.ToXna(), instance.PhiLight);
            }
        }

        EnsurePlaceholder();
        UpdateCollisionMesh();
    }

    public override void DrawProperties()
    {
        if (Eddy.SelectedContext != EddyContext.Trile || _selectedCursor.Emplacements.Count == 0)
        {
            return;
        }

        if (_selectedCursor.GroupId != null && Level.Groups.TryGetValue(_selectedCursor.GroupId.Value, out var group))
        {
            DrawGroupProperties(_selectedCursor.GroupId.Value, group);
            return;
        }

        if (_selectedCursor.Emplacements.Count > 1)
        {
            DrawMultiProperties();
            return;
        }

        var emplacement = _selectedCursor.Emplacements.First();
        if (!Level.Triles.TryGetValue(emplacement, out var instance) || instance.TrileId == InvalidId)
        {
            return;
        }

        var trile = _set!.Triles[instance.TrileId];
        ImGui.Text($"Trile: {trile.Name} (ID={instance.TrileId})");

        var empArray = new[] { emplacement.X, emplacement.Y, emplacement.Z };
        ImGui.BeginDisabled();
        ImGui.InputInt3("Emplacement", ref empArray[0]);
        ImGui.EndDisabled();

        var position = instance.Position.ToXna();
        if (ImGuiX.InputFloat3("Position", ref position))
        {
            using (Eddy.History.BeginScope("Edit Trile Position"))
            {
                instance.Position = position.ToRepacker();
            }
        }

        var phi = (int)instance.PhiLight;
        var phiNames = new[] { "Front", "Right", "Back", "Left" };
        if (ImGui.Combo("Rotation", ref phi, phiNames, phiNames.Length))
        {
            using (Eddy.History.BeginScope("Edit Trile Rotation"))
            {
                instance.PhiLight = (byte)phi;
            }
        }

        ImGui.SeparatorText("Actor Settings");

        if (instance.ActorSettings == null)
        {
            if (ImGui.Button($"{Icons.Add} Add"))
            {
                using (Eddy.History.BeginScope("Add ActorSettings"))
                {
                    instance.ActorSettings = new TrileInstanceActorSettings();
                }
            }
        }

        if (instance.ActorSettings != null)
        {
            if (ImGui.Button($"{Icons.Trash} Remove"))
            {
                using (Eddy.History.BeginScope("Remove ActorSettings"))
                {
                    instance.ActorSettings = null;
                }
            }
        }

        if (instance.ActorSettings != null)
        {
            var containedTrile = instance.ActorSettings.ContainedTrile ?? InvalidId;
            if (ImGui.InputInt("Contained Trile", ref containedTrile))
            {
                using (Eddy.History.BeginScope("Edit Contained Trile"))
                {
                    instance.ActorSettings.ContainedTrile = containedTrile;
                }
            }

            var signText = instance.ActorSettings.SignText;
            if (ImGui.InputText("Sign Text", ref signText, 1024))
            {
                using (Eddy.History.BeginScope("Edit Sign Text"))
                {
                    instance.ActorSettings.SignText = signText;
                }
            }

            var sequence = instance.ActorSettings.Sequence;
            if (ImGuiX.EditableArray("Sequence", ref sequence, RenderItem))
            {
                using (Eddy.History.BeginScope("Edit Sequence"))
                {
                    instance.ActorSettings.Sequence = sequence;
                }
            }

            var seqSample = instance.ActorSettings.SequenceSampleName;
            if (ImGui.InputText("Sequence Sample", ref seqSample, 255))
            {
                using (Eddy.History.BeginScope("Edit Sequence Sample"))
                {
                    instance.ActorSettings.SequenceSampleName = seqSample;
                }
            }

            var altSeqSample = instance.ActorSettings.SequenceAlternateSampleName;
            if (ImGui.InputText("Sequence Alternate Sample", ref altSeqSample, 255))
            {
                using (Eddy.History.BeginScope("Edit Sequence Alternate Sample"))
                {
                    instance.ActorSettings.SequenceAlternateSampleName = altSeqSample;
                }
            }

            var hostVolume = instance.ActorSettings.HostVolume ?? InvalidId;
            if (ImGui.InputInt("Host Volume", ref hostVolume))
            {
                using (Eddy.History.BeginScope("Edit Host Volume"))
                {
                    instance.ActorSettings.HostVolume = hostVolume;
                }
            }
        }
    }

    private void DrawMultiProperties()
    {
        var instances = _selectedCursor.Emplacements
            .Where(e => Level.Triles.TryGetValue(e, out var ti) && ti.TrileId != InvalidId)
            .Select(e => Level.Triles[e])
            .ToList();

        if (instances.Count == 0)
        {
            return;
        }

        ImGui.TextDisabled($"{instances.Count} triles selected");

        var positionDrag = _previousPositionDrag;
        if (ImGuiX.DragFloat3("Position", ref positionDrag, 0.1f))
        {
            var delta = positionDrag - _previousPositionDrag;
            _previousPositionDrag = positionDrag;
            using (Eddy.History.BeginScope("Edit Triles Position"))
            {
                foreach (var inst in instances)
                {
                    inst.Position = (inst.Position.ToXna() + delta).ToRepacker();
                }
            }
        }

        if (!ImGui.IsItemActive())
        {
            _previousPositionDrag = Vector3.Zero;
        }

        var allSameFace = instances.All(i => i.PhiLight == instances[0].PhiLight);
        var face = allSameFace ? instances[0].PhiLight : -1;
        if (ImGui.Combo("Rotation", ref face, Rotations, Rotations.Length))
        {
            using (Eddy.History.BeginScope("Edit Trile Rotation"))
            {
                foreach (var inst in instances)
                {
                    inst.PhiLight = (byte)face;
                }
            }
        }

        ImGui.SeparatorText("Actor Settings");
        var anyWithout = instances.Any(i => i.ActorSettings == null);
        var anyWith = instances.Any(i => i.ActorSettings != null);

        if (anyWithout)
        {
            if (ImGui.Button($"{Icons.Add} Add to All"))
            {
                using (Eddy.History.BeginScope("Add ActorSettings"))
                {
                    foreach (var inst in instances.Where(i => i.ActorSettings == null))
                    {
                        inst.ActorSettings = new TrileInstanceActorSettings();
                    }
                }
            }
        }

        if (anyWith)
        {
            if (anyWithout)
            {
                ImGui.SameLine();
            }

            if (ImGui.Button($"{Icons.Trash} Remove from All"))
            {
                using (Eddy.History.BeginScope("Remove ActorSettings"))
                {
                    foreach (var inst in instances.Where(i => i.ActorSettings != null))
                    {
                        inst.ActorSettings = null;
                    }
                }
            }
        }
    }

    private static bool RenderItem(int index, ref bool item)
    {
        return ImGui.Checkbox($"{index + 1}", ref item);
    }

    private void DrawGroupProperties(int id, TrileGroup group)
    {
        ImGui.TextDisabled($"Trile Group: {group.Triles.Count} trile(s) (ID={id})");

        var actor = (int)group.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actor, actors, actors.Length))
        {
            using (Eddy.History.BeginScope("Edit Group ActorType"))
            {
                group.ActorType = (ActorType)actor;
            }
        }

        var heavy = group.Heavy;
        if (ImGui.Checkbox("Heavy", ref heavy))
        {
            using (Eddy.History.BeginScope("Edit Group Heavy"))
            {
                group.Heavy = heavy;
            }
        }

        var sound = group.AssociatedSound;
        if (ImGui.InputText("Sound", ref sound, 255))
        {
            using (Eddy.History.BeginScope("Edit Group Sound"))
            {
                group.AssociatedSound = sound;
            }
        }

        ImGui.SeparatorText("Geyser");
        {
            var geyserOffset = group.GeyserOffset;
            if (ImGui.DragFloat("Offset", ref geyserOffset, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Offset"))
                {
                    group.GeyserOffset = geyserOffset;
                }
            }

            var geyserPause = group.GeyserPauseFor;
            if (ImGui.DragFloat("Pause For", ref geyserPause, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Pause"))
                {
                    group.GeyserPauseFor = geyserPause;
                }
            }

            var geyserLift = group.GeyserLiftFor;
            if (ImGui.DragFloat("Lift For", ref geyserLift, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Lift"))
                {
                    group.GeyserLiftFor = geyserLift;
                }
            }

            var geyserApex = group.GeyserApexHeight;
            if (ImGui.DragFloat("Apex Height", ref geyserApex, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Apex"))
                {
                    group.GeyserApexHeight = geyserApex;
                }
            }
        }

        ImGui.SeparatorText("Spin");
        {
            var spinCenter = group.SpinCenter.ToXna();
            if (ImGuiX.DragFloat3("Center", ref spinCenter, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Spin Center"))
                {
                    group.SpinCenter = spinCenter.ToRepacker();
                }
            }

            var spinClockwise = group.SpinClockwise;
            if (ImGui.Checkbox("Clockwise", ref spinClockwise))
            {
                using (Eddy.History.BeginScope("Edit Spin Clockwise"))
                {
                    group.SpinClockwise = spinClockwise;
                }
            }

            var spinFreq = group.SpinFrequency;
            if (ImGui.DragFloat("Frequency", ref spinFreq, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Spin Frequency"))
                {
                    group.SpinFrequency = spinFreq;
                }
            }

            var spinNeedsTrigger = group.SpinNeedsTriggering;
            if (ImGui.Checkbox("Needs Triggering", ref spinNeedsTrigger))
            {
                using (Eddy.History.BeginScope("Edit Spin NeedsTriggering"))
                {
                    group.SpinNeedsTriggering = spinNeedsTrigger;
                }
            }

            var spin180 = group.Spin180Degrees;
            if (ImGui.Checkbox("180 Degrees", ref spin180))
            {
                using (Eddy.History.BeginScope("Edit Spin 180"))
                {
                    group.Spin180Degrees = spin180;
                }
            }

            var fallOnRotate = group.FallOnRotate;
            if (ImGui.Checkbox("Fall On Rotate", ref fallOnRotate))
            {
                using (Eddy.History.BeginScope("Edit Spin FallOnRotate"))
                {
                    group.FallOnRotate = fallOnRotate;
                }
            }

            var spinOffset = group.SpinOffset;
            if (ImGui.DragFloat("Offset", ref spinOffset, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Spin Offset"))
                {
                    group.SpinOffset = spinOffset;
                }
            }
        }

        if (group.Path != null)
        {
            ImGui.SeparatorText("Path");
            ImGui.TextDisabled($"{group.Path.Segments.Count} segment(s)");
            if (ImGui.Button("Edit Path##GroupPath"))
            {
                Eddy.Pending = new PathContext.Pending(id, IsGroup: true);
                Eddy.SelectedContext = EddyContext.Path;
                Eddy.Tool = EddyTool.Select;
            }
        }
    }

    private IEnumerable<(MeshSurface, PrimitiveType)> BuildBoxSurfaces(
        IEnumerable<TrileEmplacement> emplacements,
        Color color)
    {
        return emplacements
            .Where(e => Level.Triles.TryGetValue(e, out _))
            .Select(e =>
            {
                var center = Level.Triles[e].Position.ToXna() + new Vector3(0.5f);
                var surface = MeshSurface.CreateColoredBox(Vector3.One, color);
                for (var i = 0; i < surface.Vertices.Length; i++)
                {
                    surface.Vertices[i] += center;
                }

                return (surface, PrimitiveType.TriangleList);
            });
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Trile;
    }

    public override void Dispose()
    {
        _translateScope?.Dispose();
        _scaleScope?.Dispose();
        _paintOps.Clear();
        TeardownVisualization(force: true);
        base.Dispose();
    }

    private void TeardownVisualization(bool force)
    {
        if (force && _collisionMapActor != null)
        {
            Eddy.Scene.DestroyActor(_collisionMapActor);
            _collisionMapActor = null;
        }

        foreach (var actor in _trileActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _trileActors.Clear();
    }

    private string GetHoveredName()
    {
        if (_hoveredCursor.Emplacement == null ||
            !Level.Triles.TryGetValue(_hoveredCursor.Emplacement, out var instance))
        {
            return string.Empty;
        }

        if (_set!.Triles.TryGetValue(instance.TrileId, out var trile))
        {
            return trile.Name;
        }

        return string.Empty;
    }

    private void BuildClipboard()
    {
        _select.Clipboard.Clear();
        var entries = _selectedCursor.Emplacements
            .Select(e => (Emplacement: e, Instance: Level.Triles[e]))
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var anchor = _select.SelectionAnchor ?? entries[0].Emplacement;
        var range = entries
            .Select(e => new ClipboardEntry(
                new TrileEmplacement(e.Emplacement.X - anchor.X, e.Emplacement.Y - anchor.Y,
                    e.Emplacement.Z - anchor.Z),
                e.Instance.TrileId,
                e.Instance.PhiLight));

        _select.Clipboard.AddRange(range);
    }

    private void PasteClipboard()
    {
        if (_select.Clipboard.Count == 0 || _hoveredCursor.Emplacement == null)
        {
            return;
        }

        var origin = _hoveredCursor.Emplacement;
        _selectedCursor.Emplacements.Clear();
        _selectedCursor.Face = null;

        if (_hoveredCursor.Face.HasValue)
        {
            var step = _hoveredCursor.Face.Value.AsVector();
            origin = new TrileEmplacement(
                origin.X + (int)step.X,
                origin.Y + (int)step.Y,
                origin.Z + (int)step.Z);
        }

        foreach (var entry in _select.Clipboard)
        {
            var emp = new TrileEmplacement(
                origin.X + entry.Offset.X,
                origin.Y + entry.Offset.Y,
                origin.Z + entry.Offset.Z);

            var instance = new TrileInstance
            {
                TrileId = entry.TrileId,
                PhiLight = entry.PhiLight,
                Position = new Vector3(emp.X, emp.Y, emp.Z).ToRepacker()
            };

            Level.Triles[emp] = instance;
            EnsureTrileActor(entry.TrileId).GetComponent<TrilesMesh>()
                .SetInstanceData(emp, instance.Position.ToXna(), instance.PhiLight);

            _selectedCursor.Emplacements.Add(emp);
        }

        UpdateCollisionMesh();
        EnsurePlaceholder();
    }

    private void RemoveSelected()
    {
        if (_selectedCursor.Emplacements.Count == 0)
        {
            return;
        }

        foreach (var emplacement in _selectedCursor.Emplacements.ToList())
        {
            if (!Level.Triles.Remove(emplacement, out var instance))
            {
                continue;
            }

            _selectedCursor.Emplacements.Remove(emplacement);
            if (_trileActors.TryGetValue(instance.TrileId, out var actor))
            {
                var mesh = actor.GetComponent<TrilesMesh>();
                mesh.RemoveInstance(emplacement);
                CleanupEmptyActor(instance.TrileId, mesh);
            }
        }

        UpdateCollisionMesh();
        EnsurePlaceholder();
    }

    private void AddToGroup(int groupId, TrileEmplacement emp, TrileInstance instance)
    {
        if (!Level.Groups.TryGetValue(groupId, out var group))
        {
            return;
        }

        group.Triles.Add(instance);
        _emplacementGroups[emp] = groupId;
        if (_groupEmplacements.TryGetValue(groupId, out var set))
        {
            set.Add(emp);
        }
    }

    private void RemoveFromGroup(TrileEmplacement emp, TrileInstance instance)
    {
        if (!_emplacementGroups.TryGetValue(emp, out var groupId))
        {
            return;
        }

        if (Level.Groups.TryGetValue(groupId, out var group))
        {
            group.Triles.Remove(instance);
        }

        _emplacementGroups.Remove(emp);
        if (_groupEmplacements.TryGetValue(groupId, out var set))
        {
            set.Remove(emp);
        }
    }

    private void CleanupEmptyActor(int trileId, TrilesMesh mesh)
    {
        if (mesh.InstanceCount == 0)
        {
            Eddy.Scene.DestroyActor(_trileActors[trileId]);
            _trileActors.Remove(trileId);
        }
    }

    private Actor EnsureTrileActor(int trileId)
    {
        if (_trileActors.TryGetValue(trileId, out var existing))
        {
            return existing;
        }

        var actor = CreateSubActor();
        actor.Name = _set!.Triles.TryGetValue(trileId, out var trile)
            ? $"{trileId}: {trile.Name}"
            : $"{trileId}: (missing)";
        _trileActors[trileId] = actor;

        var mesh = actor.AddComponent<TrilesMesh>();
        mesh.Visualize(_set, trileId);

        return actor;
    }

    private void ComputeLevelBounds(Vector3 min, Vector3 max)
    {
        _levelBounds = new BoundingBox(min, max);
        var size = (max - min + Vector3.One).ToRepacker();

        var expanded = new RVector3(
            Math.Max(Level.Size.X, size.X),
            Math.Max(Level.Size.Y, size.Y),
            Math.Max(Level.Size.Z, size.Z));

        if (Level.Size != expanded)
        {
            Level.Size = expanded;
        }
    }

    private void UpdateCollisionMesh()
    {
        if (_collisionMapActor == null)
        {
            _collisionMapActor = CreateSubActor();
            _collisionMapActor.Name = $"Collision Map: {Level.TrileSetName}";
            _collisionMapActor.AddComponent<TrileCollisionMesh>();
        }

        var collisionMap = _collisionMapActor.GetComponent<TrileCollisionMesh>();
        collisionMap.ClearInstanceData();

        foreach (var instance in Level.Triles.Values.Where(ti => ti.TrileId != InvalidId))
        {
            if (_set!.Triles.TryGetValue(instance.TrileId, out var trile))
            {
                collisionMap.AddInstanceData(instance.Position.ToXna(), trile.Faces, trile.Size.ToXna());
            }
        }
    }

    private void EnsurePlaceholder()
    {
        if (Level.Triles.Count > 0)
        {
            return;
        }

        var sizeX = (int)Level.Size.X;
        if (sizeX <= 0)
        {
            sizeX = 1;
        }

        var sizeZ = (int)Level.Size.Z;
        if (sizeZ <= 0)
        {
            sizeZ = 1;
        }

        var trileId = _set!.Triles
            .FirstOrDefault(kv => kv.Value.Geometry.Vertices.Length > 0)
            .Key;

        for (var x = 0; x < sizeX; x++)
        {
            for (var z = 0; z < sizeZ; z++)
            {
                var emplacement = new TrileEmplacement(x, 0, z);
                var instance = new TrileInstance
                {
                    Position = new Vector3(x, 0, z).ToRepacker(),
                    TrileId = trileId,
                    PhiLight = 0
                };

                Level.Triles[emplacement] = instance;
            }
        }

        TrilesMesh mesh;
        if (!_trileActors.TryGetValue(trileId, out var actor))
        {
            actor = CreateSubActor();
            actor.Name = "Placeholder";
            _trileActors[trileId] = actor;

            mesh = actor.AddComponent<TrilesMesh>();
            mesh.Visualize(_set!, trileId);
        }

        mesh = actor.GetComponent<TrilesMesh>();
        foreach (var (emplacement, instance) in Level.Triles.Where(kv => kv.Value.TrileId == InvalidId))
        {
            mesh.SetInstanceData(emplacement, instance.Position.ToXna(), instance.PhiLight);
        }
    }

    private record struct ClipboardEntry(TrileEmplacement Offset, int TrileId, byte PhiLight);

    private struct CursorState()
    {
        public readonly HashSet<TrileEmplacement> Emplacements = new();
        public FaceOrientation? Face = null;
        public int? GroupId = null;

        public readonly TrileEmplacement? Emplacement =>
            Emplacements.Count == 1 ? Emplacements.First() : null;

        public void Reset()
        {
            Emplacements.Clear();
            Face = null;
            GroupId = null;
        }
    }

    private struct SelectState()
    {
        public TrileEmplacement? RectOrigin;
        public TrileEmplacement? SelectionAnchor;
        public bool WasDrag;
        public bool PressedOnViewport;
        public readonly List<ClipboardEntry> Clipboard = new();
    }

    private abstract record PaintOp(TrileEmplacement Emp)
    {
        public record Added(TrileEmplacement Emp, int Id) : PaintOp(Emp);

        public record Erased(TrileEmplacement Emp) : PaintOp(Emp);

        public record Changed(TrileEmplacement Emp, int NewId) : PaintOp(Emp);
    }

    private record struct TrileEntry(TrileEmplacement Emp, int TrileId, byte PhiLight);

    private record struct ScaleOp(int Step, bool Add);

    private struct ScaleState()
    {
        public readonly List<TrileEntry> Snapshot = new();
        public readonly Queue<ScaleOp> Ops = new();
        public int PreviousSteps;
        public int Dx, Dy, Dz;
    }
}