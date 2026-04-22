using System.Text.Json;
using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class VolumeContext : BaseContext
{
    private static readonly Color DefaultColor = Color.LimeGreen;

    private readonly Dictionary<int, Actor> _volumeActors = new();

    private int? _hoveredId;

    private readonly HashSet<int> _selectedIds = new();

    private IDisposable? _translateScope;

    private IDisposable? _scaleScope;

    private readonly List<Volume> _clipboard = new();

    public VolumeContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        _hoveredId = null;
        foreach (var actor in _volumeActors.Values)
        {
            var mesh = actor.GetComponent<VolumeMesh>();
            mesh.Color = DefaultColor;
        }

        if (Eddy.Visuals.IsDirty)
        {
            var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.Volumes);
            foreach (var actor in _volumeActors.Values)
            {
                actor.Visible = visible;
                var mesh = actor.GetComponent<VolumeMesh>();
                mesh.Pickable = visible;
            }
        }

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.HasComponent<VolumeMesh>())
        {
            var actor = Eddy.Hit.Value.Actor;
            var foundId = _volumeActors.FirstOrDefault(kv => kv.Value == actor).Key;
            if (_volumeActors.ContainsKey(foundId) && Eddy.Tool is EddyTool.Select or EddyTool.Pick)
            {
                _hoveredId = foundId;
                Eddy.HoveredContext = EddyContext.Volume;
                return;
            }
        }

        if (!_hoveredId.HasValue && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            Eddy.SelectedContext == EddyContext.Volume &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedIds.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedIds.Count > 0)
        {
            Eddy.SelectedContext = EddyContext.Volume;
        }

        if (Eddy.AssetBrowser.Select(AssetType.ArtObject))
        {
            Eddy.Tool = EddyTool.Paint;
            Eddy.SelectedContext = EddyContext.Volume;
        }

        if (Eddy.InstanceBrowser.Select(out var sel) && sel.context == EddyContext.Volume)
        {
            Eddy.InstanceBrowser.Consume();
            var volume = Level.Volumes[sel.id];
            Eddy.FocusOn((volume.From.ToXna() + volume.To.ToXna()) / 2f);
        }
    }

    protected override void Act()
    {
        Eddy.AllowedTools.Add(EddyTool.Select);
        Eddy.AllowedTools.Add(EddyTool.Translate);
        Eddy.AllowedTools.Add(EddyTool.Scale);
        Eddy.AllowedTools.Add(EddyTool.Paint);

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
            case EddyTool.Scale: UpdateScale(); break;
            case EddyTool.Paint: UpdatePaint(); break;
            case EddyTool.Rotate:
            case EddyTool.Pick: break;
            default: throw new ArgumentOutOfRangeException();
        }

        foreach (var (id, actor) in _volumeActors)
        {
            var mesh = actor.GetComponent<VolumeMesh>();
            if (id == _hoveredId)
            {
                mesh.Color = HoverColor;
            }
            if (_selectedIds.Contains(id))
            {
                mesh.Color = SelectionColor;
            }
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
            using (Eddy.History.BeginScope("Delete Volumes"))
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
                using (Eddy.History.BeginScope("Cut Volumes"))
                {
                    RemoveSelected();
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (Eddy.History.BeginScope("Paste Volumes"))
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
            Eddy.SelectedContext = EddyContext.Volume;
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
                if (Level.Volumes.TryGetValue(id, out var instance))
                {
                    instance.From += delta.ToRepacker();
                    instance.To += delta.ToRepacker();
                    if (_volumeActors.TryGetValue(id, out var actor))
                    {
                        var center = (instance.From.ToXna() + instance.To.ToXna()) / 2f;
                        actor.Transform.Position = center;
                    }
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate Volume");
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

        var sum = Vector3.Zero;
        foreach (var id in _selectedIds)
        {
            var instance = Level.Volumes[id];
            var size = instance.To.ToXna() - instance.From.ToXna();
            sum += instance.From.ToXna() + size / 2f;
        }

        return sum / _selectedIds.Count;
    }

    private void UpdateScale()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();
        var firstInstance = Level.Volumes[_selectedIds.First()];

        var primarySize = (firstInstance.To - firstInstance.From).ToXna();
        var previousSize = primarySize;

        if (Eddy.Gizmo.Scale(centroid, ref primarySize))
        {
            var delta = primarySize - previousSize;
            foreach (var id in _selectedIds)
            {
                if (Level.Volumes.TryGetValue(id, out var instance))
                {
                    var center = (instance.From + instance.To).ToXna() / 2f;
                    var newSize = (instance.To - instance.From).ToXna() + delta;
                    instance.From = (center - newSize / 2f).ToRepacker();
                    instance.To = (center + newSize / 2f).ToRepacker();
                    if (_volumeActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Position = center;
                        var mesh = actor.GetComponent<VolumeMesh>();
                        mesh.Size = newSize;
                    }
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _scaleScope?.Dispose();
            _scaleScope = Eddy.History.BeginScope("Scale Volume");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _scaleScope?.Dispose();
            _scaleScope = null;
        }
    }

    private void UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Place Volume")
        );

        TrileEmplacement? hoveredEmp = null;
        FaceOrientation? hoveredFace = null;

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
        {
            var index = Eddy.Hit.Value.Index;
            hoveredEmp = mesh.GetEmplacement(index);

            if (Level.Triles.TryGetValue(hoveredEmp, out var hoveredInstance))
            {
                var box = mesh.GetBounds().ElementAt(index);
                hoveredFace = Mathz.DetermineFace(box, Eddy.Ray, Eddy.Hit.Value.Distance);

                var trileCenter = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
                var origin = trileCenter + hoveredFace.Value.AsVector() * (0.5f + CursorMesh.OverlayOffset);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, hoveredFace.Value);
                Eddy.Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hoveredEmp != null && hoveredFace != null)
        {
            using (Eddy.History.BeginScope("Place Volume"))
            {
                var id = NextAvailableId();
                var position = new Vector3(hoveredEmp.X, hoveredEmp.Y, hoveredEmp.Z) + hoveredFace.Value.AsVector();
                Level.Volumes[id] = new Volume
                {
                    From = position.ToRepacker(),
                    To = (position + Vector3.One).ToRepacker(),
                    Orientations = [],
                    ActorSettings = new VolumeActorSettings()
                };
            }
        }
    }

    public override void DrawOverlay()
    {
        if (Eddy.Tool != EddyTool.Paint || !Eddy.IsViewportHovered || Eddy.SelectedContext != EddyContext.Volume)
        {
            return;
        }

        var mousePos = ImGui.GetMousePos();
        var drawPos = mousePos + new NVector2(12f, 12f);

        const string text = $"{Lucide.SquareDashed} Volume";
        var padding = new NVector2(4f, 2f);
        var dl = ImGui.GetForegroundDrawList(ImGui.GetMainViewport());
        dl.AddRectFilled(drawPos - padding, drawPos + ImGui.CalcTextSize(text) + padding, ImGui.GetColorU32(ImGuiCol.PopupBg));
        dl.AddText(drawPos, ImGui.GetColorU32(ImGuiCol.Text), text);
    }

    public override void DrawProperties()
    {
        if (Eddy.SelectedContext != EddyContext.Volume || _selectedIds.Count == 0)
        {
            return;
        }

        var id = _selectedIds.First();
        if (!Level.Volumes.TryGetValue(id, out var instance))
        {
            return;
        }

        ImGui.Text($"Volume (ID={id})");

        var from = instance.From.ToXna();
        if (ImGuiX.InputFloat3("From", ref from))
        {
            using (Eddy.History.BeginScope("Edit From"))
            {
                instance.From = from.ToRepacker();
            }
        }

        var to = instance.To.ToXna();
        if (ImGuiX.InputFloat3("To", ref to))
        {
            using (Eddy.History.BeginScope("Edit To"))
            {
                instance.To = to.ToRepacker();
            }
        }

        var orientations = instance.Orientations;
        if (ImGuiX.EditableArray("Orientations", ref orientations, RenderFace))
        {
            using (Eddy.History.BeginScope("Edit Orientations"))
            {
                instance.Orientations = orientations;
            }
        }

        ImGui.SeparatorText("Actor Settings");

        var settings = instance.ActorSettings;

        var farawayPlaneOffset = settings.FarawayPlaneOffset.ToXna();
        if (ImGuiX.InputFloat2("Faraway Plane Offset", ref farawayPlaneOffset))
        {
            using (Eddy.History.BeginScope("Edit Faraway Plane Offset"))
            {
                settings.FarawayPlaneOffset = farawayPlaneOffset.ToRepacker();
            }
        }

        var isPointOfInterest = settings.IsPointOfInterest;
        if (ImGui.Checkbox("Is Point Of Interest", ref isPointOfInterest))
        {
            using (Eddy.History.BeginScope("Edit Is Point Of Interest"))
            {
                settings.IsPointOfInterest = isPointOfInterest;
            }
        }

        var dotDialogue = settings.DotDialogue;
        if (ImGuiX.EditableList("Dot Dialogue", ref dotDialogue, RenderDotDialog, () => new DotDialogueLine()))
        {
            using (Eddy.History.BeginScope("Edit Dot Dialogue"))
            {
                settings.DotDialogue = dotDialogue;
            }
        }

        var waterLocked = settings.WaterLocked;
        if (ImGui.Checkbox("Water Locked", ref waterLocked))
        {
            using (Eddy.History.BeginScope("Edit Water Locked"))
            {
                settings.WaterLocked = waterLocked;
            }
        }

        var codePattern = settings.CodePattern;
        if (ImGuiX.EditableArray("Code Pattern", ref codePattern, RenderCodePattern))
        {
            using (Eddy.History.BeginScope("Edit Code Pattern"))
            {
                settings.CodePattern = codePattern;
            }
        }

        var isBlackHole = settings.IsBlackHole;
        if (ImGui.Checkbox("Is Black Hole", ref isBlackHole))
        {
            using (Eddy.History.BeginScope("Edit Is Black Hole"))
            {
                settings.IsBlackHole = isBlackHole;
            }
        }

        var needsTrigger = settings.NeedsTrigger;
        if (ImGui.Checkbox("Needs Trigger", ref needsTrigger))
        {
            using (Eddy.History.BeginScope("Edit Needs Trigger"))
            {
                settings.NeedsTrigger = needsTrigger;
            }
        }

        var isSecretPassage = settings.IsSecretPassage;
        if (ImGui.Checkbox("Is Secret Passage", ref isSecretPassage))
        {
            using (Eddy.History.BeginScope("Edit Is Secret Passage"))
            {
                settings.IsSecretPassage = isSecretPassage;
            }
        }
    }

    private static bool RenderFace(int index, ref FaceOrientation item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();

        var face = (int)item;
        var faces = Enum.GetNames<FaceOrientation>();

        var edited = ImGui.Combo("##fo" + index, ref face, faces, faces.Length);
        item = (FaceOrientation)face;
        return edited;
    }

    private static bool RenderDotDialog(int index, ref DotDialogueLine item)
    {
        ImGui.TextDisabled(index + ":");

        var resourceText = item.ResourceText;
        if (ImGui.InputText("Resource Text##dd1" + index, ref resourceText, 255))
        {
            item.ResourceText = resourceText;
            return true;
        }

        var grouped = item.Grouped;
        if (ImGui.Checkbox("Grouped##dd2" + index, ref grouped))
        {
            item.Grouped = grouped;
            return true;
        }

        return false;
    }

    private static bool RenderCodePattern(int index, ref CodeInput item)
    {
        ImGui.TextDisabled(index + ":");
        ImGui.SameLine();

        var input = (int)item;
        var inputs = Enum.GetNames<CodeInput>();

        var edited = ImGui.Combo("##cp" + index, ref input, inputs, inputs.Length);
        item = (CodeInput)input;
        return edited;
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.SelectedContext != EddyContext.Volume && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Volumes

        foreach (var (id, instance) in Level.Volumes.Where(kv => kv.Key != InvalidId))
        {
            var actor = CreateSubActor();
            actor.Name = $"{id}: Volume";
            actor.Transform.Position = (instance.From + (instance.To - instance.From) / 2f).ToXna();
            _volumeActors[id] = actor;

            var mesh = actor.AddComponent<VolumeMesh>();
            mesh.Size = (instance.To - instance.From).ToXna();
            mesh.Color = DefaultColor;
        }

        #endregion
    }

    private void RemoveSelected()
    {
        foreach (var id in _selectedIds)
        {
            Level.Volumes.Remove(id);
            if (_volumeActors.TryGetValue(id, out var actor))
            {
                Eddy.Scene.DestroyActor(actor);
                _volumeActors.Remove(id);
            }
        }

        _selectedIds.Clear();
    }

    private void BuildClipboard()
    {
        _clipboard.Clear();
        foreach (var id in _selectedIds)
        {
            var instance = Level.Volumes[id];

            // Deep copy
            var settingsJson = JsonSerializer.Serialize(instance.ActorSettings);
            var settings = JsonSerializer.Deserialize<VolumeActorSettings>(settingsJson)!;

            _clipboard.Add(new Volume
            {
                Orientations = instance.Orientations.ToArray(),
                From = instance.From,
                To = instance.To,
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
            Level.Volumes[id] = instance;

            var actor = CreateSubActor();
            actor.Name = $"{id}: Volume";
            actor.Transform.Position = (instance.From + (instance.To - instance.From) / 2f).ToXna();
            _volumeActors[id] = actor;

            var mesh = actor.AddComponent<VolumeMesh>();
            mesh.Color = DefaultColor;
            _selectedIds.Add(id);
        }
    }

    private int NextAvailableId()
    {
        return Level.Volumes.Keys.Where(k => k != InvalidId).DefaultIfEmpty(-1).Max() + 1;
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Volume;
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
        foreach (var actor in _volumeActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _volumeActors.Clear();
    }
}