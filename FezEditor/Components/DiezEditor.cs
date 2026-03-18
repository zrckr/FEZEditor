using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.TrackedSong;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace FezEditor.Components;

public class DiezEditor : EditorComponent
{
    public override object Asset => _trackedSong;

    private readonly TrackedSong _trackedSong;

    private int _loopIndex = -1;

    private SoundEffect? _assembleChordSound;
    private string? _requestedLoopToOpen;

    private TimeSpan _assembleChordElapsed = TimeSpan.Zero;

    private readonly EditorService _editorService;

    public DiezEditor(Game game, string title, TrackedSong trackedSong) : base(game, title)
    {
        _editorService = game.GetService<EditorService>();

        _trackedSong = trackedSong;
        History.Track(trackedSong);
    }

    public override void Update(GameTime gameTime)
    {
        if (_assembleChordSound != null)
        {
            _assembleChordElapsed += gameTime.ElapsedGameTime;
            if (_assembleChordElapsed >= _assembleChordSound.Duration)
            {
                _assembleChordSound.Dispose();
                _assembleChordSound = null;
                _assembleChordElapsed = TimeSpan.Zero;
            }
        }

        if (_requestedLoopToOpen != null)
        {
            if (TryFindLoopPath(_requestedLoopToOpen, out var loopPath))
            {
                _editorService.OpenEditorFor(loopPath);
            }
            _requestedLoopToOpen = null;
        }
    }

    public override void Draw()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));

        var availSize = ImGui.GetContentRegionAvail();
        var width = availSize.X / 3f;

        if (ImGuiX.BeginChild("SongProperties", new Vector2(width, 0), ImGuiChildFlags.Border))
        {
            DrawSongProperties();
            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGuiX.BeginChild("OverlayLoops", new Vector2(width, 0), ImGuiChildFlags.Border))
        {
            DrawLoopsList();
            ImGui.EndChild();
        }

        ImGui.SameLine();

        if (ImGuiX.BeginChild("LoopProperties", Vector2.Zero, ImGuiChildFlags.Border))
        {
            DrawLoopProperties();
            ImGui.EndChild();
        }

        ImGui.PopStyleVar();
    }

    private void DrawLoopsList()
    {
        ImGui.Text("Overlay Loops");
        ImGui.Separator();

        ImGui.BeginDisabled(_loopIndex == -1 || _loopIndex == _trackedSong.Loops.Count - 1);
        if (ImGui.Button($"{Icons.ChevronDown} Down"))
        {
            MoveLoop(_loopIndex, ++_loopIndex);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(_loopIndex is -1 or 0);
        if (ImGui.Button($"{Icons.ChevronUp} Up"))
        {
            MoveLoop(_loopIndex, --_loopIndex);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button($"{Icons.Add} Add"))
        {
            using (History.BeginScope("Add New Loop"))
            {
                _trackedSong.Loops.Add(new Loop
                {
                    Name = $"{_trackedSong.Name} ^ Loop{_trackedSong.Loops.Count}"
                });
            }
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(_loopIndex == -1);
        if (ImGui.Button($"{Icons.Remove} Remove"))
        {
            using (History.BeginScope("Remove The Loop"))
            {
                _trackedSong.Loops.RemoveAt(_loopIndex);
                _loopIndex = -1;
            }
        }

        ImGui.EndDisabled();

        ImGui.Separator();
        if (ImGui.BeginChild("##LoopsList"))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _loopIndex = -1;
            }

            if (_trackedSong.Loops.Count == 0)
            {
                const string emptyText = "There's no loops...";
                ImGuiX.SetTextCentered(emptyText);
                ImGui.Text(emptyText);
            }

            for (var i = 0; i < _trackedSong.Loops.Count; i++)
            {
                var loop = _trackedSong.Loops[i];
                if (ImGui.Selectable(loop.Name, _loopIndex == i, ImGuiSelectableFlags.AllowDoubleClick))
                {
                    _loopIndex = i;

                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                    {
                        _requestedLoopToOpen = loop.Name;
                    }
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawSongProperties()
    {
        ImGui.Text("Tracked Song Properties");
        ImGui.Separator();

        var name = _trackedSong.Name;
        if (ImGui.InputText("Song Name", ref name, 255))
        {
            using (History.BeginScope("Change Name"))
            {
                _trackedSong.Name = name;
            }
        }

        var tempo = _trackedSong.Tempo;
        if (ImGui.InputInt("Tempo", ref tempo, 10, 100))
        {
            using (History.BeginScope("Change Tempo"))
            {
                _trackedSong.Tempo = tempo;
            }
        }

        var timeSignature = _trackedSong.TimeSignature;
        if (ImGui.InputInt("Time Signature /4", ref timeSignature, 1, 1))
        {
            using (History.BeginScope("Change Time Signature"))
            {
                _trackedSong.TimeSignature = timeSignature;
            }
        }

        ImGui.SeparatorText("Notes");
        {
            var assembleChord = (int)_trackedSong.AssembleChord;
            var assembleChords = Enum.GetNames<AssembleChords>();
            if (ImGui.Combo("Assemble Chord", ref assembleChord, assembleChords, assembleChords.Length))
            {
                using (History.BeginScope("Change Assemble Chord"))
                {
                    _trackedSong.AssembleChord = (AssembleChords)assembleChord;
                }
            }

            var notes = Enum.GetNames<ShardNotes>();
            for (var i = 0; i < 8; i++)
            {
                var note = (int)_trackedSong.Notes[i];
                if (ImGui.Combo($"Note #{i + 1}", ref note, notes, notes.Length))
                {
                    using (History.BeginScope("Change Shard Note"))
                    {
                        _trackedSong.Notes[i] = (ShardNotes)note;
                    }
                }
            }

            if (_assembleChordSound != null)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Button($"{Icons.Play} Preview the Assemble Chord"))
            {
                var path = $"Sounds/Collects/SplitUpCube/Assemble_{_trackedSong.AssembleChord}";
                using var stream = ResourceService.OpenStream(path, ".wav");
                _assembleChordSound = SoundEffect.FromStream(stream);
                _assembleChordSound.Play();
                _assembleChordElapsed = TimeSpan.Zero;
            }

            if (_assembleChordSound != null)
            {
                ImGui.EndDisabled();
            }
        }

        ImGui.SeparatorText("Ordering");
        {
            var randomOrdering = _trackedSong.RandomOrdering;
            if (ImGui.Checkbox("Random One-At-a-Time Ordering", ref randomOrdering))
            {
                using (History.BeginScope("Change Random Ordering"))
                {
                    _trackedSong.RandomOrdering = randomOrdering;
                }
            }

            var customOrdering = _trackedSong.CustomOrdering.ToList();
            if (ImGuiX.EditableList("Custom Ordering:", ref customOrdering, RenderInt, () => 0))
            {
                using (History.BeginScope("Change Custom Ordering"))
                {
                    _trackedSong.CustomOrdering = customOrdering.ToArray();
                }
            }
        }
    }

    private void DrawLoopProperties()
    {
        const string text = "Selected Loop Properties";
        ImGui.Text(text);
        ImGui.Separator();

        if (_loopIndex == -1)
        {
            const string emptyText = "Select Loop from the list";
            ImGuiX.SetTextCentered(emptyText);
            ImGui.Text(emptyText);
            return;
        }

        var loop = _trackedSong.Loops[_loopIndex];

        ImGui.SeparatorText("Filename");
        {
            var filename = loop.Name;
            if (ImGui.InputText("##Filename", ref filename, 255))
            {
                using (History.BeginScope("Change Loop Name"))
                {
                    loop.Name = filename;
                }
            }
        }

        ImGui.SeparatorText("Trigger between after every...");
        {
            var triggerFrom = loop.TriggerFrom;
            ImGui.SetNextItemWidth(96);
            if (ImGui.InputInt("##TriggerFrom", ref triggerFrom))
            {
                using (History.BeginScope("Change Loop Trigger From"))
                {
                    loop.TriggerFrom = triggerFrom;
                }
            }

            ImGui.SameLine();
            ImGui.Text("and");
            ImGui.SameLine();

            var triggerTo = loop.TriggerTo;
            ImGui.SetNextItemWidth(96);
            if (ImGui.InputInt("bars...##TriggerTo", ref triggerTo))
            {
                using (History.BeginScope("Change Loop Trigger To"))
                {
                    loop.TriggerTo = triggerTo;
                }
            }

            var fractional = loop.FractionalTime;
            if (ImGui.Checkbox("Fractional Time", ref fractional))
            {
                using (History.BeginScope("Change Loop Fractional Time"))
                {
                    loop.FractionalTime = fractional;
                }
            }
        }

        ImGui.SeparatorText("...and loop between...");
        {
            var loopTimesFrom = loop.LoopTimesFrom;
            ImGui.SetNextItemWidth(96);
            if (ImGui.InputInt("##TimesFrom", ref loopTimesFrom))
            {
                using (History.BeginScope("Change Loop Times From"))
                {
                    loop.LoopTimesFrom = loopTimesFrom;
                }
            }

            ImGui.SameLine();
            ImGui.Text("and");
            ImGui.SameLine();

            var loopTimesTo = loop.LoopTimesTo;
            ImGui.SetNextItemWidth(96);
            if (ImGui.InputInt("times.##TimesTo", ref loopTimesTo))
            {
                using (History.BeginScope("Change Loop Times To"))
                {
                    loop.LoopTimesTo = loopTimesTo;
                }
            }
        }


        ImGui.SeparatorText("The loop is...");
        {
            var duration = loop.Duration;
            if (ImGui.InputInt("bars long.##Duration", ref duration))
            {
                using (History.BeginScope("Change Loop Duration"))
                {
                    loop.Duration = duration;
                }
            }
        }

        ImGui.SeparatorText("Delay first trigger by...");
        {
            var delay = loop.Delay;
            if (ImGui.InputInt("bars.##Delay", ref delay))
            {
                using (History.BeginScope("Change Loop Delay"))
                {
                    loop.Delay = delay;
                }
            }
        }

        var oneAtATime = loop.OneAtATime;
        if (ImGui.Checkbox("One-at-a-time", ref oneAtATime))
        {
            using (History.BeginScope("Change Loop One At a Time"))
            {
                loop.OneAtATime = oneAtATime;
            }
        }

        ImGui.SameLine();

        var cutOffTail = loop.CutOffTail;
        if (ImGui.Checkbox("Cut off tail", ref cutOffTail))
        {
            using (History.BeginScope("Change Loop Cut Off Tail"))
            {
                loop.CutOffTail = cutOffTail;
            }
        }

        ImGui.SeparatorText("Time of day");
        {
            var day = loop.Day;
            if (ImGui.Checkbox("Day", ref day))
            {
                using (History.BeginScope("Change Loop Day"))
                {
                    loop.Day = day;
                }
            }

            ImGui.SameLine();

            var night = loop.Night;
            if (ImGui.Checkbox("Night", ref night))
            {
                using (History.BeginScope("Change Loop Night"))
                {
                    loop.Night = night;
                }
            }

            ImGui.SameLine();

            var dawn = loop.Dawn;
            if (ImGui.Checkbox("Dawn", ref dawn))
            {
                using (History.BeginScope("Change Loop Dawn"))
                {
                    loop.Dawn = dawn;
                }
            }

            ImGui.SameLine();

            var dusk = loop.Dusk;
            if (ImGui.Checkbox("Dusk", ref dusk))
            {
                using (History.BeginScope("Change Loop Dusk"))
                {
                    loop.Dusk = dusk;
                }
            }
        }
    }

    private static bool RenderInt(int index, ref int item)
    {
        return ImGui.InputInt("##item" + index, ref item);
    }

    private void MoveLoop(int from, int to)
    {
        using (History.BeginScope($"Move loop from {from} to {to}"))
        {
            var loop = _trackedSong.Loops[from];
            _trackedSong.Loops.RemoveAt(from);
            _trackedSong.Loops.Insert(to, loop);
        }
    }

    private bool TryFindLoopPath(string loopName, out string loopPath)
    {
        var loopRelativePath = loopName.Replace(" ^ ", "/").ToLowerInvariant();
        var trackedSongDirectory = Title[..Title.LastIndexOf('/')];

        var pathsToCheck = new[]
        {
            $"{trackedSongDirectory}/{loopRelativePath}",
            $"music/{loopRelativePath}",
            loopRelativePath
        };

        foreach (var path in pathsToCheck)
        {
            if (ResourceService.Exists(path))
            {
                loopPath = path;
                return true;
            }
        }
        loopPath = string.Empty;
        return false;
    }

    public static object Create(string name)
    {
        return new TrackedSong
        {
            Name = name
        };
    }
}