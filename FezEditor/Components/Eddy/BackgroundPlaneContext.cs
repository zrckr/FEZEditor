using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class BackgroundPlaneContext : BaseContext
{
    private readonly Dictionary<int, Actor> _bgPlaneActors = new();

    private int? _hoveredId;

    private readonly HashSet<int> _selectedIds = new();

    private IDisposable? _translateScope;

    private IDisposable? _scaleScope;

    private readonly List<BackgroundPlane> _clipboard = new();

    public BackgroundPlaneContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        _hoveredId = null;
        if (Eddy.Visuals.IsDirty)
        {
            var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.BackgroundPlanes);
            foreach (var actor in _bgPlaneActors.Values)
            {
                actor.Visible = visible;
                var mesh = actor.GetComponent<BackgroundPlaneMesh>();
                mesh.Pickable = visible;
            }
        }

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.HasComponent<BackgroundPlaneMesh>())
        {
            var actor = Eddy.Hit.Value.Actor;
            var foundId = _bgPlaneActors.FirstOrDefault(kv => kv.Value == actor).Key;
            if (_bgPlaneActors.ContainsKey(foundId) && Eddy.Tool is EddyTool.Select or EddyTool.Pick)
            {
                _hoveredId = foundId;
                Eddy.HoveredContext = EddyContext.BackgroundPlane;
                return;
            }
        }

        if (!_hoveredId.HasValue && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedIds.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedIds.Count > 0)
        {
            Eddy.SelectedContext = EddyContext.BackgroundPlane;
        }

        if (Eddy.AssetBrowser.WasSelected(AssetType.BackgroundPlane))
        {
            Eddy.Tool = EddyTool.Paint;
            Eddy.SelectedContext = EddyContext.BackgroundPlane;
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
            var hoverSurfaces = BuildWireframeForBg(_hoveredId.Value, HoverColor);
            if (hoverSurfaces.HasValue)
            {
                Eddy.Cursor.SetHoverSurfaces([hoverSurfaces.Value], HoverColor);
            }
        }

        var selectionSurfaces = _selectedIds
            .Select(id => BuildWireframeForBg(id, SelectionColor))
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
            using (Eddy.History.BeginScope("Delete Background Planes"))
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
                using (Eddy.History.BeginScope("Cut Background Planes"))
                {
                    RemoveSelected();
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (Eddy.History.BeginScope("Paste Background Planes"))
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
                if (Level.BackgroundPlanes.TryGetValue(id, out var instance))
                {
                    var position = instance.Position.ToXna() + delta;
                    instance.Position = position.ToRepacker();
                    if (_bgPlaneActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Position = position;
                    }
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate Background Plane");
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
            .Select(id => Level.BackgroundPlanes[id])
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
            using (Eddy.History.BeginScope("Rotate Background Plane(s)"))
            {
                var step = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathHelper.PiOver2);
                foreach (var id in _selectedIds)
                {
                    var instance = Level.BackgroundPlanes[id];
                    var newRotation = step * instance.Rotation.ToXna();
                    instance.Rotation = newRotation.ToRepacker();
                    if (_bgPlaneActors.TryGetValue(id, out var actor))
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
        var firstInstance = Level.BackgroundPlanes[_selectedIds.First()];
        var primaryScale = firstInstance.Scale.ToXna();
        var previousScale = primaryScale;

        if (Eddy.Gizmo.Scale(centroid, ref primaryScale))
        {
            var delta = primaryScale - previousScale;
            foreach (var id in _selectedIds)
            {
                var instance = Level.BackgroundPlanes[id];
                var newScale = instance.Scale.ToXna() + delta;
                instance.Scale = newScale.ToRepacker();
                if (_bgPlaneActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Scale = newScale;
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _scaleScope?.Dispose();
            _scaleScope = Eddy.History.BeginScope("Scale Background Plane");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _scaleScope?.Dispose();
            _scaleScope = null;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.R) && _selectedIds.Count > 0 && _scaleScope == null)
        {
            using (Eddy.History.BeginScope("Reset Background Plane Scale"))
            {
                foreach (var id in _selectedIds)
                {
                    if (Level.BackgroundPlanes.TryGetValue(id, out var instance))
                    {
                        instance.Scale = RVector3.One;
                        if (_bgPlaneActors.TryGetValue(id, out var actor))
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
        if (Eddy.Tool != EddyTool.Paint || !Eddy.IsViewportHovered ||
            Eddy.SelectedContext != EddyContext.BackgroundPlane)
        {
            return;
        }

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.BackgroundPlane);
        if (string.IsNullOrEmpty(entry))
        {
            return;
        }

        var thumb = Eddy.AssetBrowser.GetThumbnail(AssetType.BackgroundPlane, entry);
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

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.BackgroundPlane);

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
            using (Eddy.History.BeginScope("Place Background Plane"))
            {
                var id = NextAvailableId();
                var position = new Vector3(hoveredEmp.X, hoveredEmp.Y, hoveredEmp.Z);
                Level.BackgroundPlanes[id] = new BackgroundPlane
                {
                    TextureName = entry,
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
            ("LMB", "Pick Background Plane")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredId.HasValue)
        {
            if (Level.BackgroundPlanes.TryGetValue(_hoveredId.Value, out var instance))
            {
                Eddy.AssetBrowser.Pick(instance.TextureName, AssetType.BackgroundPlane);
                Eddy.Tool = EddyTool.Paint;
            }
        }
    }

    private (MeshSurface, PrimitiveType)? BuildWireframeForBg(int id, Color color)
    {
        if (!Level.BackgroundPlanes.ContainsKey(id))
        {
            return null;
        }

        if (!_bgPlaneActors.TryGetValue(id, out var actor))
        {
            return null;
        }

        if (!actor.TryGetComponent<BackgroundPlaneMesh>(out var mesh))
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
        if (Eddy.SelectedContext != EddyContext.BackgroundPlane || _selectedIds.Count == 0)
        {
            return;
        }

        var id = _selectedIds.First();
        if (!Level.BackgroundPlanes.TryGetValue(id, out var instance))
        {
            return;
        }

        ImGui.Text($"Background Plane: {instance.TextureName} (ID={id})");

        var position = instance.Position.ToXna();
        if (ImGuiX.InputFloat3("Position", ref position))
        {
            using (Eddy.History.BeginScope("Edit BG Position"))
            {
                instance.Position = position.ToRepacker();
                if (_bgPlaneActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = position;
                }
            }
        }

        var rotation = instance.Rotation.ToXna();
        var euler = rotation.ToEuler();
        if (ImGuiX.DragFloat3("Rotation (Euler)", ref euler, 1f))
        {
            using (Eddy.History.BeginScope("Edit BG Rotation"))
            {
                var newRotation = euler.FromEuler();
                instance.Rotation = newRotation.ToRepacker();
                if (_bgPlaneActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Rotation = newRotation;
                }
            }
        }

        var scale = instance.Scale.ToXna();
        if (ImGuiX.DragFloat3("Scale", ref scale, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit BG Scale"))
            {
                instance.Scale = scale.ToRepacker();
                if (_bgPlaneActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Scale = scale;
                }
            }
        }

        var size = instance.Size.ToXna();
        if (ImGuiX.DragFloat3("Size", ref size, 0.01f))
        {
            using (Eddy.History.BeginScope("Edit BG Size"))
            {
                instance.Size = size.ToRepacker();
            }
        }

        var lightMap = instance.LightMap;
        if (ImGui.Checkbox("Light Map", ref lightMap))
        {
            using (Eddy.History.BeginScope("Edit BG Light Map"))
            {
                instance.LightMap = lightMap;
            }
        }

        var allowOverbrightness = instance.AllowOverbrightness;
        if (ImGui.Checkbox("Allow Overbrightness", ref allowOverbrightness))
        {
            using (Eddy.History.BeginScope("Edit BG Allow Overbrightness"))
            {
                instance.AllowOverbrightness = allowOverbrightness;
            }
        }

        var filter = instance.Filter.ToXna();
        if (ImGuiX.ColorEdit4("Filter", ref filter))
        {
            using (Eddy.History.BeginScope("Edit BG Filter"))
            {
                instance.Filter = filter.ToRepacker();
            }
        }

        var animated = instance.Animated;
        ImGui.BeginDisabled();
        ImGui.Checkbox("Animated", ref animated);
        ImGui.EndDisabled();

        var doublesided = instance.Doublesided;
        if (ImGui.Checkbox("Double Sided", ref doublesided))
        {
            using (Eddy.History.BeginScope("Edit BG Double Sided"))
            {
                instance.Doublesided = doublesided;
            }
        }

        var opacity = instance.Opacity;
        if (ImGui.InputFloat("Opacity", ref opacity))
        {
            using (Eddy.History.BeginScope("Edit BG Opacity"))
            {
                instance.Opacity = opacity;
            }
        }

        var attachedGroup = instance.AttachedGroup ?? InvalidId;
        if (ImGui.InputInt("Attached Group", ref attachedGroup))
        {
            using (Eddy.History.BeginScope("Edit BG Attached Group"))
            {
                instance.AttachedGroup = attachedGroup == InvalidId ? null : attachedGroup;
            }
        }

        var billboard = instance.Billboard;
        if (ImGui.Checkbox("Billboard", ref billboard))
        {
            using (Eddy.History.BeginScope("Edit BG Billboard"))
            {
                instance.Billboard = billboard;
            }
        }

        var syncWithSamples = instance.SyncWithSamples;
        if (ImGui.Checkbox("Sync With Samples", ref syncWithSamples))
        {
            using (Eddy.History.BeginScope("Edit BG Sync With Samples"))
            {
                instance.SyncWithSamples = syncWithSamples;
            }
        }

        var crosshatch = instance.Crosshatch;
        if (ImGui.Checkbox("Crosshatch", ref crosshatch))
        {
            using (Eddy.History.BeginScope("Edit BG Crosshatch"))
            {
                instance.Crosshatch = crosshatch;
            }
        }

        var alwaysOnTop = instance.AlwaysOnTop;
        if (ImGui.Checkbox("Always On Top", ref alwaysOnTop))
        {
            using (Eddy.History.BeginScope("Edit BG Always On Top"))
            {
                instance.AlwaysOnTop = alwaysOnTop;
            }
        }

        var fullbright = instance.Fullbright;
        if (ImGui.Checkbox("Fullbright", ref fullbright))
        {
            using (Eddy.History.BeginScope("Edit BG Fullbright"))
            {
                instance.Fullbright = fullbright;
            }
        }

        var pixelatedLightmap = instance.PixelatedLightmap;
        if (ImGui.Checkbox("Pixelated Lightmap", ref pixelatedLightmap))
        {
            using (Eddy.History.BeginScope("Edit BG Pixelated Lightmap"))
            {
                instance.PixelatedLightmap = pixelatedLightmap;
            }
        }

        var xTextureRepeat = instance.XTextureRepeat;
        if (ImGui.Checkbox("Xtexture Repeat", ref xTextureRepeat))
        {
            using (Eddy.History.BeginScope("Edit BG Xtexture Repeat"))
            {
                instance.XTextureRepeat = xTextureRepeat;
            }
        }

        var yTextureRepeat = instance.YTextureRepeat;
        if (ImGui.Checkbox("Ytexture Repeat", ref yTextureRepeat))
        {
            using (Eddy.History.BeginScope("Edit BG Ytexture Repeat"))
            {
                instance.YTextureRepeat = yTextureRepeat;
            }
        }

        var clampTexture = instance.ClampTexture;
        if (ImGui.Checkbox("Clamp Texture", ref clampTexture))
        {
            using (Eddy.History.BeginScope("Edit BG Clamp Texture"))
            {
                instance.ClampTexture = clampTexture;
            }
        }

        var actorType = (int)instance.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actorType, actors, actors.Length))
        {
            using (Eddy.History.BeginScope("Edit BG Actor Type"))
            {
                instance.ActorType = (ActorType)actorType;
            }
        }

        var attachedPlane = instance.AttachedPlane ?? InvalidId;
        if (ImGui.InputInt("Attached Plane", ref attachedPlane))
        {
            using (Eddy.History.BeginScope("Edit BG Attached Plane"))
            {
                instance.AttachedPlane = attachedPlane == InvalidId ? null : attachedPlane;
            }
        }

        var parallaxFactor = instance.ParallaxFactor;
        if (ImGui.InputFloat("Parallax Factor", ref parallaxFactor))
        {
            using (Eddy.History.BeginScope("Edit BG Parallax Factor"))
            {
                instance.ParallaxFactor = parallaxFactor;
            }
        }

    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            if (Eddy.SelectedContext != EddyContext.BackgroundPlane)
            {
                return;
            }

            var presentIds = Level.BackgroundPlanes.Keys.Where(k => k != InvalidId).ToHashSet();

            foreach (var id in _bgPlaneActors.Keys.ToList())
            {
                if (!presentIds.Contains(id))
                {
                    Eddy.Scene.DestroyActor(_bgPlaneActors[id]);
                    _bgPlaneActors.Remove(id);
                }
            }

            foreach (var (id, instance) in Level.BackgroundPlanes.Where(kv => kv.Key != InvalidId))
            {
                if (_bgPlaneActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = instance.Position.ToXna();
                    actor.Transform.Rotation = instance.Rotation.ToXna();
                    actor.Transform.Scale = instance.Scale.ToXna();
                }
                else
                {
                    actor = CreateSubActor();
                    actor.Name = $"{id}: {instance.TextureName}";
                    actor.Transform.Position = instance.Position.ToXna();
                    actor.Transform.Rotation = instance.Rotation.ToXna();
                    actor.Transform.Scale = instance.Scale.ToXna();
                    _bgPlaneActors[id] = actor;

                    var mesh = actor.AddComponent<BackgroundPlaneMesh>();
                    var ao = ResourceService.Load($"Background Planes/{instance.TextureName}");
                    mesh.Visualize(ao);
                }
            }

            _selectedIds.RemoveWhere(id => !Level.BackgroundPlanes.ContainsKey(id));
            if (_hoveredId.HasValue && !Level.BackgroundPlanes.ContainsKey(_hoveredId.Value))
            {
                _hoveredId = null;
            }

            return;
        }

        TeardownVisualization();
        _selectedIds.Clear();
        _hoveredId = null;

        #region Background Planes

        foreach (var (id, bgPlane) in Level.BackgroundPlanes.Where(kv => kv.Key != InvalidId))
        {
            var actor = CreateSubActor();
            actor.Name = $"{id}: {bgPlane.TextureName}";
            actor.Transform.Position = bgPlane.Position.ToXna();
            actor.Transform.Rotation = bgPlane.Rotation.ToXna();
            actor.Transform.Scale = bgPlane.Scale.ToXna();
            _bgPlaneActors[id] = actor;

            var mesh = actor.AddComponent<BackgroundPlaneMesh>();
            mesh.Billboard = bgPlane.Billboard;
            mesh.DoubleSided = bgPlane.Doublesided;
            mesh.Color = bgPlane.Filter.ToXna();
            mesh.Opacity = bgPlane.Opacity;

            var asset = ResourceService.Load($"Background Planes/{bgPlane.TextureName}");
            mesh.Visualize(asset);
        }

        #endregion
    }

    private void RemoveSelected()
    {
        foreach (var id in _selectedIds)
        {
            Level.BackgroundPlanes.Remove(id);
            if (_bgPlaneActors.TryGetValue(id, out var actor))
            {
                Eddy.Scene.DestroyActor(actor);
                _bgPlaneActors.Remove(id);
            }
        }

        _selectedIds.Clear();
    }

    private void BuildClipboard()
    {
        _clipboard.Clear();
        foreach (var id in _selectedIds)
        {
            var instance = Level.BackgroundPlanes[id];
            _clipboard.Add(new BackgroundPlane
            {
                TextureName = instance.TextureName,
                Position = instance.Position,
                Rotation = instance.Rotation,
                Scale = instance.Scale,
                Size = instance.Size,
                LightMap = instance.LightMap,
                AllowOverbrightness = instance.AllowOverbrightness,
                Filter = instance.Filter,
                Animated = instance.Animated,
                Doublesided = instance.Doublesided,
                Opacity = instance.Opacity,
                AttachedGroup = instance.AttachedGroup,
                Billboard = instance.Billboard,
                SyncWithSamples = instance.SyncWithSamples,
                Crosshatch = instance.Crosshatch,
                UnusedFlag = instance.UnusedFlag,
                AlwaysOnTop = instance.AlwaysOnTop,
                Fullbright = instance.Fullbright,
                PixelatedLightmap = instance.PixelatedLightmap,
                XTextureRepeat = instance.XTextureRepeat,
                YTextureRepeat = instance.YTextureRepeat,
                ClampTexture = instance.ClampTexture,
                ActorType = instance.ActorType,
                AttachedPlane = instance.AttachedPlane,
                ParallaxFactor = instance.ParallaxFactor
            });
        }
    }

    private void PasteClipboard()
    {
        _selectedIds.Clear();
        foreach (var instance in _clipboard)
        {
            var id = NextAvailableId();
            Level.BackgroundPlanes[id] = instance;

            var actor = CreateSubActor();
            actor.Name = $"{id}: {instance.TextureName}";
            actor.Transform.Position = instance.Position.ToXna();
            actor.Transform.Rotation = instance.Rotation.ToXna();
            actor.Transform.Scale = instance.Scale.ToXna();
            _bgPlaneActors[id] = actor;

            var mesh = actor.AddComponent<BackgroundPlaneMesh>();
            var bg = ResourceService.Load($"Background Planes/{instance.TextureName}");
            mesh.Visualize(bg);

            _selectedIds.Add(id);
        }
    }

    private int NextAvailableId()
    {
        return Level.BackgroundPlanes.Keys.Where(k => k != InvalidId).DefaultIfEmpty(-1).Max() + 1;
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.BackgroundPlane;
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
        foreach (var actor in _bgPlaneActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _bgPlaneActors.Clear();
    }
}