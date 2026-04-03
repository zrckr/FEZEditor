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

internal sealed class TrileContext : EddyContext
{
    public override bool IsSelected => _selected.Emplacements.Count > 0;

    private readonly Dictionary<int, Actor> _trileActors = new();

    private Actor? _collisionMapActor;

    private TrileSet? _set;

    private CursorState _hovered = new();

    private CursorState _selected = new();

    private SelectState _select;

    private TranslateState _translate;

    private ScaleState _scale;

    private IDisposable? _paintScope;

    private Vector2 _viewport;

    private readonly Dictionary<TrileEmplacement, int> _emplacementGroups = new();

    private readonly Dictionary<int, HashSet<TrileEmplacement>> _groupEmplacements = new();

    public void ShowCollisionMap(bool visible)
    {
        if (_collisionMapActor != null)
        {
            var mesh = _collisionMapActor.GetComponent<TrileCollisionMesh>();
            mesh.Visible = visible;
        }

        foreach (var actor in _trileActors.Values)
        {
            var trilesMesh = actor.GetComponent<TrilesMesh>();
            if (trilesMesh is { HasGeometry: false })
            {
                trilesMesh.Pickable = visible;
            }
        }
    }

    public override void TestConditions(Ray ray, RaycastHit? hit, Vector2 viewport)
    {
        _viewport = viewport;
        if (hit.HasValue && hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
        {
            var index = hit.Value.Index;
            var emplacement = mesh.GetEmplacement(index);
            if (Level.Triles.ContainsKey(emplacement))
            {
                var box = mesh.GetBounds().ElementAt(index);
                var distance = hit.Value.Distance;
                _hovered.Emplacements.Clear();
                _hovered.Emplacements.Add(emplacement);
                _hovered.Face = Mathz.DetermineFace(box, ray, distance);
                _hovered.GroupId = _emplacementGroups.TryGetValue(emplacement, out var gid) ? gid : null;
                Contexts.TransitionTo<TrileContext>();
            }
        }
    }

    public override void Update()
    {
        StatusService.ClearHints();

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _translate.Reset();
            _scale.Reset();
            _paintScope?.Dispose();
            _paintScope = null;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selected.Reset();
            Tool = EddyTool.Select;
        }

        if (Tool.Value is not (EddyTool.Select or EddyTool.Pick))
        {
            _hovered.Reset();
        }

        var nextTool = Tool.Value;
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            nextTool = Tool.Value switch
            {
                EddyTool.Select => UpdateSelect(),
                EddyTool.Translate => UpdateTranslate(Tool.IsDirty),
                EddyTool.Rotate => UpdateRotate(),
                EddyTool.Paint => UpdatePaint(),
                EddyTool.Pick => UpdatePick(),
                EddyTool.Scale => UpdateScale(Tool.IsDirty),
                _ => throw new ArgumentOutOfRangeException()
            } ?? Tool.Value;
        }

        Tool = Tool.Clean();
        if (nextTool != Tool.Value)
        {
            Tool = nextTool;
        }

        UpdateCursor();
    }

    private EddyTool? UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
            ("LMB Drag", "Select Multiple")
        );

        if (_selected.Emplacements.Count > 0)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );

            if (_selected.GroupId != null)
            {
                StatusService.AddHints(("Ctrl+Shift+G", "Ungroup"));
            }
            else if (_selected.Emplacements.Count > 1)
            {
                StatusService.AddHints(("Ctrl+Shift+G", "Group"));
            }
        }

        if (_select.Clipboard != null && _hovered.Emplacement != null)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selected.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (History.BeginScope("Delete Triles"))
            {
                RemoveSelected();
            }
            _selected.Reset();
        }

        if (ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.G) && _selected.GroupId != null)
        {
            using (History.BeginScope("Remove Trile Group"))
            {
                Level.Groups.Remove(_selected.GroupId.Value);
            }
            _selected.GroupId = null;
        }

        if (ImGui.GetIO().KeyCtrl)
        {
            if (_selected.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                _select.Clipboard = BuildClipboard();
            }

            if (_selected.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                _select.Clipboard = BuildClipboard();
                using (History.BeginScope("Cut Triles"))
                {
                    RemoveSelected();
                }
                _selected.Emplacements.Clear();
                _selected.Face = null;
            }

            if (_select.Clipboard != null && _hovered.Emplacement != null && ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (History.BeginScope("Paste Triles"))
                {
                    PasteClipboard(_select.Clipboard, _hovered.Emplacement);
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _select.RectOrigin = _hovered.Emplacement;
            _select.WasDrag = false;
        }

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _select.RectOrigin != null && _hovered.Emplacement != null)
        {
            var min = _select.RectOrigin.Min(_hovered.Emplacement);
            var max = _select.RectOrigin.Max(_hovered.Emplacement);

            _selected.Emplacements.Clear();
            _selected.GroupId = null;
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
                        if (_emplacementGroups.TryGetValue(emplacement, out var gid) && _groupEmplacements.TryGetValue(gid, out var gset))
                        {
                            _selected.Emplacements.UnionWith(gset);
                        }
                        else
                        {
                            _selected.Emplacements.Add(emplacement);
                        }
                    }
                }
            }

            _selected.Face = _hovered.Face;
            _select.WasDrag = true;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            var wasDrag = _select.WasDrag;
            _select.SelectionAnchor = _select.RectOrigin;
            _select.RectOrigin = null;
            _select.WasDrag = false;

            if (!wasDrag)
            {
                _selected.Emplacements.Clear();
                _selected.GroupId = null;

                if (_hovered.Emplacement != null)
                {
                    // If hovering a grouped trile, select the whole group
                    if (_hovered.GroupId != null && _groupEmplacements.TryGetValue(_hovered.GroupId.Value, out var groupSet))
                    {
                        _selected.Emplacements.UnionWith(groupSet);
                        _selected.GroupId = _hovered.GroupId;
                    }
                    else
                    {
                        _selected.Emplacements.Add(_hovered.Emplacement);
                    }
                }
            }

            _selected.Face = _hovered.Face;
        }

        return null;
    }

    private EddyTool? UpdateTranslate(bool entered)
    {
        StatusService.AddHints(
            ("LMB Drag", "Move X or Z"),
            ("Alt+LMB Drag", "Move Y"),
            ("Shift", "Trixel Snap"),
            ("R", "Reset")
        );

        if (entered)
        {
            _translate = new TranslateState();
            InitTranslateDragState();
        }

        if (!entered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _translate = new TranslateState();
            InitTranslateDragState();
        }

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _translate.Active)
        {
            var ray = Scene.Viewport.Unproject(ImGuiX.GetMousePos(), _viewport);
            var t = ray.Intersects(_translate.DragPlane);
            if (t == null)
            {
                return null;
            }

            var rawWorldPoint = ray.Position + ray.Direction * t.Value;
            var rawDelta = rawWorldPoint - _translate.InitialHandlePosition;

            Vector3 snappedDelta;
            if (ImGui.GetIO().KeyAlt)
            {
                snappedDelta = new Vector3(0f, rawDelta.Y, 0f);
            }
            else
            {
                if (!_translate.AxisLocked)
                {
                    _translate.LockAccum += InputService.GetMouseDelta();
                    const float lockThreshold = 4f;
                    if (_translate.LockAccum.Length() < lockThreshold)
                    {
                        return EddyTool.Translate;
                    }

                    var screenX = ScreenDir(Vector3.UnitX);
                    var screenZ = ScreenDir(Vector3.UnitZ);
                    var drag = Vector2.Normalize(_translate.LockAccum);
                    _translate.LockedAxis =
                        MathF.Abs(Vector2.Dot(drag, screenX)) >= MathF.Abs(Vector2.Dot(drag, screenZ))
                            ? Vector3.UnitX
                            : Vector3.UnitZ;
                    _translate.AxisLocked = true;
                }

                var axis = _translate.LockedAxis!.Value;
                snappedDelta = axis * Vector3.Dot(rawDelta, axis);
            }

            var snapSize = ImGui.GetIO().KeyShift ? Mathz.TrixelSize : 1f;
            snappedDelta.X = MathF.Round(snappedDelta.X / snapSize) * snapSize;
            snappedDelta.Y = MathF.Round(snappedDelta.Y / snapSize) * snapSize;
            snappedDelta.Z = MathF.Round(snappedDelta.Z / snapSize) * snapSize;

            foreach (var emp in _selected.Emplacements)
            {
                if (!Level.Triles.TryGetValue(emp, out var instance))
                {
                    continue;
                }

                if (!_translate.InitialPositions.TryGetValue(emp, out var initialPos))
                {
                    continue;
                }

                var newPos = initialPos + snappedDelta;
                instance.Position = newPos.ToRepacker();

                if (_trileActors.TryGetValue(instance.TrileId, out var actor))
                {
                    actor.GetComponent<TrilesMesh>().SetInstanceData(emp, newPos, instance.PhiLight);
                }
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _translate.Reset();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selected.Emplacements.Count > 0 && !_translate.Active)
        {
            using (History.BeginScope("Reset Translate"))
            {
                foreach (var emp in _selected.Emplacements)
                {
                    if (!Level.Triles.TryGetValue(emp, out var instance))
                    {
                        continue;
                    }

                    var pos = new Vector3(emp.X, emp.Y, emp.Z);
                    instance.Position = pos.ToRepacker();

                    if (_trileActors.TryGetValue(instance.TrileId, out var actor))
                    {
                        actor.GetComponent<TrilesMesh>().SetInstanceData(emp, pos, instance.PhiLight);
                    }
                }
            }
        }

        return null;
    }

    private Vector2 ScreenDir(Vector3 worldDir)
    {
        var origin = Camera.Project(Vector3.Zero, Vector2.Zero);
        var projected = Camera.Project(worldDir, Vector2.Zero);
        var screen = new Vector2(projected.X - origin.X, projected.Y - origin.Y);
        return screen.LengthSquared() > float.Epsilon ? Vector2.Normalize(screen) : screen;
    }

    private void InitTranslateDragState()
    {
        if (_selected.Emplacements.Count == 0)
        {
            return;
        }

        var avgPos = Vector3.Zero;
        var count = 0;

        foreach (var emp in _selected.Emplacements)
        {
            if (!Level.Triles.TryGetValue(emp, out var inst))
            {
                continue;
            }

            avgPos += inst.Position.ToXna();
            count++;
        }

        if (count == 0)
        {
            return;
        }

        avgPos /= count;
        var planeNormal = Vector3.Normalize(Camera.InverseView.Backward);
        _translate.DragPlane = new Plane(planeNormal, -Vector3.Dot(planeNormal, avgPos));
        _translate.InitialHandlePosition = avgPos;

        _translate.InitialPositions = [];
        foreach (var emplacement in _selected.Emplacements)
        {
            if (Level.Triles.TryGetValue(emplacement, out var inst))
            {
                _translate.InitialPositions[emplacement] = inst.Position.ToXna();
            }
        }

        _translate.Active = true;
        _translate.Scope = History.BeginScope("Translate Triles");
    }

    private EddyTool? UpdateRotate()
    {
        StatusService.AddHints(
            ("LMB", "Rotate 90°")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _selected.Emplacements.Count > 0)
        {
            using (History.BeginScope("Rotate Triles"))
            {
                foreach (var emp in _selected.Emplacements)
                {
                    if (!Level.Triles.TryGetValue(emp, out var instance))
                    {
                        continue;
                    }

                    instance.PhiLight = (byte)((instance.PhiLight + 1) % 4);
                    if (_trileActors.TryGetValue(instance.TrileId, out var actor))
                    {
                        actor.GetComponent<TrilesMesh>()
                            .SetInstanceData(emp, instance.Position.ToXna(), instance.PhiLight);
                    }
                }
            }
        }

        return null;
    }

    private EddyTool? UpdateScale(bool entered)
    {
        StatusService.AddHints(
            ("LMB Drag", "Add / Remove Triles")
        );

        if (entered)
        {
            _scale.Reset();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _scale.Reset();
            _scale.Face = _hovered.Face ?? _selected.Face;

            if (_scale.Face.HasValue && _selected.Emplacements.Count > 0)
            {
                var faceNormal = _selected.Face!.Value.AsVector();
                var avgPos = _selected.Emplacements.Aggregate(Vector3.Zero,
                    (current, emp) => current + new Vector3(emp.X, emp.Y, emp.Z))
                             / _selected.Emplacements.Count;

                var planeNormal = Vector3.Normalize(Camera.InverseView.Backward);
                _scale.FaceNormal = faceNormal;
                _scale.InitialHandlePosition = avgPos;
                _scale.DragPlane = new Plane(planeNormal, -Vector3.Dot(planeNormal, avgPos));

                _scale.InitialSnapshot = [];
                foreach (var emplacement in _selected.Emplacements)
                {
                    if (Level.Triles.TryGetValue(emplacement, out var inst))
                    {
                        _scale.InitialSnapshot.Add((emplacement, inst.TrileId, inst.PhiLight));
                    }
                }

                _scale.InitialEmplacements = _selected.Emplacements.ToHashSet();
                _scale.CommittedSteps = 0;
                _scale.Active = true;
                _scale.Scope = History.BeginScope("Scale Triles");
            }
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && _scale.Active)
        {
            _selected.Face = _scale.Face;

            var ray = Scene.Viewport.Unproject(ImGuiX.GetMousePos(), _viewport);
            var t = ray.Intersects(_scale.DragPlane);
            if (t == null)
            {
                return null;
            }

            var worldPoint = ray.Position + ray.Direction * t.Value;
            var rawScalar = Vector3.Dot(worldPoint - _scale.InitialHandlePosition, _scale.FaceNormal);
            var snappedSteps = (int)MathF.Round(rawScalar);

            if (snappedSteps == _scale.CommittedSteps)
            {
                return null;
            }

            // Rollback: remove anything outside InitialEmplacements, restore InitialSnapshot
            foreach (var emp in _selected.Emplacements.ToList())
            {
                if (_scale.InitialEmplacements.Contains(emp))
                {
                    continue;
                }

                if (Level.Triles.Remove(emp, out var removed))
                {
                    RemoveFromGroup(emp, removed);
                    if (_trileActors.TryGetValue(removed.TrileId, out var a))
                    {
                        var m = a.GetComponent<TrilesMesh>();
                        m.RemoveInstance(emp);
                        CleanupEmptyActor(removed.TrileId, m);
                    }
                }
            }

            // Restore initial layer if it was removed
            foreach (var (emp, trileId, phi) in _scale.InitialSnapshot)
            {
                if (Level.Triles.ContainsKey(emp))
                {
                    continue;
                }

                var instance = new TrileInstance
                {
                    Position = new Vector3(emp.X, emp.Y, emp.Z).ToRepacker(),
                    TrileId = trileId,
                    PhiLight = phi
                };
                Level.Triles[emp] = instance;
                if (_selected.GroupId != null)
                {
                    AddToGroup(_selected.GroupId.Value, emp, instance);
                }

                EnsureTrileActor(trileId).GetComponent<TrilesMesh>()
                    .SetInstanceData(emp, instance.Position.ToXna(), phi);
            }

            _selected.Emplacements.Clear();
            foreach (var emp in _scale.InitialEmplacements)
            {
                _selected.Emplacements.Add(emp);
            }

            var ndx = (int)_scale.FaceNormal.X;
            var ndy = (int)_scale.FaceNormal.Y;
            var ndz = (int)_scale.FaceNormal.Z;

            if (snappedSteps > 0)
            {
                for (var s = 1; s <= snappedSteps; s++)
                {
                    foreach (var (emp, trileId, phi) in _scale.InitialSnapshot)
                    {
                        var target = new TrileEmplacement(emp.X + ndx * s, emp.Y + ndy * s, emp.Z + ndz * s);
                        if (Level.Triles.ContainsKey(target))
                        {
                            continue;
                        }

                        var instance = new TrileInstance
                        {
                            Position = new Vector3(target.X, target.Y, target.Z).ToRepacker(),
                            TrileId = trileId,
                            PhiLight = phi
                        };
                        Level.Triles[target] = instance;
                        if (_selected.GroupId != null)
                        {
                            AddToGroup(_selected.GroupId.Value, target, instance);
                        }

                        EnsureTrileActor(trileId).GetComponent<TrilesMesh>()
                            .SetInstanceData(target, instance.Position.ToXna(), phi);
                    }
                }

                _selected.Emplacements.Clear();
                foreach (var (emp, _, _) in _scale.InitialSnapshot)
                {
                    var target = new TrileEmplacement(emp.X + ndx * snappedSteps, emp.Y + ndy * snappedSteps, emp.Z + ndz * snappedSteps);
                    if (Level.Triles.ContainsKey(target))
                    {
                        _selected.Emplacements.Add(target);
                    }
                }

                UpdateCollisionMesh();
            }
            else if (snappedSteps < 0)
            {
                for (var s = 0; s >= snappedSteps; s--)
                {
                    foreach (var (emp, _, _) in _scale.InitialSnapshot)
                    {
                        var target = new TrileEmplacement(emp.X + ndx * s, emp.Y + ndy * s, emp.Z + ndz * s);
                        if (!Level.Triles.Remove(target, out var removed))
                        {
                            continue;
                        }

                        RemoveFromGroup(target, removed);
                        if (_trileActors.TryGetValue(removed.TrileId, out var actor))
                        {
                            var mesh = actor.GetComponent<TrilesMesh>();
                            mesh.RemoveInstance(target);
                            CleanupEmptyActor(removed.TrileId, mesh);
                        }
                    }
                }

                _selected.Emplacements.Clear();
                foreach (var (emp, _, _) in _scale.InitialSnapshot)
                {
                    var target = new TrileEmplacement(emp.X + ndx * snappedSteps, emp.Y + ndy * snappedSteps, emp.Z + ndz * snappedSteps);
                    if (Level.Triles.ContainsKey(target))
                    {
                        _selected.Emplacements.Add(target);
                    }
                }

                UpdateCollisionMesh();
                EnsurePlaceholder();
            }

            _scale.CommittedSteps = snappedSteps;
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _scale.Reset();
        }

        return null;
    }

    private EddyTool? UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Paint")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _paintScope?.Dispose();
            _paintScope = History.BeginScope("Paint Triles");
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _paintScope?.Dispose();
            _paintScope = null;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var entry = AssetBrowser.SelectedEntry;
            var trileId = entry.Type == AssetType.Trile ? _set!.FindByName(entry.Name).Id : InvalidId;

            if (_selected.Emplacements.Count > 0 && trileId != InvalidId)
            {
                foreach (var emp in _selected.Emplacements)
                {
                    if (!Level.Triles.TryGetValue(emp, out var instance))
                    {
                        continue;
                    }

                    var oldTrileId = instance.TrileId;
                    if (oldTrileId == trileId)
                    {
                        continue;
                    }

                    if (_trileActors.TryGetValue(oldTrileId, out var oldActor))
                    {
                        var oldMesh = oldActor.GetComponent<TrilesMesh>();
                        oldMesh.RemoveInstance(emp);
                        CleanupEmptyActor(oldTrileId, oldMesh);
                    }

                    instance.TrileId = trileId;
                    EnsureTrileActor(trileId).GetComponent<TrilesMesh>()
                        .SetInstanceData(emp, instance.Position.ToXna(), instance.PhiLight);
                }

                UpdateCollisionMesh();
            }
        }

        return null;
    }

    private EddyTool? UpdatePick()
    {
        StatusService.AddHints(
            ("LMB", "Pick Trile")
        );

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            return null;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hovered.Emplacement != null)
        {
            var pickedName = GetHoveredName();
            if (!string.IsNullOrEmpty(pickedName))
            {
                AssetBrowser.Pick(pickedName, AssetType.Trile);
                return EddyTool.Paint;
            }
        }

        return null;
    }


    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
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

            if (_selected.GroupId != null && !Level.Groups.ContainsKey(_selected.GroupId.Value))
            {
                _selected.GroupId = null;
            }

            var presentIds = Level.Triles.Values
                .Where(ti => ti.TrileId != InvalidId)
                .Select(ti => ti.TrileId)
                .ToHashSet();

            foreach (var id in _trileActors.Keys.ToList())
            {
                if (id != InvalidId && !presentIds.Contains(id))
                {
                    Scene.DestroyActor(_trileActors[id]);
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

            _selected.Emplacements.RemoveWhere(e => !Level.Triles.ContainsKey(e));
            if (_hovered.Emplacement != null && !Level.Triles.ContainsKey(_hovered.Emplacement))
            {
                _hovered.Reset();
            }
        }
        else
        {
            TeardownVisualization(force: false);

            #region Trile Set

            var path = $"Trile Sets/{Level.TrileSetName}";
            _set = (TrileSet)ResourceService.Load(path);
            AssetBrowser.SetTrileSet(path, _set);

            #endregion

            #region Triles

            var trileIds = Level.Triles.Values
                .Where(ti => ti.TrileId != InvalidId)
                .Select(ti => ti.TrileId)
                .Distinct();

            foreach (var id in trileIds)
            {
                var actor = Scene.CreateActor();
                actor.Name = _set.Triles.TryGetValue(id, out var trile) ? $"{id}: {trile.Name}" : $"{id}";
                _trileActors[id] = actor;

                var mesh = actor.AddComponent<TrilesMesh>();
                mesh.Visualize(_set, id);
                if (!mesh.HasGeometry)
                {
                    mesh.Pickable = Contexts.ShowCollisionMap.Value;
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

            _selected.GroupId = null;

            #endregion

            _selected.Reset();
            _hovered.Reset();
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
        if (_selected.Emplacements.Count == 0)
        {
            base.DrawProperties();
            return;
        }

        if (_selected.GroupId != null && Level.Groups.TryGetValue(_selected.GroupId.Value, out var group))
        {
            DrawGroupProperties(_selected.GroupId.Value, group);
            return;
        }

        var emplacement = _selected.Emplacements.First();
        if (!Level.Triles.TryGetValue(emplacement, out var instance) || instance.TrileId == InvalidId)
        {
            base.DrawProperties();
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
            using (History.BeginScope("Edit Trile Position"))
            {
                instance.Position = position.ToRepacker();
            }
        }

        var phi = (int)instance.PhiLight;
        var phiNames = new[] { "Front", "Right", "Back", "Left" };
        if (ImGui.Combo("Rotation", ref phi, phiNames, phiNames.Length))
        {
            using (History.BeginScope("Edit Trile Rotation"))
            {
                instance.PhiLight = (byte)phi;
            }
        }

        ImGui.SeparatorText("Actor Settings");

        if (instance.ActorSettings == null)
        {
            if (ImGui.Button($"{Icons.Add} Add"))
            {
                using (History.BeginScope("Add ActorSettings"))
                {
                    instance.ActorSettings = new TrileInstanceActorSettings();
                }
            }
        }

        if (instance.ActorSettings != null)
        {
            if (ImGui.Button($"{Icons.Trash} Remove"))
            {
                using (History.BeginScope("Remove ActorSettings"))
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
                using (History.BeginScope("Edit Contained Trile"))
                {
                    instance.ActorSettings.ContainedTrile = containedTrile;
                }
            }

            var signText = instance.ActorSettings.SignText;
            if (ImGui.InputText("Sign Text", ref signText, 1024))
            {
                using (History.BeginScope("Edit Sign Text"))
                {
                    instance.ActorSettings.SignText = signText;
                }
            }

            var sequence = instance.ActorSettings.Sequence;
            if (ImGuiX.EditableArray("Sequence", ref sequence, RenderItem))
            {
                using (History.BeginScope("Edit Sequence"))
                {
                    instance.ActorSettings.Sequence = sequence;
                }
            }

            var seqSample = instance.ActorSettings.SequenceSampleName;
            if (ImGui.InputText("Sequence Sample", ref seqSample, 255))
            {
                using (History.BeginScope("Edit Sequence Sample"))
                {
                    instance.ActorSettings.SequenceSampleName = seqSample;
                }
            }

            var altSeqSample = instance.ActorSettings.SequenceAlternateSampleName;
            if (ImGui.InputText("Sequence Alternate Sample", ref altSeqSample, 255))
            {
                using (History.BeginScope("Edit Sequence Alternate Sample"))
                {
                    instance.ActorSettings.SequenceAlternateSampleName = altSeqSample;
                }
            }

            var hostVolume = instance.ActorSettings.HostVolume ?? InvalidId;
            if (ImGui.InputInt("Host Volume", ref hostVolume))
            {
                using (History.BeginScope("Edit Host Volume"))
                {
                    instance.ActorSettings.HostVolume = hostVolume;
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
            using (History.BeginScope("Edit Group ActorType"))
            {
                group.ActorType = (ActorType)actor;
            }
        }

        var heavy = group.Heavy;
        if (ImGui.Checkbox("Heavy", ref heavy))
        {
            using (History.BeginScope("Edit Group Heavy"))
            {
                group.Heavy = heavy;
            }
        }

        var sound = group.AssociatedSound;
        if (ImGui.InputText("Sound", ref sound, 255))
        {
            using (History.BeginScope("Edit Group Sound"))
            {
                group.AssociatedSound = sound;
            }
        }

        ImGui.SeparatorText("Geyser");
        {
            var geyserOffset = group.GeyserOffset;
            if (ImGui.DragFloat("Offset", ref geyserOffset, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Offset"))
                {
                    group.GeyserOffset = geyserOffset;
                }
            }

            var geyserPause = group.GeyserPauseFor;
            if (ImGui.DragFloat("Pause For", ref geyserPause, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Pause"))
                {
                    group.GeyserPauseFor = geyserPause;
                }
            }

            var geyserLift = group.GeyserLiftFor;
            if (ImGui.DragFloat("Lift For", ref geyserLift, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Lift"))
                {
                    group.GeyserLiftFor = geyserLift;
                }
            }

            var geyserApex = group.GeyserApexHeight;
            if (ImGui.DragFloat("Apex Height", ref geyserApex, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Apex"))
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
                using (History.BeginScope("Edit Spin Center"))
                {
                    group.SpinCenter = spinCenter.ToRepacker();
                }
            }

            var spinClockwise = group.SpinClockwise;
            if (ImGui.Checkbox("Clockwise", ref spinClockwise))
            {
                using (History.BeginScope("Edit Spin Clockwise"))
                {
                    group.SpinClockwise = spinClockwise;
                }
            }

            var spinFreq = group.SpinFrequency;
            if (ImGui.DragFloat("Frequency", ref spinFreq, 0.1f))
            {
                using (History.BeginScope("Edit Spin Frequency"))
                {
                    group.SpinFrequency = spinFreq;
                }
            }

            var spinNeedsTrigger = group.SpinNeedsTriggering;
            if (ImGui.Checkbox("Needs Triggering", ref spinNeedsTrigger))
            {
                using (History.BeginScope("Edit Spin NeedsTriggering"))
                {
                    group.SpinNeedsTriggering = spinNeedsTrigger;
                }
            }

            var spin180 = group.Spin180Degrees;
            if (ImGui.Checkbox("180 Degrees", ref spin180))
            {
                using (History.BeginScope("Edit Spin 180"))
                {
                    group.Spin180Degrees = spin180;
                }
            }

            var fallOnRotate = group.FallOnRotate;
            if (ImGui.Checkbox("Fall On Rotate", ref fallOnRotate))
            {
                using (History.BeginScope("Edit Spin FallOnRotate"))
                {
                    group.FallOnRotate = fallOnRotate;
                }
            }

            var spinOffset = group.SpinOffset;
            if (ImGui.DragFloat("Offset", ref spinOffset, 0.1f))
            {
                using (History.BeginScope("Edit Spin Offset"))
                {
                    group.SpinOffset = spinOffset;
                }
            }
        }

        if (group.Path != null)
        {
            ImGui.TextDisabled($"Path: {group.Path.Segments.Count} segments");
        }
    }

    private void UpdateCursor()
    {
        if (Tool.Value is EddyTool.Select or EddyTool.Pick && _hovered.Emplacement != null)
        {
            // When hovering a grouped trile with no current selection, highlight the whole group as boxes
            if (_hovered.GroupId != null && _selected.Emplacements.Count == 0
                && _groupEmplacements.TryGetValue(_hovered.GroupId.Value, out var groupSet))
            {
                Cursor.SetHoverSurfaces(BuildBoxSurfaces(groupSet, HoverColor), HoverColor);
            }
            else if (Level.Triles.TryGetValue(_hovered.Emplacement, out var hoveredInstance))
            {
                var face = _hovered.Face ?? FaceOrientation.Front;
                var center = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
                var origin = center + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
                Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
            }
        }
        else
        {
            Cursor.ClearHover();
        }

        UpdateCursorSurfaces(_selected);
    }

    private void UpdateCursorSurfaces(CursorState cursor)
    {
        var validEmplacements = cursor.Emplacements
            .Where(e => Level.Triles.TryGetValue(e, out _))
            .ToList();

        if (validEmplacements.Count == 0)
        {
            Cursor.ClearSelection();
            return;
        }

        // Group selection: show boxes around each trile in the group
        if (cursor.GroupId != null)
        {
            Cursor.SetSelectionSurfaces(BuildBoxSurfaces(validEmplacements, SelectionColor), SelectionColor);
            return;
        }

        if (!cursor.Face.HasValue)
        {
            return;
        }

        var normal = cursor.Face.Value.AsVector();
        var faceSurfaces = validEmplacements.Select(e =>
        {
            var trileCenter = Level.Triles[e].Position.ToXna() + new Vector3(0.5f);
            var origin = trileCenter + normal * (0.5f + CursorMesh.OverlayOffset);
            var s = MeshSurface.CreateFaceQuad(Vector3.One, origin, cursor.Face.Value);
            return (s, PrimitiveType.TriangleList);
        });

        Cursor.SetSelectionSurfaces(faceSurfaces, SelectionColor);
    }

    private IEnumerable<(MeshSurface, PrimitiveType)> BuildBoxSurfaces(IEnumerable<TrileEmplacement> emplacements, Color color)
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

    public override void Dispose()
    {
        _paintScope?.Dispose();
        _translate.Scope?.Dispose();
        _scale.Scope?.Dispose();
        TeardownVisualization(force: true);
    }

    private void TeardownVisualization(bool force)
    {
        if (force && _collisionMapActor != null)
        {
            Scene.DestroyActor(_collisionMapActor);
            _collisionMapActor = null;
        }

        foreach (var actor in _trileActors.Values)
        {
            Scene.DestroyActor(actor);
        }

        _trileActors.Clear();
    }

    private string GetHoveredName()
    {
        if (_hovered.Emplacement == null || !Level.Triles.TryGetValue(_hovered.Emplacement, out var instance))
        {
            return string.Empty;
        }

        if (_set!.Triles.TryGetValue(instance.TrileId, out var trile))
        {
            return trile.Name;
        }

        return string.Empty;
    }

    private List<ClipboardEntry> BuildClipboard()
    {
        var validEntries = _selected.Emplacements
            .Where(e => Level.Triles.TryGetValue(e, out var inst) && inst.TrileId != InvalidId)
            .Select(e => (Emp: e, Inst: Level.Triles[e]))
            .ToList();

        if (validEntries.Count == 0)
        {
            return [];
        }

        var anchor = _select.SelectionAnchor ?? validEntries[0].Emp;
        return validEntries
            .Select(e => new ClipboardEntry(
                new TrileEmplacement(e.Emp.X - anchor.X, e.Emp.Y - anchor.Y, e.Emp.Z - anchor.Z),
                e.Inst.TrileId,
                e.Inst.PhiLight))
            .ToList();
    }

    private void PasteClipboard(List<ClipboardEntry> clipboard, TrileEmplacement origin)
    {
        _selected.Emplacements.Clear();
        _selected.Face = null;

        if (_hovered.Face.HasValue)
        {
            var step = _hovered.Face.Value.AsVector();
            origin = new TrileEmplacement(
                origin.X + (int)step.X,
                origin.Y + (int)step.Y,
                origin.Z + (int)step.Z);
        }

        foreach (var entry in clipboard)
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

            _selected.Emplacements.Add(emp);
        }

        UpdateCollisionMesh();
        EnsurePlaceholder();
    }

    private void RemoveSelected()
    {
        if (_selected.Emplacements.Count == 0)
        {
            return;
        }

        foreach (var emplacement in _selected.Emplacements.ToList())
        {
            if (!Level.Triles.Remove(emplacement, out var instance))
            {
                continue;
            }

            _selected.Emplacements.Remove(emplacement);
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
            Scene.DestroyActor(_trileActors[trileId]);
            _trileActors.Remove(trileId);
        }
    }

    private Actor EnsureTrileActor(int trileId)
    {
        if (_trileActors.TryGetValue(trileId, out var existing))
        {
            return existing;
        }

        var actor = Scene.CreateActor();
        actor.Name = $"{trileId}: {_set!.Triles[trileId].Name}";
        _trileActors[trileId] = actor;

        var mesh = actor.AddComponent<TrilesMesh>();
        mesh.Visualize(_set, trileId);

        return actor;
    }

    private void UpdateCollisionMesh()
    {
        if (_collisionMapActor == null)
        {
            _collisionMapActor = Scene.CreateActor();
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

        for (var x = 0; x < sizeX; x++)
        {
            for (var z = 0; z < sizeZ; z++)
            {
                var emplacement = new TrileEmplacement(x, 0, z);
                var instance = new TrileInstance
                {
                    Position = new Vector3(x, 0, z).ToRepacker(),
                    TrileId = InvalidId,
                    PhiLight = 0
                };

                Level.Triles[emplacement] = instance;
            }
        }

        TrilesMesh mesh;
        if (!_trileActors.TryGetValue(InvalidId, out var actor))
        {
            actor = Scene.CreateActor();
            actor.Name = "Placeholder";
            _trileActors[InvalidId] = actor;

            mesh = actor.AddComponent<TrilesMesh>();
            mesh.Visualize(_set!, InvalidId);
        }

        mesh = actor.GetComponent<TrilesMesh>();
        foreach (var (emplacement, instance) in Level.Triles.Where(kv => kv.Value.TrileId == InvalidId))
        {
            mesh.SetInstanceData(emplacement, instance.Position.ToXna(), instance.PhiLight);
        }
    }

    private record struct ClipboardEntry(TrileEmplacement Offset, int TrileId, byte PhiLight);

    private struct CursorState
    {
        public readonly HashSet<TrileEmplacement> Emplacements = [];
        public FaceOrientation? Face = null;
        public int? GroupId = null;

        public CursorState() { }

        public readonly TrileEmplacement? Emplacement =>
            Emplacements.Count == 1 ? Emplacements.First() : null;

        public void Reset()
        {
            Emplacements.Clear();
            Face = null;
            GroupId = null;
        }
    }

    private struct SelectState
    {
        public TrileEmplacement? RectOrigin;
        public TrileEmplacement? SelectionAnchor;
        public bool WasDrag;
        public List<ClipboardEntry>? Clipboard; // null = nothing copied
    }

    private struct TranslateState
    {
        public Vector3 InitialHandlePosition;
        public Dictionary<TrileEmplacement, Vector3> InitialPositions = [];
        public Plane DragPlane;
        public Vector3? LockedAxis;
        public Vector2 LockAccum;
        public bool AxisLocked;
        public bool Active;
        public IDisposable? Scope;

        public TranslateState() { }

        public void Reset()
        {
            Scope?.Dispose();
            this = new TranslateState();
        }
    }

    private struct ScaleState
    {
        public FaceOrientation? Face;
        public Vector3 FaceNormal;
        public Vector3 InitialHandlePosition;
        public Plane DragPlane;
        public List<(TrileEmplacement Emp, int TrileId, byte PhiLight)> InitialSnapshot = [];
        public HashSet<TrileEmplacement> InitialEmplacements = [];
        public int CommittedSteps;
        public bool Active;
        public IDisposable? Scope;

        public ScaleState() { }

        public void Reset()
        {
            Scope?.Dispose();
            this = new ScaleState();
        }
    }
}