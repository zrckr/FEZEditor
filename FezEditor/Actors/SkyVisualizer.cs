using FezEditor.Structure;
using FEZRepacker.Core.Definitions.Game.Sky;

namespace FezEditor.Actors;

public struct SkyVisualizer
{
    private readonly Scene _scene;

    private readonly Clock _clock;

    private Actor? _background;

    public SkyVisualizer(Scene scene, Clock clock)
    {
        _scene = scene;
        _clock = clock;
    }

    public void Visualize(Sky sky)
    {
        #region Background

        _background = _scene.CreateActor();
        var mesh = _background.AddComponent<SkyBackgroundMesh>();
        mesh.Clock = _clock;
        mesh.Visualize(sky.Name, sky.Background);

        #endregion
    }
}