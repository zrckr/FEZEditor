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

internal class TrileContext : EddyContext
{
    private static readonly Color HoverColor = Color.Blue with { A = 85 }; // 33%

    private static readonly Color SelectionColor = Color.Red with { A = 85 }; // 33%

    public Dirty<bool> ShowCollisionMap { get; set; } = new(false);

    private readonly Dictionary<int, Actor> _trileActors = new();

    private Actor? _collisionMapActor;

    private TrileSet? _set;

    private TrileEmplacement? _hoveredEmplacement;

    private FaceOrientation? _hoveredFace;

    private readonly HashSet<TrileEmplacement> _selectedEmplacements = new();

    private FaceOrientation? _selectedFace;

    private SelectState _select;

    private TranslateState _translate;

    private ScaleState _scale;

    private IDisposable? _paintScope;

    public override bool Pick(Ray ray)
    {
        Actor? actor = null;
        TrilesMesh? mesh = null;
        PickHit hit = default;

        foreach (var (a, h) in Scene.RaycastAll(ray))
        {
            if (a.TryGetComponent<TrilesMesh>(out var m) && m != null)
            {
                actor = a;
                mesh = m;
                hit = h;
                break;
            }
        }

        var toolNeedsHover = Tool.Value is EddyTool.Select or EddyTool.Pick;

        if (actor == null || mesh == null)
        {
            ClearHover();
            return !toolNeedsHover;
        }

        var emplacement = mesh.GetEmplacement(hit.Index);
        if (!Level.Triles.ContainsKey(emplacement) || !toolNeedsHover)
        {
            ClearHover();
            return !toolNeedsHover;
        }

        #region Determine Face

        FaceOrientation face;
        {
            var box = mesh.GetBounds().ElementAt(hit.Index);
            var hitPoint = ray.Position + ray.Direction * hit.Distance;
            var center = (box.Min + box.Max) / 2f;
            var halfSize = (box.Max - box.Min) / 2f;

            var local = hitPoint - center;
            var abs = new Vector3
            {
                X = MathF.Abs(local.X / halfSize.X),
                Y = MathF.Abs(local.Y / halfSize.Y),
                Z = MathF.Abs(local.Z / halfSize.Z)
            };

            Vector3 normal;
            if (abs.X > abs.Y && abs.X > abs.Z)
            {
                normal = new Vector3(MathF.Sign(local.X), 0, 0);
            }
            else if (abs.Y > abs.Z)
            {
                normal = new Vector3(0, MathF.Sign(local.Y), 0);
            }
            else
            {
                normal = new Vector3(0, 0, MathF.Sign(local.Z));
            }

            face = FaceExtensions.OrientationFromDirection(normal);
        }

        #endregion

        _hoveredEmplacement = emplacement;
        _hoveredFace = face;
        return true;
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
            _selectedEmplacements.Clear();
            _selectedFace = null;
            Tool = EddyTool.Select;
            return;
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

        if (_selectedEmplacements.Count > 0)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );
        }

        if (_select.Clipboard != null && _hoveredEmplacement != null)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selectedEmplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (History.BeginScope("Delete Triles"))
            {
                RemoveSelected();
            }
            _selectedEmplacements.Clear();
            _selectedFace = null;
        }

        if (ImGui.GetIO().KeyCtrl)
        {
            if (_selectedEmplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                _select.Clipboard = BuildClipboard();
            }

            if (_selectedEmplacements.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                _select.Clipboard = BuildClipboard();
                using (History.BeginScope("Cut Triles"))
                {
                    RemoveSelected();
                }
                _selectedEmplacements.Clear();
                _selectedFace = null;
            }

            if (_select.Clipboard != null && _hoveredEmplacement != null && ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (History.BeginScope("Paste Triles"))
                {
                    PasteClipboard(_select.Clipboard, _hoveredEmplacement);
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _select.RectOrigin = _hoveredEmplacement;
            _select.WasDrag = false;
        }

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _select.RectOrigin != null && _hoveredEmplacement != null)
        {
            var min = _select.RectOrigin.Min(_hoveredEmplacement);
            var max = _select.RectOrigin.Max(_hoveredEmplacement);

            _selectedEmplacements.Clear();
            for (var x = min.X; x <= max.X; x++)
            {
                for (var y = min.Y; y <= max.Y; y++)
                {
                    for (var z = min.Z; z <= max.Z; z++)
                    {
                        var emplacement = new TrileEmplacement(x, y, z);
                        if (Level.Triles.ContainsKey(emplacement))
                        {
                            _selectedEmplacements.Add(emplacement);
                        }
                    }
                }
            }

            _selectedFace = _hoveredFace;
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
                // Single click: select or deselect hovered emplacement
                if (_hoveredEmplacement == null)
                {
                    _selectedEmplacements.Clear();
                }
                else
                {
                    _selectedEmplacements.Clear();
                    _selectedEmplacements.Add(_hoveredEmplacement);
                }
            }

            _selectedFace = _hoveredFace;
        }

        return null;
    }

    private EddyTool? UpdateTranslate(bool entered)
    {
        StatusService.AddHints(
            ("LMB Drag", "Move X or Z"),
            ("Alt+LMB Drag", "Move Y"),
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
            var ray = Scene.Viewport.Unproject(ImGuiX.GetMousePos(), ViewportMin);
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

            snappedDelta.X = MathF.Round(snappedDelta.X / Mathz.TrixelSize) * Mathz.TrixelSize;
            snappedDelta.Y = MathF.Round(snappedDelta.Y / Mathz.TrixelSize) * Mathz.TrixelSize;
            snappedDelta.Z = MathF.Round(snappedDelta.Z / Mathz.TrixelSize) * Mathz.TrixelSize;

            foreach (var emp in _selectedEmplacements)
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

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedEmplacements.Count > 0 && !_translate.Active)
        {
            using (History.BeginScope("Reset Translate"))
            {
                foreach (var emp in _selectedEmplacements)
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
        if (_selectedEmplacements.Count == 0)
        {
            return;
        }

        var avgPos = Vector3.Zero;
        var count = 0;

        foreach (var emp in _selectedEmplacements)
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
        foreach (var emplacement in _selectedEmplacements)
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

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _selectedEmplacements.Count > 0)
        {
            using (History.BeginScope("Rotate Triles"))
            {
                foreach (var emp in _selectedEmplacements)
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
            _scale.Face = _hoveredFace ?? _selectedFace;

            if (_scale.Face.HasValue && _selectedEmplacements.Count > 0)
            {
                var faceNormal = _selectedFace!.Value.AsVector();
                var avgPos = _selectedEmplacements.Aggregate(Vector3.Zero,
                    (current, emp) => current + new Vector3(emp.X, emp.Y, emp.Z))
                             / _selectedEmplacements.Count;

                var planeNormal = Vector3.Normalize(Camera.InverseView.Backward);
                _scale.FaceNormal = faceNormal;
                _scale.InitialHandlePosition = avgPos;
                _scale.DragPlane = new Plane(planeNormal, -Vector3.Dot(planeNormal, avgPos));

                _scale.InitialSnapshot = [];
                foreach (var emplacement in _selectedEmplacements)
                {
                    if (Level.Triles.TryGetValue(emplacement, out var inst))
                    {
                        _scale.InitialSnapshot.Add((emplacement, inst.TrileId, inst.PhiLight));
                    }
                }

                _scale.InitialEmplacements = _selectedEmplacements.ToHashSet();
                _scale.CommittedSteps = 0;
                _scale.Active = true;
                _scale.Scope = History.BeginScope("Scale Triles");
            }
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left) && _scale.Active)
        {
            _selectedFace = _scale.Face;

            var ray = Scene.Viewport.Unproject(ImGuiX.GetMousePos(), ViewportMin);
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
            foreach (var emp in _selectedEmplacements.ToList())
            {
                if (_scale.InitialEmplacements.Contains(emp))
                {
                    continue;
                }

                if (Level.Triles.Remove(emp, out var removed) && _trileActors.TryGetValue(removed.TrileId, out var a))
                {
                    var m = a.GetComponent<TrilesMesh>();
                    m.RemoveInstance(emp);
                    CleanupEmptyActor(removed.TrileId, m);
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
                EnsureTrileActor(trileId).GetComponent<TrilesMesh>()
                    .SetInstanceData(emp, instance.Position.ToXna(), phi);
            }

            _selectedEmplacements.Clear();
            foreach (var emp in _scale.InitialEmplacements)
            {
                _selectedEmplacements.Add(emp);
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
                        EnsureTrileActor(trileId).GetComponent<TrilesMesh>()
                            .SetInstanceData(target, instance.Position.ToXna(), phi);
                    }
                }

                _selectedEmplacements.Clear();
                foreach (var (emp, _, _) in _scale.InitialSnapshot)
                {
                    var target = new TrileEmplacement(emp.X + ndx * snappedSteps, emp.Y + ndy * snappedSteps, emp.Z + ndz * snappedSteps);
                    if (Level.Triles.ContainsKey(target))
                    {
                        _selectedEmplacements.Add(target);
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

                        if (_trileActors.TryGetValue(removed.TrileId, out var actor))
                        {
                            var mesh = actor.GetComponent<TrilesMesh>();
                            mesh.RemoveInstance(target);
                            CleanupEmptyActor(removed.TrileId, mesh);
                        }
                    }
                }

                _selectedEmplacements.Clear();
                foreach (var (emp, _, _) in _scale.InitialSnapshot)
                {
                    var target = new TrileEmplacement(emp.X + ndx * snappedSteps, emp.Y + ndy * snappedSteps, emp.Z + ndz * snappedSteps);
                    if (Level.Triles.ContainsKey(target))
                    {
                        _selectedEmplacements.Add(target);
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

            if (_selectedEmplacements.Count > 0 && trileId != InvalidId)
            {
                foreach (var emp in _selectedEmplacements)
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

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredEmplacement != null)
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

            _selectedEmplacements.RemoveWhere(e => !Level.Triles.ContainsKey(e));
            if (!Level.Triles.ContainsKey(_hoveredEmplacement ?? new TrileEmplacement()))
            {
                _hoveredEmplacement = null;
                _hoveredFace = null;
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
            }

            #endregion

            _selectedEmplacements.Clear();
            _hoveredEmplacement = null;
            _hoveredFace = null;
            _selectedFace = null;

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
        if (_selectedEmplacements.Count == 0)
        {
            base.DrawProperties();
            return;
        }

        var emplacement = _selectedEmplacements.First();
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

    public virtual void DrawDebug(Dictionary<string, string> stats)
    {
        stats["Face"] = _hoveredFace.HasValue ? _hoveredFace.Value.ToString() : "none";
    }

    private void UpdateCursor()
    {
        if (Tool.Value == EddyTool.Select
            && _hoveredEmplacement != null
            && Level.Triles.TryGetValue(_hoveredEmplacement, out var hoveredInstance))
        {
            var face = _hoveredFace ?? FaceOrientation.Front;
            var center = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
            var origin = center + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
            var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
            Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
        }
        else
        {
            Cursor.ClearHover();
        }

        var validPositions = _selectedEmplacements
            .Where(e => Level.Triles.TryGetValue(e, out _))
            .Select(e => Level.Triles[e].Position.ToXna())
            .ToList();

        if (validPositions.Count == 0)
        {
            Cursor.ClearSelection();
            return;
        }

        if (!_selectedFace.HasValue)
        {
            return;
        }

        var normal = _selectedFace.Value.AsVector();
        var surfaces = validPositions.Select(pos =>
        {
            var trileCenter = pos + new Vector3(0.5f);
            var origin = trileCenter + normal * (0.5f + CursorMesh.OverlayOffset);
            var s = MeshSurface.CreateFaceQuad(Vector3.One, origin, _selectedFace.Value);
            return (s, PrimitiveType.TriangleList);
        });

        Cursor.SetSelectionSurfaces(surfaces, SelectionColor);
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

    private void ClearHover()
    {
        _hoveredEmplacement = null;
        _hoveredFace = null;
    }

    private string GetHoveredName()
    {
        if (_hoveredEmplacement == null || !Level.Triles.TryGetValue(_hoveredEmplacement, out var instance))
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
        var validEntries = _selectedEmplacements
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
        _selectedEmplacements.Clear();
        _selectedFace = null;

        if (_hoveredFace.HasValue)
        {
            var step = _hoveredFace.Value.AsVector();
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

            _selectedEmplacements.Add(emp);
        }

        UpdateCollisionMesh();
        EnsurePlaceholder();
    }

    private void RemoveSelected()
    {
        if (_selectedEmplacements.Count == 0)
        {
            return;
        }

        foreach (var emplacement in _selectedEmplacements.ToList())
        {
            if (!Level.Triles.Remove(emplacement, out var instance))
            {
                continue;
            }

            _selectedEmplacements.Remove(emplacement);
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