using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class BackgroundPlaneContext : BaseContext
{
    private readonly Dictionary<int, Actor> _bgPlaneActors = new();

    public BackgroundPlaneContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.Context != EddyContext.BackgroundPlane && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Background Planes

        foreach (var (id, bgPlane) in Level.BackgroundPlanes.Where(kv => kv.Key != InvalidId))
        {
            var actor = Eddy.Scene.CreateActor();
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

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.BackgroundPlane;
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        foreach (var actor in _bgPlaneActors.Values)
        {
            Eddy.Scene.DestroyActor(actor);
        }

        _bgPlaneActors.Clear();
    }
}