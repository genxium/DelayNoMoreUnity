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

        private static void _calcTrapMovementPushbacks(RoomDownsyncFrame currRenderFrame, RepeatedField<Trap> nextRenderFrameTraps, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, FrameRingBuffer<Collider> residueCollided, ILoggerBridge logger) {
            int primaryHardOverlapIndex;
            for (int i = trapColliderCntOffset; i < bulletColliderCntOffset; i++) {
                primaryOverlapResult.reset();
                Collider aCollider = dynamicRectangleColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;

                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where trapColliderCntOffset=" + trapColliderCntOffset + ", bulletColliderCntOffset=" + bulletColliderCntOffset);
                }

                if (!colliderAttr.ProvidesHardPushback) {
                    // [WARNING] By now only the parts that provide hardPushback would interact with barriers and other hardPushback traps!
                    continue;
                }

                ConvexPolygon aShape = aCollider.Shape;

                bool hitsAnActualBarrier;
                int hardPushbackCnt = calcHardPushbacksNormsForTrap(colliderAttr, aCollider, aShape, hardPushbackNormsArr[i], collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, residueCollided, out hitsAnActualBarrier, logger);

                if (0 < hardPushbackCnt) {
                    // We don't have any trap-slope interaction designed yet
                    processPrimaryAndImpactEffPushback(effPushbacks[i], hardPushbackNormsArr[i], hardPushbackCnt, primaryHardOverlapIndex, 0);

                    if (hitsAnActualBarrier) {
                        var trapInNextRenderFrame = nextRenderFrameTraps[i - trapColliderCntOffset];
                        trapInNextRenderFrame.VelX = 0;
                        trapInNextRenderFrame.VelY = 0;
                    }
                }
            }
        }

        private static void _calcTrapDamageCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, Collision collision, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, List<Collider> completelyStaticTrapColliders, ILoggerBridge logger) {
            // [WARNING] Like Bullet, Trap damage collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
            int ed = bulletColliderCntOffset + completelyStaticTrapColliders.Count;
            for (int i = trapColliderCntOffset; i < ed; i++) {
                Collider aCollider = i < bulletColliderCntOffset ? dynamicRectangleColliders[i] : completelyStaticTrapColliders[i-bulletColliderCntOffset];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;

                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where trapColliderCntOffset=" + trapColliderCntOffset + ", bulletColliderCntOffset=" + bulletColliderCntOffset);
                }

                if (!colliderAttr.ProvidesDamage) {
                    continue;
                }
                
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
                if (!collided) continue;

                var aShape = aCollider.Shape;
                var currTrap = currRenderFrame.TrapsArr[colliderAttr.TrapLocalId];
                var trapConfig = currTrap.Config;
                while (true) {
                    var (ok, bCollider) = collision.PopFirstContactedCollider();
                    if (false == ok || null == bCollider) {
                        break;
                    }
                    var defenderShape = bCollider.Shape;
                    var (overlapped, _, _) = calcPushbacks(0, 0, aShape, defenderShape, false, ref overlapResult);
                    if (!overlapped) continue;

                    switch (bCollider.Data) {
                        case CharacterDownsync atkedCharacterInCurrFrame:
                            if (invinsibleSet.Contains(atkedCharacterInCurrFrame.CharacterState)) continue;
                            if (0 < atkedCharacterInCurrFrame.FramesInvinsible) continue;
                            
                            int atkedJ = atkedCharacterInCurrFrame.JoinIndex - 1;
                            var atkedCharacterInNextFrame = (atkedJ < roomCapacity ? nextRenderFramePlayers[atkedJ] : nextRenderFrameNpcs[atkedJ - roomCapacity]);
                            atkedCharacterInNextFrame.Hp -= trapConfig.Damage;
                            // [WARNING] Deliberately NOT assigning to "atkedCharacterInNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
                            
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
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public static void calcTrapBoxInCollisionSpace(TrapColliderAttr colliderAttr, int newVx, int newVy, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(newVx + colliderAttr.HitboxOffsetX, newVy + colliderAttr.HitboxOffsetY);
            (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(colliderAttr.HitboxSizeX, colliderAttr.HitboxSizeY);
        }
    }
}
