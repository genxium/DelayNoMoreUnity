using shared;

namespace Story {
    public class StoryUtil {
        public static LevelStory getStory(int levelId) {
            return StoryConstants.STORIES_OF_LEVELS[levelId];
        }

        public static StoryPoint getStoryPoint(LevelStory levelStory, int storyPointId) {
            return levelStory.Points[storyPointId];
        }
    }
}
