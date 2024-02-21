using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        public static TriggerConfig NSwitch = new TriggerConfig {
            SpeciesId = 1,
            SpeciesName = "NSwitch",
            TriggerMask = TRIGGER_MASK_BY_MOVEMENT,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig PSwitch = new TriggerConfig {
            SpeciesId = 2,
            SpeciesName = "PSwitch",
            TriggerMask = TRIGGER_MASK_BY_ATK,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig TimedDoor1 = new TriggerConfig {
            SpeciesId = 3,
            SpeciesName = "TimedDoor1",
            TriggerMask = TRIGGER_MASK_BY_CYCLIC_TIMER,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig WaveTimedDoor1 = new TriggerConfig {
            SpeciesId = 4,
            SpeciesName = "WaveTimedDoor1",
            TriggerMask = TRIGGER_MASK_BY_SUBSCRIPTION,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static TriggerConfig StoryPoint = new TriggerConfig {
            SpeciesId = 5,
            SpeciesName = "StoryPoint",
            TriggerMask = TRIGGER_MASK_BY_MOVEMENT,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig WaveInducer = new TriggerConfig {
            SpeciesId = 6,
            SpeciesName = "WaveInducer",
            TriggerMask = TRIGGER_MASK_BY_MOVEMENT,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static ImmutableDictionary<int, TriggerConfig> triggerConfigs = ImmutableDictionary.Create<int, TriggerConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, TriggerConfig>(NSwitch.SpeciesId, NSwitch),
                    new KeyValuePair<int, TriggerConfig>(PSwitch.SpeciesId, PSwitch),
                    new KeyValuePair<int, TriggerConfig>(TimedDoor1.SpeciesId, TimedDoor1),
                    new KeyValuePair<int, TriggerConfig>(WaveTimedDoor1.SpeciesId, WaveTimedDoor1),
                    new KeyValuePair<int, TriggerConfig>(StoryPoint.SpeciesId, StoryPoint),
                    new KeyValuePair<int, TriggerConfig>(WaveInducer.SpeciesId, WaveInducer),
                }
        );
    }
}
