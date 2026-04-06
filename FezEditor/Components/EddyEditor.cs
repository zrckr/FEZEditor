using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class EddyEditor : EditorComponent, IEddyEditor
{
    public override object Asset => _level;

    public Scene Scene { get; private set; } = null!;

    public Clock Clock { get; } = new();

    public AssetBrowser AssetBrowser { get; }

    public Camera Camera => _cameraActor.GetComponent<Camera>();

    public CursorMesh Cursor => _cursorActor.GetComponent<CursorMesh>();

    public Gizmo Gizmo => _gizmoActor.GetComponent<Gizmo>();

    public bool IsViewportHovered { get; private set; }

    public Ray Ray { get; private set; }

    public RaycastHit? Hit { get; private set; }

    public EddyTool Tool { get; set; } = EddyTool.Select;

    public EddyContext Context { get; set; } = EddyContext.Default;

    public Dirty<bool> ShowPickableBounds { get; set; } = new(false);

    public Dirty<bool> ShowCollisionMap { get; set; } = new(false);

    private readonly Level _level;

    private Actor _cameraActor = null!;

    private Actor _cursorActor = null!;

    private Actor _gizmoActor = null!;

    private readonly List<BaseContext> _contexts = new();

    private bool _showProperties;

    private bool _showAssetBrowser;

    private bool _queueRevisualization;

    public EddyEditor(Game game, string title, Level level) : base(game, title)
    {
        _level = level;
        AssetBrowser = new AssetBrowser(game);
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
        Context = EddyContext.Default;

        Clock.Tick(gameTime);

        foreach (var context in _contexts)
        {
            context.Update();
        }

        Scene.Update(gameTime);
    }

    public override void LoadContent()
    {
        AssetBrowser.LoadContent(ContentManager);
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
            var defaultCtx = new DefaultContext(Game, _level, this);
            _contexts.Add(defaultCtx);
            _contexts.Add(new TrileContext(Game, _level, this));
            _contexts.Add(new ArtObjectContext(Game, _level, this));
            _contexts.Add(new BackgroundPlaneContext(Game, _level, this));
            _contexts.Add(new NpcContext(Game, _level, this));
            _contexts.Add(new GomezContext(Game, _level, this));
            _contexts.Add(new VolumeContext(Game, _level, this));
            _contexts.Add(new PathContext(Game, _level, this));
            _contexts.Add(new ScriptContext(Game, _level, this));

            foreach (var ctx in _contexts)
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

        var position = _level.StartingFace.Id.ToXna().ToVector3();
        position += (Vector3.Up * 1.5f) + _level.StartingFace.Face.AsVector() * 10f;
        _cameraActor.Transform.Position = position;
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
                InputService.CaptureScroll(ImGui.IsItemHovered());

                var viewportMin = ImGuiX.GetItemRectMin();
                _gizmoActor.GetComponent<Gizmo>().Viewport = viewportMin;

                Hit = null;
                IsViewportHovered = ImGui.IsItemHovered() && !ImGui.IsMouseDragging(ImGuiMouseButton.Right);
                if (IsViewportHovered)
                {
                    Ray = Scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
                    Hit = Scene.Raycast(Ray);
                }

                var orientation = _cameraActor.GetComponent<OrientationGizmo>();
                orientation.UseFaceLabels = true;
                orientation.Draw(viewportMin + new Vector2(size.X - 8f, 8f));

                ImGuiX.DrawStats(viewportMin + new Vector2(8, 8), RenderingService.GetStats());

                DrawRaycastDebug(viewportMin + new Vector2(8, size.Y - 8f));

                var topCenter = viewportMin + new Vector2(size.X / 2f, 8f);
                ImGuiX.DrawClock(topCenter, Clock);
            }
        }

        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoCollapse;
            if (ImGui.Begin("Properties", ref _showProperties, flags))
            {
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
    }

    public override void Dispose()
    {
        foreach (var ctx in _contexts)
        {
            ctx.Dispose();
        }

        AssetBrowser.Dispose();
        Scene.Dispose();
        base.Dispose();
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
        ImGui.BeginDisabled(_showAssetBrowser);
        if (ImGui.Button($"{Lucide.Palette}"))
        {
            _showAssetBrowser = true;
        }

        ImGui.EndDisabled();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Asset Browser");
        }

        ImGui.SameLine();
        ImGui.TextDisabled("|");

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.Export} Diorama"))
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

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Export as diorama");
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_showProperties);
        if (ImGui.Button($"{Icons.SymbolProperty} Properties"))
        {
            _showProperties = true;
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.KebabVertical} Perspective"))
        {
            ImGui.OpenPopup("ViewOptions");
        }

        if (ImGui.BeginPopup("ViewOptions"))
        {
            var showCollisionMap = ShowCollisionMap.Value;
            if (ImGui.Checkbox("Collision Map", ref showCollisionMap))
            {
                ShowCollisionMap = showCollisionMap;
            }

            var showPickableBounds = ShowPickableBounds.Value;
            if (ImGui.Checkbox("Pickable Bounds", ref showPickableBounds))
            {
                ShowPickableBounds = showPickableBounds;
            }

            ImGui.EndPopup();
        }
    }

    private void DrawToolButton(string icon, EddyTool tool)
    {
        ImGui.BeginDisabled(Tool == tool);
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
        var stats = new Dictionary<string, string>();
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
}