using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class ArtObjectContext : EddyContext
{
    public override bool IsSelected => _selectedIds.Count > 0;

    private readonly Dictionary<int, Actor> _artObjectActors = new();

    private int? _hoveredId;

    private readonly HashSet<int> _selectedIds = new();

    private TranslateState _translate;

    private ScaleState _scale;

    private Vector2 _viewport;

    private IDisposable? _paintScope;

    private Actor? _ghostActor;

    private string? _ghostName;

    private List<ArtObjectInstance>? _clipboard;

    public override void TestConditions(Ray ray, RaycastHit? hit, Vector2 viewport)
    {
        _viewport = viewport;
        if (hit.HasValue && hit.Value.Actor.HasComponent<ArtObjectMesh>())
        {
            var foundId = _artObjectActors
                .FirstOrDefault(kv => kv.Value == hit.Value.Actor).Key;

            if (_artObjectActors.ContainsKey(foundId))
            {
                _hoveredId = foundId;
                Contexts.TransitionTo<ArtObjectContext>();
            }
        }
    }

    public override void End()
    {
        _hoveredId = null;
        Cursor.ClearHover();
        Cursor.ClearSelection();
        DestroyGhost();
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
            _selectedIds.Clear();
            Tool = EddyTool.Select;
        }

        if (Tool.Value is not (EddyTool.Select or EddyTool.Pick))
        {
            _hoveredId = null;
        }

        var nextTool = Tool.Value;
        if (!ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            nextTool = Tool.Value switch
            {
                EddyTool.Select => UpdateSelect(),
                EddyTool.Translate => UpdateTranslate(Tool.IsDirty),
                EddyTool.Rotate => UpdateRotate(),
                EddyTool.Scale => UpdateScale(Tool.IsDirty),
                EddyTool.Paint => UpdatePaint(),
                EddyTool.Pick => UpdatePick(),
                _ => throw new ArgumentOutOfRangeException()
            } ?? Tool.Value;
        }

        Tool = Tool.Clean();
        if (nextTool != Tool.Value)
        {
            Tool = nextTool;
        }

        if (Tool.Value != EddyTool.Paint)
        {
            DestroyGhost();
        }

        UpdateCursor();
    }

    private EddyTool? UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
            ("Shift+LMB", "Add to Selection")
        );

        if (_selectedIds.Count > 0)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );
        }

        if (_clipboard != null)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (History.BeginScope("Delete Art Objects"))
            {
                foreach (var id in _selectedIds.ToList())
                {
                    Level.ArtObjects.Remove(id);
                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        Scene.DestroyActor(actor);
                        _artObjectActors.Remove(id);
                    }
                }
            }

            _selectedIds.Clear();
        }

        if (ImGui.GetIO().KeyCtrl)
        {
            if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                _clipboard = BuildClipboard();
            }

            if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                _clipboard = BuildClipboard();
                using (History.BeginScope("Cut Art Objects"))
                {
                    foreach (var id in _selectedIds.ToList())
                    {
                        Level.ArtObjects.Remove(id);
                        if (_artObjectActors.TryGetValue(id, out var actor))
                        {
                            Scene.DestroyActor(actor);
                            _artObjectActors.Remove(id);
                        }
                    }
                }

                _selectedIds.Clear();
            }

            if (_clipboard != null && ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (History.BeginScope("Paste Art Objects"))
                {
                    PasteClipboard(_clipboard);
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var shift = ImGui.GetIO().KeyShift;
            if (_hoveredId.HasValue)
            {
                if (shift)
                {
                    if (!_selectedIds.Add(_hoveredId.Value))
                    {
                        _selectedIds.Remove(_hoveredId.Value);
                    }
                }
                else
                {
                    _selectedIds.Clear();
                    _selectedIds.Add(_hoveredId.Value);
                }
            }
            else if (!shift)
            {
                _selectedIds.Clear();
            }
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

            foreach (var id in _selectedIds)
            {
                if (!Level.ArtObjects.TryGetValue(id, out var instance))
                {
                    continue;
                }

                if (!_translate.InitialPositions.TryGetValue(id, out var initialPos))
                {
                    continue;
                }

                var newPos = initialPos + snappedDelta;
                instance.Position = newPos.ToRepacker();

                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = newPos;
                }
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _translate.Reset();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedIds.Count > 0 && !_translate.Active)
        {
            using (History.BeginScope("Reset Translate"))
            {
                foreach (var id in _selectedIds)
                {
                    if (!Level.ArtObjects.TryGetValue(id, out var instance))
                    {
                        continue;
                    }

                    var pos = Vector3.Zero;
                    instance.Position = pos.ToRepacker();

                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Position = pos;
                    }
                }
            }
        }

        return null;
    }

    private void InitTranslateDragState()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var avgPos = Vector3.Zero;
        var count = 0;

        foreach (var id in _selectedIds)
        {
            if (!Level.ArtObjects.TryGetValue(id, out var inst))
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
        foreach (var id in _selectedIds)
        {
            if (Level.ArtObjects.TryGetValue(id, out var inst))
            {
                _translate.InitialPositions[id] = inst.Position.ToXna();
            }
        }

        _translate.Active = true;
        _translate.Scope = History.BeginScope("Translate Art Objects");
    }

    private Vector2 ScreenDir(Vector3 worldDir)
    {
        var origin = Camera.Project(Vector3.Zero, Vector2.Zero);
        var projected = Camera.Project(worldDir, Vector2.Zero);
        var screen = new Vector2(projected.X - origin.X, projected.Y - origin.Y);
        return screen.LengthSquared() > float.Epsilon ? Vector2.Normalize(screen) : screen;
    }

    private EddyTool? UpdateRotate()
    {
        StatusService.AddHints(
            ("LMB", "Rotate 90°")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _selectedIds.Count > 0)
        {
            using (History.BeginScope("Rotate Art Objects"))
            {
                foreach (var id in _selectedIds)
                {
                    if (!Level.ArtObjects.TryGetValue(id, out var instance))
                    {
                        continue;
                    }

                    var rotation = instance.Rotation.ToXna() * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
                    instance.Rotation = rotation.ToRepacker();

                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Rotation = rotation;
                    }
                }
            }
        }

        return null;
    }

    private EddyTool? UpdateScale(bool entered)
    {
        StatusService.AddHints(
            ("LMB Drag", "Scale Uniformly"),
            ("R", "Reset")
        );

        if (entered)
        {
            _scale = new ScaleState();
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _selectedIds.Count > 0)
        {
            _scale = new ScaleState();

            _scale.InitialScales = [];
            foreach (var id in _selectedIds)
            {
                if (Level.ArtObjects.TryGetValue(id, out var inst))
                {
                    _scale.InitialScales[id] = inst.Scale.ToXna();
                }
            }

            _scale.Active = true;
            _scale.Scope = History.BeginScope("Scale Art Objects");
        }

        if (ImGui.IsMouseDragging(ImGuiMouseButton.Left) && _scale.Active)
        {
            // Use screen-space drag delta: right = grow, left = shrink
            var dragDelta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left);
            var rawScalar = dragDelta.X;

            var scaleFactor = 1f + rawScalar * 0.005f;
            scaleFactor = MathF.Max(scaleFactor, 0.01f);
            {

                foreach (var id in _selectedIds)
                {
                    if (!Level.ArtObjects.TryGetValue(id, out var instance))
                    {
                        continue;
                    }

                    if (!_scale.InitialScales.TryGetValue(id, out var initialScale))
                    {
                        continue;
                    }

                    var newScale = initialScale * scaleFactor;
                    instance.Scale = newScale.ToRepacker();

                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Scale = newScale;
                    }
                }
            }
        }

        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            _scale.Reset();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedIds.Count > 0 && !_scale.Active)
        {
            using (History.BeginScope("Reset Scale"))
            {
                foreach (var id in _selectedIds)
                {
                    if (!Level.ArtObjects.TryGetValue(id, out var instance))
                    {
                        continue;
                    }

                    instance.Scale = Vector3.One.ToRepacker();

                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Scale = Vector3.One;
                    }
                }
            }
        }

        return null;
    }

    private EddyTool? UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Place")
        );

        UpdateGhost();

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var entry = AssetBrowser.SelectedEntry;
            if (entry.Type != AssetType.ArtObject)
            {
                return null;
            }

            if (_ghostActor == null)
            {
                return null;
            }

            var position = _ghostActor.Transform.Position;

            using (History.BeginScope("Place Art Object"))
            {
                var id = NextAvailableId();
                var instance = new ArtObjectInstance
                {
                    Name = entry.Name,
                    Position = position.ToRepacker(),
                    Rotation = Quaternion.Identity.ToRepacker(),
                    Scale = Vector3.One.ToRepacker()
                };
                Level.ArtObjects[id] = instance;

                var actor = Scene.CreateActor();
                actor.Name = $"{id}: {instance.Name}";
                actor.Transform.Position = position;
                actor.Transform.Rotation = Quaternion.Identity;
                actor.Transform.Scale = Vector3.One;
                _artObjectActors[id] = actor;

                var mesh = actor.AddComponent<ArtObjectMesh>();
                var ao = (ArtObject)ResourceService.Load($"Art Objects/{instance.Name}");
                mesh.Visualize(ao);

                _selectedIds.Clear();
                _selectedIds.Add(id);
            }
        }

        return null;
    }

    private void UpdateGhost()
    {
        var entry = AssetBrowser.SelectedEntry;
        if (entry.Type != AssetType.ArtObject)
        {
            DestroyGhost();
            return;
        }

        if (_ghostName != entry.Name)
        {
            DestroyGhost();
            _ghostName = entry.Name;

            _ghostActor = Scene.CreateActor();
            _ghostActor.Name = $"Ghost: {entry.Name}";
            _ghostActor.Transform.Scale = Vector3.One;
            _ghostActor.Transform.Rotation = Quaternion.Identity;

            var mesh = _ghostActor.AddComponent<ArtObjectMesh>();
            var ao = (ArtObject)ResourceService.Load($"Art Objects/{entry.Name}");
            mesh.Visualize(ao);
        }

        if (_ghostActor != null && _hoveredId.HasValue && Level.ArtObjects.TryGetValue(_hoveredId.Value, out var hovered))
        {
            var snapped = SnapToGrid(hovered.Position.ToXna());
            _ghostActor.Transform.Position = snapped;
        }
        else if (_ghostActor != null)
        {
            // Position ghost at world origin if no hovered AO
            _ghostActor.Transform.Position = Vector3.Zero;
        }
    }

    private static Vector3 SnapToGrid(Vector3 pos)
    {
        return new Vector3(
            MathF.Round(pos.X),
            MathF.Round(pos.Y),
            MathF.Round(pos.Z));
    }

    private void DestroyGhost()
    {
        if (_ghostActor != null)
        {
            Scene.DestroyActor(_ghostActor);
            _ghostActor = null;
            _ghostName = null;
        }
    }

    private EddyTool? UpdatePick()
    {
        StatusService.AddHints(
            ("LMB", "Pick Art Object")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredId.HasValue)
        {
            if (Level.ArtObjects.TryGetValue(_hoveredId.Value, out var instance))
            {
                AssetBrowser.Pick(instance.Name, AssetType.ArtObject);
                return EddyTool.Paint;
            }
        }

        return null;
    }


    private void UpdateCursor()
    {
        if (Tool.Value is EddyTool.Select or EddyTool.Pick && _hoveredId.HasValue)
        {
            var hoverSurfaces = BuildWireframeForAo(_hoveredId.Value, HoverColor);
            if (hoverSurfaces.HasValue)
            {
                Cursor.SetHoverSurfaces([hoverSurfaces.Value], HoverColor);
            }
        }
        else
        {
            Cursor.ClearHover();
        }

        var selectionSurfaces = _selectedIds
            .Select(id => BuildWireframeForAo(id, SelectionColor))
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        if (selectionSurfaces.Count > 0)
        {
            Cursor.SetSelectionSurfaces(selectionSurfaces, SelectionColor);
        }
        else
        {
            Cursor.ClearSelection();
        }
    }

    private (MeshSurface, PrimitiveType)? BuildWireframeForAo(int id, Color color)
    {
        if (!Level.ArtObjects.ContainsKey(id))
        {
            return null;
        }

        if (!_artObjectActors.TryGetValue(id, out var actor))
        {
            return null;
        }

        if (!actor.TryGetComponent<ArtObjectMesh>(out var mesh))
        {
            return null;
        }

        var box = mesh!.GetBounds().First();
        var size = box.Max - box.Min;
        var center = (box.Min + box.Max) * 0.5f;

        var surface = MeshSurface.CreateWireframeBox(size, color);
        for (var i = 0; i < surface.Vertices.Length; i++)
        {
            surface.Vertices[i] += center;
        }

        return (surface, PrimitiveType.LineList);
    }

    public override void DrawProperties()
    {
        if (_selectedIds.Count == 0)
        {
            base.DrawProperties();
            return;
        }

        var id = _selectedIds.First();
        if (!Level.ArtObjects.TryGetValue(id, out var instance))
        {
            base.DrawProperties();
            return;
        }

        ImGui.Text($"Art Object: {instance.Name} (ID={id})");

        var position = instance.Position.ToXna();
        if (ImGuiX.InputFloat3("Position", ref position))
        {
            using (History.BeginScope("Edit AO Position"))
            {
                instance.Position = position.ToRepacker();
                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = position;
                }
            }
        }

        var rotation = instance.Rotation.ToXna();
        var euler = QuaternionToEuler(rotation);
        if (ImGuiX.DragFloat3("Rotation (Euler)", ref euler, 1f))
        {
            using (History.BeginScope("Edit AO Rotation"))
            {
                var newRotation = EulerToQuaternion(euler);
                instance.Rotation = newRotation.ToRepacker();
                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Rotation = newRotation;
                }
            }
        }

        var scale = instance.Scale.ToXna();
        if (ImGuiX.DragFloat3("Scale", ref scale, 0.01f))
        {
            using (History.BeginScope("Edit AO Scale"))
            {
                instance.Scale = scale.ToRepacker();
                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Scale = scale;
                }
            }
        }

        ImGui.SeparatorText("Actor Settings");

        var settings = instance.ActorSettings;

        var inactive = settings.Inactive;
        if (ImGui.Checkbox("Inactive", ref inactive))
        {
            using (History.BeginScope("Edit AO Inactive"))
            {
                settings.Inactive = inactive;
            }
        }

        var containedTrile = (int)settings.ContainedTrile;
        var actorNames = Enum.GetNames<FEZRepacker.Core.Definitions.Game.Common.ActorType>();
        if (ImGui.Combo("Contained Trile", ref containedTrile, actorNames, actorNames.Length))
        {
            using (History.BeginScope("Edit AO Contained Trile"))
            {
                settings.ContainedTrile = (FEZRepacker.Core.Definitions.Game.Common.ActorType)containedTrile;
            }
        }

        var attachedGroup = settings.AttachedGroup ?? InvalidId;
        if (ImGui.InputInt("Attached Group", ref attachedGroup))
        {
            using (History.BeginScope("Edit AO Attached Group"))
            {
                settings.AttachedGroup = attachedGroup == InvalidId ? null : attachedGroup;
            }
        }

        var spinEvery = settings.SpinEvery;
        if (ImGui.DragFloat("Spin Every", ref spinEvery, 0.1f))
        {
            using (History.BeginScope("Edit AO Spin Every"))
            {
                settings.SpinEvery = spinEvery;
            }
        }

        var spinOffset = settings.SpinOffset;
        if (ImGui.DragFloat("Spin Offset", ref spinOffset, 0.1f))
        {
            using (History.BeginScope("Edit AO Spin Offset"))
            {
                settings.SpinOffset = spinOffset;
            }
        }

        var offCenter = settings.OffCenter;
        if (ImGui.Checkbox("Off Center", ref offCenter))
        {
            using (History.BeginScope("Edit AO Off Center"))
            {
                settings.OffCenter = offCenter;
            }
        }
    }

    private static Vector3 QuaternionToEuler(Quaternion q)
    {
        var sinRCosP = 2f * (q.W * q.X + q.Y * q.Z);
        var cosRCosP = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        var roll = MathF.Atan2(sinRCosP, cosRCosP);

        var sinP = 2f * (q.W * q.Y - q.Z * q.X);
        var pitch = MathF.Abs(sinP) >= 1f ? MathF.CopySign(MathHelper.PiOver2, sinP) : MathF.Asin(sinP);

        var sinYCosP = 2f * (q.W * q.Z + q.X * q.Y);
        var cosYCosP = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        var yaw = MathF.Atan2(sinYCosP, cosYCosP);

        return new Vector3(
            MathHelper.ToDegrees(roll),
            MathHelper.ToDegrees(pitch),
            MathHelper.ToDegrees(yaw));
    }

    private static Quaternion EulerToQuaternion(Vector3 euler)
    {
        return Quaternion.CreateFromYawPitchRoll(
            MathHelper.ToRadians(euler.Y),
            MathHelper.ToRadians(euler.X),
            MathHelper.ToRadians(euler.Z));
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            var presentIds = Level.ArtObjects.Keys.Where(k => k != InvalidId).ToHashSet();

            foreach (var id in _artObjectActors.Keys.ToList())
            {
                if (!presentIds.Contains(id))
                {
                    Scene.DestroyActor(_artObjectActors[id]);
                    _artObjectActors.Remove(id);
                }
            }

            foreach (var (id, instance) in Level.ArtObjects.Where(kv => kv.Key != InvalidId))
            {
                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = instance.Position.ToXna();
                    actor.Transform.Rotation = instance.Rotation.ToXna();
                    actor.Transform.Scale = instance.Scale.ToXna();
                }
                else
                {
                    actor = Scene.CreateActor();
                    actor.Name = $"{id}: {instance.Name}";
                    actor.Transform.Position = instance.Position.ToXna();
                    actor.Transform.Rotation = instance.Rotation.ToXna();
                    actor.Transform.Scale = instance.Scale.ToXna();
                    _artObjectActors[id] = actor;

                    var mesh = actor.AddComponent<ArtObjectMesh>();
                    var ao = (ArtObject)ResourceService.Load($"Art Objects/{instance.Name}");
                    mesh.Visualize(ao);
                }
            }

            _selectedIds.RemoveWhere(id => !Level.ArtObjects.ContainsKey(id));
            if (_hoveredId.HasValue && !Level.ArtObjects.ContainsKey(_hoveredId.Value))
            {
                _hoveredId = null;
            }

            return;
        }

        TeardownVisualization();
        _selectedIds.Clear();
        _hoveredId = null;

        #region ArtObjects

        foreach (var (id, instance) in Level.ArtObjects.Where(kv => kv.Key != InvalidId))
        {
            var actor = Scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            actor.Transform.Rotation = instance.Rotation.ToXna();
            actor.Transform.Scale = instance.Scale.ToXna();
            _artObjectActors[id] = actor;

            var mesh = actor.AddComponent<ArtObjectMesh>();
            var ao = (ArtObject)ResourceService.Load($"Art Objects/{instance.Name}");
            mesh.Visualize(ao);
        }

        #endregion
    }

    private List<ArtObjectInstance> BuildClipboard()
    {
        return _selectedIds
            .Where(id => Level.ArtObjects.ContainsKey(id))
            .Select(id =>
            {
                var src = Level.ArtObjects[id];
                return new ArtObjectInstance
                {
                    Name = src.Name,
                    Position = src.Position,
                    Rotation = src.Rotation,
                    Scale = src.Scale
                };
            })
            .ToList();
    }

    private void PasteClipboard(List<ArtObjectInstance> clipboard)
    {
        _selectedIds.Clear();

        foreach (var src in clipboard)
        {
            var id = NextAvailableId();
            var instance = new ArtObjectInstance
            {
                Name = src.Name,
                Position = src.Position,
                Rotation = src.Rotation,
                Scale = src.Scale
            };
            Level.ArtObjects[id] = instance;

            var actor = Scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            actor.Transform.Rotation = instance.Rotation.ToXna();
            actor.Transform.Scale = instance.Scale.ToXna();
            _artObjectActors[id] = actor;

            var mesh = actor.AddComponent<ArtObjectMesh>();
            var ao = (ArtObject)ResourceService.Load($"Art Objects/{instance.Name}");
            mesh.Visualize(ao);

            _selectedIds.Add(id);
        }
    }

    private int NextAvailableId()
    {
        return Level.ArtObjects.Keys.Where(k => k != InvalidId).DefaultIfEmpty(-1).Max() + 1;
    }

    public override void Dispose()
    {
        _paintScope?.Dispose();
        _translate.Scope?.Dispose();
        _scale.Scope?.Dispose();
        DestroyGhost();
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _artObjectActors.Values)
        {
            Scene.DestroyActor(actor);
        }

        _artObjectActors.Clear();
    }

    private struct TranslateState
    {
        public bool Active;
        public Plane DragPlane;
        public Vector3 InitialHandlePosition;
        public Dictionary<int, Vector3> InitialPositions = [];
        public Vector2 LockAccum;
        public Vector3? LockedAxis;
        public bool AxisLocked;
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
        public bool Active;
        public Dictionary<int, Vector3> InitialScales = [];
        public IDisposable? Scope;

        public ScaleState() { }

        public void Reset()
        {
            Scope?.Dispose();
            this = new ScaleState();
        }
    }
}