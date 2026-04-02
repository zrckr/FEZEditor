using FezEditor.Actors;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class NpcContext : EddyContext
{
    private readonly Dictionary<int, Actor> _npcActors = new();

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            return;
        }

        TeardownVisualization();

        #region Non-Playable Characters

        foreach (var (id, instance) in Level.NonPlayerCharacters.Where(kv => kv.Key != InvalidId))
        {
            var actor = Scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            _npcActors[id] = actor;

            var mesh = actor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations($"Character Animations/{instance.Name}");
            mesh.Visualize(animations);
        }

        #endregion
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var npcActor in _npcActors.Values)
        {
            Scene.DestroyActor(npcActor);
        }

        _npcActors.Clear();
    }
}