using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;

namespace FezEditor.Components.Eddy;

internal class TrileGroupContext : EddyContext
{
    private readonly Dictionary<TrileEmplacement, int> _emplacementGroups = new();

    private readonly Dictionary<int, HashSet<TrileEmplacement>> _reverseLookup = new();

    private TrileGroup? _group;

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

        #region Trile Groups

        foreach (var (id, group) in Level.Groups.Where(kv => kv.Key != InvalidId))
        {
            // NOTE:
            // Grouped emplacements are stored in TrileInstance objects.
            // Check FEZRepacker.Core.Definitions.Json.TrileGroupJsonModel

            foreach (var instance in group.Triles)
            {
                var emplacement = new TrileEmplacement(instance.Position);
                _emplacementGroups[emplacement] = id;

                if (!_reverseLookup.TryGetValue(id, out var hashSet))
                {
                    hashSet = new HashSet<TrileEmplacement>();
                    _reverseLookup[id] = hashSet;
                }

                hashSet.Add(emplacement);
            }
        }

        #endregion
    }

    public override void DrawProperties()
    {
        if (_group == null)
        {
            base.DrawProperties();
            return;
        }

        var actor = (int)_group.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actor, actors, actors.Length))
        {
            using (History.BeginScope("Edit Group ActorType"))
            {
                _group.ActorType = (ActorType)actor;
            }
        }

        var heavy = _group.Heavy;
        if (ImGui.Checkbox("Heavy", ref heavy))
        {
            using (History.BeginScope("Edit Group Heavy"))
            {
                _group.Heavy = heavy;
            }
        }

        var sound = _group.AssociatedSound;
        if (ImGui.InputText("Sound", ref sound, 255))
        {
            using (History.BeginScope("Edit Group Sound"))
            {
                _group.AssociatedSound = sound;
            }
        }

        ImGui.SeparatorText("Geyser");
        {
            var geyserOffset = _group.GeyserOffset;
            if (ImGui.DragFloat("Offset", ref geyserOffset, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Offset"))
                {
                    _group.GeyserOffset = geyserOffset;
                }
            }

            var geyserPause = _group.GeyserPauseFor;
            if (ImGui.DragFloat("Pause For", ref geyserPause, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Pause"))
                {
                    _group.GeyserPauseFor = geyserPause;
                }
            }

            var geyserLift = _group.GeyserLiftFor;
            if (ImGui.DragFloat("Lift For", ref geyserLift, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Lift"))
                {
                    _group.GeyserLiftFor = geyserLift;
                }
            }

            var geyserApex = _group.GeyserApexHeight;
            if (ImGui.DragFloat("Apex Height", ref geyserApex, 0.1f))
            {
                using (History.BeginScope("Edit Geyser Apex"))
                {
                    _group.GeyserApexHeight = geyserApex;
                }
            }
        }

        ImGui.SeparatorText("Spin");
        {
            var spinCenter = _group.SpinCenter.ToXna();
            if (ImGuiX.DragFloat3("Center", ref spinCenter, 0.1f))
            {
                using (History.BeginScope("Edit Spin Center"))
                {
                    _group.SpinCenter = spinCenter.ToRepacker();
                }
            }

            var spinClockwise = _group.SpinClockwise;
            if (ImGui.Checkbox("Clockwise", ref spinClockwise))
            {
                using (History.BeginScope("Edit Spin Clockwise"))
                {
                    _group.SpinClockwise = spinClockwise;
                }
            }

            var spinFreq = _group.SpinFrequency;
            if (ImGui.DragFloat("Frequency", ref spinFreq, 0.1f))
            {
                using (History.BeginScope("Edit Spin Frequency"))
                {
                    _group.SpinFrequency = spinFreq;
                }
            }

            var spinNeedsTrigger = _group.SpinNeedsTriggering;
            if (ImGui.Checkbox("Needs Triggering", ref spinNeedsTrigger))
            {
                using (History.BeginScope("Edit Spin NeedsTriggering"))
                {
                    _group.SpinNeedsTriggering = spinNeedsTrigger;
                }
            }

            var spin180 = _group.Spin180Degrees;
            if (ImGui.Checkbox("180 Degrees", ref spin180))
            {
                using (History.BeginScope("Edit Spin 180"))
                {
                    _group.Spin180Degrees = spin180;
                }
            }

            var fallOnRotate = _group.FallOnRotate;
            if (ImGui.Checkbox("Fall On Rotate", ref fallOnRotate))
            {
                using (History.BeginScope("Edit Spin FallOnRotate"))
                {
                    _group.FallOnRotate = fallOnRotate;
                }
            }

            var spinOffset = _group.SpinOffset;
            if (ImGui.DragFloat("Offset", ref spinOffset, 0.1f))
            {
                using (History.BeginScope("Edit Spin Offset"))
                {
                    _group.SpinOffset = spinOffset;
                }
            }

            ImGui.TreePop();
        }

        if (_group.Path != null)
        {
            ImGui.TextDisabled($"Path: {_group.Path.Segments.Count} segments");
        }
    }

    public override void Dispose()
    {
        TeardownVisualization();
    }

    private void TeardownVisualization()
    {
        _emplacementGroups.Clear();
        _reverseLookup.Clear();
    }
}