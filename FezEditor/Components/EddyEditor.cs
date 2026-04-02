using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class EddyEditor : EditorComponent
{
    private bool _showAssetBrowser;

    public override object Asset => _level;

    private readonly Level _level;

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private readonly Clock _clock = new();

    private readonly AssetBrowser _assetBrowser;

    private readonly EddyContexts _contexts = new();

    private (Actor Actor, PickHit Hit)? _raycastResult;

    private bool _showProperties;

    private bool _showCollisionMap;

    private bool _showPickableBounds;

    private bool _queueRevisualization;

    public EddyEditor(Game game, string title, Level level) : base(game, title)
    {
        _level = level;
        _assetBrowser = new AssetBrowser(game);
        History.RegisterConverter(new TrileEmplacementConverter());
        History.Track(level);
        History.StateChanged += () => _queueRevisualization = true;
    }

    public override void Update(GameTime gameTime)
    {
        _clock.Tick(gameTime);
        _contexts.Update();
        _scene.Update(gameTime);
    }

    public override void LoadContent()
    {
        _assetBrowser.LoadContent(ContentManager);
        _scene = new Scene(Game, ContentManager);
        {
            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";

            var camera = _cameraActor.AddComponent<Camera>();
            var gizmo = _cameraActor.AddComponent<OrientationGizmo>();
            _cameraActor.AddComponent<FirstPersonControl>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            camera.Far = 5000f;
            gizmo.UseFaceLabels = false;
        }
        {
            _contexts.AddOrdered(MakeContext<DefaultEddyContext>());
            _contexts.AddOrdered(MakeContext<TrileContext>());
            _contexts.AddOrdered(MakeContext<TrileGroupContext>());
            _contexts.AddOrdered(MakeContext<ArtObjectContext>());
            _contexts.AddOrdered(MakeContext<BackgroundPlaneContext>());
            _contexts.AddOrdered(MakeContext<NpcContext>());
            _contexts.AddOrdered(MakeContext<GomezContext>());
            _contexts.AddOrdered(MakeContext<VolumeContext>());
            _contexts.AddOrdered(MakeContext<PathContext>());
            _contexts.AddOrdered(MakeContext<ScriptContext>());
            _contexts.RevisualizeAll();
            _contexts.Init<DefaultEddyContext>();
        }
        {
            var actor = _scene.CreateActor();
            actor.Name = "Cursor";
            var cursor = actor.AddComponent<CursorMesh>();
            _contexts.ProvideCursor(cursor);
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
            _contexts.Current.Revisualize(partial: true);
        }

        DrawToolbar();

        var size = ImGuiX.GetContentRegionAvail();
        var w = (int)size.X;
        var h = (int)size.Y;

        if (w > 0 && h > 0)
        {
            var texture = _scene.Viewport.GetTexture();
            if (texture == null || texture.Width != w || texture.Height != h)
            {
                _scene.Viewport.SetSize(w, h);
            }

            if (texture is { IsDisposed: false })
            {
                ImGuiX.Image(texture, size);
                InputService.CaptureScroll(ImGui.IsItemHovered());

                var viewportMin = ImGuiX.GetItemRectMin();
                _raycastResult = null;

                if (ImGui.IsItemHovered() && !ImGui.IsMouseDragging(ImGuiMouseButton.Right))
                {
                    var ray = _scene.Viewport.Unproject(ImGuiX.GetMousePos(), viewportMin);
                    _raycastResult = _scene.Raycast(ray);
                    _contexts.TestConditions(ray, viewportMin);
                }

                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                gizmo.UseFaceLabels = true;
                gizmo.Draw(viewportMin + new Vector2(size.X - 8f, 8f));

                ImGuiX.DrawStats(viewportMin + new Vector2(8, 8), RenderingService.GetStats());

                DrawRaycastDebug(viewportMin + new Vector2(8, size.Y - 8f));

                var topCenter = viewportMin + new Vector2(size.X / 2f, 8f);
                ImGuiX.DrawClock(topCenter, _clock);
            }
        }

        if (_showProperties)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize |
                                           ImGuiWindowFlags.NoCollapse;
            if (ImGui.Begin("Properties", ref _showProperties, flags))
            {
                _contexts.Selected.DrawProperties();
                ImGui.End();
            }
        }

        if (_showAssetBrowser)
        {
            const ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse;
            ImGuiX.SetNextWindowSize(new Vector2(700, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Asset Browser", ref _showAssetBrowser, flags))
            {
                _assetBrowser.Draw();
                ImGui.End();
            }
        }
    }

    public override void Dispose()
    {
        _contexts.Dispose();
        _assetBrowser.Dispose();
        _scene.Dispose();
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
            if (ImGui.Checkbox("Collision Map", ref _showCollisionMap))
            {
                _contexts.ShowCollisionMap = _showCollisionMap;
            }

            if (ImGui.Checkbox("Pickable Bounds", ref _showPickableBounds))
            {
                _contexts.ShowPickableBounds = _showPickableBounds;
            }

            ImGui.EndPopup();
        }
    }

    private void DrawToolButton(string icon, EddyTool tool)
    {
        var isActive = _contexts.Current.Tool.Value == tool;
        if (isActive)
        {
            ImGui.BeginDisabled(true);
        }

        if (ImGui.Button($"{icon}##{tool}"))
        {
            _contexts.SyncTool(tool);
        }

        if (isActive)
        {
            ImGui.EndDisabled();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetItemTooltip(tool.GetLabel());
        }
    }

    private void DrawRaycastDebug(Vector2 position)
    {
        var stats = new Dictionary<string, string>
        {
            ["Context"] = _contexts.Current.GetType().Name
        };

        if (_raycastResult.HasValue)
        {
            var (actor, hit) = _raycastResult.Value;
            stats["Hit"] = actor.Name;
            stats["Distance"] = $"{hit.Distance:F2}";
            stats["Triangle"] = $"{hit.Index}";
            if (actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
            {
                var emp = mesh.GetEmplacement(hit.Index);
                stats["Emplacement"] = $"{emp.X}, {emp.Y}, {emp.Z}";
            }

            _contexts.DrawDebug(stats);
        }
        else
        {
            stats["Hit"] = "None";
        }

        var lineHeight = ImGui.GetTextLineHeight();
        ImGuiX.DrawStats(position - new Vector2(0, lineHeight * stats.Count + 8), stats);
    }

    private T MakeContext<T>() where T : EddyContext, new()
    {
        return new T
        {
            Scene = _scene,
            Clock = _clock,
            History = History,
            Level = _level,
            Camera = _cameraActor.GetComponent<Camera>(),
            AssetBrowser = _assetBrowser,
            ResourceService = ResourceService,
            InputService = InputService,
            StatusService = StatusService,
            ContentManager = ContentManager,
            Contexts = _contexts
        };
    }
}