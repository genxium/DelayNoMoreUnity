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

        public static InputFrameDecoded DecodeInput(ulong encodedInput) {
            int encodedDirection = (int)(encodedInput & 15);
            int btnALevel = (int)((encodedInput >> 4) & 1);
            int btnBLevel = (int)((encodedInput >> 5) & 1);

            InputFrameDecoded ret = new InputFrameDecoded();
            ret.Dx = DIRECTION_DECODER[encodedDirection, 0];
            ret.Dy = DIRECTION_DECODER[encodedDirection, 1];
            ret.BtnALevel = btnALevel;
            ret.BtnBLevel = btnBLevel;
            return ret;
        }

        public static (bool, double, double) calcPushbacks(ConvexPolygon a, ConvexPolygon b, ref SatResult overlapResult) {
            overlapResult.OverlapMag = 0;
            overlapResult.OverlapX = 0;
            overlapResult.OverlapY = 0;
            overlapResult.AContainedInB = true;
            overlapResult.BContainedInA = true;
            overlapResult.AxisX = 0;
            overlapResult.AxisY = 0;

            bool overlapped = isPolygonPairOverlapped(a, b, ref overlapResult);
            if (true == overlapped) {
                double pushbackX = overlapResult.OverlapMag * overlapResult.OverlapX;
                double pushbackY = overlapResult.OverlapMag * overlapResult.OverlapY;
                return (true, pushbackX, pushbackY);
            } else {
                return (false, 0, 0);
            }
        }

        public static float InvSqrt32(float x) {
            float xhalf = 0.5f * x;
            int i = BitConverter.SingleToInt32Bits(x);
            i = 0x5f3759df - (i >> 1);
            x = BitConverter.Int32BitsToSingle(i);
            x = x * (1.5f - xhalf * x * x);
            return x;
        }

        public static double InvSqrt64(double x) {
            double xhalf = 0.5 * x;
            long i = BitConverter.DoubleToInt64Bits(x);
            i = 0x5fe6eb50c7b537a9 - (i >> 1);
            x = BitConverter.Int64BitsToDouble(i);
            x = x * (1.5 - xhalf * x * x);
            return x;
        }

        public static bool isPolygonPairOverlapped(ConvexPolygon a, ConvexPolygon b, ref SatResult result) {
            int aCnt = a.Points.Cnt;
            int bCnt = b.Points.Cnt;
            // Single point case
            if (1 == aCnt && 1 == bCnt) {
                result.OverlapMag = 0;
                Vector? aPoint = a.GetPointByOffset(0);
                Vector? bPoint = b.GetPointByOffset(0);
                return null != aPoint && null != bPoint && aPoint.X == bPoint.X && aPoint.Y == bPoint.Y;
            }

            if (1 < aCnt) {
                // Deliberately using "Points" instead of "SATAxes" to avoid unnecessary heap memory alloc
                for (int i = 0; i < aCnt; i++) {
                    Vector? u = a.GetPointByOffset(i);
					if (null == u) {
						throw new ArgumentNullException("Getting a null point u from polygon a!");
					}
                    Vector? v = a.GetPointByOffset(0);
                    if (i != aCnt - 1) {
                        v = a.GetPointByOffset(i + 1);
                    }
					if (null == v) {
						throw new ArgumentNullException("Getting a null point v from polygon a!");
					}
                    double dx = v.X - u.X;
                    double dy = v.Y - u.Y;
                    double invSqrtForAxis = InvSqrt64(dx * dx + dy * dy);
                    dx *= invSqrtForAxis;
                    dy *= invSqrtForAxis;
                    if (isPolygonPairSeparatedByDir(a, b, dx, dy, ref result)) {
                        return false;
                    }
                }
            }

            if (1 < bCnt) {
                for (int i = 0; i < bCnt; i++) {
                    Vector? u = b.GetPointByOffset(i);
					if (null == u) {
						throw new ArgumentNullException("Getting a null point u from polygon b!");
					}
                    Vector? v = b.GetPointByOffset(0);
                    if (i != bCnt - 1) {
                        v = b.GetPointByOffset(i + 1);
                    }
					if (null == v) {
						throw new ArgumentNullException("Getting a null point v from polygon b!");
					}
                    double dx = v.X - u.X;
                    double dy = v.Y - u.Y;
                    double invSqrtForAxis = InvSqrt64(dx * dx + dy * dy);
                    dx *= invSqrtForAxis;
                    dy *= invSqrtForAxis;
                    if (isPolygonPairSeparatedByDir(a, b, dx, dy, ref result)) {
                        return false;
                    }
                }
            }

            return true;
        }

		public static bool isPolygonPairSeparatedByDir(ConvexPolygon a, ConvexPolygon b, double axisX, double axisY, ref SatResult result) {
			/*
				[WARNING] This function is deliberately made private, it shouldn't be used alone (i.e. not along the norms of a polygon), otherwise the pushbacks calculated would be meaningless.

				Consider the following example
				a: {
					anchor: [1337.19 1696.74]
					points: [[0 0] [24 0] [24 24] [0 24]]
				},
				b: {
					anchor: [1277.72 1570.56]
					points: [[642.57 319.16] [0 319.16] [5.73 0] [643.75 0.90]]
				}

				e = (-2.98, 1.49).Unit()
			*/

			double aStart = MAX_FLOAT64;
			double aEnd = -MAX_FLOAT64;
			double bStart = MAX_FLOAT64;
			double bEnd = -MAX_FLOAT64;
			for (int i = 0; i < a.Points.Cnt; i++) {
				Vector? p = a.GetPointByOffset(i);
				if (null == p) {
					throw new ArgumentNullException("Getting a null point from polygon a!");
				}
				double dot = (p.X+a.X)*axisX + (p.Y+a.Y)*axisY;

				if (aStart > dot) {
					aStart = dot;
				}

				if (aEnd < dot) {
					aEnd = dot;
				}
			}

			for (int i = 0; i < b.Points.Cnt; i++) {
				Vector? p = b.GetPointByOffset(i);
				if (null == p) {
					throw new ArgumentNullException("Getting a null point from polygon b!");
				}
				double dot = (p.X+b.X)*axisX + (p.Y+b.Y)*axisY;

				if (bStart > dot) {
					bStart = dot;
				}

				if (bEnd < dot) {
					bEnd = dot;
				}
			}

			if (aStart > bEnd || aEnd < bStart) {
				// Separated by unit vector (axisX, axisY)
				return true;
			}

			double overlapProjected = 0;

			if (aStart < bStart) {
				result.AContainedInB = false;

				if (aEnd < bEnd) {
					overlapProjected = aEnd - bStart;
					result.BContainedInA = false;
				} else {
					double option1 = aEnd - bStart;
					double option2 = bEnd - aStart;
					if (option1 < option2) {
						overlapProjected = option1;
					} else {
						overlapProjected = -option2;
					}
				}
			} else {
				result.BContainedInA = false;

				if (aEnd > bEnd) {
					overlapProjected = aStart - bEnd;
					result.AContainedInB = false;
				} else {
					double option1 = aEnd - bStart;
					double option2 = bEnd - aStart;
					if (option1 < option2) {
						overlapProjected = option1;
					} else {
						overlapProjected = -option2;
					}
				}
			}

			double currentOverlap = result.OverlapMag;
			double absoluteOverlap = overlapProjected;
			if (overlapProjected < 0) {
				absoluteOverlap = -overlapProjected;
			}

			if ((0 == result.AxisX && 0 == result.AxisY) || (currentOverlap > absoluteOverlap)) {
				double sign = 1;
				if (overlapProjected < 0) {
					sign = -1;
				}

				result.OverlapMag = absoluteOverlap;
				result.OverlapX = axisX * sign;
				result.OverlapY = axisY * sign;
			}

			result.AxisX = axisX;
			result.AxisY = axisY;

			// the specified unit vector (axisX, axisY) doesn't separate "a" and "b", overlap result is generated
			return false;
		}

		public static (int, int) WorldToVirtualGridPos(double wx, double wy) {
			// [WARNING] Introduces loss of precision!
			// In JavaScript floating numbers suffer from seemingly non-deterministic arithmetics, and even if certain libs solved this issue by approaches such as fixed-point-number, they might not be used in other libs -- e.g. the "collision libs" we're interested in -- thus couldn't kill all pains.
			int vx = (int)(Math.Round(wx * WORLD_TO_VIRTUAL_GRID_RATIO));
			int vy = (int)(Math.Round(wy * WORLD_TO_VIRTUAL_GRID_RATIO));
			return (vx, vy);
		}

		public static (double, double) VirtualGridToWorldPos(int vx, int vy) {
			// No loss of precision
			double wx = (double)(vx) * VIRTUAL_GRID_TO_WORLD_RATIO;
			double wy = (double)(vy) * VIRTUAL_GRID_TO_WORLD_RATIO;
			return (wx, wy);
		}

		public static (double, double) WorldToPolygonColliderBLPos(double wx, double wy, double halfBoundingW, double halfBoundingH, double topPadding, double bottomPadding, double leftPadding, double rightPadding, double collisionSpaceOffsetX, double collisionSpaceOffsetY) {
			return (wx - halfBoundingW - leftPadding + collisionSpaceOffsetX, wy - halfBoundingH - bottomPadding + collisionSpaceOffsetY);
		}

		public static (double, double) PolygonColliderBLToWorldPos(double cx, double cy, double halfBoundingW, double halfBoundingH, double topPadding, double bottomPadding, double leftPadding, double rightPadding, double collisionSpaceOffsetX, double collisionSpaceOffsetY) {
			return (cx + halfBoundingW + leftPadding - collisionSpaceOffsetX, cy + halfBoundingH + bottomPadding - collisionSpaceOffsetY);
		}

		public static (int, int) PolygonColliderBLToVirtualGridPos(double cx, double cy, double halfBoundingW, double halfBoundingH, double topPadding, double bottomPadding, double leftPadding, double rightPadding, double collisionSpaceOffsetX, double collisionSpaceOffsetY) {
			var (wx, wy) = PolygonColliderBLToWorldPos(cx, cy, halfBoundingW, halfBoundingH, topPadding, bottomPadding, leftPadding, rightPadding, collisionSpaceOffsetX, collisionSpaceOffsetY);
			return WorldToVirtualGridPos(wx, wy);
		}

		public static (double, double) VirtualGridToPolygonColliderBLPos(int vx, int vy, double halfBoundingW, double halfBoundingH, double topPadding, double bottomPadding, double leftPadding, double rightPadding, double collisionSpaceOffsetX, double collisionSpaceOffsetY) {
			var (wx, wy) = VirtualGridToWorldPos(vx, vy);
			return WorldToPolygonColliderBLPos(wx, wy, halfBoundingW, halfBoundingH, topPadding, bottomPadding, leftPadding, rightPadding, collisionSpaceOffsetX, collisionSpaceOffsetY);
		}
    }
}
