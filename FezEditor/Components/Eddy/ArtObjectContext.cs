using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.ArtObject;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class ArtObjectContext : EddyContext
{
    private readonly Dictionary<int, Actor> _artObjectActors = new();

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

        #region ArtObjects

        foreach (var (id, instance) in Level.ArtObjects.Where(kv => kv.Key != InvalidId))
        {
            var actor = Scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            actor.Transform.Rotation = instance.Rotation.ToXna();
            actor.Transform.Scale = instance.Scale.ToXna();
            _artObjectActors[id] = actor;

            var mesh = actor.AddComponent<ArtObjectMesh>();
            var ao = (ArtObject)ResourceService.Load($"Art Objects/{instance.Name}");
            mesh.Visualize(ao);
        }

        #endregion
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _artObjectActors.Values)
        {
            Scene.DestroyActor(actor);
        }

        _artObjectActors.Clear();
    }
}