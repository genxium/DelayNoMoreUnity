using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace shared {
    public partial class Battle {
        private static void _moveAndInsertDynamicTrapColliders(RoomDownsyncFrame currRenderFrame, RepeatedField<Trap> nextRenderFrameTraps, Vector[] effPushbacks, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, ref int colliderCnt, int trapColliderCntOffset, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, ILoggerBridge logger) {
            var currRenderFrameTraps = currRenderFrame.TrapsArr;
            for (int i = 0; i < currRenderFrameTraps.Count; i++) {
                var currTrap = currRenderFrameTraps[i];
                if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) continue;
                if (currTrap.IsCompletelyStatic) continue;
                int newVx = currTrap.VirtualGridX + currTrap.VelX, newVy = currTrap.VirtualGridY + currTrap.VelY;
                effPushbacks[i + trapColliderCntOffset].X = 0;
                effPushbacks[i + trapColliderCntOffset].Y = 0;

                List<TrapColliderAttr> colliderAttrs = trapLocalIdToColliderAttrs[currTrap.TrapLocalId];
                for (int j = 0; j < colliderAttrs.Count; j++) {
                    var colliderAttr = colliderAttrs[j];
                    Collider trapCollider = dynamicRectangleColliders[colliderCnt];
                    float boxCx, boxCy, boxCw, boxCh;
                    calcTrapBoxInCollisionSpace(colliderAttr, newVx, newVy, out boxCx, out boxCy, out boxCw, out boxCh);
                    UpdateRectCollider(trapCollider, boxCx, boxCy, boxCw, boxCh, 0, 0, 0, 0, 0, 0, colliderAttr); 
                    colliderCnt++;

                    // Add to collision system
                    collisionSys.AddSingle(trapCollider);
                }
            }
        }

        private static void _calcDynamicTrapMovementCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, InputFrameDecoded decodedInputHolder, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, FrameRingBuffer<Collider> residueCollided, ILoggerBridge logger) {
            int primaryHardOverlapIndex;
            for (int i = trapColliderCntOffset; i < bulletColliderCntOffset; i++) {
                primaryOverlapResult.reset();
                Collider aCollider = dynamicRectangleColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;

                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where trapColliderCntOffset=" + trapColliderCntOffset + ", bulletColliderCntOffset=" + bulletColliderCntOffset + ", aCollider.Data=" + aCollider.Data);
                }

                ConvexPolygon aShape = aCollider.Shape;

                bool hitsAnActualBarrier;
                int hardPushbackCnt = calcHardPushbacksNormsForTrap(colliderAttr, aCollider, aShape, hardPushbackNormsArr[i], collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, residueCollided, out hitsAnActualBarrier, logger);

                if (colliderAttr.ProvidesHardPushback && 0 < hardPushbackCnt) {
                    // We don't have any trap-slope interaction designed yet
                    processPrimaryAndImpactEffPushback(effPushbacks[i], hardPushbackNormsArr[i], hardPushbackCnt, primaryHardOverlapIndex, 0);

                    if (hitsAnActualBarrier) {
                        var trapInNextRenderFrame = nextRenderFrameTraps[i - trapColliderCntOffset];
                        trapInNextRenderFrame.VelX = 0;
                        trapInNextRenderFrame.VelY = 0;
                    }
                }

                while (true) {
                    var (ok3, bCollider) = residueCollided.Pop();
                    if (false == ok3 || null == bCollider) {
                        break;
                    }
                    // [WARNING] "bCollider" from "residueCollided" has NOT been checked by shape collision! 
                    bool maskMatched = true, isBarrier = false, isAnotherCharacter = false, isBullet = false, isPatrolCue = false;
                    switch (bCollider.Data) {
                        case CharacterDownsync v1:
                            if (!COLLIDABLE_PAIRS.Contains(v1.CollisionTypeMask | colliderAttr.CollisionTypeMask)) {
                                maskMatched = false;
                                break;
                            }
                            isAnotherCharacter = true;
                            break;
                        case Bullet v2:
                            if (!COLLIDABLE_PAIRS.Contains(v2.Config.CollisionTypeMask | colliderAttr.CollisionTypeMask)) {
                                maskMatched = false;
                                break;
                            }
                            isBullet = true;
                            break;
                        case PatrolCue v3:
                            if (!COLLIDABLE_PAIRS.Contains(v3.CollisionTypeMask | colliderAttr.CollisionTypeMask)) {
                                maskMatched = false;
                                break;
                            }
                            isPatrolCue = true;
                            break;
                        default:
                            // By default it's a regular barrier, even if data is nil
                            isBarrier = true;
                            break;
                    }
                    if (false == maskMatched) {
                        continue;
                    }
                    if (isBullet || isBarrier) {
                        continue;
                    }

                    if (isAnotherCharacter) {
                        var atkedCharacterInCurrFrame = bCollider.Data as CharacterDownsync;
                        if (null == atkedCharacterInCurrFrame) {
                            throw new ArgumentNullException("The casting into atkedCharacterInCurrFrame shouldn't be null for bCollider.Data=" + bCollider.Data);
                        }
                        if (colliderAttr.ProvidesDamage) {
                            _processSingleTrapDamageOnSingleCharacter(currRenderFrame, aShape, bCollider.Shape, ref overlapResult, colliderAttr, atkedCharacterInCurrFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, logger);
                        }
                    }

                    if (isPatrolCue) {
                        var patrolCue = bCollider.Data as PatrolCue;
                        if (null == patrolCue) {
                            throw new ArgumentNullException("The casting into patrolCue shouldn't be null for bCollider.Data=" + bCollider.Data);
                        } 
                        _processSingleTrapOnSinglePatrolCue(currRenderFrame, nextRenderFrameTraps, ref overlapResult, aCollider, bCollider, decodedInputHolder, colliderAttr, patrolCue, logger);
                    }
                }
            }
        }

        private static void _calcCompletelyStaticTrapDamage(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, Collision collision, List<Collider> completelyStaticTrapColliders, ILoggerBridge logger) {
            for (int i = 0; i < completelyStaticTrapColliders.Count; i++) {
                Collider aCollider = completelyStaticTrapColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;

                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for completelyStaticTrapColliders[i=" + i + "]");
                }

                if (!colliderAttr.ProvidesDamage) {
                    continue;
                }
                
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
                if (!collided) continue;

                while (true) {
                    var (ok, bCollider) = collision.PopFirstContactedCollider();
                    if (false == ok || null == bCollider) {
                        break;
                    }

                    switch (bCollider.Data) {
                        case CharacterDownsync atkedCharacterInCurrFrame:
                            _processSingleTrapDamageOnSingleCharacter(currRenderFrame, aCollider.Shape, bCollider.Shape, ref overlapResult, colliderAttr, atkedCharacterInCurrFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, logger);
                        break;
                    }
                }
            }
        }

        public static void calcTrapBoxInCollisionSpace(TrapColliderAttr colliderAttr, int newVx, int newVy, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(newVx + colliderAttr.HitboxOffsetX, newVy + colliderAttr.HitboxOffsetY);
            (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(colliderAttr.HitboxSizeX, colliderAttr.HitboxSizeY);
        }

        private static void _processSingleTrapDamageOnSingleCharacter(RoomDownsyncFrame currRenderFrame, ConvexPolygon aShape, ConvexPolygon bShape, ref SatResult overlapResult, TrapColliderAttr colliderAttr, CharacterDownsync atkedCharacterInCurrFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ILoggerBridge logger) {
            if (invinsibleSet.Contains(atkedCharacterInCurrFrame.CharacterState)) return;
            int atkedJ = atkedCharacterInCurrFrame.JoinIndex - 1;
            var atkedCharacterInNextFrame = (atkedJ < roomCapacity ? nextRenderFramePlayers[atkedJ] : nextRenderFrameNpcs[atkedJ - roomCapacity]);
            // [WARNING] As trap damage is calculated after those of bullets, don't overwrite blown-up effect!
            if (CharacterState.BlownUp1 == atkedCharacterInNextFrame.CharacterState) {
                return;
            }
            if (0 < atkedCharacterInCurrFrame.FramesInvinsible) return;

            var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
            if (!overlapped) {
                return;
            }

            var currTrap = currRenderFrame.TrapsArr[colliderAttr.TrapLocalId];
            var trapConfig = currTrap.Config;

            /* [WARNING] 
                Like Bullet, Trap damage collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
                Deliberately NOT assigning to "atkedCharacterInNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
            */
            atkedCharacterInNextFrame.Hp -= trapConfig.Damage;

            if (0 >= atkedCharacterInNextFrame.Hp) {
                // [WARNING] We don't have "dying in air" animation for now, and for better graphical recognition, play the same dying animation even in air
                atkedCharacterInNextFrame.Hp = 0;
                atkedCharacterInNextFrame.VelX = 0; // yet no need to change "VelY" because it could be falling
                atkedCharacterInNextFrame.CharacterState = CharacterState.Dying;
                atkedCharacterInNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
            } else {
                bool shouldOmitStun = ((0 >= trapConfig.HitStunFrames) || (atkedCharacterInCurrFrame.OmitPushback));
                if (false == shouldOmitStun) {
                    if (trapConfig.BlowUp) {
                        atkedCharacterInNextFrame.CharacterState = CharacterState.BlownUp1;
                    } else {
                        atkedCharacterInNextFrame.CharacterState = CharacterState.Atked1;
                    }
                    int oldFramesToRecover = atkedCharacterInNextFrame.FramesToRecover;
                    if (trapConfig.HitStunFrames > oldFramesToRecover) {
                        atkedCharacterInNextFrame.FramesToRecover = trapConfig.HitStunFrames;
                    }
                    int oldInvincibleFrames = atkedCharacterInNextFrame.FramesInvinsible;
                    if (trapConfig.HitInvinsibleFrames > oldInvincibleFrames) {
                        atkedCharacterInNextFrame.FramesInvinsible = trapConfig.HitInvinsibleFrames;
                    }
                    atkedCharacterInNextFrame.VelX = 0;
                    atkedCharacterInNextFrame.VelY = 0;
                }
            }
        }

        private static void _processSingleTrapOnSinglePatrolCue(RoomDownsyncFrame currRenderFrame, RepeatedField<Trap> nextRenderFrameTraps, ref SatResult overlapResult, Collider aCollider, Collider bCollider, InputFrameDecoded decodedInputHolder, TrapColliderAttr colliderAttr, PatrolCue patrolCue, ILoggerBridge logger) {
            var currTrap = currRenderFrame.TrapsArr[colliderAttr.TrapLocalId];
            var nextTrap = nextRenderFrameTraps[colliderAttr.TrapLocalId];
            bool prevCapturedByPatrolCue = currTrap.CapturedByPatrolCue;
            bool shouldWaivePatrolCueReaction = (false == prevCapturedByPatrolCue && 0 < currTrap.FramesInPatrolCue && patrolCue.Id == currTrap.WaivingPatrolCueId);

            if (shouldWaivePatrolCueReaction) {
                // [WARNING] We can have this "early return" block to be before the "shape overlap check" here, because we don't need data from "decodedInputHolder". 
                return;
            }
            ConvexPolygon aShape = aCollider.Shape;
            ConvexPolygon bShape = bCollider.Shape;

            var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
            if (!overlapped) {
                return;
            }

            // By now we're sure that it should react to the PatrolCue
            var colliderDx = (aCollider.X - bCollider.X);
            var colliderDy = (aCollider.Y - bCollider.Y);
            bool fr = 0 < colliderDx && (0 > currTrap.VelX || (0 == currTrap.VelX && 0 > currTrap.DirX));
            bool fl = 0 > colliderDx && (0 < currTrap.VelX || (0 == currTrap.VelX && 0 < currTrap.DirX));
            bool fu = 0 < colliderDy && (0 > currTrap.VelY || (0 == currTrap.VelY && 0 > currTrap.DirY));
            bool fd = 0 > colliderDy && (0 < currTrap.VelY || (0 == currTrap.VelY && 0 < currTrap.DirY));
            if (!fr && !fl && !fu && !fd) {
                fr = 0 > currTrap.DirX;
                fl = 0 < currTrap.DirX;
            }

            int targetFramesInPatrolCue = 0;
            if (fr) {
                targetFramesInPatrolCue = (int)patrolCue.FrCaptureFrames;
                DecodeInput(patrolCue.FrAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the right", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue)); 
            } else if (fl) {
                targetFramesInPatrolCue = (int)patrolCue.FlCaptureFrames;
                DecodeInput(patrolCue.FlAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the left", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue));
            } else if (fu) {
                targetFramesInPatrolCue = (int)patrolCue.FuCaptureFrames;
                DecodeInput(patrolCue.FuAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the top", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue)); 
            } else if (fd) {
                targetFramesInPatrolCue = (int)patrolCue.FdCaptureFrames;
                DecodeInput(patrolCue.FdAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the bottom", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue)); 
            } else {
                //logger.LogWarn(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3}, dirX: {9} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} but direction couldn't be determined!", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue, currTrap.DirX));
            }

            bool shouldBreakPatrolCueCapture = ((true == prevCapturedByPatrolCue) && (currTrap.WaivingPatrolCueId == patrolCue.Id) && (0 == currTrap.FramesInPatrolCue));

            bool shouldEnterCapturedPeriod = ((false == prevCapturedByPatrolCue) && (false == shouldBreakPatrolCueCapture) && (0 < targetFramesInPatrolCue));

            if (shouldEnterCapturedPeriod) {
                nextTrap.CapturedByPatrolCue = true;
                nextTrap.FramesInPatrolCue = targetFramesInPatrolCue;
                nextTrap.WaivingPatrolCueId = patrolCue.Id;
                nextTrap.VelX = 0;
                nextTrap.VelY = 0;
                return;
            }

            bool isReallyCaptured = ((true == prevCapturedByPatrolCue) && (false == shouldBreakPatrolCueCapture) && (patrolCue.Id == currTrap.WaivingPatrolCueId) && (0 < currTrap.FramesInPatrolCue));
            if (isReallyCaptured) {
                return;
            }

            nextTrap.CapturedByPatrolCue = false;
            nextTrap.FramesInPatrolCue = DEFAULT_PATROL_CUE_WAIVING_FRAMES; // re-purposed
            nextTrap.WaivingPatrolCueId = patrolCue.Id;
            nextTrap.DirX = decodedInputHolder.Dx;
            nextTrap.DirY = decodedInputHolder.Dy;

            var dirMagSq = nextTrap.DirX * nextTrap.DirX + nextTrap.DirY * nextTrap.DirY;
            var invDirMag = InvSqrt32(dirMagSq);
            var speedXfac = invDirMag * nextTrap.DirX;
            var speedYfac = invDirMag * nextTrap.DirY;
            var speedVal = currTrap.ConfigFromTiled.Speed;
            nextTrap.VelX = (int)(speedXfac * speedVal); 
            nextTrap.VelY = (int)(speedYfac * speedVal);
        }
    }
}
