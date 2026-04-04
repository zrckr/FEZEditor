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
    public override bool IsSelected => _selectedCursor.Emplacements.Count > 0;

    private readonly Dictionary<int, Actor> _trileActors = new();

    private Actor? _collisionMapActor;

    private TrileSet? _set;

    private CursorState _hoveredCursor = new();

    private CursorState _selectedCursor = new();

    private SelectState _select = new();

    private ScaleState _scale = new();

    private IDisposable? _translateScope;

    private IDisposable? _scaleScope;

    private IDisposable? _paintScope;

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

    public override bool IsHovered(Ray ray, RaycastHit? hit)
    {
        _hoveredCursor.Reset();
        if (!hit.HasValue || !hit.Value.Actor.HasComponent<TrilesMesh>())
        {
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !Gizmo.IsActive)
            {
                _selectedCursor.Reset();
                Tool = EddyTool.Select;
            }

            return false;
        }

        if (!hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) || mesh == null)
        {
            return false;
        }

        var index = hit.Value.Index;
        var emplacement = mesh.GetEmplacement(index);

        if (!Level.Triles.ContainsKey(emplacement))
        {
            return false;
        }

        var box = mesh.GetBounds().ElementAt(index);
        var distance = hit.Value.Distance;
        _hoveredCursor.Emplacements.Clear();
        _hoveredCursor.Emplacements.Add(emplacement);
        _hoveredCursor.Face = Mathz.DetermineFace(box, ray, distance);
        _hoveredCursor.GroupId = _emplacementGroups.TryGetValue(emplacement, out var gid) ? gid : null;
        return true;
    }

    public override void Update()
    {
        StatusService.ClearHints();

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selectedCursor.Reset();
            Tool = EddyTool.Select;
        }

        if (Tool is not (EddyTool.Select or EddyTool.Pick))
        {
            _hoveredCursor.Reset();
        }

        switch (Tool)
        {
            case EddyTool.Select: UpdateSelect(); break;
            case EddyTool.Translate: UpdateTranslate(); break;
            case EddyTool.Rotate: UpdateRotate(); break;
            case EddyTool.Paint: UpdatePaint(); break;
            case EddyTool.Pick: UpdatePick(); break;
            case EddyTool.Scale: UpdateScale(); break;
            default: throw new InvalidOperationException();
        }
    }

    public override void DrawCursor()
    {
        if (Tool is EddyTool.Select or EddyTool.Pick && _hoveredCursor.Emplacement != null)
        {
            // When hovering a grouped trile with no current selection, highlight the whole group as boxes
            if (_hoveredCursor.GroupId != null && _selectedCursor.Emplacements.Count == 0
                                               && _groupEmplacements.TryGetValue(_hoveredCursor.GroupId.Value,
                                                   out var groupSet))
            {
                CursorMesh.SetHoverSurfaces(BuildBoxSurfaces(groupSet, HoverColor), HoverColor);
            }
            else if (Level.Triles.TryGetValue(_hoveredCursor.Emplacement, out var hoveredInstance))
            {
                var face = _hoveredCursor.Face ?? FaceOrientation.Front;
                var center = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
                var origin = center + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
                CursorMesh.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
            }
        }

        if (_selectedCursor.Emplacements.Count > 0)
        {
            // Group selection: show boxes around each trile in the group
            if (_selectedCursor.GroupId != null)
            {
                var surfaces = BuildBoxSurfaces(_selectedCursor.Emplacements, SelectionColor);
                CursorMesh.SetSelectionSurfaces(surfaces, SelectionColor);
                return;
            }

            if (_selectedCursor.Face.HasValue)
            {
                var normal = _selectedCursor.Face.Value.AsVector();
                var faceSurfaces = _selectedCursor.Emplacements.Select(e =>
                {
                    var trileCenter = Level.Triles[e].Position.ToXna() + new Vector3(0.5f);
                    var origin = trileCenter + normal * (0.5f + CursorMesh.OverlayOffset);
                    var s = MeshSurface.CreateFaceQuad(Vector3.One, origin, _selectedCursor.Face.Value);
                    return (s, PrimitiveType.TriangleList);
                });
                CursorMesh.SetSelectionSurfaces(faceSurfaces, SelectionColor);
            }
        }
    }

    private void UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
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
                StatusService.AddHints(("Ctrl+Shift+G", "Group"));
            }
        }

        if (_select.Clipboard.Count > 0 && _hoveredCursor.Emplacement != null)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selectedCursor.Emplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (History.BeginScope("Delete Triles"))
            {
                RemoveSelected();
            }

            _selectedCursor.Reset();
        }

        if (ImGui.GetIO().KeyCtrl && ImGui.GetIO().KeyShift && ImGui.IsKeyPressed(ImGuiKey.G) &&
            _selectedCursor.GroupId != null)
        {
            using (History.BeginScope("Remove Trile Group"))
            {
                Level.Groups.Remove(_selectedCursor.GroupId.Value);
            }

            _selectedCursor.GroupId = null;
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
                using (History.BeginScope("Cut Triles"))
                {
                    RemoveSelected();
                }

                _selectedCursor.Emplacements.Clear();
                _selectedCursor.Face = null;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (History.BeginScope("Paste Triles"))
                {
                    PasteClipboard();
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _select.RectOrigin = _hoveredCursor.Emplacement;
            _select.WasDrag = false;
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

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            var wasDrag = _select.WasDrag;
            _select.SelectionAnchor = _select.RectOrigin;
            _select.RectOrigin = null;
            _select.WasDrag = false;

            if (!wasDrag)
            {
                _selectedCursor.Emplacements.Clear();
                _selectedCursor.GroupId = null;

                if (_hoveredCursor.Emplacement != null)
                {
                    // If hovering a grouped trile, select the whole group
                    if (_hoveredCursor.GroupId != null &&
                        _groupEmplacements.TryGetValue(_hoveredCursor.GroupId.Value, out var groupSet))
                    {
                        _selectedCursor.Emplacements.UnionWith(groupSet);
                        _selectedCursor.GroupId = _hoveredCursor.GroupId;
                    }
                    else
                    {
                        _selectedCursor.Emplacements.Add(_hoveredCursor.Emplacement);
                    }
                }
            }

            _selectedCursor.Face = _hoveredCursor.Face;
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
        if (Gizmo.Translate(ref centroid))
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

        if (Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = History.BeginScope("Translate Trile");
        }

        if (Gizmo.DragEnded)
        {
            _translateScope?.Dispose();
            _translateScope = null;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedCursor.Emplacements.Count > 0 && _translateScope == null)
        {
            using (History.BeginScope("Reset Translate Trile"))
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

        if (Gizmo.Rotate(centroid))
        {
            using (History.BeginScope("Rotate Trile(s)"))
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
        if (Gizmo.ScaleFace(centroid, _selectedCursor.Face.Value, out var delta))
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

        if (Gizmo.DragStarted)
        {
            _scaleScope?.Dispose();
            _scaleScope = History.BeginScope("Scale Triles");
            _scale = new ScaleState();

            var faceVec = _selectedCursor.Face.Value.AsVector();
            _scale.Dx = (int)faceVec.X;
            _scale.Dy = (int)faceVec.Y;
            _scale.Dz = (int)faceVec.Z;

            foreach (var emplacement in _selectedCursor.Emplacements)
            {
                if (Level.Triles.TryGetValue(emplacement, out var inst))
                {
                    _scale.Snapshot.Add(new TrileEntry(emplacement, inst.TrileId, inst.PhiLight));
                }
            }
        }

        if (Gizmo.DragEnded)
        {
            _scale = new ScaleState();
            _scaleScope?.Dispose();
            _scaleScope = null;
        }
    }

    private void UpdatePaint()
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

            if (_selectedCursor.Emplacements.Count > 0 && trileId != InvalidId)
            {
                foreach (var emp in _selectedCursor.Emplacements)
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
                AssetBrowser.Pick(pickedName, AssetType.Trile);
                Tool = EddyTool.Paint;
            }
        }
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
        if (_selectedCursor.Emplacements.Count == 0)
        {
            base.DrawProperties();
            return;
        }

        if (_selectedCursor.GroupId != null && Level.Groups.TryGetValue(_selectedCursor.GroupId.Value, out var group))
        {
            DrawGroupProperties(_selectedCursor.GroupId.Value, group);
            return;
        }

        var emplacement = _selectedCursor.Emplacements.First();
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

    private IEnumerable<(MeshSurface, PrimitiveType)> BuildBoxSurfaces(IEnumerable<TrileEmplacement> emplacements,
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

    public override void Dispose()
    {
        _translateScope?.Dispose();
        _scaleScope?.Dispose();
        _paintScope?.Dispose();
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
        public readonly List<ClipboardEntry> Clipboard = new();
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