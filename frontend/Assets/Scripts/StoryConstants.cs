using System.Collections.Generic;
using System.Collections.Immutable;
using shared;

namespace Story {
    public class StoryConstants {
        public const int SAVE_SLOT_ID_NONE = 0;

        public const int REGION_NONE = 0;
        public const int REGION_AUTUMN = 1; // The story mode starts from Autumn
        public const int REGION_WINTER = 2;
        public const int REGION_SPRING = 3;
        public const int REGION_SUMMER = 4;

        public const int REGION_FINISHED_DEP_ID_OFFSET = 4096;

        public const int REGION_LAVA = 5;
        public const int REGION_DESERT = 6;
        public const int REGION_ICEBERG = 7;
        public const int REGION_HEAVENLAND = 8;

        public const int LEVEL_CNT_CAP_PER_REGION = 128;

        public const int LEVEL_NONE = 0;
        public const int LEVEL_DELICATE_FOREST = 1;
        public const int LEVEL_SMALL_FOREST = 2;
        public const int LEVEL_ZIGZAGZOO = 3;
        public const int LEVEL_CASCADE_FARM = 4;
        public const int LEVEL_DESCENDING_PALACE = 5;
        public const int LEVEL_ARROW_PALACE = 6;

        public const int LEVEL_VILLAGE_AMORA_GATE = 7;
        public const int LEVEL_VILLAGE_AMORA_INSIDE = 8;
        public const int LEVEL_VILLAGE_AMORA_RESISTANCE = 9;
        public const int LEVEL_VILLAGE_AMORA_WEREWOLF = 10;

        public const int STORY_POINT_NONE = 0;

        public static ImmutableDictionary<int, string> REGION_NAMES = ImmutableDictionary.Create<int, string>().AddRange(new[]
            {
            new KeyValuePair<int, string>(REGION_AUTUMN, "Autumn"),
            new KeyValuePair<int, string>(REGION_WINTER, "Winter"),
            new KeyValuePair<int, string>(REGION_SPRING, "Spring"),
            new KeyValuePair<int, string>(REGION_SUMMER, "Summer"),
            new KeyValuePair<int, string>(REGION_LAVA, "Lava"),
            new KeyValuePair<int, string>(REGION_DESERT, "Desert"),
            new KeyValuePair<int, string>(REGION_ICEBERG, "Iceberg"),
            new KeyValuePair<int, string>(REGION_HEAVENLAND, "HeavenLand"),
        });

        public static ImmutableDictionary<int, string> LEVEL_NAMES = ImmutableDictionary.Create<int, string>().AddRange(new[]
            {
            new KeyValuePair<int, string>(LEVEL_DELICATE_FOREST, "DelicateForest"),
            //new KeyValuePair<int, string>(LEVEL_SMALL_FOREST, "SmallForest"),
            new KeyValuePair<int, string>(LEVEL_SMALL_FOREST, "FoxBay"),
            //new KeyValuePair<int, string>(LEVEL_SMALL_FOREST, "CaveVersus"),
            new KeyValuePair<int, string>(LEVEL_ZIGZAGZOO, "ZigZagZoo"),
            new KeyValuePair<int, string>(LEVEL_CASCADE_FARM, "CascadeFarm"),
            //new KeyValuePair<int, string>(LEVEL_CASCADE_FARM, "FlatVersus"),
            new KeyValuePair<int, string>(LEVEL_DESCENDING_PALACE, "DescendingPalace"),
            new KeyValuePair<int, string>(LEVEL_ARROW_PALACE, "ArrowPalace")
        });

        /*---------------------------------STORY, LEVEL relations---------------------------------------------------------------------------------------------------*/
        public static StoryPoint STORY_POINT_1 = new StoryPoint(
            new StoryPointStep[] {
                new StoryPointStep(
                    new StoryPointDialogLine[] {
                        new StoryPointDialogLine {
                            NarratorJoinIndex = 1,
                            NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                            Content = "No way, I didn't expect soldiers here...",
                            DownOrNot = true
                        }
                    }
                ),
                new StoryPointStep(
                    new StoryPointDialogLine[] {
                        new StoryPointDialogLine {
                            NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                            NarratorSpeciesId = Battle.SPECIES_LIGHTGUARD_RED,
                            Content = "Stay there! Show your pass or turn back!",
                            DownOrNot = false
                        }
                    }
                ),
                new StoryPointStep(
                    new StoryPointDialogLine[] {
                        new StoryPointDialogLine {
                            NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                            NarratorSpeciesId = Battle.SPECIES_EKRAIL,
                            Content = "Be careful girl!",
                            DownOrNot = false
                        },
                        new StoryPointDialogLine {
                            NarratorJoinIndex = 1,
                            NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                            Content = "Sure",
                            DownOrNot = true
                        }
                    }
                )
            }    
        );

        public static StoryPoint STORY_POINT_2 = new StoryPoint {
            Autoplay = true
        } 
        .AddStep(new StoryPointStep {
            AutoplayAliveRdfCount = 2 * Battle.BATTLE_DYNAMICS_FPS
            }
            .AddLine(
                    new StoryPointDialogLine {
                        NarratorJoinIndex = 1,
                        NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                        Content = "Villagers, would they know where the animals are taken?",
                        DownOrNot = false,
                    }
            )
        )
        .AddStep(new StoryPointStep {
            AutoplayAliveRdfCount = (int)(2.5f * Battle.BATTLE_DYNAMICS_FPS)
            }
            .AddLine(
                    new StoryPointDialogLine {
                        NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                        NarratorSpeciesId = Battle.SPECIES_EKRAIL,
                        Content = "Same thought here",
                        DownOrNot = false
                    }
            )
            .AddLine(
                    new StoryPointDialogLine {
                        NarratorJoinIndex = 1,
                        NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                        Content = "That's REALLY helpful hmm?",
                        DownOrNot = true
                    }
            )
        );

        public static StoryPoint STORY_POINT_3 = new StoryPoint(
            new StoryPointStep[] {
                new StoryPointStep(
                    new StoryPointDialogLine[] {
                        new StoryPointDialogLine {
                            NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                            NarratorSpeciesId = Battle.SPECIES_VIL_FEMALE1,
                            Content = "Animals? Sure this village is full of animals",
                            DownOrNot = false
                        }
                    }
                ),
                new StoryPointStep(
                    new StoryPointDialogLine[] {
                        new StoryPointDialogLine {
                            NarratorJoinIndex = 1,
                            NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                            Content = "Did someone recently buy many of them?",
                            DownOrNot = true
                        }
                    }
                ),
                new StoryPointStep(
                    new StoryPointDialogLine[] {
                        new StoryPointDialogLine {
                            NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                            NarratorSpeciesId = Battle.SPECIES_VIL_FEMALE1,
                            Content = "No but the soldiers took lots of our boars!",
                            DownOrNot = false
                        },
                        new StoryPointDialogLine {
                            NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                            NarratorSpeciesId = Battle.SPECIES_VIL_FEMALE2,
                            Content = "Without leaving a cent, they're like gangs...",
                            DownOrNot = true
                        }
                    }
                )
            }    
        );

        public static StoryPoint STORY_POINT_4 = new StoryPoint {
            Autoplay = true
        } 
        .AddStep(new StoryPointStep {
            AutoplayAliveRdfCount = 2 * Battle.BATTLE_DYNAMICS_FPS
            }
            .AddLine(
                    new StoryPointDialogLine {
                        NarratorJoinIndex = 1,
                        NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                        Content = "Is the cave... inviting me?",
                        DownOrNot = false,
                    }
            )
            .AddLine(
                    new StoryPointDialogLine {
                        NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                        NarratorSpeciesId = Battle.SPECIES_EKRAIL,
                        Content = "Yet certainly not a friendly one",
                        DownOrNot = true
                    }
            )
        );

        public static LevelStory STORY_DELICATE_FOREST = new LevelStory()
            .UpdatePoint(1, STORY_POINT_1)
            .UpdatePoint(2, STORY_POINT_2)
            .UpdatePoint(3, STORY_POINT_3)
            .UpdatePoint(4, STORY_POINT_4)
            ;

        public static LevelStory STORY_SMALL_FOREST = new LevelStory();

        public static ImmutableDictionary<int, LevelStory> STORIES_OF_LEVELS = ImmutableDictionary.Create<int, LevelStory>().AddRange(new[]
            {
            new KeyValuePair<int, LevelStory>(LEVEL_DELICATE_FOREST, STORY_DELICATE_FOREST), 
        });

        /*---------------------------------REGION, LEVEL, and DEPENDENCIES---------------------------------------------------------------------------------------------------*/
        public static ImmutableDictionary<int, ImmutableArray<int>> LEVEL_UNDER_REGION = ImmutableDictionary.Create<int, ImmutableArray<int>>().AddRange(new[]
            {
            new KeyValuePair<int, ImmutableArray<int>>(REGION_AUTUMN, ImmutableArray.Create(LEVEL_DELICATE_FOREST, LEVEL_SMALL_FOREST, LEVEL_ZIGZAGZOO, LEVEL_CASCADE_FARM, LEVEL_DESCENDING_PALACE, LEVEL_ARROW_PALACE)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_WINTER, ImmutableArray.Create(LEVEL_VILLAGE_AMORA_GATE, LEVEL_VILLAGE_AMORA_INSIDE, LEVEL_VILLAGE_AMORA_RESISTANCE, LEVEL_VILLAGE_AMORA_WEREWOLF)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_SPRING, ImmutableArray.Create<int>()),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_SUMMER, ImmutableArray.Create<int>()),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_LAVA, ImmutableArray.Create<int>()),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_DESERT, ImmutableArray.Create <int>()),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_ICEBERG, ImmutableArray.Create <int>()),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_HEAVENLAND, ImmutableArray.Create<int>()),
        });

        /*---------------------------------DEPENDENCIES---------------------------------------------------------------------------------------------------*/
        public static ImmutableDictionary<int, ImmutableArray<int>> LEVEL_UNLOCK_INIT_DEPS = ImmutableDictionary.Create<int, ImmutableArray<int>>().AddRange(new[]
            {
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_DELICATE_FOREST, ImmutableArray.Create<int>()),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_SMALL_FOREST, ImmutableArray.Create<int>(LEVEL_DELICATE_FOREST)),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_ZIGZAGZOO, ImmutableArray.Create<int>(LEVEL_SMALL_FOREST)),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_CASCADE_FARM, ImmutableArray.Create(LEVEL_ZIGZAGZOO)),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_DESCENDING_PALACE, ImmutableArray.Create(LEVEL_CASCADE_FARM)),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_ARROW_PALACE, ImmutableArray.Create(LEVEL_DESCENDING_PALACE)),

            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_VILLAGE_AMORA_INSIDE, ImmutableArray.Create(LEVEL_VILLAGE_AMORA_GATE)),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_VILLAGE_AMORA_RESISTANCE, ImmutableArray.Create(LEVEL_VILLAGE_AMORA_INSIDE)),
            new KeyValuePair<int, ImmutableArray<int>>(LEVEL_VILLAGE_AMORA_WEREWOLF, ImmutableArray.Create(LEVEL_VILLAGE_AMORA_RESISTANCE)),
        });

        public static ImmutableDictionary<int, ImmutableArray<int>> REGION_UNLOCK_INIT_DEPS = ImmutableDictionary.Create<int, ImmutableArray<int>>().AddRange(new[] {       
            new KeyValuePair<int, ImmutableArray<int>>(REGION_WINTER, ImmutableArray.Create(REGION_AUTUMN+REGION_FINISHED_DEP_ID_OFFSET)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_SPRING, ImmutableArray.Create(REGION_WINTER+REGION_FINISHED_DEP_ID_OFFSET)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_SUMMER, ImmutableArray.Create(REGION_SPRING+REGION_FINISHED_DEP_ID_OFFSET)),

            new KeyValuePair<int, ImmutableArray<int>>(REGION_LAVA, ImmutableArray.Create(REGION_SUMMER+REGION_FINISHED_DEP_ID_OFFSET)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_DESERT, ImmutableArray.Create(REGION_LAVA+REGION_FINISHED_DEP_ID_OFFSET)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_ICEBERG, ImmutableArray.Create(REGION_DESERT+REGION_FINISHED_DEP_ID_OFFSET)),
            new KeyValuePair<int, ImmutableArray<int>>(REGION_HEAVENLAND, ImmutableArray.Create(REGION_ICEBERG+REGION_FINISHED_DEP_ID_OFFSET)),
        });
    }
}
