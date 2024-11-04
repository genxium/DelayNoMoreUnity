using UnityEngine;
using shared;
using Story;
using System.IO;
using Google.Protobuf;
using System;
using Google.Protobuf.Collections;

public class PlayerStoryProgressManager {

    public const int FIXED_SAVE_SLOT_CNT = 4;

    private static PlayerStoryProgressManager _instance;

    public static PlayerStoryProgressManager Instance {
        get {
            if (null == _instance) _instance = new PlayerStoryProgressManager();
            return _instance;
        }
    }

    private int currentSaveSlotId = StoryConstants.SAVE_SLOT_ID_NONE;
    private PlayerStoryProgress currentProgress;

    private PlayerStoryProgressManager() {
        initialize();
    }

    private string dirPath = Application.persistentDataPath; 

    private PlayerLevelProgress newLevelProgress(int levelId) {
        var ret = new PlayerLevelProgress();
        if (StoryConstants.LEVEL_UNLOCK_INIT_DEPS.ContainsKey(levelId)) {
            foreach (var depId in StoryConstants.LEVEL_UNLOCK_INIT_DEPS[levelId]) {
                ret.RemainingDependencies.Add(depId, true);
            }
        }
        return ret;
    }

    private PlayerRegionProgress newRegionProgress(int regionId) {
        var ret = new PlayerRegionProgress();
        if (StoryConstants.REGION_UNLOCK_INIT_DEPS.ContainsKey(regionId)) {
            foreach (var depId in StoryConstants.REGION_UNLOCK_INIT_DEPS[regionId]) {
                ret.RemainingDependencies.Add(depId, true);
            }
        }
        return ret;
    }

    private void initialize() {
        currentProgress = new PlayerStoryProgress {
            CursorRegionId = StoryConstants.REGION_AUTUMN,
            CursorLevelId = StoryConstants.LEVEL_SMALL_FOREST,
            View = PlayerStoryModeSelectView.Level
        };

        // Levels
        foreach (var (regionId, regionLevelIds) in StoryConstants.LEVEL_UNDER_REGION) {
            var regionProgress = newRegionProgress(regionId);
            currentProgress.RegionProgressDict.Add(regionId, regionProgress);   
            foreach (var levelId in regionLevelIds) {
                var levelProgress = newLevelProgress(levelId);
                currentProgress.LevelProgressDict.Add(levelId, levelProgress);
            }
        }
    }

    private string saveSlotFilename(int slotId) {
        /*
        [WARNING]

        THe use of fixed slotId in [1, 3] prohibits flexible slot delete and compact rearrangement! However, to enable flexible compact arrangement I'd need a generator function based on yet another "id keeper file". 
        */
        return "saveslot_" + slotId + ".styprg";
    }

    // TODO: Consider implementing and using the async versions of "Save/Load"?
    public void SaveIntoSlot(int slotId) {
        if (StoryConstants.SAVE_SLOT_ID_NONE == currentSaveSlotId) {
            currentSaveSlotId = 1;
        }
        // [WARNING] Not necessarily "currentSlotId", the player can choose "Save As..." option.
        var filename = saveSlotFilename(slotId);
        using (StreamWriter outputFile = new StreamWriter(Path.Combine(dirPath, filename))) {
            currentProgress.SavedAtGmtMillis = (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
            outputFile.Write(currentProgress.ToByteString().ToBase64());
        }
    }

    public void LoadFromSlot(int slotId) {
        var filename = saveSlotFilename(slotId);
        var filepath = Path.Combine(dirPath, filename);
        if (File.Exists(filepath)) {
            using (StreamReader sr = new StreamReader(filepath)) {
                var buffer = new Char[(int)sr.BaseStream.Length];
                sr.Read(buffer, 0, (int)sr.BaseStream.Length);

                currentSaveSlotId = slotId;
                currentProgress = PlayerStoryProgress.Parser.ParseFrom(ByteString.FromBase64(new string(buffer)));
                _sanitizeDeps();
            }
        } else {
            // Use the just initialized "currentProgress" but into a chosen slot
            initialize();
            currentSaveSlotId = slotId;
        }
    }

    private void _sanitizeDeps() {
        foreach (var (regionId, regionLevelIds) in StoryConstants.LEVEL_UNDER_REGION) {
            // Region
            var referenceRegionProgress = newRegionProgress(regionId);
            if (currentProgress.RegionProgressDict.ContainsKey(regionId)) {
                var regionProgress = currentProgress.RegionProgressDict[regionId];   
                // Remove obsolete deps
                foreach (var depId in regionProgress.RemainingDependencies.Keys) {
                    if (!referenceRegionProgress.RemainingDependencies.ContainsKey(depId)) {
                        regionProgress.RemainingDependencies.Remove(depId);
                        if (regionProgress.RemovedDependencies.ContainsKey(depId)) {    
                            regionProgress.RemovedDependencies.Remove(depId);
                        }
                    } 
                }
                // Add new and unmet deps
                foreach (var depId in referenceRegionProgress.RemainingDependencies.Keys) {
                    if (!regionProgress.RemainingDependencies.ContainsKey(depId) && !regionProgress.RemovedDependencies.ContainsKey(depId)) {
                        regionProgress.RemainingDependencies.Add(depId, true);
                    }
                }
            } else {
                currentProgress.RegionProgressDict.Add(regionId, referenceRegionProgress);
            }
            
            // Level
            foreach (var levelId in regionLevelIds) {
                var referenceLevelProgress = newLevelProgress(levelId);
                if (currentProgress.LevelProgressDict.ContainsKey(levelId)) {
                    var levelProgress = currentProgress.LevelProgressDict[levelId];   
                    // Remove obsolete deps
                    foreach (var depId in levelProgress.RemainingDependencies.Keys) {
                        if (!referenceLevelProgress.RemainingDependencies.ContainsKey(depId)) {
                            levelProgress.RemainingDependencies.Remove(depId);
                            if (levelProgress.RemovedDependencies.ContainsKey(depId)) {
                                levelProgress.RemovedDependencies.Remove(depId);
                            }
                        } 
                    }
                    // Add new and unmet deps
                    foreach (var depId in referenceLevelProgress.RemainingDependencies.Keys) {
                        if (!levelProgress.RemainingDependencies.ContainsKey(depId) && !levelProgress.RemovedDependencies.ContainsKey(depId)) {
                            levelProgress.RemainingDependencies.Add(depId, true);
                        }
                    }
                } else {
                    currentProgress.LevelProgressDict.Add(levelId, referenceLevelProgress);
                }
            }
        }
    } 

    public void DeleteSlot(int slotId) {
        var filename = saveSlotFilename(slotId);
        File.Delete(Path.Combine(dirPath, filename)); 
    }

    public void FinishLevel(int levelId, int score, int finishTime, uint characterSpeciesId, bool saveIntoCurrentSlot) {
        Debug.LogFormat("FinishLevel levelId={0}, score={1}, finishTime={2}, characterSpeciesId={3}, saveIntoCurrentSlot={4}", levelId, score, finishTime, characterSpeciesId, saveIntoCurrentSlot);
        currentProgress.CursorLevelId = levelId;
        var levelProgress = currentProgress.LevelProgressDict[levelId];

        if (0 == levelProgress.HighestScore || score > levelProgress.HighestScore) {
            levelProgress.HighestScore = score;
            levelProgress.ShortestFinishTimeAtHighestScore = finishTime;
            levelProgress.CharacterSpeciesIdAtHighestScore = characterSpeciesId;
        }
        
        if (0 == levelProgress.ShortestFinishTime || finishTime < levelProgress.ShortestFinishTime) {
            levelProgress.ShortestFinishTime = finishTime;
            levelProgress.ScoreAtShortestFinishTime = score;
            levelProgress.CharacterSpeciesIdAtShortestFinishTime = characterSpeciesId;
        }

        var publishingDepId = levelId;

        // TODO: Handle visibility change of a new level. The following approach only assumes that all regions and levels are initially visible but locked.
        foreach (var (regionId, regionLevelIds) in StoryConstants.LEVEL_UNDER_REGION) {
            var regionProgress = currentProgress.RegionProgressDict[regionId];
            if (regionProgress.RemainingDependencies.ContainsKey(publishingDepId)) {
                regionProgress.RemainingDependencies.Remove(publishingDepId);
                regionProgress.RemovedDependencies.Add(publishingDepId, true);
                Debug.LogFormat("FinishLevel removed publishingDepId={0} from regionProgress for regionId={1}", publishingDepId, regionId);
            }
            foreach (var otherLevelId in regionLevelIds) {
                if (otherLevelId == levelId) continue;
                var otherLevelProgress = currentProgress.LevelProgressDict[otherLevelId];
                if (otherLevelProgress.RemainingDependencies.ContainsKey(publishingDepId)) {
                    otherLevelProgress.RemainingDependencies.Remove(publishingDepId);
                    otherLevelProgress.RemovedDependencies.Add(publishingDepId, true);
                    Debug.LogFormat("FinishLevel removed publishingDepId={0} from otherLevelProgress for regionId={1}, otherLevelId={2}", publishingDepId, regionId, otherLevelId);
                }
            }
        }

        if (saveIntoCurrentSlot) {
            SaveIntoSlot(currentSaveSlotId);
        }
    }

    public bool HasAnyUsedSlot() {
        int cnt = 0;
        foreach (var file in Directory.EnumerateFiles(dirPath, searchPattern: "saveslot_*.styprg")) {
            cnt++;
        }
        return 0 < cnt;
    }

    public PlayerStoryProgress[] LoadHeadingsFromAllSaveSlots() {
        var headings = new PlayerStoryProgress[FIXED_SAVE_SLOT_CNT]; 
        for (int slotId = 1; slotId < FIXED_SAVE_SLOT_CNT; slotId++) {
            var filename = saveSlotFilename(slotId);
            var filePath = Path.Combine(dirPath, filename);
            if (!File.Exists(filePath)) {
                headings[slotId - 1] = null;
            } else {
                using (StreamReader sr = new StreamReader(filePath)) {
                    var buffer = new Char[(int)sr.BaseStream.Length];
                    sr.Read(buffer, 0, (int)sr.BaseStream.Length);
                    headings[slotId - 1] = PlayerStoryProgress.Parser.ParseFrom(ByteString.FromBase64(new string(buffer)));
                }
            }
        }
        return headings;
    }

    public MapField<int, PlayerRegionProgress> LoadRegions() {
        return currentProgress.RegionProgressDict;
    }

    public RepeatedField<PlayerLevelProgress> LoadLevelsUnderCurrentRegion() {
        var levelProgressList = new RepeatedField<PlayerLevelProgress>();
        var levelIds = StoryConstants.LEVEL_UNDER_REGION[currentProgress.CursorRegionId];
        foreach (var levelId in levelIds) {
            levelProgressList.Add(currentProgress.LevelProgressDict[levelId]);
        }
        return levelProgressList;
    }

    public int GetCurrentRegionId() {
        return currentProgress.CursorRegionId;
    }

    public int GetCurrentLevelId() {
        return currentProgress.CursorLevelId;
    }

    public PlayerStoryModeSelectView GetCurrentView() {
        return currentProgress.View;
    }

    public void SetView(PlayerStoryModeSelectView aView) {
        currentProgress.View = aView;
        SaveIntoSlot(currentSaveSlotId);
    }

    private uint cachedChSpeciedId = Battle.SPECIES_NONE_CH;
    private string cachedLevelName = null;
    public void SetCachedForOfflineMap(uint chSpeciesId, string levelName) {
        cachedChSpeciedId = chSpeciesId;
        cachedLevelName = levelName;
    }

    public void ResetCachedForOfflineMap() {
        cachedChSpeciedId = Battle.SPECIES_NONE_CH;
        cachedLevelName = null;
    }

    public uint GetCachedChSpeciesId() {
        return cachedChSpeciedId;
    }

    public string GetCachedLevelName() {
        return cachedLevelName;
    }
}
