using FezEditor.Actors;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class BackgroundPlaneContext : EddyContext
{
    private readonly Dictionary<int, Actor> _bgPlaneActors = new();

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            return;
        }

        TeardownVisualization();

        #region Background Planes

        foreach (var (id, bgPlane) in Level.BackgroundPlanes.Where(kv => kv.Key != InvalidId))
        {
            var actor = Scene.CreateActor();
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

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _bgPlaneActors.Values)
        {
            Scene.DestroyActor(actor);
        }

        _bgPlaneActors.Clear();
    }
}