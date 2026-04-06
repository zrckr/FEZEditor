using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class VolumeContext : BaseContext
{
    private readonly Dictionary<int, Actor> _volumeActors = new();

    public VolumeContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.Context != EddyContext.Volume && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Volumes

        foreach (var (id, volume) in Level.Volumes.Where(kv => kv.Key != InvalidId))
        {
            var actor = Eddy.Scene.CreateActor();
            actor.Name = $"{id}: Volume";
            _volumeActors[id] = actor;

            var mesh = actor.AddComponent<VolumeMesh>();
            mesh.Visualize(volume.From.ToXna(), volume.To.ToXna());
        }

        #endregion
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Volume;
    }

    public override void Dispose()
    {
        TeardownVisualization();
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