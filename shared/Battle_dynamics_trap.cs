using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        private static void _moveAndInsertDynamicTrapColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<Trap> nextRenderFrameTraps, Vector[] effPushbacks, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, ref int colliderCnt, int trapColliderCntOffset, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, ILoggerBridge logger) {
            var currRenderFrameTraps = currRenderFrame.TrapsArr;
            for (int i = 0; i < currRenderFrameTraps.Count; i++) {
                var src = currRenderFrameTraps[i];
                if (TERMINATING_TRAP_ID == src.TrapLocalId) break;
                if (src.IsCompletelyStatic) continue;
                
                int newVx = src.VirtualGridX + src.VelX, newVy = src.VirtualGridY + src.VelY;

                bool spinFlipX = (0 > src.DirX);
                var dst = nextRenderFrameTraps[i];
                int dstVelX = src.VelX, dstVelY = src.VelY;
                float dstSpinCos = src.SpinCos, dstSpinSin = src.SpinSin;
                if (0 != src.AngularFrameVelCos || 0 != src.AngularFrameVelSin) {
                    float dstVelXFloat = 0f, dstVelYFloat = 0f;
                    if (!spinFlipX) {
                        Vector.Rotate(src.SpinCos, src.SpinSin, src.AngularFrameVelCos, src.AngularFrameVelSin, out dstSpinCos, out dstSpinSin);
                        Vector.Rotate(src.VelX, src.VelY, src.AngularFrameVelCos, src.AngularFrameVelSin, out dstVelXFloat, out dstVelYFloat);
                    } else {
                        Vector.Rotate(src.SpinCos, src.SpinSin, src.AngularFrameVelCos, -src.AngularFrameVelSin, out dstSpinCos, out dstSpinSin);
                        Vector.Rotate(src.VelX, src.VelY, src.AngularFrameVelCos, -src.AngularFrameVelSin, out dstVelXFloat, out dstVelYFloat);
                    }
                    dstVelX = (int)Math.Ceiling(dstVelXFloat);
                    dstVelY = (int)Math.Ceiling(dstVelYFloat);
                }

                dst.VelX = dstVelX;
                dst.VelY = dstVelY;
                dst.SpinCos = dstSpinCos;
                dst.SpinSin = dstSpinSin;
                dst.VirtualGridX = newVx;
                dst.VirtualGridY = newVy;

                var srcConfig = trapConfigs[src.ConfigFromTiled.SpeciesId];
                bool isTrapRotary = (0 != srcConfig.AngularFrameVelCos);
                List<TrapColliderAttr> colliderAttrs = trapLocalIdToColliderAttrs[src.TrapLocalId];
                for (int j = 0; j < colliderAttrs.Count; j++) {
                    var colliderAttr = colliderAttrs[j];
                    Collider trapCollider = dynamicRectangleColliders[colliderCnt];
                    effPushbacks[colliderCnt].X = 0;
                    effPushbacks[colliderCnt].Y = 0;
                    float boxCx, boxCy, boxCw, boxCh;
                    calcTrapBoxInCollisionSpace(colliderAttr, newVx, newVy, out boxCx, out boxCy, out boxCw, out boxCh);
                    if (0 != src.SpinCos || 0 != src.SpinSin) {
                        // [WARNING] "colliderAttr.HitboxOffsetX" is from trap tile object center to collider center, thus is half the left edge offset. Same applies to "colliderAttr.HitboxOffsetY".
                        var (anchorOffsetCx, anchorOffsetCy) = VirtualGridToPolygonColliderCtr((colliderAttr.HitboxOffsetX << 1), (colliderAttr.HitboxOffsetY << 1));
                        UpdateRectCollider(trapCollider, boxCx, boxCy, boxCw, boxCh, 0, 0, 0, 0, 0, 0, colliderAttr, colliderAttr.CollisionTypeMask, spinFlipX, isRotary: isTrapRotary, srcConfig.SpinAnchorX - anchorOffsetCx, srcConfig.SpinAnchorY - anchorOffsetCy, src.SpinCos, src.SpinSin); // [WARNING] Deliberately NOT using "dstSpinCos & dstSpinSin" to allow initial frame stopping by a patrol cue.
                    } else {
                        UpdateRectCollider(trapCollider, boxCx, boxCy, boxCw, boxCh, 0, 0, 0, 0, 0, 0, colliderAttr, colliderAttr.CollisionTypeMask);
                    }
                    
                    colliderCnt++;

                    // Add to collision system
                    collisionSys.AddSingleToCellTail(trapCollider);
                }    
            }
        }

        private static void _calcDynamicTrapMovementCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, InputFrameDecoded decodedInputHolder, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int trapColliderCntOffsetEd, FrameRingBuffer<Collider> residueCollided, ILoggerBridge logger) {
            int primaryHardOverlapIndex;
            for (int i = trapColliderCntOffset; i < trapColliderCntOffsetEd; i++) {
                primaryOverlapResult.reset();
                Collider aCollider = dynamicRectangleColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;
                
                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where trapColliderCntOffset=" + trapColliderCntOffset + ", trapColliderCntOffsetEd=" + trapColliderCntOffsetEd + ", aCollider.Data=" + aCollider.Data);
                }
                Trap currTrap = currRenderFrame.TrapsArr[colliderAttr.TrapLocalId-1];
                ConvexPolygon aShape = aCollider.Shape;

                bool hitsAnActualBarrier;
                int hardPushbackCnt = calcHardPushbacksNormsForTrap(currRenderFrame, colliderAttr, aCollider, aShape, hardPushbackNormsArr[i], collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, residueCollided, out hitsAnActualBarrier, logger);

                if (colliderAttr.ProvidesHardPushback && 0 < hardPushbackCnt) {
                    // We don't have any trap-slope interaction designed yet
                    processPrimaryAndImpactEffPushback(effPushbacks[i], hardPushbackNormsArr[i], hardPushbackCnt, primaryHardOverlapIndex, 0, false);

                    if (hitsAnActualBarrier) {
                        float primaryPushbackX = hardPushbackNormsArr[i][primaryHardOverlapIndex].X;
                        float primaryPushbackY = hardPushbackNormsArr[i][primaryHardOverlapIndex].Y;
                        float velProjected = currTrap.VelX*primaryPushbackX + currTrap.VelY*primaryPushbackY;
                        if (SNAP_INTO_PLATFORM_THRESHOLD < Math.Abs(velProjected)) {
                            var trapInNextRenderFrame = nextRenderFrameTraps[colliderAttr.TrapLocalId-1];
                            trapInNextRenderFrame.VelX = 0;
                            trapInNextRenderFrame.VelY = 0;
                        }
                    }
                }

                while (true) {
                    var (ok3, bCollider) = residueCollided.Pop();
                    if (false == ok3 || null == bCollider) {
                        break;
                    }
                    // [WARNING] "bCollider" from "residueCollided" has NOT been checked by shape collision! 
                    bool isBarrier = false, isAnotherCharacter = false, isBullet = false, isPatrolCue = false;
                    switch (bCollider.Data) {
                        case CharacterDownsync v1:
                            isAnotherCharacter = true;
                            break;
                        case Bullet v2:
                            isBullet = true;
                            break;
                        case PatrolCue v3:
                            isPatrolCue = true;
                            break;
                        case TrapColliderAttr v4:
                        case TriggerColliderAttr v5:
                            break;
                        default:
                            // By default it's a regular barrier, even if data is nil
                            isBarrier = true;
                            break;
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
                            _processSingleTrapDamageOnSingleCharacter(currRenderFrame, aShape, bCollider.Shape, ref overlapResult, colliderAttr, atkedCharacterInCurrFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, logger);
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

        private static void _calcCompletelyStaticTrapDamage(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, Collision collision, List<Collider> completelyStaticTrapColliders, ILoggerBridge logger) {
            for (int i = 0; i < completelyStaticTrapColliders.Count; i++) {
                Collider aCollider = completelyStaticTrapColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;

                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for completelyStaticTrapColliders[i=" + i + "]");
                }

                if (!colliderAttr.ProvidesDamage) {
                    continue;
                }
                
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision, COLLIDABLE_PAIRS);
                if (!collided) continue;

                while (true) {
                    var (ok, bCollider) = collision.PopFirstContactedCollider();
                    if (false == ok || null == bCollider) {
                        break;
                    }

                    switch (bCollider.Data) {
                        case CharacterDownsync atkedCharacterInCurrFrame:
                            _processSingleTrapDamageOnSingleCharacter(currRenderFrame, aCollider.Shape, bCollider.Shape, ref overlapResult, colliderAttr, atkedCharacterInCurrFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, logger);
                        break;
                    }
                }
            }
        }

        public static void calcTrapBoxInCollisionSpace(TrapColliderAttr colliderAttr, int newVx, int newVy, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(newVx + colliderAttr.HitboxOffsetX, newVy + colliderAttr.HitboxOffsetY);
            (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(colliderAttr.HitboxSizeX, colliderAttr.HitboxSizeY);
        }

        private static void _processSingleTrapDamageOnSingleCharacter(RoomDownsyncFrame currRenderFrame, ConvexPolygon aShape, ConvexPolygon bShape, ref SatResult overlapResult, TrapColliderAttr colliderAttr, CharacterDownsync atkedCharacterInCurrFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ILoggerBridge logger) {
            // The traversal order of traps (both static & dynamic) is deterministic, thus the following assignment is deterministic regardless of the order of collision result popping.
            if (invinsibleSet.Contains(atkedCharacterInCurrFrame.CharacterState)) return;
            int atkedJ = atkedCharacterInCurrFrame.JoinIndex - 1;
            var atkedCharacterInNextFrame = (atkedJ < roomCapacity ? nextRenderFramePlayers[atkedJ] : nextRenderFrameNpcs[atkedJ - roomCapacity]);
            // [WARNING] As trap damage is calculated after those of bullets, don't overwrite blown-up effect!
            if (0 < atkedCharacterInCurrFrame.FramesInvinsible) return;

            var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult);
            if (!overlapped) {
                return;
            }

            var trapConfig = trapConfigs[colliderAttr.SpeciesId];

            /* [WARNING] 
                Like Bullet, Trap damage collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
                Deliberately NOT assigning to "atkedCharacterInNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
            */
            atkedCharacterInNextFrame.Hp -= trapConfig.Damage;
            atkedCharacterInNextFrame.FramesSinceLastDamaged = DEFAULT_FRAMES_TO_SHOW_DAMAGED;

            if (0 >= atkedCharacterInNextFrame.Hp) {
                // [WARNING] We don't have "dying in air" animation for now, and for better graphical recognition, play the same dying animation even in air
                atkedCharacterInNextFrame.Hp = 0;
                atkedCharacterInNextFrame.VelX = 0; // yet no need to change "VelY" because it could be falling
                atkedCharacterInNextFrame.CharacterState = Dying;
                atkedCharacterInNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
                resetJumpStartupOrHolding(atkedCharacterInNextFrame, true);
            } else {
                var atkedChConfig = characters[atkedCharacterInNextFrame.SpeciesId];
                bool shouldOmitHitPushback = (atkedChConfig.Hardness > trapConfig.Hardness);   
                bool shouldOmitStun = ((0 >= trapConfig.HitStunFrames) || (shouldOmitHitPushback));
                if (false == shouldOmitStun) {
                    resetJumpStartupOrHolding(atkedCharacterInNextFrame, true);
                    if (trapConfig.BlowUp) {
                        atkedCharacterInNextFrame.CharacterState = BlownUp1;
                        if (atkedChConfig.OmitGravity) {
                            atkedCharacterInNextFrame.FramesToRecover = DEFAULT_BLOWNUP_FRAMES_FOR_FLYING; 
                        } else {
                            atkedCharacterInNextFrame.OmitGravity = false; 
                        }
                    } else if (BlownUp1 != atkedCharacterInNextFrame.CharacterState) {
                        if (CrouchIdle1 == atkedCharacterInNextFrame.CharacterState || CrouchIdle1 == atkedCharacterInNextFrame.CharacterState || CrouchAtked1 == atkedCharacterInNextFrame.CharacterState) {
                            atkedCharacterInNextFrame.CharacterState = CrouchAtked1;
                        } else {
                            atkedCharacterInNextFrame.CharacterState = Atked1;
                        }
                    }
                    var oldFramesToRecover = atkedCharacterInNextFrame.FramesToRecover;
                    if (trapConfig.HitStunFrames > oldFramesToRecover) {
                        atkedCharacterInNextFrame.FramesToRecover = trapConfig.HitStunFrames;
                    }
                    atkedCharacterInNextFrame.VelX = 0;
                    atkedCharacterInNextFrame.VelY = 0;
                }

                var oldInvincibleFrames = atkedCharacterInNextFrame.FramesInvinsible;
                if (trapConfig.HitInvinsibleFrames > oldInvincibleFrames) {
                    atkedCharacterInNextFrame.FramesInvinsible = trapConfig.HitInvinsibleFrames;
                }
            }
        }

        private static void _processSingleTrapOnSinglePatrolCue(RoomDownsyncFrame currRenderFrame, RepeatedField<Trap> nextRenderFrameTraps, ref SatResult overlapResult, Collider aCollider, Collider bCollider, InputFrameDecoded decodedInputHolder, TrapColliderAttr colliderAttr, PatrolCue patrolCue, ILoggerBridge logger) {
            decodedInputHolder.Reset();
            var currTrap = currRenderFrame.TrapsArr[colliderAttr.TrapLocalId-1];
            var nextTrap = nextRenderFrameTraps[colliderAttr.TrapLocalId-1];
            var trapConfig = trapConfigs[currTrap.ConfigFromTiled.SpeciesId];
            bool prevCapturedByPatrolCue = currTrap.CapturedByPatrolCue;
            bool shouldWaivePatrolCueReaction = (false == prevCapturedByPatrolCue && 0 < currTrap.FramesInPatrolCue && patrolCue.Id == currTrap.WaivingPatrolCueId);

            if (shouldWaivePatrolCueReaction) {
                // [WARNING] We can have this "early return" block to be before the "shape overlap check" here, because we don't need data from "decodedInputHolder". 
                return;
            }
            ConvexPolygon aShape = aCollider.Shape;
            ConvexPolygon bShape = bCollider.Shape;

            var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult);
            if (!overlapped) {
                return;
            }
            
            if (trapConfig.PatrolCueRequiresFullContain && !overlapResult.BContainedInA) {
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
                if (0 != currTrap.ConfigFromTiled.Speed) {
                    fr = 0 > currTrap.DirX;
                    fl = 0 < currTrap.DirX;
                    fd = 0 < currTrap.DirY;
                    fu = 0 > currTrap.DirY;
                } else if (0 != trapConfig.AngularFrameVelCos || 0 != trapConfig.AngularFrameVelSin) {
                    if (0 != currTrap.AngularFrameVelSin) {
                        fr = 0 < currTrap.AngularFrameVelSin;
                        fl = 0 > currTrap.AngularFrameVelSin;
                    } else {
                        float toBeReleasedAngularFrameVelSin = 0;
                        if (0 < (currTrap.PatrolCueAngularVelFlipMark & 1)) {
                            toBeReleasedAngularFrameVelSin = -trapConfig.AngularFrameVelSin;
                        } else {
                            toBeReleasedAngularFrameVelSin = trapConfig.AngularFrameVelSin;
                        }
                        fr = 0 < toBeReleasedAngularFrameVelSin;
                        fl = 0 > toBeReleasedAngularFrameVelSin;
                    }
                    fd = false;
                    fu = false;
                }
            }

            int targetFramesInPatrolCue = 0;
            if (fr) {
                targetFramesInPatrolCue = patrolCue.FrCaptureFrames;
                DecodeInput(patrolCue.FrAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the right", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue)); 
            } else if (fl) {
                targetFramesInPatrolCue = patrolCue.FlCaptureFrames;
                DecodeInput(patrolCue.FlAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the left", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue));
            } else if (fu) {
                targetFramesInPatrolCue = patrolCue.FuCaptureFrames;
                DecodeInput(patrolCue.FuAct, decodedInputHolder);
                //logger.LogInfo(String.Format("Trap aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the top", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, patrolCue)); 
            } else if (fd) {
                targetFramesInPatrolCue = patrolCue.FdCaptureFrames;
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
                if (0 != decodedInputHolder.Dx || 0 != decodedInputHolder.Dy) {
                    nextTrap.DirX = decodedInputHolder.Dx;
                    nextTrap.DirY = decodedInputHolder.Dy;
                }
                nextTrap.VelX = 0;
                nextTrap.VelY = 0;
                nextTrap.AngularFrameVelCos = 0;
                nextTrap.AngularFrameVelSin = 0;
                if ((0 != trapConfig.AngularFrameVelCos || 0 != trapConfig.AngularFrameVelSin) && 0 < decodedInputHolder.BtnALevel) {
                    ++nextTrap.PatrolCueAngularVelFlipMark;
                }
                return;
            }

            bool isReallyCaptured = ((true == prevCapturedByPatrolCue) && (false == shouldBreakPatrolCueCapture) && (patrolCue.Id == currTrap.WaivingPatrolCueId) && (0 < currTrap.FramesInPatrolCue));
            if (isReallyCaptured) {
                return;
            }

            nextTrap.CapturedByPatrolCue = false;
            nextTrap.FramesInPatrolCue = DEFAULT_PATROL_CUE_WAIVING_FRAMES; // re-purposed
            nextTrap.WaivingPatrolCueId = patrolCue.Id;
            if (0 != decodedInputHolder.Dx || 0 != decodedInputHolder.Dy) {
                nextTrap.DirX = decodedInputHolder.Dx;
                nextTrap.DirY = decodedInputHolder.Dy;
            }

            var dirMagSq = nextTrap.DirX * nextTrap.DirX + nextTrap.DirY * nextTrap.DirY;
            var invDirMag = InvSqrt32(dirMagSq);
            var speedXfac = invDirMag * nextTrap.DirX;
            var speedYfac = invDirMag * nextTrap.DirY;
            var speedVal = currTrap.ConfigFromTiled.Speed;
            nextTrap.VelX = (int)(speedXfac * speedVal); 
            nextTrap.VelY = (int)(speedYfac * speedVal);
            if (0 != trapConfig.AngularFrameVelCos || 0 != trapConfig.AngularFrameVelSin) {
                if (0 < (currTrap.PatrolCueAngularVelFlipMark & 1)) {
                    nextTrap.AngularFrameVelCos = trapConfig.AngularFrameVelCos;
                    nextTrap.AngularFrameVelSin = -trapConfig.AngularFrameVelSin;
                } else {
                    nextTrap.AngularFrameVelCos = trapConfig.AngularFrameVelCos;
                    nextTrap.AngularFrameVelSin = trapConfig.AngularFrameVelSin;
                }
            }
        }

        private static void _calcTriggerReactions(RoomDownsyncFrame currRenderFrame, RoomDownsyncFrame nextRenderFrame, int roomCapacity, RepeatedField<Trigger> nextRenderFrameTriggers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Dictionary<int, int> triggerEditorIdToLocalId, InputFrameDecoded decodedInputHolder, ref int npcLocalIdCounter, ref int npcCnt, ref ulong fulfilledTriggerSetMask, ref int justTriggeredStoryPointId, ref int justTriggeredBgmId, ILoggerBridge logger) {
            for (int i = 0; i < currRenderFrame.TriggersArr.Count; i++) {
                var currTrigger = currRenderFrame.TriggersArr[i];
                if (TERMINATING_TRIGGER_ID == currTrigger.TriggerLocalId) {
                    break;
                }
                var triggerInNextFrame = nextRenderFrameTriggers[i];
                var triggerConfig = triggerConfigs[currTrigger.ConfigFromTiled.SpeciesId];
                // [WARNING] The ORDER of zero checks of "currTrigger.FramesToRecover" and "currTrigger.FramesToFire" below is important, because we want to avoid "wrong SubCycleQuotaLeft replenishing when 0 == currTrigger.FramesToRecover"!
                
                bool mainCycleFulfilled = (EVTSUB_NO_DEMAND_MASK != currTrigger.DemandedEvtMask && currTrigger.DemandedEvtMask == currTrigger.FulfilledEvtMask);

                if (TRIGGER_SPECIES_TIMED_WAVE_DOOR_1 == triggerConfig.SpeciesId) {
                    if (0 >= currTrigger.FramesToRecover) {
                        if (EVTSUB_NO_DEMAND_MASK == currTrigger.DemandedEvtMask) {
                            // If the TimedWaveDoor doesn't subscribe to any other trigger, it should just tick on its own
                            if (0 < currTrigger.Quota) {
                                mainCycleFulfilled = true;
                            }
                            // The exhaust should still be triggered by NPC-killed
                        } else {
                            if (currTrigger.ConfigFromTiled.QuotaCap > currTrigger.Quota && 0 < currTrigger.Quota) {
                                // If the TimedWaveDoor subscribes to any other trigger, it should ONLY respect that for the initial firing, the rest firings should still be done by ticking on its own
                                mainCycleFulfilled = true;
                            }
                            // The exhaust should still be triggered by NPC-killed
                        }
                    } else {
                        // [WARNING] Regardless of whether or not the TimedWaveDoor subscribers to any other trigger, when (0 < currTrigger.FramesToRecover), it shouldn't fire EXCEPT FOR all spawned NPC-killed 
                        if (0 == currTrigger.Quota && EVTSUB_NO_DEMAND_MASK != currTrigger.DemandedEvtMask) {
                        } else {
                            /*
                            if (mainCycleFulfilled) {
                                logger.LogInfo(String.Format("@rdfId={0}, timed wave trigger editor id = {1}, local id = {2}, of currTrigger:: quota = {3}, framesToRecover = {4}, demandedEvtMask = {3}, fulfilledEvtMask = {4}, waveNpcKilledEvtMaskCounter = {5}, is fulfilled by mask but about to be turned into not fulfilled", currRenderFrame.Id, currTrigger.EditorId, currTrigger.TriggerLocalId, currTrigger.DemandedEvtMask, currTrigger.FulfilledEvtMask, currTrigger.WaveNpcKilledEvtMaskCounter));
                            }
                            */
                            mainCycleFulfilled = false;
                        } 
                    }
                }

                bool subCycleFulfilled = (0 >= currTrigger.FramesToFire);
                switch (triggerConfig.SpeciesId) {
                case TRIGGER_SPECIES_VICTORY_TRIGGER_TRIVIAL:
                case TRIGGER_SPECIES_NPC_AWAKER_MV:
                case TRIGGER_SPECIES_BOSS_AWAKER_MV:
                    if (mainCycleFulfilled) {
                            if (0 < currTrigger.Quota) {
                                triggerInNextFrame.State = TriggerState.TcoolingDown;
                                triggerInNextFrame.FramesInState = 0;
                                triggerInNextFrame.Quota = 0;
                                triggerInNextFrame.FramesToRecover = MAX_INT;
                                triggerInNextFrame.FramesToFire = MAX_INT;
                                justTriggeredBgmId = triggerInNextFrame.ConfigFromTiled.BgmId;
                                fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                                _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, false, false, logger);
                                //logger.LogInfo(String.Format("@rdfId={0}, one-off trigger editor id = {1}, local id = {2} is fulfilled", currRenderFrame.Id, triggerInNextFrame.EditorId ,triggerInNextFrame.TriggerLocalId));
                                if (0 != currTrigger.ConfigFromTiled.NewRevivalX || 0 != currTrigger.ConfigFromTiled.NewRevivalY) {
                                    if (0 < currTrigger.OffenderJoinIndex && currTrigger.OffenderJoinIndex <= roomCapacity) {
                                        var thatCharacterInNextFrame = nextRenderFrame.PlayersArr[currTrigger.OffenderJoinIndex - 1];
                                        thatCharacterInNextFrame.RevivalVirtualGridX = currTrigger.ConfigFromTiled.NewRevivalX;
                                        thatCharacterInNextFrame.RevivalVirtualGridY = currTrigger.ConfigFromTiled.NewRevivalY;
                                    } else if (1 == roomCapacity) {
                                        var thatCharacterInNextFrame = nextRenderFrame.PlayersArr[0];
                                        thatCharacterInNextFrame.RevivalVirtualGridX = currTrigger.ConfigFromTiled.NewRevivalX;
                                        thatCharacterInNextFrame.RevivalVirtualGridY = currTrigger.ConfigFromTiled.NewRevivalY;
                                    }
                                }   
                            }
                    }
                break;
                case TRIGGER_SPECIES_BOSS_SAVEPOINT:
                    if (mainCycleFulfilled) {
                            if (0 < currTrigger.Quota) {
                                triggerInNextFrame.FramesToRecover = DEFAULT_FRAMES_DELAYED_OF_BOSS_SAVEPOINT;
                                triggerInNextFrame.FramesToFire = MAX_INT;
                                if (0 != currTrigger.ConfigFromTiled.NewRevivalX || 0 != currTrigger.ConfigFromTiled.NewRevivalY) {
                                    if (0 < currTrigger.OffenderJoinIndex && currTrigger.OffenderJoinIndex <= roomCapacity) {
                                        var thatCharacterInNextFrame = nextRenderFrame.PlayersArr[currTrigger.OffenderJoinIndex - 1];
                                        thatCharacterInNextFrame.RevivalVirtualGridX = currTrigger.ConfigFromTiled.NewRevivalX;
                                        thatCharacterInNextFrame.RevivalVirtualGridY = currTrigger.ConfigFromTiled.NewRevivalY;
                                    } else if (1 == roomCapacity) {
                                        var thatCharacterInNextFrame = nextRenderFrame.PlayersArr[0];
                                        thatCharacterInNextFrame.RevivalVirtualGridX = currTrigger.ConfigFromTiled.NewRevivalX;
                                        thatCharacterInNextFrame.RevivalVirtualGridY = currTrigger.ConfigFromTiled.NewRevivalY;
                                    }
                                }   
                            }
                    } else if (0 == currTrigger.FramesToRecover) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.FramesToRecover = MAX_INT;
                            triggerInNextFrame.Quota = 0;
                            fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                        }
                    }
                break;
                case TRIGGER_SPECIES_NSWITCH:
                case TRIGGER_SPECIES_PSWITCH:
                    if (mainCycleFulfilled) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.State = TriggerState.TcoolingDown;
                            triggerInNextFrame.FramesInState = 0;
                            triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                            triggerInNextFrame.Quota = currTrigger.Quota - 1;
                            triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                            triggerInNextFrame.FramesToFire = currTrigger.ConfigFromTiled.DelayedFrames;
                            fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                            _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, false, false, logger);
                            //logger.LogInfo(String.Format("@rdfId={0}, switch trigger editor id = {1}, local id = {2} is fulfilled", currRenderFrame.Id, triggerInNextFrame.EditorId ,triggerInNextFrame.TriggerLocalId));
                        }
                    } else if (0 == currTrigger.FramesToRecover) {
                        // replenish upon mainCycle ends, but "false == mainCycleFulfilled"
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.State = TriggerState.Tready;
                            triggerInNextFrame.FramesInState = 0;
                        }
                    }
                break;
                case TRIGGER_SPECIES_STORYPOINT_TRIVIAL:
                case TRIGGER_SPECIES_STORYPOINT_MV:
                    if (mainCycleFulfilled) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.Quota = currTrigger.Quota - 1;
                            triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                            triggerInNextFrame.FramesToFire = currTrigger.ConfigFromTiled.DelayedFrames;
                            justTriggeredStoryPointId = triggerInNextFrame.ConfigFromTiled.StoryPointId;
                            justTriggeredBgmId = triggerInNextFrame.ConfigFromTiled.BgmId;
                            fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                            //logger.LogInfo(String.Format("@rdfId={0}, story point trigger editor id = {1}, local id = {2} is fulfilled", currRenderFrame.Id, triggerInNextFrame.EditorId ,triggerInNextFrame.TriggerLocalId));
                        }
                    }
                break;
                case TRIGGER_SPECIES_TIMED_WAVE_DOOR_1:
                case TRIGGER_SPECIES_INDI_WAVE_DOOR_1:
                case TRIGGER_SPECIES_SYNC_WAVE_DOOR_1:
                    var firstSubscribesToTriggerEditorId = (0 >= currTrigger.ConfigFromTiled.SubscribesToIdList.Count ? TERMINATING_EVTSUB_ID_INT : currTrigger.ConfigFromTiled.SubscribesToIdList[0]);
                    var subscribesToTriggerInNextFrame = (TERMINATING_EVTSUB_ID_INT == firstSubscribesToTriggerEditorId ? null : nextRenderFrameTriggers[triggerEditorIdToLocalId[firstSubscribesToTriggerEditorId] - 1]);
                    var npcKilledReceptionTriggerInNextFrame = ((TRIGGER_SPECIES_INDI_WAVE_DOOR_1 == triggerConfig.SpeciesId || TRIGGER_SPECIES_TIMED_WAVE_DOOR_1 == triggerConfig.SpeciesId) ? triggerInNextFrame : subscribesToTriggerInNextFrame);

                    if (subCycleFulfilled) {
                        // [WARNING] The information of "justFulfilled" will be lost after then just-fulfilled renderFrame, thus temporarily using "FramesToFire" to keep track of subsequent spawning
                        int chSpawnerConfigIdx = currTrigger.ConfigFromTiled.QuotaCap - currTrigger.Quota;
                        var chSpawnerConfig = lowerBoundForSpawnerConfig(chSpawnerConfigIdx, currTrigger.ConfigFromTiled.CharacterSpawnerTimeSeq);
                        fireTriggerSpawning(currRenderFrame, currTrigger, triggerInNextFrame, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt, npcKilledReceptionTriggerInNextFrame, decodedInputHolder, chSpawnerConfig, logger);
                    } else if (mainCycleFulfilled) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.Quota = currTrigger.Quota - 1;
                            triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                            triggerInNextFrame.FramesToFire = currTrigger.ConfigFromTiled.DelayedFrames;
                            int chSpawnerConfigIdx = currTrigger.ConfigFromTiled.QuotaCap - triggerInNextFrame.Quota;
                            var chSpawnerConfig = lowerBoundForSpawnerConfig(chSpawnerConfigIdx, currTrigger.ConfigFromTiled.CharacterSpawnerTimeSeq);
                            int nextWaveNpcCnt = (chSpawnerConfig.SpeciesIdList.Count < currTrigger.ConfigFromTiled.SubCycleQuota ? chSpawnerConfig.SpeciesIdList.Count : currTrigger.ConfigFromTiled.SubCycleQuota);
                            triggerInNextFrame.SubCycleQuotaLeft = currTrigger.ConfigFromTiled.SubCycleQuota;
                            if (TRIGGER_SPECIES_TIMED_WAVE_DOOR_1 == currTrigger.ConfigFromTiled.SpeciesId) {
                                // [WARNING] For exhaustion of a TimedWaveDoor, we required all spawned NPC-killed
                                if (currTrigger.Quota == currTrigger.ConfigFromTiled.QuotaCap) {
                                    triggerInNextFrame.WaveNpcKilledEvtMaskCounter = 1UL;
                                    triggerInNextFrame.DemandedEvtMask = (1UL << nextWaveNpcCnt) - 1; 
                                    //logger.LogInfo(String.Format("@rdfId={0}, {10} editor id = {1} INITIAL mainCycleFulfilled, local id = {2}, of next frame:: demandedEvtMask = {3}, fulfilledEvtMask = {4}, waveNpcKilledEvtMaskCounter = {5}, quota = {6}, subCycleQuota = {7}, framesToRecover = {8}, framesToFire = {9}", currRenderFrame.Id, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId, triggerInNextFrame.DemandedEvtMask, triggerInNextFrame.FulfilledEvtMask, triggerInNextFrame.WaveNpcKilledEvtMaskCounter, triggerInNextFrame.Quota, triggerInNextFrame.SubCycleQuotaLeft, triggerInNextFrame.FramesToRecover, triggerInNextFrame.FramesToFire, currTrigger.Config.SpeciesName));
                                } else {
                                    triggerInNextFrame.DemandedEvtMask <<= nextWaveNpcCnt; 
                                    triggerInNextFrame.DemandedEvtMask |= (1UL << nextWaveNpcCnt) - 1;
                                    //logger.LogInfo(String.Format("@rdfId={0}, {10} editor id = {1} SUBSEQ mainCycleFulfilled, local id = {2}, of next frame: demandedEvtMask = {3}, fulfilledEvtMask = {4}, waveNpcKilledEvtMaskCounter = {5}, quota = {6}, subCycleQuota = {7}, framesToRecover = {8}, framesToFire = {9}", currRenderFrame.Id, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId, triggerInNextFrame.DemandedEvtMask, triggerInNextFrame.FulfilledEvtMask, triggerInNextFrame.WaveNpcKilledEvtMaskCounter, triggerInNextFrame.Quota, triggerInNextFrame.SubCycleQuotaLeft, triggerInNextFrame.FramesToRecover, triggerInNextFrame.FramesToFire, currTrigger.Config.SpeciesName));
                                }     
                            } else {
                                // [WARNING] For SyncWaveDoor, its main cycles are not triggered by NPC-killed, thus assigning this value uniformly is not an issue!
                                triggerInNextFrame.WaveNpcKilledEvtMaskCounter = 1UL;
                                triggerInNextFrame.DemandedEvtMask = (1UL << nextWaveNpcCnt) - 1;
                                triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                                //logger.LogInfo(String.Format("@rdfId={0}, {1} editor id = {2} mainCycleFulfilled, local id = {3} of next frame: demandedEvtMask = {4}, fulfilledEvtMask = {5}, waveNpcKilledEvtMaskCounter = {6}, quota = {7}, subCycleQuota = {8}, framesToRecover = {9}, framesToFire = {10}", currRenderFrame.Id, currTrigger.Config.SpeciesName, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId, triggerInNextFrame.DemandedEvtMask, triggerInNextFrame.FulfilledEvtMask, triggerInNextFrame.WaveNpcKilledEvtMaskCounter, triggerInNextFrame.Quota, triggerInNextFrame.SubCycleQuotaLeft, triggerInNextFrame.FramesToRecover, triggerInNextFrame.FramesToFire));
                            }

                            fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                        } else if (0 == currTrigger.Quota) {
                            // Set to exhausted
                            // [WARNING] Exclude MAGIC_QUOTA_INFINITE and MAGIC_QUOTA_EXHAUSTED here!
                            //logger.LogInfo(String.Format("@rdfId={0}, {6} editor id = {1} exhausted, local id = {2}, of next frame:: demandedEvtMask = {3}, fulfilledEvtMask = {4}, waveNpcKilledEvtMaskCounter = {5}", currRenderFrame.Id, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId, triggerInNextFrame.DemandedEvtMask, triggerInNextFrame.FulfilledEvtMask, triggerInNextFrame.WaveNpcKilledEvtMaskCounter, currTrigger.Config.SpeciesName));
                            triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                            triggerInNextFrame.DemandedEvtMask = EVTSUB_NO_DEMAND_MASK;
                            
                            if (null != subscribesToTriggerInNextFrame) {
                                // [WARNING] Whenever a single NPC wave door is subscribing to any group trigger, ignore its exhaust subscribers, i.e. exhaust subscribers should respect the group trigger instead!
                                subscribesToTriggerInNextFrame.FulfilledEvtMask |= (1UL << (currTrigger.TriggerLocalId-1));
                            } else {
                                _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, false, true, logger);
                            }
                        }
                    } else if (0 == currTrigger.FramesToRecover) {
                        // replenish upon mainCycle ends, but "false == mainCycleFulfilled"
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.State = TriggerState.Tready;
                            triggerInNextFrame.FramesInState = 0;
                        }
                    }
                break;
                case TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_TRIVIAL:
                case TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_MV:
                    if (mainCycleFulfilled) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.Quota = currTrigger.Quota - 1;
                            triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                            _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, false, false, logger); 
                            // Special handling, reverse subscription and repurpose evt mask fields. 
                            triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                            triggerInNextFrame.DemandedEvtMask = triggerInNextFrame.SubscriberLocalIdsMask;
                            triggerInNextFrame.SubscriberLocalIdsMask = EVTSUB_NO_DEMAND_MASK; // There's no long any subscriber to this group trigger 
                            fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                            //logger.LogInfo(String.Format("@rdfId={0}, {3} editor id = {1}, local id = {2} is fulfilled for the first time and re-purposed", currRenderFrame.Id, triggerInNextFrame.EditorId ,triggerInNextFrame.TriggerLocalId, currTrigger.Config.SpeciesName));
                        } else {
                            // Set to exhausted
                            //logger.LogInfo(String.Format("@rdfId={0}, {3} editor id = {1}, local id = {2} is exhausted", currRenderFrame.Id, triggerInNextFrame.EditorId ,triggerInNextFrame.TriggerLocalId, currTrigger.Config.SpeciesName));
                            triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                            triggerInNextFrame.DemandedEvtMask = EVTSUB_NO_DEMAND_MASK;
                            _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, false, true, logger);
                        }
                    }
                break;
                case TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_TRIVIAL:
                case TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_MV:
                        if (mainCycleFulfilled) {
                            if (0 < currTrigger.Quota) {
                                triggerInNextFrame.Quota = currTrigger.Quota - 1;
                                triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                                triggerInNextFrame.WaveNpcKilledEvtMaskCounter = 1UL;
                                int nextWaveNpcCnt = _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, true, false, logger); 
                                triggerInNextFrame.DemandedEvtMask = (1UL << nextWaveNpcCnt) - 1;
                                // Special handling, reverse subscription and repurpose evt mask fields. 
                                triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                                fulfilledTriggerSetMask |= (1UL << (triggerInNextFrame.TriggerLocalId - 1));
                                //logger.LogInfo(String.Format("@rdfId={0}, {1} editor id = {2}, local id = {3} is initiated and re-purposed. DemandedEvtMask={4} from nextWaveNpcCnt={5}", currRenderFrame.Id, currTrigger.Config.SpeciesName, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId, triggerInNextFrame.DemandedEvtMask, nextWaveNpcCnt));
                            } else {
                                // Set to exhausted
                                //logger.LogInfo(String.Format("@rdfId={0}, {1} editor id = {2}, local id = {3} is exhausted", currRenderFrame.Id, currTrigger.Config.SpeciesName, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId));
                                triggerInNextFrame.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                                triggerInNextFrame.DemandedEvtMask = EVTSUB_NO_DEMAND_MASK;
                                _notifySubscriberTriggers(currRenderFrame.Id, triggerInNextFrame, nextRenderFrameTriggers, false, true, logger);
                            }
                        }
                        break;
                }
            }
        }
    
        private static void _calcTrapReaction(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<Trap> nextRenderFrameTraps, Dictionary<int, int> triggerEditorIdToLocalId, ulong fulfilledEvtSubscriptionSetMask, ILoggerBridge logger) {
            for (int i = 0; i < nextRenderFrameTraps.Count; i++) {
                var trapInNextFrame = nextRenderFrameTraps[i]; 
                if (TERMINATING_TRAP_ID == trapInNextFrame.TrapLocalId) break;
                if (TERMINATING_TRIGGER_ID == trapInNextFrame.SubscribesToTriggerLocalId) continue;
                ulong trapSubscribedTriggerMask = (1UL << (trapInNextFrame.SubscribesToTriggerLocalId - 1));
                if (0 == (fulfilledEvtSubscriptionSetMask & trapSubscribedTriggerMask)) {
                    if (TERMINATING_TRIGGER_ID == trapInNextFrame.SubscribesToTriggerLocalIdAlt || 0 == (fulfilledEvtSubscriptionSetMask & trapSubscribedTriggerMask)) {
                        continue;
                    }
                }

                var trapConfig = trapConfigs[trapInNextFrame.ConfigFromTiled.SpeciesId];
                if (trapConfig.DeactivateUponTriggered) {
                    if (TrapState.Tidle == trapInNextFrame.TrapState) {
                        trapInNextFrame.TrapState = TrapState.Tdeactivated;
                        trapInNextFrame.FramesInTrapState = 0;
                        if (TERMINATING_EVTSUB_ID_INT != trapInNextFrame.ConfigFromTiled.SubscribesToIdAfterInitialFire) {
                            trapInNextFrame.SubscribesToTriggerLocalId = triggerEditorIdToLocalId[trapInNextFrame.ConfigFromTiled.SubscribesToIdAfterInitialFire];
                        } else {
                            trapInNextFrame.SubscribesToTriggerLocalId = TERMINATING_TRIGGER_ID;
                        }
                    } else if (TrapState.Tdeactivated == trapInNextFrame.TrapState) {
                        trapInNextFrame.TrapState = TrapState.Tidle;
                        trapInNextFrame.FramesInTrapState = 0;
                        trapInNextFrame.SubscribesToTriggerLocalId = TERMINATING_TRIGGER_ID;
                    } else {
                        trapInNextFrame.TrapState = TrapState.Tdeactivated;
                        trapInNextFrame.FramesInTrapState = 0;
                        trapInNextFrame.SubscribesToTriggerLocalId = TERMINATING_TRIGGER_ID;
                    }
                } else {
                    if (trapInNextFrame.CapturedByPatrolCue) {
                        trapInNextFrame.CapturedByPatrolCue = false; // [WARNING] Important to help this trap escape its currently capturing PatrolCue!
                        trapInNextFrame.FramesInPatrolCue = MAGIC_FRAMES_FOR_TRAP_TO_WAIVE_PATROL_CUE;
                        var dirMagSq =  trapInNextFrame.DirX*trapInNextFrame.DirX + trapInNextFrame.DirY*trapInNextFrame.DirY;
                        var invDirMag = InvSqrt32(dirMagSq);
                        var speedXfac = invDirMag * trapInNextFrame.DirX;
                        var speedYfac = invDirMag * trapInNextFrame.DirY;
                        var speedVal = trapInNextFrame.ConfigFromTiled.Speed;
                        trapInNextFrame.VelX = (int)(speedXfac * speedVal);
                        trapInNextFrame.VelY = (int)(speedYfac * speedVal);
                    } else {
                        var configFromTiled = trapInNextFrame.ConfigFromTiled;
                        trapInNextFrame.DirX = configFromTiled.DirX;
                        trapInNextFrame.DirY = configFromTiled.DirY;
                        var dirMagSq = configFromTiled.DirX * configFromTiled.DirX + configFromTiled.DirY * configFromTiled.DirY;
                        var invDirMag = InvSqrt32(dirMagSq);
                        var speedXfac = invDirMag * configFromTiled.DirX;
                        var speedYfac = invDirMag * configFromTiled.DirY;
                        var speedVal = trapInNextFrame.ConfigFromTiled.Speed;
                        trapInNextFrame.VelX = (int)(speedXfac * speedVal);
                        trapInNextFrame.VelY = (int)(speedYfac * speedVal);
                    }
                    var currTrap = currRenderFrame.TrapsArr[i];
                    if (0 != trapConfig.AngularFrameVelCos || 0 != trapConfig.AngularFrameVelSin) {
                        if (0 < (currTrap.PatrolCueAngularVelFlipMark & 1)) {
                            trapInNextFrame.AngularFrameVelCos = trapConfig.AngularFrameVelCos;
                            trapInNextFrame.AngularFrameVelSin = -trapConfig.AngularFrameVelSin;
                        } else {
                            trapInNextFrame.AngularFrameVelCos = trapConfig.AngularFrameVelCos;
                            trapInNextFrame.AngularFrameVelSin = trapConfig.AngularFrameVelSin;
                        }
                    }
                }
            }            
        }

        private static CharacterSpawnerConfig lowerBoundForSpawnerConfig(int rdfId, RepeatedField<CharacterSpawnerConfig> characterSpawnerTimeSeq) {
            int l = 0, r = characterSpawnerTimeSeq.Count;
            while (l < r) {
                int m = ((l + r) >> 1);
                var cand = characterSpawnerTimeSeq[m]; 
                if (cand.CutoffRdfFrameId == rdfId) {
                    return cand; 
                } else if (cand.CutoffRdfFrameId < rdfId) {
                    l = m+1;
                } else {
                    r = m;
                }
            }
            if (l >= characterSpawnerTimeSeq.Count) l = characterSpawnerTimeSeq.Count-1; 
            return characterSpawnerTimeSeq[l]; 
        }

        private static void fireTriggerSpawning(RoomDownsyncFrame currRenderFrame, Trigger currTrigger, Trigger triggerInNextFrame, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, Trigger? npcKilledReceptionTriggerInNextFrame, InputFrameDecoded decodedInputHolder, CharacterSpawnerConfig chSpawnerConfig, ILoggerBridge logger) {
            if (0 < currTrigger.SubCycleQuotaLeft) {
                triggerInNextFrame.SubCycleQuotaLeft = currTrigger.SubCycleQuotaLeft - 1;
                var spawnerSpeciesIdList = chSpawnerConfig.SpeciesIdList;
                var initOpList = chSpawnerConfig.InitOpList;
                if (0 < spawnerSpeciesIdList.Count) {
                    int idx = currTrigger.ConfigFromTiled.SubCycleQuota - triggerInNextFrame.SubCycleQuotaLeft -1;
                    if (idx < 0 || idx >= spawnerSpeciesIdList.Count) return;
                    if (idx < 0) idx = 0;
                    if (idx >= spawnerSpeciesIdList.Count) idx = spawnerSpeciesIdList.Count-1;
                    // [WARNING] Trigger-spawned NPCs wouldn't subscribe to any evtsub for initial movement.

                    DecodeInput(initOpList[idx], decodedInputHolder);
                    int npcKilledReceptionTriggerLocalIdInNextFrame = (null == npcKilledReceptionTriggerInNextFrame ? TERMINATING_TRIGGER_ID : npcKilledReceptionTriggerInNextFrame.TriggerLocalId);  
                    ulong npcKilledEvtMask = null == npcKilledReceptionTriggerInNextFrame ? EVTSUB_NO_DEMAND_MASK : npcKilledReceptionTriggerInNextFrame.WaveNpcKilledEvtMaskCounter; 
                    if (addNewNpcToNextFrame(currRenderFrame.Id, currTrigger.VirtualGridX, currTrigger.VirtualGridY, decodedInputHolder.Dx, decodedInputHolder.Dy, spawnerSpeciesIdList[idx], currTrigger.BulletTeamId, false, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt, npcKilledReceptionTriggerLocalIdInNextFrame, npcKilledEvtMask, TERMINATING_TRIGGER_ID)) {
                        triggerInNextFrame.State = TriggerState.TcoolingDown;
                        triggerInNextFrame.FramesInState = 0;
                        triggerInNextFrame.FramesToFire = triggerInNextFrame.ConfigFromTiled.SubCycleTriggerFrames;
                        if (null != npcKilledReceptionTriggerInNextFrame) {
                            npcKilledReceptionTriggerInNextFrame.WaveNpcKilledEvtMaskCounter <<= 1;
                        }
                        /*
                        if (TRIGGER_SPECIES_TIMED_WAVE_DOOR_1 == currTrigger.Config.SpeciesId) {
                            logger.LogInfo(String.Format("@rdfId={0}, timed-wave trigger editor id = {1} subCycleFulfilled, local id = {2}, of next frame:: demandedEvtMask = {3}, fulfilledEvtMask = {4}, waveNpcKilledEvtMaskCounter = {5}, quota = {6}, subCycleQuota = {7}, framesToRecover = {8}, framesToFire = {9}", currRenderFrame.Id, triggerInNextFrame.EditorId, triggerInNextFrame.TriggerLocalId, triggerInNextFrame.DemandedEvtMask, triggerInNextFrame.FulfilledEvtMask, triggerInNextFrame.WaveNpcKilledEvtMaskCounter, triggerInNextFrame.Quota, triggerInNextFrame.SubCycleQuotaLeft, triggerInNextFrame.FramesToRecover, triggerInNextFrame.FramesToFire));
                        }
                        */
                    }
                }
            } else {
                // Wait for "FramesToRecover" to become 0
                triggerInNextFrame.State = TriggerState.Tready;
                triggerInNextFrame.FramesToFire = MAX_INT;
            }
        }

        private static int _notifySubscriberTriggers(int currRdfId, Trigger firingTrigger, RepeatedField<Trigger> nextRenderFrameTriggers, bool providesSyncWaveNpcCnt, bool forExhaustion, ILoggerBridge logger) {
            int nextWaveNpcCnt = 0;
            int subscriberTriggerLocalId = 1;
            ulong targetMask = forExhaustion ? firingTrigger.ExhaustSubscriberLocalIdsMask : firingTrigger.SubscriberLocalIdsMask;
            do {
                ulong singleSubscriberIdMask = (1UL << (subscriberTriggerLocalId - 1));
                if (singleSubscriberIdMask > targetMask) break;
                if (0 < (singleSubscriberIdMask & targetMask)) {
                    var subscriberTriggerInNextFrame = nextRenderFrameTriggers[subscriberTriggerLocalId-1];
                    if (forExhaustion) {
                        subscriberTriggerInNextFrame.OffenderJoinIndex = firingTrigger.OffenderJoinIndex;
                        subscriberTriggerInNextFrame.OffenderBulletTeamId = firingTrigger.OffenderBulletTeamId;
                        if (EVTSUB_NO_DEMAND_MASK != firingTrigger.ConfigFromTiled.PublishingEvtMaskUponExhausted) {         
                            subscriberTriggerInNextFrame.FulfilledEvtMask |= firingTrigger.ConfigFromTiled.PublishingEvtMaskUponExhausted;
                            //logger.LogInfo(String.Format("@rdfId={0}, {1} editor id = {2}, local id = {3} published exhausted evt mask = {4} to {5}", currRdfId, firingTrigger.Config.SpeciesName, firingTrigger.EditorId, firingTrigger.TriggerLocalId, firingTrigger.ConfigFromTiled.PublishingEvtMaskUponExhausted, subscriberTriggerInNextFrame));
                        } else {
                            subscriberTriggerInNextFrame.FulfilledEvtMask = subscriberTriggerInNextFrame.DemandedEvtMask;
                        }
                    } else {
                        // Regular
                        if (providesSyncWaveNpcCnt) {
                            if (0 < subscriberTriggerInNextFrame.Quota) {
                                subscriberTriggerInNextFrame.FulfilledEvtMask = subscriberTriggerInNextFrame.DemandedEvtMask;
                                int chSpawnerConfigIdx = subscriberTriggerInNextFrame.ConfigFromTiled.QuotaCap - subscriberTriggerInNextFrame.Quota + 1;
                                var chSpawnerConfig = lowerBoundForSpawnerConfig(chSpawnerConfigIdx, subscriberTriggerInNextFrame.ConfigFromTiled.CharacterSpawnerTimeSeq);
                                nextWaveNpcCnt += (chSpawnerConfig.SpeciesIdList.Count < subscriberTriggerInNextFrame.ConfigFromTiled.SubCycleQuota ? chSpawnerConfig.SpeciesIdList.Count : subscriberTriggerInNextFrame.ConfigFromTiled.SubCycleQuota);
                            }
                        } else {
                            // Not checking quota, "subscriberTriggerInNextFrame" can be triggered for exhaustion 
                            subscriberTriggerInNextFrame.FulfilledEvtMask = subscriberTriggerInNextFrame.DemandedEvtMask;
                        }
                    }
                }
                ++subscriberTriggerLocalId;
            } while (true);

            return nextWaveNpcCnt;
        }
    }    
}
