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
            AxisY = 0,

            SecondaryOverlapMag = 0,
            SecondaryOverlapX = 0,
            SecondaryOverlapY = 0,
            SecondaryAContainedInB = false,
            SecondaryBContainedInA = false,
            SecondaryAxisX = 0,
            SecondaryAxisY = 0
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

        public static (bool, float, float) calcPushbacks(float oldDx, float oldDy, ConvexPolygon a, ConvexPolygon b, bool prefersAOnBShapeTopEdges, bool isForCharacterPushback, ref SatResult overlapResult) {
            float origX = a.X, origY = a.Y;
            try {
                a.SetPosition(origX + oldDx, origY + oldDy);
                overlapResult.resetForPushbackCalc();

                bool overlapped = isPolygonPairOverlapped(a, b, prefersAOnBShapeTopEdges, isForCharacterPushback, ref overlapResult);
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

        public static bool isVelAllowedByTrapCollider(TrapColliderAttr trapCollider, int velX, int velY) {
            if (0 == trapCollider.OnlyAllowsAlignedVelX && 0 == trapCollider.OnlyAllowsAlignedVelY) return false; 
            return (0 < (trapCollider.OnlyAllowsAlignedVelX*velX + trapCollider.OnlyAllowsAlignedVelY*velY)); 
        }

        public static int calcHardPushbacksNormsForCharacter(RoomDownsyncFrame currRenderFrame, CharacterConfig chConfig, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, Collider aCollider, ConvexPolygon aShape, Vector[] hardPushbacks, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, out int primaryOverlapIndex, out Trap? primaryTrap, out TrapColliderAttr? primaryTrapColliderAttr, out Bullet? primaryBlHardPushbackProvider, FrameRingBuffer<Collider> residueCollided, ILoggerBridge logger) {
            primaryTrap = null;
            primaryTrapColliderAttr = null;
            primaryBlHardPushbackProvider = null;
            float virtualGripToWall = 0.0f;
            bool isProactivelyGrabbingToWall = ((OnWallIdle1 == currCharacterDownsync.CharacterState || OnWallAtk1 == currCharacterDownsync.CharacterState) && currCharacterDownsync.DirX == thatCharacterInNextFrame.DirX); // [WARNING] Merely "true == currCharacterDownsync.OnWall" might NOT be proactive.
            if (isProactivelyGrabbingToWall  && 0 == thatCharacterInNextFrame.VelX) {
                float xfac = 1.0f;
                if (0 > thatCharacterInNextFrame.DirX) {
                    xfac = -xfac;
                }
                virtualGripToWall = xfac * currCharacterDownsync.Speed * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO;
            }
            int retCnt = 0;
            primaryOverlapIndex = -1;
            float primaryOverlapMag = float.MinValue;
            bool primaryIsWall = true; // Initialized to "true" to be updated even if there's only 1 vertical wall 
            float primaryNonWallTop = -MAX_FLOAT32, primaryWallTop = -MAX_FLOAT32;
            residueCollided.Clear();
            bool collided = aCollider.CheckAllWithHolder(virtualGripToWall, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) {
				//logger.LogInfo(String.Format("No collision object."));
                return retCnt;
            }

            // Get the largest to ensure determinism regardless of traversal order 
            float largestNormAlignmentWithHorizon1 = -MAX_FLOAT32;
            float largestNormAlignmentWithHorizon2 = -MAX_FLOAT32;

            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                int trapLocalId = TERMINATING_TRAP_ID;
                TrapColliderAttr? trapColliderAttr = null;
                bool isCharacterFlying = (currCharacterDownsync.OmitGravity || chConfig.OmitGravity);
                bool isBarrier = false;
                bool onTrap = false;
                bool onBullet = false;
                Bullet? bl = null;
                BulletConfig? blConfig = null;
                bool providesSlipJump = false;
                bool forcesCrouching = false;
                switch (bCollider.Data) {
                    case CharacterDownsync v0:
                        if (SPECIES_BRICK1 != currCharacterDownsync.SpeciesId && SPECIES_BRICK1 == v0.SpeciesId && v0.BulletTeamId != currCharacterDownsync.BulletTeamId) {
                            isBarrier = true;
                        }
                        if (SPECIES_FIRETOTEM != currCharacterDownsync.SpeciesId && SPECIES_FIRETOTEM == v0.SpeciesId && v0.BulletTeamId != currCharacterDownsync.BulletTeamId) {
                            isBarrier = true;
                        }
                        if (SPECIES_DARKBEAMTOWER != currCharacterDownsync.SpeciesId && SPECIES_DARKBEAMTOWER == v0.SpeciesId && v0.BulletTeamId != currCharacterDownsync.BulletTeamId) {
                            isBarrier = true;
                        }
                        break;
                    case Bullet v1:
                        if (SPECIES_FIRETOTEM == currCharacterDownsync.SpeciesId || SPECIES_BRICK1 == currCharacterDownsync.SpeciesId || SPECIES_DARKBEAMTOWER == currCharacterDownsync.SpeciesId) {
                            break;
                        }
                        (_, blConfig) = FindBulletConfig(v1.SkillId, v1.ActiveSkillHit);
                        if (null == blConfig) {
                            break;
                        }
                        if (blConfig.ProvidesXHardPushback || blConfig.ProvidesYHardPushbackTop || blConfig.ProvidesYHardPushbackBottom) {
                            isBarrier = true;
                            onBullet = true;
                            bl = v1;
                        }
                        break;
                    case PatrolCue v2:
                        break;
                    case Pickable v3:
                        //logger.LogInfo(String.Format("Character encountered a pickable v3 = {0}", v3));
                        break;
                    case TrapColliderAttr v4:
                        trapColliderAttr = v4;
                        trapLocalId = v4.TrapLocalId;
                        providesSlipJump = (v4.ProvidesSlipJump && !isCharacterFlying);
                        forcesCrouching = (v4.ForcesCrouching && !isCharacterFlying); // Obviously you cannot crouch when flying...
                        bool specialFlyingPass = (isCharacterFlying && v4.ProvidesSlipJump);
                        if (TERMINATING_TRAP_ID != trapLocalId) {
                            var trap = currRenderFrame.TrapsArr[trapLocalId-1];
                            /*
                            [WARNING]

                            It's a bit tricky here, as currently "v4.ProvidesSlipJump" implies "v4.ProvidesHardPushback", but we want flying characters to be able to freely fly across "v4.ProvidesSlipJump & v4.ProvidesHardPushback" yet not "!v4.ProvidesSlipJump & v4.ProvidesHardPushback".
                            */
                            onTrap = (v4.ProvidesHardPushback && TrapState.Tdeactivated != trap.TrapState && !specialFlyingPass); 
                            isBarrier = onTrap && !isVelAllowedByTrapCollider(v4, currCharacterDownsync.VelX, currCharacterDownsync.VelY);
                        } else {
                            onTrap = (v4.ProvidesHardPushback && !specialFlyingPass); 
                            isBarrier = onTrap && !isVelAllowedByTrapCollider(v4, currCharacterDownsync.VelX, currCharacterDownsync.VelY);
                        }
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

                float barrierTop = bCollider.Y + bCollider.H;
                float characterBottom = aCollider.Y;

                if (forcesCrouching && chConfig.CrouchingEnabled) {
                    // [WARNING] If "forcesCrouching" but "false == chConfig.CrouchingEnabled", then the current "bCollider" should be deemed as a regular barrier!
                    float characterTop = aCollider.Y + aCollider.H;
                    if (characterTop < barrierTop && 0 < overlapResult.OverlapY) {
                        thatCharacterInNextFrame.ForcedCrouching = true;
                    }
                    continue;
                }

                if (providesSlipJump) {
                    /*
                    Only provides hardPushbacks when 
                    - the character is not uprising, and
                    - the "bottom of the character" is higher than "top of the barrier rectangle - chConfig.SlipJumpThresHoldBelowTopFace". 
                    */
                    if (0 < currCharacterDownsync.VelY) {
                        continue;
                    }
                    if (characterBottom < (barrierTop - chConfig.SlipJumpThresHoldBelowTopFace)) {
                        continue;
                    }
                }

                float normAlignmentWithHorizon1 = (overlapResult.OverlapX * +1f);
                float normAlignmentWithHorizon2 = (overlapResult.OverlapX * -1f);
                bool isWall = (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1 || VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2);
                if (onBullet && null != bl && null != blConfig) {
                    // [WARNING] Special cases to deal damage only!
                    if (isWall && !blConfig.ProvidesXHardPushback) {
                        continue;
                    }
                    if (!isWall) {
                        bool bulletProvidesEffPushback = (blConfig.ProvidesYHardPushbackTop && 0 > overlapResult.OverlapY) || (blConfig.ProvidesYHardPushbackBottom && 0 < overlapResult.OverlapY);
                        if (!bulletProvidesEffPushback) {
                            continue;
                        }
                    }
                }
                
                bool isAlongForwardPropagation = (0 <= currCharacterDownsync.VelX * (bCollider.X-aCollider.X));  
                if (isWall && isAlongForwardPropagation && primaryNonWallTop >= barrierTop /* barrierTop is wall-top now*/) {
                    // If primary non-wall is traversed before wall
                    // [WARNING] Deliberately NOT just skipping "(isWall && !isAlongForwardPropagation)" like bullets, because for a character, "(isWall && !isAlongForwardPropagation)" provides immediate "push outward". 
                    continue;
                }

                if (normAlignmentWithHorizon1 > largestNormAlignmentWithHorizon1) {
                    largestNormAlignmentWithHorizon1 = normAlignmentWithHorizon1;
                }
                if (normAlignmentWithHorizon2 > largestNormAlignmentWithHorizon2) {
                    largestNormAlignmentWithHorizon2 = normAlignmentWithHorizon2;
                }
                // [WARNING] At a corner with 1 vertical edge and 1 horizontal edge, make sure that the HORIZONTAL edge is chosen as primary!
                if (!isWall && primaryIsWall) {
                    // Initial non-wall transition
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                    primaryIsWall = isWall;
                    if (0 > overlapResult.OverlapY) {
                        primaryNonWallTop = barrierTop;
                    }
                    if (onBullet) {
                        primaryBlHardPushbackProvider = bl;
                        primaryTrapColliderAttr = null;
                        primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                    } else if (onTrap) {
                        primaryTrapColliderAttr = trapColliderAttr;
                        if (TERMINATING_TRAP_ID != trapLocalId) {
                            primaryTrap = currRenderFrame.TrapsArr[trapLocalId-1];
                        } else {
                            primaryTrap = null;
                        }
                        primaryBlHardPushbackProvider = null;
                    } else {
                        primaryBlHardPushbackProvider = null;
                        primaryTrapColliderAttr = null;
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
                        if (onBullet) {
                            primaryBlHardPushbackProvider = bl;
                            primaryTrapColliderAttr = null;
                            primaryTrap = null;
                        } else if (onTrap) {
                            primaryTrapColliderAttr = trapColliderAttr;
                            if (TERMINATING_TRAP_ID != trapLocalId) {
                                primaryTrap = currRenderFrame.TrapsArr[trapLocalId-1];
                            } else {
                                primaryTrap = null;
                            }
                            primaryBlHardPushbackProvider = null;
                        } else {
                            primaryBlHardPushbackProvider = null;
                            primaryTrapColliderAttr = null;
                            primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                        }
                    } else if (overlapResult.OverlapMag == primaryOverlapMag) {
                        // [WARNING] Here's an important block for guaranteeing determinism regardless of traversal order.
                        if (onTrap && null == primaryTrapColliderAttr) {       
                            // If currently straddling across a trap and a non-trap, with equal overlapMap, then the trap takes higher priority!
                            primaryOverlapIndex = retCnt;
                            primaryOverlapMag = overlapResult.OverlapMag;
                            overlapResult.cloneInto(ref primaryOverlapResult);
                            primaryIsWall = isWall;
                            primaryTrapColliderAttr = trapColliderAttr;
                            primaryBlHardPushbackProvider = null;
                            if (TERMINATING_TRAP_ID != trapLocalId) {
                                primaryTrap = currRenderFrame.TrapsArr[trapLocalId-1];
                            } else {
                                primaryTrap = null;
                            }
                            primaryBlHardPushbackProvider = null;
                        } else {
                            if ((overlapResult.AxisX < primaryOverlapResult.AxisX) || (overlapResult.AxisX == primaryOverlapResult.AxisX && overlapResult.AxisY < primaryOverlapResult.AxisY)) {
                                primaryOverlapIndex = retCnt;
                                primaryOverlapMag = overlapResult.OverlapMag;
                                overlapResult.cloneInto(ref primaryOverlapResult);
                                primaryIsWall = isWall;
                                if (onBullet) {
                                    primaryBlHardPushbackProvider = bl;
                                    primaryTrapColliderAttr = null;
                                    primaryTrap = null;
                                } else if (onTrap) {
                                    primaryTrapColliderAttr = trapColliderAttr;
                                    if (TERMINATING_TRAP_ID != trapLocalId) {
                                        primaryTrap = currRenderFrame.TrapsArr[trapLocalId-1];
                                    } else {
                                        primaryTrap = null;
                                    }
                                    primaryBlHardPushbackProvider = null;
                                } else {
                                    primaryBlHardPushbackProvider = null;
                                    primaryTrapColliderAttr = null;
                                    primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                                }
                            }
                        }
                    }

                    if (primaryOverlapIndex == retCnt) {
                        if (isWall) {
                            primaryWallTop = barrierTop;
                        } else {
                            if (0 > overlapResult.OverlapY) {
                                primaryNonWallTop = barrierTop;
                            }
                        }
                    }
                }

                hardPushbacks[retCnt].X = pushbackX;
                hardPushbacks[retCnt].Y = pushbackY;

                retCnt++;
                
                if (retCnt >= hardPushbacks.Length) break;
            }

            if (VERTICAL_PLATFORM_THRESHOLD < largestNormAlignmentWithHorizon1) {
                thatCharacterInNextFrame.OnWall = true;
                thatCharacterInNextFrame.OnWallNormX = +1;
                thatCharacterInNextFrame.OnWallNormY = 0;
            } else if (VERTICAL_PLATFORM_THRESHOLD < largestNormAlignmentWithHorizon2) {
                thatCharacterInNextFrame.OnWall = true;
                thatCharacterInNextFrame.OnWallNormX = -1;
                thatCharacterInNextFrame.OnWallNormY = 0;
            } else {
                thatCharacterInNextFrame.OnWall = false;
                thatCharacterInNextFrame.OnWallNormX = 0;
                thatCharacterInNextFrame.OnWallNormY = 0;
            }

            return retCnt;
        }

        public static int calcHardPushbacksNormsForBullet(RoomDownsyncFrame currRenderFrame, Bullet bullet, Collider aCollider, ConvexPolygon aShape, Vector[] hardPushbacks, FrameRingBuffer<Collider> residueCollided, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, out int primaryOverlapIndex, out Trap? primaryTrap, out TrapColliderAttr? primaryTrapColliderAttr, ILoggerBridge logger) {
            primaryTrap = null;
            primaryTrapColliderAttr = null;
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

            var (_, bulletConfig) = FindBulletConfig(bullet.SkillId, bullet.ActiveSkillHit);
            if (null == bulletConfig) {
                return 0;
            }
            while (true) {
                var (exists, bCollider) = collision.PopFirstContactedCollider();

                if (!exists || null == bCollider) {
                    break;
                }
                int trapLocalId = TERMINATING_TRAP_ID;
                TrapColliderAttr? trapColliderAttr = null;
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
                        trapColliderAttr = v5;
                        trapLocalId = v5.TrapLocalId;
                        if (TERMINATING_TRAP_ID != v5.TrapLocalId) {
                            var trap = currRenderFrame.TrapsArr[v5.TrapLocalId-1];
                            isAnotherHardPushbackTrap = (v5.ProvidesHardPushback && TrapState.Tdeactivated != trap.TrapState && !isVelAllowedByTrapCollider(v5, bullet.VelX, bullet.VelY));
                            providesSlipJump = v5.ProvidesSlipJump;
                        } else {
                            isAnotherHardPushbackTrap = v5.ProvidesHardPushback && !isVelAllowedByTrapCollider(v5, bullet.VelX, bullet.VelY);
                            providesSlipJump = v5.ProvidesSlipJump;
                        }
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

                if (overlapResult.OverlapMag < CLAMPABLE_COLLISION_SPACE_MAG) {
                    /*
                    [WARNING] 
                    If I didn't clamp "pushbackX & pushbackY" here, there could be disagreed shape overlapping between backend and frontend, see comments around "shapeOverlappedOtherChCnt" in "Battle_dynamics". 
                    */
                    continue;
                }

                float normAlignmentWithGravity = (overlapResult.OverlapY * -1f);
                bool isAlongForwardPropagation = (0 <= bullet.VelX * (bCollider.X-aCollider.X)); 
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
                        /*
                        if (BulletType.GroundWave == bulletConfig.BType) {
                            logger.LogInfo("@rdfId= " + currRenderFrame.Id + ", groundWave bullet " + bullet.BulletLocalId + " skipping wall not along forward propagation#1. overlapResult=" + overlapResult.ToString());
                        }
                        */
                        continue;
                    }
                    if (primaryNonWallTop >= barrierTop /* barrierTop is wall-top now*/) {
                        // If primary non-wall is traversed before wall
                        /*
                        if (BulletType.GroundWave == bulletConfig.BType) {
                            logger.LogInfo("@rdfId= " + currRenderFrame.Id + ", groundWave bullet " + bullet.BulletLocalId + " skipping wallTop=" + barrierTop + " <= primaryNonWallTop=" + primaryNonWallTop + " #1. overlapResult=" + overlapResult.ToString());
                        }
                        */
                        continue;
                    }
                    // Initial wall transition
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                    primaryIsWall = isWall;
                    primaryWallTop = barrierTop;
                    if (isAnotherHardPushbackTrap) {
                        primaryTrapColliderAttr = trapColliderAttr;
                        if (TERMINATING_TRAP_ID != trapLocalId) {
                            primaryTrap = currRenderFrame.TrapsArr[trapLocalId-1];
                        } else {
                            primaryTrap = null;
                        }
                    } else {
                        primaryTrapColliderAttr = null;
                        primaryTrap = null;
                    }
                } else if (!isWall && primaryIsWall && barrierTop /* barrierTop is non-wall-top now */ < primaryWallTop) {
                    // Just skip, once the bullet is checked to collide with a wall, any parasitic non-wall collision would be ignored...
                    /*
                    if (BulletType.GroundWave == bulletConfig.BType) {
                        logger.LogInfo("@rdfId= " + currRenderFrame.Id + ", groundWave bullet (primaryIsWall) " + bullet.BulletLocalId + " skipping wallTop=" + barrierTop + " < primaryWallTop=" + primaryWallTop + ". overlapResult=" + overlapResult.ToString());
                    }
                    */
                    continue;
                } else {
                    if (isWall) {
                        if (!isAlongForwardPropagation) {
                            /*
                            if (BulletType.GroundWave == bulletConfig.BType) {
                                logger.LogInfo("@rdfId= " + currRenderFrame.Id + ", groundWave bullet " + bullet.BulletLocalId + " skipping wall not along forward propagation#2. overlapResult=" + overlapResult.ToString());
                            }
                            */
                            continue;
                        }
                        if (primaryNonWallTop >= barrierTop /* barrierTop is wall-top now*/) {
                            // If primary non-wall is traversed before wall
                            /*
                            if (BulletType.GroundWave == bulletConfig.BType) {
                                logger.LogInfo("@rdfId= " + currRenderFrame.Id + ", groundWave bullet " + bullet.BulletLocalId + " skipping wallTop=" + barrierTop + " <= primaryNonWallTop=" + primaryNonWallTop + " #2. overlapResult=" + overlapResult.ToString());
                            }
                            */
                            continue;
                        }
                    }

                    // Same polarity
                    if (overlapResult.OverlapMag < primaryOverlapMag) {
                        // [WARNING] Just add to "hardPushbacks[*]", don't touch primary markers
                    } else {
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

                        if (primaryOverlapIndex == retCnt) {
                            primaryIsWall = isWall;
                            if (isWall) {
                                primaryWallTop = barrierTop;
                            } else {
                                if (0 > overlapResult.OverlapY) {
                                    primaryNonWallTop = barrierTop;
                                }
                            }

                            if (isAnotherHardPushbackTrap) {
                                primaryTrapColliderAttr = trapColliderAttr;
                                if (TERMINATING_TRAP_ID != trapLocalId) {
                                    primaryTrap = currRenderFrame.TrapsArr[trapLocalId - 1];
                                } else {
                                    primaryTrap = null;
                                }
                            } else {
                                primaryTrapColliderAttr = null;
                                primaryTrap = null;
                            }
                        }
                    }
                }

                hardPushbacks[retCnt].X = pushbackX;
                hardPushbacks[retCnt].Y = pushbackY;

                retCnt++;

                if (retCnt >= hardPushbacks.Length) break;
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
                int trapLocalId = TERMINATING_TRAP_ID;
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
                        trapLocalId = v5.TrapLocalId;
                        if (TERMINATING_TRAP_ID != trapLocalId) {
                            var trap = currRenderFrame.TrapsArr[trapLocalId-1];
                            isAnotherHardPushbackTrap = (v5.ProvidesHardPushback && TrapState.Tdeactivated != trap.TrapState && !isVelAllowedByTrapCollider(v5, pickable.VelX, pickable.VelY));
                        } else {
                            isAnotherHardPushbackTrap = (v5.ProvidesHardPushback && !isVelAllowedByTrapCollider(v5, pickable.VelX, pickable.VelY));
                        }
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
                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, true, false, ref overlapResult);

                if (!overlapped) {
                    continue;
                }

                if (overlapResult.OverlapMag < CLAMPABLE_COLLISION_SPACE_MAG) {
                    /*
                    [WARNING] 
                    If I didn't clamp "pushbackX & pushbackY" here, there could be disagreed shape overlapping between backend and frontend, see comments around "shapeOverlappedOtherChCnt" in "Battle_dynamics". 
                    */
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

                if (retCnt >= hardPushbacks.Length) break;
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
                int trapLocalId = TERMINATING_TRAP_ID;
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
                        trapLocalId = v5.TrapLocalId;
                        if (TERMINATING_TRAP_ID != trapLocalId) {
                            var trap = currRenderFrame.TrapsArr[trapLocalId-1];
                            isAnotherHardPushbackTrap = (!v5.ProvidesSlipJump && v5.ProvidesHardPushback && TrapState.Tdeactivated != trap.TrapState && !isVelAllowedByTrapCollider(v5, trap.VelX, trap.VelY));
                        } else {
                            isAnotherHardPushbackTrap = (!v5.ProvidesSlipJump && v5.ProvidesHardPushback);
                        }
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
                var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, false, true, ref overlapResult);

                if (!overlapped) {
                    continue;
                }

                if (overlapResult.OverlapMag < CLAMPABLE_COLLISION_SPACE_MAG) {
                    /*
                    [WARNING] 
                    If I didn't clamp "pushbackX & pushbackY" here, there could be disagreed shape overlapping between backend and frontend, see comments around "shapeOverlappedOtherChCnt" in "Battle_dynamics". 
                    */
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

                if (retCnt >= hardPushbacks.Length) break;
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

        public static bool isPolygonPairOverlapped(ConvexPolygon a, ConvexPolygon b, bool prefersAOnBShapeTopEdges, bool isForCharacterPushback, ref SatResult result) {
            int aCnt = a.Points.Cnt;
            int bCnt = b.Points.Cnt;
            // Single point case
            if (1 == aCnt && 1 == bCnt) {
                result.OverlapMag = 0;
                Vector? aPoint = a.GetPointByOffset(0);
                Vector? bPoint = b.GetPointByOffset(0);
                return null != aPoint && null != bPoint && aPoint.X == bPoint.X && aPoint.Y == bPoint.Y;
            }
            bool onlyOnBShapeEdges = (!a.IsRotary && !b.IsRotary); // [WARNING] If both are not rotary, i.e. both rectilinear, then only need check edges of either! 
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
                    float dx2 = dx * dx, dy2 = dy * dy;
                    if (0 >= dx2 && 0 >= dy2) {
                        // Not a valid polygon
                        return false;
                    }
                    float invSqrtForAxis = InvSqrt32(dx2 + dy2);
                    dx *= invSqrtForAxis;
                    dy *= invSqrtForAxis;
                    
                    if (isForCharacterPushback || (onlyOnBShapeEdges && prefersAOnBShapeTopEdges)) {
                        if (isPolygonPairSeparatedByDir(a, b, dx, dy, ref tmpResultHolder)) {
                            return false;
                        }
                        // Overlapped if only axis projection separation were required
                        if (tmpResultHolder.OverlapMag < TOO_FAR_FOR_PUSHBACK_MAGNITUDE) {
                            foundNonOverwhelmingOverlap = true;
                            tmpResultHolder.cloneInto(ref result);
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

                if (prefersAOnBShapeTopEdges && 0 <= result.OverlapY && 0 > result.SecondaryOverlapY) {
                    if (result.SecondaryOverlapMag < GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP) {
                        // [WARNING] Close enough to just lift the character.
                        result.shiftFromSecondary();
                    } else if (result.SecondaryOverlapMag < SIDE_PUSHBACK_REPEL_THRESHOLD) {
                        // [WARNING] In this case, the "SecondaryOverlap" is still a top edge of bShape while the "PrimaryOverlap" is a side or bottom edge. However, due to "GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP <= SecondaryOverlapMag" we think that "aShape" has fallen into "SecondaryOverlap" too much to deem "SecondaryOverlap" a supporting edge. Therefore, if "SecondaryOverlapMag" is not yet too deep, i.e. "GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP <= SecondaryOverlapMag < SIDE_PUSHBACK_REPEL_THRESHOLD", then we just enlarge "PrimaryOverlapMag" to push out aShape, as well as setting "SideSuppressingTop" to assign extra "InertiaFramesToRecover" for a character (if aShape is a character).  
                        result.OverlapMag *= 4;
                        result.SideSuppressingTop = true;
                    } else {
                        // [WARNING] Intentionally left as-is for wall grabbing.
                    }
                }
            }

            return (isForCharacterPushback || (onlyOnBShapeEdges && prefersAOnBShapeTopEdges)) ? foundNonOverwhelmingOverlap : true;
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
                result.AContainedInB = false;
                result.BContainedInA = false;
                return true;
            }

            float overlapProjected = 0;
            bool falsifyAContainedInB = (aStart < bStart || aEnd > bEnd);
            bool falsifyBContainedInA = (aEnd < bEnd || aStart > bStart);

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
                result.BContainedInA &= !falsifyBContainedInA;

                if (aEnd > bEnd) {
                    overlapProjected = (float)((decimal)aStart - (decimal)bEnd);
                    result.AContainedInB &= false;
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

            float currentOverlapMag = result.OverlapMag;
            float newOverlapMag = overlapProjected;
            if (overlapProjected < 0) {
                newOverlapMag = -overlapProjected;
            }

            float sign = 1;
            if (overlapProjected < 0) {
                sign = -1;
            }
            
            float newOverlapX = axisX * sign, newOverlapY = axisY * sign;
            bool hasSmallerOverlapMag = (currentOverlapMag > newOverlapMag);
            bool effectivelySameAsPrimary = !hasSmallerOverlapMag && (currentOverlapMag == newOverlapMag && newOverlapX == result.OverlapX && newOverlapY == result.OverlapY);
            if (0 == result.AxisX && 0 == result.AxisY) {
                result.AContainedInB &= !falsifyAContainedInB;
                result.BContainedInA &= !falsifyBContainedInA;
                result.OverlapMag = newOverlapMag;
                result.OverlapX = newOverlapX;
                result.OverlapY = newOverlapY;
                result.AxisX = axisX;
                result.AxisY = axisY;
            } else if (hasSmallerOverlapMag) {
                if ((0 == result.SecondaryAxisX && 0 == result.SecondaryAxisY) || result.SecondaryOverlapMag > result.OverlapMag) {
                    result.shiftToSecondary();
                }
                result.AContainedInB &= !falsifyAContainedInB;
                result.BContainedInA &= !falsifyBContainedInA;
                result.OverlapMag = newOverlapMag;
                result.OverlapX = newOverlapX;
                result.OverlapY = newOverlapY;
                result.AxisX = axisX;
                result.AxisY = axisY;
            } else if ((0 == result.SecondaryAxisX && 0 == result.SecondaryAxisY) || (result.SecondaryOverlapMag > newOverlapMag)) {
                if (!effectivelySameAsPrimary) {
                    result.SecondaryAContainedInB &= !falsifyAContainedInB;
                    result.SecondaryBContainedInA &= !falsifyBContainedInA;
                    result.SecondaryOverlapMag = newOverlapMag;
                    result.SecondaryOverlapX = newOverlapX;
                    result.SecondaryOverlapY = newOverlapY;
                    result.SecondaryAxisX = axisX;
                    result.SecondaryAxisY = axisY;
                }
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
        public static (int, int, int) DiscretizeDirection(float continuousDx, float continuousDy, float eps = 0.1f, bool mustHaveNonZeroX = false) {
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

                if (mustHaveNonZeroX) {
                    if (0 == continuousDx) {
                        if (0 < dy) {
                            dx = +1;
                        } else {
                            dx = -1;
                        }
                    } else if (0 < continuousDx) {
                        dx = +1;
                    } else {
                        dx = -1;
                    }

                    if (0 < dx) {
                        if (0 < dy) {
                            dy = +1;
                            encodedIdx = 5;
                        } else {
                            dy = -1;
                            encodedIdx = 7;
                        }
                    } else {
                        dx = -1;
                        if (0 < dy) {
                            dy = +1;
                            encodedIdx = 8;
                        } else {
                            dy = -1;
                            encodedIdx = 6;
                        }
                    }
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

        private static void findHorizontallyClosestCharacterCollider(int rdfId, CharacterDownsync currCharacterDownsync, Collider visionCollider, Collider entityCollider, Collision collision, ref SatResult overlapResult, out Collider? res1, out CharacterDownsync? res1Ch, out Collider? res2, out Bullet? res2Bl, ILoggerBridge logger) {
            res1 = null;
            res1Ch = null;
            res2 = null;
            res2Bl = null;

            // [WARNING] Finding only the closest non-self character to react to for avoiding any randomness. 
            bool collided = visionCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) return;

            float minAbsColliderDx = MAX_FLOAT32;
            float minAbsColliderDy = MAX_FLOAT32;

            ConvexPolygon aShape = visionCollider.Shape;
            while (true) {
                var (ok3, bCollider) = collision.PopFirstContactedCollider();
                if (false == ok3 || null == bCollider) {
                    break;
                }

                CharacterDownsync? v3 = bCollider.Data as CharacterDownsync;
                if (null != v3) {
                    // Only check shape collision (which is relatively expensive) if it's the targeted entity type 
                    if (v3.JoinIndex == currCharacterDownsync.JoinIndex || v3.BulletTeamId == currCharacterDownsync.BulletTeamId) {
                        continue;
                    }

                    if (Dimmed == v3.CharacterState || invinsibleSet.Contains(v3.CharacterState) || 0 < v3.FramesInvinsible) continue; // Target is invinsible, nothing can be done

                    ConvexPolygon bShape = bCollider.Shape;
                    var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult);
                    if (!overlapped && !overlapResult.BContainedInA) {
                        continue;
                    }

                    var colliderDx = (bCollider.X - entityCollider.X);
                    var colliderDy = (bCollider.Y - entityCollider.Y);

                    var absColliderDx = Math.Abs(colliderDx);
                    if (absColliderDx > minAbsColliderDx) {
                        continue;
                    }

                    var absColliderDy = Math.Abs(colliderDy);
                    if (absColliderDx == minAbsColliderDx && absColliderDy > minAbsColliderDy) {
                        continue;
                    }
                    minAbsColliderDx = absColliderDx;
                    minAbsColliderDy = absColliderDy;
                    res1 = bCollider;
                    res1Ch = v3;
                    res2 = null;
                    res2Bl = null;
                } else {
                    Bullet? v4 = bCollider.Data as Bullet;
                    if (null == v4) {
                        continue;
                    }
                    if (v4.TeamId == currCharacterDownsync.BulletTeamId) {
                        continue;
                    }

                    var (_, bulletConfig) = FindBulletConfig(v4.SkillId, v4.ActiveSkillHit);
                    if (null == bulletConfig || (0 >= bulletConfig.Damage && null == bulletConfig.BuffConfig)) continue; // v4 is not offensive
                    var colliderDx = (bCollider.X - entityCollider.X);
                    var colliderDy = (bCollider.Y - entityCollider.Y);
                    if (0 <= v4.DirX*colliderDx) {
                        /*
                        if (Def1 == currCharacterDownsync.CharacterState) {
                            logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, dirX={2}, ch.VirtualX={3}, ch.VirtualY={4} evaluating bullet localId={5}, v4.VirtualX={6}, v4.VirtualY={7}, colliderDx={8}, colliderDy={9}; bullet is not offensive", rdfId, currCharacterDownsync.Id, currCharacterDownsync.DirX, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, v4.BulletLocalId, v4.VirtualGridX, v4.VirtualGridY, colliderDx, colliderDy));
                        }
                        */
                        continue; // v4 is not offensive
                    }

                    ConvexPolygon bShape = bCollider.Shape;
                    var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult);
                    if (!overlapped && !overlapResult.BContainedInA) {
                        continue;
                    }

                    var absColliderDx = Math.Abs(colliderDx);
                    if (absColliderDx > minAbsColliderDx) {
                        continue;
                    }

                    var absColliderDy = Math.Abs(colliderDy);
                    if (absColliderDx == minAbsColliderDx && absColliderDy > minAbsColliderDy) {
                        continue;
                    }
                    minAbsColliderDx = absColliderDx;
                    minAbsColliderDy = absColliderDy;
                    res1 = null;
                    res1Ch = null;
                    res2 = bCollider;
                    res2Bl = v4;
                }
            }
        }

        private static void findHorizontallyClosestCharacterCollider(Bullet blWithVision, Collider aCollider, Collision collision, ref SatResult overlapResult, out Collider? res1, out CharacterDownsync? res2) {
            res1 = null;
            res2 = null;

            // [WARNING] Finding only the closest non-self character to react to for avoiding any randomness. 
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) return;

            float minAbsColliderDx = MAX_FLOAT32;
            float minAbsColliderDy = MAX_FLOAT32;

            ConvexPolygon aShape = aCollider.Shape;
            while (true) {
                var (ok3, bCollider) = collision.PopFirstContactedCollider();
                if (false == ok3 || null == bCollider) {
                    break;
                }

                CharacterDownsync? v3 = bCollider.Data as CharacterDownsync;
                if (null == v3) {
                    // Only check shape collision (which is relatively expensive) if it's CharacterDownsync 
                    continue;
                }

                if (v3.JoinIndex == blWithVision.OffenderJoinIndex || v3.BulletTeamId == blWithVision.TeamId) {
                    continue;
                }

                if (invinsibleSet.Contains(v3.CharacterState) || 0 < v3.FramesInvinsible) continue; // Target is invinsible, nothing can be done

                var (_, bulletConfig) = FindBulletConfig(blWithVision.SkillId, blWithVision.ActiveSkillHit);
                if (null == bulletConfig) continue;
                int immuneRcdI = 0;
                bool shouldBeImmune = false;
                if (bulletConfig.RemainsUponHit) {
                    while (immuneRcdI < v3.BulletImmuneRecords.Count) {
                        var candidate = v3.BulletImmuneRecords[immuneRcdI];
                        if (TERMINATING_BULLET_LOCAL_ID == candidate.BulletLocalId) break;
                        if (candidate.BulletLocalId == blWithVision.BulletLocalId) {
                            shouldBeImmune = true;
                            break;
                        }
                        immuneRcdI++;
                    }
                }

                if (shouldBeImmune) {
                    continue;
                }

                ConvexPolygon bShape = bCollider.Shape;
                var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult);
                if (!overlapped) {
                    continue;
                }

                // By now we're sure that it should react to the PatrolCue
                var colliderDx = (aCollider.X - bCollider.X);

                var absColliderDx = Math.Abs(colliderDx);
                if (absColliderDx > minAbsColliderDx) {
                    continue;
                }

                var colliderDy = (aCollider.Y - bCollider.Y);
                var absColliderDy = Math.Abs(colliderDy);
                if (absColliderDx == minAbsColliderDx && absColliderDy > minAbsColliderDy) {
                    continue;
                }
                minAbsColliderDx = absColliderDx;
                minAbsColliderDy = absColliderDy;
                res1 = bCollider;
                res2 = v3;
            }
        }

        private static void findHorizontallyClosestPatrolCueCollider(CharacterDownsync currCharacterDownsync, Collider aCollider, Collision collision, ref SatResult overlapResult, out Collider? res1, out PatrolCue? res2) {
            res1 = null;
            res2 = null;

            // [WARNING] Finding only the closest patrol cue to react to for avoiding any randomness. 
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
            if (!collided) return;

            float minAbsColliderDx = MAX_FLOAT32;
            float minAbsColliderDy = MAX_FLOAT32;

            ConvexPolygon aShape = aCollider.Shape;
            while (true) {
                var (ok3, bCollider) = collision.PopFirstContactedCollider();
                if (false == ok3 || null == bCollider) {
                    break;
                }

                PatrolCue? v3 = bCollider.Data as PatrolCue;
                if (null == v3) {
                    // Only check shape collision (which is relatively expensive) if it's PatrolCue 
                    continue;
                }

                ConvexPolygon bShape = bCollider.Shape;
                var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult);
                if (!overlapped) {
                    continue;
                }

                // By now we're sure that it should react to the PatrolCue
                var colliderDx = (aCollider.X - bCollider.X);
                var absColliderDx = Math.Abs(colliderDx);
                if (absColliderDx > minAbsColliderDx) {
                    continue;
                }

                var colliderDy = (aCollider.Y - bCollider.Y);
                var absColliderDy = Math.Abs(colliderDy);
                if (absColliderDx == minAbsColliderDx && absColliderDy > minAbsColliderDy) {
                    continue;
                }
                minAbsColliderDx = absColliderDx;
                minAbsColliderDy = absColliderDy;
                res1 = bCollider;
                res2 = v3;
            }
        }
    }
}

