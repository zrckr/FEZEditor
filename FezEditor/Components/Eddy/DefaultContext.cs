using FezEditor.Actors;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.Sky;
using ImGuiNET;
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
        if (Eddy.Visuals.IsDirty)
        {
            if (_pickablesActor?.TryGetComponent<PickableBounds>(out var bounds) ?? false)
            {
                var actors = Eddy.Visuals.Value.HasFlag(EddyVisuals.PickableBounds)
                    ? Eddy.Scene.GetChildren(Eddy.Scene.Root)
                    : Enumerable.Empty<Actor>();

                var children = actors
                    .SelectMany(a => Eddy.Scene.GetChildren(a));

                bounds?.Visualize(children);
            }

            if (_skyActor?.HasComponent<SkyVisualizer>() ?? false)
            {
                _skyActor.Visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.Sky);
            }

            if (_liquidActor?.HasComponent<LiquidMesh>() ?? false)
            {
                _liquidActor.Visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.Liquid);
            }
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

    public override void DrawProperties()
    {
        if (Eddy.SelectedContext != EddyContext.Default)
        {
            return;
        }

        var name = Level.Name;
        if (ImGui.InputText("Name", ref name, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Name"))
            {
                Level.Name = name;
            }
        }

        var size = Level.Size.ToXna();
        if (ImGuiX.InputFloat3("Size", ref size))
        {
            using (Eddy.History.BeginScope("Edit Level Size"))
            {
                Level.Size = size.ToRepacker();
            }
        }

        var sequenceSamplesPath = Level.SequenceSamplesPath;
        if (ImGui.InputText("Sequence Samples Path", ref sequenceSamplesPath, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Sequence Samples Path"))
            {
                Level.SequenceSamplesPath = sequenceSamplesPath;
            }
        }

        var flat = Level.Flat;
        if (ImGui.Checkbox("Flat", ref flat))
        {
            using (Eddy.History.BeginScope("Edit Level Flat"))
            {
                Level.Flat = flat;
            }
        }

        var skipPostProcess = Level.SkipPostProcess;
        if (ImGui.Checkbox("Skip Post Process", ref skipPostProcess))
        {
            using (Eddy.History.BeginScope("Edit Level Skip Post Process"))
            {
                Level.SkipPostProcess = skipPostProcess;
            }
        }

        var baseDiffuse = Level.BaseDiffuse;
        if (ImGui.InputFloat("Base Diffuse", ref baseDiffuse))
        {
            using (Eddy.History.BeginScope("Edit Level Base Diffuse"))
            {
                Level.BaseDiffuse = baseDiffuse;
            }
        }

        var baseAmbient = Level.BaseAmbient;
        if (ImGui.InputFloat("Base Ambient", ref baseAmbient))
        {
            using (Eddy.History.BeginScope("Edit Level Base Ambient"))
            {
                Level.BaseAmbient = baseAmbient;
            }
        }

        var gomezHaloName = Level.GomezHaloName;
        if (ImGui.InputText("Gomez Halo Name", ref gomezHaloName, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Gomez Halo Name"))
            {
                Level.GomezHaloName = gomezHaloName;
            }
        }

        var haloFiltering = Level.HaloFiltering;
        if (ImGui.Checkbox("Halo Filtering", ref haloFiltering))
        {
            using (Eddy.History.BeginScope("Edit Level Halo Filtering"))
            {
                Level.HaloFiltering = haloFiltering;
            }
        }

        var blinkingAlpha = Level.BlinkingAlpha;
        if (ImGui.Checkbox("Blinking Alpha", ref blinkingAlpha))
        {
            using (Eddy.History.BeginScope("Edit Level Blinking Alpha"))
            {
                Level.BlinkingAlpha = blinkingAlpha;
            }
        }

        var loops = Level.Loops;
        if (ImGui.Checkbox("Loops", ref loops))
        {
            using (Eddy.History.BeginScope("Edit Level Loops"))
            {
                Level.Loops = loops;
            }
        }

        var waterType = (int)Level.WaterType;
        var waterTypes = Enum.GetNames<LiquidType>();

        if (ImGui.Combo("Water Type", ref waterType, waterTypes, waterTypes.Length))
        {
            using (Eddy.History.BeginScope("Edit Level Water Type"))
            {
                Level.WaterType = (LiquidType)waterType;
            }
        }

        var waterHeight = Level.WaterHeight;
        if (ImGui.DragFloat("Water Height", ref waterHeight))
        {
            using (Eddy.History.BeginScope("Edit Level Water Height"))
            {
                Level.WaterHeight = waterHeight;
            }
        }

        ImGui.LabelText("Sky Name", Level.SkyName);
        ImGui.SameLine();
        if (ImGui.Button("...##SkyPick"))
        {
            var skiesDir = ResourceService.GetFullPath("Skies");
            var options = new FileDialog.Options
            {
                Title = "Select Sky",
                DefaultLocation = skiesDir,
                Filters = [new FileDialog.Filter("Sky", "fezsky.json")]
            };

            FileDialog.Show(FileDialog.Type.OpenFile, files =>
            {
                if (files.Length == 0)
                {
                    return;
                }

                var picked = Path.GetFileName(files[0]).Replace(".fezsky.json", "");
                using (Eddy.History.BeginScope("Edit Level Sky Name"))
                {
                    Level.SkyName = picked;
                }
            }, options);
        }

        var songName = Level.SongName;
        if (ImGui.InputText("Song Name", ref songName, 255))
        {
            using (Eddy.History.BeginScope("Edit Level Song Name"))
            {
                Level.SongName = songName;
            }
        }

        var fapFadeOutStart = Level.FAPFadeOutStart;
        if (ImGui.InputInt("FAP Fade Out Start", ref fapFadeOutStart))
        {
            using (Eddy.History.BeginScope("Edit Level FAP Fade Out Start"))
            {
                Level.FAPFadeOutStart = fapFadeOutStart;
            }
        }

        var fapFadeOutLength = Level.FAPFadeOutLength;
        if (ImGui.InputInt("FAP Fade Out Length", ref fapFadeOutLength))
        {
            using (Eddy.History.BeginScope("Edit Level FAP Fade Out Length"))
            {
                Level.FAPFadeOutLength = fapFadeOutLength;
            }
        }

        var descending = Level.Descending;
        if (ImGui.Checkbox("Descending", ref descending))
        {
            using (Eddy.History.BeginScope("Edit Level Descending"))
            {
                Level.Descending = descending;
            }
        }

        var rainy = Level.Rainy;
        if (ImGui.Checkbox("Rainy", ref rainy))
        {
            using (Eddy.History.BeginScope("Edit Level Rainy"))
            {
                Level.Rainy = rainy;
            }
        }

        var lowPass = Level.LowPass;
        if (ImGui.Checkbox("Low Pass", ref lowPass))
        {
            using (Eddy.History.BeginScope("Edit Level Low Pass"))
            {
                Level.LowPass = lowPass;
            }
        }

        var mutedLoops = Level.MutedLoops;
        if (ImGuiX.EditableList("Muted Loops", ref mutedLoops, RenderLoops, () => ""))
        {
            using (Eddy.History.BeginScope("Edit Level Muted Loops"))
            {
                Level.MutedLoops = mutedLoops;
            }
        }

        var ambienceTracks = Level.AmbienceTracks;
        if (ImGuiX.EditableList("Ambience Tracks", ref ambienceTracks, RenderTracks, () => new AmbienceTrack()))
        {
            using (Eddy.History.BeginScope("Edit Level Ambience Tracks"))
            {
                Level.AmbienceTracks = ambienceTracks;
            }
        }

        var nodeType = (int)Level.NodeType;
        var nodeTypes = Enum.GetNames<LevelNodeType>();

        if (ImGui.Combo("Node Type", ref nodeType, nodeTypes, nodeTypes.Length))
        {
            using (Eddy.History.BeginScope("Edit Level Node Type"))
            {
                Level.NodeType = (LevelNodeType)nodeType;
            }
        }

        var quantum = Level.Quantum;
        if (ImGui.Checkbox("Quantum", ref quantum))
        {
            using (Eddy.History.BeginScope("Edit Level Quantum"))
            {
                Level.Quantum = quantum;
            }
        }
    }

    private static bool RenderLoops(int index, ref string item)
    {
        return ImGui.InputText("##loop" + index, ref item, 255);
    }

    private static bool RenderTracks(int index, ref AmbienceTrack item)
    {
        ImGui.TextDisabled(index + ":");

        var name = item.Name;
        if (ImGui.InputText("Name##name" + index, ref name, 255))
        {
            item.Name = name;
            return true;
        }

        var day = item.Day;
        if (ImGui.Checkbox("Day##day" + index, ref day))
        {
            item.Day = day;
            return true;
        }

        var dusk = item.Dusk;
        ImGui.SameLine();
        if (ImGui.Checkbox("Dusk##dusk" + index, ref dusk))
        {
            item.Dusk = dusk;
            return true;
        }

        var night = item.Night;
        ImGui.SameLine();
        if (ImGui.Checkbox("Night##night" + index, ref night))
        {
            item.Night = night;
            return true;
        }

        var dawn = item.Dawn;
        ImGui.SameLine();
        if (ImGui.Checkbox("Dawn##dawn" + index, ref dawn))
        {
            item.Dawn = dawn;
            return true;
        }

        return false;
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            if (_boundsActor!.TryGetComponent<BoundsMesh>(out var boundsMesh))
            {
                boundsMesh!.Size = Level.Size.ToXna();
            }

            if (Eddy.SelectedContext != EddyContext.Default)
            {
                return;
            }

            _sky = (Sky)ResourceService.Load($"Skies/{Level.SkyName}");
            Eddy.Scene.Lighting.Ambient = Color.White * Level.BaseAmbient;
            Eddy.Scene.Lighting.Diffuse = Color.White * Level.BaseDiffuse;

            if (_skyActor!.TryGetComponent<SkyVisualizer>(out var visualizer))
            {
                visualizer!.LevelSize = Level.Size.ToXna();
                visualizer.Visualize(_sky);
                visualizer.VisualizeShadows(_sky.Name, _sky.Shadows);
            }

            if (Level.WaterType == LiquidType.None && _liquidActor != null)
            {
                Eddy.Scene.DestroyActor(_liquidActor);
                _liquidActor = null;
            }
            else if (Level.WaterType != LiquidType.None && _liquidActor == null)
            {
                _liquidActor = CreateSubActor();
                _liquidActor.Name = $"Water: {Level.WaterType}";
                _liquidActor.AddComponent<LiquidMesh>();
            }

            if (_liquidActor != null)
            {
                var mesh = _liquidActor.GetComponent<LiquidMesh>();
                mesh.Visualize(Level.WaterType, Level.WaterHeight, Level.Size.ToXna());
            }

            return;
        }

        TeardownVisualization();

        #region Sky

        _sky = (Sky)ResourceService.Load($"Skies/{Level.SkyName}");
        Eddy.Scene.Lighting.Ambient = Color.White * Level.BaseAmbient;
        Eddy.Scene.Lighting.Diffuse = Color.White * Level.BaseDiffuse;

        {
            _skyActor = CreateSubActor();
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
            _boundsActor = CreateSubActor();
            _boundsActor.Name = "Level Bounds";

            var mesh = _boundsActor.AddComponent<BoundsMesh>();
            mesh.Size = Level.Size.ToXna();
        }

        #endregion

        #region Liquid

        if (Level.WaterType != LiquidType.None)
        {
            _liquidActor = CreateSubActor();
            _liquidActor.Name = $"Water: {Level.WaterType}";

            var mesh = _liquidActor.AddComponent<LiquidMesh>();
            mesh.Visualize(Level.WaterType, Level.WaterHeight, Level.Size.ToXna());
        }

        #endregion

        #region Pickable Bounds

        _pickablesActor = CreateSubActor();
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
            _pickablesActor = CreateSubActor();
            _pickablesActor.Name = "Debug";

            var bounds = _pickablesActor.AddComponent<PickableBounds>();
            bounds.WireColor = Color.Purple;
        }

        #endregion
    }

    protected override void Act()
    {
        Eddy.AllowedTools.UnionWith(Enum.GetValues<EddyTool>());
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.Default;
    }

    public override void Dispose()
    {
        TeardownVisualization();
        base.Dispose();
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
