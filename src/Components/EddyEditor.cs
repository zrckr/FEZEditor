using FezEditor.Actors;
using FezEditor.Components.Eddy;
using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class EddyEditor : EditorComponent
{
    public const int InvalidId = -1;

    public const int MaxRecentEntries = 10;

    public override object Asset
    {
        get
        {
            _level.Name = Path.GetFileName(Title);
            return _level;
        }
    }

    public ToolState Tool
    {
        get;
        set
        {
            field.Clear();
            field = value;
        }
    } = new ToolState.Select();

    public EddyVisuals Visuals { get; set; } = EddyVisuals.Default;

    public TrileSet TrileSet { get; private set; } = null!;

    public InstanceActorRegistry Registry { get; private set; } = null!;

    public InstanceThumbnails Thumbnails { get; private set; } = null!;

    public ViewMode CurrentView { get; private set; } = ViewMode.Perspective;

    public int OverlapIndex
    {
        get;
        set
        {
            field = Math.Max(0, value);
            Selected = new SelectionState.Empty();
            VisualizeAllOverlappedTriles();
        }
    } // 0 is main layer

    public bool ShowAssetBrowser { get; set; }

    public bool ShowInstanceBrowser { get; set; }

    public bool ShowScriptBrowser { get; set; }

    public bool ShowProperties { get; set; }

    public bool ShowRaycastDebug { get; set; }

    public (FayAwayPreviewState Current, FayAwayPreviewState Previous) PreviewState { get; private set; } // Closed

    public ViewportFrame Frame { get; set; }

    public PickingState Picked { get; set; } = new PickingState.None();

    public SelectionState Selected { get; set; } = new SelectionState.Empty();

    public (InstanceId Instance, FaceOrientation Face)? Hovered { get; set; }

    public (InstanceId.Trile Trile, FaceOrientation Face)? HoveredTrile { get; set; }

    public PaintRotationMode TrilePaintRotationMode { get; set; } = new PaintRotationMode.Fixed(0);

    public AssetEntry? SelectedEntry { get; private set; }

    public IReadOnlyList<AssetEntry> RecentEntries => _recentEntries;

    private readonly Level _level;

    private readonly Clock _clock = new();

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private Actor _cursorActor = null!;

    private Actor _gizmoActor = null!;

    private PerspectiveState _savedPerspectiveState;

    private FarAwayPreviewSystem _farAwayPreviewer = null!;

    private readonly InputHints _inputHints = new();

    private readonly List<EddySystem> _tools = new();

    private readonly List<EddySystem> _instances = new();

    private readonly List<EddySystem> _interfaces = new();

    private readonly List<AssetEntry> _recentEntries = new();

    public EddyEditor(Game game, string title, Level level) : base(game, title)
    {
        _level = level;
        _level.Name = Path.GetFileName(title);
        History.Track(level);
        History.StateChanged += Sync;
        ResourceService.ProviderReset += OnProviderReset;
    }

    public override void LoadContent()
    {
        #region Setup a minimal 3D scene

        {
            _scene = new Scene(Game, ContentManager, _inputHints);

            _cameraActor = _scene.CreateActor();
            _cameraActor.Name = "Camera";

            var camera = _cameraActor.AddComponent<Camera>();
            var orientation = _cameraActor.AddComponent<OrientationGizmo>();
            _cameraActor.AddComponent<FirstPersonControl>();

            camera.Projection = Camera.ProjectionType.Perspective;
            camera.FieldOfView = 90f;
            camera.Near = 0.25f;
            camera.Far = 500f;
            orientation.UseFaceLabels = false;
        }

        #endregion

        #region Setup an actor for visual instances

        {
            var instancesActor = _scene.CreateActor();
            instancesActor.Name = "Instances";
            Registry = new InstanceActorRegistry(_scene, instancesActor);
        }

        #endregion

        #region Load a Trile Set

        {
            TrileSet = ResourceService.Load<TrileSet>("Trile Sets/" + _level.TrileSetName);
        }

        #endregion

        #region Setup instance thumbnails storage

        {
            Thumbnails = new InstanceThumbnails(ResourceService, _level, TrileSet);
            Thumbnails.LoadContent(ContentManager);
        }

        #endregion

        #region Instance systems

        {
            var camera = _cameraActor.GetComponent<Camera>();
            AddSystems(_instances,
                new SkySystem(_scene, camera, _clock),
                new LevelBoundsSystem(),
                new TrileSystem(),
                new CollisionMapSystem(),
                new ArtObjectSystem(),
                new BackgroundPlaneSystem(camera),
                new NpcSystem(),
                new GomezSystem(),
                new VolumeSystem(),
                new PathSystem(),
                new PickableBoundsSystem(),
                new LiquidSystem(),
                new RainSystem(camera),
                new LevelPropertiesSystem()
            );
        }

        #endregion

        #region Setup a cursor mesh

        {
            // In order to draw it properly,
            // it should be created after level geometry was created!
            _cursorActor = _scene.CreateActor();
            _cursorActor.Name = "Cursor";
            _cursorActor.AddComponent<CursorMesh>();
        }

        #endregion

        #region Setup an 3D gizmo

        {
            // Ditto
            _gizmoActor = _scene.CreateActor();
            _gizmoActor.Name = "Gizmo";
            var gizmo = _gizmoActor.AddComponent<Gizmo>();
            gizmo.Camera = _cameraActor.GetComponent<Camera>();
        }

        #endregion

        #region Interface systems

        {
            var orientation = _cameraActor.GetComponent<OrientationGizmo>();
            var gizmo = _gizmoActor.GetComponent<Gizmo>();
            var editors = Game.GetService<EditorService>();
            AddSystems(_interfaces,
                new ToolbarSystem(),
                _farAwayPreviewer = new FarAwayPreviewSystem(_scene, editors, _cameraActor),
                new ViewportSystem(_scene, _clock, orientation, gizmo),
                new InstanceInspectorSystem(),
                new AssetBrowserSystem(),
                new InstanceBrowserSystem(),
                new ScriptBrowserSystem()
            );
        }

        #endregion

        #region Tool systems

        {
            var cursor = _cursorActor.GetComponent<CursorMesh>();
            var gizmo = _gizmoActor.GetComponent<Gizmo>();
            AddSystems(_tools,
                new RaycastSystem(_scene, gizmo),
                new SelectionToolSystem(),
                new ClipboardSystem(),
                new PaintToolSystem(),
                new TrilePaintToolSystem(),
                new PickToolSystem(),
                new TranslateToolSystem(gizmo),
                new RotateToolSystem(gizmo),
                new ScaleToolSystem(gizmo),
                new CursorSystem(cursor)
            );
        }

        #endregion

        #region Camera's initial position

        {
            var fpc = _cameraActor.GetComponent<FirstPersonControl>();

            if (_level.StartingFace is { } startingFace)
            {
                var gomezPos = startingFace.Id.ToXna().ToVector3() + (Vector3.Up * 1.5f);
                fpc.FocusOn(gomezPos, startingFace.Face.AsVector(), 10f);
            }
            else
            {
                fpc.FocusOn(_level.Size.ToXna() / 2f, Vector3.Forward, 10f);
            }
        }

        #endregion
    }

    public override void Update(GameTime gameTime)
    {
        _inputHints.Clear();
        _gizmoActor.Visible = false;

        foreach (var system in _tools)
        {
            system.Update();
        }

        _clock.Tick(gameTime);
        _scene.Update(gameTime);
    }

    public override void Draw()
    {
        _farAwayPreviewer.BeforeDraw();

        foreach (var system in _interfaces)
        {
            system.Draw();
        }

        foreach (var system in _tools)
        {
            system.Draw();
        }
    }

    public override void Dispose()
    {
        ResourceService.ProviderReset -= OnProviderReset;

        foreach (var system in _tools
                     .Concat(_interfaces)
                     .Concat(_instances))
        {
            system.Dispose();
        }

        Thumbnails.Dispose();
        _scene.Dispose();
        base.Dispose();
    }

    public void SwitchToPerspective()
    {
        if (CurrentView == ViewMode.Perspective)
        {
            return;
        }

        CurrentView = ViewMode.Perspective;
        _cameraActor.RemoveComponent<MapPanControl>();
        _cameraActor.RemoveComponent<MapZoomControl>();

        _cameraActor.AddComponent<FirstPersonControl>();
        _cameraActor.Transform.Position = _savedPerspectiveState.Position;
        _cameraActor.Transform.Rotation = _savedPerspectiveState.Rotation;

        var camera = _cameraActor.GetComponent<Camera>();
        camera.Projection = Camera.ProjectionType.Perspective;
        camera.FieldOfView = 90f;
        camera.Offset = _savedPerspectiveState.Offset;
    }

    public void SwitchToOrtho(ViewMode mode, float yaw)
    {
        var camera = _cameraActor.GetComponent<Camera>();
        if (CurrentView == ViewMode.Perspective)
        {
            _savedPerspectiveState = new PerspectiveState(
                _cameraActor.Transform.Position,
                _cameraActor.Transform.Rotation,
                camera.Offset
            );
            _cameraActor.RemoveComponent<FirstPersonControl>();
        }
        else
        {
            _cameraActor.RemoveComponent<MapPanControl>();
            _cameraActor.RemoveComponent<MapZoomControl>();
        }

        CurrentView = mode;
        camera.Projection = Camera.ProjectionType.Orthographic;
        camera.Offset = new Vector3(0f, 0f, 400f);

        _cameraActor.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(yaw, 0f, 0f);
        _cameraActor.Transform.Position = _level.Size.ToXna() / 2f;

        _cameraActor.AddComponent<MapPanControl>();
        _cameraActor.AddComponent<MapZoomControl>().Reset();
    }

    public void VisualizeAll()
    {
        foreach (var instanceId in Registry.Instances)
        {
            Visualize(instanceId);
        }
    }

    private void VisualizeAllOverlappedTriles()
    {
        foreach (var (emplacement, trile) in _level.Triles)
        {
            if (trile.OverlappedTriles == null) continue;
            for (var i = 0; i < trile.OverlappedTriles.Count; i++)
            {
                var overlap = trile.OverlappedTriles[i];
                Visualize(new InstanceId.TrileOverlapChange(emplacement, i, overlap, overlap));
            }
        }
    }

    internal TrileInstance? GetActiveTrile(TrileEmplacement emplacement)
    {
        if (!_level.Triles.TryGetValue(emplacement, out var trile))
        {
            return null;
        }

        if (OverlapIndex == 0)
        {
            return trile.TrileId == InvalidId ? null : trile;
        }

        var slot = OverlapIndex - 1;
        return trile.OverlappedTriles != null &&
               slot < trile.OverlappedTriles.Count &&
               trile.OverlappedTriles[slot].TrileId != InvalidId
            ? trile.OverlappedTriles[slot]
            : null;
    }

    public void Visualize(InstanceId instanceId)
    {
        foreach (var system in _instances.Concat(_tools))
        {
            system.Visualize(instanceId);
        }
    }

    public void Inspect(params HashSet<InstanceId> instanceIds)
    {
        foreach (var system in _instances)
        {
            system.Inspect(instanceIds);
        }
    }

    public ToolState.Paint PickAndPaint(AssetEntry? entry)
    {
        SelectedEntry = entry;
        if (entry != null)
        {
            _recentEntries.Remove(entry);
            _recentEntries.Insert(0, entry);
            if (_recentEntries.Count > MaxRecentEntries)
            {
                _recentEntries.RemoveAt(_recentEntries.Count - 1);
            }
        }

        return entry switch
        {
            AssetEntry.Trile t => new ToolState.Paint.Trile(t.Name, t.Id),
            AssetEntry.ArtObject ao => new ToolState.Paint.ArtObject(ao.Name),
            AssetEntry.BackgroundPlane bp => new ToolState.Paint.BackgroundPlane(bp.Name),
            AssetEntry.NonPlayableCharacter npc => new ToolState.Paint.NonPlayableCharacter(npc.Name),
            _ => new ToolState.Paint.None()
        };
    }

    public bool IsToolEnabled(ToolState tool)
    {
        return tool is ToolState.Select or ToolState.Paint or ToolState.Pick ||
               _tools.Any(system => system.IsToolEnabled(tool));
    }

    public void SetPreviewState(FayAwayPreviewState state)
    {
        if (PreviewState.Current != state)
        {
            PreviewState = (state, PreviewState.Current);
        }
    }

    public void ConsumePreviewStateTransition()
    {
        PreviewState = (PreviewState.Current, PreviewState.Current);
    }

    private void Sync(History.Change change)
    {
        foreach (var instanceId in LevelDifference.Get(change))
        {
            Visualize(instanceId);
        }
    }

    private void OnProviderReset()
    {
        SelectedEntry = null;
        _recentEntries.Clear();
    }

    private void AddSystems(List<EddySystem> collection, params EddySystem[] systems)
    {
        foreach (var system in systems)
        {
            system.Game = Game;
            system.Eddy = this;
            system.Level = _level;
            system.Resources = ResourceService;
            system.Rendering = RenderingService;
            system.Input = InputService;
            system.Storage = StorageService;
            system.Hints = _inputHints;
            system.Content = ContentManager;
            system.Initialize();
            collection.Add(system);
        }
    }

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
            if (!trile.Geometry.IsNullOrEmpty())
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

    private readonly record struct PerspectiveState(Vector3 Position, Quaternion Rotation, Vector3 Offset);
}