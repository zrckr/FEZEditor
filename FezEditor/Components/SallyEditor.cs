using FezEditor.Services;
using FezEditor.Structure;
using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FezEditor.Components;

public class SallyEditor : EditorComponent
{
    private const string Missing = "MISSING";
    
    public override object Asset => _saveData;
    
    private readonly EditWindow _edit;

    private readonly ConfirmWindow _confirm;

    private readonly ResourceService _resources;
    
    private readonly SaveData _saveData;
    
    private readonly Dictionary<string, Texture2D> _iconsPath = new(StringComparer.OrdinalIgnoreCase);
    
    private Dictionary<string, string> _listing = null!;

    private int _levelIndex = -1;
    
    private string _levelName = "";

    private State _nextState = State.PropertiesView;

    private Func<SaveData>? _saveSlotOverrider;

    public SallyEditor(Game game, string title, SaveData saveData) : base(game, title)
    {
        _resources = game.GetService<ResourceService>();
        _saveData = saveData;
        History.Track(saveData);
        Game.AddComponent(_edit = new EditWindow(game));
        Game.AddComponent(_confirm = new ConfirmWindow(game));
    }

    public override void Dispose()
    {
        Game.RemoveComponent(_edit);
        Game.RemoveComponent(_confirm);
    }

    public override void LoadContent()
    {
        _listing = ContentManager.LoadJson<Dictionary<string, string>>("MapScreensListing");
    }

    public override void Draw()
    {
        ImGuiX.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 8));
        
        DrawToolbar();
        
        var availSize = ImGui.GetContentRegionAvail();
        var width = availSize.X / 3f;

        if (ImGuiX.BeginChild("##Properties", new Vector2(width, 0), ImGuiChildFlags.Border))
        {
            DrawProperties();
            ImGui.EndChild();
        }
        
        ImGui.SameLine();
        
        if (ImGuiX.BeginChild("##LevelList", new Vector2(width, 0), ImGuiChildFlags.Border))
        {
            DrawLevelList();
            ImGui.EndChild();
        }
        
        ImGui.SameLine();
        
        if (ImGuiX.BeginChild("##LevelProperties", Vector2.Zero, ImGuiChildFlags.Border))
        {
            DrawLevelProperties();
            ImGui.EndChild();
        }

        DrawRenameLevelModal();
        DrawDeleteLevelModal();
        DrawOverrideSaveSlotModal();

        ImGui.PopStyleVar();
    }

    private void DrawToolbar()
    {
        if (ImGui.Button($"{Icons.Refresh} New Save"))
        {
            _nextState = State.OverrideSaveSlot;
            _saveSlotOverrider = () => new SaveData();
        }
        
        ImGui.SameLine();

        if (ImGui.Button($"{Icons.StarEmpty} 209.4% Save"))
        {
            _nextState = State.OverrideSaveSlot;
            _saveSlotOverrider = () => _resources.LoadSaveDataFromContent("SaveSlots/209,4%");
        }
        
        ImGui.SameLine();
        
        if (ImGui.Button($"{Icons.StarFull} Full Completion Save"))
        {
            _nextState = State.OverrideSaveSlot;
            _saveSlotOverrider = () => _resources.LoadSaveDataFromContent("SaveSlots/100%");
        }
    }

    private void DrawProperties()
    {
        var isNew = _saveData.IsNew;
        if (ImGui.Checkbox("Is New", ref isNew))
        {
            using (History.BeginScope("Edit Is New"))
            {
                _saveData.IsNew = isNew;
            }
        }
        
        var creationTime = _saveData.CreationTime;
        if (ImGuiX.DateTimeInput("Creation Time:", ref creationTime))
        {
            using (History.BeginScope("Edit Creation Time"))
            {
                _saveData.CreationTime = creationTime;
            }
        }
        
        var playTime = _saveData.PlayTime;
        if (ImGuiX.TimeSpanInput("Play Time:", ref playTime))
        {
            using (History.BeginScope("Edit Play Time"))
            {
                _saveData.PlayTime = playTime;
            }
        }
        
        var finished32 = _saveData.Finished32;
        if (ImGui.Checkbox("Finished 32", ref finished32))
        {
            using (History.BeginScope("Edit Finished 32"))
            {
                _saveData.Finished32 = finished32;
            }
        }
        
        var finished64 = _saveData.Finished64;
        if (ImGui.Checkbox("Finished 64", ref finished64))
        {
            using (History.BeginScope("Edit Finished 64"))
            {
                _saveData.Finished64 = finished64;
            }
        }
        
        var hasFpView = _saveData.HasFpView;
        if (ImGui.Checkbox("Has Fp View", ref hasFpView))
        {
            using (History.BeginScope("Edit Has Fp View"))
            {
                _saveData.HasFpView = hasFpView;
            }
        }
        
        var hasStereo3D = _saveData.HasStereo3D;
        if (ImGui.Checkbox("Has Stereo 3D", ref hasStereo3D))
        {
            using (History.BeginScope("Edit Has Stereo 3 D"))
            {
                _saveData.HasStereo3D = hasStereo3D;
            }
        }
        
        var canNewGamePlus = _saveData.CanNewGamePlus;
        if (ImGui.Checkbox("Can New Game Plus", ref canNewGamePlus))
        {
            using (History.BeginScope("Edit Can New Game Plus"))
            {
                _saveData.CanNewGamePlus = canNewGamePlus;
            }
        }
        
        var isNewGamePlus = _saveData.IsNewGamePlus;
        if (ImGui.Checkbox("Is New Game Plus", ref isNewGamePlus))
        {
            using (History.BeginScope("Edit Is New Game Plus"))
            {
                _saveData.IsNewGamePlus = isNewGamePlus;
            }
        }
        
        ImGui.Text("One Time Tutorials:");
        ImGui.SameLine();
        var count = "key" + (_saveData.OneTimeTutorials.Count > 1 ? "s" : "");
        var header = $"Dictionary ({_saveData.OneTimeTutorials.Count} {count})";
        if (ImGui.CollapsingHeader(header))
        {
            if (ImGuiX.BeginListBox("##OneTimeTutorials", new Vector2(-1, 0)))
            {
                foreach (var key in _saveData.OneTimeTutorials.Keys.ToList())
                {
                    var value = _saveData.OneTimeTutorials[key];
                    if (ImGui.Checkbox(key, ref value))
                    {
                        using (History.BeginScope("Edit One Time Tutorial"))
                        {
                            _saveData.OneTimeTutorials[key] = value;
                        }
                    }
                }
        
                ImGui.EndListBox();
            }
        }

        var level = _saveData.Level ?? "";
        if (ImGui.InputText("Level", ref level, 255))
        {
            using (History.BeginScope("Edit Level"))
            {
                _saveData.Level = level;
            }
        }
        
        var view = (int)_saveData.View;
        var views = Enum.GetNames<Viewpoint>();
        if (ImGui.Combo("View", ref view, views, views.Length))
        {
            using (History.BeginScope("Edit View"))
            {
                _saveData.View = (Viewpoint)view;
            }
        }
        
        var ground = _saveData.Ground;
        if (ImGuiX.DragFloat3("Ground", ref ground))
        {
            using (History.BeginScope("Edit Ground"))
            {
                _saveData.Ground = ground;
            }
        }
        
        var timeOfDay = _saveData.TimeOfDay;
        if (ImGuiX.TimeSpanInput("Time Of Day:", ref timeOfDay))
        {
            using (History.BeginScope("Edit Time Of Day"))
            {
                _saveData.TimeOfDay = timeOfDay;
            }
        }
        
        var keys = _saveData.Keys;
        if (ImGui.InputInt("Keys", ref keys))
        {
            using (History.BeginScope("Edit Keys"))
            {
                _saveData.Keys = keys;
            }
        }
        
        var cubeShards = _saveData.CubeShards;
        if (ImGui.InputInt("Cube Shards", ref cubeShards))
        {
            using (History.BeginScope("Edit Cube Shards"))
            {
                _saveData.CubeShards = cubeShards;
            }
        }
        
        var secretCubes = _saveData.SecretCubes;
        if (ImGui.InputInt("Secret Cubes", ref secretCubes))
        {
            using (History.BeginScope("Edit Secret Cubes"))
            {
                _saveData.SecretCubes = secretCubes;
            }
        }
        
        var collectedParts = _saveData.CollectedParts;
        if (ImGui.InputInt("Collected Parts", ref collectedParts))
        {
            using (History.BeginScope("Edit Collected Parts"))
            {
                _saveData.CollectedParts = collectedParts;
            }
        }
        
        var collectedOwls = _saveData.CollectedOwls;
        if (ImGui.InputInt("Collected Owls", ref collectedOwls))
        {
            using (History.BeginScope("Edit Collected Owls"))
            {
                _saveData.CollectedOwls = collectedOwls;
            }
        }
        
        var piecesOfHeart = _saveData.PiecesOfHeart;
        if (ImGui.InputInt("Pieces Of Heart", ref piecesOfHeart))
        {
            using (History.BeginScope("Edit Pieces Of Heart"))
            {
                _saveData.PiecesOfHeart = piecesOfHeart;
            }
        }
        
        var maps = _saveData.Maps;
        if (ImGuiX.EditableList("Maps", ref maps, RenderString, () => string.Empty))
        {
            using (History.BeginScope("Edit Maps"))
            {
                _saveData.Maps = maps;
            }
        }

        var artifacts = _saveData.Artifacts;
        if (ImGuiX.EditableList("Artifacts", ref artifacts, RenderEnum, () => ActorType.None))
        {
            using (History.BeginScope("Edit Artifacts"))
            {
                _saveData.Artifacts = artifacts;
            }
        }
        
        var earnedAchievements = _saveData.EarnedAchievements;
        if (ImGuiX.EditableList("Earned Achievements", ref earnedAchievements, RenderString, () => string.Empty))
        {
            using (History.BeginScope("Edit Earned Achievements"))
            {
                _saveData.EarnedAchievements = earnedAchievements;
            }
        }
        
        var earnedGamerPictures = _saveData.EarnedGamerPictures;
        if (ImGuiX.EditableList("Earned Gamer Pictures", ref earnedGamerPictures, RenderString, () => string.Empty))
        {
            using (History.BeginScope("Edit Earned Gamer Pictures"))
            {
                _saveData.EarnedGamerPictures = earnedGamerPictures;
            }
        }
        
        var scriptingState = _saveData.ScriptingState ?? "";
        if (ImGui.InputText("Scripting State", ref scriptingState, 255))
        {
            using (History.BeginScope("Edit Scripting State"))
            {
                _saveData.ScriptingState = scriptingState;
            }
        }
        
        var fezHidden = _saveData.FezHidden;
        if (ImGui.Checkbox("Fez Hidden", ref fezHidden))
        {
            using (History.BeginScope("Edit Fez Hidden"))
            {
                _saveData.FezHidden = fezHidden;
            }
        }
        
        var globalWaterLevelModifier = _saveData.GlobalWaterLevelModifier ?? 0f;
        ImGui.SetNextItemWidth(128);
        if (ImGui.DragFloat("Global Water Level Modifier", ref globalWaterLevelModifier))
        {
            using (History.BeginScope("Edit Global Water Level Modifier"))
            {
                _saveData.GlobalWaterLevelModifier = globalWaterLevelModifier;
            }
        }
        
        var hasHadMapHelp = _saveData.HasHadMapHelp;
        if (ImGui.Checkbox("Has Had Map Help", ref hasHadMapHelp))
        {
            using (History.BeginScope("Edit Has Had Map Help"))
            {
                _saveData.HasHadMapHelp = hasHadMapHelp;
            }
        }
        
        var canOpenMap = _saveData.CanOpenMap;
        if (ImGui.Checkbox("Can Open Map", ref canOpenMap))
        {
            using (History.BeginScope("Edit Can Open Map"))
            {
                _saveData.CanOpenMap = canOpenMap;
            }
        }
        
        var achievementCheatCodeDone = _saveData.AchievementCheatCodeDone;
        if (ImGui.Checkbox("Achievement Cheat Code Done", ref achievementCheatCodeDone))
        {
            using (History.BeginScope("Edit Achievement Cheat Code Done"))
            {
                _saveData.AchievementCheatCodeDone = achievementCheatCodeDone;
            }
        }
        
        var anyCodeDeciphered = _saveData.AnyCodeDeciphered;
        if (ImGui.Checkbox("Any Code Deciphered", ref anyCodeDeciphered))
        {
            using (History.BeginScope("Edit Any Code Deciphered"))
            {
                _saveData.AnyCodeDeciphered = anyCodeDeciphered;
            }
        }
        
        var mapCheatCodeDone = _saveData.MapCheatCodeDone;
        if (ImGui.Checkbox("Map Cheat Code Done", ref mapCheatCodeDone))
        {
            using (History.BeginScope("Edit Map Cheat Code Done"))
            {
                _saveData.MapCheatCodeDone = mapCheatCodeDone;
            }
        }
        
        var scoreDirty = _saveData.ScoreDirty;
        if (ImGui.Checkbox("Score Dirty", ref scoreDirty))
        {
            using (History.BeginScope("Edit Score Dirty"))
            {
                _saveData.ScoreDirty = scoreDirty;
            }
        }
        
        var hasDoneHeartReboot = _saveData.HasDoneHeartReboot;
        if (ImGui.Checkbox("Has Done Heart Reboot", ref hasDoneHeartReboot))
        {
            using (History.BeginScope("Edit Has Done Heart Reboot"))
            {
                _saveData.HasDoneHeartReboot = hasDoneHeartReboot;
            }
        }
        
    }

    private void DrawLevelList()
    {
        ImGui.Text("Levels");
        ImGui.Separator();
        
        if (ImGui.Button($"{Icons.Add} Add"))
        {
            using (History.BeginScope("Add New Level"))
            {
                var newName = $"UNTITLED_{_saveData.World.Count + 1}";
                _saveData.World.Add(newName, new LevelSaveData());
                _levelIndex = _saveData.World.Count - 1;
            }
        }
        
        ImGui.SameLine();
        ImGui.BeginDisabled(_levelIndex == -1);
        if (ImGui.Button($"{Icons.Rename} Rename"))
        {
            _nextState = State.RenameLevel;
        }
        ImGui.EndDisabled();
            
        ImGui.SameLine();
        ImGui.BeginDisabled(_levelIndex == -1);
        if (ImGui.Button($"{Icons.Remove} Remove"))
        {
            _nextState = State.RemoveLevel;
        }
        ImGui.EndDisabled();
            
        ImGui.Separator();
        
        if (ImGui.BeginChild("##LevelList"))
        {
            if (ImGui.IsWindowHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered())
            {
                _levelIndex = -1;
            }

            if (_saveData.World.Count == 0)
            {
                const string emptyText = "There's no levels...";
                ImGuiX.SetTextCentered(emptyText);
                ImGui.Text(emptyText);
            }
            
            for (var i = 0; i < _saveData.World.Count; i++)
            {
                var level = _saveData.World.GetAt(i).Key;
                var icon = GetOrLoadIcon(level);
                if (ImGuiX.SelectableWithImage(icon, new Vector2(64, 64), level, i == _levelIndex))
                {
                    _levelIndex = i;
                }
            }

            ImGui.EndChild();
        }
    }

    private Texture2D GetOrLoadIcon(string level)
    {
        var path = _listing.GetValueOrDefault(level, _listing[Missing]);
        if (_iconsPath.TryGetValue(path, out var icon))
        {
            return icon;
        }
        
        icon = ContentManager.Load<Texture2D>(path);
        _iconsPath[path] = icon;
        return icon;
    }

    private void DrawLevelProperties()
    {
        if (_levelIndex == -1)
        {
            const string emptyText = "Select level from the list";
            ImGuiX.SetTextCentered(emptyText);
            ImGui.Text(emptyText);
            return;
        }
        
        ImGui.SeparatorText("Level Save data");
        var levelData = _saveData.World.GetAt(_levelIndex).Value;
        
        var lastStableLiquidHeight = levelData.LastStableLiquidHeight ?? 0f;
        ImGui.SetNextItemWidth(240);
        if (ImGui.DragFloat("Last Stable Liquid Height", ref lastStableLiquidHeight)) 
        {
            using (History.BeginScope("Edit Last Stable")) 
            {
                levelData.LastStableLiquidHeight = lastStableLiquidHeight;
            }
        }
        
        var scriptingState = levelData.ScriptingState ?? "";
        if (ImGui.InputText("Scripting State", ref scriptingState, 255)) 
        {
            using (History.BeginScope("Edit Scripting State")) 
            {
                levelData.ScriptingState = scriptingState;
            }
        }
        
        var firstVisit = levelData.FirstVisit;
        if (ImGui.Checkbox("First Visit", ref firstVisit)) 
        {
            using (History.BeginScope("Edit First Visit")) 
            {
                levelData.FirstVisit = firstVisit;
            }
        }

        var destroyedTriles = levelData.DestroyedTriles;
        if (ImGuiX.EditableList("Destroyed Triles", ref destroyedTriles, RenderTrileEmplacement, () => new TrileEmplacement())) 
        {
            using (History.BeginScope("Edit Destroyed Triles")) 
            {
                levelData.DestroyedTriles = destroyedTriles;
            }
        }
        
        var inactiveTriles = levelData.InactiveTriles;
        if (ImGuiX.EditableList("Inactive Triles", ref inactiveTriles, RenderTrileEmplacement, () => new TrileEmplacement())) 
        {
            using (History.BeginScope("Edit Inactive Triles")) 
            {
                levelData.InactiveTriles = inactiveTriles;
            }
        }
        
        var inactiveArtObjects = levelData.InactiveArtObjects;
        if (ImGuiX.EditableList("Inactive Art Objects", ref inactiveArtObjects, RenderInt, () => 0)) 
        {
            using (History.BeginScope("Edit Inactive Art")) 
            {
                levelData.InactiveArtObjects = inactiveArtObjects;
            }
        }
        
        var inactiveEvents = levelData.InactiveEvents;
        if (ImGuiX.EditableList("Inactive Events", ref inactiveEvents, RenderInt, () => 0)) 
        {
            using (History.BeginScope("Edit Inactive Events")) 
            {
                levelData.InactiveEvents = inactiveEvents;
            }
        }
        
        var inactiveGroups = levelData.InactiveGroups;
        if (ImGuiX.EditableList("Inactive Groups", ref inactiveGroups, RenderInt, () => 0)) 
        {
            using (History.BeginScope("Edit Inactive Groups")) 
            {
                levelData.InactiveGroups = inactiveGroups;
            }
        }
        
        var inactiveVolumes = levelData.InactiveVolumes;
        if (ImGuiX.EditableList("Inactive Volumes", ref inactiveVolumes, RenderInt, () => 0)) 
        {
            using (History.BeginScope("Edit Inactive Volumes")) 
            {
                levelData.InactiveVolumes = inactiveVolumes;
            }
        }
        
        var inactiveNpcs = levelData.InactiveNPCs;
        if (ImGuiX.EditableList("Inactive NPCs", ref inactiveNpcs, RenderInt, () => 0)) 
        {
            using (History.BeginScope("Edit Inactive NPCs")) 
            {
                levelData.InactiveNPCs = inactiveNpcs;
            }
        }
        
        var pivotRotations = levelData.PivotRotations;
        if (ImGuiX.EditableDict("Pivot Rotations", ref pivotRotations, RenderIntPair, AddIntKey, () => 0)) 
        {
            using (History.BeginScope("Edit Pivot Rotations")) 
            {
                levelData.PivotRotations = pivotRotations;
            }
        }
        
        ImGui.SeparatorText("Filled Win Conditions");
        var winConditions = levelData.FilledConditions;
        
        var chestCount = winConditions.ChestCount;
        if (ImGui.InputInt("Chest Count", ref chestCount))
        {
            using (History.BeginScope("Edit Chest Count"))
            {
                winConditions.ChestCount = chestCount;
            }
        }    
        
        var lockedDoorCount = winConditions.LockedDoorCount;
        if (ImGui.InputInt("Locked Door Count", ref lockedDoorCount))
        {
            using (History.BeginScope("Edit Locked Door Count"))
            {
                winConditions.LockedDoorCount = lockedDoorCount;
            }
        }    
        
        var unlockedDoorCount = winConditions.UnlockedDoorCount;
        if (ImGui.InputInt("Unlocked Door Count", ref unlockedDoorCount))
        {
            using (History.BeginScope("Edit Unlocked Door Count"))
            {
                winConditions.UnlockedDoorCount = unlockedDoorCount;
            }
        }    
        
        var cubeShardCount = winConditions.CubeShardCount;
        if (ImGui.InputInt("Cube Shard Count", ref cubeShardCount))
        {
            using (History.BeginScope("Edit Cube Shard Count"))
            {
                winConditions.CubeShardCount = cubeShardCount;
            }
        }    
        
        var otherCollectibleCount = winConditions.OtherCollectibleCount;
        if (ImGui.InputInt("Other Collectible Count", ref otherCollectibleCount))
        {
            using (History.BeginScope("Edit Other Collectible Count"))
            {
                winConditions.OtherCollectibleCount = otherCollectibleCount;
            }
        }    
        
        var splitUpCount = winConditions.SplitUpCount;
        if (ImGui.InputInt("Split Up Count", ref splitUpCount))
        {
            using (History.BeginScope("Edit Split Up Count"))
            {
                winConditions.SplitUpCount = splitUpCount;
            }
        }    
        
        var secretCount = winConditions.SecretCount;
        if (ImGui.InputInt("Secret Count", ref secretCount))
        {
            using (History.BeginScope("Edit Secret Count"))
            {
                winConditions.SecretCount = secretCount;
            }
        }
        
        var scriptIds = winConditions.ScriptIds;
        if (ImGuiX.EditableList("Script Ids", ref scriptIds, RenderInt, () => 0))
        {
            using (History.BeginScope("Edit Script Ids"))
            {
                winConditions.ScriptIds = scriptIds;
            }
        }
    }

    private static bool RenderTrileEmplacement(int index, ref TrileEmplacement item)
    {
        var values = new[] { item.X, item.Y, item.Z };
        var changed = ImGui.DragInt3("##item", ref values[0]);
        if (changed) item = new TrileEmplacement(values[0], values[1], values[2]);
        return changed;
    }

    private static bool RenderIntPair(int key, ref int value)
    {
        ImGui.Text(key.ToString());
        ImGui.SameLine();
        return ImGui.InputInt($"##{key}_value", ref value);
    }

    private static bool AddIntKey(ref int key)
    {
        return ImGui.InputInt("##item", ref key);
    }

    private static bool RenderInt(int index, ref int item)
    {
        return ImGui.InputInt("##item", ref item);
    }

    private static bool RenderString(int i, ref string item)
    {
        return ImGui.InputText("##item", ref item, 256);
    }
    
    private static bool RenderEnum<T>(int index, ref T item) where T : Enum
    {
        var name = (int)Enum.GetValues(typeof(T)).GetValue(index)!;
        var names = Enum.GetNames(typeof(T));

        if (ImGui.Combo("##item", ref name, names, names.Length))
        {
            item = (T)Enum.ToObject(typeof(T), name);
            return true;
        }

        return false;
    }
    
    private void DrawRenameLevelModal()
    {
        if (_nextState != State.RenameLevel)
        {
            return;
        }
        
        _nextState = State.PropertiesView;
        _levelName = _saveData.World.GetAt(_levelIndex).Key;

        _edit.Text = "Enter new level name:";
        _edit.EditValue = () =>
        {
            ImGui.InputText("##NewLevelName", ref _levelName, 255);
           
            var levelExists = _saveData.World.ContainsKey(_levelName);
            if (levelExists)
            {
                ImGuiX.TextColored(Color.Red, "Level already exists.");
            }

            var levelEmpty = string.IsNullOrWhiteSpace(_levelName);
            if (levelEmpty)
            {
                ImGuiX.TextColored(Color.Red, "Level cannot be empty.");
            }

            return !levelExists && !levelEmpty;
        };
        
        _edit.Accepted = () =>
        {
            using (History.BeginScope("Rename Level"))
            {
                var kv = _saveData.World.GetAt(_levelIndex);
                _saveData.World.Remove(kv.Key);
                _saveData.World[_levelName] = kv.Value;
                _levelIndex = -1;
                _levelName = "";
            }
        };
    }

    private void DrawDeleteLevelModal()
    {
        if (_nextState != State.RemoveLevel)
        {
            return;
        }
        
        _nextState = State.PropertiesView;
        _levelName = _saveData.World.GetAt(_levelIndex).Key;
        
        _confirm.Text = "Are you sure you want to delete " + _levelName + "?";
        _confirm.Confirmed = () =>
        {
            using (History.BeginScope("Delete Level"))
            {
                _saveData.World.Remove(_levelName);
                _levelIndex = -1;
            }
        };
    }

    private void DrawOverrideSaveSlotModal()
    {
        if (_nextState != State.OverrideSaveSlot)
        {
            return;
        }
        
        _nextState = State.PropertiesView;
        _confirm.Text = "Are you sure you want to override this SaveSlot?";
        _confirm.Confirmed = () =>
        {
            using (History.BeginScope("Override SaveSlot"))
            {
                var saveData = _saveSlotOverrider!.Invoke();
                saveData.CopyTo(_saveData);
            }
        };
    }

    private enum State
    {
        PropertiesView,
        RenameLevel,
        RemoveLevel,
        OverrideSaveSlot
    }
}