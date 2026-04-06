using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class PathContext : BaseContext
{
    private readonly Dictionary<int, Actor> _pathActors = new();

    public PathContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.Context != EddyContext.Path && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Paths

        foreach (var (id, path) in Level.Paths.Where(kv => kv.Key != InvalidId))
        {
            var actor = Eddy.Scene.CreateActor();
            actor.Name = $"{id}: Path";
            _pathActors[id] = actor;

            var segments = path.Segments.Select(ps => ps.Destination.ToXna()).ToArray();
            var mesh = actor.AddComponent<PathMesh>();
            mesh.Visualize(segments);
        }

        #endregion
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Path;
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _pathActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _pathActors.Clear();
    }
}