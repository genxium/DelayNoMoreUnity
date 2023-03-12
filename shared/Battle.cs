using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace shared {
    public class Battle {
		public static double MAX_FLOAT64 = Double.MaxValue;
		public static int MAX_INT = 999999999;
		public static int COLLISION_PLAYER_INDEX_PREFIX = (1 << 17);
		public static int COLLISION_BARRIER_INDEX_PREFIX = (1 << 16);
		public static int COLLISION_BULLET_INDEX_PREFIX = (1 << 15);
		public static int PATTERN_ID_UNABLE_TO_OP = -2;
		public static int PATTERN_ID_NO_OP = -1;

		public static double WORLD_TO_VIRTUAL_GRID_RATIO = 10.0;
		public static double VIRTUAL_GRID_TO_WORLD_RATIO = 1.0 / WORLD_TO_VIRTUAL_GRID_RATIO;

		public static int GRAVITY_X = 0;
		public static int GRAVITY_Y = -(int)(0.5 * WORLD_TO_VIRTUAL_GRID_RATIO); // makes all "playerCollider.Y" a multiple of 0.5 in all cases
		public static int INPUT_DELAY_FRAMES = 4; // in the count of render frames

		/*
		   [WARNING]
		   Experimentally having an input rate > 15 (e.g., 60 >> 2) doesn't improve multiplayer smoothness, in fact higher input rate often results in higher packet loss (both TCP and UDP) thus higher wrong prediction rate!
		*/
		public static int INPUT_SCALE_FRAMES = 2; // inputDelayedAndScaledFrameId = ((originalFrameId - InputDelayFrames) >> InputScaleFrames)

		public static int SP_ATK_LOOKUP_FRAMES = 5;
		public static double SNAP_INTO_PLATFORM_OVERLAP = 0.1;
		public static double SNAP_INTO_PLATFORM_THRESHOLD = 0.5;
		public static double VERTICAL_PLATFORM_THRESHOLD = 0.9;
		public static int MAGIC_FRAMES_TO_BE_ONWALL = 12;


		public static int DYING_FRAMES_TO_RECOVER = 60; // MUST BE SAME FOR EVERY CHARACTER FOR FAIRNESS!

		public static int NO_SKILL = -1;
		public static int NO_SKILL_HIT = -1;

		public static int NO_LOCK_VEL = -1;

		// Used in preallocated RoomDownsyncFrame to check termination
		public static int TERMINATING_BULLET_LOCAL_ID = (-1);
		public static int TERMINATING_PLAYER_ID = (-1);
		public static int TERMINATING_RENDER_FRAME_ID = (-1);

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

	public static int ATK_CHARACTER_STATE_IDLE1 = (0);

		public static int ATK_CHARACTER_STATE_WALKING = (1);

		public static int ATK_CHARACTER_STATE_ATK1 = (2);
		public static int ATK_CHARACTER_STATE_ATKED1 = (3);

		public static int ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP = (4);

		public static int ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP = (5);

		public static int ATK_CHARACTER_STATE_INAIR_ATK1 = (6);
		public static int ATK_CHARACTER_STATE_INAIR_ATKED1 = (7);
		public static int ATK_CHARACTER_STATE_BLOWN_UP1 = (8);
		public static int ATK_CHARACTER_STATE_LAY_DOWN1 = (9);
		public static int ATK_CHARACTER_STATE_GET_UP1 = (10);
		public static int ATK_CHARACTER_STATE_ATK2 = (11);

		public static int ATK_CHARACTER_STATE_ATK3 = (12);

		public static int ATK_CHARACTER_STATE_ATK4 = (13);

		public static int ATK_CHARACTER_STATE_ATK5 = (14);

		public static int ATK_CHARACTER_STATE_DASHING = (15);

	public static int ATK_CHARACTER_STATE_ONWALL = (16);

    public static int ATK_CHARACTER_STATE_TURNAROUND = (17);

    public static int ATK_CHARACTER_STATE_DYING = (18);

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
		public static bool ShouldGenerateInputFrameUpsync(int renderFrameId) {
			return ((renderFrameId & ((1 << INPUT_SCALE_FRAMES) - 1)) == 0);
		}

		public static (bool, int) ShouldPrefabInputFrameDownsync(int prevRenderFrameId, int renderFrameId) {
			for (int i = prevRenderFrameId + 1; i <= renderFrameId; i++) {
				if ((0 <= i) && ShouldGenerateInputFrameUpsync(i)) {
					return (true, i);
				}
			}
			return (false, -1);
		}

		public static int ConvertToDelayedInputFrameId(int renderFrameId) {
			if (renderFrameId < INPUT_DELAY_FRAMES) {
						return 0;
			}
			return ((renderFrameId - INPUT_DELAY_FRAMES) >> INPUT_SCALE_FRAMES);
		}

		public static int ConvertToNoDelayInputFrameId(int renderFrameId) {
			return (renderFrameId >> INPUT_SCALE_FRAMES);
		}

		public static int ConvertToFirstUsedRenderFrameId(int inputFrameId) {
			return ((inputFrameId << INPUT_SCALE_FRAMES) + INPUT_DELAY_FRAMES);
		}

		public static int ConvertToLastUsedRenderFrameId(int inputFrameId) {
			return ((inputFrameId << INPUT_SCALE_FRAMES) + INPUT_DELAY_FRAMES + (1 << INPUT_SCALE_FRAMES) - 1);
		}

    }
}
