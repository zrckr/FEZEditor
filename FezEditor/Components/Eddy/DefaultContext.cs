using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.Sky;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class DefaultContext : BaseContext
{
    private Sky? _sky;

    private Actor? _skyActor;

    private Actor? _boundsActor;

    private Actor? _liquidActor;

    private Actor? _pickablesActor;

    public DefaultContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        if (Eddy.Context == EddyContext.Default)
        {
            // Nobody claimed it, stay default
        }

        if (Eddy.ShowPickableBounds.IsDirty)
        {
            var bounds = _pickablesActor?.GetComponent<PickableBounds>();
            var actors = Eddy.ShowPickableBounds.Value
                ? Eddy.Scene.GetChildren(Eddy.Scene.Root)
                : Enumerable.Empty<Actor>();

            bounds?.Visualize(actors);
            Eddy.ShowPickableBounds = Eddy.ShowPickableBounds.Clean();
        }

        if (_skyActor != null)
        {
            var visualizer = _skyActor.GetComponent<SkyVisualizer>();
            var actualAmbient = new Color(Level.BaseAmbient, Level.BaseAmbient, Level.BaseAmbient);
            var actualDiffuse = new Color(Level.BaseDiffuse, Level.BaseDiffuse, Level.BaseDiffuse);

            if (Eddy.Clock.NightContribution != 0f)
            {
                actualDiffuse = Color.Lerp(actualDiffuse, visualizer.FogColor, Eddy.Clock.NightContribution * 0.4f);
                actualAmbient = Color.Lerp(actualAmbient, visualizer.FoliageShadows
                    ? Color.Lerp(visualizer.FogColor, Color.White, 0.5f)
                    : Color.White, Eddy.Clock.NightContribution * 0.5f);
            }

            actualAmbient = Color.Lerp(actualAmbient, visualizer.FogColor, 23f / 160f);

            Eddy.Scene.Lighting.Ambient = actualAmbient;
            Eddy.Scene.Lighting.Diffuse = actualDiffuse;
        }
    }

    public override void Revisualize(bool partial = false)
    {
        if (Eddy.Context != EddyContext.Default && partial)
        {
            return;
        }

        TeardownVisualization();

        #region Sky

        _sky = (Sky)ResourceService.Load($"Skies/{Level.SkyName}");
        Eddy.Scene.Lighting.Ambient = Color.White * Level.BaseAmbient;
        Eddy.Scene.Lighting.Diffuse = Color.White * Level.BaseDiffuse;

        {
            _skyActor = Eddy.Scene.CreateActor();
            _skyActor.Name = "Sky";
            var visualizer = _skyActor.AddComponent<SkyVisualizer>();
            visualizer.Initialize(Eddy.Scene, Eddy.Camera, Eddy.Clock);
            visualizer.LevelSize = Level.Size.ToXna();
            visualizer.Visualize(_sky);
            visualizer.VisualizeShadows(_sky.Name, _sky.Shadows);
        }

        #endregion

        #region Level bounds

        {
            _boundsActor = Eddy.Scene.CreateActor();
            _boundsActor.Name = "Level Bounds";

            var mesh = _boundsActor.AddComponent<BoundsMesh>();
            mesh.Size = Level.Size.ToXna();
        }

        #endregion

        #region Liquid

        if (Level.WaterType != LiquidType.None)
        {
            _liquidActor = Eddy.Scene.CreateActor();
            _liquidActor.Name = $"Water: {Level.WaterType}";

            var mesh = _liquidActor.AddComponent<LiquidMesh>();
            mesh.Visualize(Level.WaterType, Level.WaterHeight, Level.Size.ToXna());
        }

        #endregion

        #region Pickable Bounds

        _pickablesActor = Eddy.Scene.CreateActor();
        _pickablesActor.Name = "Debug";

        var bounds = _pickablesActor.AddComponent<PickableBounds>();
        bounds.WireColor = Color.Purple;

        #endregion
    }

    public void PostRevisualize()
    {
        #region Cloud Shadows

        if (_skyActor?.TryGetComponent<SkyVisualizer>(out var visualizer) ?? false)
        {
            visualizer?.VisualizeShadows(_sky!.Name, _sky.Shadows);
        }

        #endregion

        #region Pickable Bounds

        if (_pickablesActor?.HasComponent<PickableBounds>() ?? false)
        {
            _pickablesActor = Eddy.Scene.CreateActor();
            _pickablesActor.Name = "Debug";

            var bounds = _pickablesActor.AddComponent<PickableBounds>();
            bounds.WireColor = Color.Purple;
        }

        #endregion
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Default;
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        if (_pickablesActor != null)
        {
            Eddy.Scene.DestroyActor(_pickablesActor);
            _pickablesActor = null;
        }

        if (_liquidActor != null)
        {
            Eddy.Scene.DestroyActor(_liquidActor);
            _liquidActor = null;
        }

        if (_boundsActor != null)
        {
            Eddy.Scene.DestroyActor(_boundsActor);
            _boundsActor = null;
        }

        if (_skyActor != null)
        {
            Eddy.Scene.DestroyActor(_skyActor);
            _skyActor = null;
        }
    }
}
