using shared;

namespace Story {
    public class StoryUtil {
        public static LevelStory STORY_NONE = new LevelStory {};

        public static LevelStory getStory(int levelId) {
            if (!StoryConstants.STORIES_OF_LEVELS.ContainsKey(levelId)) return STORY_NONE;
            return StoryConstants.STORIES_OF_LEVELS[levelId];
        }

        public static StoryPoint getStoryPoint(LevelStory levelStory, int storyPointId) {
            return levelStory.Points[storyPointId];
        }
    }
}
