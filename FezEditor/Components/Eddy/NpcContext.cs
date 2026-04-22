using FezEditor.Actors;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components.Eddy;

internal class NpcContext : BaseContext
{
    private readonly Dictionary<int, Actor> _npcActors = new();

    private int? _hoveredId;

    private readonly HashSet<int> _selectedIds = new();

    private IDisposable? _translateScope;

    private readonly List<NpcInstance> _clipboard = new();

    public NpcContext(Game game, Level level, IEddyEditor eddy) : base(game, level, eddy)
    {
    }

    protected override void TestConditions()
    {
        _hoveredId = null;
        if (Eddy.Visuals.IsDirty)
        {
            var visible = Eddy.Visuals.Value.HasFlag(EddyVisuals.NonPlayableCharacters);
            foreach (var actor in _npcActors.Values)
            {
                actor.Visible = visible;
                var mesh = actor.GetComponent<NpcMesh>();
                mesh.Pickable = visible;
            }
        }

        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.HasComponent<NpcMesh>())
        {
            var actor = Eddy.Hit.Value.Actor;
            var foundId = _npcActors.FirstOrDefault(kv => kv.Value == actor).Key;
            if (_npcActors.ContainsKey(foundId) && Eddy.Tool is EddyTool.Select or EddyTool.Pick)
            {
                _hoveredId = foundId;
                Eddy.HoveredContext = EddyContext.NonPlayableCharacter;
                return;
            }
        }

        if (!_hoveredId.HasValue && !Eddy.Gizmo.IsActive && Eddy.Tool != EddyTool.Paint &&
            Eddy.SelectedContext == EddyContext.NonPlayableCharacter &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Eddy.IsViewportHovered)
        {
            _selectedIds.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        if (_selectedIds.Count > 0)
        {
            Eddy.SelectedContext = EddyContext.NonPlayableCharacter;
        }

        if (Eddy.AssetBrowser.Select(AssetType.NonPlayableCharacter))
        {
            Eddy.Tool = EddyTool.Paint;
            Eddy.SelectedContext = EddyContext.NonPlayableCharacter;
        }

        if (Eddy.InstanceBrowser.Select(out var sel) && sel.context == EddyContext.NonPlayableCharacter)
        {
            Eddy.InstanceBrowser.Consume();
            Eddy.FocusOn(Level.NonPlayerCharacters[sel.id].Position.ToXna());
        }
    }

    protected override void Act()
    {
        Eddy.AllowedTools.Add(EddyTool.Select);
        Eddy.AllowedTools.Add(EddyTool.Translate);
        Eddy.AllowedTools.Add(EddyTool.Paint);
        Eddy.AllowedTools.Add(EddyTool.Pick);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            _selectedIds.Clear();
            Eddy.SelectedContext = EddyContext.Default;
            Eddy.Tool = EddyTool.Select;
        }

        switch (Eddy.Tool)
        {
            case EddyTool.Select: UpdateSelect(); break;
            case EddyTool.Translate: UpdateTranslate(); break;
            case EddyTool.Paint: UpdatePaint(); break;
            case EddyTool.Pick: UpdatePick(); break;
            case EddyTool.Rotate:
            case EddyTool.Scale: break;
            default: throw new ArgumentOutOfRangeException();
        }

        if (_hoveredId.HasValue)
        {
            var hoverSurfaces = BuildWireframeForNpc(_hoveredId.Value, HoverColor);
            if (hoverSurfaces.HasValue)
            {
                Eddy.Cursor.SetHoverSurfaces([hoverSurfaces.Value], HoverColor);
            }
        }

        var selectionSurfaces = _selectedIds
            .Select(id => BuildWireframeForNpc(id, SelectionColor))
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToList();

        if (selectionSurfaces.Count > 0)
        {
            Eddy.Cursor.SetSelectionSurfaces(selectionSurfaces, SelectionColor);
        }
    }

    private void UpdateSelect()
    {
        StatusService.AddHints(
            ("LMB", "Select"),
            ("Shift+LMB", "Add to Selection")
        );

        if (_selectedIds.Count > 0)
        {
            StatusService.AddHints(
                ("Delete", "Erase"),
                ("Ctrl+C", "Copy"),
                ("Ctrl+X", "Cut")
            );
        }

        if (_clipboard.Count > 0)
        {
            StatusService.AddHints(("Ctrl+V", "Paste"));
        }

        if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            using (Eddy.History.BeginScope("Delete NPCs"))
            {
                RemoveSelected();
            }
        }

        if (ImGui.GetIO().KeyCtrl)
        {
            if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.C))
            {
                BuildClipboard();
            }

            if (_selectedIds.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.X))
            {
                BuildClipboard();
                using (Eddy.History.BeginScope("Cut NPCs"))
                {
                    RemoveSelected();
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false))
            {
                using (Eddy.History.BeginScope("Paste NPCs"))
                {
                    PasteClipboard();
                }
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredId.HasValue)
        {
            if (!ImGui.GetIO().KeyShift)
            {
                _selectedIds.Clear();
            }

            _selectedIds.Add(_hoveredId.Value);
            Eddy.SelectedContext = EddyContext.NonPlayableCharacter;
        }
    }

    private void UpdateTranslate()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        var centroid = ComputeSelectionCentroid();
        if (Eddy.Gizmo.Translate(ref centroid))
        {
            var delta = centroid - ComputeSelectionCentroid();
            foreach (var id in _selectedIds)
            {
                if (Level.NonPlayerCharacters.TryGetValue(id, out var instance))
                {
                    var position = instance.Position.ToXna() + delta;
                    instance.Position = position.ToRepacker();
                    if (_npcActors.TryGetValue(id, out var actor))
                    {
                        actor.Transform.Position = position;
                    }
                }
            }
        }

        if (Eddy.Gizmo.DragStarted)
        {
            _translateScope?.Dispose();
            _translateScope = Eddy.History.BeginScope("Translate NPC");
        }

        if (Eddy.Gizmo.DragEnded)
        {
            _translateScope?.Dispose();
            _translateScope = null;
        }
    }


    private Vector3 ComputeSelectionCentroid()
    {
        if (_selectedIds.Count == 0)
        {
            return Vector3.Zero;
        }

        var sum = _selectedIds
            .Select(id => Level.NonPlayerCharacters[id])
            .Select(instance => instance.Position.ToXna())
            .Aggregate(Vector3.Zero, (current, position) => current + position);

        return sum / _selectedIds.Count;
    }

    public override void DrawOverlay()
    {
        if (Eddy.Tool != EddyTool.Paint || !Eddy.IsViewportHovered ||
            Eddy.SelectedContext != EddyContext.NonPlayableCharacter)
        {
            return;
        }

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.NonPlayableCharacter);
        if (string.IsNullOrEmpty(entry))
        {
            return;
        }

        var thumb = Eddy.AssetBrowser.GetThumbnail(AssetType.NonPlayableCharacter, entry);
        if (thumb != null)
        {
            var mousePos = ImGui.GetMousePos();
            var drawMin = mousePos + new NVector2(12f, 12f);
            var drawMax = drawMin + new NVector2(32f, 32f);
            ImGui.GetForegroundDrawList(ImGui.GetMainViewport()).AddImage(ImGuiX.Bind(thumb), drawMin, drawMax);
        }
    }

    private void UpdatePaint()
    {
        StatusService.AddHints(
            ("LMB", "Place")
        );

        var entry = Eddy.AssetBrowser.GetSelectedEntry(AssetType.NonPlayableCharacter);

        // Highlight hovered emplacement for placement preview
        TrileEmplacement? hoveredEmp = null;
        if (Eddy.Hit.HasValue && Eddy.Hit.Value.Actor.TryGetComponent<TrilesMesh>(out var mesh) && mesh != null)
        {
            var index = Eddy.Hit.Value.Index;
            hoveredEmp = mesh.GetEmplacement(index);
            if (Level.Triles.TryGetValue(hoveredEmp, out var hoveredInstance))
            {
                var box = mesh.GetBounds().ElementAt(index);
                var face = Mathz.DetermineFace(box, Eddy.Ray, Eddy.Hit.Value.Distance);
                var trileCenter = hoveredInstance.Position.ToXna() + new Vector3(0.5f);
                var origin = trileCenter + face.AsVector() * (0.5f + CursorMesh.OverlayOffset);
                var surface = MeshSurface.CreateFaceQuad(Vector3.One, origin, face);
                Eddy.Cursor.SetHoverSurfaces([(surface, PrimitiveType.TriangleList)], HoverColor);
            }
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hoveredEmp != null && !string.IsNullOrEmpty(entry))
        {
            using (Eddy.History.BeginScope("Place NPC"))
            {
                var id = NextAvailableId();
                var position = new Vector3(hoveredEmp.X, hoveredEmp.Y, hoveredEmp.Z);
                Level.NonPlayerCharacters[id] = new NpcInstance
                {
                    Name = entry,
                    Position = position.ToRepacker()
                };
            }
        }
    }

    private void UpdatePick()
    {
        StatusService.AddHints(
            ("LMB", "Pick NPC")
        );

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && _hoveredId.HasValue)
        {
            if (Level.NonPlayerCharacters.TryGetValue(_hoveredId.Value, out var instance))
            {
                Eddy.AssetBrowser.Pick(instance.Name, AssetType.NonPlayableCharacter);
                Eddy.Tool = EddyTool.Paint;
            }
        }
    }

    private (MeshSurface, PrimitiveType)? BuildWireframeForNpc(int id, Color color)
    {
        if (!Level.NonPlayerCharacters.ContainsKey(id))
        {
            return null;
        }

        if (!_npcActors.TryGetValue(id, out var actor))
        {
            return null;
        }

        if (!actor.TryGetComponent<NpcMesh>(out var mesh))
        {
            return null;
        }

        var box = mesh!.GetBounds().First();
        var size = box.Max - box.Min;
        var center = (box.Min + box.Max) * 0.5f;

        var surface = MeshSurface.CreateWireframeBox(size, color);
        for (var i = 0; i < surface.Vertices.Length; i++)
        {
            surface.Vertices[i] += center;
        }

        return (surface, PrimitiveType.LineList);
    }

    public override void DrawProperties()
    {
        if (Eddy.SelectedContext != EddyContext.NonPlayableCharacter || _selectedIds.Count == 0)
        {
            return;
        }

        var id = _selectedIds.First();
        if (!Level.NonPlayerCharacters.TryGetValue(id, out var instance))
        {
            return;
        }

        ImGui.Text($"NPC: {instance.Name} ID={id})");

        var position = instance.Position.ToXna();
        if (ImGuiX.InputFloat3("Position", ref position))
        {
            using (Eddy.History.BeginScope("Edit BG Position"))
            {
                instance.Position = position.ToRepacker();
                if (_npcActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = position;
                }
            }
        }

        var destinationOffset = instance.DestinationOffset.ToXna();
        if (ImGuiX.DragFloat3("Destination Offset", ref destinationOffset))
        {
            using (Eddy.History.BeginScope("Edit NPC Destination Offset"))
            {
                instance.DestinationOffset = destinationOffset.ToRepacker();
            }
        }

        var walkSpeed = instance.WalkSpeed;
        if (ImGui.InputFloat("Walk Speed", ref walkSpeed))
        {
            using (Eddy.History.BeginScope("Edit NPC Walk Speed"))
            {
                instance.WalkSpeed = walkSpeed;
            }
        }

        var randomizeSpeech = instance.RandomizeSpeech;
        if (ImGui.Checkbox("Randomize Speech", ref randomizeSpeech))
        {
            using (Eddy.History.BeginScope("Edit NPC Randomize Speech"))
            {
                instance.RandomizeSpeech = randomizeSpeech;
            }
        }

        var sayFirstSpeechLineOnce = instance.SayFirstSpeechLineOnce;
        if (ImGui.Checkbox("Say First Speech Line Once", ref sayFirstSpeechLineOnce))
        {
            using (Eddy.History.BeginScope("Edit NPC Say First Speech Line Once"))
            {
                instance.SayFirstSpeechLineOnce = sayFirstSpeechLineOnce;
            }
        }

        var avoidsGomez = instance.AvoidsGomez;
        if (ImGui.Checkbox("Avoids Gomez", ref avoidsGomez))
        {
            using (Eddy.History.BeginScope("Edit NPC Avoids Gomez"))
            {
                instance.AvoidsGomez = avoidsGomez;
            }
        }

        var actorType = (int)instance.ActorType;
        var actors = Enum.GetNames<ActorType>();

        if (ImGui.Combo("Actor Type", ref actorType, actors, actors.Length))
        {
            using (Eddy.History.BeginScope("Edit NPC Actor Type"))
            {
                instance.ActorType = (ActorType)actorType;
            }
        }

        var speech = instance.Speech;
        if (ImGuiX.EditableList("Speech", ref speech, RenderSpeechLine, () => new SpeechLine()))
        {
            using (Eddy.History.BeginScope("Edit NPC Speech"))
            {
                instance.Speech = speech;
            }
        }

        var actions = instance.Actions.ToDictionary(a => (int)a.Key, a => a.Value);
        if (ImGuiX.EditableDict("Actions", ref actions, RenderNpcActionContent, RenderNewContent, () => new NpcActionContent()))
        {
            using (Eddy.History.BeginScope("Edit NPC Actions"))
            {
                instance.Actions = actions.ToDictionary(a => (NpcAction)a.Key, a => a.Value);
            }
        }
    }

    private static bool RenderSpeechLine(int index, ref SpeechLine item)
    {
        ImGui.TextDisabled(index + ":");
        var edited = false;
        {
            var text = item.Text;
            edited |= ImGui.InputText("Text##sl" + index, ref text, 255);
            item.Text = text;
        }
        {
            var animationName = item.OverrideContent.AnimationName;
            edited |= ImGui.InputText("Animation Name##sl1" + index, ref animationName, 255);
            item.OverrideContent.AnimationName = animationName;
        }
        {
            var soundName = item.OverrideContent.SoundName;
            edited |= ImGui.InputText("Sound Name##sl2" + item, ref soundName, 255);
            item.OverrideContent.SoundName = soundName;
        }
        return edited;
    }

    private static bool RenderNpcActionContent(int key, ref NpcActionContent value)
    {
        ImGui.TextDisabled((NpcAction)key + ":");
        var edited = false;
        {
            var animationName = value.AnimationName;
            edited |= ImGui.InputText("Animation Name##npc1" + key, ref animationName, 255);
            value.AnimationName = animationName;
        }
        {
            var soundName = value.SoundName;
            edited |= ImGui.InputText("Sound Name##npc2" + key, ref soundName, 255);
            value.SoundName = soundName;
        }
        return edited;
    }

    private static bool RenderNewContent(ref int key)
    {
        var actions = Enum.GetNames<NpcAction>();
        return ImGui.Combo("##npcAction", ref key, actions, actions.Length);
    }

    public override void Revisualize(bool partial = false)
    {
        if (partial)
        {
            if (Eddy.SelectedContext != EddyContext.NonPlayableCharacter)
            {
                return;
            }

            var presentIds = Level.NonPlayerCharacters.Keys.Where(k => k != InvalidId).ToHashSet();
            foreach (var id in _npcActors.Keys.ToList())
            {
                if (!presentIds.Contains(id))
                {
                    Eddy.Scene.DestroyActor(_npcActors[id]);
                    _npcActors.Remove(id);
                }
            }

            foreach (var (id, instance) in Level.NonPlayerCharacters.Where(kv => kv.Key != InvalidId))
            {
                if (_npcActors.TryGetValue(id, out var actor))
                {
                    actor.Transform.Position = instance.Position.ToXna();
                }
                else
                {
                    actor = CreateSubActor();
                    actor.Name = $"{id}: {instance.Name}";
                    actor.Transform.Position = instance.Position.ToXna();
                    _npcActors[id] = actor;

                    var mesh = actor.AddComponent<NpcMesh>();
                    var ao = ResourceService.LoadAnimations($"Character Animations/{instance.Name}");
                    mesh.Visualize(ao);
                }
            }

            _selectedIds.RemoveWhere(id => !Level.NonPlayerCharacters.ContainsKey(id));
            if (_hoveredId.HasValue && !Level.NonPlayerCharacters.ContainsKey(_hoveredId.Value))
            {
                _hoveredId = null;
            }

            return;
        }

        TeardownVisualization();

        #region Non-Playable Characters

        foreach (var (id, instance) in Level.NonPlayerCharacters.Where(kv => kv.Key != InvalidId))
        {
            var actor = CreateSubActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            _npcActors[id] = actor;

            var mesh = actor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations($"Character Animations/{instance.Name}");
            mesh.Visualize(animations);
        }

        #endregion
    }

    private void RemoveSelected()
    {
        foreach (var id in _selectedIds)
        {
            Level.NonPlayerCharacters.Remove(id);
            if (_npcActors.TryGetValue(id, out var actor))
            {
                Eddy.Scene.DestroyActor(actor);
                _npcActors.Remove(id);
            }
        }

        _selectedIds.Clear();
    }

    private void BuildClipboard()
    {
        _clipboard.Clear();
        foreach (var id in _selectedIds)
        {
            var instance = Level.NonPlayerCharacters[id];
            _clipboard.Add(new NpcInstance
            {
                Name = instance.Name,
                Position = instance.Position,
                DestinationOffset = instance.DestinationOffset,
                WalkSpeed = instance.WalkSpeed,
                RandomizeSpeech = instance.RandomizeSpeech,
                SayFirstSpeechLineOnce = instance.SayFirstSpeechLineOnce,
                AvoidsGomez = instance.AvoidsGomez,
                ActorType = instance.ActorType,
                Speech = instance.Speech.Select(sl => new SpeechLine
                {
                    Text = sl.Text,
                    OverrideContent = new NpcActionContent
                    {
                        AnimationName = sl.OverrideContent.AnimationName,
                        SoundName = sl.OverrideContent.SoundName
                    }
                }).ToList(),
                Actions = instance.Actions.Select(kv =>
                    new KeyValuePair<NpcAction, NpcActionContent>(kv.Key, new NpcActionContent
                    {
                        AnimationName = kv.Value.AnimationName,
                        SoundName = kv.Value.SoundName
                    })).ToDictionary()
            });
        }
    }

    private void PasteClipboard()
    {
        _selectedIds.Clear();
        foreach (var instance in _clipboard)
        {
            var id = NextAvailableId();
            Level.NonPlayerCharacters[id] = instance;

            var actor = CreateSubActor();
            actor.Name = $"{id}: {instance.Name}";
            actor.Transform.Position = instance.Position.ToXna();
            _npcActors[id] = actor;

            var mesh = actor.AddComponent<NpcMesh>();
            var animations = ResourceService.LoadAnimations($"Character Animations/{instance.Name}");
            mesh.Visualize(animations);

            _selectedIds.Add(id);
        }
    }

    private int NextAvailableId()
    {
        return Level.NonPlayerCharacters.Keys.Where(k => k != InvalidId).DefaultIfEmpty(-1).Max() + 1;
    }

    protected override bool IsContextAllowed(EddyContext context)
    {
        return context == EddyContext.NonPlayableCharacter;
    }

    public override void Dispose()
    {
        _translateScope?.Dispose();
        TeardownVisualization();
        base.Dispose();
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