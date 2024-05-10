using System;
using System.Threading;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        private const float magicLeanLowerBound = 0.1f;
        private const float magicLeanUpperBound = 0.9f;

        private static SatResult tmpResultHolder = new SatResult {
            OverlapMag = 0,
            OverlapX = 0,
            OverlapY = 0,
            AContainedInB = false,
            BContainedInA = false,
            AxisX = 0,
            AxisY = 0    
        };

        public static void roundToRectilinearDir(ref float normX, ref float normY) {
            if (0 == normX) {
                if (0 > normY) {
                    normY = -1f;
                } else {
                    normY = 1f;
                }
            } else if (0 == normY) {
                if (0 > normX) {
                    normX = -1f;
                } else {
                    normX = 1f;
                }
            }
        }

        public static (bool, float, float) calcPushbacks(float oldDx, float oldDy, ConvexPolygon a, ConvexPolygon b, bool onlyOnBShapeEdges, bool prefersAOnBShapeTopEdges, ref SatResult overlapResult) {
            float origX = a.X, origY = a.Y;
            try {
                a.SetPosition(origX + oldDx, origY + oldDy);
                overlapResult.OverlapMag = 0;
                overlapResult.OverlapX = 0;
                overlapResult.OverlapY = 0;
                overlapResult.AContainedInB = true;
                overlapResult.BContainedInA = true;
                overlapResult.AxisX = 0;
                overlapResult.AxisY = 0;

                bool overlapped = isPolygonPairOverlapped(a, b, onlyOnBShapeEdges, prefersAOnBShapeTopEdges, ref overlapResult);
                if (true == overlapped) {
                    float pushbackX = overlapResult.OverlapMag * overlapResult.OverlapX;
                    float pushbackY = overlapResult.OverlapMag * overlapResult.OverlapY;
                    return (true, pushbackX, pushbackY);
                } else {
                    return (false, 0, 0);
                }
            } finally {
                a.SetPosition(origX, origY);
            }
        }

        public static int calcHardPushbacksNormsForCharacter(RoomDownsyncFrame currRenderFrame, CharacterConfig chConfig, CharacterDownsync currCharacterDownsync, CharacterDownsync thatPlayerInNextFrame, Collider aCollider, ConvexPolygon aShape, Vector[] hardPushbacks, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, out int primaryOverlapIndex, out Trap? primaryTrap, FrameRingBuffer<Collider> residueCollided, ILoggerBridge logger) {
            primaryTrap = null;
            float virtualGripToWall = 0.0f;
            if (OnWallIdle1 == currCharacterDownsync.CharacterState && 0 == thatPlayerInNextFrame.VelX && currCharacterDownsync.DirX == thatPlayerInNextFrame.DirX) {
                float xfac = 1.0f;
                if (0 > thatPlayerInNextFrame.DirX) {
                    xfac = -xfac;
                }
                virtualGripToWall = xfac * currCharacterDownsync.Speed * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO;
            }
            int retCnt = 0;
            primaryOverlapIndex = -1;
            float primaryOverlapMag = float.MinValue;
            bool primaryIsWall = true; // Initialized to "true" to be updated even if there's only 1 vertical wall 
            residueCollided.Clear();
            bool collided = aCollider.CheckAllWithHolder(virtualGripToWall, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) {
				//logger.LogInfo(String.Format("No collision object."));
                return retCnt;
            }
 
            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                int trapLocalId = TERMINATING_TRAP_ID;
                bool isBarrier = false;
                bool onTrap = false;
                bool providesSlipJump = false;
                bool forcesCrouching = false;
                switch (bCollider.Data) {
                    case CharacterDownsync v0:
                    case Bullet v1:
                    case PatrolCue v2:
                        break;
                    case Pickable v3:
                        //logger.LogInfo(String.Format("Character encountered a pickable v3 = {0}", v3));
                        break;
                    case TrapColliderAttr v4:
                        trapLocalId = v4.TrapLocalId;
                        providesSlipJump = v4.ProvidesSlipJump;
                        forcesCrouching = v4.ForcesCrouching;
                        var trap = currRenderFrame.TrapsArr[trapLocalId];
                        onTrap = (v4.ProvidesHardPushback && TrapState.Tdestroyed != trap.TrapState);
                        isBarrier = onTrap;
                        break;
                    case TriggerColliderAttr v5:
                        break;
                    default:
                        // By default it's a regular barrier, even if data is nil, note that Golang syntax of switch-case is kind of confusing, this "default" condition is met only if "!*CharacterDownsync && !*Bullet".
                        isBarrier = true;
                        break;
                }

                if (!isBarrier && !forcesCrouching) {
                    if (residueCollided.Cnt >= residueCollided.N) {
                        throw new ArgumentException(String.Format("residueCollided is already full! residueCollided.Cnt={0}, residueCollided.N={1}: trying to insert collider.Shape={4}, collider.Data={5}", residueCollided.Cnt, residueCollided.N, bCollider.Shape, bCollider.Data));
                    }
                    residueCollided.Put(bCollider);
                    continue;
                }

                ConvexPolygon bShape = bCollider.Shape;

                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, true, true, ref overlapResult);

                if (!overlapped) {
                    continue;
                }

                if (overlapResult.OverlapMag < CLAMPABLE_COLLISION_SPACE_MAG) {
                    /*
                    [WARNING] 

                    Kindly note that if I clamped by a larger threshold here, e.g. "overlapResult.OverlapMag < VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO", it would cause unintended bouncing between "DefaultSizeY & ShrinkedSizeY" on slopes!

                    Moreover if I didn't clamp "pushbackX & pushbackY" here, there could be disagreed shape overlapping between backend and frontend, see comments around "shapeOverlappedOtherChCnt" in "Battle_dynamics". 
                    */
                    continue;
                }

                if (forcesCrouching && chConfig.CrouchingEnabled) {
                    // [WARNING] If "forcesCrouching" but "false == chConfig.CrouchingEnabled", then the current "bCollider" should be deemed as a regular barrier!
                    float characterTop = aCollider.Y + aCollider.H;
                    float barrierTop = bCollider.Y + bCollider.H;
                    if (characterTop < barrierTop) {
                        thatPlayerInNextFrame.ForcedCrouching = true;
                    }
                    continue;
                }

                if (providesSlipJump) {
                    /*
                    Only provides hardPushbacks when 
                    - the character is not uprising, and
                    - the "bottom of the character" is higher than "top of the barrier rectangle - SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE". 
                    */
                    if (0 < currCharacterDownsync.VelY) {
                        continue;
                    }
                    float characterBottom = aCollider.Y;
                    float barrierTop = bCollider.Y + bCollider.H;
                    if (characterBottom < (barrierTop - SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE)) {
                        continue;
                    }
                }

                float normAlignmentWithHorizon1 = (overlapResult.OverlapX * +1f);
                float normAlignmentWithHorizon2 = (overlapResult.OverlapX * -1f);
                bool isWall = (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1 || VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2);
                // [WARNING] At a corner with 1 vertical edge and 1 horizontal edge, make sure that the HORIZONTAL edge is chosen as primary!
                if (!isWall && primaryIsWall) {
                    // Initial non-wall transition
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                    primaryIsWall = isWall;
                    if (onTrap) {
                        primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                    } else {
                        primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                    }
                } else if (isWall && !primaryIsWall) {
                    // Just skip, once the character is checked to collide with a non-wall, any parasitic wall collision would be ignored...
                } else {
                    // Same polarity
                    if (overlapResult.OverlapMag > primaryOverlapMag) {
                        primaryOverlapIndex = retCnt;
                        primaryOverlapMag = overlapResult.OverlapMag;
                        overlapResult.cloneInto(ref primaryOverlapResult);
                        primaryIsWall = isWall;
                        if (onTrap) {
                            primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                        } else {
                            primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                        }
                    } else if (overlapResult.OverlapMag == primaryOverlapMag) {
                        // [WARNING] Here's an important block for guaranteeing determinism regardless of traversal order.
                        if (onTrap && null == primaryTrap) {       
                            // If currently straddling across a trap and a non-trap, with equal overlapMap, then the trap takes higher priority!
                            primaryOverlapIndex = retCnt;
                            primaryOverlapMag = overlapResult.OverlapMag;
                            overlapResult.cloneInto(ref primaryOverlapResult);
                            primaryIsWall = isWall;
                            primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                        } else {
                            if ((overlapResult.AxisX < primaryOverlapResult.AxisX) || (overlapResult.AxisX == primaryOverlapResult.AxisX && overlapResult.AxisY < primaryOverlapResult.AxisY)) {
                                primaryOverlapIndex = retCnt;
                                primaryOverlapMag = overlapResult.OverlapMag;
                                overlapResult.cloneInto(ref primaryOverlapResult);
                                primaryIsWall = isWall;
                                if (onTrap) {
                                    primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                                } else {
                                    primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                                }
                            }
                        }
                    }
                }

                hardPushbacks[retCnt].X = pushbackX;
                hardPushbacks[retCnt].Y = pushbackY;

                retCnt++;
            }

            return retCnt;
        }

        public static int calcHardPushbacksNormsForBullet(RoomDownsyncFrame currRenderFrame, Bullet bullet, Collider aCollider, ConvexPolygon aShape, Vector[] hardPushbacks, FrameRingBuffer<Collider> residueCollided, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, out int primaryOverlapIndex, ILoggerBridge logger) {
            // [WARNING] There's no plan for a GroundWave bullet to take "FrictionVelX" from a moving trap, thus no need for using "primaryTrap".
            int retCnt = 0;
            primaryOverlapIndex = -1;
            float primaryOverlapMag = float.MinValue;
            bool primaryIsWall = false; // [WARNING] OPPOSITE preference w.r.t. "calcHardPushbacksNormsForCharacter" here! 
            float primaryNonWallTop = -MAX_FLOAT32, primaryWallTop = -MAX_FLOAT32;
            residueCollided.Clear();
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) {
                //logger.LogInfo(String.Format("No collision object."));
                return retCnt;
            }

            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                bool isAnotherHardPushbackTrap = false; 
                bool isAnActualBarrier = false;
                bool providesSlipJump = false;
                switch (bCollider.Data) {
                    case Pickable v0:
                    case CharacterDownsync v1:
                    case Bullet v2:
                    case PatrolCue v3:
                    case TriggerColliderAttr v4:
                        break;
                    case TrapColliderAttr v5:
                        var trap = currRenderFrame.TrapsArr[v5.TrapLocalId];
                        isAnotherHardPushbackTrap = (v5.ProvidesHardPushback && TrapState.Tdestroyed != trap.TrapState);
                        providesSlipJump = v5.ProvidesSlipJump;
                        break;
                    default:
                        // By default it's a regular barrier, even if data is nil, note that Golang syntax of switch-case is kind of confusing, this "default" condition is met only if "!*CharacterDownsync && !*Bullet".
                        isAnActualBarrier = true;
                        break;
                }

                if (!isAnotherHardPushbackTrap && !isAnActualBarrier) {
                    if (residueCollided.Cnt >= residueCollided.N) {
                        throw new ArgumentException(String.Format("residueCollided is already full! residueCollided.Cnt={0}, residueCollided.N={1}: trying to insert collider.Shape={4}, collider.Data={5}", residueCollided.Cnt, residueCollided.N, bCollider.Shape, bCollider.Data));
                    }
                    residueCollided.Put(bCollider);
                    continue;
                }
                ConvexPolygon bShape = bCollider.Shape;

                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, true, true, ref overlapResult);

                if (!overlapped) {
                    continue;
                }

                float normAlignmentWithGravity = (overlapResult.OverlapY * -1f);
                bool isAlongForwardPropagation = (0 <= bullet.VelX * (bCollider.X-aCollider.X)); // [WARNING] Character handles this (equivalently) outside of "calcHardPushbacksNormsForCharacter", but for bullets I temporarily found it more convenient handling here, there might be some room for enhancement. 
                bool isSqueezer = (-SNAP_INTO_PLATFORM_THRESHOLD > normAlignmentWithGravity);
                /*
                 [WARNING] Deliberately excluding "providesSlipJump" traps for easier handling of intermittent flat terrains as well as better player intuition!

                The "isWall" criteria here is deliberately made different from character counterpart!
                 */
                bool isWall = (SNAP_INTO_PLATFORM_THRESHOLD >= normAlignmentWithGravity);
                if ((isSqueezer || isWall) && providesSlipJump) {
                    continue;
                }
                
                float barrierTop = bCollider.Y + bCollider.H;
                // [WARNING] At a corner with 1 vertical edge and 1 horizontal edge, make sure that the VERTICAL edge is chosen as primary!
                if (isWall && !primaryIsWall) {
                    if (!isAlongForwardPropagation) {
                        continue;
                    }
                    if (primaryNonWallTop >= barrierTop /* barrierTop is wall-top now*/) {
                        // If primary non-wall is traversed before wall
                        continue;
                    }
                    // Initial wall transition
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                    primaryIsWall = isWall;
                    primaryWallTop = barrierTop;
                } else if (!isWall && primaryIsWall && barrierTop /* barrierTop is non-wall-top now */ < primaryWallTop) {
                    // Just skip, once the bullet is checked to collide with a wall, any parasitic non-wall collision would be ignored...
                } else {
                    // Same polarity
                    if (overlapResult.OverlapMag > primaryOverlapMag) {
                        primaryOverlapIndex = retCnt;
                        primaryOverlapMag = overlapResult.OverlapMag;
                        overlapResult.cloneInto(ref primaryOverlapResult);
                    } else {
                        if ((overlapResult.AxisX < primaryOverlapResult.AxisX) || (overlapResult.AxisX == primaryOverlapResult.AxisX && overlapResult.AxisY < primaryOverlapResult.AxisY)) {
                            primaryOverlapIndex = retCnt;
                            primaryOverlapMag = overlapResult.OverlapMag;
                            overlapResult.cloneInto(ref primaryOverlapResult);
                        }
                    }
                    
                    primaryIsWall = isWall;
                    if (isWall) {
                        primaryWallTop = barrierTop;
                    } else {            
                        primaryNonWallTop = barrierTop;
                    }
                }

                hardPushbacks[retCnt].X = pushbackX;
                hardPushbacks[retCnt].Y = pushbackY;

                retCnt++;
            }

            return retCnt;
        }

        public static int calcHardPushbacksNormsForPickable(RoomDownsyncFrame currRenderFrame, Pickable pickable, Collider aCollider, ConvexPolygon aShape, Vector[] hardPushbacks, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, out int primaryOverlapIndex, ILoggerBridge logger) {
            int retCnt = 0;
            primaryOverlapIndex = -1;
            float primaryOverlapMag = float.MinValue;
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) {
                //logger.LogInfo(String.Format("No collision object."));
                return retCnt;
            }

            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                bool isAnotherHardPushbackTrap = false;
                bool isAnActualBarrier = false;

                switch (bCollider.Data) {
                    case Pickable v0:
                    case CharacterDownsync v1:
                    case Bullet v2:
                    case PatrolCue v3:
                    case TriggerColliderAttr v4:
                        break;
                    case TrapColliderAttr v5:
                        var trap = currRenderFrame.TrapsArr[v5.TrapLocalId];
                        isAnotherHardPushbackTrap = (v5.ProvidesHardPushback && TrapState.Tdestroyed != trap.TrapState);
                        break;
                    default:
                        // By default it's a regular barrier, even if data is nil, note that Golang syntax of switch-case is kind of confusing, this "default" condition is met only if "!*CharacterDownsync && !*Bullet".
                        isAnActualBarrier = true;
                        break;
                }

                if (!isAnotherHardPushbackTrap && !isAnActualBarrier) {
                    continue;
                }
                ConvexPolygon bShape = bCollider.Shape;

                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, true, true, ref overlapResult);

                if (!overlapped) {
                    continue;
                }

                // Same polarity
                if (overlapResult.OverlapMag > primaryOverlapMag) {
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                } else {
                    if ((overlapResult.AxisX < primaryOverlapResult.AxisX) || (overlapResult.AxisX == primaryOverlapResult.AxisX && overlapResult.AxisY < primaryOverlapResult.AxisY)) {
                        primaryOverlapIndex = retCnt;
                        primaryOverlapMag = overlapResult.OverlapMag;
                        overlapResult.cloneInto(ref primaryOverlapResult);
                    }
                }

                hardPushbacks[retCnt].X = pushbackX;
                hardPushbacks[retCnt].Y = pushbackY;

                retCnt++;
            }

            return retCnt;
        }

        public static int calcHardPushbacksNormsForTrap(RoomDownsyncFrame currRenderFrame, TrapColliderAttr colliderAttr, Collider aCollider, ConvexPolygon aShape, Vector[] hardPushbacks, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, out int primaryOverlapIndex, FrameRingBuffer<Collider> residueCollided, out bool hitsAnActualBarrier, ILoggerBridge logger) {
            hitsAnActualBarrier = false;
            int retCnt = 0;
            primaryOverlapIndex = -1;
            float primaryOverlapMag = float.MinValue;
            residueCollided.Clear();
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) {
                //logger.LogInfo(String.Format("No collision object."));
                return retCnt;
            }

            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                bool isAnotherHardPushbackTrap = false;
                bool isAnActualBarrier = false;

                switch (bCollider.Data) {
                    case CharacterDownsync v0:
                    case Bullet v1:
                    case PatrolCue v2:
                    case TriggerColliderAttr v3:
                    case Pickable v4:
                        break;
                    case TrapColliderAttr v5:
                        var trap = currRenderFrame.TrapsArr[v5.TrapLocalId];
                        isAnotherHardPushbackTrap = (v5.ProvidesHardPushback && TrapState.Tdestroyed != trap.TrapState);
                        break;
                    default:
                        // By default it's a regular barrier, even if data is nil, note that Golang syntax of switch-case is kind of confusing, this "default" condition is met only if "!*CharacterDownsync && !*Bullet".
                        isAnActualBarrier = true;
                        break;
                }

                if (!isAnotherHardPushbackTrap && !isAnActualBarrier) {
                    if (residueCollided.Cnt >= residueCollided.N) {
                        throw new ArgumentException(String.Format("residueCollided is already full! residueCollided.Cnt={0}, residueCollided.N={1}: trying to insert collider.Shape={4}, collider.Data={5}", residueCollided.Cnt, residueCollided.N, bCollider.Shape, bCollider.Data));
                    }
                    residueCollided.Put(bCollider);
                    continue;
                }
                ConvexPolygon bShape = bCollider.Shape;

                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, true, false, ref overlapResult);

                if (!overlapped) {
                    continue;
                }

                if (isAnActualBarrier) {
                    hitsAnActualBarrier = true;
                }

                // Same polarity
                if (overlapResult.OverlapMag > primaryOverlapMag) {
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                } else {
                    if ((overlapResult.AxisX < primaryOverlapResult.AxisX) || (overlapResult.AxisX == primaryOverlapResult.AxisX && overlapResult.AxisY < primaryOverlapResult.AxisY)) {
                        primaryOverlapIndex = retCnt;
                        primaryOverlapMag = overlapResult.OverlapMag;
                        overlapResult.cloneInto(ref primaryOverlapResult);
                    }
                }

                hardPushbacks[retCnt].X = pushbackX;
                hardPushbacks[retCnt].Y = pushbackY;

                retCnt++;
            }

            return retCnt;
        }

        public static void processPrimaryAndImpactEffPushback(Vector effPushback, Vector[] pushbacks, int pushbacksCnt, int primaryOverlapIndex, float snapOverlap, bool eraseReverseDirection) {
            if (0 == pushbacksCnt) {
                return;
            }
            // Now that we have a "primaryOverlap" which we should get off by top priority, i.e. all the other hardPushbacks should clamp their x and y components to be no bigger than that of the "primaryOverlap".
            float primaryPushbackX = pushbacks[primaryOverlapIndex].X;
            float primaryPushbackY = pushbacks[primaryOverlapIndex].Y;
            if (0 == primaryPushbackX && 0 == primaryPushbackY) return;
            for (int i = 0; i < pushbacksCnt; i++) {
                var pushback = pushbacks[i];
                if (0 == pushback.X && 0 == pushback.Y) continue;
                if (i != primaryOverlapIndex) {
                    if (pushback.X * primaryPushbackX > 0) {
                        if (Math.Abs(pushback.X) > Math.Abs(primaryPushbackX)) {
                            pushback.X -= primaryPushbackX;
                        } else {
                            pushback.X = 0;
                        }
                    } else if (eraseReverseDirection) {
                        // Otherwise the sum over the reverse direction might pile up to large value.
                        pushback.X = 0;
                    }

                    if (pushback.Y * primaryPushbackY > 0) {
                        if (Math.Abs(pushback.Y) > Math.Abs(primaryPushbackY)) {
                            pushback.Y -= primaryPushbackY;
                        } else {
                            pushback.Y = 0;
                        }
                    } else if (eraseReverseDirection) {
                        // Otherwise the sum over the reverse direction might pile up to large value.
                        pushback.Y = 0;
                    }
                }
                // Normalize and thus re-purpose "pushbacks[i]" to be later used
                var magSqr = pushback.X * pushback.X + pushback.Y * pushback.Y;
                var invMag = InvSqrt32(magSqr);
                var mag = magSqr * invMag;

                float normX = pushback.X*invMag, normY = pushback.Y*invMag;
                // [WARNING] The following statement works even when "mag < snapOverlap"!
                effPushback.X += (mag-snapOverlap)*normX;
                effPushback.Y += (mag-snapOverlap)*normY;

                pushback.X = normX;
                pushback.Y = normY;
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

        private static float TOO_FAR_FOR_PUSHBACK_MAGNITUDE = 64.0f; // Roughly the height of a character
        public static bool isPolygonPairOverlapped(ConvexPolygon a, ConvexPolygon b, bool onlyOnBShapeEdges, bool prefersAOnBShapeTopEdges, ref SatResult result) {
            int aCnt = a.Points.Cnt;
            int bCnt = b.Points.Cnt;
            // Single point case
            if (1 == aCnt && 1 == bCnt) {
                result.OverlapMag = 0;
                Vector? aPoint = a.GetPointByOffset(0);
                Vector? bPoint = b.GetPointByOffset(0);
                return null != aPoint && null != bPoint && aPoint.X == bPoint.X && aPoint.Y == bPoint.Y;
            }

            bool foundNonOverwhelmingOverlap = false;
            if (1 < aCnt && !onlyOnBShapeEdges) {
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
                    float dx = (v.Y - u.Y);
                    float dy = -(v.X - u.X);
                    float invSqrtForAxis = InvSqrt32(dx * dx + dy * dy);
                    dx *= invSqrtForAxis;
                    dy *= invSqrtForAxis;
                    if (isPolygonPairSeparatedByDir(a, b, dx, dy, ref result)) {
                        return false;
                    }
                    if (result.OverlapMag < TOO_FAR_FOR_PUSHBACK_MAGNITUDE) {
                        foundNonOverwhelmingOverlap = true;
                    }
                }
            }

            bool alreadyGotPreferredAOnBTopResult = false;
            if (1 < bCnt) {
                tmpResultHolder.reset(); // [WARNING] It's important NOT to reset "tmpResultHolder" on each "i in [0, bCnt)", because inside "isPolygonPairSeparatedByDir(a, b, dx, dy, ref tmpResultHolder)" we rely on the historic value of "tmpResultHolder" to check whether or not we're currently on "shortest path to escape overlap"!
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

                    float dx = (v.Y - u.Y);
                    float dy = -(v.X - u.X);
                    float invSqrtForAxis = InvSqrt32(dx * dx + dy * dy);
                    dx *= invSqrtForAxis;
                    dy *= invSqrtForAxis;
                    
                    if (onlyOnBShapeEdges && prefersAOnBShapeTopEdges) {
                        if (isPolygonPairSeparatedByDir(a, b, dx, dy, ref tmpResultHolder)) {
                            return false;
                        } 
                        // Overlapped if only axis projection separation were required
                        if (alreadyGotPreferredAOnBTopResult && u.X == v.X) {
                            continue;
                        } else if (tmpResultHolder.OverlapMag < TOO_FAR_FOR_PUSHBACK_MAGNITUDE) {
                            foundNonOverwhelmingOverlap = true;
                            tmpResultHolder.cloneInto(ref result);
                            if (u.X != v.X) {
                                alreadyGotPreferredAOnBTopResult = true;
                            }
                        }
                    } else {
                        // Just regular usage -- always pick the last overlapping result 
                        if (isPolygonPairSeparatedByDir(a, b, dx, dy, ref result)) {
                            return false;
                        }
                        // Overlapped if only axis projection separation were required
                        if (result.OverlapMag < TOO_FAR_FOR_PUSHBACK_MAGNITUDE) {
                            foundNonOverwhelmingOverlap = true;
                        }
                    }
                }
            }

            return foundNonOverwhelmingOverlap;
        }

        public static bool isPolygonPairSeparatedByDir(ConvexPolygon a, ConvexPolygon b, float axisX, float axisY, ref SatResult result) {
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
            roundToRectilinearDir(ref axisX, ref axisY);
            float aStart = MAX_FLOAT32;
            float aEnd = -MAX_FLOAT32;
            float bStart = MAX_FLOAT32;
            float bEnd = -MAX_FLOAT32;
            for (int i = 0; i < a.Points.Cnt; i++) {
                Vector? p = a.GetPointByOffset(i);
                if (null == p) {
                    throw new ArgumentNullException("Getting a null point from polygon a!");
                }
                float dot = (p.X + a.X) * axisX + (p.Y + a.Y) * axisY;

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
                float dot = (p.X + b.X) * axisX + (p.Y + b.Y) * axisY;

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

            float overlapProjected = 0;

            if (aStart < bStart) {
                result.AContainedInB = false;

                if (aEnd < bEnd) {
                    overlapProjected = (float)((decimal)aEnd - (decimal)bStart);
                    result.BContainedInA = false;
                } else {
                    float option1 = (float)((decimal)aEnd - (decimal)bStart);
                    float option2 = (float)((decimal)bEnd - (decimal)aStart);
                    if (option1 < option2) {
                        overlapProjected = option1;
                    } else {
                        overlapProjected = -option2;
                    }
                }
            } else {
                result.BContainedInA = false;

                if (aEnd > bEnd) {
                    overlapProjected = (float)((decimal)aStart - (decimal)bEnd);
                    result.AContainedInB = false;
                } else {
                    float option1 = (float)((decimal)aEnd - (decimal)bStart);
                    float option2 = (float)((decimal)bEnd - (decimal)aStart);
                    if (option1 < option2) {
                        overlapProjected = option1;
                    } else {
                        overlapProjected = -option2;
                    }
                }
            }

            float currentOverlap = result.OverlapMag;
            float absoluteOverlap = overlapProjected;
            if (overlapProjected < 0) {
                absoluteOverlap = -overlapProjected;
            }

            if ((0 == result.AxisX && 0 == result.AxisY) || (currentOverlap > absoluteOverlap)) {
                float sign = 1;
                if (overlapProjected < 0) {
                    sign = -1;
                }

                result.OverlapMag = absoluteOverlap;
                result.OverlapX = axisX * sign;
                result.OverlapY = axisY * sign;
                result.AxisX = axisX;
                result.AxisY = axisY;
            }

            // the specified unit vector (axisX, axisY) doesn't separate "a" and "b", overlap result is generated
            return false;
        }

        public static (int, int) PolygonColliderCtrToVirtualGridPos(float wx, float wy) {
            // [WARNING] Introduces loss of precision!
            // In JavaScript floating numbers suffer from seemingly non-deterministic arithmetics, and even if certain libs solved this issue by approaches such as fixed-point-number, they might not be used in other libs -- e.g. the "collision libs" we're interested in -- thus couldn't kill all pains.
            int vx = (int)(Math.Round(wx * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));
            int vy = (int)(Math.Round(wy * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));
            return (vx, vy);
        }

        public static (float, float) VirtualGridToPolygonColliderCtr(int vx, int vy) {
            // No loss of precision
            float wx = (vx) * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO;
            float wy = (vy) * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO;
            return (wx, wy);
        }

        public static (float, float) PolygonColliderCtrToBL(float wx, float wy, float halfBoundingW, float halfBoundingH, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float collisionSpaceOffsetX, float collisionSpaceOffsetY) {
            return (wx - halfBoundingW - leftPadding + collisionSpaceOffsetX, wy - halfBoundingH - bottomPadding + collisionSpaceOffsetY);
        }

        public static (float, float) PolygonColliderBLToCtr(float cx, float cy, float halfBoundingW, float halfBoundingH, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float collisionSpaceOffsetX, float collisionSpaceOffsetY) {
            return (cx + halfBoundingW + leftPadding - collisionSpaceOffsetX, cy + halfBoundingH + bottomPadding - collisionSpaceOffsetY);
        }

        public static (int, int) PolygonColliderBLToVirtualGridPos(float cx, float cy, float halfBoundingW, float halfBoundingH, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float collisionSpaceOffsetX, float collisionSpaceOffsetY) {
            var (wx, wy) = PolygonColliderBLToCtr(cx, cy, halfBoundingW, halfBoundingH, topPadding, bottomPadding, leftPadding, rightPadding, collisionSpaceOffsetX, collisionSpaceOffsetY);
            return PolygonColliderCtrToVirtualGridPos(wx, wy);
        }

        public static (float, float) VirtualGridToPolygonColliderBLPos(int vx, int vy, float halfBoundingW, float halfBoundingH, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float collisionSpaceOffsetX, float collisionSpaceOffsetY) {
            var (wx, wy) = VirtualGridToPolygonColliderCtr(vx, vy);
            return PolygonColliderCtrToBL(wx, wy, halfBoundingW, halfBoundingH, topPadding, bottomPadding, leftPadding, rightPadding, collisionSpaceOffsetX, collisionSpaceOffsetY);
        }

        public static void AlignPolygon2DToBoundingBox(ConvexPolygon input) {
            // Transform again to put "anchor" at the "bottom-left point (w.r.t. world space)" of the bounding box for "resolv"
            float boundingBoxBLX = MAX_FLOAT32, boundingBoxBLY = MAX_FLOAT32;
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

        public static (float, float) TiledLayerPositionToCollisionSpacePosition(float tiledLayerX, float tiledLayerY, float spaceOffsetX, float spaceOffsetY) {
            return (tiledLayerX, spaceOffsetY + spaceOffsetY - tiledLayerY);
        }

        public static (float, float) CollisionSpacePositionToWorldPosition(float collisionSpaceX, float collisionSpaceY, float spaceOffsetX, float spaceOffsetY) {
            // [WARNING] This conversion is specifically added for Unity+SuperTiled2Unity
            return (collisionSpaceX, -spaceOffsetY - spaceOffsetY + collisionSpaceY);
        }

        // [WARNING] "continuousDx", "continuousDy" and "eps" are already scaled into [0, 1]
        public static (int, int, int) DiscretizeDirection(float continuousDx, float continuousDy, float eps = 0.1f) {
            int dx = 0, dy = 0, encodedIdx = 0;
            float absContinuousDx = Math.Abs(continuousDx);
            float absContinuousDy = Math.Abs(continuousDy);

            if (absContinuousDx < eps && absContinuousDy < eps) {
                return (dx, dy, encodedIdx);
            }
            float criticalRatio = continuousDy / continuousDx;
            float absCriticalRatio = Math.Abs(criticalRatio);
            float downEps = 5*eps; // dragging down is often more tentative for a player, thus give it a larger threshold!

            if (absCriticalRatio < magicLeanLowerBound && eps < absContinuousDx) {
                dy = 0;
                if (0 < continuousDx) {
                    dx = +2; // right 
                    encodedIdx = 3;
                } else {
                    dx = -2; // left 
                    encodedIdx = 4;
                }
            } else if (absCriticalRatio > magicLeanUpperBound && eps < absContinuousDy) {
                dx = 0;
                if (0 < continuousDy) {
                    dy = +2; // up
                    encodedIdx = 1;
                } else if (downEps < absContinuousDy) {
                    dy = -2; // down
                    encodedIdx = 2;
                } else {
                    // else stays at "encodedIdx == 0" 
                }
            } else if (eps < absContinuousDx && eps < absContinuousDy) {
                if (0 < continuousDx) {
                    dx = +1;
                    if (0 < continuousDy) {
                        dy = +1;
                        encodedIdx = 5;
                    } else {
                        if (downEps < absContinuousDy) {
                            dy = -1;
                            encodedIdx = 7;
                        } else {
                            dx = +2; // right 
                            encodedIdx = 3;
                        }
                    } 
                } else {
                    // 0 > continuousDx
                    dx = -1;
                    if (0 < continuousDy) {
                        dy = +1;
                        encodedIdx = 8;
                    } else {
                        if (downEps < absContinuousDy) {
                            dy = -1;
                            encodedIdx = 6;
                        } else {
                            dx = -2; // left 
                            encodedIdx = 4;
                        }
                    }
                }
            } else {
                // just use encodedIdx = 0
            }

            return (dx, dy, encodedIdx);
        }
    }
}

