using System;
using System.Collections.Generic;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        public const int BATTLE_DYNAMICS_FPS = 60;
        public const int DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD = 10000; // in milliseconds

        // Deliberately NOT using enum for "room states" to make use of "C# CompareAndExchange" 
        public const int ROOM_ID_NONE = 0;

        public const long ROOM_STATE_IMPOSSIBLE = 0;
        public const long ROOM_STATE_IDLE = 1;
        public const long ROOM_STATE_WAITING = 2;
        public const long ROOM_STATE_PREPARE = 3;
        public const long ROOM_STATE_IN_BATTLE = 4;
        public const long ROOM_STATE_IN_SETTLEMENT = 5;
        public const long ROOM_STATE_STOPPED = 6;
        public const long ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN = 7;
        public const long ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN = 8;
        public const long ROOM_STATE_FRONTEND_REJOINING = 9;

        // Deliberately NOT using enum for "player battle states" to make use of "C# CompareAndExchange" 
        public const long PLAYER_BATTLE_STATE_IMPOSSIBLE = -2;
        public const long PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK = 0;
        public const long PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC = 1;
        public const long PLAYER_BATTLE_STATE_ACTIVE = 2;
        public const long PLAYER_BATTLE_STATE_DISCONNECTED = 3;
        public const long PLAYER_BATTLE_STATE_LOST = 4;
        public const long PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME = 5;
        public const long PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL = 6;

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
        public const int DOWNSYNC_MSG_ACT_PLAYER_DISCONNECTED = -96;
        public const int DOWNSYNC_MSG_ACT_PLAYER_READDED_AND_ACKED = -97;
        public const int DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED = -98;
        public const int DOWNSYNC_MSG_WS_CLOSED = -99;
        public const int DOWNSYNC_MSG_WS_OPEN = -100;

        public const int MAGIC_JOIN_INDEX_INVALID = 0;
        public const int MAGIC_JOIN_INDEX_DEFAULT = -1;
        public const int MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL = 0;
        public const int MAGIC_QUOTA_INFINITE = -1;

        public const int MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED = -1;
        public const int MAGIC_LAST_SENT_INPUT_FRAME_ID_READDED = -2;

        public const int BGM_NO_CHANGE = 0;

        public const int INVALID_DEFAULT_PLAYER_ID = 0;

        public static float MAX_FLOAT32 = float.MaxValue;
        public static int MAX_INT = 999999999;
        public static uint MAX_UINT = 999999999;
        public static int MAX_REVERSE_PUSHBACK_FRAMES_TO_RECOVER = 30;

        public static ulong COLLISION_NONE_INDEX = 0;
        public static ulong COLLISION_BARRIER_INDEX_PREFIX = (1 << 0);
        public static ulong COLLISION_CHARACTER_INDEX_PREFIX = (1 << 1);
        public static ulong COLLISION_TRAP_INDEX_PREFIX = (1 << 2);
        public static ulong COLLISION_PICKABLE_INDEX_PREFIX = (1 << 3);

        public static ulong COLLISION_MELEE_BULLET_INDEX_PREFIX = (1 << 4);
        public static ulong COLLISION_B_M_FIREBALL_INDEX_PREFIX = (1 << 5); // type of fireball that collides with both barrier and melee (and of course characters and traps)
        public static ulong COLLISION_B_FIREBALL_INDEX_PREFIX = (1 << 6); // type of fireball that collides with barrier but not melee
        public static ulong COLLISION_M_FIREBALL_INDEX_PREFIX = (1 << 7); // type of fireball that collides with melee but not barrier
        public static ulong COLLISION_FIREBALL_INDEX_PREFIX = (1 << 8); // type of fireball that doesn't collide with barrier or melee

        public static ulong COLLISION_NPC_PATROL_CUE_INDEX_PREFIX = (1 << 9);
        public static ulong COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX = (1 << 10);
        public static ulong COLLISION_TRIGGER_INDEX_PREFIX = (1 << 11);

        public static ulong COLLISION_VISION_INDEX_PREFIX = (1 << 12);
        public static ulong COLLISION_FLYING_CHARACTER_INDEX_PREFIX = (1 << 13);
        public static ulong COLLISION_FLYING_NPC_PATROL_CUE_INDEX_PREFIX = (1 << 14);
        public static ulong COLLISION_REFRACTORY_INDEX_PREFIX = (1 << 15);
        public static ulong COLLISION_BARRIER_FREE_INDEX_PREFIX = (1 << 16);

        public static ulong TRIGGER_MASK_NONE = 0;
        public static ulong TRIGGER_MASK_BY_MOVEMENT = (1 << 0);
        public static ulong TRIGGER_MASK_BY_ATK = (1 << 1);
        public static ulong TRIGGER_MASK_BY_CYCLIC_TIMER = (1 << 2);
        public static ulong TRIGGER_MASK_BY_SUBSCRIPTION = (1 << 3);

        public static int TERMINATING_EVTSUB_ID_INT = 0; // Default for proto int32 to save space in "CharacterDownsync.subscriptionId"
        public static int MAGIC_EVTSUB_ID_DUMMY = 65535;

        public static int SPEED_NOT_HIT_NOT_SPECIFIED = 0;

        public static HashSet<ulong> COLLIDABLE_PAIRS = new HashSet<ulong>() {
            COLLISION_CHARACTER_INDEX_PREFIX, // such that characters collide with each other
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX, 
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX ,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_PICKABLE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_NPC_PATROL_CUE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX, // such that flying characters collide with each other as well as non-flying ones
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX, 
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX ,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_PICKABLE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_NPC_PATROL_CUE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            // Melee bullet, it wouldn't collide with barrier, specifically 
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            // Fireball bullets
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX, // Refractory only
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX, // Refractory only
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_M_FIREBALL_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX, // Refractory only
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX, // Refractory only
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_MELEE_BULLET_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_M_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_B_M_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_MELEE_BULLET_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            // Trap
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_FREE_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_VISION_INDEX_PREFIX,
            
            // Vision
            COLLISION_VISION_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX, 
            COLLISION_VISION_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_FLYING_CHARACTER_INDEX_PREFIX, 
            COLLISION_VISION_INDEX_PREFIX | COLLISION_MELEE_BULLET_INDEX_PREFIX, 
            COLLISION_VISION_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX, 
            COLLISION_VISION_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX, 
            COLLISION_VISION_INDEX_PREFIX | COLLISION_M_FIREBALL_INDEX_PREFIX,
            COLLISION_VISION_INDEX_PREFIX | COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        
            // Pickable
            COLLISION_PICKABLE_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_PICKABLE_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_PICKABLE_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_PICKABLE_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_REFRACTORY_INDEX_PREFIX,
        };

        public const float COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO = 10.0f;
        public static float VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO = 1.0f / COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO;
        public static float CLAMPABLE_COLLISION_SPACE_MAG = VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO / 10.0f;
        public static float CLAMPABLE_COLLISION_SPACE_MAG_SQUARED = (CLAMPABLE_COLLISION_SPACE_MAG*CLAMPABLE_COLLISION_SPACE_MAG);

        public static int DEFAULT_PLAYER_RADIUS = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int DEFAULT_PREALLOC_NPC_CAPACITY = 24; // 1 serialized "CharacterDownsync" is around 112 bytes per experiment, (7465 - 7017)/(28-24) 
        public static int DEFAULT_PREALLOC_BULLET_CAPACITY = 48; // 1 serialized "Bullet" is around 18.5 bytes per experiment, (7465 - 7317)/(56 - 48)
        public static int DEFAULT_PREALLOC_TRAP_CAPACITY = 12;
        public static int DEFAULT_PREALLOC_TRIGGER_CAPACITY = 14;
        public static int DEFAULT_PREALLOC_PICKABLE_CAPACITY = 32;
        public static int DEFAULT_PER_CHARACTER_BUFF_CAPACITY = 1;
        public static int DEFAULT_PER_CHARACTER_DEBUFF_CAPACITY = 1;
        public static int DEFAULT_PER_CHARACTER_INVENTORY_CAPACITY = 3;
        public static int DEFAULT_PER_CHARACTER_IMMUNE_BULLET_RECORD_CAPACITY = 3;

        public static int GRAVITY_X = 0;
        public static int GRAVITY_Y = -(int)(0.59f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int GRAVITY_Y_JUMP_HOLDING = -(int)(0.35f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);

        public static int INPUT_DELAY_FRAMES = 2; // in the count of render frames
        public static int DEFAULT_PATROL_CUE_WAIVING_FRAMES = 150; // in the count of render frames, should be big enough for any NPC to move across the largest patrol cue
        public static int NO_PATROL_CUE_ID = -1;
        public static int NO_VFX_ID = 0;
        
        public static int DEFAULT_PICKABLE_HITBOX_SIZE_X = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int DEFAULT_PICKABLE_HITBOX_SIZE_Y = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int DEFAULT_PICKABLE_DISAPPEARING_ANIM_FRAMES = 10;
        public static int DEFAULT_PICKABLE_CONSUMED_ANIM_FRAMES = 30;
        public static int DEFAULT_PICKABLE_RISING_VEL_Y_VIRTUAL_GRID = (int)(8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int DEFAULT_PICKABLE_NONPICKABLE_STARTUP_FRAMES = 45;
        public static int DEFAULT_FRAMES_TO_SHOW_DAMAGED = (int)(1.2f * BATTLE_DYNAMICS_FPS);
        public static int DEFAULT_FRAMES_TO_CONTINUE_COMBO = (int)(0.8f * BATTLE_DYNAMICS_FPS);

        public static int DEFAULT_BLOCK_STUN_FRAMES = 10;
        public static int DEFAULT_BLOWNUP_FRAMES_FOR_FLYING = 30;
        
        public static int DEFAULT_GAUGE_INC_BY_HIT = 5;

        public static int DEFAULT_FRAMES_DELAYED_OF_BOSS_SAVEPOINT = 8;
        /*
		   [WARNING]
		   Experimentally having an input rate > 15 (e.g., 60 >> 2) doesn't improve multiplayer smoothness, in fact higher input rate often results in higher packet loss (both TCP and UDP) thus higher wrong prediction rate!
		*/
        public static int INPUT_SCALE_FRAMES = 2; // inputDelayedAndScaledFrameId = ((originalFrameId - InputDelayFrames) >> InputScaleFrames)

        public static int SP_ATK_LOOKUP_FRAMES = 5;
        public static float SNAP_INTO_PLATFORM_OVERLAP = 0.1f;
        public static int SNAP_INTO_PLATFORM_OVERLAP_VIRTUAL_GRID = (int)(SNAP_INTO_PLATFORM_OVERLAP* COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);

        public static float GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP = 0.8f;
        public static float SIDE_PUSHBACK_REPEL_THRESHOLD = 6.0f;

        public static float SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE = 8.0f; // Currently only supports rectilinear rectangle shape; kindly note that "8.0f" is half the minimum height in any feasible map of this game!
        public static int SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL = (int)(SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int SLIP_JUMP_CHARACTER_DROP_VIRTUAL = (SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL << 1) + (SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL >> 1);

        // [WARNING] The "zero overlap collision" might be randomly detected/missed on either frontend or backend, to have deterministic result we added paddings to all sides of a characterCollider. As each velocity component of (velX, velY) being a multiple of 0.5 at any renderFrame, each position component of (x, y) can only be a multiple of 0.5 too, thus whenever a 1-dimensional collision happens between players from [player#1: i*0.5, player#2: j*0.5, not collided yet] to [player#1: (i+k)*0.5, player#2: j*0.5, collided], the overlap becomes (i+k-j)*0.5+2*s, and after snapping subtraction the effPushback magnitude for each player is (i+k-j)*0.5, resulting in 0.5-multiples-position for the next renderFrame.
        public static float SNAP_INTO_CHARACTER_OVERLAP = 2 * SNAP_INTO_PLATFORM_OVERLAP;
        public static float SNAP_INTO_PLATFORM_THRESHOLD = 0.5f;
        public static float VERTICAL_PLATFORM_THRESHOLD = 0.9f;
        public static int MAGIC_FRAMES_TO_BE_ON_WALL = 12;

        public static int DYING_FRAMES_TO_RECOVER = 100; // MUST BE SAME FOR EVERY CHARACTER FOR FAIRNESS!

        public static int PARRIED_FRAMES_TO_RECOVER = 25;
        public static int PARRIED_FRAMES_TO_START_CANCELLABLE = 4;

        public static uint NO_SKILL = 0;
        public static int NO_SKILL_HIT = 0;

        public static uint INVENTORY_BTN_B_SKILL_BH = 4094;
        public static uint INVENTORY_BTN_B_SKILL_MSG = 4095;

        public static int NO_LOCK_VEL = -1;

        public static ulong EVTSUB_NO_DEMAND_MASK = 0;

        // TODO: Shall I use 0 for all "TERMINATING_XXX_ID"s to save space in serialization?
        // Used in preallocated RoomDownsyncFrame to check termination
        public static int TERMINATING_RENDER_FRAME_ID = (-1026);
        public static int TERMINATING_INPUT_FRAME_ID = (-1027);

        public static int TERMINATING_TRAP_ID = 0;
        public static int TERMINATING_TRIGGER_ID = 0;
        public static int TERMINATING_PICKABLE_LOCAL_ID = 0;
        public static int TERMINATING_PLAYER_ID = 0;
        public static int TERMINATING_BULLET_LOCAL_ID = 0;
        public static int TERMINATING_BULLET_TEAM_ID = 0; // Default for proto int32 to save space
        public static uint TERMINATING_BUFF_SPECIES_ID = 0; // Default for proto int32 to save space in "CharacterDownsync.killedToDropBuffSpeciesId"
        public static uint TERMINATING_DEBUFF_SPECIES_ID = 0;
        public static uint TERMINATING_EVTSUB_ID_UINT = (uint)TERMINATING_EVTSUB_ID_INT;
        public static uint TERMINATING_CONSUMABLE_SPECIES_ID = 0; // Default for proto int32 to save space in "CharacterDownsync.killedToDropConsumableSpeciesId"

        public static int DEFAULT_BULLET_TEAM_ID = -1028;

        public static int FRONTEND_WS_RECV_BYTELENGTH = 8196; // Expirically enough and not too big to have a graphic smoothness impact when receiving
        public static int BACKEND_WS_RECV_BYTELENGTH = (FRONTEND_WS_RECV_BYTELENGTH + (FRONTEND_WS_RECV_BYTELENGTH >> 1)); // Slightly larger than FRONTEND_WS_RECV_BYTELENGTH because it has to receive some initial collider information

        // These directions are chosen such that when speed is changed to "(speedX+delta, speedY+delta)" for any of them, the direction is unchanged.
        public static int[,] DIRECTION_DECODER = new int[,] {
            {0, 0}, // 0
            {0, +2}, // 1
            {0, -2}, // 2
            {+2, 0}, // 3
            {-2, 0}, // 4
            {+1, +1}, // 5
            {-1, -1}, // 6
            {+1, -1}, // 7
            {-1, +1}, // 8
        };

        public static HashSet<CharacterState> proactiveJumpingSet = new HashSet<CharacterState>() {
            InAirIdle1ByJump,
            InAirIdle1ByWallJump,
            InAirIdle2ByJump,
        };

        public static HashSet<CharacterState> inAirSet = new HashSet<CharacterState>() {
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            InAirIdle1ByWallJump,
            InAirIdle2ByJump,
            InAirAtk1,
            InAirAtk2,
            InAirAtked1,
            BlownUp1,
            OnWallIdle1,
            InAirWalking,
            InAirWalkStopping,
            Dashing // Yes dashing is an InAir state even if you dashed on the ground :)
        };

        public static HashSet<CharacterState> noOpSet = new HashSet<CharacterState>() {
            Atked1,
            InAirAtked1,
            CrouchAtked1,
            BlownUp1,
            LayDown1,
			// [WARNING] During the invinsible frames of GET_UP1, the player is allowed to take any action
			Dying,
            Dimmed
        };

        public static HashSet<CharacterState> invinsibleSet = new HashSet<CharacterState>() {
            BlownUp1,
            LayDown1,
            GetUp1,
            Dying,
            TransformingInto
        };

        public static HashSet<CharacterState> nonAttackingSet = new HashSet<CharacterState>() {
            Idle1,
            Walking,
            WalkStopping,
            Dashing,
            BackDashing,
            Sliding,
            GroundDodged, 
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            InAirIdle1ByWallJump,
            InAirIdle2ByJump,
            InAirWalking,
            InAirWalkStopping,
            OnWallIdle1,
            CrouchIdle1,
            GetUp1,
            Def1, 
            Def1Atked1,
            Def1Broken,
            Atked1,
            InAirAtked1,
            CrouchAtked1,
            BlownUp1,
            LayDown1,
            Dying,
            Dimmed, 
            TransformingInto,
            Awaking
        };

        public static HashSet<CharacterState> btnBChargeableSet = new HashSet<CharacterState>() {
            Idle1,
            Walking,
            WalkStopping,
            Dashing,
            BackDashing,
            Sliding,
            GroundDodged,
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            InAirIdle1ByWallJump,
            InAirIdle2ByJump,
            InAirWalking,
            InAirWalkStopping,
            OnWallIdle1,
            CrouchIdle1,
            GetUp1,

            Atk7Charging,
        };

        public static HashSet<CharacterState> shrinkedSizeSet = new HashSet<CharacterState>() {
            BlownUp1,
            LayDown1,
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            InAirIdle2ByJump,
            InAirIdle1ByWallJump,
            InAirAtk1,
            InAirAtk2,
            InAirAtked1,
            InAirWalking,
            OnWallIdle1,
            Sliding,
            GroundDodged,
            CrouchIdle1,
            CrouchAtk1,
            CrouchAtked1,
        };

        public static int BTN_B_HOLDING_RDF_CNT_THRESHOLD_2 = BATTLE_DYNAMICS_FPS + (BATTLE_DYNAMICS_FPS >> 1);
        public static int BTN_B_HOLDING_RDF_CNT_THRESHOLD_1 = (BTN_B_HOLDING_RDF_CNT_THRESHOLD_2 >> 1);

        public static int JUMP_HOLDING_RDF_CNT_THRESHOLD_1 = (4 << INPUT_SCALE_FRAMES) - 3;  
        public static int JUMP_HOLDING_IFD_CNT_THRESHOLD_1 = (int)Math.Ceiling((float)JUMP_HOLDING_RDF_CNT_THRESHOLD_1/(1 << INPUT_SCALE_FRAMES));  

        public static int JUMP_HOLDING_RDF_CNT_THRESHOLD_2 = (8 << INPUT_SCALE_FRAMES) - 3;  
        public static int JUMP_HOLDING_IFD_CNT_THRESHOLD_2 = (int)Math.Ceiling((float)JUMP_HOLDING_RDF_CNT_THRESHOLD_2/(1 << INPUT_SCALE_FRAMES));  

        public static int IN_AIR_DASH_GRACE_PERIOD_RDF_CNT = 3;
        public static int IN_AIR_JUMP_GRACE_PERIOD_RDF_CNT = 6;

        public static int BTN_E_HOLDING_RDF_CNT_THRESHOLD_1 = (4 << INPUT_SCALE_FRAMES) - 3;  
        public static int BTN_E_HOLDING_IFD_CNT_THRESHOLD_1 = (int)Math.Ceiling((float)BTN_E_HOLDING_RDF_CNT_THRESHOLD_1/(1 << INPUT_SCALE_FRAMES));  
    }
}
