using Google.Protobuf.Collections;
using System;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        private static (int, bool, int, int) deriveNpcOpPattern(CharacterDownsync currCharacterDownsync, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, Collision collision, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);

            // returns (patternId, jumpedOrNot, effectiveDx, effectiveDy)

            if (0 < currCharacterDownsync.FramesToRecover) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            int patternId = PATTERN_ID_NO_OP;
            bool jumpedOrNot = false;

            // By default keeps the movement aligned with current facing
            int effectiveDx = currCharacterDownsync.DirX;
            int effectiveDy = currCharacterDownsync.DirY;

            bool hasVisionReaction = false;
            var aCollider = dynamicRectangleColliders[currCharacterDownsync.JoinIndex - 1]; // already added to collisionSys
            if (!currCharacterDownsync.InAir) {
                // TODO: There's no InAir vision reaction yet.
                float visionCx, visionCy, visionCw, visionCh;
                calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);
                float closeEnoughToAtkRange = chConfig.CloseEnoughVirtualGridDistance * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO;

                var visionCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(visionCollider, visionCx, visionCy, visionCw, visionCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync);
                collisionSys.AddSingle(visionCollider);
                bool collided = visionCollider.CheckAllWithHolder(0, 0, collision);
                if (collided) {
                    ConvexPolygon aShape = visionCollider.Shape;
                    while (!hasVisionReaction) {
                        var (ok3, bCollider) = collision.PopFirstContactedCollider();
                        if (false == ok3 || null == bCollider) {
                            break;
                        }

                        ConvexPolygon bShape = bCollider.Shape;
                        var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                        if (!overlapped) {
                            continue;
                        }
                        switch (bCollider.Data) {
                            case CharacterDownsync v3:
                                if (!COLLIDABLE_PAIRS.Contains(v3.CollisionTypeMask | currCharacterDownsync.CollisionTypeMask)) {
                                    break;
                                }
                                if (v3.JoinIndex == currCharacterDownsync.JoinIndex || v3.BulletTeamId == currCharacterDownsync.BulletTeamId) {
                                    break;
                                }
                                var colliderDx = (bCollider.X - aCollider.X);
                                var colliderDy = (bCollider.Y - aCollider.Y);

                                bool prevCapturedByInertia = currCharacterDownsync.CapturedByInertia;
                                if (!prevCapturedByInertia) {
                                    // To emulate input delay, and double it to give the players some advantages
                                    thatCharacterInNextFrame.CapturedByInertia = true;
                                    thatCharacterInNextFrame.FramesToRecover = (INPUT_DELAY_FRAMES << 3);
                                    if (currCharacterDownsync.WaivingSpontaneousPatrol && 0 > colliderDx * currCharacterDownsync.DirX) {
                                        // A static NPC should turn immediately to the opponent behind it, otherwise it looks weird
                                        effectiveDx = -effectiveDx;
                                    }
                                    hasVisionReaction = true;
                                } else {
                                    thatCharacterInNextFrame.CapturedByInertia = false;
                                    if (0 > (colliderDx * currCharacterDownsync.DirX)) {
                                        // Behind me
                                        effectiveDx = -effectiveDx;
                                        hasVisionReaction = true;
                                        break;
                                    }
                                    var atkedChConfig = characters[v3.SpeciesId];
                                    if (!invinsibleSet.Contains(v3.CharacterState) && 0 >= v3.FramesInvinsible && 0 < colliderDx * currCharacterDownsync.DirX) {
                                        // Opponent is in front of me
                                        if (Math.Abs(colliderDx)-atkedChConfig.DefaultSizeX*0.5f*VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO > closeEnoughToAtkRange) {
                                            // Not close enough to attack
                                            hasVisionReaction = true;
                                        } else {
                                            // close enough to attack
                                            switch (currCharacterDownsync.SpeciesId) {
                                                case 1:
                                                    if (0.2f * aCollider.H < colliderDy) {
                                                        // In air
                                                        patternId = 2;
                                                    } else {
                                                        // On ground
                                                        patternId = 1;
                                                    }
                                                    hasVisionReaction = true;
                                                    break;
                                                case 2:
                                                    if (Math.Abs(colliderDx) < 1.5f * aCollider.W) {
                                                        // Melee reachable
                                                        if (0.2f * aCollider.H < colliderDy) {
                                                            // In air
                                                            patternId = 2;
                                                        } else {
                                                            // On ground
                                                            patternId = 1;
                                                        }
                                                    } else {
                                                        // Use fireball
                                                        patternId = 3;
                                                    }
                                                    hasVisionReaction = true;
                                                    break;
                                                case 3:
                                                    if (Math.Abs(colliderDx) < 1.5f * aCollider.W) {
                                                        // Melee reachable
                                                        if (0.2f * aCollider.H < colliderDy) {
                                                            // In air
                                                            patternId = 2;
                                                        } else {
                                                            // On ground
                                                            patternId = 1;
                                                        }
                                                    } else {
                                                        // Use fireball
                                                        patternId = 3;
                                                    }
                                                    hasVisionReaction = true;
                                                    break;
                                                case 4096:
                                                    if (Math.Abs(colliderDx) < 1.2f * aCollider.W) {
                                                        // Use melee
                                                        patternId = 1;
                                                    } else {
                                                        // Use fireball
                                                        patternId = 3;
                                                    }
                                                    hasVisionReaction = true;
                                                    break;
                                            }
                                        }
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
                visionCollider.Data = null;
                collisionSys.RemoveSingle(visionCollider); // no need to increment "colliderCnt", the visionCollider is transient
            }

            if (hasVisionReaction && PATTERN_ID_NO_OP != patternId) {
                // [WARNING] Even if "hasVisionReaction", if "PATTERN_ID_NO_OP == patternId", we still expect the NPC to make use of patrol cues to jump or turn around!
                thatCharacterInNextFrame.CapturedByPatrolCue = false;
                thatCharacterInNextFrame.FramesInPatrolCue = 0;
                thatCharacterInNextFrame.WaivingPatrolCueId = NO_PATROL_CUE_ID;
            } else if (!noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                bool hasPatrolCueReaction = false;
                // [WARNING] The field "CharacterDownsync.FramesInPatrolCue" would also be re-purposed as "patrol cue collision waiving frames" by the logic here.
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
                if (collided) {
                    ConvexPolygon aShape = aCollider.Shape;
                    while (true) {
                        var (ok3, bCollider) = collision.PopFirstContactedCollider();
                        if (false == ok3 || null == bCollider) {
                            break;
                        }

                        ConvexPolygon bShape = bCollider.Shape;
                        var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                        if (!overlapped) {
                            continue;
                        }
                        switch (bCollider.Data) {
                            case PatrolCue v3:
                                if (!COLLIDABLE_PAIRS.Contains(v3.CollisionTypeMask | currCharacterDownsync.CollisionTypeMask)) {
                                    break;
                                }
                                bool prevCapturedByPatrolCue = currCharacterDownsync.CapturedByPatrolCue;

                                // By now we're sure that it should react to the PatrolCue
                                var colliderDx = (aCollider.X - bCollider.X);
                                var colliderDy = (aCollider.Y - bCollider.Y);
                                bool fr = 0 < colliderDx && (0 > currCharacterDownsync.VelX || (0 == currCharacterDownsync.VelX && 0 > currCharacterDownsync.DirX));
                                bool fl = 0 > colliderDx && (0 < currCharacterDownsync.VelX || (0 == currCharacterDownsync.VelX && 0 < currCharacterDownsync.DirX));
                                bool fu = 0 < colliderDy && (0 > currCharacterDownsync.VelY || (0 == currCharacterDownsync.VelY && 0 > currCharacterDownsync.DirY));
                                bool fd = 0 > colliderDy && (0 < currCharacterDownsync.VelY || (0 == currCharacterDownsync.VelY && 0 < currCharacterDownsync.DirY));
                                if (!fr && !fl && !fu && !fd) {
                                    fr = 0 > currCharacterDownsync.DirX;
                                    fl = 0 < currCharacterDownsync.DirX;
                                }                                

                                int targetFramesInPatrolCue = 0;
                                if (fr) {
                                    targetFramesInPatrolCue = (int)v3.FrCaptureFrames;
                                    DecodeInput(v3.FrAct, decodedInputHolder);
                                    //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the right", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, v3)); 
                                } else if (fl) {
                                    targetFramesInPatrolCue = (int)v3.FlCaptureFrames;
                                    DecodeInput(v3.FlAct, decodedInputHolder);
                                    //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the left", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, v3));
                                } else if (fu) {
                                    targetFramesInPatrolCue = (int)v3.FuCaptureFrames;
                                    DecodeInput(v3.FuAct, decodedInputHolder);
                                    //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the top", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, v3)); 
                                } else if (fd) {
                                    targetFramesInPatrolCue = (int)v3.FdCaptureFrames;
                                    DecodeInput(v3.FdAct, decodedInputHolder);
                                    //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the bottom", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, v3)); 
                                } else {
                                    logger.LogWarn(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3}, dirX: {9} }} collided with bCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} but direction couldn't be determined!", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, v3, currCharacterDownsync.DirX));
                                }

                                bool shouldWaivePatrolCueReaction = (false == prevCapturedByPatrolCue && 0 < currCharacterDownsync.FramesInPatrolCue && v3.Id == currCharacterDownsync.WaivingPatrolCueId && (0 == decodedInputHolder.BtnALevel && 0 == decodedInputHolder.BtnBLevel)); // Don't waive if the cue contains an action button (i.e. BtnA or BtnB)

                                if (shouldWaivePatrolCueReaction) {
                                    // logger.LogInfo(String.Format("rdf.Id={0}, Npc joinIndex={1}, speciesId={2} is waived for patrolCueId={3} because it has (prevCapturedByPatrolCue={4}, framesInPatrolCue={5}, waivingPatrolCueId={6}).", currRenderFrame.Id, currCharacterDownsync.JoinIndex, currCharacterDownsync.SpeciesId, v3.Id, prevCapturedByPatrolCue, currCharacterDownsync.FramesInPatrolCue, currCharacterDownsync.WaivingPatrolCueId));
                                    break;
                                }

                                bool shouldBreakPatrolCueCapture = ((true == prevCapturedByPatrolCue) && (currCharacterDownsync.WaivingPatrolCueId == v3.Id) && (0 == currCharacterDownsync.FramesInPatrolCue));

                                bool shouldEnterCapturedPeriod = ((false == prevCapturedByPatrolCue) && (false == shouldBreakPatrolCueCapture) && (0 < targetFramesInPatrolCue));

                                if (shouldEnterCapturedPeriod) {
                                    thatCharacterInNextFrame.CapturedByPatrolCue = true;
                                    thatCharacterInNextFrame.FramesInPatrolCue = targetFramesInPatrolCue;
                                    thatCharacterInNextFrame.WaivingPatrolCueId = v3.Id;
                                    effectiveDx = 0;
                                    effectiveDy = 0;
                                    jumpedOrNot = false;
                                    hasPatrolCueReaction = true;
                                    break;
                                }

                                bool isReallyCaptured = ((true == prevCapturedByPatrolCue) && (false == shouldBreakPatrolCueCapture) && (v3.Id == currCharacterDownsync.WaivingPatrolCueId) && (0 < currCharacterDownsync.FramesInPatrolCue));
                                if (isReallyCaptured) {
                                    effectiveDx = 0;
                                    effectiveDy = 0;
                                    jumpedOrNot = false;
                                    hasPatrolCueReaction = true;
                                    break;
                                }

                                effectiveDx = decodedInputHolder.Dx;
                                effectiveDy = decodedInputHolder.Dy;
                                jumpedOrNot = (0 == currCharacterDownsync.FramesToRecover) && !inAirSet.Contains(currCharacterDownsync.CharacterState) && (0 < decodedInputHolder.BtnALevel);
                                hasPatrolCueReaction = true;
                                thatCharacterInNextFrame.CapturedByPatrolCue = false;
                                thatCharacterInNextFrame.FramesInPatrolCue = DEFAULT_PATROL_CUE_WAIVING_FRAMES; // re-purposed
                                thatCharacterInNextFrame.WaivingPatrolCueId = v3.Id;
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (false == hasVisionReaction && false == hasPatrolCueReaction && currCharacterDownsync.WaivingSpontaneousPatrol) {
                    return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
                }
            }

            return (patternId, jumpedOrNot, effectiveDx, effectiveDy);
        }
        private static void _processNpcInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, ref int colliderCnt, Collision collision, CollisionSpace collisionSys, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = roomCapacity; i < roomCapacity + currRenderFrame.NpcsArr.Count; i++) {
                var currCharacterDownsync = currRenderFrame.NpcsArr[i - roomCapacity];
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = nextRenderFrameNpcs[i - roomCapacity];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, effDx, effDy) = deriveNpcOpPattern(currCharacterDownsync, currRenderFrame, roomCapacity, chConfig, thatCharacterInNextFrame, dynamicRectangleColliders, colliderCnt, collisionSys, collision, ref overlapResult, decodedInputHolder, logger);
                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;

                if (_useSkill(patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets)) {
                    continue; // Don't allow movement if skill is used
                }

                if (0 == currCharacterDownsync.FramesToRecover) {
                    // No inertia capture for Npcs, and most NPCs don't even have TurnAround animation clip!
                    if (0 != effDx) {
                        int xfac = (0 < effDx ? 1 : -1);
                        thatCharacterInNextFrame.DirX = effDx;
                        thatCharacterInNextFrame.DirY = effDy;

                        thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                        thatCharacterInNextFrame.CharacterState = Walking;
                    } else {
                        thatCharacterInNextFrame.CharacterState = Idle1;
                        thatCharacterInNextFrame.VelX = 0;
                    }
                }
            }
        }
    }
}
