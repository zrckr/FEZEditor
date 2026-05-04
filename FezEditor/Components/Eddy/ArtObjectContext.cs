using System.Text.Json;
using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class ArtObjectContext : BaseContext
{
    private readonly Dictionary<int, Actor> _artObjectActors = new();

    private int? _hoveredId;

    private readonly HashSet<int> _selectedIds = new();

    private IDisposable? _translateScope;

    private IDisposable? _scaleScope;

    private readonly List<ArtObjectInstance> _clipboard = new();

    public ArtObjectContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        _hoveredId = null;
        if (Eddy.Visuals.IsDirty)
        {
            var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.ArtObjects);
            foreach (var actor in _artObjectActors.Values)
            {
                actor.Visible = visible;
                var mesh = actor.GetComponent<ArtObjectMesh>();
                mesh.Pickable = visible;
            }
        }

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.HasComponent<ArtObjectMesh>())
        {
            var actor = Eddy.Hit.Value.Actor;
            var foundId = _artObjectActors.FirstOrDefault(kv => kv.Value == actor).Key;
            if (_artObjectActors.ContainsKey(foundId) && Eddy.Tool is EddyTool.Select or EddyTool.Pick)
            {
                _hoveredId = foundId;
                Eddy.HoveredContext = EddyContext.ArtObject;
                return;
            }
        }

        if (!_hoveredId.HasValue && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            Eddy.SelectedContext == EddyContext.ArtObject &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedIds.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedIds.Count > 0)
        {
            Eddy.SelectedContext = EddyContext.ArtObject;
        }

        if (Eddy.AssetBrowser.Select(AssetType.ArtObject))
        {
            Eddy.Tool = EddyTool.Paint;
            Eddy.SelectedContext = EddyContext.ArtObject;
        }

        if (Eddy.InstanceBrowser.Select(out var sel) && sel.context == EddyContext.ArtObject)
        {
            Eddy.InstanceBrowser.Consume();
            Eddy.FocusOn(Level.ArtObjects[sel.id].Position.ToXna());
        }
    }

    protected override void Act()
    {
        Eddy.AllowedTools.UnionWith(Enum.GetValues<EddyTool>());

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selectedIds.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        switch (Eddy.Tool)
        {
            case EddyTool.Select: UpdateSelect(); break;
            case EddyTool.Translate: UpdateTranslate(); break;
            case EddyTool.Rotate: UpdateRotate(); break;
            case EddyTool.Scale: UpdateScale(); break;
            case EddyTool.Paint: UpdatePaint(); break;
            case EddyTool.Pick: UpdatePick(); break;
            default: throw new ArgumentOutOfRangeException();
        }

        if (_hoveredId.HasValue)
        {
            var hoverSurfaces = BuildWireframeForAo(_hoveredId.Value, HoverColor);
            if (hoverSurfaces.HasValue)
            {
                Eddy.Cursor.SetHoverSurfaces([hoverSurfaces.Value], HoverColor);
            }
        }

        var selectionSurfaces = _selectedIds
            .Select(id => BuildWireframeForAo(id, SelectionColor))
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        if (selectionSurfaces.Count > 0)
        {
            Eddy.Cursor.SetSelectionSurfaces(selectionSurfaces, SelectionColor);
        }
    }

    private void UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
            ("Shift+LMB", "Add to Selection"),
            ("Alt+LMB", "Select Overlapped")
        );

        if (_selectedIds.Count > 0)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );
        }

        if (_clipboard.Count > 0)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (Eddy.History.BeginScope("Delete Art Objects"))
            {
                RemoveSelected();
            }
        }

        if (ImGui.GetIO().KeyCtrl)
        {
            if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                BuildClipboard();
            }

            if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                BuildClipboard();
                using (Eddy.History.BeginScope("Cut Art Objects"))
                {
                    RemoveSelected();
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (Eddy.History.BeginScope("Paste Art Objects"))
                {
                    PasteClipboard();
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredId.HasValue)
        {
            if (!ImGui.GetIO().KeyShift)
            {
                _selectedIds.Clear();
            }

            _selectedIds.Add(_hoveredId.Value);
            Eddy.SelectedContext = EddyContext.ArtObject;
        }
    }

    private void UpdateTranslate()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();
        if (Eddy.Gizmo.Translate(ref centroid))
        {
            var delta = centroid - ComputeSelectionCentroid();
            foreach (var id in _selectedIds)
            {
                if (Level.ArtObjects.TryGetValue(id, out var instance))
                {
                    var position = instance.Position.ToXna() + delta;
                    instance.Position = position.ToRepacker();
                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Position = position;
                    }
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate Art Object");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _translateScope?.Dispose();
            _translateScope = null;
        }
    }

    private Vector3 ComputeSelectionCentroid()
    {
        if (_selectedIds.Count == 0)
        {
            return Vector3.Zero;
        }

        var sum = _selectedIds
            .Select(id => Level.ArtObjects[id])
            .Select(instance => instance.Position.ToXna())
            .Aggregate(Vector3.Zero, (current, position) => current + position);

        return sum / _selectedIds.Count;
    }

    private void UpdateRotate()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();

        if (Eddy.Gizmo.Rotate(centroid))
        {
            using (Eddy.History.BeginScope("Rotate Art Object(s)"))
            {
                var step = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
                foreach (var id in _selectedIds)
                {
                    var instance = Level.ArtObjects[id];
                    var newRotation = step * instance.Rotation.ToXna();
                    instance.Rotation = newRotation.ToRepacker();
                    if (_artObjectActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Rotation = newRotation;
                    }
                }
            }
        }
    }

    private void UpdateScale()
    {
        StatusService.AddHints(
            ("R", "Reset")
        );

        if (_selectedIds.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();
        var firstInstance = Level.ArtObjects[_selectedIds.First()];
        var primaryScale = firstInstance.Scale.ToXna();
        var previousScale = primaryScale;

        if (Eddy.Gizmo.Scale(centroid, ref primaryScale))
        {
            var delta = primaryScale - previousScale;
            foreach (var id in _selectedIds)
            {
                var instance = Level.ArtObjects[id];
                var newScale = instance.Scale.ToXna() + delta;
                instance.Scale = newScale.ToRepacker();
                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Scale = newScale;
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _scaleScope?.Dispose();
            _scaleScope = Eddy.History.BeginScope("Scale Art Object");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _scaleScope?.Dispose();
            _scaleScope = null;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedIds.Count > 0 && _scaleScope == null)
        {
            using (Eddy.History.BeginScope("Reset Art Object Scale"))
            {
                foreach (var id in _selectedIds)
                {
                    if (Level.ArtObjects.TryGetValue(id, out var instance))
                    {
                        instance.Scale = RVector3.One;
                        if (_artObjectActors.TryGetValue(id, out var actor))
                        {
                            actor.Transform.Scale = Vector3.One;
                        }
                    }
                }
            }
        }
    }

    public override void DrawOverlay()
    {
        if (Eddy.Tool != EddyTool.Paint || !Eddy.IsViewportHovered || Eddy.SelectedContext != EddyContext.ArtObject)
        {
            return;
        }

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.ArtObject);
        if (string.IsNullOrEmpty(entry))
        {
            return;
        }

        var thumb = Eddy.AssetBrowser.GetThumbnail(AssetType.ArtObject, entry);
        if (thumb != null)
        {
            var mousePos = ImGui.GetMousePos();
            var drawMin = mousePos + new NVector2(12f, 12f);
            var drawMax = drawMin + new NVector2(32f, 32f);
            ImGui.GetForegroundDrawList(ImGui.GetMainViewport()).AddImage(ImGuiX.Bind(thumb), drawMin, drawMax);
        }
    }

    private void UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Place")
        );

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.ArtObject);

        // Highlight hovered emplacement for placement preview
        TrileEmplacement? hoveredEmp = null;
        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
        {
            var index = Eddy.Hit.Value.Index;
            hoveredEmp = mesh.GetEmplacement(index);
            if (Level.Triles.TryGetValue(hoveredEmp, out var hoveredInstance))
            {
                var box = mesh.GetBounds().ElementAt(index);
                var face = Mathz.DetermineFace(box, Eddy.Ray, Eddy.Hit.Value.Distance);
                var trileCenter = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
                var origin = trileCenter + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
                Eddy.Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hoveredEmp != null && !string.IsNullOrEmpty(entry))
        {
            using (Eddy.History.BeginScope("Place Art Object"))
            {
                var id = NextAvailableId();
                var position = new Vector3(hoveredEmp.X, hoveredEmp.Y, hoveredEmp.Z);
                Level.ArtObjects[id] = new ArtObjectInstance
                {
                    Name = entry,
                    Position = position.ToRepacker(),
                    Rotation = RQuaternion.Identity,
                    Scale = RVector3.One
                };
            }
        }
    }

    private void UpdatePick()
    {
        StatusService.AddHints(
            ("LMB", "Pick Art Object")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredId.HasValue)
        {
            if (Level.ArtObjects.TryGetValue(_hoveredId.Value, out var instance))
            {
                Eddy.AssetBrowser.Pick(instance.Name, AssetType.ArtObject);
                Eddy.Tool = EddyTool.Paint;
            }
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
        if (Eddy.SelectedContext != EddyContext.ArtObject || _selectedIds.Count == 0)
        {
            return;
        }

        var id = _selectedIds.First();
        if (!Level.ArtObjects.TryGetValue(id, out var instance))
        {
            return;
        }

        ImGui.Text($"Art Object: {instance.Name} (ID={id})");

        var position = instance.Position.ToXna();
        if (ImGuiX.InputFloat3("Position", ref position))
        {
            using (Eddy.History.BeginScope("Edit AO Position"))
            {
                instance.Position = position.ToRepacker();
                if (_artObjectActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = position;
                }
            }
        }

        var rotation = instance.Rotation.ToXna();
        var euler = rotation.ToEuler();
        if (ImGuiX.DragFloat3("Rotation (Euler)", ref euler, 1f))
        {
            using (Eddy.History.BeginScope("Edit AO Rotation"))
            {
                var newRotation = euler.FromEuler();
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
            using (Eddy.History.BeginScope("Edit AO Scale"))
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
            using (Eddy.History.BeginScope("Edit AO Inactive"))
            {
                settings.Inactive = inactive;
            }
        }

        var containedTrile = (int)settings.ContainedTrile;
        var actorNames = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Contained Trile", ref containedTrile, actorNames, actorNames.Length))
        {
            using (Eddy.History.BeginScope("Edit AO Contained Trile"))
            {
                settings.ContainedTrile = (ActorType)containedTrile;
            }
        }

        var attachedGroup = settings.AttachedGroup ?? InvalidId;
        if (ImGui.InputInt("Attached Group", ref attachedGroup))
        {
            using (Eddy.History.BeginScope("Edit AO Attached Group"))
            {
                settings.AttachedGroup = attachedGroup == InvalidId ? null : attachedGroup;
            }
        }

        var spinEvery = settings.SpinEvery;
        if (ImGui.DragFloat("Spin Every", ref spinEvery, 0.1f))
        {
            using (Eddy.History.BeginScope("Edit AO Spin Every"))
            {
                settings.SpinEvery = spinEvery;
            }
        }

        var spinOffset = settings.SpinOffset;
        if (ImGui.DragFloat("Spin Offset", ref spinOffset, 0.1f))
        {
            using (Eddy.History.BeginScope("Edit AO Spin Offset"))
            {
                settings.SpinOffset = spinOffset;
            }
        }

        var offCenter = settings.OffCenter;
        if (ImGui.Checkbox("Off Center", ref offCenter))
        {
            using (Eddy.History.BeginScope("Edit AO Off Center"))
            {
                settings.OffCenter = offCenter;
            }
        }

        var spinView = (int)settings.SpinView;
        var viewpoints = Enum.GetNames<Viewpoint>();
        if (ImGui.Combo("Spin View", ref spinView, viewpoints, viewpoints.Length))
        {
            using (Eddy.History.BeginScope("Edit AO Spin View"))
            {
                settings.SpinView = (Viewpoint)spinView;
            }
        }

        var rotationCenter = settings.RotationCenter.ToXna();
        if (ImGuiX.DragFloat3("Rotation Center", ref rotationCenter, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit AO Rotation Center"))
            {
                settings.RotationCenter = rotationCenter.ToRepacker();
            }
        }

        var nextNode = settings.NextNode ?? InvalidId;
        if (ImGui.InputInt("Next Node", ref nextNode))
        {
            using (Eddy.History.BeginScope("Edit AO Next Node"))
            {
                settings.NextNode = nextNode == InvalidId ? null : nextNode;
            }
        }

        var destinationLevel = settings.DestinationLevel;
        if (ImGui.InputText("Destination Level", ref destinationLevel, 255))
        {
            using (Eddy.History.BeginScope("Edit AO Destination Level"))
            {
                settings.DestinationLevel = destinationLevel;
            }
        }

        var treasureMapName = settings.TreasureMapName;
        if (ImGui.InputText("Treasure Map Name", ref treasureMapName, 255))
        {
            using (Eddy.History.BeginScope("Edit AO Treasure Map Name"))
            {
                settings.TreasureMapName = treasureMapName;
            }
        }

        var timeswitchWindBackSpeed = settings.TimeswitchWindBackSpeed;
        if (ImGui.DragFloat("Timeswitch Wind Back Speed", ref timeswitchWindBackSpeed, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit AO Timeswitch Wind Back Speed"))
            {
                settings.TimeswitchWindBackSpeed = timeswitchWindBackSpeed;
            }
        }

        var vibrationPattern = settings.VibrationPattern.ToList();
        if (ImGuiX.EditableList("Vibration Pattern", ref vibrationPattern, RenderVibrationMotorItem,
                () => VibrationMotor.None))
        {
            using (Eddy.History.BeginScope("Edit AO Vibration Pattern"))
            {
                settings.VibrationPattern =
                    vibrationPattern.ToArray(); // I don't know why FEZRepacker made this as array
            }
        }

        var codePattern = settings.CodePattern.ToList();
        if (ImGuiX.EditableList("Code Pattern", ref codePattern, RenderCodeInputItem, () => CodeInput.None))
        {
            using (Eddy.History.BeginScope("Edit AO Code Pattern"))
            {
                settings.CodePattern = codePattern.ToArray(); // Ditto
            }
        }

        var invisibleSides = settings.InvisibleSides.ToList();
        if (ImGuiX.EditableList("Invisible Sides", ref invisibleSides, RenderFaceOrientationItem,
                () => FaceOrientation.Front))
        {
            using (Eddy.History.BeginScope("Edit AO Invisible Sides"))
            {
                settings.InvisibleSides = invisibleSides.Distinct().ToArray(); // FEZRepacker should use HashSet here
            }
        }

        ImGui.SeparatorText("Segment");
        {
            // Destination is recalculated at runtime by MovingGroupsHost from world-space AO positions
            // Editing it here has no effect.
            //
            // var destination = segment.Destination.ToXna();
            // if (ImGuiX.DragFloat3("Destination", ref destination, 0.01f))
            // {
            //     using (Eddy.History.BeginScope("Edit AO Segment Destination"))
            //     {
            //         segment.Destination = destination.ToRepacker();
            //     }
            // }

            var duration = settings.Segment.Duration;
            if (ImGuiX.TimeSpanInput("Duration", ref duration))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Duration"))
                {
                    settings.Segment.Duration = duration;
                }
            }

            var waitTimeOnStart = settings.Segment.WaitTimeOnStart;
            if (ImGuiX.TimeSpanInput("Wait Time On Start", ref waitTimeOnStart))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Wait Time On Start"))
                {
                    settings.Segment.WaitTimeOnStart = waitTimeOnStart;
                }
            }

            var waitTimeOnFinish = settings.Segment.WaitTimeOnFinish;
            if (ImGuiX.TimeSpanInput("Wait Time On Finish", ref waitTimeOnFinish))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Wait Time On Finish"))
                {
                    settings.Segment.WaitTimeOnFinish = waitTimeOnFinish;
                }
            }

            var acceleration = settings.Segment.Acceleration;
            if (ImGui.DragFloat("Acceleration", ref acceleration, 0.01f))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Acceleration"))
                {
                    settings.Segment.Acceleration = acceleration;
                }
            }

            var deceleration = settings.Segment.Deceleration;
            if (ImGui.DragFloat("Deceleration", ref deceleration, 0.01f))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Deceleration"))
                {
                    settings.Segment.Deceleration = deceleration;
                }
            }

            var jitterFactor = settings.Segment.JitterFactor;
            if (ImGui.DragFloat("Jitter Factor", ref jitterFactor, 0.01f))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Jitter Factor"))
                {
                    settings.Segment.JitterFactor = jitterFactor;
                }
            }

            // Orientation is not used by MovingGroupsHost for connective rail segments.
            //
            // var orientation = segment.Orientation.ToXna();
            // var orientationEuler = orientation.ToEuler();
            // if (ImGuiX.DragFloat3("Orientation (Euler)", ref orientationEuler, 1f))
            // {
            //     using (Eddy.History.BeginScope("Edit AO Segment Orientation"))
            //     {
            //         segment.Orientation = orientationEuler.FromEuler().ToRepacker();
            //     }
            // }

            var hasCustomData = settings.Segment.CustomData != null;
            if (ImGui.Checkbox("Custom Camera Data", ref hasCustomData))
            {
                using (Eddy.History.BeginScope("Edit AO Segment Custom Data"))
                {
                    settings.Segment.CustomData = hasCustomData ? new CameraNodeData() : null;
                }
            }

            if (settings.Segment.CustomData is { } customData)
            {
                var perspective = customData.Perspective;
                if (ImGui.Checkbox("Perspective##cd", ref perspective))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Custom Data Perspective"))
                    {
                        customData.Perspective = perspective;
                    }
                }

                var pixelsPerTrixel = customData.PixelsPerTrixel;
                if (ImGui.InputInt("Pixels Per Trixel##cd", ref pixelsPerTrixel))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Custom Data Pixels Per Trixel"))
                    {
                        customData.PixelsPerTrixel = pixelsPerTrixel;
                    }
                }

                var soundName = customData.SoundName;
                if (ImGui.InputText("Sound Name##cd", ref soundName, 255))
                {
                    using (Eddy.History.BeginScope("Edit AO Segment Custom Data Sound Name"))
                    {
                        customData.SoundName = soundName;
                    }
                }
            }
        }
    }

    private static bool RenderVibrationMotorItem(int index, ref VibrationMotor item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();
        var motor = (int)item;
        var motors = Enum.GetNames<VibrationMotor>();
        var edited = ImGui.Combo("##vm" + index, ref motor, motors, motors.Length);
        item = (VibrationMotor)motor;
        return edited;
    }

    private static bool RenderCodeInputItem(int index, ref CodeInput item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();
        var input = (int)item;
        var inputs = Enum.GetNames<CodeInput>();
        var edited = ImGui.Combo("##ci" + index, ref input, inputs, inputs.Length);
        item = (CodeInput)input;
        return edited;
    }

    private static bool RenderFaceOrientationItem(int index, ref FaceOrientation item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();
        var face = (int)item;
        var faces = Enum.GetNames<FaceOrientation>();
        var edited = ImGui.Combo("##fo" + index, ref face, faces, faces.Length);
        item = (FaceOrientation)face;
        return edited;
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            if (Eddy.SelectedContext != EddyContext.ArtObject)
            {
                return;
            }

            var presentIds = Level.ArtObjects.Keys.Where(k => k != InvalidId).ToHashSet();

            foreach (var id in _artObjectActors.Keys.ToList())
            {
                if (!presentIds.Contains(id))
                {
                    Eddy.Scene.DestroyActor(_artObjectActors[id]);
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
                    actor = CreateSubActor();
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
            var actor = CreateSubActor();
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

    private void RemoveSelected()
    {
        foreach (var id in _selectedIds)
        {
            Level.ArtObjects.Remove(id);
            if (_artObjectActors.TryGetValue(id, out var actor))
            {
                Eddy.Scene.DestroyActor(actor);
                _artObjectActors.Remove(id);
            }
        }

        _selectedIds.Clear();
    }

    private void BuildClipboard()
    {
        _clipboard.Clear();
        foreach (var id in _selectedIds)
        {
            var instance = Level.ArtObjects[id];

            // Deep copy
            var settingsJson = JsonSerializer.Serialize(instance.ActorSettings);
            var settings = JsonSerializer.Deserialize<ArtObjectActorSettings>(settingsJson)!;

            _clipboard.Add(new ArtObjectInstance
            {
                Name = instance.Name,
                Position = instance.Position,
                Rotation = instance.Rotation,
                Scale = instance.Scale,
                ActorSettings = settings
            });
        }
    }

    private void PasteClipboard()
    {
        _selectedIds.Clear();
        foreach (var instance in _clipboard)
        {
            var id = NextAvailableId();
            Level.ArtObjects[id] = instance;

            var actor = CreateSubActor();
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

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.ArtObject;
    }

    public override void Dispose()
    {
        _translateScope?.Dispose();
        _scaleScope?.Dispose();
        TeardownVisualization();
        base.Dispose();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _artObjectActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _artObjectActors.Clear();
    }
}