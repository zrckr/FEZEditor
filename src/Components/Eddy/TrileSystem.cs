using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.TrileSet;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

public class TrileSystem : EddySystem
{
    private static readonly string[] Rotations = FaceExtensions.SidesOnly
        .Select(fo => fo.ToString())
        .ToArray();

    private Vector3 _previousPositionDrag;

    private IDisposable? _positionScope;

    private IDisposable? _multiPositionScope;

    private Texture2D? _textureAtlas;

    private readonly Dictionary<string, Texture2D> _collisionTextures = new();

    public override void Initialize()
    {
        LoadTextures();
        var ids = Level.Triles.Values
            .Where(ti => ti.TrileId != EddyEditor.InvalidId)
            .SelectMany(ti => Enumerable.Repeat(ti.TrileId, 1)
                .Concat(ti.OverlappedTriles
                    .EmptyIfNull()
                    .Where(o => o.TrileId != EddyEditor.InvalidId)
                    .Select(o => o.TrileId)))
            .Distinct();

        foreach (var id in ids)
        {
            EnsureBatchActor(id);
        }

        foreach (var (emplacement, trile) in Level.Triles.Where(kv => kv.Value.TrileId != EddyEditor.InvalidId))
        {
            Visualize(new InstanceId.TrileChange(emplacement, null, trile));

            var overlaps = trile.OverlappedTriles.EmptyIfNull();
            for (var index = 0; index < overlaps.Count; index++)
            {
                var overlap = overlaps[index];
                Visualize(new InstanceId.TrileOverlapChange(emplacement, index, null, overlap));
            }
        }
    }

    public override void Visualize(InstanceId instance)
    {
        switch (instance)
        {
            case InstanceId.TrileBatch batch:
                {
                    var actor = EnsureBatchActor(batch.Id);
                    var mesh = actor.GetComponent<TrilesMesh>();
                    actor.Visible = Eddy.Visuals.HasFlag(mesh.HasGeometry
                        ? EddyVisuals.Triles
                        : EddyVisuals.EmptyTriles);
                    mesh.Displacements = Eddy.Visuals.HasFlag(EddyVisuals.DisplacedTriles);
                    break;
                }

            case InstanceId.TrileChange t:
                {
                    if (t.Before is { TrileId: not EddyEditor.InvalidId } oldTrile &&
                        Eddy.Registry.TryGetActor(new InstanceId.TrileBatch(oldTrile.TrileId), out var oldActor))
                    {
                        oldActor.GetComponent<TrilesMesh>().RemoveInstance(t.Emplacement);
                    }

                    if (t.After is not { TrileId: not EddyEditor.InvalidId } trile)
                    {
                        break;
                    }

                    var actor = EnsureBatchActor(trile.TrileId);
                    actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.Triles);

                    var mesh = actor.GetComponent<TrilesMesh>();
                    mesh.SetInstanceData(t.Emplacement, trile.Position.ToXna(), trile.PhiLight);

                    if (!mesh.HasGeometry)
                    {
                        actor.Visible = Eddy.Visuals.HasFlag(EddyVisuals.EmptyTriles);
                    }

                    break;
                }

            case InstanceId.TrileOverlapChange to:
                {
                    if (to.Before is { TrileId: not EddyEditor.InvalidId } oldOverlap &&
                        Eddy.Registry.TryGetActor(new InstanceId.TrileBatch(oldOverlap.TrileId), out var oldActor))
                    {
                        oldActor.GetComponent<TrilesMesh>()
                            .RemoveOverlapInstance(to.Emplacement, to.Index);
                    }

                    if (to.After is not { TrileId: not EddyEditor.InvalidId } overlap)
                    {
                        break;
                    }

                    var tint = GetActiveLayerTint(to.Index);
                    var actor = EnsureBatchActor(overlap.TrileId);
                    var mesh = actor.GetComponent<TrilesMesh>();
                    mesh.SetOverlapInstanceData(
                        to.Emplacement, to.Index, overlap.Position.ToXna(), overlap.PhiLight, tint);
                    break;
                }
        }
    }

    private Actor EnsureBatchActor(int trileId)
    {
        var trile = Eddy.TrileSet.Triles[trileId];
        var actor = Eddy.Registry.GetOrCreateActor(new InstanceId.TrileBatch(trileId));
        actor.Name = $"{trileId}: {trile.Name}";

        if (!actor.HasComponent<TrilesMesh>())
        {
            var mesh = actor.AddComponent<TrilesMesh>();
            mesh.Visualize(trile, _textureAtlas, _collisionTextures);
        }

        return actor;
    }

    public override void Inspect(params HashSet<InstanceId> instanceIds)
    {
        if (!instanceIds.TryGetSingle<InstanceId>(out var instanceId))
        {
            var triles = instanceIds.OfType<InstanceId.Trile>().ToHashSet();
            DrawMultiProperties(triles);
            return;
        }

        switch (instanceId)
        {
            case InstanceId.Trile t:
                DrawSingleProperties(t);
                break;

            case InstanceId.TrileGroup tg:
                DrawGroupProperties(tg);
                break;
        }
    }

    private void DrawSingleProperties(InstanceId.Trile i)
    {
        var instance = Eddy.GetActiveTrile(i.Emplacement);
        if (instance == null)
        {
            return;
        }

        var trile = Eddy.TrileSet.Triles[instance.TrileId];
        ImGui.Text($"Trile: {trile.Name} (ID={instance.TrileId})");

        var emplacementsGroups = Level.GetEmplacementGroups();
        if (Eddy.OverlapIndex == 0 && !emplacementsGroups.ContainsKey(i.Emplacement))
        {
            if (ImGui.Button($"{Lucide.Group} Group"))
            {
                using (Eddy.History.BeginScope("Create Trile Group"))
                {
                    var groupId = Level.Groups.Count > 0 ? Level.Groups.Keys.Max() + 1 : 0;
                    var group = new TrileGroup();
                    group.Triles.Add(Level.Triles[i.Emplacement]);
                    Level.Groups[groupId] = group;
                }

                return;
            }

            ImGui.Separator();
        }

        var empArray = new[] { i.Emplacement.X, i.Emplacement.Y, i.Emplacement.Z };
        ImGui.BeginDisabled();
        ImGui.InputInt3("Emplacement", ref empArray[0]);
        ImGui.EndDisabled();

        var position = instance.Position.ToXna();
        if (ImGuiX.DragFloat3("Position", ref position, 0.01f))
        {
            _positionScope ??= Eddy.History.BeginScope("Edit Trile Position");
            var before = instance.Clone();
            instance.Position = position.ClampWithinEmplacement(i.Emplacement).ToRepacker();
            var after = instance.Clone();
            VisualizeActiveTrile(i.Emplacement, before, after);
            VisualizeCollisionMap();
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _positionScope?.Dispose();
            _positionScope = null;
        }

        var phi = (int)instance.PhiLight;
        var phiNames = new[] { "Front", "Right", "Back", "Left" };
        if (ImGui.Combo("Rotation", ref phi, phiNames, phiNames.Length))
        {
            using (Eddy.History.BeginScope("Edit Trile Rotation"))
            {
                var before = instance.Clone();
                instance.PhiLight = (byte)phi;
                var after = instance.Clone();
                VisualizeActiveTrile(i.Emplacement, before, after);
                VisualizeCollisionMap();
            }
        }

        ImGui.SeparatorText("Actor Settings");

        if (ImGuiX.NullableToggleButton("ActorSettings", instance.ActorSettings))
        {
            var shouldAdd = instance.ActorSettings == null;
            var actionName = shouldAdd ? "Add" : "Remove";
            using (Eddy.History.BeginScope($"{actionName} ActorSettings"))
            {
                instance.ActorSettings = shouldAdd ? new TrileInstanceActorSettings() : null;
            }
        }

        if (instance.ActorSettings != null)
        {
            var containedTrile = instance.ActorSettings.ContainedTrile ?? EddyEditor.InvalidId;
            if (ImGui.InputInt("Contained Trile", ref containedTrile))
            {
                using (Eddy.History.BeginScope("Edit Contained Trile"))
                {
                    instance.ActorSettings.ContainedTrile = containedTrile;
                }
            }

            var signText = instance.ActorSettings.SignText.EmptyIfNull();
            if (ImGui.InputText("Sign Text", ref signText, 1024))
            {
                using (Eddy.History.BeginScope("Edit Sign Text"))
                {
                    instance.ActorSettings.SignText = signText.NullIfEmpty();
                }
            }

            var sequence = new Dirty<bool[]>(instance.ActorSettings.Sequence.EmptyIfNull());
            if (ImGuiX.EditableArray("Sequence", ref sequence, RenderItem, () => false))
            {
                using (Eddy.History.BeginScope("Edit Sequence"))
                {
                    instance.ActorSettings.Sequence = sequence.Value.NullIfEmpty();
                }
            }

            var seqSample = instance.ActorSettings.SequenceSampleName.EmptyIfNull();
            if (ImGui.InputText("Sequence Sample", ref seqSample, 255))
            {
                using (Eddy.History.BeginScope("Edit Sequence Sample"))
                {
                    instance.ActorSettings.SequenceSampleName = seqSample.NullIfEmpty();
                }
            }

            var altSeqSample = instance.ActorSettings.SequenceAlternateSampleName.EmptyIfNull();
            if (ImGui.InputText("Sequence Alternate Sample", ref altSeqSample, 255))
            {
                using (Eddy.History.BeginScope("Edit Sequence Alternate Sample"))
                {
                    instance.ActorSettings.SequenceAlternateSampleName = altSeqSample.NullIfEmpty();
                }
            }

            var hostVolume = instance.ActorSettings.HostVolume ?? EddyEditor.InvalidId;
            if (ImGui.InputInt("Host Volume", ref hostVolume))
            {
                using (Eddy.History.BeginScope("Edit Host Volume"))
                {
                    instance.ActorSettings.HostVolume = hostVolume;
                }
            }
        }
    }

    private void DrawMultiProperties(HashSet<InstanceId.Trile> instances)
    {
        var triles = instances
            .Select(e => (Id: e, Trile: Eddy.GetActiveTrile(e.Emplacement)))
            .Where(e => e.Trile != null)
            .ToList();

        if (triles.Count == 0)
        {
            return;
        }

        ImGui.Text($"{triles.Count} triles selected");

        var emplacementsGroups = Level.GetEmplacementGroups();
        if (Eddy.OverlapIndex == 0 && instances.All(i => !emplacementsGroups.ContainsKey(i.Emplacement)))
        {
            if (ImGui.Button($"{Lucide.Group} Group"))
            {
                using (Eddy.History.BeginScope("Create Trile Group"))
                {
                    var groupId = Level.Groups.Count > 0 ? Level.Groups.Keys.Max() + 1 : 0;
                    var group = new TrileGroup();

                    foreach (var i in instances)
                    {
                        var instance = Level.Triles[i.Emplacement];
                        group.Triles.Add(instance);
                    }

                    Level.Groups[groupId] = group;
                }

                return;
            }

            ImGui.Separator();
        }

        var positionDrag = _previousPositionDrag;
        if (ImGuiX.DragFloat3("Position", ref positionDrag, 0.1f))
        {
            var delta = positionDrag - _previousPositionDrag;
            _previousPositionDrag = positionDrag;
            _multiPositionScope ??= Eddy.History.BeginScope("Edit Triles Position");

            var trileSelection = (SelectionState.Trile)Eddy.Selected;
            foreach (var emplacement in trileSelection.Selected)
            {
                var instance = Eddy.GetActiveTrile(emplacement);
                if (instance == null)
                {
                    continue;
                }

                var before = instance.Clone();
                instance.Position = (instance.Position.ToXna() + delta)
                    .ClampWithinEmplacement(emplacement)
                    .ToRepacker();
                var after = instance.Clone();
                VisualizeActiveTrile(emplacement, before, after);
            }

            VisualizeCollisionMap();
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            _previousPositionDrag = Vector3.Zero;
            _multiPositionScope?.Dispose();
            _multiPositionScope = null;
        }

        var allSameFace = triles.All(i => i.Trile!.PhiLight == triles[0].Trile!.PhiLight);
        var face = allSameFace ? triles[0].Trile!.PhiLight : -1;
        if (ImGui.Combo("Rotation", ref face, Rotations, Rotations.Length))
        {
            using (Eddy.History.BeginScope("Edit Trile Rotation"))
            {
                foreach (var (id, trile) in triles)
                {
                    var active = trile!;
                    var before = active.Clone();
                    active.PhiLight = (byte)face;
                    var after = active.Clone();
                    VisualizeActiveTrile(id.Emplacement, before, after);
                }

                VisualizeCollisionMap();
            }
        }

        ImGui.SeparatorText("Actor Settings");
        var anyWithout = triles.Any(i => i.Trile!.ActorSettings == null);
        var anyWith = triles.Any(i => i.Trile!.ActorSettings != null);

        if (anyWithout)
        {
            if (ImGui.Button($"{Lucide.Plus} Add to All"))
            {
                using (Eddy.History.BeginScope("Add ActorSettings"))
                {
                    foreach (var (_, trile) in triles.Where(i => i.Trile!.ActorSettings == null))
                    {
                        trile!.ActorSettings = new TrileInstanceActorSettings();
                    }
                }
            }
        }

        if (anyWith)
        {
            if (anyWithout)
            {
                ImGui.SameLine();
            }

            if (ImGui.Button($"{Lucide.Trash2} Remove from All"))
            {
                using (Eddy.History.BeginScope("Remove ActorSettings"))
                {
                    foreach (var (_, trile) in triles.Where(i => i.Trile!.ActorSettings != null))
                    {
                        trile!.ActorSettings = null;
                    }
                }
            }
        }
    }

    private void DrawGroupProperties(InstanceId.TrileGroup instance)
    {
        if (!Level.Groups.TryGetValue(instance.Id, out var group))
        {
            return;
        }

        ImGui.Text($"Trile Group: {group.Triles.Count} trile(s), ID={instance.Id}");

        if (ImGui.Button($"{Lucide.Ungroup} Ungroup"))
        {
            using (Eddy.History.BeginScope("Remove Trile Group"))
            {
                Level.Groups.Remove(instance.Id);
            }

            return;
        }

        ImGui.Separator();

        var actor = (int)group.ActorType;
        var actors = Enum.GetNames<ActorType>();
        if (ImGui.Combo("Actor Type", ref actor, actors, actors.Length))
        {
            using (Eddy.History.BeginScope("Edit Group ActorType"))
            {
                group.ActorType = (ActorType)actor;
            }
        }

        var heavy = group.Heavy;
        if (ImGui.Checkbox("Heavy", ref heavy))
        {
            using (Eddy.History.BeginScope("Edit Group Heavy"))
            {
                group.Heavy = heavy;
            }
        }

        var sound = group.AssociatedSound.EmptyIfNull();
        if (ImGui.InputText("Sound", ref sound, 255))
        {
            using (Eddy.History.BeginScope("Edit Group Sound"))
            {
                group.AssociatedSound = sound.NullIfEmpty();
            }
        }

        ImGui.SeparatorText("Geyser");
        {
            var geyserOffset = group.GeyserOffset;
            if (ImGui.DragFloat("Offset##GeyserOffset", ref geyserOffset, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Offset"))
                {
                    group.GeyserOffset = geyserOffset;
                }
            }

            var geyserPause = group.GeyserPauseFor;
            if (ImGui.DragFloat("Pause For", ref geyserPause, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Pause"))
                {
                    group.GeyserPauseFor = geyserPause;
                }
            }

            var geyserLift = group.GeyserLiftFor;
            if (ImGui.DragFloat("Lift For", ref geyserLift, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Lift"))
                {
                    group.GeyserLiftFor = geyserLift;
                }
            }

            var geyserApex = group.GeyserApexHeight;
            if (ImGui.DragFloat("Apex Height", ref geyserApex, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Geyser Apex"))
                {
                    group.GeyserApexHeight = geyserApex;
                }
            }
        }

        ImGui.SeparatorText("Spin");
        {
            var spinCenter = group.SpinCenter.ToXna();
            if (ImGuiX.DragFloat3("Center", ref spinCenter, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Spin Center"))
                {
                    group.SpinCenter = spinCenter.ToRepacker();
                }
            }

            var spinClockwise = group.SpinClockwise;
            if (ImGui.Checkbox("Clockwise", ref spinClockwise))
            {
                using (Eddy.History.BeginScope("Edit Spin Clockwise"))
                {
                    group.SpinClockwise = spinClockwise;
                }
            }

            var spinFreq = group.SpinFrequency;
            if (ImGui.DragFloat("Frequency", ref spinFreq, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Spin Frequency"))
                {
                    group.SpinFrequency = spinFreq;
                }
            }

            var spinNeedsTrigger = group.SpinNeedsTriggering;
            if (ImGui.Checkbox("Needs Triggering", ref spinNeedsTrigger))
            {
                using (Eddy.History.BeginScope("Edit Spin NeedsTriggering"))
                {
                    group.SpinNeedsTriggering = spinNeedsTrigger;
                }
            }

            var spin180 = group.Spin180Degrees;
            if (ImGui.Checkbox("180 Degrees", ref spin180))
            {
                using (Eddy.History.BeginScope("Edit Spin 180"))
                {
                    group.Spin180Degrees = spin180;
                }
            }

            var fallOnRotate = group.FallOnRotate;
            if (ImGui.Checkbox("Fall On Rotate", ref fallOnRotate))
            {
                using (Eddy.History.BeginScope("Edit Spin FallOnRotate"))
                {
                    group.FallOnRotate = fallOnRotate;
                }
            }

            var spinOffset = group.SpinOffset;
            if (ImGui.DragFloat("Offset##SpinOffset", ref spinOffset, 0.1f))
            {
                using (Eddy.History.BeginScope("Edit Spin Offset"))
                {
                    group.SpinOffset = spinOffset;
                }
            }
        }

        ImGui.SeparatorText("Path");
        if (group.Path == null)
        {
            ImGui.TextDisabled("No path assigned.");
            if (ImGui.Button($"{Lucide.Route} Create Path##GroupPath"))
            {
                using (Eddy.History.BeginScope("Create Group Path"))
                {
                    group.Path = new MovementPath
                    {
                        Segments = new List<PathSegment> { new() }
                    };
                }

                var pathInstance = new InstanceId.GroupPath(instance.Id);
                Eddy.Selected = new SelectionState.Path(pathInstance, []);
            }
        }
        else
        {
            ImGui.TextDisabled($"{group.Path.Segments.Count} segment(s)");
            if (ImGui.Button("Edit Path##EditGroupPath"))
            {
                var pathInstance = new InstanceId.GroupPath(instance.Id);
                Eddy.Selected = new SelectionState.Path(pathInstance, []);
            }

            ImGui.SameLine();
            if (ImGui.Button($"{Lucide.Trash2}##DeleteGroupPath"))
            {
                using (Eddy.History.BeginScope("Delete Group Path"))
                {
                    group.Path = null;
                }
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Delete this Path");
            }
        }
    }

    private void VisualizeActiveTrile(TrileEmplacement emplacement, TrileInstance before, TrileInstance after)
    {
        Visualize(Eddy.OverlapIndex == 0
            ? new InstanceId.TrileChange(emplacement, before, after)
            : new InstanceId.TrileOverlapChange(emplacement, Eddy.OverlapIndex - 1, before, after));
    }

    private void VisualizeCollisionMap()
    {
        Eddy.Visualize(new InstanceId.CollisionMap());
        Eddy.Visualize(new InstanceId.PickableBounds());
    }

    private static bool RenderItem(int index, ref bool item)
    {
        return ImGui.Checkbox($"{index + 1}", ref item);
    }

    private Color GetActiveLayerTint(int slot)
    {
        if (!Eddy.Visuals.HasFlag(EddyVisuals.OverlappedTriles))
        {
            return Mathz.TransparentBlack;
        }

        if (Eddy.OverlapIndex > 0 && Eddy.OverlapIndex == slot + 1)
        {
            return new Color(64, 160, 255, 160);
        }

        return new Color(0, 0, 0, 96);
    }

    private void LoadTextures()
    {
        if (Eddy.TrileSet.TextureAtlas != null)
        {
            _textureAtlas = RepackerExtensions.ConvertToTexture2D(Eddy.TrileSet.TextureAtlas);
        }

        foreach (var collision in Enum.GetValues<CollisionType>())
        {
            var texture = Content.Load<Texture2D>($"Textures/{collision}");
            _collisionTextures.Add($"{collision}Texture", texture);
        }
    }

    private void UnloadTextures()
    {
        foreach (var texture in _collisionTextures.Values)
        {
            texture.Dispose();
        }

        _collisionTextures.Clear();
        _textureAtlas?.Dispose();
        _textureAtlas = null;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _positionScope?.Dispose();
        _multiPositionScope?.Dispose();
        UnloadTextures();
    }
}
