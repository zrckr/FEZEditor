using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Quaternion = Microsoft.Xna.Framework.Quaternion;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace FezEditor.Components;

public class EddyEditor : EditorComponent, IEddyEditor
{
    public override object Asset => _level;

    public Scene Scene { get; private set; } = null!;

    public Clock Clock { get; } = new();

    public AssetBrowser AssetBrowser { get; }

    public InstanceBrowser InstanceBrowser { get; }

    public Camera Camera => _cameraActor.GetComponent<Camera>();

    public CursorMesh Cursor => _cursorActor.GetComponent<CursorMesh>();

    public Gizmo Gizmo => _gizmoActor.GetComponent<Gizmo>();

    public bool IsViewportHovered { get; private set; }

    public Ray Ray { get; private set; }

    public RaycastHit? Hit { get; private set; }

    public EddyTool Tool { get; set; } = EddyTool.Select;

    public HashSet<EddyTool> AllowedTools { get; } = new();

    public EddyContext HoveredContext { get; set; } = EddyContext.Default;

    public EddyContext SelectedContext { get; set; } = EddyContext.Default;

    public Dirty<EddyVisuals> Visuals { get; set; } = new(EddyVisuals.Default);

    public object? Pending { get; set; }

    private readonly Level _level;

    private Actor _cameraActor = null!;

    private Actor _cursorActor = null!;

    private Actor _gizmoActor = null!;

    private ViewMode _viewMode = ViewMode.Perspective;

    private PerspectiveState _savedPerspectiveState;

    private readonly List<BaseContext> _contexts = new();

    private readonly ScriptBrowser _scriptBrowser;

    private bool _showProperties;

    private bool _showAssetBrowser;

    private bool _showInstanceBrowser;

    private bool _showScriptBrowser;

    private bool _showRaycastDebug;

    private bool _queueRevisualization;

    public EddyEditor(Game game, string title, Level level) : base(game, title)
    {
        _level = level;
        AssetBrowser = new AssetBrowser(game);
        InstanceBrowser = new InstanceBrowser(level, AssetBrowser);
        _scriptBrowser = new ScriptBrowser(game, level, this);
        History.RegisterConverter(new TrileEmplacementConverter());
        History.Track(level);
        History.StateChanged += () => _queueRevisualization = true;
    }

    public override void Update(GameTime gameTime)
    {
        Gizmo.Hide();
        Cursor.ClearHover();
        Cursor.ClearSelection();
        StatusService.ClearHints();
        AllowedTools.Clear();
        HoveredContext = EddyContext.Default;

        Clock.Tick(gameTime);

        foreach (var context in _contexts)
        {
            context.Update();
        }

        Visuals = Visuals.Clean();
        Scene.Update(gameTime);
    }

    public override void LoadContent()
    {
        AssetBrowser.LoadContent(ContentManager);
        InstanceBrowser.LoadContent(ContentManager);
        Scene = new Scene(Game, ContentManager);
        {
            _cameraActor = Scene.CreateActor();
            _cameraActor.Name = "Camera";

            var camera = _cameraActor.AddComponent<Camera>();
            var orientation = _cameraActor.AddComponent<OrientationGizmo>();
            _cameraActor.AddComponent<FirstPersonControl>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            camera.Far = 5000f;
            orientation.UseFaceLabels = false;

        }
        {
            // DefaultContext is instantiated first so its subroot is first in the scene (draws before level geometry).
            // It is added to _contexts last so it has the lowest update priority.
            var defaultCtx = new DefaultContext(Game, _level, this);
            _contexts.Add(new TrileContext(Game, _level, this));
            _contexts.Add(new ArtObjectContext(Game, _level, this));
            _contexts.Add(new BackgroundPlaneContext(Game, _level, this));
            _contexts.Add(new NpcContext(Game, _level, this));
            _contexts.Add(new GomezContext(Game, _level, this));
            _contexts.Add(new VolumeContext(Game, _level, this));
            _contexts.Add(new PathContext(Game, _level, this));
            _contexts.Add(defaultCtx);

            defaultCtx.Revisualize();
            foreach (var ctx in _contexts.Where(c => c != defaultCtx))
            {
                ctx.Revisualize();
            }

            defaultCtx.PostRevisualize();
        }
        {
            _cursorActor = Scene.CreateActor();
            _cursorActor.Name = "Cursor";
            _cursorActor.AddComponent<CursorMesh>();
        }
        {
            _gizmoActor = Scene.CreateActor();
            _gizmoActor.Name = "Gizmo";
            var gizmo = _gizmoActor.AddComponent<Gizmo>();
            gizmo.Camera = _cameraActor.GetComponent<Camera>();
        }

        var gomezPos = _level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up * 1.5f;
        var approachDir = _level.StartingFace.Face.AsVector();
        _cameraActor.GetComponent<FirstPersonControl>().FocusOn(gomezPos, approachDir, 10f);
    }

    public override void Draw()
    {
        if (_queueRevisualization)
        {
            _queueRevisualization = false;
            foreach (var ctx in _contexts)
            {
                ctx.Revisualize(partial: true);
            }
        }

        DrawToolbar();

        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = Scene.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                Scene.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
                InputService.IsViewportHovered = ImGui.IsItemHovered();

                var viewportMin = ImGuiX.GetItemRectMin();
                _gizmoActor.GetComponent<Gizmo>().Viewport = viewportMin;

                Hit = null;
                IsViewportHovered = ImGui.IsItemHovered() && !ImGui.IsMouseDragging(ImGuiMouseButton.Right);
                if (IsViewportHovered)
                {
                    Ray = Scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
                    Hit = Scene.Raycast(Ray);
                }

                foreach (var ctx in _contexts)
                {
                    ctx.DrawOverlay();
                }

                var orientation = _cameraActor.GetComponent<OrientationGizmo>();
                orientation.Draw(viewportMin + new Vector2(size.X - 8f, 8f));

                ImGuiX.DrawStats(viewportMin + new Vector2(8, 8), RenderingService.GetStats());

                if (_showRaycastDebug)
                {
                    DrawRaycastDebug(viewportMin + new Vector2(8, size.Y - 8f));
                }

                var topCenter = viewportMin + new Vector2(size.X / 2f, 8f);
                ImGuiX.DrawClock(topCenter, Clock);
            }
        }

        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoCollapse;

            var context = SelectedContext.GetLabel();
            if (ImGui.Begin("Properties", ref _showProperties, flags))
            {
                ImGui.SeparatorText(context);
                foreach (var ctx in _contexts)
                {
                    ctx.DrawProperties();
                }

                ImGui.End();
            }
        }

        if (_showAssetBrowser)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
            ImGuiX.SetNextWindowSize(new Vector2(700, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Asset Browser", ref _showAssetBrowser, flags))
            {
                AssetBrowser.Draw();
                ImGui.End();
            }
        }

        if (_showInstanceBrowser)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
            ImGuiX.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Instance Browser", ref _showInstanceBrowser, flags))
            {
                InstanceBrowser.Draw();
                ImGui.End();
            }
        }

        if (_showScriptBrowser)
        {
            SelectedContext = EddyContext.Script;

            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
            ImGuiX.SetNextWindowSize(new Vector2(500, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Script Browser", ref _showScriptBrowser, flags))
            {
                _scriptBrowser.Draw();
                ImGui.End();
            }

            if (!_showScriptBrowser)
            {
                SelectedContext = EddyContext.Default;
            }
        }
    }

    public override void Dispose()
    {
        foreach (var ctx in _contexts)
        {
            ctx.Dispose();
        }

        AssetBrowser.Dispose();
        InstanceBrowser.Dispose();
        Scene.Dispose();
        base.Dispose();
    }

    private void SwitchToPerspective()
    {
        if (_viewMode == ViewMode.Perspective)
        {
            return;
        }

        _viewMode = ViewMode.Perspective;
        _cameraActor.RemoveComponent<MapPanControl>();
        _cameraActor.RemoveComponent<MapZoomControl>();
        _cameraActor.AddComponent<FirstPersonControl>();
        Camera.Projection = Camera.ProjectionType.Perspective;
        Camera.FieldOfView = 90f;
        _cameraActor.Transform.Position = _savedPerspectiveState.Position;
        _cameraActor.Transform.Rotation = _savedPerspectiveState.Rotation;
        Camera.Offset = _savedPerspectiveState.Offset;
    }

    private void SwitchToOrtho(ViewMode mode, float yaw)
    {
        if (_viewMode == ViewMode.Perspective)
        {
            _savedPerspectiveState = new PerspectiveState(
                _cameraActor.Transform.Position,
                _cameraActor.Transform.Rotation,
                Camera.Offset
            );
            _cameraActor.RemoveComponent<FirstPersonControl>();
        }
        else
        {
            _cameraActor.RemoveComponent<MapPanControl>();
            _cameraActor.RemoveComponent<MapZoomControl>();
        }

        _viewMode = mode;
        Camera.Projection = Camera.ProjectionType.Orthographic;
        _cameraActor.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(yaw, 0f, 0f);
        _cameraActor.Transform.Position = _level.Size.ToXna() / 2f;
        Camera.Offset = new Vector3(0f, 0f, 500f);
        _cameraActor.AddComponent<MapPanControl>();
        var zoom = _cameraActor.AddComponent<MapZoomControl>();
        zoom.Reset();
    }

    private void DrawToolbar()
    {
        DrawToolButton(Lucide.MousePointer2, EddyTool.Select);

        ImGui.SameLine();
        DrawToolButton(Lucide.Move3D, EddyTool.Translate);

        ImGui.SameLine();
        DrawToolButton(Lucide.Rotate3D, EddyTool.Rotate);

        ImGui.SameLine();
        DrawToolButton(Lucide.Scale3D, EddyTool.Scale);

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        DrawToolButton(Lucide.Paintbrush, EddyTool.Paint);

        ImGui.SameLine();
        DrawToolButton(Lucide.Pipette, EddyTool.Pick);

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(_showAssetBrowser);
            if (ImGui.Button($"{Lucide.Sprout}"))
            {
                _showAssetBrowser = true;
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Asset Browser");
            }
        }

        ImGui.SameLine();
        {
            if (ImGui.Button($"{Lucide.SquareDashed}"))
            {
                Tool = EddyTool.Paint;
                SelectedContext = EddyContext.Volume;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Place Volume");
            }
        }


        ImGui.SameLine();
        {
            if (ImGui.Button($"{Lucide.Route}"))
            {
                Tool = EddyTool.Paint;
                SelectedContext = EddyContext.Path;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Place Path");
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(_showInstanceBrowser);
            if (ImGui.Button($"{Lucide.Trees}"))
            {
                _showInstanceBrowser = true;
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Instance Browser");
            }
        }

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(_showScriptBrowser);
            if (ImGui.Button($"{Lucide.CodeXml}"))
            {
                _showScriptBrowser = true;
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Script Browser");
            }
        }

        ImGui.SameLine();
        {
            ImGui.BeginDisabled(_showProperties);
            if (ImGui.Button($"{Lucide.List}"))
            {
                _showProperties = true;
            }
            ImGui.EndDisabled();

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Show Properties Window");
            }
        }

        DrawViewOptions();
    }

    private void DrawViewOptions()
    {
        var text = $"{Icons.KebabVertical} {_viewMode}";
        var viewButtonWidth = ImGui.CalcTextSize(text).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - viewButtonWidth);

        if (ImGui.Button(text))
        {
            ImGui.OpenPopup("##ViewOptions");
        }

        if (ImGui.BeginPopup("##ViewOptions"))
        {
            ImGui.SeparatorText($"{Lucide.Camera} Projections");
            {
                if (ImGui.Button("Perspective View"))
                {
                    SwitchToPerspective();
                }

                if (ImGui.Button("Front View"))
                {
                    SwitchToOrtho(ViewMode.Front, 0f);
                }

                if (ImGui.Button("Right View"))
                {
                    SwitchToOrtho(ViewMode.Right, MathHelper.PiOver2);
                }

                if (ImGui.Button("Back View"))
                {
                    SwitchToOrtho(ViewMode.Back, MathHelper.Pi);
                }

                if (ImGui.Button("Left View"))
                {
                    SwitchToOrtho(ViewMode.Left, -MathHelper.PiOver2);
                }
            }

            ImGui.SeparatorText($"{Lucide.Pyramid} Visuals");
            {
                var visuals = (int)Visuals.Value;
                var edited = false;
                edited |= ImGui.CheckboxFlags("Pickable Bounds", ref visuals, (int)EddyVisuals.PickableBounds);
                edited |= ImGui.CheckboxFlags("Collision Map", ref visuals, (int)EddyVisuals.CollisionMap);
                edited |= ImGui.CheckboxFlags("Triles", ref visuals, (int)EddyVisuals.Triles);
                edited |= ImGui.CheckboxFlags("Empty Triles", ref visuals, (int)EddyVisuals.EmptyTriles);
                edited |= ImGui.CheckboxFlags("Art Objects", ref visuals, (int)EddyVisuals.ArtObjects);
                edited |= ImGui.CheckboxFlags("Background Planes", ref visuals, (int)EddyVisuals.BackgroundPlanes);
                edited |= ImGui.CheckboxFlags("Non-Playable Characters", ref visuals, (int)EddyVisuals.NonPlayableCharacters);
                edited |= ImGui.CheckboxFlags("Volumes", ref visuals, (int)EddyVisuals.Volumes);
                edited |= ImGui.CheckboxFlags("Paths", ref visuals, (int)EddyVisuals.Paths);
                edited |= ImGui.CheckboxFlags("Liquid", ref visuals, (int)EddyVisuals.Liquid);
                edited |= ImGui.CheckboxFlags("Sky", ref visuals, (int)EddyVisuals.Sky);
                edited |= ImGui.CheckboxFlags("Gomez", ref visuals, (int)EddyVisuals.Gomez);
                if (edited) Visuals = (EddyVisuals)visuals;
            }

            ImGui.Separator();
            ImGui.Checkbox("Raycast Debug", ref _showRaycastDebug);

            if (ImGui.Button("Export as Diorama"))
            {
                FileDialog.Show(FileDialog.Type.SaveFile, files =>
                {
                    var exporter = new PhilExporter(Game, _level, files[0]);
                    Game.AddComponent(exporter);
                }, new FileDialog.Options
                {
                    Title = "Export level diorama",
                    DefaultLocation = Path.Combine(ResourceService.GetFullPath(""), $"{_level.Name}.glb"),
                    Filters = [new FileDialog.Filter("GLB file", "glb")]
                });
            }

            ImGui.EndPopup();
        }
    }

    private void DrawToolButton(string icon, EddyTool tool)
    {
        ImGui.BeginDisabled(Tool == tool || !AllowedTools.Contains(tool));
        if (ImGui.Button($"{icon}##{tool}"))
        {
            Tool = tool;
        }

        ImGui.EndDisabled();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetItemTooltip(tool.GetLabel());
        }
    }

    private void DrawRaycastDebug(Vector2 position)
    {
        var stats = new Dictionary<string, string>
        {
            ["Hovered"] = HoveredContext.ToString(),
            ["Selected"] = SelectedContext.ToString()
        };

        if (Hit.HasValue)
        {
            var actor = Hit.Value.Actor;
            var index = Hit.Value.Index;
            stats["Hit"] = actor.Name;
            stats["Distance"] = $"{Hit.Value.Distance:F2}";
            stats["Triangle"] = $"{index}";
            if (actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
            {
                var emp = mesh.GetEmplacement(index);
                stats["Emplacement"] = $"{emp.X}, {emp.Y}, {emp.Z}";
            }
        }
        else
        {
            stats["Hit"] = "None";
        }

        var lineHeight = ImGui.GetTextLineHeight();
        ImGuiX.DrawStats(position - new Vector2(0, lineHeight * stats.Count + 8), stats);
    }

    public void FocusOn(Vector3 target)
    {
        var candidates = new[]
        {
            Vector3.Forward,
            Vector3.Backward,
            Vector3.Left,
            Vector3.Right
        };

        var levelSize = _level.Size.ToXna();
        var levelCenter = levelSize / 2f;

        var bestDir1 = candidates[0];
        var bestClearance = -1f;

        foreach (var dir in candidates)
        {
            // Camera starts at the level boundary in this direction, at target's Y height
            var camPos = new Vector3(
                levelCenter.X + dir.X * levelSize.X / 2f,
                target.Y,
                levelCenter.Z + dir.Z * levelSize.Z / 2f
            );

            // Raycast from boundary toward target
            var toTarget = target - camPos;
            var dist = toTarget.Length();
            if (dist < 0.001f)
            {
                continue;
            }

            var toTargetDir = toTarget / dist;
            var hit = Scene.Raycast(new Ray(camPos, toTargetDir));

            // Clearance = distance to first obstruction (or full distance if unobstructed)
            var clearance = hit?.Distance ?? dist;
            if (clearance > bestClearance)
            {
                bestClearance = clearance;
                bestDir1 = dir;
            }
        }

        // Place camera at level boundary in the best direction, clamped to desired distance
        var finalCamPos = new Vector3(
            levelCenter.X + bestDir1.X * levelSize.X / 2f,
            target.Y,
            levelCenter.Z + bestDir1.Z * levelSize.Z / 2f
        );

        var approachDir = Vector3.Normalize(finalCamPos - target);
        const float desiredDistance = 10f;
        var placementDist = Math.Min(Vector3.Distance(finalCamPos, target), desiredDistance);

        _cameraActor.GetComponent<FirstPersonControl>().FocusOn(target, approachDir, placementDist);
    }

    private enum ViewMode
    {
        Perspective,
        Front,
        Back,
        Right,
        Left
    }

    private readonly record struct PerspectiveState(Vector3 Position, Quaternion Rotation, Vector3 Offset);

    public static Level Create(string name, TrileSet trileSet)
    {
        var level = new Level
        {
            Name = name,
            TrileSetName = trileSet.Name,
            SkyName = "Default",
            Size = new RVector3(16, 16, 16),
            StartingFace = new TrileFace
            {
                Id = new TrileEmplacement(0, 0, 0),
                Face = FaceOrientation.Front
            }
        };

        var trileId = -1;
        foreach (var (id, trile) in trileSet.Triles)
        {
            if (trile.Geometry.Vertices.Length > 0)
            {
                trileId = id;
                break;
            }
        }

        for (var x = 0; x < level.Size.X; x++)
        {
            for (var z = 0; z < level.Size.Y; z++)
            {
                var emplacement = new TrileEmplacement(x, 0, z);
                var instance = new TrileInstance
                {
                    Position = new Vector3(x, 0, z).ToRepacker(),
                    TrileId = trileId,
                    PhiLight = 0
                };

                level.Triles[emplacement] = instance;
            }
        }

        return level;
    }
}