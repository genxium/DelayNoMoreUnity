using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;

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
                foreach (var colliderAttr in colliderAttrs) {
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

        private static void _calcDynamicTrapMovementCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, FrameRingBuffer<Collider> residueCollided, ILoggerBridge logger) {
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

                    if (isAnotherCharacter && colliderAttr.ProvidesDamage) {
                        var atkedCharacterInCurrFrame = bCollider.Data as CharacterDownsync;
                        if (null == atkedCharacterInCurrFrame) {
                            throw new ArgumentNullException("The casting into atkedCharacterInCurrFrame shouldn't be null for bCollider.Data=" + atkedCharacterInCurrFrame);
                        } 
                        _processSingleTrapDamageOnSingleCharacter(currRenderFrame, aShape, bCollider.Shape, ref overlapResult, colliderAttr, atkedCharacterInCurrFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, logger);
                    }

                    if (isPatrolCue) {
                        // TODO
                    }
                }
            }
        }


        private static void _calcCompletelyStaticTrapDamage(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, Collision collision, FrameRingBuffer<Collider> residueCollided, List<Collider> completelyStaticTrapColliders, ILoggerBridge logger) {
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
            if (0 < atkedCharacterInCurrFrame.FramesInvinsible) return;

            var (overlapped, softPushbackX, softPushbackY) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
            if (!overlapped) {
                return;
            }

            var currTrap = currRenderFrame.TrapsArr[colliderAttr.TrapLocalId];
            var trapConfig = currTrap.Config;

            /* [WARNING] 
                Like Bullet, Trap damage collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
                Deliberately NOT assigning to "atkedCharacterInNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
            */
            int atkedJ = atkedCharacterInCurrFrame.JoinIndex - 1;
            var atkedCharacterInNextFrame = (atkedJ < roomCapacity ? nextRenderFramePlayers[atkedJ] : nextRenderFrameNpcs[atkedJ - roomCapacity]);
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

        private static void _processTrapInputs(RoomDownsyncFrame currRenderFrame, RepeatedField<Trap> nextRenderFrameTraps, Collider[] dynamicRectangleColliders, Collision collision, CollisionSpace collisionSys, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, int trapColliderCntOffset, int colliderCnt, ILoggerBridge logger) {
        }
    }
}
