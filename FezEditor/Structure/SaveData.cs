using FezEditor.Tools;
using FEZRepacker.Core.Definitions.Game.Common;
using FEZRepacker.Core.Definitions.Game.Level;
using FEZRepacker.Core.Definitions.Game.MapTree;
using Microsoft.Xna.Framework;

namespace FezEditor.Structure;

public sealed class SaveData
{
    private const long Version = 6L;

    private const int EasyStorageMaxSize = 40960;
    
    public bool IsNew { get; set; } = true;

    public DateTime CreationTime { get; set; } = DateTime.Now.ToUniversalTime();

    public TimeSpan PlayTime { get; set; }

    public bool CanNewGamePlus { get; set; }

    public bool IsNewGamePlus { get; set; }

    public bool Finished32 { get; set; }

    public bool Finished64 { get; set; }

    public bool HasFpView { get; set; }

    public bool HasStereo3D { get; set; }

    public bool HasDoneHeartReboot { get; set; }

    public string? Level { get; set; }

    public Viewpoint View { get; set; }

    public Vector3 Ground { get; set; }

    public TimeSpan TimeOfDay { get; set; } = TimeSpan.FromHours(12.0);

    public List<string> UnlockedWarpDestinations { get; set; } = ["NATURE_HUB"];

    public int Keys { get; set; }

    public int CubeShards { get; set; }

    public int SecretCubes { get; set; }

    public int CollectedParts { get; set; }

    public int CollectedOwls { get; set; }

    public int PiecesOfHeart { get; set; }

    public List<string> Maps { get; set; } = [];

    public List<ActorType> Artifacts { get; set; } = [];

    public List<string> EarnedAchievements { get; set; } = [];

    public List<string> EarnedGamerPictures { get; set; } = [];

    public bool ScoreDirty { get; set; }

    public string? ScriptingState { get; set; }

    public bool FezHidden { get; set; }

    public float? GlobalWaterLevelModifier { get; set; }

    public bool HasHadMapHelp { get; set; }

    public bool CanOpenMap { get; set; }

    public bool AchievementCheatCodeDone { get; set; }

    public bool MapCheatCodeDone { get; set; }

    public bool AnyCodeDeciphered { get; set; }

    public OrderedDictionary<string, LevelSaveData> World { get; set; } = new();

    public Dictionary<string, bool> OneTimeTutorials { get; set; } = new()
    {
        { "DOT_LOCKED_DOOR_A", false },
        { "DOT_NUT_N_BOLT_A", false },
        { "DOT_PIVOT_A", false },
        { "DOT_TIME_SWITCH_A", false },
        { "DOT_TOMBSTONE_A", false },
        { "DOT_TREASURE", false },
        { "DOT_VALVE_A", false },
        { "DOT_WEIGHT_SWITCH_A", false },
        { "DOT_LESSER_A", false },
        { "DOT_WARP_A", false },
        { "DOT_BOMB_A", false },
        { "DOT_CLOCK_A", false },
        { "DOT_CRATE_A", false },
        { "DOT_TELESCOPE_A", false },
        { "DOT_WELL_A", false },
        { "DOT_WORKING", false }
    };

    public void CopyTo(SaveData d)
    {
        d.AchievementCheatCodeDone = AchievementCheatCodeDone;
        d.AnyCodeDeciphered = AnyCodeDeciphered;
        d.CanNewGamePlus = CanNewGamePlus;
        d.CanOpenMap = CanOpenMap;
        d.CollectedOwls = CollectedOwls;
        d.CollectedParts = CollectedParts;
        d.CreationTime = CreationTime;
        d.CubeShards = CubeShards;
        d.FezHidden = FezHidden;
        d.Finished32 = Finished32;
        d.Finished64 = Finished64;
        d.GlobalWaterLevelModifier = GlobalWaterLevelModifier;
        d.Ground = Ground;
        d.HasDoneHeartReboot = HasDoneHeartReboot;
        d.HasFpView = HasFpView;
        d.HasHadMapHelp = HasHadMapHelp;
        d.HasStereo3D = HasStereo3D;
        d.IsNew = IsNew;
        d.IsNewGamePlus = IsNewGamePlus;
        d.Keys = Keys;
        d.Level = Level;
        d.MapCheatCodeDone = MapCheatCodeDone;
        d.PiecesOfHeart = PiecesOfHeart;
        d.PlayTime = PlayTime;
        d.ScoreDirty = ScoreDirty;
        d.ScriptingState = ScriptingState;
        d.SecretCubes = SecretCubes;
        d.TimeOfDay = TimeOfDay;
        d.View = View;
        
        d.Artifacts.Clear();
        d.Artifacts.AddRange(Artifacts);
        d.EarnedAchievements.Clear();
        d.EarnedAchievements.AddRange(EarnedAchievements);
        d.EarnedGamerPictures.Clear();
        d.EarnedGamerPictures.AddRange(EarnedGamerPictures);
        d.Maps.Clear();
        d.Maps.AddRange(Maps);
        d.UnlockedWarpDestinations.Clear();
        d.UnlockedWarpDestinations.AddRange(UnlockedWarpDestinations);
        d.OneTimeTutorials.Clear();
        
        foreach (var key in OneTimeTutorials.Keys)
        {
            d.OneTimeTutorials.Add(key, OneTimeTutorials[key]);
        }

        var keys = World.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!d.World.TryGetValue(key, out var value))
            {
                value = new LevelSaveData();
                d.World.Add(key, value);
            }

            value.CopyTo(value);
        }

        keys = d.World.Keys.ToArray();
        foreach (var key in keys)
        {
            if (!World.ContainsKey(key))
            {
                d.World.Remove(key);
            }
        }
    }
    
    public static SaveData Read(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        var saveData = new SaveData();
        reader.ReadInt64();     // Value written in EasyStorage.dll
        
        var version = reader.ReadInt64();
        if (version != Version)
        {
            throw new IOException($"Invalid version: {version} (expected {Version})");
        }

        saveData.CreationTime = reader.ReadDateTime();
        saveData.Finished32 = reader.ReadBoolean();
        saveData.Finished64 = reader.ReadBoolean();
        saveData.HasFpView = reader.ReadBoolean();
        saveData.HasStereo3D = reader.ReadBoolean();
        saveData.CanNewGamePlus = reader.ReadBoolean();
        saveData.IsNewGamePlus = reader.ReadBoolean();
        saveData.OneTimeTutorials.Clear();

        var capacity = reader.ReadInt32();
        saveData.OneTimeTutorials = new Dictionary<string, bool>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            saveData.OneTimeTutorials.Add(reader.ReadNullableString()!, reader.ReadBoolean());
        }

        saveData.Level = reader.ReadNullableString();
        saveData.View = (Viewpoint)reader.ReadInt32();
        saveData.Ground = reader.ReadVector3();
        saveData.TimeOfDay = reader.ReadTimeSpan();

        capacity = reader.ReadInt32();
        saveData.UnlockedWarpDestinations = new List<string>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            saveData.UnlockedWarpDestinations.Add(reader.ReadNullableString()!);
        }

        saveData.Keys = reader.ReadInt32();
        saveData.CubeShards = reader.ReadInt32();
        saveData.SecretCubes = reader.ReadInt32();
        saveData.CollectedParts = reader.ReadInt32();
        saveData.CollectedOwls = reader.ReadInt32();
        saveData.PiecesOfHeart = reader.ReadInt32();
        if (saveData.SecretCubes > 32 || saveData.CubeShards > 32 || saveData.PiecesOfHeart > 3)
        {
            saveData.ScoreDirty = true;
        }

        saveData.SecretCubes = Math.Min(saveData.SecretCubes, 32);
        saveData.CubeShards = Math.Min(saveData.CubeShards, 32);
        saveData.PiecesOfHeart = Math.Min(saveData.PiecesOfHeart, 3);

        capacity = reader.ReadInt32();
        saveData.Maps = new List<string>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            saveData.Maps.Add(reader.ReadNullableString()!);
        }

        capacity = reader.ReadInt32();
        saveData.Artifacts = new List<ActorType>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            saveData.Artifacts.Add((ActorType)reader.ReadInt32());
        }

        capacity = reader.ReadInt32();
        saveData.EarnedAchievements = new List<string>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            saveData.EarnedAchievements.Add(reader.ReadNullableString()!);
        }

        capacity = reader.ReadInt32();
        saveData.EarnedGamerPictures = new List<string>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            saveData.EarnedGamerPictures.Add(reader.ReadNullableString()!);
        }

        saveData.ScriptingState = reader.ReadNullableString();
        saveData.FezHidden = reader.ReadBoolean();
        saveData.GlobalWaterLevelModifier = reader.ReadNullableSingle();
        saveData.HasHadMapHelp = reader.ReadBoolean();
        saveData.CanOpenMap = reader.ReadBoolean();
        saveData.AchievementCheatCodeDone = reader.ReadBoolean();
        saveData.AnyCodeDeciphered = reader.ReadBoolean();
        saveData.MapCheatCodeDone = reader.ReadBoolean();

        capacity = reader.ReadInt32();
        saveData.World = new OrderedDictionary<string, LevelSaveData>();
        for (var num3 = 0; num3 < capacity; num3++)
        {
            try
            {
                saveData.World.Add(reader.ReadNullableString()!, LevelSaveData.FromReader(reader));
            }
            catch
            {
                break;
            }
        }

        reader.ReadBoolean();
        saveData.ScoreDirty = true;
        saveData.HasDoneHeartReboot = reader.ReadBoolean();
        saveData.PlayTime = reader.ReadTimeSpan();
        saveData.IsNew = string.IsNullOrEmpty(saveData.Level) ||
                         saveData.CanNewGamePlus ||
                         saveData.World.Count == 0;
        saveData.HasFpView |= saveData.HasStereo3D;
        return saveData;
    }
    
    public static Stream Write(SaveData saveData)
    {
        var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);

        w.Write(DateTime.Now.ToFileTimeUtc());
        w.Write(Version);
        w.Write(saveData.CreationTime);
        w.Write(saveData.Finished32);
        w.Write(saveData.Finished64);
        w.Write(saveData.HasFpView);
        w.Write(saveData.HasStereo3D);
        w.Write(saveData.CanNewGamePlus);
        w.Write(saveData.IsNewGamePlus);
        w.Write(saveData.OneTimeTutorials.Count);
        foreach (var oneTimeTutorial in saveData.OneTimeTutorials)
        {
            w.WriteNullable(oneTimeTutorial.Key);
            w.Write(oneTimeTutorial.Value);
        }

        w.WriteNullable(saveData.Level);
        w.Write((int)saveData.View);
        w.Write(saveData.Ground);
        w.Write(saveData.TimeOfDay);
        w.Write(saveData.UnlockedWarpDestinations.Count);
        foreach (var unlockedWarpDestination in saveData.UnlockedWarpDestinations)
        {
            w.WriteNullable(unlockedWarpDestination);
        }

        w.Write(saveData.Keys);
        w.Write(saveData.CubeShards);
        w.Write(saveData.SecretCubes);
        w.Write(saveData.CollectedParts);
        w.Write(saveData.CollectedOwls);
        w.Write(saveData.PiecesOfHeart);
        w.Write(saveData.Maps.Count);
        foreach (var map in saveData.Maps)
        {
            w.WriteNullable(map);
        }

        w.Write(saveData.Artifacts.Count);
        foreach (var artifact in saveData.Artifacts)
        {
            w.Write((int)artifact);
        }

        w.Write(saveData.EarnedAchievements.Count);
        foreach (var earnedAchievement in saveData.EarnedAchievements)
        {
            w.WriteNullable(earnedAchievement);
        }

        w.Write(saveData.EarnedGamerPictures.Count);
        foreach (var earnedGamerPicture in saveData.EarnedGamerPictures)
        {
            w.WriteNullable(earnedGamerPicture);
        }

        w.WriteNullable(saveData.ScriptingState);
        w.Write(saveData.FezHidden);
        w.WriteNullable(saveData.GlobalWaterLevelModifier);
        w.Write(saveData.HasHadMapHelp);
        w.Write(saveData.CanOpenMap);
        w.Write(saveData.AchievementCheatCodeDone);
        w.Write(saveData.AnyCodeDeciphered);
        w.Write(saveData.MapCheatCodeDone);
        w.Write(saveData.World.Count);
        foreach (var item in saveData.World)
        {
            w.WriteNullable(item.Key);
            LevelSaveData.Write(w, item.Value);
        }

        w.Write(saveData.ScoreDirty);
        w.Write(saveData.HasDoneHeartReboot);
        w.Write(saveData.PlayTime);
        w.Write(saveData.IsNew);

        if (stream.Position > EasyStorageMaxSize)
        {
            stream.Dispose();
            throw new FormatException("Save file greater than the imposed EasyStorage limit!");
        }
        
        return stream;
    }
}

public sealed class LevelSaveData
{
    public List<TrileEmplacement> DestroyedTriles { get; set; } = [];

    public List<TrileEmplacement> InactiveTriles { get; set; } = [];

    public List<int> InactiveArtObjects { get; set; } = [];

    public List<int> InactiveEvents { get; set; } = [];

    public List<int> InactiveGroups { get; set; } = [];

    public List<int> InactiveVolumes { get; set; } = [];

    public List<int> InactiveNPCs { get; set; } = [];

    public Dictionary<int, int> PivotRotations { get; set; } = new();

    public float? LastStableLiquidHeight { get; set; }

    public string? ScriptingState { get; set; }

    public WinConditions FilledConditions { get; set; } = new();

    public bool FirstVisit { get; set; }

    public void CopyTo(LevelSaveData d)
    {
        d.FilledConditions.LockedDoorCount = FilledConditions.LockedDoorCount;
        d.FilledConditions.UnlockedDoorCount = FilledConditions.UnlockedDoorCount;
        d.FilledConditions.ChestCount = FilledConditions.ChestCount;
        d.FilledConditions.CubeShardCount = FilledConditions.CubeShardCount;
        d.FilledConditions.OtherCollectibleCount = FilledConditions.OtherCollectibleCount;
        d.FilledConditions.SplitUpCount = FilledConditions.SplitUpCount;
        d.FilledConditions.SecretCount = FilledConditions.SecretCount;
        d.FilledConditions.ScriptIds.Clear();
        d.FilledConditions.ScriptIds.AddRange(FilledConditions.ScriptIds);
        
        d.FirstVisit = FirstVisit;
        d.LastStableLiquidHeight = LastStableLiquidHeight;
        d.ScriptingState = ScriptingState;
        d.DestroyedTriles.Clear();
        d.DestroyedTriles.AddRange(DestroyedTriles);
        d.InactiveArtObjects.Clear();
        d.InactiveArtObjects.AddRange(InactiveArtObjects);
        d.InactiveEvents.Clear();
        d.InactiveEvents.AddRange(InactiveEvents);
        d.InactiveGroups.Clear();
        d.InactiveGroups.AddRange(InactiveGroups);
        d.InactiveNPCs.Clear();
        d.InactiveNPCs.AddRange(InactiveNPCs);
        d.InactiveTriles.Clear();
        d.InactiveTriles.AddRange(InactiveTriles);
        d.InactiveVolumes.Clear();
        d.InactiveVolumes.AddRange(InactiveVolumes);
        d.PivotRotations.Clear();
        
        foreach (var key in PivotRotations.Keys)
        {
            d.PivotRotations.Add(key, PivotRotations[key]);
        }
    }
    
    internal static LevelSaveData FromReader(BinaryReader reader)
    {
        var levelSaveData = new LevelSaveData();

        var capacity = reader.ReadInt32();
        levelSaveData.DestroyedTriles = new List<TrileEmplacement>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.DestroyedTriles.Add(reader.ReadTrileEmplacement());
        }

        capacity = reader.ReadInt32();
        levelSaveData.InactiveTriles = new List<TrileEmplacement>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.InactiveTriles.Add(reader.ReadTrileEmplacement());
        }

        capacity = reader.ReadInt32();
        levelSaveData.InactiveArtObjects = new List<int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.InactiveArtObjects.Add(reader.ReadInt32());
        }

        capacity = reader.ReadInt32();
        levelSaveData.InactiveEvents = new List<int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.InactiveEvents.Add(reader.ReadInt32());
        }

        capacity = reader.ReadInt32();
        levelSaveData.InactiveGroups = new List<int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.InactiveGroups.Add(reader.ReadInt32());
        }

        capacity = reader.ReadInt32();
        levelSaveData.InactiveVolumes = new List<int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.InactiveVolumes.Add(reader.ReadInt32());
        }

        capacity = reader.ReadInt32();
        levelSaveData.InactiveNPCs = new List<int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.InactiveNPCs.Add(reader.ReadInt32());
        }

        capacity = reader.ReadInt32();
        levelSaveData.PivotRotations = new Dictionary<int, int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            levelSaveData.PivotRotations.Add(reader.ReadInt32(), reader.ReadInt32());
        }

        levelSaveData.LastStableLiquidHeight = reader.ReadNullableSingle();
        levelSaveData.ScriptingState = reader.ReadNullableString();
        levelSaveData.FirstVisit = reader.ReadBoolean();
        levelSaveData.FilledConditions = ReadWinConditions(reader);
        return levelSaveData;
    }
    
    private static WinConditions ReadWinConditions(BinaryReader reader)
    {
        var winConditions = new WinConditions
        {
            LockedDoorCount = reader.ReadInt32(),
            UnlockedDoorCount = reader.ReadInt32(),
            ChestCount = reader.ReadInt32(),
            CubeShardCount = reader.ReadInt32(),
            OtherCollectibleCount = reader.ReadInt32(),
            SplitUpCount = reader.ReadInt32()
        };

        var capacity = reader.ReadInt32();
        winConditions.ScriptIds = new List<int>(capacity);
        for (var i = 0; i < capacity; i++)
        {
            winConditions.ScriptIds.Add(reader.ReadInt32());
        }

        winConditions.SecretCount = reader.ReadInt32();
        return winConditions;
    }
    
    internal static void Write(BinaryWriter write, LevelSaveData levelSaveData)
    {
        write.Write(levelSaveData.DestroyedTriles.Count);
        foreach (var destroyedTrile in levelSaveData.DestroyedTriles)
        {
            write.Write(destroyedTrile);
        }

        write.Write(levelSaveData.InactiveTriles.Count);
        foreach (var inactiveTrile in levelSaveData.InactiveTriles)
        {
            write.Write(inactiveTrile);
        }

        write.Write(levelSaveData.InactiveArtObjects.Count);
        foreach (var inactiveArtObject in levelSaveData.InactiveArtObjects)
        {
            write.Write(inactiveArtObject);
        }

        write.Write(levelSaveData.InactiveEvents.Count);
        foreach (var inactiveEvent in levelSaveData.InactiveEvents)
        {
            write.Write(inactiveEvent);
        }

        write.Write(levelSaveData.InactiveGroups.Count);
        foreach (var inactiveGroup in levelSaveData.InactiveGroups)
        {
            write.Write(inactiveGroup);
        }

        write.Write(levelSaveData.InactiveVolumes.Count);
        foreach (var inactiveVolume in levelSaveData.InactiveVolumes)
        {
            write.Write(inactiveVolume);
        }

        write.Write(levelSaveData.InactiveNPCs.Count);
        foreach (var inactiveNpc in levelSaveData.InactiveNPCs)
        {
            write.Write(inactiveNpc);
        }

        write.Write(levelSaveData.PivotRotations.Count);
        foreach (var pivotRotation in levelSaveData.PivotRotations)
        {
            write.Write(pivotRotation.Key);
            write.Write(pivotRotation.Value);
        }

        write.WriteNullable(levelSaveData.LastStableLiquidHeight);
        write.WriteNullable(levelSaveData.ScriptingState);
        write.Write(levelSaveData.FirstVisit);
        Write(write, levelSaveData.FilledConditions);
    }

    private static void Write(BinaryWriter writer, WinConditions wc)
    {
        writer.Write(wc.LockedDoorCount);
        writer.Write(wc.UnlockedDoorCount);
        writer.Write(wc.ChestCount);
        writer.Write(wc.CubeShardCount);
        writer.Write(wc.OtherCollectibleCount);
        writer.Write(wc.SplitUpCount);
        writer.Write(wc.ScriptIds.Count);
        foreach (var scriptId in wc.ScriptIds)
        {
            writer.Write(scriptId);
        }

        writer.Write(wc.SecretCount);
    }
}