using shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using pbc = global::Google.Protobuf.Collections;

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

        public static bool DecodeInput(ulong encodedInput, InputFrameDecoded holder) {
            int encodedDirection = (int)(encodedInput & 15);
            int btnALevel = (int)((encodedInput >> 4) & 1);
            int btnBLevel = (int)((encodedInput >> 5) & 1);

            holder.Dx = DIRECTION_DECODER[encodedDirection, 0];
            holder.Dy = DIRECTION_DECODER[encodedDirection, 1];
            holder.BtnALevel = btnALevel;
            holder.BtnBLevel = btnBLevel;
            return true;
        }

        public static (bool, double, double) calcPushbacks(double oldDx, double oldDy, ConvexPolygon a, ConvexPolygon b, ref SatResult overlapResult) {
            double origX = a.X, origY = a.Y;
            try {
                a.SetPosition(origX + oldDx, origY + oldDy);
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
                }
                else {
                    return (false, 0, 0);
                }
            }
            finally {
                a.SetPosition(origX, origY);
            }
        }

        public static int calcHardPushbacksNorms(int joinIndex, PlayerDownsync currPlayerDownsync, PlayerDownsync thatPlayerInNextFrame, Collider playerCollider, ConvexPolygon playerShape, double snapIntoPlatformOverlap, Vector effPushback, Vector[] hardPushbackNorms, Collision collision, ref SatResult overlapResult) {
            double virtualGripToWall = 0.0;
            if (ATK_CHARACTER_STATE_ONWALL == currPlayerDownsync.CharacterState && 0 == thatPlayerInNextFrame.VelX && currPlayerDownsync.DirX == thatPlayerInNextFrame.DirX) {
                double xfac = 1.0;
                if (0 > thatPlayerInNextFrame.DirX) {
                    xfac = -xfac;
                }
                virtualGripToWall = xfac * (double)(currPlayerDownsync.Speed) * VIRTUAL_GRID_TO_WORLD_RATIO;
            }
            int retCnt = 0;
            bool collided = playerCollider.CheckAllWithHolder(virtualGripToWall, 0, collision);
            if (!collided) {
                return retCnt;
            }

            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                bool isBarrier = false;

                switch (bCollider.Data) {
                    case PlayerDownsync v1:
                    case MeleeBullet v2:
                    case FireballBullet v3:
                        break;
                    default:
                        // By default it's a regular barrier, even if data is nil, note that Golang syntax of switch-case is kind of confusing, this "default" condition is met only if "!*PlayerDownsync && !*MeleeBullet && !*FireballBullet".
                        isBarrier = true;
                        break;
                }

                if (!isBarrier) {
                    continue;
                }
                ConvexPolygon bShape = bCollider.Shape;

                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, playerShape, bShape, ref overlapResult);

                if (!overlapped) {
                    continue;
                }
                // ALWAY snap into hardPushbacks!
                // [OverlapX, OverlapY] is the unit vector that points into the platform
                pushbackX = (overlapResult.OverlapMag - snapIntoPlatformOverlap) * overlapResult.OverlapX;
                pushbackY = (overlapResult.OverlapMag - snapIntoPlatformOverlap) * overlapResult.OverlapY;

                hardPushbackNorms[retCnt].X = overlapResult.OverlapX;
                hardPushbackNorms[retCnt].Y = overlapResult.OverlapY;

                effPushback.X += pushbackX;
                effPushback.Y += pushbackY;
                retCnt++;
            }
            return retCnt;
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
                double dot = (p.X + a.X) * axisX + (p.Y + a.Y) * axisY;

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
                double dot = (p.X + b.X) * axisX + (p.Y + b.Y) * axisY;

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
                }
                else {
                    double option1 = aEnd - bStart;
                    double option2 = bEnd - aStart;
                    if (option1 < option2) {
                        overlapProjected = option1;
                    }
                    else {
                        overlapProjected = -option2;
                    }
                }
            }
            else {
                result.BContainedInA = false;

                if (aEnd > bEnd) {
                    overlapProjected = aStart - bEnd;
                    result.AContainedInB = false;
                }
                else {
                    double option1 = aEnd - bStart;
                    double option2 = bEnd - aStart;
                    if (option1 < option2) {
                        overlapProjected = option1;
                    }
                    else {
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

        public static bool UpdateInputFrameInPlaceUponDynamics(int inputFrameId, int roomCapacity, ulong confirmedList, Google.Protobuf.Collections.RepeatedField<ulong> inputList, int[] lastIndividuallyConfirmedInputFrameId, ulong[] lastIndividuallyConfirmedInputList, int toExcludeJoinIndex) {
            bool hasInputFrameUpdatedOnDynamics = false;
            for (int i = 0; i < roomCapacity; i++) {
                if ((i + 1) == toExcludeJoinIndex) {
                    // On frontend, a "self input" is only confirmed by websocket downsync, which is quite late and might get the "self input" incorrectly overwritten if not excluded here
                    continue;
                }
                ulong joinMask = ((ulong)1 << i);
                if (0 < (confirmedList & joinMask)) {
                    // This in-place update is only valid when "delayed input for this player is not yet confirmed"
                    continue;
                }
                if (lastIndividuallyConfirmedInputFrameId[i] >= inputFrameId) {
                    continue;
                }
                ulong newVal = (lastIndividuallyConfirmedInputList[i] & 15);
                if (newVal != inputList[i]) {
                    inputList[i] = newVal;
                    hasInputFrameUpdatedOnDynamics = true;
                }
            }
            return hasInputFrameUpdatedOnDynamics;
        }

        private static (int, bool, int, int) deriveOpPattern(PlayerDownsync currPlayerDownsync, PlayerDownsync thatPlayerInNextFrame, RoomDownsyncFrame currRenderFrame, FrameRingBuffer<InputFrameDownsync> inputBuffer, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
            // returns (patternId, jumpedOrNot, effectiveDx, effectiveDy)
            int delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrame.Id);
            int delayedInputFrameIdForPrevRdf = ConvertToDelayedInputFrameId(currRenderFrame.Id - 1);

            if (0 >= delayedInputFrameId) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            if (noOpSet.Contains(currPlayerDownsync.CharacterState)) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            if (!ok || null == delayedInputFrameDownsync) {
                throw new ArgumentNullException(String.Format("InputFrameDownsync for delayedInputFrameId={0} is null!", delayedInputFrameId));
            }
            var delayedInputList = delayedInputFrameDownsync.InputList;

            pbc::RepeatedField<ulong>? delayedInputListForPrevRdf = null;
            if (0 < delayedInputFrameIdForPrevRdf) {
                var (_, delayedInputFrameDownsyncForPrevRdf) = inputBuffer.GetByFrameId(delayedInputFrameIdForPrevRdf);
                if (null != delayedInputFrameDownsyncForPrevRdf) {
                    delayedInputListForPrevRdf = delayedInputFrameDownsyncForPrevRdf.InputList;
                }
            }

            bool jumpedOrNot = false;
            int joinIndex = currPlayerDownsync.JoinIndex;

            DecodeInput(delayedInputList[joinIndex - 1], decodedInputHolder);

            int effDx = 0, effDy = 0;

            if (null != delayedInputListForPrevRdf) {
                DecodeInput(delayedInputListForPrevRdf[joinIndex - 1], prevDecodedInputHolder);
            }

            // Jumping is partially allowed within "CapturedByInertia", but moving is only allowed when "0 == FramesToRecover" (constrained later in "ApplyInputFrameDownsyncDynamicsOnSingleRenderFrame")
            if (0 == currPlayerDownsync.FramesToRecover) {
                effDx = decodedInputHolder.Dx;
                effDy = decodedInputHolder.Dy;
            }

            int patternId = PATTERN_ID_NO_OP;
            return (patternId, jumpedOrNot, effDx, effDy);
        }


        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, double collisionSpaceOffsetX, double collisionSpaceOffsetY, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, bool[] jumpedOrNotList, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
            var (ok1, currRenderFrame) = renderBuffer.GetByFrameId(currRenderFrameId);
            if (!ok1 || null == currRenderFrame) {
                throw new ArgumentNullException(String.Format("Null currRenderFrame is not allowed in `Battle.Step` for currRenderFrameId={0}", currRenderFrameId));
            }
            int nextRenderFrameId = currRenderFrameId + 1;
            var (ok2, candidate) = renderBuffer.GetByFrameId(nextRenderFrameId);
			if (!ok2 || null == candidate) {
				if (nextRenderFrameId == renderBuffer.EdFrameId) {
					renderBuffer.DryPut();
					(_, candidate) = renderBuffer.GetByFrameId(nextRenderFrameId);
				} 
			}
			if (null == candidate) {
				throw new ArgumentNullException(String.Format("renderBuffer was not fully pre-allocated for nextRenderFrameId={0}!", nextRenderFrameId));
			}

            // [WARNING] On backend this function MUST BE called while "InputsBufferLock" is locked!
            var nextRenderFramePlayers = candidate.PlayersArr;
            // Make a copy first
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var src = currRenderFrame.PlayersArr[i];
                int framesToRecover = src.FramesToRecover - 1;
                int framesInChState = src.FramesInChState + 1;
                int framesInvinsible = src.FramesInvinsible - 1;
                if (framesToRecover < 0) {
                    framesToRecover = 0;
                }
                if (framesInvinsible < 0) {
                    framesInvinsible = 0;
                }
                ClonePlayerDownsync(src.Id, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.BattleState, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, src.ColliderRadius, true, false, src.OnWallNormX, src.OnWallNormY, src.CapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, nextRenderFramePlayers[i]);
            }

            // 1. Process player inputs
            var delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrame.Id);

            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];
                var (patternId, jumpedOrNot, effDx, effDy) = deriveOpPattern(currPlayerDownsync, thatPlayerInNextFrame, currRenderFrame, inputBuffer, decodedInputHolder, prevDecodedInputHolder);

                bool isWallJumping = false; // TODO
                jumpedOrNotList[i] = jumpedOrNot;
                int joinIndex = currPlayerDownsync.JoinIndex;

                if (0 == currPlayerDownsync.FramesToRecover) {
                    bool prevCapturedByInertia = currPlayerDownsync.CapturedByInertia;
                    bool alignedWithInertia = true;
                    bool exactTurningAround = false;
                    bool stoppingFromWalking = false;
                    if (0 != effDx && 0 == thatPlayerInNextFrame.VelX) {
                        alignedWithInertia = false;
                    }
                    else if (0 == effDx && 0 != thatPlayerInNextFrame.VelX) {
                        alignedWithInertia = false;
                        stoppingFromWalking = true;
                    }
                    else if (0 > effDx * thatPlayerInNextFrame.VelX) {
                        alignedWithInertia = false;
                        exactTurningAround = true;
                    }

                    if (!jumpedOrNot && !isWallJumping && !prevCapturedByInertia && !alignedWithInertia) {
                        /*
                           [WARNING] A "turn-around", or in more generic direction schema a "change in direction" is a hurdle for our current "prediction+rollback" approach, yet applying a "FramesToRecover" for "turn-around" can alleviate the graphical inconsistence to a huge extent! For better operational experience, this is intentionally NOT APPLIED TO WALL JUMPING!

                           When "false == alignedWithInertia", we're GUARANTEED TO BE WRONG AT INPUT PREDICTION ON THE FRONTEND, but we COULD STILL BE RIGHT AT POSITION PREDICTION WITHIN "InertiaFramesToRecover" -- which together with "INPUT_DELAY_FRAMES" grants the frontend a big chance to be graphically consistent even upon wrong prediction!
                        */

                        thatPlayerInNextFrame.CapturedByInertia = true;
                        if (exactTurningAround) {
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_TURNAROUND;
                            thatPlayerInNextFrame.FramesToRecover = 4; // TODO
                        }
                        else if (stoppingFromWalking) {
                            thatPlayerInNextFrame.FramesToRecover = 4;
                        }
                        else {
                            // Updates CharacterState and thus the animation to make user see graphical feedback asap.
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_WALKING;
                            thatPlayerInNextFrame.FramesToRecover = 2;
                        }
                    }
                    else {
                        thatPlayerInNextFrame.CapturedByInertia = false;
                        if (0 != effDx) {
                            int xfac = 1;
                            if (0 > effDx) {
                                xfac = -xfac;
                            }
                            thatPlayerInNextFrame.DirX = effDx;
                            thatPlayerInNextFrame.DirY = effDy;

                            if (isWallJumping) {
                                thatPlayerInNextFrame.VelX = xfac * Math.Abs(currPlayerDownsync.VelX);
                            }
                            else {
                                thatPlayerInNextFrame.VelX = xfac * currPlayerDownsync.Speed;
                            }
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_WALKING;
                        }
                        else {
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_IDLE1;
                            thatPlayerInNextFrame.VelX = 0;
                        }
                    }

                }
            }

            /*
                [WARNING]
               1. The dynamic colliders will all be removed from "Space" at the end of this function due to the need for being rollback-compatible.
               2. To achieve "zero gc" in "ApplyInputFrameDownsyncDynamicsOnSingleRenderFrame", I deliberately chose a collision system that doesn't use dynamic tree node alloc.
            */
            int colliderCnt = 0;

            // 2. Process player movement
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                int joinIndex = currPlayerDownsync.JoinIndex;
                effPushbacks[joinIndex - 1].X = 0;
                effPushbacks[joinIndex - 1].Y = 0;
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];

                // Reset playerCollider position from the "virtual grid position"
                int newVx = currPlayerDownsync.VirtualGridX + currPlayerDownsync.VelX, newVy = currPlayerDownsync.VirtualGridY + currPlayerDownsync.VelY;

                var (wx, wy) = VirtualGridToWorldPos(newVx, newVy);
                int colliderWidth = currPlayerDownsync.ColliderRadius * 2, colliderHeight = currPlayerDownsync.ColliderRadius * 4;

                switch (currPlayerDownsync.CharacterState) {
                    case ATK_CHARACTER_STATE_LAY_DOWN1:
                        colliderWidth = currPlayerDownsync.ColliderRadius * 4;
                        colliderHeight = currPlayerDownsync.ColliderRadius * 2;
                        break;
                    case ATK_CHARACTER_STATE_BLOWN_UP1:
                    case ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP:
                    case ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP:
                    case ATK_CHARACTER_STATE_ONWALL:
                        colliderWidth = currPlayerDownsync.ColliderRadius * 2;
                        colliderHeight = currPlayerDownsync.ColliderRadius * 2;
                        break;
                }

                var (colliderWorldWidth, colliderWorldHeight) = VirtualGridToWorldPos(colliderWidth, colliderHeight);

                Collider playerCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(playerCollider, wx, wy, colliderWorldWidth, colliderWorldHeight, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, collisionSpaceOffsetX, collisionSpaceOffsetY, currPlayerDownsync); // the coords of all barrier boundaries are multiples of tileWidth(i.e. 16), by adding snapping y-padding when "landedOnGravityPushback" all "playerCollider.Y" would be a multiple of 1.0
                colliderCnt++;

                // Add to collision system
                collisionSys.AddSingle(playerCollider);

                if (currPlayerDownsync.InAir) {
                    if (ATK_CHARACTER_STATE_ONWALL == currPlayerDownsync.CharacterState && !jumpedOrNotList[i]) {
                        thatPlayerInNextFrame.VelX += GRAVITY_X;
                        thatPlayerInNextFrame.VelY = 5; // TODO
                    }
                    else if (ATK_CHARACTER_STATE_DASHING == currPlayerDownsync.CharacterState) {
                        thatPlayerInNextFrame.VelX += GRAVITY_X;
                    }
                    else {
                        thatPlayerInNextFrame.VelX += GRAVITY_X;
                        thatPlayerInNextFrame.VelY += GRAVITY_Y;
                    }
                }
            }

            // 4. Calc pushbacks for each player (after its movement) w/o bullets
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                int joinIndex = currPlayerDownsync.JoinIndex;
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];
                int hardPushbackCnt = calcHardPushbacksNorms(joinIndex, currPlayerDownsync, thatPlayerInNextFrame, aCollider, aShape, SNAP_INTO_PLATFORM_OVERLAP, effPushbacks[joinIndex - 1], hardPushbackNormsArr[joinIndex - 1], collision, ref overlapResult);

                bool landedOnGravityPushback = false;
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
                if (collided) {
                    while (true) {
                        var (ok3, bCollider) = collision.PopFirstContactedCollider();
                        if (false == ok3 || null == bCollider) {
                            break;
                        }
                        bool isBarrier = false, isAnotherPlayer = false, isBullet = false;
                        switch (bCollider.Data) {
                            case PlayerDownsync v:
                                if (ATK_CHARACTER_STATE_DYING == v.CharacterState) {
                                    // ignore collision with dying player
                                    continue;
                                }
                                isAnotherPlayer = true;
                                break;
                            case MeleeBullet v1:
                            case FireballBullet v2:
                                isBullet = true;
                                break;
                            default:
                                // By default it's a regular barrier, even if data is nil
                                isBarrier = true;
                                break;
                        }
                        if (isBullet) {
                            // ignore bullets for this step
                            continue;
                        }

                        ConvexPolygon bShape = bCollider.Shape;
                        var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, ref overlapResult);
                        if (!overlapped) {
                            continue;
                        }
                        var normAlignmentWithGravity = (overlapResult.OverlapX * 0 + overlapResult.OverlapY * (-1.0));
                        if (isAnotherPlayer) {
                            // [WARNING] The "zero overlap collision" might be randomly detected/missed on either frontend or backend, to have deterministic result we added paddings to all sides of a playerCollider. As each velocity component of (velX, velY) being a multiple of 0.5 at any renderFrame, each position component of (x, y) can only be a multiple of 0.5 too, thus whenever a 1-dimensional collision happens between players from [player#1: i*0.5, player#2: j*0.5, not collided yet] to [player#1: (i+k)*0.5, player#2: j*0.5, collided], the overlap becomes (i+k-j)*0.5+2*s, and after snapping subtraction the effPushback magnitude for each player is (i+k-j)*0.5, resulting in 0.5-multiples-position for the next renderFrame.
                            pushbackX = (overlapResult.OverlapMag - SNAP_INTO_PLATFORM_OVERLAP * 2) * overlapResult.OverlapX;
                            pushbackY = (overlapResult.OverlapMag - SNAP_INTO_PLATFORM_OVERLAP * 2) * overlapResult.OverlapY;
                        }
                        for (int k = 0; k < hardPushbackCnt; k++) {
                            Vector hardPushbackNorm = hardPushbackNormsArr[joinIndex - 1][k];
                            double projectedMagnitude = pushbackX * hardPushbackNorm.X + pushbackY * hardPushbackNorm.Y;
                            if (isBarrier || (isAnotherPlayer && 0 > projectedMagnitude)) {
                                pushbackX -= projectedMagnitude * hardPushbackNorm.X;
                                pushbackY -= projectedMagnitude * hardPushbackNorm.Y;
                            }
                        }
                        effPushbacks[joinIndex - 1].X += pushbackX;
                        effPushbacks[joinIndex - 1].Y += pushbackY;

                        if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                            landedOnGravityPushback = true;
                        }
                    }
                }

                if (landedOnGravityPushback) {
                    thatPlayerInNextFrame.InAir = false;
                    bool fallStopping = (currPlayerDownsync.InAir && 0 >= currPlayerDownsync.VelY);
                    if (fallStopping) {
                        thatPlayerInNextFrame.VelY = 0;
                        thatPlayerInNextFrame.VelX = 0;
                        if (ATK_CHARACTER_STATE_DYING == thatPlayerInNextFrame.CharacterState) {
                            // No update needed for Dying
                        }
                        else if (ATK_CHARACTER_STATE_BLOWN_UP1 == thatPlayerInNextFrame.CharacterState) {
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_LAY_DOWN1;
                            thatPlayerInNextFrame.FramesToRecover = 18; // TODO
                        }
                        else {
                            switch (currPlayerDownsync.CharacterState) {
                                case ATK_CHARACTER_STATE_BLOWN_UP1:
                                case ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP:
                                case ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP:
                                case ATK_CHARACTER_STATE_ONWALL:
                                    // [WARNING] To prevent bouncing due to abrupt change of collider shape, it's important that we check "currPlayerDownsync" instead of "thatPlayerInNextFrame" here!
                                    var halfColliderWidthDiff = 0;
                                    var halfColliderHeightDiff = currPlayerDownsync.ColliderRadius;
                                    var (_, halfColliderWorldHeightDiff) = VirtualGridToWorldPos(halfColliderWidthDiff, halfColliderHeightDiff);
                                    effPushbacks[joinIndex - 1].Y -= halfColliderWorldHeightDiff;
                                    break;
                            }
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_IDLE1;
                            thatPlayerInNextFrame.FramesToRecover = 0;
                        }
                    }
                    else {
                        // landedOnGravityPushback not fallStopping, could be in LayDown or GetUp or Dying
                        if (nonAttackingSet.Contains(thatPlayerInNextFrame.CharacterState)) {
                            if (ATK_CHARACTER_STATE_DYING == thatPlayerInNextFrame.CharacterState) {
                                thatPlayerInNextFrame.VelY = 0;
                                thatPlayerInNextFrame.VelX = 0;
                            }
                            else if (ATK_CHARACTER_STATE_LAY_DOWN1 == thatPlayerInNextFrame.CharacterState) {
                                if (0 == thatPlayerInNextFrame.FramesToRecover) {
                                    thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_GET_UP1;
                                    thatPlayerInNextFrame.FramesToRecover = 10; // TODO
                                }
                            }
                            else if (ATK_CHARACTER_STATE_GET_UP1 == thatPlayerInNextFrame.CharacterState) {
                                if (0 == thatPlayerInNextFrame.FramesToRecover) {
                                    thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_IDLE1;
                                    thatPlayerInNextFrame.FramesInvinsible = 4; // TODO
                                }
                            }
                        }
                    }
                }
            }

            // 6. Get players out of stuck barriers if there's any
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                int joinIndex = currPlayerDownsync.JoinIndex;
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                // Update "virtual grid position"
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];
                (thatPlayerInNextFrame.VirtualGridX, thatPlayerInNextFrame.VirtualGridY) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[joinIndex - 1].X, aCollider.Y - effPushbacks[joinIndex - 1].Y, aCollider.W * 0.5, aCollider.H * 0.5, 0, 0, 0, 0, collisionSpaceOffsetX, collisionSpaceOffsetY);

                // Update "CharacterState"
                if (thatPlayerInNextFrame.InAir) {
                    int oldNextCharacterState = thatPlayerInNextFrame.CharacterState;
                    switch (oldNextCharacterState) {
                        case ATK_CHARACTER_STATE_IDLE1:
                        case ATK_CHARACTER_STATE_WALKING:
                        case ATK_CHARACTER_STATE_TURNAROUND:
                            if (jumpedOrNotList[i] || ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP == currPlayerDownsync.CharacterState) {
                                thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP;
                            }
                            else {
                                thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP;
                            }
                            break;
                        case ATK_CHARACTER_STATE_ATK1:
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_INAIR_ATK1;
                            // No inAir transition for ATK2/ATK3 for now
                            break;
                        case ATK_CHARACTER_STATE_ATKED1:
                            thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_INAIR_ATKED1;
                            break;
                    }
                }

                if (thatPlayerInNextFrame.OnWall) {
                    switch (thatPlayerInNextFrame.CharacterState) {
                        case ATK_CHARACTER_STATE_WALKING:
                        case ATK_CHARACTER_STATE_INAIR_IDLE1_BY_JUMP:
                        case ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP:
                            bool hasBeenOnWallChState = (ATK_CHARACTER_STATE_ONWALL == currPlayerDownsync.CharacterState);
                            bool hasBeenOnWallCollisionResultForSameChState = (currPlayerDownsync.OnWall && MAGIC_FRAMES_TO_BE_ONWALL <= thatPlayerInNextFrame.FramesInChState);
                            if (hasBeenOnWallChState || hasBeenOnWallCollisionResultForSameChState) {
                                thatPlayerInNextFrame.CharacterState = ATK_CHARACTER_STATE_ONWALL;
                            }
                            break;
                    }
                }

                // Reset "FramesInChState" if "CharacterState" is changed
                if (thatPlayerInNextFrame.CharacterState != currPlayerDownsync.CharacterState) {
                    thatPlayerInNextFrame.FramesInChState = 0;
                }

                // Remove any active skill if not attacking
                if (nonAttackingSet.Contains(thatPlayerInNextFrame.CharacterState)) {
                    thatPlayerInNextFrame.ActiveSkillId = NO_SKILL;
                    thatPlayerInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                }
            }

            for (int i = 0; i < colliderCnt; i++) {
                Collider dynamicCollider = dynamicRectangleColliders[i];
                if (null == dynamicCollider.Space) {
                    throw new ArgumentNullException("Null dynamicCollider.Space is not allowed in `Step`!");
                }
                dynamicCollider.Space.RemoveSingle(dynamicCollider);
            }

            candidate.Id = nextRenderFrameId;
        }
        
        public static void AlignPolygon2DToBoundingBox(ConvexPolygon input) {
            // Transform again to put "anchor" at the "bottom-left point (w.r.t. world space)" of the bounding box for "resolv"
            double boundingBoxBLX = MAX_FLOAT64, boundingBoxBLY = MAX_FLOAT64;
            for (int i = 0; i < input.Points.Cnt; i++) {
                var (exists, p) = input.Points.GetByOffset(i);
                if (!exists || null == p) throw new ArgumentNullException("Unexpected null point in ConvexPolygon when calling `AlignPolygon2DToBoundingBox`#1!");

                boundingBoxBLX = Math.Min(p.X, boundingBoxBLX);
                boundingBoxBLY = Math.Min(p.Y, boundingBoxBLY);
            }

            // Now "input.Anchor" should move to "input.Anchor+boundingBoxBL", thus "boundingBoxBL" is also the value of the negative diff for all "input.Points"
            input.X += boundingBoxBLX;
            input.Y += boundingBoxBLY;
            for (int i = 0; i < input.Points.Cnt; i++) {
                var (exists, p) = input.Points.GetByOffset(i);
                if (!exists || null == p) throw new ArgumentNullException("Unexpected null point in ConvexPolygon when calling `AlignPolygon2DToBoundingBox`#2!");
                p.X -= boundingBoxBLX;
                p.Y -= boundingBoxBLY;
                boundingBoxBLX = Math.Min(p.X, boundingBoxBLX);
                boundingBoxBLY = Math.Min(p.Y, boundingBoxBLY);
            }
        }

        public static Collider GenerateConvexPolygonCollider(ConvexPolygon srcPolygon, double spaceOffsetX, double spaceOffsetY, object data) {
            if (null == srcPolygon) throw new ArgumentNullException("Null srcPolygon is not allowed in `GenerateConvexPolygonCollider`");
            AlignPolygon2DToBoundingBox(srcPolygon);
            double w = 0, h = 0;

            for (int i = 0; i < srcPolygon.Points.Cnt; i++) {
                for (int j = 0; j < srcPolygon.Points.Cnt; j++) {
                    if (i == j) {
                        continue;
                    }
                    Vector? pi = srcPolygon.GetPointByOffset(i);
                    if (null == pi) {
                        throw new ArgumentNullException("Null pi is not allowed in `GenerateConvexPolygonCollider`!");
                    }
                    Vector? pj = srcPolygon.GetPointByOffset(j);
                    if (null == pj) {
                        throw new ArgumentNullException("Null pj is not allowed in `GenerateConvexPolygonCollider`!");
                    }
                    w = Math.Max(w, Math.Abs(pj.X - pi.X));
                    h = Math.Max(h, Math.Abs(pj.Y - pi.Y));
                }
            }

            return new Collider(srcPolygon.X + spaceOffsetX, srcPolygon.Y + spaceOffsetY, w, h, srcPolygon, data);
        }

        public static Collider GenerateRectCollider(double wx, double wy, double w, double h, double topPadding, double bottomPadding, double leftPadding, double rightPadding, double spaceOffsetX, double spaceOffsetY, object data) {
            // [WARNING] (spaceOffsetX, spaceOffsetY) are taken into consideration while calling "GenerateConvexPolygonCollider" -- because "GenerateConvexPolygonCollider" might also be called for "polylines extracted from Tiled", it's more convenient to organized the codes this way.
            var (blX, blY) = WorldToPolygonColliderBLPos(wx, wy, w * 0.5, h * 0.5, topPadding, bottomPadding, leftPadding, rightPadding, 0, 0); 
            double effW = leftPadding + w + rightPadding, effH = bottomPadding + h + topPadding;
            var srcPolygon = new ConvexPolygon(blX, blY, new double[] { 
                0, 0,
                0 + effW, 0, 
                0 + effW, 0 + effH, 
                0, 0 + effH 
            });

            return GenerateConvexPolygonCollider(srcPolygon, spaceOffsetX, spaceOffsetY, data);
        }

    public static void UpdateRectCollider(Collider collider, double wx, double wy, double w, double h, double topPadding, double bottomPadding, double leftPadding, double rightPadding, double spaceOffsetX, double spaceOffsetY, object data) {
            var (blX, blY) = WorldToPolygonColliderBLPos(wx, wy, w * 0.5, h * 0.5, topPadding, bottomPadding, leftPadding, rightPadding, spaceOffsetX, spaceOffsetY);

            double effW = leftPadding + w + rightPadding;
            double effH = bottomPadding + h + topPadding;
            (collider.X, collider.Y, collider.W, collider.H) = (blX, blY, effW, effH);
            collider.Shape.UpdateAsRectangle(0, 0, effW, effH);

            collider.Data = data;
        }

        public static void ClonePlayerDownsync(int id, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int velY, int framesToRecover, int framesInChState, int activeSkillId, int activeSkillHit, int framesInvinsible, int speed, int battleState, int characterState, int joinIndex, int hp, int maxHp, int colliderRadius, bool inAir, bool onWall, int onWallNormX, int onWallNormY, bool capturedByInertia, int bulletTeamId, int chCollisionTeamId, int revivalVirtualGridX, int revivalVirtualGridY, PlayerDownsync dst) {
            dst.Id = id;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;
            dst.FramesToRecover = framesToRecover;
            dst.FramesInChState = framesInChState;
            dst.ActiveSkillId = activeSkillId;
            dst.ActiveSkillHit = activeSkillHit;
            dst.FramesInvinsible = framesInvinsible;
            dst.Speed = speed;
            dst.BattleState = battleState;
            dst.CharacterState = characterState;
            dst.JoinIndex = joinIndex;
            dst.Hp = hp;
            dst.MaxHp = maxHp;
            dst.ColliderRadius = colliderRadius;
            dst.InAir = inAir;
            dst.OnWall = onWall;
            dst.OnWallNormX = onWallNormX;
            dst.OnWallNormY = onWallNormY;
            dst.CapturedByInertia = capturedByInertia;
            dst.BulletTeamId = bulletTeamId;
            dst.ChCollisionTeamId = chCollisionTeamId;
            dst.RevivalVirtualGridX = revivalVirtualGridX;
            dst.RevivalVirtualGridY = revivalVirtualGridY;
        }

        public static RoomDownsyncFrame NewPreallocatedRoomDownsyncFrame(int roomCapacity, int preallocMeleeBulletCount, int preallocFireballBulletCount) {
            var ret = new RoomDownsyncFrame();
            ret.Id = TERMINATING_RENDER_FRAME_ID;
            ret.BulletLocalIdCounter = TERMINATING_BULLET_LOCAL_ID;

            for (int i = 0; i < roomCapacity; i++) {
                var single = new PlayerDownsync();
                single.Id = TERMINATING_PLAYER_ID;
                ret.PlayersArr.Add(single);
            }

	        for (int i = 0; i < preallocMeleeBulletCount; i++) {
                var single = new MeleeBullet();
                single.BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
                ret.MeleeBullets.Add(single);
            }

            for (int i = 0; i < preallocFireballBulletCount; i++) {
                var single = new FireballBullet();
                single.BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
                ret.FireballBullets.Add(single);
            }

            return ret;
        }

        public static InputFrameDownsync NewPreallocatedInputFrameDownsync(int roomCapacity) {
            var ret = new InputFrameDownsync();
            ret.InputFrameId = TERMINATING_INPUT_FRAME_ID;
            ret.ConfirmedList = 0;
            for (int i = 0; i < roomCapacity; i++) {
                ret.InputList.Add(0);
            }

            return ret;
        }
		
		public static (double, double) tiledLayerPositionToCollisionSpacePosition(double tiledLayerX, double tiledLayerY, double spaceOffsetX, double spaceOffsetY) {
			return (-spaceOffsetX + tiledLayerX, +spaceOffsetY - tiledLayerY); 	
		}
		
		public static (double, double) CollisionSpacePositionToWorldPosition(double collisionSpaceX, double collisionSpaceY, double spaceOffsetX, double spaceOffsetY) {
			// [WARNING] This conversion is specifically added for Unity+SuperTiled2Unity
			return (collisionSpaceX+spaceOffsetX, collisionSpaceY-spaceOffsetY); 	
		}
		
    }
}
