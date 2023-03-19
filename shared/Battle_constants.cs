using System;
using System.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.BulletState;
using static shared.CharacterState;

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

        public static HashSet<CharacterState> inAirSet = new HashSet<CharacterState>() {
            InairIdle1NoJump,
            InairIdle1ByJump,
            InairAtk1,
            InairAtked1,
            BlownUp1,
            Onwall,
            Dashing // Yes dashing is an inair state even if you dashed on the ground :)
        };

        public static HashSet<CharacterState> noOpSet = new HashSet<CharacterState>() {
            Atked1,
            InairAtked1,
            BlownUp1,
            LayDown1,
			// [WARNING] During the invinsible frames of GET_UP1, the player is allowed to take any action
			Dying
        };

        public static HashSet<CharacterState> invinsibleSet = new HashSet<CharacterState>() {
            BlownUp1,
            LayDown1,
            GetUp1,
            Dying
        };

        public static HashSet<CharacterState> nonAttackingSet = new HashSet<CharacterState>() {
            Idle1,
            Walking,
            InairIdle1NoJump,
            InairIdle1ByJump,
            Atked1,
            InairAtked1,
            BlownUp1,
            LayDown1,
            GetUp1,
            Dying
        };

        public static ImmutableDictionary<int, Skill> skills = ImmutableDictionary.Create<int, Skill>().AddRange(
                new[]
                {
                    new KeyValuePair<int, Skill>(0, new SkillBuilder(30, 30, 30, SkillTriggerType.RisingEdge, CharacterState.Atk1)
                                                    .AddHit(new BulletConfigBuilder(7, 13, 30, 22, 13, 9, (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 5, (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), false, 1, 9, BulletType.Melee, 0).build())
                                                    .build())
                }        
        );

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

        public static int FindSkillId(int patternId, PlayerDownsync currPlayerDownsync, int speciesId) {
            switch (speciesId) {
                case 0:
                    switch (patternId) {
                        case 1:
                            if (0 == currPlayerDownsync.FramesToRecover) {
                                if (currPlayerDownsync.InAir) {
                                    return 255;
                                } else {
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

    }
}
