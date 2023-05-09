using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        // Deliberately NOT using enum for "room states" to make use of "C# CompareAndExchange" 
        public const long ROOM_STATE_IMPOSSIBLE = 0;
        public const long ROOM_STATE_IDLE = 1;
        public const long ROOM_STATE_WAITING = 2;
        public const long ROOM_STATE_PREPARE = 3;
        public const long ROOM_STATE_IN_BATTLE = 4;
        public const long ROOM_STATE_IN_SETTLEMENT = 5;

        // Deliberately NOT using enum for "player battle states" to make use of "C# CompareAndExchange" 
        public const long PLAYER_BATTLE_STATE_IMPOSSIBLE = -2;
        public const long PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK = 0;
        public const long PLAYER_BATTLE_STATE_READDED_PENDING_BATTLE_COLLIDER_ACK = 1;
        public const long PLAYER_BATTLE_STATE_READDED_BATTLE_COLLIDER_ACKED = 2;
        public const long PLAYER_BATTLE_STATE_ACTIVE = 3;
        public const long PLAYER_BATTLE_STATE_DISCONNECTED = 4;
        public const long PLAYER_BATTLE_STATE_LOST = 5;
        public const long PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME = 6;
        public const long PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL = 7;

        public const int UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK = 1;
        public const int UPSYNC_MSG_ACT_PLAYER_CMD = 2;
        public const int UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL = 3;
        public const int UPSYNC_MSG_ACT_HOLEPUNCH_PEER_UDP_ADDR = 4;

        public const int DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO = 1;
        public const int DOWNSYNC_MSG_ACT_INPUT_BATCH = 2;
        public const int DOWNSYNC_MSG_ACT_BATTLE_STOPPED = 3;
        public const int DOWNSYNC_MSG_ACT_FORCED_RESYNC = 4;
        public const int DOWNSYNC_MSG_ACT_PEER_INPUT_BATCH = 5;
        public const int DOWNSYNC_MSG_ACT_PEER_UDP_ADDR = 6;
        public const int DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START = -1;
        public const int DOWNSYNC_MSG_ACT_BATTLE_START = 0;
        public const int DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED = -98;
        public const int DOWNSYNC_MSG_WS_CLOSED = -99;

        public const int MAGIC_JOIN_INDEX_INVALID = -2;
        public const int MAGIC_JOIN_INDEX_DEFAULT = -1;
        public const int MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL = 0;

        public const int MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED = -1;
        public const int MAGIC_LAST_SENT_INPUT_FRAME_ID_READDED = -2;

        public const int INVALID_DEFAULT_PLAYER_ID = 0;

        public static float MAX_FLOAT32 = float.MaxValue;
        public static int MAX_INT = 999999999;
        public static int COLLISION_PLAYER_INDEX_PREFIX = (1 << 17);
        public static int COLLISION_BARRIER_INDEX_PREFIX = (1 << 16);
        public static int COLLISION_BULLET_INDEX_PREFIX = (1 << 15);
        public static int PATTERN_ID_UNABLE_TO_OP = -2;
        public static int PATTERN_ID_NO_OP = -1;

        public static float COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO = 10.0f;
        public static float VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO = 1.0f / COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO;

        public static int DEFAULT_PLAYER_RADIUS = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int DEFAULT_PREALLOC_AI_PLAYER_CAPACITY = 8;
        public static int DEFAULT_PREALLOC_BULLET_CAPACITY = 64;

        public static int GRAVITY_X = 0;
        public static int GRAVITY_Y = -(int)(0.5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO); // makes all "playerCollider.Y" a multiple of 0.5 in all cases
        public static int INPUT_DELAY_FRAMES = 2; // in the count of render frames

        /*
		   [WARNING]
		   Experimentally having an input rate > 15 (e.g., 60 >> 2) doesn't improve multiplayer smoothness, in fact higher input rate often results in higher packet loss (both TCP and UDP) thus higher wrong prediction rate!
		*/
        public static int INPUT_SCALE_FRAMES = 2; // inputDelayedAndScaledFrameId = ((originalFrameId - InputDelayFrames) >> InputScaleFrames)

        public static int SP_ATK_LOOKUP_FRAMES = 5;
        public static float SNAP_INTO_PLATFORM_OVERLAP = 0.1f;
        public static float SNAP_INTO_PLATFORM_THRESHOLD = 0.5f;
        public static float VERTICAL_PLATFORM_THRESHOLD = 0.9f;
        public static int MAGIC_FRAMES_TO_BE_ON_WALL = 12;


        public static int DYING_FRAMES_TO_RECOVER = 60; // MUST BE SAME FOR EVERY CHARACTER FOR FAIRNESS!

        public static int NO_SKILL = -1;
        public static int NO_SKILL_HIT = -1;

        public static int NO_LOCK_VEL = -1;

        // Used in preallocated RoomDownsyncFrame to check termination
        public static int TERMINATING_BULLET_LOCAL_ID = (-1024);
        public static int TERMINATING_PLAYER_ID = (-1025);
        public static int TERMINATING_RENDER_FRAME_ID = (-1026);
        public static int TERMINATING_INPUT_FRAME_ID = (-1027);

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
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            InAirIdle1ByWallJump,
            InAirAtk1,
            InAirAtked1,
            BlownUp1,
            OnWall,
            Dashing // Yes dashing is an InAir state even if you dashed on the ground :)
        };

        public static HashSet<CharacterState> noOpSet = new HashSet<CharacterState>() {
            Atked1,
            InAirAtked1,
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
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            Atked1,
            InAirAtked1,
            BlownUp1,
            LayDown1,
            GetUp1,
            Dying
        };

        public static ImmutableDictionary<int, Skill> skills = ImmutableDictionary.Create<int, Skill>().AddRange(
                new[]
                {
                    new KeyValuePair<int, Skill>(1, new SkillBuilder(27, 27, 27, SkillTriggerType.RisingEdge, CharacterState.Atk1)
                                                    .AddHit(new BulletConfigBuilder(6, 22, 14, 9, 0, (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 11, 30, false, 2, 15, BulletType.Melee, 0).UpsertCancelTransit(1, 2).build())
                                                    .build()),

                    new KeyValuePair<int, Skill>(2, new SkillBuilder(25, 25, 25, SkillTriggerType.RisingEdge, CharacterState.Atk2)
                                                    .AddHit(new BulletConfigBuilder(6, 18, 20, 9, 0, (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 14, 36, false, 2, 15, BulletType.Melee, 0).UpsertCancelTransit(1, 3).build())
                                                    .build()),

                    new KeyValuePair<int, Skill>(3, new SkillBuilder(40, 40, 40, SkillTriggerType.RisingEdge, CharacterState.Atk3)
                                                    .AddHit(new BulletConfigBuilder(8, 30, MAX_INT, 9, 0, (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0, true, 2, 15, BulletType.Melee, 0).build())
                                                    .build()),

                    new KeyValuePair<int, Skill>(4, new SkillBuilder(60, 60, 60, SkillTriggerType.RisingEdge, CharacterState.Atk4)
                                                    .AddHit(new BulletConfigBuilder(16, MAX_INT, MAX_INT, 9, 0, (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(7f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, NO_LOCK_VEL, (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0, true, 3, 30, BulletType.Fireball, (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO)).build())
                                                    .build()),

                     new KeyValuePair<int, Skill>(5, new SkillBuilder(27, 27, 27, SkillTriggerType.RisingEdge, CharacterState.Atk1)
                                                    .AddHit(new BulletConfigBuilder(9, 16, 14, 9, 0, (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0, false, 2, 15, BulletType.Melee, 0).build())
                                                    .build()),

                    new KeyValuePair<int, Skill>(6, new SkillBuilder(27, 27, 27, SkillTriggerType.RisingEdge, CharacterState.Atk2)
                                                    .AddHit(new BulletConfigBuilder(7, MAX_INT, MAX_INT, 9, 0, (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, NO_LOCK_VEL, (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0, true, 2, 15, BulletType.Melee, 0).build())
                                                    .build()),

                    new KeyValuePair<int, Skill>(12, new SkillBuilder(10, 10, 10, SkillTriggerType.RisingEdge, CharacterState.Dashing)
                                                    .AddHit(new BulletConfigBuilder(3, 0, 0, 0, 0, NO_LOCK_VEL, NO_LOCK_VEL, (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0, 0, 0, 0, 0, 0, false, 0, 0, BulletType.Melee, 0).build())
                                                    .build()),

                    new KeyValuePair<int, Skill>(255, new SkillBuilder(30, 30, 30, SkillTriggerType.RisingEdge, CharacterState.InAirAtk1)
                                                    .AddHit(new BulletConfigBuilder(2, 20, 18, 9, 6, NO_LOCK_VEL, NO_LOCK_VEL, (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0, false, 2, 15, BulletType.Melee, 0).build())
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
                    8, (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-1 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))),
                    new KeyValuePair<int, CharacterConfig>(1, new CharacterConfig(
                    1, "SwordMan",
                    11, 1,
                    16, 16, 10, 27,
                    (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    9,
                    false, false,
                    0, 0,
                    0, 0))
                }
            );

        public static int FindSkillId(int patternId, CharacterDownsync currCharacterDownsync, int speciesId) {
            switch (speciesId) {
                case 0:
                    switch (patternId) {
                        case 1:
                            if (0 == currCharacterDownsync.FramesToRecover) {
                                if (currCharacterDownsync.InAir) {
                                    return 255;
                                } else {
                                    return 1;
                                }
                            } else {
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case 3:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 4;
                            } else {
                                return NO_SKILL;
                            }
                        case 5:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            // Air-dash is allowed for this speciesId
                            return 12;
                        default:
                            return NO_SKILL;
                    }
                case 1:
                    switch (patternId) {
                        case 1:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 5;
                            } else {
                                return NO_SKILL;
                            }
                        case 2:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 6;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                default:
                    return NO_SKILL;
            }

        }

    }
}
