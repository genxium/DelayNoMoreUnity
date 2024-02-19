using System.Collections.Generic;
using System.Collections.Immutable;
using shared;

namespace Story {
    
    using StoryPointStep = ImmutableArray<StoryPointDialogLine>;
    using StoryPoint = ImmutableArray<ImmutableArray<StoryPointDialogLine>>;
    using Story = ImmutableDictionary<int, ImmutableArray<ImmutableArray<StoryPointDialogLine>>>;
    public class StoryConstants {
        public const int LEVEL_NONE = -1;

        public const int LEVEL_SMALL_FOREST = 0;
        public const int LEVEL_FOREST = 1;

        public static StoryPointStep SMALL_FOREST_STORY_POINT_1_STEP_1 = ImmutableArray.Create<StoryPointDialogLine>().AddRange(
                new StoryPointDialogLine {
                    NarratorJoinIndex = 1,
                    NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                    Content = "No way, I didn't expect a Goblin here...",
                    DownOrNot = true
                }
            );

        public static StoryPointStep SMALL_FOREST_STORY_POINT_1_STEP_2 = ImmutableArray.Create<StoryPointDialogLine>().AddRange(
                new StoryPointDialogLine {
                    NarratorJoinIndex = Battle.MAGIC_JOIN_INDEX_INVALID,
                    NarratorSpeciesId = Battle.SPECIES_GOBLIN,
                    Content = "Gwaaaaaaaaaaaaa!!!",
                    DownOrNot = false
                },
                new StoryPointDialogLine {
                    NarratorJoinIndex = 1,
                    NarratorSpeciesId = Battle.SPECIES_NONE_CH,
                    Content = "Gross sound as always >_<",
                    DownOrNot = true
                }
            );

        public static StoryPoint SMALL_FOREST_STORY_POINT_1 = ImmutableArray.Create<StoryPointStep>().AddRange(
            SMALL_FOREST_STORY_POINT_1_STEP_1,
            SMALL_FOREST_STORY_POINT_1_STEP_2
            );

        public static Story SMALL_FOREST_STORY = ImmutableDictionary.Create<int, StoryPoint>().AddRange(new[]
            {
            new KeyValuePair<int, StoryPoint>(1, SMALL_FOREST_STORY_POINT_1)
        });

        public static ImmutableDictionary<int, Story> STORIES_OF_LEVELS = ImmutableDictionary.Create<int, Story>().AddRange(new[]
            {
            new KeyValuePair<int, Story>(LEVEL_SMALL_FOREST, SMALL_FOREST_STORY)
        });
    }

}
