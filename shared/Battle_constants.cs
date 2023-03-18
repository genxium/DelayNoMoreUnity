using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        public static float MAX_FLOAT32 = float.MaxValue;
        public static int MAX_INT = 999999999;
        public static int COLLISION_PLAYER_INDEX_PREFIX = (1 << 17);
        public static int COLLISION_BARRIER_INDEX_PREFIX = (1 << 16);
        public static int COLLISION_BULLET_INDEX_PREFIX = (1 << 15);
        public static int PATTERN_ID_UNABLE_TO_OP = -2;
        public static int PATTERN_ID_NO_OP = -1;

        public static float COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO = 10.0f;
        public static float VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO = 1.0f / COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO;

        public static int GRAVITY_X = 0;
        public static int GRAVITY_Y = -(int)(0.5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO); // makes all "playerCollider.Y" a multiple of 0.5 in all cases
        public static int INPUT_DELAY_FRAMES = 4; // in the count of render frames

        /*
		   [WARNING]
		   Experimentally having an input rate > 15 (e.g., 60 >> 2) doesn't improve multiplayer smoothness, in fact higher input rate often results in higher packet loss (both TCP and UDP) thus higher wrong prediction rate!
		*/
        public static int INPUT_SCALE_FRAMES = 2; // inputDelayedAndScaledFrameId = ((originalFrameId - InputDelayFrames) >> InputScaleFrames)

        public static int SP_ATK_LOOKUP_FRAMES = 5;
        public static float SNAP_INTO_PLATFORM_OVERLAP = 0.1f;
        public static float SNAP_INTO_PLATFORM_THRESHOLD = 0.5f;
        public static float VERTICAL_PLATFORM_THRESHOLD = 0.9f;
        public static int MAGIC_FRAMES_TO_BE_ONWALL = 12;


        public static int DYING_FRAMES_TO_RECOVER = 60; // MUST BE SAME FOR EVERY CHARACTER FOR FAIRNESS!

        public static int NO_SKILL = -1;
        public static int NO_SKILL_HIT = -1;

        public static int NO_LOCK_VEL = -1;

        // Used in preallocated RoomDownsyncFrame to check termination
        public static int TERMINATING_BULLET_LOCAL_ID = (-1);
        public static int TERMINATING_PLAYER_ID = (-1);
        public static int TERMINATING_RENDER_FRAME_ID = (-1);
        public static int TERMINATING_INPUT_FRAME_ID = (-1);

        // These directions are chosen such that when speed is changed to "(speedX+delta, speedY+delta)" for any of them, the direction is unchanged.
        public static int[,] DIRECTION_DECODER = new int[,] {
            {0, 0},
            {0, +2},
            {0, -2},
            {+2, 0},
            {-2, 0},
            {+1, +1},
            {-1, -1},
            {+1, -1},
            {-1, +1},
        };

        public static int BULLET_STARTUP = 0;
        public static int BULLET_ACTIVE = 1;
        public static int BULLET_EXPLODING = 2;

        public const int ATK_CHARACTER_STATE_IDLE1 = (0);

        public const int ATK_CHARACTER_STATE_WALKING = (1);

        public const int ATK_CHARACTER_STATE_ATK1 = (2);
        public const int ATK_CHARACTER_STATE_ATKED1 = (3);

        public const int ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP = (4);

        public const int ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP = (5);

        public const int ATK_CHARACTER_STATE_INAIR_ATK1 = (6);
        public const int ATK_CHARACTER_STATE_INAIR_ATKED1 = (7);
        public const int ATK_CHARACTER_STATE_BLOWN_UP1 = (8);
        public const int ATK_CHARACTER_STATE_LAY_DOWN1 = (9);
        public const int ATK_CHARACTER_STATE_GET_UP1 = (10);
        public const int ATK_CHARACTER_STATE_ATK2 = (11);

        public const int ATK_CHARACTER_STATE_ATK3 = (12);

        public const int ATK_CHARACTER_STATE_ATK4 = (13);

        public const int ATK_CHARACTER_STATE_ATK5 = (14);

        public const int ATK_CHARACTER_STATE_DASHING = (15);

        public const int ATK_CHARACTER_STATE_ONWALL = (16);

        public const int ATK_CHARACTER_STATE_TURNAROUND = (17);

        public const int ATK_CHARACTER_STATE_DYING = (18);

        public static HashSet<int> inAirSet = new HashSet<int>() {
            ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP,
            ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP,
            ATK_CHARACTER_STATE_INAIR_ATK1,
            ATK_CHARACTER_STATE_INAIR_ATKED1,
            ATK_CHARACTER_STATE_BLOWN_UP1,
            ATK_CHARACTER_STATE_ONWALL,
            ATK_CHARACTER_STATE_DASHING // Yes dashing is an inair state even if you dashed on the ground :)
        };

        public static HashSet<int> noOpSet = new HashSet<int>() {
            ATK_CHARACTER_STATE_ATKED1,
            ATK_CHARACTER_STATE_INAIR_ATKED1,
            ATK_CHARACTER_STATE_BLOWN_UP1,
            ATK_CHARACTER_STATE_LAY_DOWN1,
			// [WARNING] During the invinsible frames of GET_UP1, the player is allowed to take any action
			ATK_CHARACTER_STATE_DYING
        };

        public static HashSet<int> invinsibleSet = new HashSet<int>() {
            ATK_CHARACTER_STATE_BLOWN_UP1,
            ATK_CHARACTER_STATE_LAY_DOWN1,
            ATK_CHARACTER_STATE_GET_UP1,
            ATK_CHARACTER_STATE_DYING
        };

        public static HashSet<int> nonAttackingSet = new HashSet<int>() {
            ATK_CHARACTER_STATE_IDLE1,
            ATK_CHARACTER_STATE_WALKING,
            ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP,
            ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP,
            ATK_CHARACTER_STATE_ATKED1,
            ATK_CHARACTER_STATE_INAIR_ATKED1,
            ATK_CHARACTER_STATE_BLOWN_UP1,
            ATK_CHARACTER_STATE_LAY_DOWN1,
            ATK_CHARACTER_STATE_GET_UP1,
            ATK_CHARACTER_STATE_DYING
        };

        public static int FindSkillId(int patternId, PlayerDownsync currPlayerDownsync, int speciesId) {
            switch (speciesId) {
                case 0:
                    switch (patternId) {
                        case 1:
                            if (0 == currPlayerDownsync.FramesToRecover) {
                                if (currPlayerDownsync.InAir) {
                                    return 255;
                                }
                                else {
                                    return 1;

                                }
                            }
                            break;
                        case 3:
                            if (0 == currPlayerDownsync.FramesToRecover && !currPlayerDownsync.InAir) {
                                return 15;
                            }
                            break;
                        case 5:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            if (!currPlayerDownsync.InAir) {
                                return 12;
                            }
                            break;
                    }
                    // By default no skill can be fired
                    return NO_SKILL;
            }
            return NO_SKILL;
        }

        public static ImmutableDictionary<int, CharacterConfig> characters = ImmutableDictionary.Create<int, CharacterConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, CharacterConfig>(0, new CharacterConfig(
                    0, "MonkGirl",
                    11, 1,
                    16, 16, 10, 27,
                    (int)(2.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    9,
                    true, true,
                    8, (int)(2.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    (int)(7 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-1 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO)))
                }
            );
    }
}
