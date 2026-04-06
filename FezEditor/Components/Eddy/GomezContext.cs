using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class GomezContext : BaseContext
{
    private Actor? _gomezActor;

    public GomezContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.Context != EddyContext.Gomez && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Gomez

        {
            _gomezActor = Eddy.Scene.CreateActor();
            _gomezActor.Name = "Gomez";
            _gomezActor.Transform.Position = Level.StartingFace.Id.ToXna().ToVector3() + Vector3.Up;
            _gomezActor.Transform.Rotation = Level.StartingFace.Face.AsQuaternion();

            var mesh = _gomezActor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations("Character Animations/Gomez");
            mesh.Visualize(animations, "IdleWink");

            var bounds = _gomezActor.AddComponent<BoundsMesh>();
            bounds.Size = Vector3.One;
            bounds.WireColor = Color.Red;
        }

        #endregion
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Gomez;
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        if (_gomezActor != null)
        {
            Eddy.Scene.DestroyActor(_gomezActor);
            _gomezActor = null;
        }
    }
}