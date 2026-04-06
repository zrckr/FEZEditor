using System.Text.Json;
using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
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
        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.HasComponent<ArtObjectMesh>())
        {
            var actor = Eddy.Hit.Value.Actor;
            var foundId = _artObjectActors.FirstOrDefault(kv => kv.Value == actor).Key;
            if (_artObjectActors.ContainsKey(foundId) && Eddy.Tool is EddyTool.Select or EddyTool.Pick)
            {
                _hoveredId = foundId;
                Eddy.Context = EddyContext.ArtObject;
                return;
            }
        }

        if (!_hoveredId.HasValue && !Eddy.Gizmo.IsActive &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedIds.Clear();
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedIds.Count > 0)
        {
            Eddy.Context = EddyContext.ArtObject;
        }
    }

    protected override void Act()
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selectedIds.Clear();
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

    private void UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Place")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            // TODO: Implement this again =/
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
        if (Eddy.Context != EddyContext.ArtObject || _selectedIds.Count == 0)
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
        var euler = QuaternionToEuler(rotation);
        if (ImGuiX.DragFloat3("Rotation (Euler)", ref euler, 1f))
        {
            using (Eddy.History.BeginScope("Edit AO Rotation"))
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
        var actorNames = Enum.GetNames<FEZRepacker.Core.Definitions.Game.Common.ActorType>();
        if (ImGui.Combo("Contained Trile", ref containedTrile, actorNames, actorNames.Length))
        {
            using (Eddy.History.BeginScope("Edit AO Contained Trile"))
            {
                settings.ContainedTrile = (FEZRepacker.Core.Definitions.Game.Common.ActorType)containedTrile;
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
            if (Eddy.Context != EddyContext.ArtObject)
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
                    actor = Eddy.Scene.CreateActor();
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
            var actor = Eddy.Scene.CreateActor();
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

            var actor = Eddy.Scene.CreateActor();
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