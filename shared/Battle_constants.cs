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
        public const int DOWNSYNC_MSG_WS_OPEN = -100;

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
        public static int TERMINATING_BULLET_TEAM_ID = (-1028);
        public static int DEFAULT_BULLET_TEAM_ID = (1028);

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
                    new KeyValuePair<int, Skill>(1, new Skill {
                        RecoveryFrames = 27,
                        RecoveryFramesOnBlock = 27,
                        RecoveryFramesOnHit = 27,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                    }
                    .AddHit(
                        new BulletConfig {
                            StartupFrames = 6,
                            ActiveFrames = 22,
                            HitStunFrames = 22,
                            BlockStunFrames = 9,
                            Damage = 13,
                            PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            PushbackVelY = 0,
                            SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SelfLockVelY = NO_LOCK_VEL,
                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            CancellableStFrame = 11,
                            CancellableEdFrame = 30,
                            BlowUp = false,
                            SpeciesId = 2,
                            ExplosionFrames = 15,
                            BType = BulletType.Melee,
                            Speed = 0
                        }.UpsertCancelTransit(1, 2)
                    )),

                    new KeyValuePair<int, Skill>(2, new Skill{
                        RecoveryFrames = 25,
                        RecoveryFramesOnBlock = 25,
                        RecoveryFramesOnHit = 25,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk2
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 6,
                                ActiveFrames = 20,
                                HitStunFrames = 20,
                                BlockStunFrames = 9,
                                Damage = 14,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 14,
                                CancellableEdFrame = 36,
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }.UpsertCancelTransit(1, 3)
                        )),

                    new KeyValuePair<int, Skill>(3, new Skill{
                        RecoveryFrames = 40,
                        RecoveryFramesOnBlock = 40,
                        RecoveryFramesOnHit = 40,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk3
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 8,
                                ActiveFrames = 30,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 18,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = true,
                                SpeciesId = 2,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(4, new Skill{
                        RecoveryFrames = 60,
                        RecoveryFramesOnBlock = 60,
                        RecoveryFramesOnHit = 60,
                        MpDelta = 240,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 16,
                                ActiveFrames = MAX_INT,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(7f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = true,
                                SpeciesId = 3,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                Speed = (int)(2*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO)
                            }
                        )),

                     new KeyValuePair<int, Skill>(5, new Skill{
                        RecoveryFrames = 35,
                        RecoveryFramesOnBlock = 35,
                        RecoveryFramesOnHit = 35,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                     }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 9,
                                ActiveFrames = 16,
                                HitStunFrames = 16,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(6, new Skill{
                        RecoveryFrames = 27,
                        RecoveryFramesOnBlock = 27,
                        RecoveryFramesOnHit = 27,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 6,
                                ActiveFrames = 22,
                                HitStunFrames = 22,
                                BlockStunFrames = 9,
                                Damage = 15,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 14,
                                CancellableEdFrame = 30,
                                BlowUp = false,
                                SpeciesId = 1,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }.UpsertCancelTransit(1, 7)
                        )),

                    new KeyValuePair<int, Skill>(7, new Skill{
                        RecoveryFrames = 30,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk2
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 6,
                                ActiveFrames = 20,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 19,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = true,
                                SpeciesId = 1,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(8, new Skill{
                        RecoveryFrames = 40,
                        RecoveryFramesOnBlock = 40,
                        RecoveryFramesOnHit = 40,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk3
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                ActiveFrames = 30,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = (int)(7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = true,
                                SpeciesId = 1,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(9, new Skill{
                        RecoveryFrames = 60,
                        RecoveryFramesOnBlock = 60,
                        RecoveryFramesOnHit = 60,
                        MpDelta = 270,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 16,
                                ActiveFrames = MAX_INT,
                                HitStunFrames = 30,
                                BlockStunFrames = 9,
                                Damage = 14,
                                PushbackVelX = (int)(0.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO)
                            }
                        )),

                    new KeyValuePair<int, Skill>(10, new Skill{
                        RecoveryFrames = 10,
                        RecoveryFramesOnBlock = 10,
                        RecoveryFramesOnHit = 10,
                        MpDelta = 60,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Dashing
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 3,
                                ActiveFrames = 0,
                                HitStunFrames = 0,
                                BlockStunFrames = 0,
                                Damage = 0,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                HitboxOffsetX = 0,
                                HitboxOffsetY = 0,
                                HitboxSizeX = 0,
                                HitboxSizeY = 0,
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 0,
                                ExplosionFrames = 0,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(11, new Skill{
                        RecoveryFrames = 10,
                        RecoveryFramesOnBlock = 10,
                        RecoveryFramesOnHit = 10,
                        MpDelta = 60,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Dashing
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 3,
                                ActiveFrames = 0,
                                HitStunFrames = 0,
                                BlockStunFrames = 0,
                                Damage = 0,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                HitboxOffsetX = 0,
                                HitboxOffsetY = 0,
                                HitboxSizeX = 0,
                                HitboxSizeY = 0,
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 0,
                                ExplosionFrames = 0,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(12, new Skill{
                        RecoveryFrames = 40,
                        RecoveryFramesOnBlock = 40,
                        RecoveryFramesOnHit = 40,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk2
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                ActiveFrames = 20,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 11,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = (int)(7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = true,
                                SpeciesId = 2,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(13, new Skill{
                        RecoveryFrames = 30,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 270,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 16,
                                ActiveFrames = MAX_INT,
                                HitStunFrames = 30,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = (int)(0.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(18*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 4,
                                ExplosionFrames = 20,
                                BType = BulletType.Fireball,
                                Speed = (int)(4.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO)
                            }
                        )),

                    new KeyValuePair<int, Skill>(255, new Skill {
                        RecoveryFrames = 30,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = InAirAtk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 2,
                                ActiveFrames = 20,
                                HitStunFrames = 18,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        )),

                    new KeyValuePair<int, Skill>(256, new Skill{
                        RecoveryFrames = 30,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = InAirAtk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 2,
                                ActiveFrames = 20,
                                HitStunFrames = 18,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 15,
                                BType = BulletType.Melee,
                                Speed = 0
                            }
                        ))
                }
        );

        public static ImmutableDictionary<int, CharacterConfig> characters = ImmutableDictionary.Create<int, CharacterConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, CharacterConfig>(0, new CharacterConfig {
                        SpeciesId = 0,
                        SpeciesName = "KnifeGirl",
                        InAirIdleFrameIdxTurningPoint = 11,
                        InAirIdleFrameIdxTurnedCycle = 1,
                        LayDownFrames = 16,
                        LayDownFramesToRecover = 16,
                        GetUpInvinsibleFrames = 10,
                        GetUpFramesToRecover = 27,
                        Speed = (int)(2.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        JumpingInitVelY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        InertiaFramesToRecover = 9,
                        DashingEnabled = true,
                        OnWallEnabled = true,
                        WallJumpingFramesToRecover = 8,
                        WallJumpingInitVelX = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        WallJumpingInitVelY =  (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        WallSlidingVelY = (int)(-1 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionOffsetX = (int)(8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionOffsetY = (int)(24f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeX = (int)(48.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeY = (int)(80.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeY = (int)(62.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeY = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeX = (int)(48.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeY = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeY = (int)(62.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        MpRegenRate = 1
                    }),

                    new KeyValuePair<int, CharacterConfig>(1, new CharacterConfig {
                        SpeciesId = 1,
                        SpeciesName = "SwordMan",
                        InAirIdleFrameIdxTurningPoint = 11,
                        InAirIdleFrameIdxTurnedCycle = 1,
                        LayDownFrames = 16,
                        LayDownFramesToRecover = 16,
                        GetUpInvinsibleFrames = 10,
                        GetUpFramesToRecover = 27,
                        Speed = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        JumpingInitVelY = (int)(5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        InertiaFramesToRecover = 9,
                        VisionOffsetX = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionOffsetY = (int)(16.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeX = (int)(80.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeY = (int)(80.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeY = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeY = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeX = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeY = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeX = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeY = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        MpRegenRate = 1
                    }),

                    new KeyValuePair<int, CharacterConfig>(2, new CharacterConfig {
                        SpeciesId = 2,
                        SpeciesName = "MonkGirl",
                        InAirIdleFrameIdxTurningPoint = 11,
                        InAirIdleFrameIdxTurnedCycle = 1,
                        LayDownFrames = 16,
                        LayDownFramesToRecover = 16,
                        GetUpInvinsibleFrames = 10,
                        GetUpFramesToRecover = 27,
                        Speed = (int)(1.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        JumpingInitVelY = (int)(7 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        InertiaFramesToRecover = 9,
                        DashingEnabled = true,
                        OnWallEnabled = true,
                        WallJumpingFramesToRecover = 8,
                        WallJumpingInitVelX = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        WallJumpingInitVelY =  (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        WallSlidingVelY = (int)(-1 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionOffsetX = (int)(19.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionOffsetY = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeX = (int)(130.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeY = (int)(80.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeX = (int)(28f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeY = (int)(46f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeX = (int)(28.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeY = (int)(28.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeX = (int)(46.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeY = (int)(28.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeX = (int)(28f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeY = (int)(46f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        MpRegenRate = 1
                    }),

                    new KeyValuePair<int, CharacterConfig>(3, new CharacterConfig {
                        SpeciesId = 3,
                        SpeciesName = "FireSwordMan",
                        InAirIdleFrameIdxTurningPoint = 11,
                        InAirIdleFrameIdxTurnedCycle = 1,
                        LayDownFrames = 16,
                        LayDownFramesToRecover = 16,
                        GetUpInvinsibleFrames = 10,
                        GetUpFramesToRecover = 27,
                        Speed = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        JumpingInitVelY = (int)(5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        InertiaFramesToRecover = 9,
                        VisionOffsetX = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionOffsetY = (int)(16.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeX = (int)(130.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        VisionSizeY = (int)(80.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DefaultSizeY = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeX = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        ShrinkedSizeY = (int)(24.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeX = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        LayDownSizeY = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeX = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DyingSizeY = (int)(44.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        MpRegenRate = 1
                    }),
            });

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
                            return 11;
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
                                return 12;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case 2:
                    switch (patternId) {
                        case 1:
                            if (0 == currCharacterDownsync.FramesToRecover) {
                                if (currCharacterDownsync.InAir) {
                                    return 256;
                                } else {
                                    return 6;
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
                        case 2:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 8;
                            } else {
                                return NO_SKILL;
                            }
                        case 3:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 9;
                            } else {
                                return NO_SKILL;
                            }
                        case 5:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            // Air-dash is prohibited for this speciesId
                            if (!currCharacterDownsync.InAir) {
                                return 10;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case 3:
                    switch (patternId) {
                        case 1:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 5;
                            } else {
                                return NO_SKILL;
                            }
                        case 2:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 12;
                            } else {
                                return NO_SKILL;
                            }
                        case 3:
                            if (0 == currCharacterDownsync.FramesToRecover && !currCharacterDownsync.InAir) {
                                return 13;
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
