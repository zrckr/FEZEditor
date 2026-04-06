using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class NpcContext : BaseContext
{
    private readonly Dictionary<int, Actor> _npcActors = new();

    public NpcContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.Context != EddyContext.NonPlayableCharacter && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Non-Playable Characters

        foreach (var (id, instance) in Level.NonPlayerCharacters.Where(kv => kv.Key != InvalidId))
        {
            var actor = Eddy.Scene.CreateActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            _npcActors[id] = actor;

            var mesh = actor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations($"Character Animations/{instance.Name}");
            mesh.Visualize(animations);
        }

        #endregion
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.NonPlayableCharacter;
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var npcActor in _npcActors.Values)
        {
            Eddy.Scene.DestroyActor(npcActor);
        }

        _npcActors.Clear();
    }
}