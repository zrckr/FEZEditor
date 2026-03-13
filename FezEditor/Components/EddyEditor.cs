using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components;

public class EddyEditor : EditorComponent
{
    private const int InvalidId = -1;

    public override object Asset => _level;

    private readonly Level _level;

    private Scene _scene = null!;

    private Actor _cameraActor = null!;

    private readonly Clock _clock = new();

    private readonly Dictionary<int, Actor> _trileActors = new();

    private readonly Dictionary<int, HashSet<TrileEmplacement>> _groups = new();

    public EddyEditor(Game game, string title, Level level) : base(game, title)
    {
        _level = level;
        History.Track(level);
    }

    public override void Update(GameTime gameTime)
    {
        _clock.Tick(gameTime);
        _scene.Update(gameTime);
    }

    public override void LoadContent()
    {
        _scene = new Scene(Game, ContentManager);
        _scene.Lighting.Ambient = Color.Gray;
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

        RevisualizeLevel();

        var position = _level.StartingFace.Id.ToXna().ToVector3();
        position += (Vector3.Up * 1.5f) + _level.StartingFace.Face.AsVector() * 10f;
        _cameraActor.Transform.Position = position;
    }

    public override void Draw()
    {
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

                var imageMin = ImGuiX.GetItemRectMin();
                var gizmo = _cameraActor.GetComponent<OrientationGizmo>();
                gizmo.UseFaceLabels = true;
                gizmo.Draw(imageMin + new Vector2(size.X - 8f, 8f));
                ImGuiX.DrawStats(imageMin + new Vector2(8, 8), RenderingService.GetStats());
                var topCenter = imageMin + new Vector2(size.X / 2f, 8f);
                ImGuiX.DrawTime(topCenter, _clock.CurrentTime);
            }
        }
    }

    public override void Dispose()
    {
        _scene.Dispose();
        base.Dispose();
    }

    private void RevisualizeLevel()
    {
        #region Sky

        // TODO: Implement Sky rendering

        #endregion

        #region Level bounds

        {
            var actor = _scene.CreateActor();
            actor.Name = "Level Bounds";

            var mesh = actor.AddComponent<BoundsMesh>();
            mesh.Size = _level.Size.ToXna();
        }

        #endregion

        #region Liquid

        if (_level.WaterType != LiquidType.None)
        {
            var actor = _scene.CreateActor();
            actor.Name = $"Water: {_level.WaterType}";

            var mesh = actor.AddComponent<LiquidMesh>();
            mesh.Visualize(_level.WaterType, _level.WaterHeight, _level.Size.ToXna());
        }

        #endregion

        #region Triles

        var trileSet = (TrileSet)ResourceService.Load($"Trile Sets/{_level.TrileSetName}");
        foreach (var id in _level.Triles.Values.Select(ti => ti.TrileId).Where(id => id != InvalidId).Distinct())
        {
            var actor = _scene.CreateActor();
            actor.Name = $"{id}: {trileSet.Triles[id].Name}";
            _trileActors.Add(id, actor);

            var mesh = actor.AddComponent<TrilesMesh>();
            mesh.Visualize(trileSet, id);
        }

        foreach (var (emplacement, instance) in _level.Triles.Where(kv => kv.Value.TrileId != InvalidId))
        {
            var actor = _trileActors[instance.TrileId];
            var mesh = actor.GetComponent<TrilesMesh>();
            mesh.SetInstanceData(emplacement, instance.Position.ToXna(), instance.PhiLight);
        }

        #endregion

        #region Collision Map

        {
            var actor = _scene.CreateActor();
            var collision = actor.AddComponent<TrileCollisionMesh>();
            foreach (var instance in _level.Triles.Values.Where(ti => ti.TrileId != InvalidId))
            {
                var trile = trileSet.Triles[instance.TrileId];
                collision.AddInstanceData(instance.Position.ToXna(), trile.Faces, trile.Size.ToXna());
            }
        }

        #endregion

        #region Trile Groups

        _groups.Clear();
        foreach (var (id, group) in _level.Groups.Where(kv => kv.Key != InvalidId))
        {
            // See comment in FEZRepacker.Core.Definitions.Json.TrileGroupJsonModel
            _groups[id] = group.Triles.Select(ti => new TrileEmplacement(ti.Position)).ToHashSet();

            // TODO: Highlight group for now, selection will be implemented later
            foreach (var actor in _trileActors.Values)
            {
                var mesh = actor.GetComponent<TrilesMesh>();
                mesh.SetSelectedInstances(_groups[id]);
            }

            if (group.Path is { Segments.Count: >= 2 })
            {
                var actor = _scene.CreateActor();
                actor.Name = $"{id}: Group Path ({group.ActorType})";

                var segments = group.Path.Segments.Select(ps => ps.Destination.ToXna()).ToArray();
                var mesh = actor.AddComponent<PathMesh>();
                mesh.Visualize(segments);
            }
        }

        #endregion

        #region ArtObjects

        foreach (var (id, instance) in _level.ArtObjects.Where(kv => kv.Key != InvalidId))
        {
            var actor = _scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            actor.Transform.Rotation = instance.Rotation.ToXna();
            actor.Transform.Scale = instance.Scale.ToXna();

            var mesh = actor.AddComponent<ArtObjectMesh>();
            var ao = (ArtObject)ResourceService.Load($"Art Objects/{instance.Name}");
            mesh.Visualize(ao);
        }

        #endregion

        #region Background Planes

        foreach (var (id, bgPlane) in _level.BackgroundPlanes.Where(kv => kv.Key != InvalidId))
        {
            var actor = _scene.CreateActor();
            actor.Name = $"{id}: {bgPlane.TextureName}";
            actor.Transform.Position = bgPlane.Position.ToXna();
            actor.Transform.Rotation = bgPlane.Rotation.ToXna();
            actor.Transform.Scale = bgPlane.Scale.ToXna();

            var mesh = actor.AddComponent<BackgroundPlaneMesh>();
            mesh.Billboard = bgPlane.Billboard;
            mesh.DoubleSided = bgPlane.Doublesided;
            mesh.Color = bgPlane.Filter.ToXna();
            mesh.Opacity = bgPlane.Opacity;

            var asset = ResourceService.Load($"Background Planes/{bgPlane.TextureName}");
            mesh.Visualize(asset);
        }

        #endregion

        #region Non-Playable Characters

        foreach (var (id, instance) in _level.NonPlayerCharacters.Where(kv => kv.Key != InvalidId))
        {
            var actor = _scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();

            var mesh = actor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations($"Character Animations/{instance.Name}");
            mesh.Visualize(animations);
        }

        #endregion

        #region Gomez

        {
            var actor = _scene.CreateActor();
            actor.Name = "Gomez";
            actor.Transform.Position = _level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
            actor.Transform.Rotation = _level.StartingFace.Face.AsQuaternion();

            var mesh = actor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations("Character Animations/Gomez");
            mesh.Visualize(animations, "IdleWink");

            var bounds = actor.AddComponent<BoundsMesh>();
            bounds.Size = Vector3.One;
            bounds.WireColor = Color.Red;
        }

        #endregion

        #region Volumes

        foreach (var (id, volume) in _level.Volumes.Where(kv => kv.Key != InvalidId))
        {
            var actor = _scene.CreateActor();
            actor.Name = $"{id}: Volume";

            var mesh = actor.AddComponent<VolumeMesh>();
            mesh.Visualize(volume.From.ToXna(), volume.To.ToXna());
        }

        #endregion

        #region Paths

        foreach (var (id, path) in _level.Paths.Where(kv => kv.Key != InvalidId))
        {
            var actor = _scene.CreateActor();
            actor.Name = $"{id}: Path";

            var segments = path.Segments.Select(ps => ps.Destination.ToXna()).ToArray();
            var mesh = actor.AddComponent<PathMesh>();
            mesh.Visualize(segments);
        }

        #endregion
    }
}