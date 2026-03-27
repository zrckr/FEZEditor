using FezEditor.Actors;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class PathContext : EddyContext
{
    private readonly Dictionary<int, Actor> _pathActors = new();

    public override bool Pick(Ray ray)
    {
        return false;
    }

    public override void Update()
    {
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            return;
        }

        TeardownVisualization();

        #region Paths

        foreach (var (id, path) in Level.Paths.Where(kv => kv.Key != InvalidId))
        {
            var actor = Scene.CreateActor();
            actor.Name = $"{id}: Path";
            _pathActors[id] = actor;

            var segments = path.Segments.Select(ps => ps.Destination.ToXna()).ToArray();
            var mesh = actor.AddComponent<PathMesh>();
            mesh.Visualize(segments);
        }

        #endregion
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _pathActors.Values)
        {
            Scene.DestroyActor(actor);
        }

        _pathActors.Clear();
    }
}