using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.Sky;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class DefaultEddyContext : EddyContext
{
    public Clock Clock { get; set; } = null!;

    public Dirty<bool> ShowPickableBounds { get; set; } = new(false);

    private Sky? _sky;

    private Actor? _skyActor;

    private Actor? _boundsActor;

    private Actor? _liquidActor;

    private Actor? _pickablesActor;

    public override bool Pick(Ray ray)
    {
        throw new InvalidOperationException("Pickable is invalid in default context");
    }

    public override void Update()
    {
        if (ShowPickableBounds.IsDirty)
        {
            var bounds = _pickablesActor?.GetComponent<PickableBounds>();
            var actors = ShowPickableBounds.Value
                ? Scene.GetChildren(Scene.Root)
                : Enumerable.Empty<Actor>();

            bounds?.Visualize(actors);
            ShowPickableBounds = ShowPickableBounds.Clean();
        }

        Cursor.ClearHover();
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            return;
        }

        TeardownVisualization();

        #region Sky

        _sky = (Sky)ResourceService.Load($"Skies/{Level.SkyName}");
        Scene.Lighting.Ambient = Color.White * Level.BaseAmbient;
        Scene.Lighting.Diffuse = Color.White * Level.BaseDiffuse;

        {
            _skyActor = Scene.CreateActor();
            _skyActor.Name = "Sky";
            var visualizer = _skyActor.AddComponent<SkyVisualizer>();
            visualizer.Initialize(Scene, Camera, Clock);
            visualizer.LevelSize = Level.Size.ToXna();
            visualizer.Visualize(_sky);
            visualizer.VisualizeShadows(_sky.Name, _sky.Shadows);
        }

        #endregion

        #region Level bounds

        {
            _boundsActor = Scene.CreateActor();
            _boundsActor.Name = "Level Bounds";

            var mesh = _boundsActor.AddComponent<BoundsMesh>();
            mesh.Size = Level.Size.ToXna();
        }

        #endregion

        #region Liquid

        if (Level.WaterType != LiquidType.None)
        {
            _liquidActor = Scene.CreateActor();
            _liquidActor.Name = $"Water: {Level.WaterType}";

            var mesh = _liquidActor.AddComponent<LiquidMesh>();
            mesh.Visualize(Level.WaterType, Level.WaterHeight, Level.Size.ToXna());
        }

        #endregion

        #region Pickable Bounds

        _pickablesActor = Scene.CreateActor();
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
            _pickablesActor = Scene.CreateActor();
            _pickablesActor.Name = "Debug";

            var bounds = _pickablesActor.AddComponent<PickableBounds>();
            bounds.WireColor = Color.Purple;
        }

        #endregion
    }

    public void UpdateLighting()
    {
        if (_skyActor == null)
        {
            return;
        }

        var visualizer = _skyActor.GetComponent<SkyVisualizer>();
        var actualAmbient = new Color(Level.BaseAmbient, Level.BaseAmbient, Level.BaseAmbient);
        var actualDiffuse = new Color(Level.BaseDiffuse, Level.BaseDiffuse, Level.BaseDiffuse);

        if (Clock.NightContribution != 0f)
        {
            actualDiffuse = Color.Lerp(actualDiffuse, visualizer.FogColor, Clock.NightContribution * 0.4f);
            actualAmbient = Color.Lerp(actualAmbient, visualizer.FoliageShadows
                ? Color.Lerp(visualizer.FogColor, Color.White, 0.5f)
                : Color.White, Clock.NightContribution * 0.5f);
        }

        actualAmbient = Color.Lerp(actualAmbient, visualizer.FogColor, 23f / 160f);

        Scene.Lighting.Ambient = actualAmbient;
        Scene.Lighting.Diffuse = actualDiffuse;
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        if (_pickablesActor != null)
        {
            Scene.DestroyActor(_pickablesActor);
            _pickablesActor = null;
        }

        if (_liquidActor != null)
        {
            Scene.DestroyActor(_liquidActor);
            _liquidActor = null;
        }

        if (_boundsActor != null)
        {
            Scene.DestroyActor(_boundsActor);
            _boundsActor = null;
        }

        if (_skyActor != null)
        {
            Scene.DestroyActor(_skyActor);
            _skyActor = null;
        }
    }
}
