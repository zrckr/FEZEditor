using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class PathContext : BaseContext
{
    private static readonly Color PathColor = new(1f, 0.5f, 0f, 0.8f);

    private static readonly Color WaypointHoverColor = Color.Blue;

    private static readonly Color WaypointSelectionColor = Color.Red;

    private readonly Dictionary<int, Actor> _pathActors = new();

    private int? _selectedPathId;

    private bool _selectedIsGroupPath;

    private Vector3 _selectedOffset;

    private readonly HashSet<int> _selectedWaypointIndices = new();

    private int? _hoveredWaypointIndex;

    private int? _hoveredHitKey;

    private IDisposable? _translateScope;

    public PathContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void Begin()
    {
        if (Eddy.Pending is Pending p)
        {
            _selectedPathId = p.Id;
            _selectedIsGroupPath = p.IsGroup;
            _selectedOffset = ComputeOffset(p.Id, p.IsGroup);
            _selectedWaypointIndices.Clear();
            Eddy.Pending = null;
            Eddy.FocusOn(_selectedOffset);
        }
    }

    protected override void End()
    {
        _translateScope?.Dispose();
        _translateScope = null;
        _selectedPathId = null;
        _selectedIsGroupPath = false;
        _selectedOffset = Vector3.Zero;
        _selectedWaypointIndices.Clear();
    }

    protected override void TestConditions()
    {
        _hoveredWaypointIndex = null;
        _hoveredHitKey = null;

        foreach (var actor in _pathActors.Values)
        {
            var mesh = actor.GetComponent<PathMesh>();
            mesh.WaypointColors = Enumerable.Repeat(PathColor, mesh.Waypoints.Count).ToList();
        }

        if (Eddy.Visuals.IsDirty)
        {
            var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.Paths);
            foreach (var actor in _pathActors.Values)
            {
                actor.Visible = visible;
                actor.GetComponent<PathMesh>().Pickable = visible;
            }
        }

        if (Eddy.InstanceBrowser.Select(out var sel) && sel.context == EddyContext.Path)
        {
            Eddy.InstanceBrowser.Consume();
            Eddy.Pending = new Pending(sel.id, IsGroup: false);
            Eddy.SelectedContext = EddyContext.Path;
            Eddy.Tool = EddyTool.Select;
        }

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.TryGetComponent<PathMesh>(out _))
        {
            var hitActor = Eddy.Hit.Value.Actor;
            _hoveredHitKey = _pathActors.FirstOrDefault(kv => kv.Value == hitActor).Key;
            _hoveredWaypointIndex = Eddy.Hit.Value.Index;
            Eddy.HoveredContext = EddyContext.Path;
        }

        if (_hoveredHitKey == null && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            Eddy.SelectedContext == EddyContext.Path &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedPathId = null;
            _selectedWaypointIndices.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedPathId.HasValue)
        {
            Eddy.SelectedContext = EddyContext.Path;
        }
    }

    protected override void Act()
    {
        Eddy.AllowedTools.Add(EddyTool.Select);
        Eddy.AllowedTools.Add(EddyTool.Translate);
        Eddy.AllowedTools.Add(EddyTool.Paint);

        if (Eddy.Tool == EddyTool.Select &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) &&
            Eddy.IsViewportHovered &&
            _hoveredHitKey.HasValue &&
            _pathActors.ContainsKey(_hoveredHitKey.Value))
        {
            var hitKey = _hoveredHitKey.Value;
            if (hitKey >= 0)
            {
                _selectedPathId = hitKey;
                _selectedIsGroupPath = false;
                _selectedOffset = Vector3.Zero;
            }
            else
            {
                _selectedPathId = -(hitKey + 1);
                _selectedIsGroupPath = true;
                _selectedOffset = ComputeOffset(_selectedPathId.Value, true);
            }

            if (!ImGui.GetIO().KeyShift)
            {
                _selectedWaypointIndices.Clear();
            }

            if (_hoveredWaypointIndex.HasValue)
            {
                _selectedWaypointIndices.Add(_hoveredWaypointIndex.Value);
            }
        }

        UpdateTranslate();
        UpdatePaint();

        foreach (var (key, actor) in _pathActors)
        {
            var mesh = actor.GetComponent<PathMesh>();
            var isSelectedPath = _selectedPathId.HasValue
                                 && ((key >= 0 && !_selectedIsGroupPath && key == _selectedPathId.Value)
                                     || (key < 0 && _selectedIsGroupPath && -(key + 1) == _selectedPathId.Value));

            var isHoveredPath = key == _hoveredHitKey;
            if (isSelectedPath)
            {
                for (var i = 0; i < mesh.WaypointColors.Count; i++)
                {
                    if (_selectedWaypointIndices.Contains(i))
                    {
                        mesh.WaypointColors[i] = WaypointSelectionColor;
                    }
                    else if (isHoveredPath && i == _hoveredWaypointIndex)
                    {
                        mesh.WaypointColors[i] = WaypointHoverColor;
                    }
                    else
                    {
                        mesh.WaypointColors[i] = PathColor;
                    }
                }
            }
            else if (isHoveredPath)
            {
                for (var i = 0; i < mesh.WaypointColors.Count; i++)
                {
                    mesh.WaypointColors[i] = i == _hoveredWaypointIndex ? WaypointHoverColor : PathColor;
                }
            }
        }
    }

    private void UpdateTranslate()
    {
        if (Eddy.Tool != EddyTool.Translate)
        {
            return;
        }

        var path = GetActivePath();
        if (path == null || _selectedWaypointIndices.Count == 0)
        {
            return;
        }

        var segIndex = _selectedWaypointIndices.Min();
        if (segIndex >= path.Segments.Count)
        {
            return;
        }

        var seg = path.Segments[segIndex];
        var prevDestination = seg.Destination.ToXna();
        var waypointWorld = _selectedOffset + prevDestination;
        if (Eddy.Gizmo.Translate(ref waypointWorld))
        {
            var delta = waypointWorld - (_selectedOffset + prevDestination);
            var activeKey = _selectedIsGroupPath ? -(_selectedPathId!.Value + 1) : _selectedPathId!.Value;
            var mesh = _pathActors.TryGetValue(activeKey, out var actor) ? actor.GetComponent<PathMesh>() : null;
            foreach (var idx in _selectedWaypointIndices.Where(i => i < path.Segments.Count))
            {
                var s = path.Segments[idx];
                var d = s.Destination.ToXna();
                s.Destination = (d + delta).ToRepacker();
                if (mesh != null && idx < mesh.Waypoints.Count)
                {
                    mesh.Waypoints[idx] = _selectedOffset + s.Destination.ToXna();
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate Path Waypoint");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _translateScope?.Dispose();
            _translateScope = null;
        }
    }

    private void UpdatePaint()
    {
        if (Eddy.Tool != EddyTool.Paint)
        {
            return;
        }

        StatusService.AddHints(("LMB", "Place Waypoint"));

        if (!Eddy.Hit.HasValue || !Eddy.Hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) || mesh == null)
        {
            return;
        }

        var index = Eddy.Hit.Value.Index;
        var emplacement = mesh.GetEmplacement(index);
        if (!Level.Triles.TryGetValue(emplacement, out var hoveredInstance))
        {
            return;
        }

        var box = mesh.GetBounds().ElementAt(index);
        var face = Mathz.DetermineFace(box, Eddy.Ray, Eddy.Hit.Value.Distance);
        var trileCenter = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
        var hitPoint = trileCenter + face.AsVector() * 0.5f;

        var origin = trileCenter + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
        var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
        Eddy.Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            using (Eddy.History.BeginScope("Place Path Waypoint"))
            {
                if (_selectedPathId == null)
                {
                    var newId = Level.Paths.Keys.Where(k => k != InvalidId).DefaultIfEmpty(-1).Max() + 1;
                    Level.Paths[newId] = new MovementPath();
                    _selectedPathId = newId;
                    _selectedIsGroupPath = false;
                    _selectedOffset = Vector3.Zero;
                    _selectedWaypointIndices.Clear();
                }

                var path = GetActivePath()!;
                path.Segments.Add(new PathSegment
                {
                    Destination = (hitPoint - _selectedOffset).ToRepacker()
                });
                _selectedWaypointIndices.Clear();
                _selectedWaypointIndices.Add(path.Segments.Count - 1);
            }
        }
    }

    public override void DrawOverlay()
    {
        if (Eddy.Tool != EddyTool.Paint || !Eddy.IsViewportHovered || Eddy.SelectedContext != EddyContext.Path)
        {
            return;
        }

        var mousePos = ImGui.GetMousePos();
        var drawPos = mousePos + new NVector2(12f, 12f);
        const string text = $"{Lucide.Route} Waypoint";
        var padding = new NVector2(4f, 2f);
        var dl = ImGui.GetForegroundDrawList(ImGui.GetMainViewport());
        dl.AddRectFilled(drawPos - padding, drawPos + ImGui.CalcTextSize(text) + padding,
            ImGui.GetColorU32(ImGuiCol.PopupBg));
        dl.AddText(drawPos, ImGui.GetColorU32(ImGuiCol.Text), text);
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.SelectedContext != EddyContext.Path && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Paths

        foreach (var (id, path) in Level.Paths.Where(kv => kv.Key != InvalidId))
        {
            CreatePathActor(id, path, Vector3.Zero);
        }

        foreach (var (groupId, group) in Level.Groups.Where(kv => kv.Key != InvalidId && kv.Value.Path != null))
        {
            CreatePathActor(-(groupId + 1), group.Path!, ComputeOffset(groupId, true));
        }

        #endregion
    }

    public override void DrawProperties()
    {
        if (Eddy.SelectedContext != EddyContext.Path)
        {
            return;
        }

        var path = GetActivePath();
        if (path == null)
        {
            ImGui.TextDisabled("No path selected.");
            return;
        }

        var label = _selectedIsGroupPath ? $"Group {_selectedPathId} Path" : $"Level Path #{_selectedPathId}";
        ImGui.TextDisabled(label);

        ImGui.SeparatorText("Properties");

        var endBehavior = (int)path.EndBehavior;
        var endBehaviorNames = Enum.GetNames<PathEndBehavior>();
        if (ImGui.Combo("End Behavior", ref endBehavior, endBehaviorNames, endBehaviorNames.Length))
        {
            using (Eddy.History.BeginScope("Edit Path End Behavior"))
            {
                path.EndBehavior = (PathEndBehavior)endBehavior;
            }
        }

        var isSpline = path.IsSpline;
        if (ImGui.Checkbox("Is Spline", ref isSpline))
        {
            using (Eddy.History.BeginScope("Edit Path Is Spline"))
            {
                path.IsSpline = isSpline;
            }
        }

        var needsTrigger = path.NeedsTrigger;
        if (ImGui.Checkbox("Needs Trigger", ref needsTrigger))
        {
            using (Eddy.History.BeginScope("Edit Path Needs Trigger"))
            {
                path.NeedsTrigger = needsTrigger;
            }
        }

        var offsetSec = path.OffsetSeconds;
        if (ImGui.DragFloat("Offset Seconds", ref offsetSec))
        {
            using (Eddy.History.BeginScope("Edit Path Offset Seconds"))
            {
                path.OffsetSeconds = offsetSec;
            }
        }

        ImGui.SeparatorText($"Segments ({path.Segments.Count})");
        for (var i = 0; i < path.Segments.Count; i++)
        {
            var seg = path.Segments[i];
            var isSegSelected = _selectedWaypointIndices.Contains(i);
            if (isSegSelected)
            {
                ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(0.6f, 0.5f, 0f, 1f));
            }

            if (ImGui.CollapsingHeader($"Segment {i}##{i}"))
            {
                ImGui.BeginDisabled(true);
                var dest = seg.Destination.ToXna();
                ImGuiX.DragFloat3("Destination", ref dest);
                ImGui.EndDisabled();
            }

            if (ImGui.IsItemClicked())
            {
                if (!ImGui.GetIO().KeyShift)
                {
                    _selectedWaypointIndices.Clear();
                }

                _selectedWaypointIndices.Add(i);
            }

            if (isSegSelected)
            {
                ImGui.PopStyleColor();
            }
        }
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Path;
    }

    public override void Dispose()
    {
        _translateScope?.Dispose();
        TeardownVisualization();
        base.Dispose();
    }

    private void CreatePathActor(int key, MovementPath path, Vector3 offset)
    {
        var actor = CreateSubActor();
        var isSelected = _selectedPathId.HasValue
                         && ((_selectedIsGroupPath && key < 0 && -(key + 1) == _selectedPathId.Value)
                             || (!_selectedIsGroupPath && key >= 0 && key == _selectedPathId.Value));

        actor.Name = key >= 0 ? $"{key}: Path" : $"Group {-(key + 1)}: Path";
        _pathActors[key] = actor;

        var mesh = actor.AddComponent<PathMesh>();
        mesh.Waypoints = path.Segments
            .Select(ps => offset + ps.Destination.ToXna())
            .ToList();
        mesh.WaypointColors = Enumerable.Repeat(isSelected
                ? WaypointSelectionColor
                : PathColor,
            mesh.Waypoints.Count).ToList();

        var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.Paths);
        actor.Visible = visible;
        mesh.Pickable = visible;
    }

    private Vector3 ComputeOffset(int groupId, bool isGroup)
    {
        if (!isGroup || !Level.Groups.TryGetValue(groupId, out var group) || group.Triles.Count == 0)
        {
            return Vector3.Zero;
        }

        return group.Triles
                   .Select(t => t.Position.ToXna())
                   .Aggregate(Vector3.Zero, (sum, p) => sum + p)
               / group.Triles.Count;
    }

    private MovementPath? GetActivePath()
    {
        if (!_selectedPathId.HasValue)
        {
            return null;
        }

        if (_selectedIsGroupPath)
        {
            return Level.Groups.TryGetValue(_selectedPathId.Value, out var g) ? g.Path : null;
        }

        return Level.Paths.GetValueOrDefault(_selectedPathId.Value);
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _pathActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _pathActors.Clear();
    }

    internal record Pending(int Id, bool IsGroup);
}