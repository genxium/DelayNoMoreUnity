using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        public static bool IsBulletRotary(BulletConfig bulletConfig) {
            return (0 != bulletConfig.AngularFrameVelCos) || (0 != bulletConfig.InitSpinCos || 0 != bulletConfig.InitSpinSin);
        }

        public static bool IsChargingAtkChState(CharacterState chState) {
            return (Atk7Charging == chState);
        }

        public static bool IsBulletVanishing(Bullet bullet, BulletConfig bulletConfig) {
            return BulletState.Vanishing == bullet.BlState;
        }

        public static bool IsBulletExploding(Bullet bullet, BulletConfig bulletConfig) {
            switch (bulletConfig.BType) {
                case BulletType.Melee:
                    return ((BulletState.Exploding == bullet.BlState || BulletState.Vanishing == bullet.BlState) && bullet.FramesInBlState < bulletConfig.ExplosionFrames);
                case BulletType.Fireball:
                case BulletType.GroundWave:
                case BulletType.MissileLinear:
                    return (BulletState.Exploding == bullet.BlState || BulletState.Vanishing == bullet.BlState);
                default:
                    return false;
            }
        }

        public static bool IsBulletActive(Bullet bullet, BulletConfig bulletConfig, int currRenderFrameId) {
            if (BulletState.Exploding == bullet.BlState || BulletState.Vanishing == bullet.BlState) {
                return false;
            }
            return (bullet.OriginatedRenderFrameId + bulletConfig.StartupFrames < currRenderFrameId) && (currRenderFrameId < bullet.OriginatedRenderFrameId + bulletConfig.StartupFrames + bulletConfig.ActiveFrames);
        }

        public static bool IsBulletJustActive(Bullet bullet, BulletConfig bulletConfig, int currRenderFrameId) {
            if (BulletState.Exploding == bullet.BlState || BulletState.Vanishing == bullet.BlState) {
                return false;
            }
            // [WARNING] Practically a bullet might propagate for a few render frames before hitting its visually "VertMovingTrapLocalIdUponActive"!
            int visualBufferRdfCnt = 3;
            if (BulletState.Active == bullet.BlState) {
                return visualBufferRdfCnt >= bullet.FramesInBlState;
            }
            return (bullet.OriginatedRenderFrameId + bulletConfig.StartupFrames < currRenderFrameId && currRenderFrameId <= bullet.OriginatedRenderFrameId + bulletConfig.StartupFrames + visualBufferRdfCnt);
        }

        public static bool IsBulletAlive(Bullet bullet, BulletConfig bulletConfig, int currRenderFrameId) {
            if (BulletState.Vanishing == bullet.BlState) {
                return bullet.FramesInBlState < bulletConfig.ActiveFrames + bulletConfig.ExplosionFrames;
            }
            if (BulletState.Exploding == bullet.BlState && MultiHitType.FromEmission != bulletConfig.MhType) {
                return bullet.FramesInBlState < bulletConfig.ExplosionFrames;
            }
            return (currRenderFrameId < bullet.OriginatedRenderFrameId + bulletConfig.StartupFrames + bulletConfig.ActiveFrames);
        }

        private static bool _updateBulletTargetJoinIndexByVision(Bullet src, Bullet dst, BulletConfig srcConfig, bool spinFlipX, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, ref SatResult overlapResult, Collision collision, ILoggerBridge logger) {
            float visionCx, visionCy, visionCw, visionCh;
            calcBulletVisionBoxInCollisionSpace(src, srcConfig, out visionCx, out visionCy, out visionCw, out visionCh);

            var visionCollider = dynamicRectangleColliders[colliderCnt];
            UpdateRectCollider(visionCollider, visionCx, visionCy, visionCw, visionCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst, COLLISION_VISION_INDEX_PREFIX, spinFlipX, isRotary: false, srcConfig.SpinAnchorX, srcConfig.SpinAnchorY, dst.SpinCos, dst.SpinSin);
            collisionSys.AddSingleToCellTail(visionCollider);

            Collider? bCollider;
            CharacterDownsync? v3;

            Collider? sameTeamChCollider;
            CharacterDownsync? v5;
            bool isAllyTargetingBl = (0 > srcConfig.Damage);
            findHorizontallyClosestCharacterColliderForBlWithVision(src, isAllyTargetingBl, visionCollider, collision, ref overlapResult, out bCollider, out v3, out sameTeamChCollider, out v5, logger);

            if (isAllyTargetingBl) {
                if (null != sameTeamChCollider && null != v5) {
                    dst.TargetCharacterJoinIndex = v5.JoinIndex;
                } else {
                    dst.TargetCharacterJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                }
            } else {
                if (null != bCollider && null != v3) {
                    dst.TargetCharacterJoinIndex = v3.JoinIndex;
                } else {
                    dst.TargetCharacterJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                }
            }

            collisionSys.RemoveSingleFromCellTail(visionCollider); // no need to increment "colliderCnt", the visionCollider is transient
            visionCollider.Data = null;

            return true;
        }

        private static bool handleBulletVelSpinning(RoomDownsyncFrame currRenderFrame, int roomCapacity, BulletConfig srcConfig, int dstTargetChJoinIndex, bool spinFlipX, Bullet src, ref int dstVelX, ref int dstVelY, ref float dstSpinCos, ref float dstSpinSin) {
            if (0 == srcConfig.AngularFrameVelCos && 0 == srcConfig.AngularFrameVelSin) return false; 
            float dstVelXFloat = src.VelX, dstVelYFloat = src.VelY;
            if (BulletType.MissileLinear == srcConfig.BType) {
                // [WARNING] "HopperMissile" never spins and relies on "RotatesAlongVelocity" to mimic spin rendering!
                if (MAGIC_JOIN_INDEX_INVALID != dstTargetChJoinIndex) {
                    // Spin to follow target if possible
                    var targetCh = getChdFromRdf(dstTargetChJoinIndex, roomCapacity, currRenderFrame);;
                    if (null == targetCh || invinsibleSet.Contains(targetCh.CharacterState)) {
                        dstTargetChJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else {
                        int diffX = (targetCh.VirtualGridX - src.VirtualGridX);
                        int diffY = (targetCh.VirtualGridY - src.VirtualGridY);
                        int crossProd = (src.VelX * diffY - src.VelY * diffX);
                        if (0 < crossProd) {
                            Vector.Rotate(src.VelX, src.VelY, srcConfig.AngularFrameVelCos, srcConfig.AngularFrameVelSin, out dstVelXFloat, out dstVelYFloat);
                            Vector.Rotate(src.SpinCos, src.SpinSin, srcConfig.AngularFrameVelCos, srcConfig.AngularFrameVelSin, out dstSpinCos, out dstSpinSin);
                        } else if (0 > crossProd) {
                            // flip sign for sines
                            Vector.Rotate(src.VelX, src.VelY, srcConfig.AngularFrameVelCos, -srcConfig.AngularFrameVelSin, out dstVelXFloat, out dstVelYFloat);
                            Vector.Rotate(src.SpinCos, src.SpinSin, srcConfig.AngularFrameVelCos, -srcConfig.AngularFrameVelSin, out dstSpinCos, out dstSpinSin);
                        }
                    }
                }
            } else {
                if (!spinFlipX) {
                    Vector.Rotate(src.SpinCos, src.SpinSin, srcConfig.AngularFrameVelCos, srcConfig.AngularFrameVelSin, out dstSpinCos, out dstSpinSin);
                    Vector.Rotate(src.VelX, src.VelY, srcConfig.AngularFrameVelCos, srcConfig.AngularFrameVelSin, out dstVelXFloat, out dstVelYFloat);
                } else {
                    Vector.Rotate(src.SpinCos, src.SpinSin, srcConfig.AngularFrameVelCos, -srcConfig.AngularFrameVelSin, out dstSpinCos, out dstSpinSin);
                    Vector.Rotate(src.VelX, src.VelY, srcConfig.AngularFrameVelCos, -srcConfig.AngularFrameVelSin, out dstVelXFloat, out dstVelYFloat);
                }
            }
            dstVelX = 0 < dstVelXFloat ? (int)Math.Ceiling(dstVelXFloat) : (int)Math.Floor(dstVelXFloat);
            dstVelY = 0 < dstVelYFloat ? (int)Math.Ceiling(dstVelYFloat) : (int)Math.Floor(dstVelYFloat);
            return true;
        }

        private static void _moveAndInsertBulletColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Bullet> currRenderFrameBullets, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, ref int colliderCnt, CollisionSpace collisionSys, ref int bulletCnt, Vector[] effPushbacks, ref SatResult overlapResult, Collision collision, ILoggerBridge logger) {
            int rdfId = currRenderFrame.Id;
            for (int i = 0; i < currRenderFrameBullets.Count; i++) {
                var src = currRenderFrameBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BulletLocalId) break;
                var (_, srcConfig) = FindBulletConfig(src.SkillId, src.ActiveSkillHit);
                if (null == srcConfig) {
                    continue;
                }
                var dst = nextRenderFrameBullets[bulletCnt];
                var dstVelX = src.VelX;
                var dstVelY = src.VelY + (srcConfig.TakesGravity && IsBulletActive(src, srcConfig, currRenderFrame.Id) ? GRAVITY_Y_JUMP_HOLDING : 0);
                if (dstVelY < DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID) {
                    dstVelY = DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID;
                }

                var dstDirX = src.DirX;
                var dstDirY = src.DirY;
                float dstSpinCos = src.SpinCos, dstSpinSin = src.SpinSin;
                bool spinFlipX = (0 > dstDirX);
                var dstTargetChJoinIndex = src.TargetCharacterJoinIndex;

                // [WARNING] Handle spinning of velocities first!
                if (IsBulletActive(src, srcConfig, currRenderFrame.Id)) {
                    handleBulletVelSpinning(currRenderFrame, roomCapacity, srcConfig, dstTargetChJoinIndex, spinFlipX, src, ref dstVelX, ref dstVelY, ref dstSpinCos, ref dstSpinSin);
                }

                AssignToBullet(
                        src.BulletLocalId,
                        src.OriginatedRenderFrameId,
                        src.OffenderJoinIndex,
                        src.OffenderTrapLocalId,
                        src.TeamId,
                        src.BlState, src.FramesInBlState + 1,
                        src.OriginatedVirtualGridX, src.OriginatedVirtualGridY,
                        src.VirtualGridX, src.VirtualGridY, // virtual grid position
                        dstDirX, dstDirY, // dir
                        dstVelX, dstVelY, // velocity
                        src.ActiveSkillHit, src.SkillId, src.VertMovingTrapLocalIdUponActive,
                        srcConfig.RepeatQuota,
                        src.RemainingHardPushbackBounceQuota,
                        dstTargetChJoinIndex, // [WARNING] Still prone to change in the coming calculations, but already used to determine spin-related fields
                        dstSpinCos, dstSpinSin, // spin
                        src.DamageDealed,
                        src.ExplodedOnIfc,
                        dst);

                Trap? offenderTrap = null;
                if (TERMINATING_TRAP_ID != dst.OffenderTrapLocalId) {
                    offenderTrap = nextRenderFrameTraps[dst.OffenderTrapLocalId-1];
                }
                CharacterDownsync? offender = null;
                if (1 <= dst.OffenderJoinIndex && dst.OffenderJoinIndex <= (roomCapacity + npcCnt)) {
                    // Although "nextRenderFrameNpcs" is terminated by a special "id", a bullet could reference an npc instance outside of termination by "OffenderJoinIndex" and thus get "contaminated data from reused memory" -- the rollback netcode implemented by this project only guarantees "eventual correctness" within the termination bounds of "playersArr/npcsArr/bulletsArr" while incorrect predictions could remain outside of the bounds. 
                    offender = getChdFromRdf(dst.OffenderJoinIndex, roomCapacity, currRenderFrame);
                }

                if (!IsBulletAlive(dst, srcConfig, currRenderFrame.Id)) {
                    continue;
                }

                if (srcConfig.BeamCollision) {
                    if (null != offender && offender.ActiveSkillId != src.SkillId) {
                        // [WARNING] Such that a multi-hit beam stops as the offender recovers
                        continue;
                    }
                }

                if (BulletType.Melee == srcConfig.BType && !IsBulletExploding(src, srcConfig)) {
                    if (null != offender && offender.ActiveSkillId != src.SkillId) {
                        // [WARNING] Such that a multi-hit beam stops as the offender recovers
                        continue;
                    }
                }

                bool isBulletRotary = IsBulletRotary(srcConfig);
                if (BulletType.Melee == srcConfig.BType) {
                    if (null == offender && null == offenderTrap) continue;
                    if (null != offender && noOpSet.Contains(offender.CharacterState) && !IsBulletExploding(dst, srcConfig)) {
                        // If a melee is alive but the offender got attacked, remove it even if it's active
                        continue;
                    }
                    if (IsBulletActive(dst, srcConfig, currRenderFrame.Id)) {
                        var (newVx, newVy) = (0, 0);
                        if (null != offender) {
                            (newVx, newVy) = (offender.VirtualGridX + dst.DirX * srcConfig.HitboxOffsetX, offender.VirtualGridY + srcConfig.HitboxOffsetY);
                        } else if (null != offenderTrap) {
                            (newVx, newVy) = (offenderTrap.VirtualGridX + dst.DirX * srcConfig.HitboxOffsetX, offenderTrap.VirtualGridY + + srcConfig.HitboxOffsetY);
                        }
                        var (bulletCx, bulletCy) = VirtualGridToPolygonColliderCtr(newVx, newVy);
                        var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(srcConfig.HitboxSizeX + srcConfig.HitboxSizeIncX * (int)src.FramesInBlState, srcConfig.HitboxSizeY + srcConfig.HitboxSizeIncY * (int)src.FramesInBlState);
                        var newBulletCollider = dynamicRectangleColliders[colliderCnt];
                        UpdateRectCollider(newBulletCollider, bulletCx, bulletCy, hitboxSizeCx, hitboxSizeCy, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst, srcConfig.CollisionTypeMask, spinFlipX, isRotary: isBulletRotary, srcConfig.SpinAnchorX, srcConfig.SpinAnchorY, dst.SpinCos, dst.SpinSin);
                        effPushbacks[colliderCnt].X = 0;
                        effPushbacks[colliderCnt].Y = 0;
                        colliderCnt++;

                        collisionSys.AddSingleToCellTail(newBulletCollider);
                        dst.VirtualGridX = newVx;
                        dst.VirtualGridY = newVy;
                        dst.BlState = BulletState.Active;
                        if (dst.BlState != src.BlState) {
                            dst.FramesInBlState = 0;
                        }

                        if (MultiHitType.FromVisionSeekOrDefault == srcConfig.MhType) {
                            if (MAGIC_JOIN_INDEX_INVALID == dst.TargetCharacterJoinIndex) {
                                _updateBulletTargetJoinIndexByVision(src, dst, srcConfig, spinFlipX, dynamicRectangleColliders, colliderCnt, collisionSys, ref overlapResult, collision, logger);
                            }
                        }
                    }

                    bulletCnt++;
                } else if (BulletType.Fireball == srcConfig.BType || BulletType.GroundWave == srcConfig.BType || BulletType.MissileLinear == srcConfig.BType) {
                    if (IsBulletActive(dst, srcConfig, currRenderFrame.Id)) {
                        var (proposedNewVx, proposedNewVy) = (src.VirtualGridX + src.VelX, src.VirtualGridY + src.VelY);
                        if (BulletState.StartUp == src.BlState) {
                            dst.BlState = BulletState.Active;
                            dst.FramesInBlState = 0;
                            if (srcConfig.AllowsWalking && null != offender && (WalkingAtk1 == offender.CharacterState || WalkingAtk4 == offender.CharacterState)) {
                                proposedNewVx = (offender.VirtualGridX + src.VelX);
                                dst.OriginatedVirtualGridX = proposedNewVx;   
                            }
                        }
                        if (!srcConfig.BeamCollision) {
                            var (bulletCx, bulletCy) = VirtualGridToPolygonColliderCtr(proposedNewVx, proposedNewVy);
                            var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(srcConfig.HitboxSizeX + srcConfig.HitboxSizeIncX * src.FramesInBlState, srcConfig.HitboxSizeY + srcConfig.HitboxSizeIncY * src.FramesInBlState);
                            var newBulletCollider = dynamicRectangleColliders[colliderCnt];
                            float overlap = (BulletType.GroundWave == srcConfig.BType ? GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP : SNAP_INTO_PLATFORM_OVERLAP);
                            UpdateRectCollider(newBulletCollider, bulletCx, bulletCy, hitboxSizeCx, hitboxSizeCy, overlap, overlap, overlap, overlap, 0, 0, dst, srcConfig.CollisionTypeMask, spinFlipX, isRotary: isBulletRotary, srcConfig.SpinAnchorX, srcConfig.SpinAnchorY, dst.SpinCos, dst.SpinSin);
                            effPushbacks[colliderCnt].X = 0;
                            effPushbacks[colliderCnt].Y = 0;
                            colliderCnt++;

                            collisionSys.AddSingleToCellTail(newBulletCollider);
                            (dst.VirtualGridX, dst.VirtualGridY) = (proposedNewVx, proposedNewVy);

                            // [WARNING] There's no support for missile beam yet! 
                            if (BulletType.MissileLinear == srcConfig.BType) {
                                if (!srcConfig.HopperMissile) {
                                    // regular missile
                                    if (0 < srcConfig.MissileSearchIntervalPow2Minus1 && 0 == (src.FramesInBlState & srcConfig.MissileSearchIntervalPow2Minus1)) {
                                        _updateBulletTargetJoinIndexByVision(src, dst, srcConfig, spinFlipX, dynamicRectangleColliders, colliderCnt, collisionSys, ref overlapResult, collision, logger);
                                    }
                                } else {
                                    // hopper-missile
                                    if (MAGIC_JOIN_INDEX_INVALID == dst.TargetCharacterJoinIndex) {
                                        _updateBulletTargetJoinIndexByVision(src, dst, srcConfig, spinFlipX, dynamicRectangleColliders, colliderCnt, collisionSys, ref overlapResult, collision, logger);
                                        dstTargetChJoinIndex = dst.TargetCharacterJoinIndex;
                                        if (MAGIC_JOIN_INDEX_INVALID == dstTargetChJoinIndex) {
                                            // No next target, drop this bullet
                                            continue;
                                        }

                                        var targetChNextFrame = getChdFromChdArrs(dstTargetChJoinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
                                        if (null == targetChNextFrame) {
                                            // No next target, drop this bullet
                                            continue;
                                        }
                                        int diffX = (targetChNextFrame.VirtualGridX - src.VirtualGridX);
                                        int diffY = (targetChNextFrame.VirtualGridY - src.VirtualGridY);

                                        var diffMagSq = diffX * diffX + diffY * diffY;
                                        var invDiffMag = InvSqrt32(diffMagSq);
                                        var speedXfac = invDiffMag * diffX;
                                        var speedYfac = invDiffMag * diffY;

                                        int nextVelX = (int)(speedXfac * srcConfig.Speed);
                                        int nextVelY = (int)(speedYfac * srcConfig.Speed);
                                        dst.VelX = nextVelX;
                                        dst.VelY = nextVelY;
                                        dst.DirX = 0 > nextVelX ? -2 : +2;
                                    }
                                }
                            }
                        } else {
                            var (bulletCx, bulletCy) = VirtualGridToPolygonColliderCtr(src.OriginatedVirtualGridX, proposedNewVy);
                            var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(src.VirtualGridX - src.OriginatedVirtualGridX, srcConfig.HitboxSizeY + srcConfig.HitboxSizeIncY * src.FramesInBlState);
                            var newBulletCollider = dynamicRectangleColliders[colliderCnt];
                            UpdateRectCollider(newBulletCollider, bulletCx + 0.5f * hitboxSizeCx, bulletCy, 0 < hitboxSizeCx ? hitboxSizeCx : -hitboxSizeCx, hitboxSizeCy, 0, 0, 0, 0, 0, 0, dst, srcConfig.CollisionTypeMask, spinFlipX, isRotary: isBulletRotary, srcConfig.SpinAnchorX, srcConfig.SpinAnchorY, dst.SpinCos, dst.SpinSin);
                            /*
                            if (BulletState.Active == src.BlState && src.FramesInBlState < 10) {
                                logger.LogInfo(String.Format("@rdfId={0}, active beam bullet isRotary={1}, newBulletCollider.Shape={2}", rdfId, isBulletRotary, newBulletCollider.Shape.ToString(true)));
                            }
                            */
                            effPushbacks[colliderCnt].X = 0;
                            effPushbacks[colliderCnt].Y = 0;
                            colliderCnt++;

                            collisionSys.AddSingleToCellTail(newBulletCollider);
                            if (BulletState.StartUp == src.BlState) {
                                dst.BlState = BulletState.Active;
                                dst.FramesInBlState = 0;
                            }
                            (dst.VirtualGridX, dst.VirtualGridY) = (proposedNewVx, proposedNewVy);
                        }
                    } else if (MultiHitType.None == srcConfig.MhType && (null != offender && noOpSet.Contains(offender.CharacterState)) && !IsBulletExploding(dst, srcConfig)) {
                        // If a fireball is not yet active but the offender got attacked, remove it
                        continue;
                    }
                    bulletCnt++;
                } else {
                    continue;
                }
            }

            // Explicitly specify termination of nextRenderFrameBullets
            if (bulletCnt < nextRenderFrameBullets.Count) nextRenderFrameBullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
        }

        private static void _insertFromEmissionDerivedBullets(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> currRenderFrameBullets, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            bool dummyHasLockVel = false; // Would be ALWAYS false when used within this function bcz we're only adding subsequent multihit bullets!
            for (int i = 0; i < currRenderFrameBullets.Count; i++) {
                var src = currRenderFrameBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BulletLocalId) break;
                if (1 > src.OffenderJoinIndex || src.OffenderJoinIndex > (roomCapacity + npcCnt)) {
                    // Although "nextRenderFrameNpcs" is terminated by a special "id", a bullet could reference an npc instance outside of termination by "OffenderJoinIndex" and thus get "contaminated data from reused memory" -- the rollback netcode implemented by this project only guarantees "eventual correctness" within the termination bounds of "playersArr/npcsArr/bulletsArr" while incorrect predictions could remain outside of the bounds.
                    continue;
                }
                var offender = getChdFromRdf(src.OffenderJoinIndex, roomCapacity, currRenderFrame);
                var offenderNextFrame = getChdFromChdArrs(src.OffenderJoinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);

                bool isSameBullet = (offender.ActiveSkillId == src.SkillId && offender.ActiveSkillHit == src.ActiveSkillHit);
                /*
                if (77 == offender.ActiveSkillId) {
                    logger.LogInfo("_insertFromEmissionDerivedBullets/before, currRdfId=" + currRenderFrame.Id + ", used DiverImpact, next VelX = " + offenderNextFrame.VelX);
                }
                */
                
                var (skillConfig, srcConfig) = FindBulletConfig(src.SkillId, src.ActiveSkillHit);
                if (null == skillConfig || null == srcConfig) {
                    continue;
                }

                bool isExploding = IsBulletExploding(src, srcConfig);
                if (isExploding) {
                    continue;
                }

                if ((MultiHitType.FromEmission != srcConfig.MhType && MultiHitType.FromEmissionJustActive != srcConfig.MhType) || src.ActiveSkillHit + 1 > skillConfig.Hits.Count) {
                    continue;
                }

                if (!isSameBullet && (BulletType.Melee == srcConfig.BType || MultiHitType.FromEmissionJustActive == srcConfig.MhType)) {
                    continue;
                }

                bool justEndedCurrentHit = (MultiHitType.FromEmission == srcConfig.MhType && (src.OriginatedRenderFrameId + srcConfig.StartupFrames + srcConfig.ActiveFrames == currRenderFrame.Id));

                bool justBecameActive = (MultiHitType.FromEmissionJustActive == srcConfig.MhType && IsBulletJustActive(src, srcConfig, currRenderFrame.Id));

                if (justEndedCurrentHit || justBecameActive) {
                    // [WARNING] Different from Fireball, multihit of Melee would add a new "Bullet" to "nextRenderFrameBullets" for convenience of handling explosion! The bullet "dst" could also be exploding by reaching here!
                    // No need to worry about Mp consumption here, it was already paid at the first "offenderNextFrame.ActiveSkillHit" in "_useSkill"
                    int xfac = (0 < offenderNextFrame.DirX ? 1 : -1);
                    var existingDebuff = offender.DebuffList[DEBUFF_ARR_IDX_ELEMENTAL];
                    bool isParalyzed = (TERMINATING_DEBUFF_SPECIES_ID != existingDebuff.SpeciesId && 0 < existingDebuff.Stock && DebuffType.PositionLockedOnly == debuffConfigs[existingDebuff.SpeciesId].Type);
                    if (addNewBulletToNextFrame(src.OriginatedRenderFrameId, currRenderFrame.Id, offender, offenderNextFrame, characters[offender.SpeciesId], isParalyzed, xfac, skillConfig, nextRenderFrameBullets, src.ActiveSkillHit + 1, src.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, (srcConfig.BeamCollision ? src : null), (srcConfig.BeamCollision ? srcConfig : null), src, null, src.OffenderJoinIndex, src.TeamId, logger)) {
                        var targetNewBullet = nextRenderFrameBullets[bulletCnt - 1];
                        offenderNextFrame.ActiveSkillHit = targetNewBullet.ActiveSkillHit;
                        var (_, newBlConfig) = FindBulletConfig(targetNewBullet.SkillId, targetNewBullet.ActiveSkillHit);
                        if (null != newBlConfig) {
                            if (offenderNextFrame.FramesInvinsible < newBlConfig.StartupInvinsibleFrames) {
                                offenderNextFrame.FramesInvinsible = newBlConfig.StartupInvinsibleFrames;
                            }
                            /*
                            if (SPECIES_DEMON_FIRE_SLIME == offender.SpeciesId) {
                                logger.LogInfo("currRdfId = " + currRenderFrame.Id + ", add new bullet by emission time offset = \n" + bulletConfig.ToString() + ", for offender.JoinIndex = " + offender.JoinIndex);
                            }
                            */
                        }
                    }
                    /*
                    if (77 == offender.ActiveSkillId) {
                        logger.LogInfo("_insertFromEmissionDerivedBullets/after, currRdfId=" + currRenderFrame.Id + ", used DiverImpact, next VelX = " + offenderNextFrame.VelX);
                    }
                    */
                }
            }
        }

        private static bool _calcBulletBounceOrExplosionOnHardPushback(int rdfId, Bullet bulletNextFrame, BulletConfig bulletConfig, Vector effPushback, in SatResult primaryOverlapResult, TrapColliderAttr? primaryTrapColliderAttr, CharacterDownsync? offender, CharacterDownsync? offenderNextFrame, bool isMeleeBouncer, ILoggerBridge logger) {
            if (BulletType.Melee != bulletConfig.BType && BulletType.Fireball != bulletConfig.BType && BulletType.MissileLinear != bulletConfig.BType) {
                throw new ArgumentNullException(String.Format("This method shouldn't be called on a bullet without Melee/Fireball/MissileLinear type! bulletNextFrame={0}, bulletConfig={1}", bulletNextFrame, bulletConfig));
            }
            if (bulletConfig.HopperMissile || 0 >= bulletNextFrame.RemainingHardPushbackBounceQuota) {
                // [WARNING] HopperMissile seeks next (VelX, VelY) right after explosion!
                return true;
            }
            if (!isMeleeBouncer) {
                effPushback.X += primaryOverlapResult.OverlapMag * primaryOverlapResult.OverlapX;
                effPushback.Y += primaryOverlapResult.OverlapMag * primaryOverlapResult.OverlapY;
            }
            /*
            [Reminder] (primaryOverlapResult.OverlapX, primaryOverlapResult.OverlapY) points inside the slope. 
            */

            int origVelX = (isMeleeBouncer && null != offenderNextFrame) ? offenderNextFrame.VelX : bulletNextFrame.VelX, origVelY = (isMeleeBouncer && null != offenderNextFrame) ? offenderNextFrame.VelY : bulletNextFrame.VelY;
            float projectedVel = (origVelX * primaryOverlapResult.OverlapX + origVelY * primaryOverlapResult.OverlapY); // This value is actually in VirtualGrid unit, but converted to float, thus it'd be eventually rounded 
            if (0 >= projectedVel) {
                return false;
            }

            bulletNextFrame.RemainingHardPushbackBounceQuota -= 1;
            /*
            # VelocityPerpendicularIntoSlope = (projectedVel*primaryOverlapResult.OverlapX, projectedVel*primaryOverlapResult.OverlapY)
            # VelocityParallelWithSlope = (bulletNextFrame.VelX, bulletNextFrame.VelY) - VelocityPerpendicularIntoSlope 
            # NewVel = VelocityParallelWithSlope*SheerFactor + (-VelocityPerpendicularIntoSlope*NormFactor) 
            */

            float projectedVelX = primaryOverlapResult.OverlapX * projectedVel, projectedVelY = primaryOverlapResult.OverlapY * projectedVel;
            float newNormVelXApprox = bulletConfig.HardPushbackBounceNormFactor * (-projectedVelX);
            float newNormVelYApprox = bulletConfig.HardPushbackBounceNormFactor * (-projectedVelY);
            float newSheerVelXApprox = bulletConfig.HardPushbackBounceSheerFactor * (origVelX - projectedVelX);
            float newSheerVelYApprox = bulletConfig.HardPushbackBounceSheerFactor * (origVelY - projectedVelY);

            float newVelXApprox = newNormVelXApprox + newSheerVelXApprox;
            float newVelYApprox = newNormVelYApprox + newSheerVelYApprox;
            if (isMeleeBouncer && null != offenderNextFrame) {
                int newOffenderVelX = 0 > newVelXApprox ? (int)Math.Floor(newVelXApprox) : (int)Math.Ceiling(newVelXApprox);
                int newOffenderVelY = 0 > newVelYApprox ? (int)Math.Floor(newVelYApprox) : (int)Math.Ceiling(newVelYApprox); 
                offenderNextFrame.VelX = newOffenderVelX;
                offenderNextFrame.VelY = newOffenderVelY;
                // logger.LogInfo("@rdfId=" + rdfId + ", meleeBounce for offender.JoinIndex=" + offenderNextFrame.JoinIndex + ", (newOffenderVelX=" + offenderNextFrame.VelX + ", newOffenderVelY=" + offenderNextFrame.VelY + "), #1:newVelMag=" + (offenderNextFrame.VelX * offenderNextFrame.VelX + offenderNextFrame.VelY * offenderNextFrame.VelY));

            } else {
                bulletNextFrame.VelX = 0 > newVelXApprox ? (int)Math.Floor(newVelXApprox) : (int)Math.Ceiling(newVelXApprox);
                bulletNextFrame.VelY = 0 > newVelYApprox ? (int)Math.Floor(newVelYApprox) : (int)Math.Ceiling(newVelYApprox);
            }

            if (null != primaryTrapColliderAttr) {
                var trapConfig = trapConfigs[primaryTrapColliderAttr.SpeciesId];
                if (isMeleeBouncer && null != offenderNextFrame) {
                    offenderNextFrame.VelX += trapConfig.ConstFrictionVelXTop;
                } else {
                    bulletNextFrame.VelX += trapConfig.ConstFrictionVelXTop;
                }
            }

            /*
            if (isMeleeBouncer && SPECIES_STONE_GOLEM == offenderNextFrame.SpeciesId && Atk2 == offenderNextFrame.CharacterState && 40 <= offenderNextFrame.FramesInChState) {
                logger.LogInfo("_calcBulletBounceOrExplosionOnHardPushback/end, currRdfId=" + rdfId + ", (VelX = " + offender.VelX + ", VelY = " + offender.VelY + "), (NextVelX = " + offenderNextFrame.VelX + ", NextVelY = " + offenderNextFrame.VelY + "). primaryOverlapResult = " + primaryOverlapResult.ToString());
            }
            */
            return false;
        }

        private static void _assignExplodedOnHardPushback(RoomDownsyncFrame currRenderFrame, Bullet bulletNextFrame, Vector effPushback, SatResult primaryOverlapResult, TrapColliderAttr? primaryTrapColliderAttr, CharacterDownsync? offender, CharacterDownsync? offenderNextFrame, ref bool exploded, ref bool explodedOnHardPushback, ref IfaceCat anotherHarderBulletIfc, bool potentiallyInTheMiddleOfPrevHitMhTransition, BulletConfig bulletConfig, ILoggerBridge logger) {
            bool isMeleeBouncer = (BulletType.Melee == bulletConfig.BType && null != offender && null != offenderNextFrame);
            if (bulletConfig.NoExplosionOnHardPushback && !isMeleeBouncer) return;
            if (0 < bulletConfig.DefaultHardPushbackBounceQuota) {
                exploded = _calcBulletBounceOrExplosionOnHardPushback(currRenderFrame.Id, bulletNextFrame, bulletConfig, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, isMeleeBouncer, logger);
            } else {
                exploded = true;
            }
            if (!exploded) return;
            explodedOnHardPushback = (!potentiallyInTheMiddleOfPrevHitMhTransition || bulletConfig.GroundImpactMeleeCollision || bulletConfig.WallImpactMeleeCollision || BulletType.GroundWave == bulletConfig.BType);
            if (!explodedOnHardPushback) return;
            anotherHarderBulletIfc = IfaceCat.Rock;
        }

        private static bool _deriveFromVisionSingleBullet(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, int xfac, Bullet bulletNextFrame, BulletConfig bulletConfig, ref int bulletLocalIdCounter, ref int bulletCnt, ref bool dummyHasLockVel, CharacterDownsync? offender, CharacterDownsync? offenderNextFrame, Skill? skillConfig, ILoggerBridge logger) {
            if (null == offender || null == offenderNextFrame || null == skillConfig || bulletNextFrame.ActiveSkillHit+1 > skillConfig.Hits.Count) {
                return false;
            }
            int targetChJoinIndex = bulletNextFrame.TargetCharacterJoinIndex;
            
            // Silently retires the starter bullet and use the default offset
            bulletNextFrame.BlState = BulletState.Exploding; // Such that no collider from next rdf on
            bulletNextFrame.FramesInBlState = 1 + bulletConfig.ExplosionFrames;

            CharacterDownsync? targetChNextFrame = null;
            if (MAGIC_JOIN_INDEX_INVALID != targetChJoinIndex) {
               targetChNextFrame = getChdFromChdArrs(targetChJoinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);;
            }
            var existingDebuff = offender.DebuffList[DEBUFF_ARR_IDX_ELEMENTAL];
            bool isParalyzed = (TERMINATING_DEBUFF_SPECIES_ID != existingDebuff.SpeciesId && 0 < existingDebuff.Stock && DebuffType.PositionLockedOnly == debuffConfigs[existingDebuff.SpeciesId].Type);

            bool res = addNewBulletToNextFrame(currRenderFrame.Id, currRenderFrame.Id, offender, offenderNextFrame, characters[offender.SpeciesId], isParalyzed, xfac, skillConfig, nextRenderFrameBullets, bulletNextFrame.ActiveSkillHit + 1, bulletNextFrame.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, bulletNextFrame, bulletConfig, bulletNextFrame, targetChNextFrame, bulletNextFrame.OffenderJoinIndex, bulletNextFrame.TeamId, logger); 
            if (!res) return false;
            var targetNewBullet = nextRenderFrameBullets[bulletCnt - 1];
            var (_, newBlConfig) = FindBulletConfig(targetNewBullet.SkillId, targetNewBullet.ActiveSkillHit);
            if (null != newBlConfig) {
                offenderNextFrame.ActiveSkillHit = targetNewBullet.ActiveSkillHit;
                if (offenderNextFrame.FramesInvinsible < newBlConfig.StartupInvinsibleFrames) {
                    offenderNextFrame.FramesInvinsible = newBlConfig.StartupInvinsibleFrames;
                }
            }
            return res;
        }

        private static void _handleNonVisionSingleBulletHardPushbacks(RoomDownsyncFrame currRenderFrame, Bullet bulletNextFrame, BulletConfig bulletConfig, Vector effPushback, TrapColliderAttr? primaryTrapColliderAttr, Trap? primaryTrap, CharacterDownsync? offender, CharacterDownsync? offenderNextFrame, ref bool exploded, ref bool explodedOnHardPushback, ref IfaceCat anotherHarderBulletIfc, ref bool beamBlockedByHardPushback, bool potentiallyInTheMiddleOfPrevHitMhTransition, in SatResult primaryOverlapResult, ILoggerBridge logger) {
            if (BulletType.GroundWave == bulletConfig.BType) {
                /*
                if (1 < hardPushbackCnt) {
                    logger.LogInfo("@rdfId= " + currRenderFrame.Id + ", groundWave bullet " + bulletNextFrame.BulletLocalId + " got " + hardPushbackCnt + " hardPushbacks: " + Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt) + ", primaryHardOverlapIndex=" + primaryHardOverlapIndex);
                }
                */
                effPushback.X += (primaryOverlapResult.OverlapMag - GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP) * primaryOverlapResult.OverlapX;
                effPushback.Y += (primaryOverlapResult.OverlapMag - GROUNDWAVE_SNAP_INTO_PLATFORM_OVERLAP) * primaryOverlapResult.OverlapY;
                float normAlignmentWithGravity = (primaryOverlapResult.OverlapY * -1f); // [WARNING] "calcHardPushbacksNormsForBullet" takes wall for a higher priority than flat ground!  
                if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                    // [WARNING] i.e. landedOnGravityPushback = true
                    // Kindly remind that (primaryOverlapResult.OverlapX, primaryOverlapResult.OverlapY) points INTO the slope :) 
                    float projectedVel = (bulletNextFrame.VelX * primaryOverlapResult.OverlapX + bulletNextFrame.VelY * primaryOverlapResult.OverlapY); // This value is actually in VirtualGrid unit, but converted to float, thus it'd be eventually rounded 
                    int oldBulletNextFrameVelX = bulletNextFrame.VelX;
                    float newVelXApprox = bulletNextFrame.VelX - primaryOverlapResult.OverlapX * projectedVel;
                    float newVelYApprox = bulletNextFrame.VelY - primaryOverlapResult.OverlapY * projectedVel;
                    bulletNextFrame.VelX = 0 > newVelXApprox ? (int)Math.Floor(newVelXApprox) : (int)Math.Ceiling(newVelXApprox);
                    bulletNextFrame.VelY = (int)Math.Floor(newVelYApprox); // "VelY" here is < 0, take the floor to get a larger absolute value!
                    if (bulletConfig.IgnoreSlopeDeceleration) {
                        bulletNextFrame.VelX = oldBulletNextFrameVelX;
                    }
                    if (null != primaryTrapColliderAttr) {
                        var trapConfig = trapConfigs[primaryTrapColliderAttr.SpeciesId];
                        bulletNextFrame.VelX += trapConfig.ConstFrictionVelXTop;
                    }
                } else {
                    // [WARNING] GroundWave hitting a wall
                    _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                }
            } else if (BulletType.Fireball == bulletConfig.BType || BulletType.MissileLinear == bulletConfig.BType) {
                if (null != primaryTrap) {
                    bool bulletJustBecameActive = IsBulletJustActive(bulletNextFrame, bulletConfig, currRenderFrame.Id + 1);
                    bool bulletIsStillActive = IsBulletActive(bulletNextFrame, bulletConfig, currRenderFrame.Id + 1);
                    if (bulletJustBecameActive) {
                        float normAlignmentWithGravity = (primaryOverlapResult.OverlapY * -1f);
                        bool landedOnGravityPushback = (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity);
                        if (landedOnGravityPushback && 0 < primaryTrap.VelY && (null != offenderNextFrame && primaryTrap.VelY == offenderNextFrame.FrictionVelY)) {
                            bulletNextFrame.VertMovingTrapLocalIdUponActive = primaryTrap.TrapLocalId;
                            effPushback.X += primaryOverlapResult.OverlapMag * primaryOverlapResult.OverlapX;
                            effPushback.Y += primaryOverlapResult.OverlapMag * primaryOverlapResult.OverlapY;
                            //logger.LogInfo(String.Format("@rdf.Id={0}, bulletLocalId={1} marks VertMovingTrapLocalIdUponActive={2}", currRenderFrame.Id, bulletNextFrame.BulletLocalId, primaryTrap.TrapLocalId));
                        } else {
                            _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                        }
                    } else if (bulletIsStillActive && primaryTrap.TrapLocalId == bulletNextFrame.VertMovingTrapLocalIdUponActive) {
                        // [WARNING] Neither "landedOnGravityPushback" nor "primaryTrap.VelY" matters in this case! Once remembered this bullet will pass thru this specific "VertMovingTrapLocalIdUponActive" from all sides! 
                        effPushback.X += primaryOverlapResult.OverlapMag * primaryOverlapResult.OverlapX;
                        effPushback.Y += primaryOverlapResult.OverlapMag * primaryOverlapResult.OverlapY;
                        //logger.LogInfo(String.Format("@rdf.Id={0}, bulletLocalId={1} rides on VertMovingTrapLocalIdUponActive={2}", currRenderFrame.Id, bulletNextFrame.BulletLocalId, primaryTrap.TrapLocalId));
                    } else {
                        if (!bulletConfig.BeamCollision) {
                            _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                        } else {
                            // [WARNING] DON'T explode the beam in this case!
                            beamBlockedByHardPushback = true;
                            effPushback.X += (0 < bulletNextFrame.DirX ? primaryOverlapResult.OverlapMag : -primaryOverlapResult.OverlapMag);
                            bulletNextFrame.VelX = 0;
                            bulletNextFrame.VelY = 0;
                        }
                    }
                } else {
                    if (!bulletConfig.BeamCollision) {
                        _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                    } else {
                        // [WARNING] DON'T explode the beam in this case!
                        beamBlockedByHardPushback = true;
                        effPushback.X += (0 < bulletNextFrame.DirX ? primaryOverlapResult.OverlapMag : -primaryOverlapResult.OverlapMag);
                        bulletNextFrame.VelX = 0;
                        bulletNextFrame.VelY = 0;
                    }
                }
            } else {
                // [WARNING] If the bullet "collisionTypeMask" is barrier penetrating, it'd not have reached "0 < hardPushbackCnt".
                _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
            }
        }

        private static void _calcSingleBulletCollision(RoomDownsyncFrame currRenderFrame, Bullet bulletNextFrame, BulletConfig bulletConfig, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Bullet> nextRenderFrameBullets, RepeatedField<Trigger> nextRenderFrameTriggers, ref int bulletLocalIdCounter, ref int bulletCnt, ref bool dummyHasLockVel, Collider bulletCollider, Vector[] hardPushbackNorms, Vector effPushback, FrameRingBuffer<Collider> residueCollided, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToTiledConfig, ILoggerBridge logger) {
            CharacterDownsync? offender = null, offenderNextFrame = null;
            if (1 <= bulletNextFrame.OffenderJoinIndex && bulletNextFrame.OffenderJoinIndex <= (roomCapacity + npcCnt)) {
                offender = getChdFromRdf(bulletNextFrame.OffenderJoinIndex, roomCapacity, currRenderFrame);
                offenderNextFrame = getChdFromChdArrs(bulletNextFrame.OffenderJoinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
            }

            Trap? offenderTrap = null, offenderTrapNextFrame = null;
            if (TERMINATING_TRAP_ID != bulletNextFrame.OffenderTrapLocalId) {
                offenderTrap = currRenderFrame.TrapsArr[bulletNextFrame.OffenderTrapLocalId - 1];
                offenderTrapNextFrame = nextRenderFrameTraps[bulletNextFrame.OffenderTrapLocalId - 1];
            }
            int effDirX = bulletNextFrame.DirX;
            if (BulletType.Melee == bulletConfig.BType) {
                if (null != offender) {
                    effDirX = offender.DirX;
                } else if (null != offenderTrap) {
                    effDirX = offenderTrap.DirX;
                } else {
                    return;
                }
            }
            int xfac = (0 < effDirX ? 1 : -1);
            Skill? skillConfig = (NO_SKILL != bulletNextFrame.SkillId ? skills[bulletNextFrame.SkillId] : null);

            var origFramesInActiveState = bulletNextFrame.FramesInBlState; // [WARNING] By entering "_calcAllBulletsCollisions", the "bulletNextFrame" attached to collider can only be active.

            if (MultiHitType.FromVisionSeekOrDefault == bulletConfig.MhType) {
                _deriveFromVisionSingleBullet(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameBullets, xfac, bulletNextFrame, bulletConfig, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, offender, offenderNextFrame, skillConfig, logger);
                return;
            }

            var bulletShape = bulletCollider.Shape;
            int primaryHardOverlapIndex;
            Trap? primaryTrap;
            TrapColliderAttr? primaryTrapColliderAttr;

            int hardPushbackCnt = calcHardPushbacksNormsForBullet(currRenderFrame, bulletNextFrame, bulletCollider, bulletShape, hardPushbackNorms, residueCollided, collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, out primaryTrap, out primaryTrapColliderAttr, logger);

            bool exploded = false;
            bool explodedOnCh = false;
            bool explodedOnHardPushback = false;
            bool explodedOnAnotherHarderBullet = false;
            bool beamBlockedByHardPushback = false;
            bool everAddedExplosion = false;
            IfaceCat anotherHarderBulletIfc = IfaceCat.Empty;

            bool potentiallyInTheMiddleOfPrevHitMhTransition = (null != skillConfig) && (MultiHitType.FromPrevHitAnyway == bulletConfig.MhType || MultiHitType.FromPrevHitActual == bulletConfig.MhType || MultiHitType.FromPrevHitActualOrActiveTimeUp == bulletConfig.MhType) && (bulletNextFrame.ActiveSkillHit < skillConfig.Hits.Count);

            if (0 < hardPushbackCnt) {
                _handleNonVisionSingleBulletHardPushbacks(currRenderFrame, bulletNextFrame, bulletConfig, effPushback, primaryTrapColliderAttr, primaryTrap, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, ref beamBlockedByHardPushback, potentiallyInTheMiddleOfPrevHitMhTransition, in primaryOverlapResult, logger);
            } else {
                if (BulletType.GroundWave == bulletConfig.BType) {
                    // GroundWave leaving platform
                    if (!bulletConfig.AirRidingGroundWave) {
                        _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                    }
                }
            }

            if (!exploded && null != offender && !offender.InAir && offender.ActiveSkillId == bulletNextFrame.SkillId && offender.ActiveSkillHit == bulletNextFrame.ActiveSkillHit) {
                if (bulletConfig.GroundImpactMeleeCollision) {
                    // [WARNING] All "GroundImpactMeleeCollision" bullets have "OmitSoftPushback" to avoid false emission!
                    _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                } else if (bulletConfig.WallImpactMeleeCollision) {
                    bool hasReconingPushback = offender.OnWall && (0 < offender.OnWallNormX * offender.VelX);
                    if (hasReconingPushback) {
                        _assignExplodedOnHardPushback(currRenderFrame, bulletNextFrame, effPushback, primaryOverlapResult, primaryTrapColliderAttr, offender, offenderNextFrame, ref exploded, ref explodedOnHardPushback, ref anotherHarderBulletIfc, potentiallyInTheMiddleOfPrevHitMhTransition, bulletConfig, logger);
                    }
                }
            }

            if (!exploded && MultiHitType.FromPrevHitActualOrActiveTimeUp == bulletConfig.MhType) {
                if (BulletState.Active == bulletNextFrame.BlState && bulletConfig.ActiveFrames <= bulletNextFrame.FramesInBlState + 3) {
                    exploded = true;
                }
            }

            while (true) {
                var (ok, bCollider) = residueCollided.Pop();
                if (false == ok || null == bCollider) {
                    break;
                }
                var defenderShape = bCollider.Shape;

                // [WARNING] Because bullets and traps are potentially rotary, if not both "aShape" and "bShape" are rectilinear we have to check axes of both to determine whether they overlapped!
                var (overlapped, _, _) = calcPushbacks(0, 0, bulletShape, defenderShape, false, false, ref overlapResult, logger);
                bool mutualContains = (overlapResult.AContainedInB || overlapResult.BContainedInA);
                if (!overlapped && !mutualContains) continue;

                if (!mutualContains && overlapResult.OverlapMag < CLAMPABLE_COLLISION_SPACE_MAG) {
                    /*
                       [WARNING] 
                       If I didn't clamp "pushbackX & pushbackY" here, there could be disagreed shape overlapping between backend and frontend, see comments around "shapeOverlappedOtherChCnt" in "Battle_dynamics". 
                     */
                    continue;
                }

                switch (bCollider.Data) {
                    case TriggerColliderAttr atkedTriggerColliderAttr:
                        var atkedTrigger = currRenderFrame.TriggersArr[atkedTriggerColliderAttr.TriggerLocalId - 1];
                        var triggerConfigFromTiled = triggerEditorIdToTiledConfig[atkedTrigger.EditorId];
                        var triggerConfig = triggerConfigs[triggerConfigFromTiled.SpeciesId];
                        if (TriggerType.TtAttack != triggerConfig.TriggerType) continue;
                        if (0 < atkedTrigger.FramesToRecover || 0 >= atkedTrigger.Quota) continue;
                        if (0 < bulletNextFrame.OffenderJoinIndex && bulletNextFrame.OffenderJoinIndex <= roomCapacity) {
                            // Only allowing Player to click type "TtAttack"
                            var atkedTriggerInNextFrame = nextRenderFrameTriggers[atkedTriggerColliderAttr.TriggerLocalId - 1];
                            atkedTriggerInNextFrame.FulfilledEvtMask = atkedTriggerInNextFrame.DemandedEvtMask; // then fired in "_calcTriggerReactions"
                            exploded = true;
                        }
                        break;
                    case CharacterDownsync victimCurrFrame:
                        if (TERMINATING_TRIGGER_ID != victimCurrFrame.SubscribesToTriggerLocalId) continue; // Skip if evtsub-triggered but but triggered yet
                        bool isAllyTargetingBl = (0 > bulletConfig.Damage);
                        if (!isAllyTargetingBl && bulletNextFrame.OffenderJoinIndex == victimCurrFrame.JoinIndex) continue;
                        if (!isAllyTargetingBl && bulletNextFrame.TeamId == victimCurrFrame.BulletTeamId) continue;
                        if (isAllyTargetingBl && bulletNextFrame.TeamId != victimCurrFrame.BulletTeamId) continue;
                        if (!isAllyTargetingBl && invinsibleSet.Contains(victimCurrFrame.CharacterState)) continue;
                        bool bulletProvidesHardPushbackOnly = (bulletConfig.ProvidesXHardPushback && 0 != overlapResult.OverlapX) || (bulletConfig.ProvidesYHardPushbackTop && 0 < overlapResult.OverlapY) || (bulletConfig.ProvidesYHardPushbackBottom && 0 > overlapResult.OverlapY);
                        if (bulletProvidesHardPushbackOnly) {
                            break;
                        }

                        var victimChConfig = characters[victimCurrFrame.SpeciesId];
                        CharacterDownsync victimNextFrame = getChdFromChdArrs(victimCurrFrame.JoinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
                        var victimExistingDebuff = victimNextFrame.DebuffList[DEBUFF_ARR_IDX_ELEMENTAL];
                        bool victimIsParalyzed = (TERMINATING_DEBUFF_SPECIES_ID != victimExistingDebuff.SpeciesId && 0 < victimExistingDebuff.Stock && DebuffType.PositionLockedOnly == debuffConfigs[victimExistingDebuff.SpeciesId].Type);
                        bool victimIsFrozen = (TERMINATING_DEBUFF_SPECIES_ID != victimExistingDebuff.SpeciesId && 0 < victimExistingDebuff.Stock && DebuffType.FrozenPositionLocked == debuffConfigs[victimExistingDebuff.SpeciesId].Type); // [WARNING] It's important to check against TERMINATING_DEBUFF_SPECIES_ID such that we're safe from array reuse contamination
                        /*
                           [WARNING] Deliberately checking conditions using "victimNextFrame" instead of "victimCurrFrame" to allow more responsive graphics. 
                         */
                        if (!isAllyTargetingBl && victimChConfig.GroundDodgeEnabledByRdfCntFromBeginning > victimNextFrame.FramesInChState) {
                            bool notDashing = isNotDashing(victimNextFrame);
                            bool dashing = !notDashing;
                            bool effInAir = isEffInAir(victimNextFrame, notDashing);
                            if (!effInAir && dashing) {
                                // [WARNING] If at a certain "rdfId", multiple "bullets" are hitting a same "victimNextFrame" who can dodge the first of these "bullets" during the current traversal in "_calcAllBulletsCollisions", then it can certainly dodge all the other "bullets" too according to the simple criteria here --  hence there's no "traversal order concern" needed.  
                                transitToGroundDodgedChState(victimNextFrame, victimChConfig, victimIsParalyzed);
                                accumulateGauge(DEFAULT_GAUGE_INC_BY_HIT, null, victimNextFrame);
                                break;
                            }
                        }

                        if (!isAllyTargetingBl && 0 < victimCurrFrame.FramesInvinsible) continue;

                        int immuneRcdI = 0;
                        bool shouldBeImmune = false;
                        if (bulletConfig.RemainsUponHit) {
                            while (immuneRcdI < victimCurrFrame.BulletImmuneRecords.Count) {
                                var candidate = victimCurrFrame.BulletImmuneRecords[immuneRcdI];
                                if (TERMINATING_BULLET_LOCAL_ID == candidate.BulletLocalId) break;
                                if (candidate.BulletLocalId == bulletNextFrame.BulletLocalId) {
                                    shouldBeImmune = true;
                                    break;
                                }
                                immuneRcdI++;
                            }
                        }

                        if (shouldBeImmune) {
                            //logger.LogInfo("joinIndex = " + victimCurrFrame.JoinIndex + " is immune to bulletLocalId= " + bulletNextFrame.BulletLocalId + " at rdfId=" + currRenderFrame.Id);a
                            break;
                        }

                        explodedOnCh = !shouldBeImmune && (!bulletConfig.RemainsUponHit || potentiallyInTheMiddleOfPrevHitMhTransition || bulletConfig.HopperMissile);
                        exploded |= explodedOnCh;

                        //logger.LogWarn(String.Format("MeleeBullet with collider:[blx:{0}, bly:{1}, w:{2}, h:{3}], bullet:{8} exploded on bCollider: [blx:{4}, bly:{5}, w:{6}, h:{7}], victimCurrFrame: {9}", bulletCollider.X, bulletCollider.Y, bulletCollider.W, bulletCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, bullet, victimCurrFrame));

                        CharacterState oldNextCharacterState = victimNextFrame.CharacterState;

                        Skill? victimActiveSkill = null;
                        BuffConfig? victimActiveSkillBuff = null;
                        if (NO_SKILL != victimNextFrame.ActiveSkillId) {
                            victimActiveSkill = skills[victimNextFrame.ActiveSkillId];
                            victimActiveSkillBuff = victimActiveSkill.SelfNonStockBuff;
                        }
                        var effDamage = 0;
                        bool successfulDef1 = false;
                        if (!shouldBeImmune) {
                            (effDamage, successfulDef1) = _calcEffDamage(currRenderFrame.Id, oldNextCharacterState, victimChConfig, victimNextFrame, victimActiveSkillBuff, bulletNextFrame, bulletConfig, isAllyTargetingBl, bulletCollider, bCollider, logger);
                            if (successfulDef1) {
                                if (bulletConfig.TakesDef1AsHardPushback) {
                                    beamBlockedByHardPushback = true;
                                } else {
                                    accumulateGauge(DEFAULT_GAUGE_INC_BY_HIT, null, victimNextFrame);
                                    exploded = true;
                                    explodedOnAnotherHarderBullet = true;
                                }
                            }
                        }

                        if (bulletConfig.RemainsUponHit && !shouldBeImmune && !(successfulDef1 && bulletConfig.TakesDef1AsHardPushback)) {
                            // [WARNING] Strictly speaking, I should re-traverse "victimNextFrame.BulletImmuneRecords" to determine "nextImmuneRcdI", but whatever...
                            int nextImmuneRcdI = immuneRcdI;
                            int terminatingImmuneRcdI = nextImmuneRcdI + 1;
                            if (nextImmuneRcdI == victimNextFrame.BulletImmuneRecords.Count) {
                                nextImmuneRcdI = 0;
                                terminatingImmuneRcdI = victimNextFrame.BulletImmuneRecords.Count; // [WARNING] DON'T update termination in this case! 
                                                                                                   //logger.LogWarn("Replacing the first immune record of joinIndex = " + victimNextFrame.JoinIndex + " due to overflow!");
                            }
                            AssignToBulletImmuneRecord(bulletNextFrame.BulletLocalId, (MAX_INT <= bulletConfig.HitStunFrames) ? MAX_INT : (bulletConfig.HitStunFrames << 3), victimNextFrame.BulletImmuneRecords[nextImmuneRcdI]);

                            //logger.LogInfo("joinIndex = " + victimCurrFrame.JoinIndex + " JUST BECOMES immune to bulletLocalId= " + bulletNextFrame.BulletLocalId + " for rdfCount=" + bulletConfig.HitStunFrames + " at rdfId=" + currRenderFrame.Id);

                            if (terminatingImmuneRcdI < victimNextFrame.BulletImmuneRecords.Count) victimNextFrame.BulletImmuneRecords[terminatingImmuneRcdI].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
                        }

                        var origVictimInNextFrameHp = victimNextFrame.Hp;
                        victimNextFrame.Hp -= effDamage;
                        if (victimNextFrame.Hp >= victimChConfig.Hp) {
                            victimNextFrame.Hp = victimChConfig.Hp;
                        }
                        if (0 >= hardPushbackCnt && bulletConfig.BeamCollision) {
                            // [WARNING] In this case, beam velocity will recover after victim death.
                            effPushback.X += (0 < bulletNextFrame.DirX ? overlapResult.OverlapMag : -overlapResult.OverlapMag);
                            bulletNextFrame.VelX = 0;
                            bulletNextFrame.VelY = 0;
                        }
                        if (!shouldBeImmune) {
                            if (0 < effDamage) {
                                bulletNextFrame.DamageDealed = effDamage;
                                victimNextFrame.FramesSinceLastDamaged = DEFAULT_FRAMES_TO_SHOW_DAMAGED;
                                victimNextFrame.LastDamagedByJoinIndex = bulletNextFrame.OffenderJoinIndex;
                                victimNextFrame.LastDamagedByBulletTeamId = bulletNextFrame.TeamId;
                                victimNextFrame.DamageElementalAttrs = bulletConfig.ElementalAttrs; // Just pick the last one for display
                                victimNextFrame.FramesCapturedByInertia = 0; // Being attacked breaks movement inertia.
                            } else if (0 < bulletConfig.Damage) {
                                // victim has a 0 damage yield
                                bulletNextFrame.DamageDealed = effDamage;
                                victimNextFrame.FramesSinceLastDamaged = DEFAULT_FRAMES_TO_SHOW_DAMAGED;
                                victimNextFrame.FramesCapturedByInertia = 0; // Being attacked breaks movement inertia.
                            } else if (isAllyTargetingBl) {
                                bulletNextFrame.DamageDealed = effDamage;
                            }

                            if (0 != effDamage) {
                                addNewBulletExplosionToNextFrame(currRenderFrame.Id, currRenderFrame.Id, bulletConfig, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, bulletNextFrame, victimNextFrame, effDamage, victimChConfig.Ifc, logger);
                                everAddedExplosion = true;
                                /*
                                   if (2 == victimNextFrame.BulletTeamId && SPECIES_RIDERGUARD_RED == victimNextFrame.SpeciesId) {
                                   logger.LogInfo("currRdfId=" + currRenderFrame.Id + ", bullet localId=" + bulletNextFrame.BulletLocalId + " deals effDamage to RIDER_GUARD, " + "overlapResult=" + overlapResult.ToString());
                                   }
                                 */
                            }
                        }
                        if (isAllyTargetingBl) {
                            // No need to handle stuns
                            break;
                        }
                        if (0 >= victimNextFrame.Hp) {
                            // [WARNING] We don't have "dying in air" animation for now, and for better graphical recognition, play the same dying animation even in air
                            // If "victimCurrFrame" took multiple bullets in the same renderFrame, where a bullet in the middle of the set made it DYING, then all consecutive bullets would just take it into this small block again!
                            victimNextFrame.Hp = 0;
                            victimNextFrame.CharacterState = Dying;
                            victimNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
                            victimNextFrame.VelX = 0;
                            if (victimChConfig.OmitGravity || victimNextFrame.OmitGravity) {
                                victimNextFrame.VelY = 0;
                            } else {
                                // otherwise no need to change "VelY"
                            }
                            resetJumpStartup(victimNextFrame, true);
                            if (victimNextFrame.JoinIndex <= roomCapacity) {
                                if (null != offenderNextFrame) offenderNextFrame.BeatsCnt += 1;
                                victimNextFrame.BeatenCnt += 1;
                            }

                            accumulateGauge(victimChConfig.GaugeIncWhenKilled, bulletConfig, offenderNextFrame);
                        } else {
                            // [WARNING] Deliberately NOT assigning to "victimNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
                            int victimEffHardness = victimChConfig.Hardness;
                            if (null != victimActiveSkillBuff) {
                                victimEffHardness += victimActiveSkillBuff.CharacterHardnessDelta;
                            }
                            bool shouldOmitHitPushback = (successfulDef1 || victimEffHardness > bulletConfig.Hardness);
                            if (!shouldOmitHitPushback && BlownUp1 != oldNextCharacterState) {
                                var (pushbackVelX, pushbackVelY) = (xfac * bulletConfig.PushbackVelX, bulletConfig.PushbackVelY);
                                if (NO_LOCK_VEL == bulletConfig.PushbackVelX) {
                                    pushbackVelX = NO_LOCK_VEL;
                                }
                                if (NO_LOCK_VEL == bulletConfig.PushbackVelY) {
                                    pushbackVelY = NO_LOCK_VEL;
                                }
                                // The traversal order of bullets is deterministic, thus the following assignment is deterministic regardless of the order of collision result popping.
                                if (
                                        successfulDef1
                                        ||
                                        (victimNextFrame.OnWall && (0 != victimNextFrame.OnWallNormX || 0 != victimNextFrame.OnWallNormY))
                                   ) {
                                    bool victimXRevPushback = (0 < bulletNextFrame.VelX * victimNextFrame.OnWallNormX);
                                    bool victimYRevPushback = (0 < bulletNextFrame.VelY * victimNextFrame.OnWallNormY);
                                    if (BulletType.Melee == bulletConfig.BType) {
                                        if (victimXRevPushback) {
                                            if (null != offenderNextFrame) {
                                                if (NO_LOCK_VEL != pushbackVelX) {
                                                    offenderNextFrame.VelX = -(pushbackVelX >> 2);
                                                }
                                                if (offenderNextFrame.FramesToRecover > MAX_REVERSE_PUSHBACK_FRAMES_TO_RECOVER) {
                                                    offenderNextFrame.FramesToRecover = MAX_REVERSE_PUSHBACK_FRAMES_TO_RECOVER;
                                                }
                                            }
                                        } else {
                                            if (NO_LOCK_VEL != pushbackVelX) {
                                                victimNextFrame.VelX = pushbackVelX;
                                            }
                                        }

                                        if (victimYRevPushback) {
                                            if (null != offenderNextFrame) {
                                                if (NO_LOCK_VEL != pushbackVelY) {
                                                    offenderNextFrame.VelY = -(pushbackVelY >> 2);
                                                }
                                                if (offenderNextFrame.FramesToRecover > MAX_REVERSE_PUSHBACK_FRAMES_TO_RECOVER) {
                                                    offenderNextFrame.FramesToRecover = MAX_REVERSE_PUSHBACK_FRAMES_TO_RECOVER;
                                                }
                                            }
                                        } else {
                                            if (NO_LOCK_VEL != pushbackVelY) {
                                                victimNextFrame.VelY = pushbackVelY;
                                            }
                                        }
                                    } else if (BulletType.Fireball == bulletConfig.BType || BulletType.MissileLinear == bulletConfig.BType || BulletType.GroundWave == bulletConfig.BType) {
                                        if (!victimXRevPushback && NO_LOCK_VEL != pushbackVelX) {
                                            victimNextFrame.VelX = pushbackVelX;
                                        }
                                        if (!victimYRevPushback && NO_LOCK_VEL != pushbackVelY) {
                                            victimNextFrame.VelY = pushbackVelY;
                                        }

                                        if (victimXRevPushback && bulletConfig.ProvidesXHardPushback) {
                                            // [WARNING] Deliberately NOT checking victimXRevPushback or victimYRevPushback due to concern of false residue bullet.
                                            exploded = true;
                                        }
                                    }
                                } else {
                                    if (NO_LOCK_VEL != pushbackVelX) {
                                        victimNextFrame.VelX = pushbackVelX;
                                    }
                                    if (NO_LOCK_VEL != pushbackVelY) {
                                        victimNextFrame.VelY = pushbackVelY;
                                    }
                                }
                            }

                            // [WARNING] Gravity omitting characters shouldn't take a "blow up".
                            bool shouldOmitStun = (victimChConfig.OmitGravity || (0 >= bulletConfig.HitStunFrames) || shouldOmitHitPushback);
                            var oldFramesToRecover = victimNextFrame.FramesToRecover;
                            bool shouldExtendDef1Broken = (!victimIsFrozen && !victimIsParalyzed && Def1Broken == oldNextCharacterState && bulletConfig.HitStunFrames <= oldFramesToRecover);
                            if (false == shouldOmitStun) {
                                resetJumpStartup(victimNextFrame, true);
                                CharacterState newNextCharacterState = Atked1;
                                if (!victimIsFrozen && bulletConfig.BlowUp) {
                                    // [WARNING] Deliberately allowing "victimIsParalyzed" to be blown up!
                                    newNextCharacterState = BlownUp1;
                                } else if (victimIsFrozen || BlownUp1 != oldNextCharacterState) {
                                    if (isCrouching(oldNextCharacterState, victimChConfig)) {
                                        newNextCharacterState = CrouchAtked1;
                                    }
                                }

                                // [WARNING] The following assignment should be both order-insensitive and avoiding incorrect transfer of recovery frames from Atk[N] to Atked1!
                                if (Dying != victimNextFrame.CharacterState) {
                                    bool oldNextCharacterStateAtked = (Atked1 == oldNextCharacterState || InAirAtked1 == oldNextCharacterState || CrouchAtked1 == oldNextCharacterState || BlownUp1 == oldNextCharacterState || Dying == oldNextCharacterState);
                                    if (!shouldExtendDef1Broken && !oldNextCharacterStateAtked) {
                                        victimNextFrame.FramesToRecover = bulletConfig.HitStunFrames;
                                    } else {
                                        if (bulletConfig.HitStunFrames > oldFramesToRecover) {
                                            victimNextFrame.FramesToRecover = bulletConfig.HitStunFrames;
                                        }
                                    }
                                    victimNextFrame.CharacterState = newNextCharacterState;
                                    if (BlownUp1 == newNextCharacterState && victimNextFrame.OmitGravity) {
                                        if (victimChConfig.OmitGravity) {
                                            victimNextFrame.FramesToRecover = DEFAULT_BLOWNUP_FRAMES_FOR_FLYING;
                                        } else {
                                            victimNextFrame.OmitGravity = false;
                                        }
                                    }
                                    /*
                                       if (SPECIES_HEAVYGUARD_RED == victimNextFrame.SpeciesId && null != offender) {
                                       logger.LogInfo(String.Format("@rdfId={0}, offender.FramesInChState={1}, HeavyGuardRed id={2} next FramesToRecover becomes {3}", currRenderFrame.Id, offender.FramesInChState, victimNextFrame.Id, victimNextFrame.FramesToRecover));
                                       }
                                     */
                                }
                            } else {
                                if (!(successfulDef1 && bulletConfig.TakesDef1AsHardPushback)) {
                                    exploded = true;
                                    explodedOnAnotherHarderBullet = true;
                                }
                            }

                            if (!successfulDef1 && victimNextFrame.FramesInvinsible < bulletConfig.HitInvinsibleFrames) {
                                victimNextFrame.FramesInvinsible = bulletConfig.HitInvinsibleFrames;
                            }

                            accumulateGauge(DEFAULT_GAUGE_INC_BY_HIT, bulletConfig, offenderNextFrame);

                            if (BlownUp1 != victimNextFrame.CharacterState && Dying != victimNextFrame.CharacterState && !(successfulDef1 && victimChConfig.Def1DefiesDebuff)) {
                                if (shouldExtendDef1Broken) {
                                    victimNextFrame.CharacterState = Def1Broken;
                                }

                                if (null != bulletConfig.BuffConfig) {
                                    BuffConfig buffConfig = bulletConfig.BuffConfig;
                                    if (null != buffConfig.AssociatedDebuffs) {
                                        for (int q = 0; q < buffConfig.AssociatedDebuffs.Count; q++) {
                                            DebuffConfig associatedDebuffConfig = debuffConfigs[buffConfig.AssociatedDebuffs[q]];
                                            if (null == associatedDebuffConfig || TERMINATING_BUFF_SPECIES_ID == associatedDebuffConfig.SpeciesId) break;
                                            int debuffArrIdx = associatedDebuffConfig.ArrIdx;
                                            switch (associatedDebuffConfig.Type) {
                                                case DebuffType.FrozenPositionLocked:
                                                    // Overwrite existing debuff for now
                                                    AssignToDebuff(associatedDebuffConfig.SpeciesId, associatedDebuffConfig.Stock, victimNextFrame.DebuffList[debuffArrIdx]);
                                                    // The following transition is deterministic because we checked "victimNextFrame.DebuffList" before transiting into BlownUp1.
                                                    if (isCrouching(victimNextFrame.CharacterState, victimChConfig)) {
                                                        victimNextFrame.CharacterState = CrouchAtked1;
                                                    } else {
                                                        victimNextFrame.CharacterState = Atked1;
                                                    }
                                                    victimNextFrame.VelX = 0;
                                                    resetJumpStartup(victimNextFrame, true);
                                                    switch (associatedDebuffConfig.StockType) {
                                                        case BuffStockType.Timed:
                                                            victimNextFrame.FramesToRecover = associatedDebuffConfig.Stock;
                                                            break;
                                                    }
                                                    break;
                                                case DebuffType.PositionLockedOnly:
                                                    // Overwrite existing debuff for now
                                                    AssignToDebuff(associatedDebuffConfig.SpeciesId, associatedDebuffConfig.Stock, victimNextFrame.DebuffList[debuffArrIdx]);
                                                    if (isCrouching(victimNextFrame.CharacterState, victimChConfig)) {
                                                        victimNextFrame.CharacterState = CrouchAtked1;
                                                    } else {
                                                        victimNextFrame.CharacterState = Atked1;
                                                    }
                                                    victimNextFrame.VelX = 0;
                                                    resetJumpStartup(victimNextFrame, true);
                                                    // [WARNING] Don't change "victimNextFrame.FramesToRecover" for paralyzer!
                                                    break;
                                            }
                                        }
                                    }
                                } else if (null != offender && null != offender.BuffList) {
                                    for (int w = 0; w < offender.BuffList.Count; w++) {
                                        Buff buff = offender.BuffList[w];
                                        if (TERMINATING_BUFF_SPECIES_ID == buff.SpeciesId) break;
                                        if (0 >= buff.Stock) continue;
                                        if (buff.OriginatedRenderFrameId > bulletNextFrame.OriginatedRenderFrameId) continue;
                                        BuffConfig buffConfig = buffConfigs[buff.SpeciesId];
                                        if (null == buffConfig.AssociatedDebuffs) continue;
                                        for (int q = 0; q < buffConfig.AssociatedDebuffs.Count; q++) {
                                            DebuffConfig associatedDebuffConfig = debuffConfigs[buffConfig.AssociatedDebuffs[q]];
                                            if (null == associatedDebuffConfig || TERMINATING_BUFF_SPECIES_ID == associatedDebuffConfig.SpeciesId) break;
                                            int debuffArrIdx = associatedDebuffConfig.ArrIdx;
                                            switch (associatedDebuffConfig.Type) {
                                                case DebuffType.FrozenPositionLocked:
                                                    if (BulletType.Melee == bulletConfig.BType) break; // Forbid melee attacks to use freezing buff while in offender-scope, otherwise it'd be too unbalanced. 
                                                                                                       // Overwrite existing debuff for now
                                                    AssignToDebuff(associatedDebuffConfig.SpeciesId, associatedDebuffConfig.Stock, victimNextFrame.DebuffList[debuffArrIdx]);
                                                    // The following transition is deterministic because we checked "victimNextFrame.DebuffList" before transiting into BlownUp1.
                                                    if (isCrouching(victimNextFrame.CharacterState, victimChConfig)) {
                                                        victimNextFrame.CharacterState = CrouchAtked1;
                                                    } else {
                                                        victimNextFrame.CharacterState = Atked1;
                                                    }
                                                    victimNextFrame.VelX = 0;
                                                    resetJumpStartup(victimNextFrame, true);
                                                    switch (associatedDebuffConfig.StockType) {
                                                        case BuffStockType.Timed:
                                                            victimNextFrame.FramesToRecover = associatedDebuffConfig.Stock;
                                                            break;
                                                    }
                                                    break;
                                                case DebuffType.PositionLockedOnly:
                                                    // Overwrite existing debuff for now
                                                    AssignToDebuff(associatedDebuffConfig.SpeciesId, associatedDebuffConfig.Stock, victimNextFrame.DebuffList[debuffArrIdx]);
                                                    if (isCrouching(victimNextFrame.CharacterState, victimChConfig)) {
                                                        victimNextFrame.CharacterState = CrouchAtked1;
                                                    } else {
                                                        victimNextFrame.CharacterState = Atked1;
                                                    }
                                                    victimNextFrame.VelX = 0;
                                                    resetJumpStartup(victimNextFrame, true);
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case Bullet v4:
                        var (_, v4Config) = FindBulletConfig(v4.SkillId, v4.ActiveSkillHit);
                        if (null == v4Config) {
                            break;
                        }
                        if (!COLLIDABLE_PAIRS.Contains(bulletConfig.CollisionTypeMask | v4Config.CollisionTypeMask)) {
                            break;
                        }
                        if (bulletNextFrame.TeamId == v4.TeamId) break;
                        if (bulletConfig.Hardness > v4Config.Hardness) break;
                        // Now that "bulletNextFrame.Config.Hardness <= v4.Config.Hardness". 
                        if (!IsBulletExploding(bulletNextFrame, bulletConfig) && BulletType.Fireball == bulletConfig.BType && v4Config.ReflectFireballXIfNotHarder && !bulletConfig.RejectsReflectionFromAnotherBullet) {
                            exploded = !IsBulletExploding(bulletNextFrame, bulletConfig);
                            anotherHarderBulletIfc = v4Config.Ifc;
                            addReflectedBulletToNextFrame(currRenderFrame.Id, currRenderFrame, v4.OffenderJoinIndex, v4.TeamId, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, bulletNextFrame, bulletConfig, logger);
                            break;
                        }
                        // Same hardness (i.e. bulletNextFrame.Config.Hardness == v4.Config.Hardness), whether or not "bulletNextFrame" explodes depends on a few extra factors
                        if (bulletConfig.Hardness < v4Config.Hardness || !bulletConfig.RemainsUponHit || (bulletConfig.RemainsUponHit && v4Config.RemainsUponHit)) {
                            // e.g. FireTornadoStarterBullet v.s. IcePillarStarterBullet, special annihilation
                            exploded = true;
                            explodedOnAnotherHarderBullet = true;
                            anotherHarderBulletIfc = v4Config.Ifc;
                        } else {
                            // bulletNextFrame.Config.RemainsUponHit && !v4.Config.RemainsUponHit, let "v4" play its own explosion alone
                        }
                        break;
                    case TrapColliderAttr v5:
                        // Any non-hardPushback traps shall be ignored
                        break;
                    default:
                        exploded = true;
                        if (0 < hardPushbackCnt) {
                            explodedOnHardPushback = !bulletConfig.NoExplosionOnHardPushback && !potentiallyInTheMiddleOfPrevHitMhTransition;
                        }
                        break;
                }
            }

            bool inTheMiddleOfPrevHitMhTransition = (exploded || bulletConfig.BeamCollision || bulletConfig.TouchExplosionBombCollision || bulletConfig.GroundImpactMeleeCollision || bulletConfig.WallImpactMeleeCollision) && potentiallyInTheMiddleOfPrevHitMhTransition;
            if (bulletConfig.MhNotTriggerOnChHit && !explodedOnHardPushback && !explodedOnAnotherHarderBullet) {
                inTheMiddleOfPrevHitMhTransition = false;
            }
            if (bulletConfig.MhNotTriggerOnHarderBulletHit && !explodedOnHardPushback && !explodedOnCh) {
                inTheMiddleOfPrevHitMhTransition = false;
            }
            if (bulletConfig.MhNotTriggerOnHardPushbackHit && !explodedOnAnotherHarderBullet && !explodedOnCh) {
                inTheMiddleOfPrevHitMhTransition = false;
            }

            //bool fromTouchExplosion = false;
            // [WARNING] The following check-and-assignment is used for correction of complicated cases for "TouchExplosionBombCollision".
            if (exploded && bulletConfig.TouchExplosionBombCollision) {
                inTheMiddleOfPrevHitMhTransition = true;
                //fromTouchExplosion = true;
            }

            // [WARNING] The following check-and-assignment is used for correction of complicated cases for "GroundImpactMeleeCollision".
            if (null != offender && !offender.InAir && bulletConfig.GroundImpactMeleeCollision && offender.ActiveSkillId == bulletNextFrame.SkillId && offender.ActiveSkillHit == bulletNextFrame.ActiveSkillHit) {
                inTheMiddleOfPrevHitMhTransition = true;
                exploded = true;
            }

            if (exploded) {
                if (BulletType.Melee == bulletConfig.BType) {
                    if (BulletState.Exploding != bulletNextFrame.BlState && (!bulletConfig.NoExplosionOnHardPushback || explodedOnHardPushback || explodedOnAnotherHarderBullet)) {
                        // [WARNING] This is just silently retiring the melee bullet
                        bulletNextFrame.BlState = BulletState.Exploding; // Such that no collider from next rdf on
                        bulletNextFrame.FramesInBlState = bulletConfig.ExplosionFrames + 1; // It'd still be deemed "alive" for emitting the next hit if "MhType == FromEmission"
                    }
                    if (MultiHitType.FromVisionSeekOrDefault == bulletConfig.MhType) {
                        bulletNextFrame.OriginatedVirtualGridX = bulletNextFrame.VirtualGridX;
                        bulletNextFrame.OriginatedVirtualGridY = bulletNextFrame.VirtualGridY;
                    }
                } else if (BulletType.Fireball == bulletConfig.BType || BulletType.GroundWave == bulletConfig.BType || BulletType.MissileLinear == bulletConfig.BType) {
                    if (!bulletConfig.RemainsUponHit || explodedOnHardPushback || explodedOnAnotherHarderBullet) {
                        if (BulletState.Exploding != bulletNextFrame.BlState || explodedOnHardPushback || explodedOnAnotherHarderBullet) {
                            if (NO_VFX_ID != bulletConfig.InplaceVanishExplosionSpeciesId) {
                                addNewBulletVanishingExplosionToNextFrame(currRenderFrame.Id, currRenderFrame.Id, bulletConfig, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, bulletNextFrame, anotherHarderBulletIfc, logger);
                            } else if ((explodedOnHardPushback || explodedOnAnotherHarderBullet) && !everAddedExplosion) {
                                addNewBulletExplosionToNextFrame(currRenderFrame.Id, currRenderFrame.Id, bulletConfig, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, bulletNextFrame, null, 0, anotherHarderBulletIfc, logger);
                            }
                            bulletNextFrame.BlState = BulletState.Exploding;
                            bulletNextFrame.FramesInBlState = bulletConfig.ExplosionFrames + 1;
                        }
                    } else if (bulletConfig.HopperMissile) {
                        bulletNextFrame.TargetCharacterJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                        bulletNextFrame.VelX = 0;
                        bulletNextFrame.VelY = 0;
                        bulletNextFrame.OriginatedVirtualGridX = bulletNextFrame.VirtualGridX;
                        bulletNextFrame.OriginatedVirtualGridY = bulletNextFrame.VirtualGridY;
                        bulletNextFrame.RemainingHardPushbackBounceQuota -= 1;
                        if (0 >= bulletNextFrame.RemainingHardPushbackBounceQuota) {
                            bulletNextFrame.RemainingHardPushbackBounceQuota = 0;
                            bulletNextFrame.BlState = BulletState.Exploding;
                            bulletNextFrame.FramesInBlState = bulletConfig.ExplosionFrames + 1;
                        }
                    }
                } else {
                    // Nothing to do
                }

                if (inTheMiddleOfPrevHitMhTransition) {
                    if (null != offender && null != offenderNextFrame && null != skillConfig) {
                        var offenderExistingDebuff = offender.DebuffList[DEBUFF_ARR_IDX_ELEMENTAL];
                        bool offenderIsParalyzed = (TERMINATING_DEBUFF_SPECIES_ID != offenderExistingDebuff.SpeciesId && 0 < offenderExistingDebuff.Stock && DebuffType.PositionLockedOnly == debuffConfigs[offenderExistingDebuff.SpeciesId].Type);
                        if (addNewBulletToNextFrame(currRenderFrame.Id, currRenderFrame.Id, offender, offenderNextFrame, characters[offender.SpeciesId], offenderIsParalyzed, xfac, skillConfig, nextRenderFrameBullets, bulletNextFrame.ActiveSkillHit + 1, bulletNextFrame.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, bulletNextFrame, bulletConfig, (bulletConfig.BeamCollision ? bulletNextFrame : null), null, bulletNextFrame.OffenderJoinIndex, bulletNextFrame.TeamId, logger)) {

                            var targetNewBullet = nextRenderFrameBullets[bulletCnt - 1];
                            /*
                            if (fromTouchExplosion) {
                                logger.LogInfo("@rdfId=" + currRenderFrame.Id + ", touch explosion of bulletLocalId=" + bulletNextFrame.BulletLocalId + " generates targetNewBullet.LocalId=" + targetNewBullet.BulletLocalId);
                            }
                            */
                            var (_, newBlConfig) = FindBulletConfig(targetNewBullet.SkillId, targetNewBullet.ActiveSkillHit);
                            if (null != newBlConfig) {
                                if (newBlConfig.HopperMissile) {
                                    targetNewBullet.OriginatedVirtualGridX = bulletNextFrame.VirtualGridX;
                                    targetNewBullet.OriginatedVirtualGridY = bulletNextFrame.VirtualGridY;
                                    targetNewBullet.VirtualGridX = bulletNextFrame.VirtualGridX;
                                    targetNewBullet.VirtualGridY = bulletNextFrame.VirtualGridY;
                                }
                                offenderNextFrame.ActiveSkillHit = targetNewBullet.ActiveSkillHit;
                                if (offenderNextFrame.FramesInvinsible < newBlConfig.StartupInvinsibleFrames) {
                                    offenderNextFrame.FramesInvinsible = newBlConfig.StartupInvinsibleFrames;
                                }
                            }
                        }
                        /*
                           if (80 == offenderNextFrame.ActiveSkillId) {
                           logger.LogInfo("currRdfId=" + currRenderFrame.Id + ", offenderNextFrame.ChState=" + offenderNextFrame.CharacterState + ", offenderNextFrame.FramesInChState=" + offenderNextFrame.FramesInChState + ", offenderNextFrame.ActiveSkillHit=" + offenderNextFrame.ActiveSkillHit + ", offenderNextFrame.FramesToRecover=" + offenderNextFrame.FramesToRecover);
                           }
                         */
                    } // TODO: Support "inTheMiddleOfPrevHitMhTransition" for traps
                }

                if (null != offenderNextFrame && (bulletConfig.GroundImpactMeleeCollision || bulletConfig.WallImpactMeleeCollision)) {
                    // [WARNING] As long as "true == exploded", we should end this bullet regardless of landing on character or hardPushback.
                    var shiftedRdfCnt = (bulletConfig.ActiveFrames - origFramesInActiveState);
                    if (0 < shiftedRdfCnt) {
                        offenderNextFrame.FramesInChState += shiftedRdfCnt;
                        offenderNextFrame.FramesToRecover -= shiftedRdfCnt;
                    }
                    if (offenderNextFrame.OnSlope && 0 > offenderNextFrame.VelY) {
                        offenderNextFrame.VelY = 0;
                    }
                }
            } else if (!beamBlockedByHardPushback) {
                if ((BulletType.Fireball == bulletConfig.BType || BulletType.GroundWave == bulletConfig.BType) && SPEED_NOT_HIT_NOT_SPECIFIED != bulletConfig.SpeedIfNotHit && bulletConfig.Speed != bulletConfig.SpeedIfNotHit) {
                    var bulletDirMagSq = bulletConfig.DirX * bulletConfig.DirX + bulletConfig.DirY * bulletConfig.DirY;
                    var invBulletDirMag = InvSqrt32(bulletDirMagSq);
                    var bulletSpeedXfac = xfac * invBulletDirMag * bulletConfig.DirX;
                    var bulletSpeedYfac = invBulletDirMag * bulletConfig.DirY;
                    bulletNextFrame.VelX = (int)(bulletSpeedXfac * bulletConfig.SpeedIfNotHit);
                    bulletNextFrame.VelY = (int)(bulletSpeedYfac * bulletConfig.SpeedIfNotHit);
                }
            }
        }

        private static void _calcAllBulletsCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Bullet> nextRenderFrameBullets, RepeatedField<Trigger> nextRenderFrameTriggers, ref SatResult overlapResult, CollisionSpace collisionSys, Collision collision, Collider[] dynamicRectangleColliders, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, FrameRingBuffer<Collider> residueCollided, ref SatResult primaryOverlapResult, int iSt, int iEd, ref int bulletLocalIdCounter, ref int bulletCnt, ref ulong fulfilledEvtSubscriptionSetMask, int colliderCnt, Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToTiledConfig, ILoggerBridge logger) {
            bool dummyHasLockVel = false;
            // [WARNING] Bullet collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
            // Check bullet-anything collisions
            for (int i = iSt; i < iEd; i++) {
                Collider bulletCollider = dynamicRectangleColliders[i];
                if (null == bulletCollider.Data) continue;
                var bulletNextFrame = bulletCollider.Data as Bullet; // [WARNING] See "_moveAndInsertBulletColliders", the bound data in each collider is already belonging to "nextRenderFrameBullets"!
                if (null == bulletNextFrame || TERMINATING_BULLET_LOCAL_ID == bulletNextFrame.BulletLocalId) {
                    //logger.LogWarn(String.Format("dynamicRectangleColliders[i:{0}] is not having bullet type! iSt={1}, iEd={2}", i, iSt, iEd));
                    continue;
                }
                
                var (_, bulletConfig) = FindBulletConfig(bulletNextFrame.SkillId, bulletNextFrame.ActiveSkillHit);
                if (null == bulletConfig) {
                    continue;
                }
                var effPushback = effPushbacks[i];
                var hardPushbackNorms = hardPushbackNormsArr[i]; 
                _calcSingleBulletCollision(currRenderFrame, bulletNextFrame, bulletConfig, roomCapacity, npcCnt, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, nextRenderFrameBullets, nextRenderFrameTriggers, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, bulletCollider, hardPushbackNorms, effPushback, residueCollided, collision, ref overlapResult, ref primaryOverlapResult, triggerEditorIdToTiledConfig, logger);
            }
        }

        protected static bool addNewBulletExplosionToNextFrame(int originatedRdfId, int currRdfId, BulletConfig bulletConfig, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, Bullet referenceBullet, CharacterDownsync? referenceVictim, int damageDealed, IfaceCat explodedOnIfc, ILoggerBridge logger) {
            //logger.LogInfo($"At rdfId={currRdf.Id} adding bullet explosion for referenceBullet.Id={referenceBullet.BulletLocalId}");
            if (BulletType.GroundWave == bulletConfig.BType && NO_VFX_ID == bulletConfig.ExplosionSpeciesId && NO_VFX_ID == bulletConfig.ExplosionVfxSpeciesId) {
                return false;
            }
            if (bulletCnt >= nextRenderFrameBullets.Count) {
                logger.LogWarn("bullet explosion overwhelming#1, currRdfId=" + currRdfId);
                return false;
            }
            int newOriginatedVirtualX = referenceBullet.OriginatedVirtualGridX;
            int newOriginatedVirtualY = referenceBullet.OriginatedVirtualGridY;

            int newVirtualX = referenceBullet.VirtualGridX;
            int newVirtualY = referenceBullet.VirtualGridY;
            if (null != referenceVictim) {
                // To make explosion visually more consistent
                newVirtualX = referenceVictim.VirtualGridX;
                newVirtualY = referenceVictim.VirtualGridY;            
            }

            AssignToBullet(
                    bulletLocalIdCounter,
                    originatedRdfId,
                    referenceBullet.OffenderJoinIndex,
                    referenceBullet.OffenderTrapLocalId,
                    referenceBullet.TeamId,
                    BulletState.Exploding, 0,
                    newOriginatedVirtualX,
                    newOriginatedVirtualY,
                    newVirtualX,
                    newVirtualY,
                    referenceBullet.DirX, referenceBullet.DirY, // dir
                    0, 0, // velocity
                    referenceBullet.ActiveSkillHit, referenceBullet.SkillId, referenceBullet.VertMovingTrapLocalIdUponActive, bulletConfig.RepeatQuota, bulletConfig.DefaultHardPushbackBounceQuota, MAGIC_JOIN_INDEX_INVALID,
                    referenceBullet.SpinCos, referenceBullet.SpinSin,
                    damageDealed,
                    explodedOnIfc,
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // Explicitly specify termination of nextRenderFrameBullets
            nextRenderFrameBullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            return true;
        }

        protected static bool addNewBulletVanishingExplosionToNextFrame(int originatedRdfId, int currRdfId, BulletConfig bulletConfig, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, Bullet referenceBullet, IfaceCat explodedOnIfc, ILoggerBridge logger) {
            if (NO_VFX_ID == bulletConfig.InplaceVanishExplosionSpeciesId) {
                return false;
            }
            if (bulletCnt >= nextRenderFrameBullets.Count) {
                logger.LogWarn("bullet vanishing overwhelming#1, currRdfId=" + currRdfId);
                return false;
            }
            int newOriginatedVirtualX = referenceBullet.OriginatedVirtualGridX;
            int newOriginatedVirtualY = referenceBullet.OriginatedVirtualGridY;
            int newVirtualX = referenceBullet.VirtualGridX;
            int newVirtualY = referenceBullet.VirtualGridY;

            AssignToBullet(
                    bulletLocalIdCounter,
                    originatedRdfId,
                    referenceBullet.OffenderJoinIndex,
                    referenceBullet.OffenderTrapLocalId,
                    referenceBullet.TeamId,
                    BulletState.Vanishing, referenceBullet.FramesInBlState,
                    newOriginatedVirtualX,
                    newOriginatedVirtualY,
                    newVirtualX,
                    newVirtualY,
                    referenceBullet.DirX, referenceBullet.DirY, // dir
                    0, 0, // velocity
                    referenceBullet.ActiveSkillHit, referenceBullet.SkillId, referenceBullet.VertMovingTrapLocalIdUponActive, bulletConfig.RepeatQuota, bulletConfig.DefaultHardPushbackBounceQuota, MAGIC_JOIN_INDEX_INVALID,
                    referenceBullet.SpinCos, referenceBullet.SpinSin,
                    0,
                    explodedOnIfc,
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // Explicitly specify termination of nextRenderFrameBullets
            nextRenderFrameBullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            return true;
        }

        protected static bool addNewBulletToNextFrame(int originatedRdfId, int currRdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, bool isParalyzed, int xfac, Skill skillConfig, RepeatedField<Bullet> nextRenderFrameBullets, int activeSkillHit, uint activeSkillId, ref int bulletLocalIdCounter, ref int bulletCnt, ref bool hasLockVel, Bullet? referencePrevHitBullet, BulletConfig? referencePrevHitBulletConfig, Bullet? referencePrevEmissionBullet, CharacterDownsync? targetChNextFrame, int offenderJoinIndex, int bulletTeamId, ILoggerBridge logger) {
            if (NO_SKILL_HIT == activeSkillHit || activeSkillHit > skillConfig.Hits.Count) return false;
            if (bulletCnt >= nextRenderFrameBullets.Count) {
                logger.LogWarn("bullet overwhelming#1, currRdfId=" + currRdfId);
                return false;
            }
            var bulletConfig = skillConfig.Hits[activeSkillHit-1];
            var prevBulletConfig = (null == referencePrevHitBullet || null == referencePrevHitBulletConfig)
                                ? 
                                (2 <= activeSkillHit ? skillConfig.Hits[activeSkillHit - 2] : null)
                                :
                                referencePrevHitBulletConfig;
            var bulletDirMagSq = bulletConfig.DirX * bulletConfig.DirX + bulletConfig.DirY * bulletConfig.DirY;
            var invBulletDirMag = InvSqrt32(bulletDirMagSq);
            if (OnWallAtk1 == skillConfig.BoundChState && (BulletType.Melee != bulletConfig.BType)) {
                xfac *= -1;
            }
            var bulletSpeedXfac = xfac * invBulletDirMag * bulletConfig.DirX;
            var bulletSpeedYfac = invBulletDirMag * bulletConfig.DirY;
            int newOriginatedVirtualX = (null == referencePrevEmissionBullet || bulletConfig.UseChOffsetRegardlessOfEmissionMh) ? (currCharacterDownsync.VirtualGridX + xfac * bulletConfig.HitboxOffsetX) : referencePrevEmissionBullet.OriginatedVirtualGridX;
            int newOriginatedVirtualY = (null == referencePrevEmissionBullet || bulletConfig.UseChOffsetRegardlessOfEmissionMh) ? (currCharacterDownsync.VirtualGridY + bulletConfig.HitboxOffsetY) : referencePrevEmissionBullet.OriginatedVirtualGridY;
            int newVirtualX = (null == referencePrevHitBullet || (BulletType.Melee == bulletConfig.BType && MultiHitType.FromVisionSeekOrDefault != bulletConfig.MhType)) ? currCharacterDownsync.VirtualGridX + xfac * bulletConfig.HitboxOffsetX : referencePrevHitBullet.VirtualGridX + xfac * bulletConfig.HitboxOffsetX;
            int newVirtualY = (null == referencePrevHitBullet || (BulletType.Melee == bulletConfig.BType && MultiHitType.FromVisionSeekOrDefault != bulletConfig.MhType)) ? currCharacterDownsync.VirtualGridY + bulletConfig.HitboxOffsetY : referencePrevHitBullet.VirtualGridY + bulletConfig.HitboxOffsetY;
            int groundWaveVelY = bulletConfig.DownSlopePrimerVelY;

            if (null != prevBulletConfig && prevBulletConfig.GroundImpactMeleeCollision) {
                newVirtualY = thatCharacterInNextFrame.VirtualGridY + bulletConfig.HitboxOffsetY;
                if (thatCharacterInNextFrame.OnSlope && thatCharacterInNextFrame.OnSlopeFacingDown) {
                    newVirtualY -= (chConfig.DefaultSizeY >> 1);
                }
            } else if (BulletType.GroundWave == bulletConfig.BType) {
                if (currCharacterDownsync.OnSlope && currCharacterDownsync.OnSlopeFacingDown) {
                    newVirtualY -= (chConfig.DefaultSizeY >> 1);
                }
            }

            // To favor startup vfx display which is based on bullet originatedVx&originatedVy.
            if (null != referencePrevHitBulletConfig && MultiHitType.FromVisionSeekOrDefault == referencePrevHitBulletConfig.MhType) {
                if (null != targetChNextFrame) {
                    newVirtualX = targetChNextFrame.VirtualGridX + bulletConfig.HitboxOffsetX;
                    newVirtualY = targetChNextFrame.VirtualGridY + bulletConfig.HitboxOffsetY;
                }
                newOriginatedVirtualX = newVirtualX;
                newOriginatedVirtualY = newVirtualY;
            }

            float dstSpinCos = 1f, dstSpinSin = 0f;
            if (bulletConfig.MhInheritsSpin) {
                if (null != referencePrevHitBullet) {
                    dstSpinCos = referencePrevHitBullet.SpinCos;
                    dstSpinSin = referencePrevHitBullet.SpinSin;
                } else if (null != referencePrevEmissionBullet) {
                    dstSpinCos = referencePrevEmissionBullet.SpinCos;
                    dstSpinSin = referencePrevEmissionBullet.SpinSin;
                }
            } else if (0 != bulletConfig.InitSpinCos || 0 != bulletConfig.InitSpinSin) {
                dstSpinCos = bulletConfig.InitSpinCos;
                dstSpinSin = 0 < xfac ? bulletConfig.InitSpinSin : -bulletConfig.InitSpinSin;
            }

            var initBlState = BulletState.StartUp;
            int initFramesInBlState = 0;
            if (bulletConfig.MhInheritsFramesInBlState) {
                if (null != referencePrevEmissionBullet) {
                    initBlState = referencePrevEmissionBullet.BlState;
                    initFramesInBlState = referencePrevEmissionBullet.FramesInBlState + 1;
                    //logger.LogInfo("@rdfId=" + currRdf.Id + ", new bulletLocalId=" + bulletLocalIdCounter+ ", #1initFramesInBlState=" + initFramesInBlState + " from referencePrevEmissionBullet.LocalId=" + referencePrevEmissionBullet.BulletLocalId);
                    newVirtualX = referencePrevEmissionBullet.VirtualGridX;
                    newVirtualY = referencePrevEmissionBullet.VirtualGridY;
                    newOriginatedVirtualX = newVirtualX;
                    newOriginatedVirtualY = newVirtualY;
                } else if (null != referencePrevHitBullet && null != referencePrevHitBulletConfig) {
                    initBlState = referencePrevHitBullet.BlState;
                    initFramesInBlState = referencePrevHitBullet.FramesInBlState + 1;
                    //logger.LogInfo("@rdfId=" + currRdf.Id + ", new bulletLocalId=" + bulletLocalIdCounter + ", #2initFramesInBlState=" + initFramesInBlState + " from referencePrevHitBullet.LocalId=" + referencePrevHitBullet.BulletLocalId);
                    newVirtualX = referencePrevHitBullet.VirtualGridX;
                    newVirtualY = referencePrevHitBullet.VirtualGridY;
                    newOriginatedVirtualX = newVirtualX;
                    newOriginatedVirtualY = newVirtualY;
                }             
            }

            AssignToBullet(
                    bulletLocalIdCounter,
                    originatedRdfId,
                    offenderJoinIndex,
                    TERMINATING_TRAP_ID,
                    bulletTeamId,
                    initBlState, initFramesInBlState,
                    newOriginatedVirtualX,
                    newOriginatedVirtualY,
                    newVirtualX,
                    newVirtualY,
                    xfac * bulletConfig.DirX, bulletConfig.DirY, // dir
                    (int)(bulletSpeedXfac * bulletConfig.Speed), (int)(bulletSpeedYfac * bulletConfig.Speed) + groundWaveVelY, // velocity
                    activeSkillHit, activeSkillId, TERMINATING_TRAP_ID, bulletConfig.RepeatQuota, bulletConfig.DefaultHardPushbackBounceQuota, MAGIC_JOIN_INDEX_INVALID,
                    dstSpinCos, dstSpinSin, // spin
                    0, // damage dealed
                    bulletConfig.Ifc, 
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // [WARNING] This part locks velocity by the last bullet in the simultaneous array
            if (!bulletConfig.DelaySelfVelToActive && !isParalyzed) {
                if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                    hasLockVel = true;
                    thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
                }
                if (!currCharacterDownsync.OmitGravity) {
                    if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                        if (0 <= bulletConfig.SelfLockVelY || thatCharacterInNextFrame.InAir) {
                            hasLockVel = true;
                            // [WARNING] DON'T assign negative velY to a character not in air!
                            thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
                        }
                    }
                } else {
                    if (NO_LOCK_VEL != bulletConfig.SelfLockVelYWhenFlying) {
                        hasLockVel = true;
                        thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelYWhenFlying;
                    }
                }
            }

            // Explicitly specify termination of nextRenderFrameBullets
            if (bulletCnt < nextRenderFrameBullets.Count) nextRenderFrameBullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            if (0 < bulletConfig.SimultaneousMultiHitCnt && activeSkillHit < skillConfig.Hits.Count) {
                return addNewBulletToNextFrame(originatedRdfId, currRdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, isParalyzed, xfac, skillConfig, nextRenderFrameBullets, activeSkillHit+1, activeSkillId, ref bulletLocalIdCounter, ref bulletCnt, ref hasLockVel, referencePrevHitBullet, referencePrevHitBulletConfig, referencePrevEmissionBullet, targetChNextFrame, offenderJoinIndex, bulletTeamId, logger);
            } else {
                return true;
            }
        }

        protected static bool addNewTrapBulletToNextFrame(int originatedRdfId, RoomDownsyncFrame currRdf, Trap trapNextFrame, TrapConfig trapConfig, BulletConfig bulletConfig, Skill skill, int xfac, int yfac, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            if (bulletCnt >= nextRenderFrameBullets.Count) {
                logger.LogWarn("bullet overwhelming#4, currRdf=" + stringifyRdf(currRdf));
                return false;
            }
            if (0 != xfac) {
                xfac = 0 < xfac ? +1 : -1;
            }
            if (0 != yfac) {
                yfac = 0 < yfac ? +1 : -1;
            }
            var bulletDirMagSq = bulletConfig.DirX * bulletConfig.DirX + bulletConfig.DirY * bulletConfig.DirY;
            var invBulletDirMag = InvSqrt32(bulletDirMagSq);
            var bulletSpeedXfac = xfac * invBulletDirMag * bulletConfig.DirX;
            var bulletSpeedYfac = yfac * invBulletDirMag * bulletConfig.DirY;
            // [WARNING] I understand that it's better use "trapCurrFrame" for position reference here but it's not a major issue.
            int effOffsetX = xfac * bulletConfig.HitboxOffsetX, effOffsetY = yfac * bulletConfig.HitboxOffsetY; 
            if (trapConfig.IsRotary) {
                float halfBw = 0.5f * trapNextFrame.ConfigFromTiled.BoxCw, halfBh = 0.5f * trapNextFrame.ConfigFromTiled.BoxCh;
                var (effOffsetCx, effOffsetCy) = VirtualGridToPolygonColliderCtr(effOffsetX, effOffsetY);
                float effOffsetXFloatFromAnchor = (effOffsetCx + halfBw - trapConfig.SpinAnchorX), effOffsetYFloatFromAnchor = (effOffsetCy + halfBh - trapConfig.SpinAnchorY); 
                float effOffsetXFloat = 0, effOffsetYFloat = 0; 
                Vector.Rotate(effOffsetXFloatFromAnchor, effOffsetYFloatFromAnchor, trapNextFrame.SpinCos, 0 < xfac ? trapNextFrame.SpinSin : -trapNextFrame.SpinSin, out effOffsetXFloat, out effOffsetYFloat);

                //logger.LogInfo($"At rdfId={currRdf.Id}, (trapNextFrame.SpinCos, trapNextFrame.SpinSin, xfac) = ({Math.Round(trapNextFrame.SpinCos,2)}, {Math.Round(trapNextFrame.SpinSin,2)}, {xfac}) rotates (effOffsetXFloatFromAnchor, effOffsetYFloatFromAnchor) = ({Math.Round(effOffsetXFloatFromAnchor,2)}, {Math.Round(effOffsetYFloatFromAnchor,2)}) to (effOffsetXFloat, effOffsetYFloat) = ({Math.Round(effOffsetXFloat,2)}, {Math.Round(effOffsetYFloat,2)})");
                effOffsetXFloat += (trapConfig.SpinAnchorX-halfBw); 
                effOffsetYFloat += (trapConfig.SpinAnchorY-halfBh); 
                (effOffsetX, effOffsetY) = PolygonColliderCtrToVirtualGridPos(effOffsetXFloat, effOffsetYFloat);
            }
            int newVirtualX = trapNextFrame.VirtualGridX + effOffsetX;
            int newVirtualY = trapNextFrame.VirtualGridY + effOffsetY;
            int newOriginatedVirtualX = newVirtualX;
            int newOriginatedVirtualY = newVirtualY;
            int groundWaveVelY = bulletConfig.DownSlopePrimerVelY;

            float srcSpinCos = 1f, srcSpinSin = 0f;
            float dstSpinCos = 1f, dstSpinSin = 0f;
            if (IsBulletRotary(bulletConfig)) {
                srcSpinCos = bulletConfig.InitSpinCos;
                srcSpinSin = 0 < xfac ? bulletConfig.InitSpinSin : -bulletConfig.InitSpinSin;
                dstSpinCos = srcSpinCos;
                dstSpinSin = srcSpinSin;
            }
            var velX = (int)(bulletSpeedXfac * bulletConfig.Speed); 
            var velY = (int)(bulletSpeedYfac * bulletConfig.Speed) + groundWaveVelY;
            if (trapConfig.IsRotary) {
                Vector.Rotate(srcSpinCos, srcSpinSin, trapNextFrame.SpinCos, 0 < xfac ? trapNextFrame.SpinSin : -trapNextFrame.SpinSin, out dstSpinCos, out dstSpinSin);
                //logger.LogInfo($"At rdfId={currRdf.Id}, (trapNextFrame.SpinCos, trapNextFrame.SpinSin, xfac) = ({Math.Round(trapNextFrame.SpinCos,2)}, {Math.Round(trapNextFrame.SpinSin,2)}, {xfac}) rotates (srcSpinCos, srcSpinSin) = ({Math.Round(srcSpinCos,2)}, {Math.Round(srcSpinSin,2)}) to (dstSpinCos, dstSpinSin) = ({Math.Round(dstSpinCos,2)}, {Math.Round(dstSpinSin,2)})");
            }

            AssignToBullet(
                    bulletLocalIdCounter,
                    originatedRdfId,
                    MAGIC_JOIN_INDEX_DEFAULT,
                    trapNextFrame.TrapLocalId,
                    DEFAULT_BULLET_TEAM_ID,
                    BulletState.StartUp, 0,
                    newOriginatedVirtualX,
                    newOriginatedVirtualY,
                    newVirtualX,
                    newVirtualY,
                    xfac * bulletConfig.DirX, yfac * bulletConfig.DirY, // dir
                    velX, velY, // velocity
                    1, skill.Id, TERMINATING_TRAP_ID, bulletConfig.RepeatQuota, bulletConfig.DefaultHardPushbackBounceQuota, MAGIC_JOIN_INDEX_INVALID,
                    dstSpinCos, dstSpinSin, // spin
                    0,
                    IfaceCat.Empty,
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // Explicitly specify termination of nextRenderFrameBullets
            if (bulletCnt < nextRenderFrameBullets.Count) nextRenderFrameBullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            return true;
        }

        protected static bool addReflectedBulletToNextFrame(int rdfId, RoomDownsyncFrame currRdf, int newOffenderJoinIndex, int newTeamId, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, Bullet referencePrevHitBullet, BulletConfig referencePrevHitBulletConfig, ILoggerBridge logger) {
            if (bulletCnt >= nextRenderFrameBullets.Count) {
                logger.LogWarn("bullet overwhelming#3, currRdf=" + stringifyRdf(currRdf));
                return false;
            }
            var origBattleAttr = referencePrevHitBullet;
            var bulletConfig = referencePrevHitBulletConfig;
            int newOriginatedVirtualX = referencePrevHitBullet.VirtualGridX;
            int newOriginatedVirtualY = referencePrevHitBullet.VirtualGridY;
            int newVirtualX = referencePrevHitBullet.VirtualGridX;
            int newVirtualY = referencePrevHitBullet.VirtualGridY;

            AssignToBullet(
                    bulletLocalIdCounter,
                    rdfId - bulletConfig.StartupFrames, // Such that it is immediately active
                    newOffenderJoinIndex,
                    TERMINATING_TRAP_ID,
                    newTeamId,
                    BulletState.Active, 0,
                    newOriginatedVirtualX,
                    newOriginatedVirtualY,
                    newVirtualX,
                    newVirtualY,
                    -referencePrevHitBullet.DirX, referencePrevHitBullet.DirY, // dir
                    -referencePrevHitBullet.VelX, referencePrevHitBullet.VelY, // velocity
                    origBattleAttr.ActiveSkillHit, origBattleAttr.SkillId, TERMINATING_TRAP_ID, bulletConfig.RepeatQuota, bulletConfig.DefaultHardPushbackBounceQuota, MAGIC_JOIN_INDEX_INVALID,
                    referencePrevHitBullet.SpinCos, referencePrevHitBullet.SpinSin, // spin
                    0, // damage dealed
                    bulletConfig.Ifc,
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // Explicitly specify termination of nextRenderFrameBullets
            if (bulletCnt < nextRenderFrameBullets.Count) nextRenderFrameBullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            return true;
        }

        public static void calcBulletVisionBoxInCollisionSpace(Bullet currBullet, BulletConfig bulletConfig, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currBullet.VirtualGridX, currBullet.VirtualGridY + (bulletConfig.VisionOffsetY));
            (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bulletConfig.VisionSizeX, bulletConfig.VisionSizeY);
        }

        public static void _processDelayedBulletSelfVel(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, bool isParalyzed, ILoggerBridge logger) {
            var (skill, bulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
            if (null == skill || null == bulletConfig) {
                return;
            }
            if (currCharacterDownsync.CharacterState != skill.BoundChState) {
                // This shouldn't happen, but if it does, we don't proceed to set "selfLockVel"
                return;
            }
            if (currCharacterDownsync.FramesInChState != bulletConfig.StartupFrames) {
                return;
            }
            if (isParalyzed) {
                return;
            }
            int xfac = (0 < currCharacterDownsync.DirX ? 1 : -1);
            if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
            }
            if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                if (0 <= bulletConfig.SelfLockVelY || thatCharacterInNextFrame.InAir) {
                    thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
                }
            }
        }

        public static (int, bool) _calcEffDamage(int rdfId, CharacterState origOffenderStateNextFrame, CharacterConfig victimChConfig, CharacterDownsync victimNextFrame, BuffConfig? victimActiveSkillBuff, Bullet bulletNextFrame, BulletConfig bulletConfig, bool isAllyTargetingBl, Collider bulletCollider, Collider victimCollider, ILoggerBridge logger) {
            if (isAllyTargetingBl) {
                if (Dying == victimNextFrame.CharacterState) {
                    return (0, false);
                } else {
                    return (bulletConfig.Damage, false);
                }
            }
            bool bulletInFrontOfVictim = (0 > victimNextFrame.DirX*bulletNextFrame.DirX);
            bool successfulDef1 = (Def1 == origOffenderStateNextFrame && victimChConfig.Def1StartupFrames < victimNextFrame.FramesInChState && 0 < victimNextFrame.RemainingDef1Quota && bulletInFrontOfVictim);
            bool eleWeaknessHit = 0 < (victimChConfig.EleWeakness & bulletConfig.ElementalAttrs);
            bool eleResistanceHit = 0 < (victimChConfig.EleResistance & bulletConfig.ElementalAttrs);
            bool isWalkingAutoDef1 = (Walking == victimNextFrame.CharacterState && victimChConfig.WalkingAutoDef1);
            bool isSkillAutoDef1 = (null != victimActiveSkillBuff && victimActiveSkillBuff.AutoDef1);
            if (eleWeaknessHit && !victimChConfig.Def1DefiesEleWeaknessPenetration) {
                successfulDef1 = false;
            } else {
                if (isWalkingAutoDef1 && bulletInFrontOfVictim) {
                    successfulDef1 = true;
                } else if (isSkillAutoDef1 && bulletInFrontOfVictim) {
                    successfulDef1 = true;
                }
            }
            if (!bulletInFrontOfVictim) {
                victimNextFrame.CachedCueCmd = EncodeInput(-victimNextFrame.DirX, -victimNextFrame.DirY, 0, 0, 0, 0, 0);
                switch (victimNextFrame.GoalAsNpc) {
                    case NpcGoal.Nidle:
                    case NpcGoal.NidleIfGoHuntingThenPatrol:
                        // To avoid "CachedCueCmd" got overwritten
                        victimNextFrame.GoalAsNpc = NpcGoal.NhuntThenIdle;
                        break;
                }
            }
            if (successfulDef1) {
                if (bulletConfig.TakesDef1AsHardPushback) {
                    //logger.LogInfo($"At rdfId={rdfId}, bullet=(localId:{bulletNextFrame.BulletLocalId}) takes def1 of victim=(jidx:{victimNextFrame.JoinIndex},spid:{victimNextFrame.SpeciesId}) as hardpushback");
                    return (0, true);
                }
                uint effDef1QuotaReduction = (1 + bulletConfig.GuardBreakerExtraHitCnt);
                if (effDef1QuotaReduction > victimNextFrame.RemainingDef1Quota) {
                    effDef1QuotaReduction = victimNextFrame.RemainingDef1Quota;
                } 
                victimNextFrame.RemainingDef1Quota -= effDef1QuotaReduction; 
                var effStunFrames = (DEFAULT_BLOCK_STUN_FRAMES > bulletConfig.BlockStunFrames ? DEFAULT_BLOCK_STUN_FRAMES : bulletConfig.BlockStunFrames);
                effStunFrames = (effStunFrames < bulletConfig.HitStunFrames ? effStunFrames : bulletConfig.HitStunFrames);
                if (effStunFrames > victimNextFrame.FramesToRecover) {
                    victimNextFrame.FramesToRecover = effStunFrames;
                }
                if (isWalkingAutoDef1) {
                    victimNextFrame.VelX = 0;
                    victimNextFrame.CharacterState = Def1;
                    victimNextFrame.RemainingDef1Quota = victimChConfig.DefaultDef1Quota;
                } else if (!isSkillAutoDef1 && Def1Broken != victimNextFrame.CharacterState && 0 >= victimNextFrame.RemainingDef1Quota) {
                    // [WARNING] "isSkillAutoDef1" is rare and relatively more difficult to handle broken, so skipping it for now.
                    victimNextFrame.CharacterState = Def1Broken;
                    victimNextFrame.FramesToRecover = victimChConfig.DefaultDef1BrokenFramesToRecover;
                    victimNextFrame.FramesInvinsible >>= 1;
                    victimNextFrame.RemainingDef1Quota = 0;
                }
            }
            var effDamage = bulletConfig.Damage;
            if (eleWeaknessHit) {
                effDamage = (int)Math.Floor(bulletConfig.Damage * ELE_WEAKNESS_DEFAULT_YIELD);
            } else if (successfulDef1) {
                var minimumDamage = (0 == victimChConfig.Def1DamageYield ? 0 : 1);
                var candidateDamage = (int)Math.Floor(bulletConfig.Damage * victimChConfig.Def1DamageYield);
                if (0 < bulletConfig.Damage && 0 >= candidateDamage) {
                    candidateDamage = minimumDamage;
                }
                if (candidateDamage < effDamage) {
                    effDamage = candidateDamage;
                }
            } else if (eleResistanceHit) {
                var candidateDamage = (int)Math.Floor(bulletConfig.Damage * ELE_RESISTANCE_DEFAULT_YIELD);
                if (candidateDamage < effDamage) effDamage = candidateDamage;
            }
            return (effDamage, successfulDef1);
        }
        
        public static void accumulateGauge(int rawInc, BulletConfig? bulletConfig, CharacterDownsync? offenderNextFrame) {
            if (null == offenderNextFrame) return;
            if (null == offenderNextFrame.Inventory) return;
            if (null == offenderNextFrame.Inventory.Slots) return;
            if (1 > offenderNextFrame.Inventory.Slots.Count) return;
            var targetSlot = offenderNextFrame.Inventory.Slots[0];
            if (InventorySlotStockType.GaugedMagazineIv != targetSlot.StockType) return; 
            if (targetSlot.Quota >= targetSlot.DefaultQuota) return;
            
            int inc = (null != bulletConfig && 0 != bulletConfig.GaugeIncReductionRatio ? (int)((1f - bulletConfig.GaugeIncReductionRatio)*rawInc) : rawInc);
            targetSlot.GaugeCharged += inc;
            if (targetSlot.GaugeCharged >= targetSlot.GaugeRequired) {
                targetSlot.Quota = targetSlot.Quota + 1;
                targetSlot.GaugeCharged -= targetSlot.GaugeRequired;
            }
    
            if (targetSlot.Quota >= targetSlot.DefaultQuota) {
                targetSlot.Quota = targetSlot.DefaultQuota;
                targetSlot.GaugeCharged = 0;
            }
        }
    }
}
