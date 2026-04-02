using FezEditor.Actors;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class VolumeContext : EddyContext
{
    private readonly Dictionary<int, Actor> _volumeActors = new();

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            return;
        }

        TeardownVisualization();

        #region Volumes

        foreach (var (id, volume) in Level.Volumes.Where(kv => kv.Key != InvalidId))
        {
            var actor = Scene.CreateActor();
            actor.Name = $"{id}: Volume";
            _volumeActors[id] = actor;

            var mesh = actor.AddComponent<VolumeMesh>();
            mesh.Visualize(volume.From.ToXna(), volume.To.ToXna());
        }

        #endregion
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _volumeActors.Values)
        {
            Scene.DestroyActor(actor);
        }

        _volumeActors.Clear();
    }
}