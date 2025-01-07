using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        public const int TRIGGER_SPECIES_NSWITCH = 1;
        public const int TRIGGER_SPECIES_PSWITCH = 2;
        public const int TRIGGER_SPECIES_STORYPOINT_TRIVIAL = 3;
        public const int TRIGGER_SPECIES_STORYPOINT_MV = 4;
        public const int TRIGGER_SPECIES_TIMED_WAVE_DOOR_1 = 5;
        public const int TRIGGER_SPECIES_INDI_WAVE_DOOR_1 = 6;
        public const int TRIGGER_SPECIES_SYNC_WAVE_DOOR_1 = 7;
        public const int TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_TRIVIAL = 8;
        public const int TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_MV = 9;
        public const int TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_TRIVIAL = 10;
        public const int TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_MV = 11;
        public const int TRIGGER_SPECIES_TIMED_WAVE_GROUP_TRIGGER_TRIVIAL = 12;
        public const int TRIGGER_SPECIES_TIMED_WAVE_GROUP_TRIGGER_MV = 13;

        public const int TRIGGER_SPECIES_TIMED_WAVE_PICKABLE_DROPPER = 14;

        public const int TRIGGER_SPECIES_VICTORY_TRIGGER_TRIVIAL = 1024;
        public const int TRIGGER_SPECIES_NPC_AWAKER_MV = 1025;
        public const int TRIGGER_SPECIES_BOSS_SAVEPOINT = 1026;
        public const int TRIGGER_SPECIES_BOSS_AWAKER_MV = 1027;

        public static TriggerConfig NSwitch = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_NSWITCH,
            SpeciesName = "NSwitch",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig PSwitch = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_PSWITCH,
            SpeciesName = "PSwitch",
            TriggerType = TriggerType.TtAttack,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig StoryPointTrivial = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_STORYPOINT_TRIVIAL,
            SpeciesName = "StoryPointTrivial",
            TriggerType = TriggerType.TtTrivial,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig StoryPointMv = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_STORYPOINT_MV,
            SpeciesName = "StoryPointMv",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig TimedWaveDoor1 = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_TIMED_WAVE_DOOR_1,
            SpeciesName = "TimedWaveDoor1",
            TriggerType = TriggerType.TtCyclicTimed,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig IndiWaveDoor1 = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_INDI_WAVE_DOOR_1,
            SpeciesName = "IndiWaveDoor1",
            TriggerType = TriggerType.TtIndiWave,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig SyncWaveDoor1 = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_SYNC_WAVE_DOOR_1,
            SpeciesName = "SyncWaveDoor1",
            TriggerType = TriggerType.TtSyncWave,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig IndiWaveGroupTriggerTrivial = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_TRIVIAL,
            SpeciesName = "IndiWaveGroupTriggerTrivial",
            TriggerType = TriggerType.TtTrivial,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig IndiWaveGroupTriggerMv = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_MV,
            SpeciesName = "IndiWaveGroupTriggerMv",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig SyncWaveGroupTriggerTrivial = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_TRIVIAL,
            SpeciesName = "SyncWaveGroupTriggerTrivial",
            TriggerType = TriggerType.TtTrivial,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig SyncWaveGroupTriggerMv = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_MV,
            SpeciesName = "SyncWaveGroupTriggerMv",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig TimedWavePickableDropper = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_TIMED_WAVE_PICKABLE_DROPPER,
            SpeciesName = "TimedWavePickableDropper",
            TriggerType = TriggerType.TtCyclicTimed,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig VictoryTriggerTrivial = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_VICTORY_TRIGGER_TRIVIAL,
            SpeciesName = "VictoryTriggerTrivial",
            TriggerType = TriggerType.TtTrivial,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig NpcAwakerMv = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_NPC_AWAKER_MV,
            SpeciesName = "NpcAwakerMv",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig BossSavepoint = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_BOSS_SAVEPOINT,
            SpeciesName = "BossSavepoint",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig BossAwakerMv = new TriggerConfig {
            SpeciesId = TRIGGER_SPECIES_BOSS_AWAKER_MV,
            SpeciesName = "BossAwakerMv",
            TriggerType = TriggerType.TtMovement,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static ImmutableDictionary<int, TriggerConfig> triggerConfigs = ImmutableDictionary.Create<int, TriggerConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, TriggerConfig>(NSwitch.SpeciesId, NSwitch),
                    new KeyValuePair<int, TriggerConfig>(PSwitch.SpeciesId, PSwitch),
                    new KeyValuePair<int, TriggerConfig>(StoryPointTrivial.SpeciesId, StoryPointTrivial),
                    new KeyValuePair<int, TriggerConfig>(StoryPointMv.SpeciesId, StoryPointMv),
                    new KeyValuePair<int, TriggerConfig>(TimedWaveDoor1.SpeciesId, TimedWaveDoor1),
                    new KeyValuePair<int, TriggerConfig>(IndiWaveDoor1.SpeciesId, IndiWaveDoor1),
                    new KeyValuePair<int, TriggerConfig>(SyncWaveDoor1.SpeciesId, SyncWaveDoor1),
                    new KeyValuePair<int, TriggerConfig>(TimedWavePickableDropper.SpeciesId, TimedWavePickableDropper),
                    new KeyValuePair<int, TriggerConfig>(IndiWaveGroupTriggerTrivial.SpeciesId, IndiWaveGroupTriggerTrivial),
                    new KeyValuePair<int, TriggerConfig>(IndiWaveGroupTriggerMv.SpeciesId, IndiWaveGroupTriggerMv),
                    new KeyValuePair<int, TriggerConfig>(SyncWaveGroupTriggerTrivial.SpeciesId, SyncWaveGroupTriggerTrivial),
                    new KeyValuePair<int, TriggerConfig>(SyncWaveGroupTriggerMv.SpeciesId, SyncWaveGroupTriggerMv),
                    new KeyValuePair<int, TriggerConfig>(VictoryTriggerTrivial.SpeciesId, VictoryTriggerTrivial),
                    new KeyValuePair<int, TriggerConfig>(NpcAwakerMv.SpeciesId, NpcAwakerMv),
                    new KeyValuePair<int, TriggerConfig>(BossSavepoint.SpeciesId, BossSavepoint),
                    new KeyValuePair<int, TriggerConfig>(BossAwakerMv.SpeciesId, BossAwakerMv),
                }
        );
    }
}
