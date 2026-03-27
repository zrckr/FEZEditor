using FezEditor.Actors;
using FezEditor.Tools;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class GomezContext : EddyContext
{
    private Actor? _gomezActor;

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

        #region Gomez

        {
            _gomezActor = Scene.CreateActor();
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

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        if (_gomezActor != null)
        {
            Scene.DestroyActor(_gomezActor);
            _gomezActor = null;
        }
    }
}