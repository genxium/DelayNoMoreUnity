using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        private const int TARGET_CH_REACTION_UNKNOWN = 0;
        private const int TARGET_CH_REACTION_USE_MELEE = 1;
        private const int TARGET_CH_REACTION_USE_DRAGONPUNCH = 2;
        private const int TARGET_CH_REACTION_USE_FIREBALL = 3;
        private const int TARGET_CH_REACTION_FOLLOW = 4;
        private const int TARGET_CH_REACTION_USE_SLOT_C = 5;
        private const int TARGET_CH_REACTION_NOT_ENOUGH_MP = 6;
        private const int TARGET_CH_REACTION_FLEE_OPPO = 7;
        private const int TARGET_CH_REACTION_DEF1 = 8;
        private const int TARGET_CH_REACTION_STOP_BY_MV_BLOCKER = 9;

        private const int TARGET_CH_REACTION_JUMP_TOWARDS_CH = 10;
        private const int TARGET_CH_REACTION_JUMP_TOWARDS_MV_BLOCKER = 11;
        private const int TARGET_CH_REACTION_SLIP_JUMP_TOWARDS_CH = 12;
        private const int TARGET_CH_REACTION_WALK_ALONG = 13;
        private const int TARGET_CH_REACTION_TURNAROUND_MV_BLOCKER = 14;

        private const int TARGET_CH_REACTION_STOP_BY_PATROL_CUE_ENTER = 15;
        private const int TARGET_CH_REACTION_STOP_BY_PATROL_CUE_CAPTURED = 16;

        private static int NPC_DEF1_MIN_HOLDING_RDF_CNT = 90;

        private static int VISION_SEARCH_RDF_RANDOMIZE_MASK = (1 << 4) + (1 << 3) + (1 << 1);

        private static int OPPO_DX_OFFSET_MOD_MINUS_1 = (1 << 3) - 1;
        private static float OPPO_DX_OFFSET = 10.0f; // In collision space
    
        private static float TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD = 32.0f;

        private static void _handleAllyCh(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool notDashing, bool canJumpWithinInertia, bool isCharacterFlying, CharacterDownsync v5, CharacterConfig allyConfig, Collider? allyChCollider, float allyChColliderDx, float allyChColliderDy, float allyBoxLeft, float allyBoxRight, float allyBoxBottom, float allyBoxTop, ref int visionReaction, InputFrameDecoded decodedInputHolder) {
            if (null == allyChCollider || null == v5) return;

            float absColliderDx = Math.Abs(allyChColliderDx), absColliderDy = Math.Abs(allyChColliderDy);

            bool allyBehindMe = (0 > (allyChColliderDx * currCharacterDownsync.DirX));
            if (!allyBehindMe) {
                // Ally is in front of me
                int s0 = TARGET_CH_REACTION_UNKNOWN, s1 = TARGET_CH_REACTION_UNKNOWN, s2 = TARGET_CH_REACTION_UNKNOWN, s3 = TARGET_CH_REACTION_UNKNOWN;

                s0 = frontAllyReachableByIvSlot(currCharacterDownsync, effInAir, chConfig, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, absColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                if (TARGET_CH_REACTION_USE_SLOT_C == s0) {
                    decodedInputHolder.Dx = 0;
                    decodedInputHolder.Dy = 0;
                    decodedInputHolder.BtnALevel = 0;
                    decodedInputHolder.BtnBLevel = 0;
                    decodedInputHolder.BtnCLevel = 1;
                    decodedInputHolder.BtnDLevel = 0;
                    decodedInputHolder.BtnELevel = 0;
                    visionReaction = s0;
                } else {
                    s1 = frontAllyReachableByDragonPunch(currCharacterDownsync, effInAir, chConfig, canJumpWithinInertia, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, absColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                    visionReaction = s1;
                    if (TARGET_CH_REACTION_USE_DRAGONPUNCH == s1) {
                        decodedInputHolder.Dx = 0;
                        decodedInputHolder.Dy = +2;
                        decodedInputHolder.BtnALevel = 0;
                        decodedInputHolder.BtnBLevel = 1;
                        decodedInputHolder.BtnCLevel = 0;
                        decodedInputHolder.BtnDLevel = 0;
                        decodedInputHolder.BtnELevel = 0;
                    } else {
                        s2 = frontAllyReachableByMelee1(currCharacterDownsync, effInAir, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, absColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop);
                        visionReaction = s2;
                        if (TARGET_CH_REACTION_USE_MELEE == s2) {
                            decodedInputHolder.Dx = 0;
                            decodedInputHolder.Dy = 0;
                            decodedInputHolder.BtnALevel = 0;
                            decodedInputHolder.BtnBLevel = 1;
                            decodedInputHolder.BtnCLevel = 0;
                            decodedInputHolder.BtnDLevel = 0;
                            decodedInputHolder.BtnELevel = 0;
                        } else {
                            s3 = frontAllyReachableByFireball(currCharacterDownsync, chConfig, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop);
                            visionReaction = s3;
                            if (TARGET_CH_REACTION_USE_FIREBALL == s3) {
                                decodedInputHolder.Dx = 0;
                                decodedInputHolder.Dy = -2;
                                decodedInputHolder.BtnALevel = 0;
                                decodedInputHolder.BtnBLevel = 1;
                                decodedInputHolder.BtnCLevel = 0;
                                decodedInputHolder.BtnDLevel = 0;
                                decodedInputHolder.BtnELevel = 0;
                            } else {
                                if (chConfig.NpcPrioritizeAllyHealing && v5.Hp < allyConfig.Hp) {
                                    visionReaction = TARGET_CH_REACTION_FOLLOW; 
                                } else {
                                    visionReaction = TARGET_CH_REACTION_UNKNOWN; // No need to follow ally if buff/healing not required
                                }
                            }
                        }
                    }
                }
                // No need to flee from ally
            } else {
                // Ally is behind me
                if (0 >= chConfig.Speed) {
                    visionReaction = TARGET_CH_REACTION_UNKNOWN;
                } else {
                    if (chConfig.NpcPrioritizeAllyHealing && v5.Hp < allyConfig.Hp) {
                        visionReaction = TARGET_CH_REACTION_FOLLOW;
                    } else {
                        visionReaction = TARGET_CH_REACTION_UNKNOWN; // No need to follow ally if buff/healing not required
                    }
                }
            }
            
            if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                bool allyAboveMe = (0.6f * aCollider.H < allyChColliderDy);
                bool shouldJumpTowardsTarget = (canJumpWithinInertia && !effInAir && allyAboveMe && (0 <= currCharacterDownsync.DirX * allyChColliderDx));
                bool temptingToFly = (allyAboveMe && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState) && effInAir && chConfig.JumpHoldingToFly);
                shouldJumpTowardsTarget |= temptingToFly;
                bool shouldSlipJumpTowardsTarget = (canJumpWithinInertia && !effInAir && 0 > allyChColliderDy && currCharacterDownsync.PrimarilyOnSlippableHardPushback);
                shouldSlipJumpTowardsTarget = (!chConfig.OmitGravity && chConfig.JumpHoldingToFly && currCharacterDownsync.OmitGravity && !allyAboveMe && !allyBehindMe);
                if (0 >= chConfig.JumpingInitVelY) {
                    shouldJumpTowardsTarget = false;
                    shouldSlipJumpTowardsTarget = false;
                } else if (chConfig.HasDef1) {
                    shouldJumpTowardsTarget = false;
                }
                if (shouldSlipJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_SLIP_JUMP_TOWARDS_CH;
                } else if (shouldJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                }
            }

            if (TARGET_CH_REACTION_JUMP_TOWARDS_CH == visionReaction) {
                if (0 != allyChColliderDx) {
                    decodedInputHolder.Dx = (0 < allyChColliderDx ? +2 : -2);
                }
            } else if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                if (0 != allyChColliderDx) {
                    decodedInputHolder.Dx = (0 < allyChColliderDx ? +2 : -2);
                }
                if (isCharacterFlying) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = allyChColliderDx * allyChColliderDx + allyChColliderDy * allyChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = allyChColliderDx * invMag, normY = allyChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY);
                        decodedInputHolder.Dx = effDx;
                        decodedInputHolder.Dy = effDy;
                    }
                } else {
                    if (allyBehindMe) {
                        if (0 >= currCharacterDownsync.FramesToRecover) {
                            decodedInputHolder.Dx = -decodedInputHolder.Dx;
                        }
                    } else {
                        float visionThresholdPortion = 0.95f;
                        bool veryFarAway = (0 < allyChColliderDx ? (allyChCollider.X > (visionCollider.X + visionThresholdPortion*visionCollider.W)) : (allyChCollider.X < visionCollider.X + (1-visionThresholdPortion)*visionCollider.W));
                        if (notDashing && veryFarAway) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.BtnELevel = 1;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.BtnELevel = 1;
                            }
                        }
                    }
                }
            }
        }

        private static void _handleOppoCh(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool notDashing, bool canJumpWithinInertia, bool isCharacterFlying, Collider? oppoChCollider, CharacterDownsync? v3, float oppoChColliderDx, float oppoChColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop, ref int visionReaction, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            if (null == oppoChCollider || null == v3) {
                switch (currCharacterDownsync.GoalAsNpc) {
                    case NpcGoal.NhuntThenIdle:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Nidle;
                        break;
                    case NpcGoal.NhuntThenPatrol:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Npatrol;
                        break;
                    case NpcGoal.NhuntThenFollowAlly:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NfollowAlly;
                        break;
                    default:
                        break;
                }
                return;
            }

            //logger.LogInfo($"handleOppoCh/begin, rdfId={rdfId}, handling oppoCh=(jidx:{v3.JoinIndex}), currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");

            float absColliderDx = Math.Abs(oppoChColliderDx), absColliderDy = Math.Abs(oppoChColliderDy);

            bool opponentBehindMe = (0 > (oppoChColliderDx * currCharacterDownsync.DirX));
            if (!opponentBehindMe) {
                // Opponent is in front of me
                int s0 = TARGET_CH_REACTION_UNKNOWN, s1 = TARGET_CH_REACTION_UNKNOWN, s2 = TARGET_CH_REACTION_UNKNOWN, s3 = TARGET_CH_REACTION_UNKNOWN;

                s0 = frontOpponentReachableByIvSlot(currCharacterDownsync, effInAir, chConfig, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                if (TARGET_CH_REACTION_USE_SLOT_C == s0) {
                    decodedInputHolder.Dx = 0;
                    decodedInputHolder.Dy = 0;
                    decodedInputHolder.BtnALevel = 0;
                    decodedInputHolder.BtnBLevel = 0;
                    decodedInputHolder.BtnCLevel = 1;
                    decodedInputHolder.BtnDLevel = 0;
                    decodedInputHolder.BtnELevel = 0;
                    visionReaction = s0;
                } else {
                    s1 = frontOpponentReachableByDragonPunch(currCharacterDownsync, effInAir, chConfig, canJumpWithinInertia, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, false); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                    visionReaction = s1;
                    if (TARGET_CH_REACTION_USE_DRAGONPUNCH == s1) {
                        decodedInputHolder.Dx = 0;
                        decodedInputHolder.Dy = +2;
                        decodedInputHolder.BtnALevel = 0;
                        decodedInputHolder.BtnBLevel = 1;
                        decodedInputHolder.BtnCLevel = 0;
                        decodedInputHolder.BtnDLevel = 0;
                        decodedInputHolder.BtnELevel = 0;
                    } else {
                        s2 = frontOpponentReachableByMelee1(currCharacterDownsync, effInAir, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop);
                        visionReaction = s2;
                        if (TARGET_CH_REACTION_USE_MELEE == s2) {
                            decodedInputHolder.Dx = 0;
                            decodedInputHolder.Dy = 0;
                            decodedInputHolder.BtnALevel = 0;
                            decodedInputHolder.BtnBLevel = 1;
                            decodedInputHolder.BtnCLevel = 0;
                            decodedInputHolder.BtnDLevel = 0;
                            decodedInputHolder.BtnELevel = 0;
                        } else {
                            s3 = frontOpponentReachableByFireball(currCharacterDownsync, chConfig, aCollider, oppoChCollider, oppoChColliderDx, oppoChColliderDy, absColliderDx, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop);
                            visionReaction = s3;
                            if (TARGET_CH_REACTION_USE_FIREBALL == s3) {
                                decodedInputHolder.Dx = 0;
                                decodedInputHolder.Dy = -2;
                                decodedInputHolder.BtnALevel = 0;
                                decodedInputHolder.BtnBLevel = 1;
                                decodedInputHolder.BtnCLevel = 0;
                                decodedInputHolder.BtnDLevel = 0;
                                decodedInputHolder.BtnELevel = 0;
                            } else {
                                visionReaction = TARGET_CH_REACTION_FOLLOW;
                            }
                        }
                    }
                }

                if (TARGET_CH_REACTION_NOT_ENOUGH_MP == s0 && TARGET_CH_REACTION_NOT_ENOUGH_MP == s1 && TARGET_CH_REACTION_NOT_ENOUGH_MP == s2 && TARGET_CH_REACTION_NOT_ENOUGH_MP == s3 && !chConfig.IsKeyCh) {
                    visionReaction = TARGET_CH_REACTION_FLEE_OPPO;
                } else if (TARGET_CH_REACTION_UNKNOWN == s0 && TARGET_CH_REACTION_UNKNOWN == s1 && TARGET_CH_REACTION_UNKNOWN == s2 && TARGET_CH_REACTION_UNKNOWN == s3) {
                    visionReaction = TARGET_CH_REACTION_FOLLOW;
                }
            } else {
                // Opponent is behind me
                if (0 >= chConfig.Speed) {
                    // e.g. Tower
                    visionReaction = TARGET_CH_REACTION_UNKNOWN;
                } else {
                    CharacterState opCh = v3.CharacterState;
                    bool opponentIsAttacking = (InAirAtk1 == opCh || InAirAtk2 == opCh || Atk1 == opCh || Atk2 == opCh || Atk3 == opCh || Atk4 == opCh || Atk5 == opCh || Atk6 == opCh || Atk7 == opCh);
                    bool opponentIsFacingMe = (0 > (oppoChColliderDx * v3.DirX)) && (absColliderDy < 0.2f * aCollider.H);
                    bool farEnough = (absColliderDx > 0.6f * (aCollider.W + oppoChCollider.W)); // To avoid bouncing turn-arounds
                    if ((opponentIsAttacking && opponentIsFacingMe) || farEnough) {
                        visionReaction = TARGET_CH_REACTION_FOLLOW;
                    } else {
                        visionReaction = TARGET_CH_REACTION_FLEE_OPPO;
                    }
                }
            }
            
            if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                bool opponentAboveMe = 0 < oppoChColliderDy && (oppoChCollider.H < (1.67f*oppoChColliderDy+aCollider.H)); // i.e. "0.6f * (oppoChCollider.H - aCollider.H) < oppoChColliderDy"
                bool shouldJumpTowardsTarget = (canJumpWithinInertia && !effInAir && opponentAboveMe && (0 <= currCharacterDownsync.DirX * oppoChColliderDx));
                bool temptingToFly = (opponentAboveMe && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState) && effInAir && chConfig.JumpHoldingToFly);
                if (temptingToFly) {
                    logger.LogInfo($"handleOppoCh/end, rdfId={rdfId}, temptingToFly towards oppoCh=(jidx:{v3.JoinIndex}), currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                }
                shouldJumpTowardsTarget |= temptingToFly;
                bool shouldSlipJumpTowardsTarget = (canJumpWithinInertia && !effInAir && 0 > oppoChColliderDy && currCharacterDownsync.PrimarilyOnSlippableHardPushback);
                shouldSlipJumpTowardsTarget = (!chConfig.OmitGravity && chConfig.JumpHoldingToFly && currCharacterDownsync.OmitGravity && !opponentAboveMe && !opponentBehindMe);
                if (0 >= chConfig.JumpingInitVelY) {
                    shouldJumpTowardsTarget = false;
                    shouldSlipJumpTowardsTarget = false;
                } else if (chConfig.HasDef1 || chConfig.NpcNotHuntingInAirOppoCh) {
                    if (shouldJumpTowardsTarget) {
                        shouldJumpTowardsTarget = false; // [WARNING] Leave decision to "deriveReactionForMvBlocker"!
                    }
                }

                if (shouldSlipJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_SLIP_JUMP_TOWARDS_CH;
                    //logger.LogInfo($"handleOppoCh/end, rdfId={rdfId}, should slip jump towards oppoCh=(jidx:{v3.JoinIndex}), currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                } else if (shouldJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                }
            }

            if (TARGET_CH_REACTION_JUMP_TOWARDS_CH == visionReaction) {
                if (0 != oppoChColliderDx) {
                    decodedInputHolder.Dx = (0 < oppoChColliderDx ? +2 : -2);
                    decodedInputHolder.Dy = 0;
                }
            } else if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                if (0 != oppoChColliderDx) {
                    decodedInputHolder.Dx = (0 < oppoChColliderDx ? +2 : -2);
                    decodedInputHolder.Dy = 0;
                    decodedInputHolder.BtnALevel = 0;
                }
                if (isCharacterFlying) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = oppoChColliderDx * oppoChColliderDx + oppoChColliderDy * oppoChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = oppoChColliderDx * invMag, normY = oppoChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY);
                        decodedInputHolder.Dx = effDx;
                        decodedInputHolder.Dy = effDy;
                        decodedInputHolder.BtnALevel = 0;
                    }
                } else {
                    if (opponentBehindMe) {
                        if (0 >= currCharacterDownsync.FramesToRecover) {
                            decodedInputHolder.Dx = -currCharacterDownsync.DirX;
                            decodedInputHolder.Dy = 0;
                            decodedInputHolder.BtnALevel = 0;
                        }
                    } else {
                        float visionThresholdPortion = 0.95f;
                        bool veryFarAway = (0 < oppoChColliderDx ? (oppoChCollider.X > (visionCollider.X + visionThresholdPortion*visionCollider.W)) : (oppoChCollider.X < visionCollider.X + (1-visionThresholdPortion)*visionCollider.W));
                        if (notDashing && veryFarAway) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                                decodedInputHolder.BtnALevel = 0;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                                decodedInputHolder.BtnALevel = 0;
                            }
                        }
                    }
                }
            } else if (TARGET_CH_REACTION_FLEE_OPPO == visionReaction) {
                if (0 != oppoChColliderDx) {
                    decodedInputHolder.Dx = (0 < oppoChColliderDx ? -2 : +2);
                    decodedInputHolder.Dy = 0;
                    decodedInputHolder.BtnALevel = 0;
                }
                if (SPECIES_ANGEL == currCharacterDownsync.SpeciesId) {
                    if (opponentBehindMe) {
                        decodedInputHolder.Dx = -decodedInputHolder.Dx;
                        if (notDashing && currCharacterDownsync.Mp >= AngelDashing.MpDelta) {
                            if (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                                decodedInputHolder.BtnALevel = 0;
                            }
                        }
                    } else {
                        bool notBackDashing = (BackDashing != currCharacterDownsync.CharacterState);
                        if (notBackDashing && currCharacterDownsync.Mp >= AngelBackDashing.MpDelta) {
                            decodedInputHolder.Dx = 0;
                            decodedInputHolder.Dy = 0;
                            decodedInputHolder.BtnELevel = 1;
                            decodedInputHolder.BtnALevel = 0;
                        }
                    }
                } if (isCharacterFlying) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = oppoChColliderDx * oppoChColliderDx + oppoChColliderDy * oppoChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = -oppoChColliderDx * invMag, normY = -oppoChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY);
                        decodedInputHolder.Dx = effDx;
                        decodedInputHolder.Dy = effDy;
                        decodedInputHolder.BtnALevel = 0;
                    }
                } else {
                    bool isBackDashingSpecies = (SPECIES_WITCHGIRL == currCharacterDownsync.SpeciesId || SPECIES_BRIGHTWITCH == currCharacterDownsync.SpeciesId || SPECIES_BOUNTYHUNTER == currCharacterDownsync.SpeciesId);
                    bool notBackDashingSpecies = !(SPECIES_WITCHGIRL == currCharacterDownsync.SpeciesId || SPECIES_BRIGHTWITCH == currCharacterDownsync.SpeciesId || SPECIES_BOUNTYHUNTER == currCharacterDownsync.SpeciesId);
                    if (opponentBehindMe) {
                        // DO NOTHING, just continue walking
                        if (notDashing && notBackDashingSpecies) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                                decodedInputHolder.BtnALevel = 0;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                                decodedInputHolder.BtnALevel = 0;
                            }
                        }
                    } else {
                        bool notBackDashing = (BackDashing != currCharacterDownsync.CharacterState);
                        if (!effInAir && notBackDashing && isBackDashingSpecies) {
                            decodedInputHolder.Dx = 0;
                            decodedInputHolder.Dy = 0;
                            decodedInputHolder.BtnELevel = 1;
                            decodedInputHolder.BtnALevel = 0;
                        }
                    }
                }
            }

            if (TARGET_CH_REACTION_UNKNOWN != visionReaction) {
                //logger.LogInfo($"handleOppoCh/end, rdfId={rdfId}, visionReaction={visionReaction} for oppoCh=(jidx:{v3.JoinIndex}), currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                switch (currCharacterDownsync.GoalAsNpc) {
                    case NpcGoal.Nidle:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NhuntThenIdle;
                        break;
                    case NpcGoal.Npatrol:
                    case NpcGoal.NidleIfGoHuntingThenPatrol:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NhuntThenPatrol;
                        break;
                    case NpcGoal.NfollowAlly:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NhuntThenFollowAlly;
                        break;
                    default:
                        break;
                }
            } else {
                switch (currCharacterDownsync.GoalAsNpc) {
                    case NpcGoal.NhuntThenIdle:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Nidle;
                        break;
                    case NpcGoal.NhuntThenPatrol:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Npatrol;
                        break;
                    case NpcGoal.NhuntThenFollowAlly:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NfollowAlly;
                        break;
                    default:
                        break;
                }
            }
        }

        private static void _handleOppoBl(int rdfId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool notDashing, bool canJumpWithinInertia, Collider? oppoChCollider, CharacterDownsync? v3, float oppoChColliderDx, float oppoChColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop, Collider? oppoBlCollider, Bullet? v4, ref int visionReaction, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            if (null == oppoBlCollider || null == v4) return;
            float oppoBlColliderDx = oppoBlCollider.X - aCollider.X;
            bool blBehindMe = (0 > (oppoBlColliderDx * currCharacterDownsync.DirX));
            decodedInputHolder.Dx = (blBehindMe ? -currCharacterDownsync.DirX : currCharacterDownsync.DirX); // turn-around to counter, regardless of being able to counter or not
            if (chConfig.HasDef1 && !effInAir && !blBehindMe) {
                if (chConfig.WalkingAutoDef1 && Walking == currCharacterDownsync.CharacterState) {
                } else {
                    decodedInputHolder.Dx = 0;
                    decodedInputHolder.Dy = +2;
                    decodedInputHolder.BtnALevel = 0;
                    decodedInputHolder.BtnBLevel = 0;
                    decodedInputHolder.BtnCLevel = 0;
                    decodedInputHolder.BtnDLevel = 0;
                    decodedInputHolder.BtnELevel = 0;
                    visionReaction = TARGET_CH_REACTION_DEF1;
                    /*
                    if (SPECIES_LIGHTGUARD_RED == currCharacterDownsync.SpeciesId) {
                        logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, effectiveDx={2}, effectiveDy={3} to turn Def1", rdfId, currCharacterDownsync.Id, effectiveDx, effectiveDy));
                    }
                    */
                }
            } else {
                var (_, blConfig) = FindBulletConfig(v4.SkillId, v4.ActiveSkillHit);
                bool isMelee = null != blConfig && (BulletType.Melee == blConfig.BType);
                if (!isMelee) {
                    if (!blBehindMe && effInAir && chConfig.NpcNoDefaultAirWalking) {
                        decodedInputHolder.Dx = 0;
                    }
                    decodedInputHolder.Dy = 0;
                    if (SPECIES_BOARWARRIOR != chConfig.SpeciesId && SPECIES_RIDERGUARD_RED != chConfig.SpeciesId && SPECIES_DEMON_FIRE_SLIME != chConfig.SpeciesId && !chConfig.HasDef1 && 0 < chConfig.JumpingInitVelY) {
                        visionReaction = TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                    } else if (!effInAir && canJumpWithinInertia && chConfig.HasDef1) {
                        decodedInputHolder.Dx = 0;
                        decodedInputHolder.Dy = +2;
                        decodedInputHolder.BtnALevel = 0;
                        decodedInputHolder.BtnBLevel = 0;
                        decodedInputHolder.BtnCLevel = 0;
                        decodedInputHolder.BtnDLevel = 0;
                        decodedInputHolder.BtnELevel = 0;
                        visionReaction = TARGET_CH_REACTION_DEF1;
                    } else {
                        visionReaction = TARGET_CH_REACTION_FOLLOW;
                    }

                } else {
                    if (null != v3 && null != oppoChCollider && (SPECIES_SWORDMAN == chConfig.SpeciesId || SPECIES_SWORDMAN_BOSS == chConfig.SpeciesId || SPECIES_FIRESWORDMAN == chConfig.SpeciesId) && !effInAir && canJumpWithinInertia) {
                        float absColliderDx = Math.Abs(oppoChColliderDx), absColliderDy = Math.Abs(oppoChColliderDy);
                        visionReaction = frontOpponentReachableByDragonPunch(currCharacterDownsync, effInAir, chConfig, canJumpWithinInertia, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, true); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                    }
                    if (TARGET_CH_REACTION_USE_DRAGONPUNCH == visionReaction) {
                        decodedInputHolder.Dx = 0;
                        decodedInputHolder.Dy = +2;
                        decodedInputHolder.BtnALevel = 0;
                        decodedInputHolder.BtnBLevel = 1;
                        decodedInputHolder.BtnCLevel = 0;
                        decodedInputHolder.BtnDLevel = 0;
                        decodedInputHolder.BtnELevel = 0;
                    } else if (!effInAir && canJumpWithinInertia && chConfig.HasDef1) {
                        decodedInputHolder.Dx = 0;
                        decodedInputHolder.Dy = +2;
                        decodedInputHolder.BtnALevel = 0;
                        decodedInputHolder.BtnBLevel = 0;
                        decodedInputHolder.BtnCLevel = 0;
                        decodedInputHolder.BtnDLevel = 0;
                        decodedInputHolder.BtnELevel = 0;
                        visionReaction = TARGET_CH_REACTION_DEF1;
                    } else {
                        // Just don't jump if it's melee incoming bullet
                        if (notDashing) {
                            // Because dashing often has a few invinsible startup frames.
                            if (chConfig.SlidingEnabled && !effInAir) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                decodedInputHolder.Dx = currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnELevel = 1;
                            }
                            visionReaction = TARGET_CH_REACTION_FOLLOW;
                        }
                    }
                }
            }

            if (TARGET_CH_REACTION_UNKNOWN == visionReaction) {
                if (canJumpWithinInertia && !effInAir && currCharacterDownsync.PrimarilyOnSlippableHardPushback) {
                    visionReaction = TARGET_CH_REACTION_SLIP_JUMP_TOWARDS_CH;
                }
            }
        }

        private static int frontAllyReachableByIvSlot(CharacterDownsync currCharacterDownsync, bool effInAir, CharacterConfig chConfig, Collider aCollider, CharacterDownsync v5, CharacterConfig allyConfig, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float allyBoxLeft, float allyBoxRight, float allyBoxBottom, float allyBoxTop) {
            return TARGET_CH_REACTION_UNKNOWN;
        }

        private static int frontAllyReachableByDragonPunch(CharacterDownsync currCharacterDownsync, bool effInAir, CharacterConfig chConfig, bool canJumpWithinInertia, Collider aCollider, CharacterDownsync v5, CharacterConfig allyConfig, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float allyBoxLeft, float allyBoxRight, float allyBoxBottom, float allyBoxTop) {
            return TARGET_CH_REACTION_UNKNOWN;
        }

        private static int frontAllyReachableByMelee1(CharacterDownsync currCharacterDownsync, bool effInAir, Collider aCollider, CharacterDownsync v5, CharacterConfig allyConfig, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float allyBoxLeft, float allyBoxRight, float allyBoxBottom, float allyBoxTop) {
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_ANGEL:
                    if (v5.Hp >= allyConfig.Hp) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < BasicHpHealer.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BasicHpHealerStarterHit.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BasicHpHealerStarterHit.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BasicHpHealerStarterHit.HitboxSizeX), (BasicHpHealerStarterHit.HitboxSizeY));
                    break;
                default:
                    return TARGET_CH_REACTION_UNKNOWN;
            }
 
            if (0 <= colliderDx) {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx + boxCwHalf > allyBoxLeft) && (boxCy + boxChHalf > allyBoxBottom); 
                } else {
                    closeEnough = (boxCx + boxCwHalf > allyBoxLeft) && (boxCy - boxChHalf < allyBoxTop); 
                }
            } else {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx - boxCwHalf < allyBoxRight) && (boxCy + boxChHalf > allyBoxBottom); 
                } else {
                    closeEnough = (boxCx - boxCwHalf < allyBoxRight) && (boxCy - boxChHalf < allyBoxTop); 
                }
            }
            if (closeEnough) {
                return TARGET_CH_REACTION_USE_MELEE;
            } else {
                return TARGET_CH_REACTION_UNKNOWN; // Don't follow in this case
            }
        }

        private static int frontAllyReachableByFireball(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, CharacterDownsync v5, CharacterConfig allyConfig, Collider bCollider, float colliderDx, float colliderDy, float absColliderDy, float allyBoxLeft, float allyBoxRight, float allyBoxBottom, float allyBoxTop) {
            return TARGET_CH_REACTION_UNKNOWN;
        }
        
        private static int frontOpponentReachableByIvSlot(CharacterDownsync currCharacterDownsync, bool effInAir, CharacterConfig chConfig, Collider aCollider, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            InventorySlot? targetSlot = null;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_BLADEGIRL:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * (SuperBladeHit1.HitboxOffsetX << 1), currCharacterDownsync.VirtualGridY + SuperBladeHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((SuperBladeHit1.HitboxSizeX), (SuperBladeHit1.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    return TARGET_CH_REACTION_USE_SLOT_C;
                case SPECIES_RIDLEYDRAKE:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return TARGET_CH_REACTION_UNKNOWN;
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*HeatBeamBulletHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + HeatBeamBulletHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((HeatBeamBulletHit1.HitboxSizeX << 1), (HeatBeamBulletHit1.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_UNKNOWN;

                    // A special case
                    if (0 <= colliderDx) {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    } else {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    }
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_SLOT_C;
                    } else {
                        return TARGET_CH_REACTION_UNKNOWN;
                    }
                case SPECIES_ANGEL:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * AngelBasicBulletHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + AngelBasicBulletHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((AngelBasicBulletHit1.HitboxSizeX << 1), (AngelBasicBulletHit1.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_FLEE_OPPO;

                    // A special case
                    if (0 <= colliderDx) {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    } else {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    }
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_SLOT_C;
                    } else {
                        return TARGET_CH_REACTION_FLEE_OPPO;
                    }
                case SPECIES_FIRESWORDMAN:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return TARGET_CH_REACTION_UNKNOWN;
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (effInAir) return TARGET_CH_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * FireSwordManFireBreathBl1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + FireSwordManFireBreathBl1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManFireBreathBl1.HitboxSizeX >> 1), (FireSwordManFireBreathBl1.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    break;
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return TARGET_CH_REACTION_UNKNOWN;
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (effInAir) return TARGET_CH_REACTION_FOLLOW;
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_NOT_ENOUGH_MP;

                    // A special case
                    return TARGET_CH_REACTION_USE_SLOT_C;
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return TARGET_CH_REACTION_UNKNOWN;
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (effInAir) return TARGET_CH_REACTION_FOLLOW;
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_NOT_ENOUGH_MP;

                    return TARGET_CH_REACTION_USE_SLOT_C;
                case SPECIES_BOMBERGOBLIN:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (effInAir) return TARGET_CH_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * (GoblinMelee1PrimerBullet.HitboxOffsetX << 1), currCharacterDownsync.VirtualGridY + GoblinMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((GoblinMelee1PrimerBullet.HitboxSizeX), (GoblinMelee1PrimerBullet.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    return TARGET_CH_REACTION_USE_SLOT_C;
                default:
                    return TARGET_CH_REACTION_UNKNOWN;
            }
 
            if (0 <= colliderDx) {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                } else {
                    closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy - boxChHalf < opponentBoxTop); 
                }
            } else {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                } else {
                    closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy - boxChHalf < opponentBoxTop); 
                }
            }
            if (closeEnough) {
                return TARGET_CH_REACTION_USE_SLOT_C;
            } else {
                return TARGET_CH_REACTION_FOLLOW;
            }
        }

        private static int frontOpponentReachableByDragonPunch(CharacterDownsync currCharacterDownsync, bool effInAir, CharacterConfig chConfig, bool canJumpWithinInertia, Collider aCollider, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop, bool forBlHandling) {
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_BLADEGIRL:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BladeGirlDragonPunchPrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + (chConfig.DefaultSizeY >> 1) + BladeGirlDragonPunchPrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BladeGirlDragonPunchPrimerBullet.HitboxSizeX >> 1), BladeGirlDragonPunchPrimerBullet.HitboxSizeY);
                    if (0.1f*aCollider.H < colliderDy) {
                        if (0 <= colliderDx) {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                        } else {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                        }
                    } // Don't use DragonPunch otherwise
                    break;
                case SPECIES_SWORDMAN_BOSS:
                case SPECIES_SWORDMAN:
                    if (currCharacterDownsync.Mp < SwordManDragonPunchPrimerSkill.MpDelta) {
                        bool closeEnoughAlt = canJumpWithinInertia && !effInAir && (0.6f * aCollider.H < colliderDy) && (0 <= currCharacterDownsync.DirX * colliderDx);
                        if (closeEnoughAlt) {
                            if (forBlHandling) {
                                return TARGET_CH_REACTION_DEF1;
                            } else {
                                return TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                            }
                        } else {
                            return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                        }
                    }

                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * SwordManDragonPunchPrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + (chConfig.DefaultSizeY >> 1) + SwordManDragonPunchPrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((SwordManDragonPunchPrimerBullet.HitboxSizeX >> 1), SwordManDragonPunchPrimerBullet.HitboxSizeY);
                    if (0.1f*aCollider.H < colliderDy) {
                        if (0 <= colliderDx) {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                        } else {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                        }
                    } // Don't use DragonPunch otherwise
                    break;
                case SPECIES_FIRESWORDMAN:
                    if (currCharacterDownsync.Mp < FireSwordManDragonPunchPrimerSkill.MpDelta) {
                        bool closeEnoughAlt = canJumpWithinInertia && !effInAir && (0.6f * aCollider.H < colliderDy) && (0 <= currCharacterDownsync.DirX * colliderDx);
                        if (closeEnoughAlt) {
                            if (forBlHandling) {
                                return TARGET_CH_REACTION_DEF1;
                            } else {
                                return TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                            }
                        } else {
                            return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                        }
                    }
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * FireSwordManDragonPunchPrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + (chConfig.DefaultSizeY >> 1) + FireSwordManDragonPunchPrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManDragonPunchPrimerBullet.HitboxSizeX >> 1), FireSwordManDragonPunchPrimerBullet.HitboxSizeY);
                    if (0.1f*aCollider.H < colliderDy) {
                        if (0 <= colliderDx) {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                        } else {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                        }
                    } // Don't use DragonPunch otherwise
                    break;
                case SPECIES_SKELEARCHER:
                    if (currCharacterDownsync.Mp < RisingPurpleArrowSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 < colliderDy && absColliderDy > 0.8f * (bCollider.H-aCollider.H)); // A special case
                    break;
                case SPECIES_DARKBEAMTOWER:
                    if (currCharacterDownsync.Mp < RisingPurpleArrowSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 < colliderDy && absColliderDy > 0.8f * aCollider.H); // A special case
                    break;
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Mp < StoneRollSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 < colliderDy && absColliderDy > 2.2f*aCollider.H); // A special case
                    break;
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Mp < DemonDiverImpact.MpDelta) {
                        return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    }
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * DemonDiverImpactPreJumpBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + (chConfig.DefaultSizeY >> 1) + DemonDiverImpactPreJumpBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((DemonDiverImpactPreJumpBullet.HitboxSizeX >> 1), (DemonDiverImpactPreJumpBullet.HitboxSizeY >> 1));
                    if (0.1f*aCollider.H < colliderDy) {
                        if (0 <= colliderDx) {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                        } else {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                        }
                    } // Don't use DragonPunch otherwise
                    break;
                case SPECIES_RIDLEYDRAKE:
                    closeEnough = canJumpWithinInertia && !currCharacterDownsync.OmitGravity && (0.6f*aCollider.H < colliderDy) && (0 <= currCharacterDownsync.DirX*colliderDx);
                    if (closeEnough) {
                        // Deliberately ignores "forBlHandling"
                        return TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                    } else {
                        return TARGET_CH_REACTION_FOLLOW;
                    }
                case SPECIES_BOAR:
                    return TARGET_CH_REACTION_FOLLOW;
                default:
                    return TARGET_CH_REACTION_UNKNOWN;
            }
            if (closeEnough) {
                return TARGET_CH_REACTION_USE_DRAGONPUNCH;
            } else {
                return TARGET_CH_REACTION_FOLLOW;
            }
        }

        private static int frontOpponentReachableByMelee1(CharacterDownsync currCharacterDownsync, bool effInAir, Collider aCollider, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_BLADEGIRL:
                    switch (currCharacterDownsync.CharacterState) {
                        case Atk1:
                            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BasicBladeHit2.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BasicBladeHit2.HitboxOffsetY);
                            (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BasicBladeHit2.HitboxSizeX >> 1), (BasicBladeHit2.HitboxSizeY >> 1));
                        break;
                        case Atk2:
                            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BasicBladeHit3.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BasicBladeHit3.HitboxOffsetY);
                            (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BasicBladeHit3.HitboxSizeX >> 1), (BasicBladeHit3.HitboxSizeY >> 1));
                        break;
                        default:
                            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BasicBladeHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BasicBladeHit1.HitboxOffsetY);
                            (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BasicBladeHit1.HitboxSizeX >> 1), (BasicBladeHit1.HitboxSizeY >> 1));
                        break;
                    }
                    break;
                case SPECIES_LIGHTGUARD_RED:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * LightGuardMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + LightGuardMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((LightGuardMelee1PrimerBullet.HitboxSizeX >> 1), (LightGuardMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_HEAVYGUARD_RED:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * HeavyGuardMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + HeavyGuardMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((HeavyGuardMelee1PrimerBullet.HitboxSizeX >> 1), (HeavyGuardMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_RIDERGUARD_RED:
                    if (currCharacterDownsync.Mp < RiderGuardMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * (RiderGuardMelee1PrimerBullet.HitboxOffsetX << 1), currCharacterDownsync.VirtualGridY + RiderGuardMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr(RiderGuardMelee1PrimerBullet.HitboxSizeX, (RiderGuardMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_SWORDMAN_BOSS:
                case SPECIES_SWORDMAN:
                    if (currCharacterDownsync.Mp < SwordManMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    if ((InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return TARGET_CH_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * SwordManMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + SwordManMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((SwordManMelee1PrimerBullet.HitboxSizeX >> 1), (SwordManMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_FIRESWORDMAN:
                    if (currCharacterDownsync.Mp < FireSwordManMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    if ((InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return TARGET_CH_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * FireSwordManMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + FireSwordManMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManMelee1PrimerBullet.HitboxSizeX >> 1), (FireSwordManMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Mp < DemonFireSlimeMelee1PrimarySkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * DemonFireSlimeMelee1PrimaryBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DemonFireSlimeMelee1PrimaryBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((DemonFireSlimeMelee1PrimaryBullet.HitboxSizeX >> 1), (DemonFireSlimeMelee1PrimaryBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_GOBLIN:
                case SPECIES_BOMBERGOBLIN:
                    if (currCharacterDownsync.Mp < GoblinMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * GoblinMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + GoblinMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((GoblinMelee1PrimerBullet.HitboxSizeX >> 1), (GoblinMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_BOARWARRIOR:
                    if (currCharacterDownsync.Mp < BoarWarriorMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BoarWarriorMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BoarWarriorMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BoarWarriorMelee1PrimerBullet.HitboxSizeX >> 1), (BoarWarriorMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_BOAR:
                    if (currCharacterDownsync.Mp < BoarMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BoarMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BoarMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BoarMelee1PrimerBullet.HitboxSizeX >> 1), (BoarMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_SKELEARCHER:
                    if (currCharacterDownsync.Mp < PurpleArrowPrimarySkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    closeEnough = (absColliderDy < 1.2f * (bCollider.H - aCollider.H)); // A special case
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_MELEE;
                    } else {
                        return TARGET_CH_REACTION_FLEE_OPPO;
                    }
                case SPECIES_DARKBEAMTOWER:
                    if (currCharacterDownsync.Mp < DarkTowerLowerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    closeEnough = (0 > colliderDy || 0.6f * aCollider.H < aCollider.Y + aCollider.H - opponentBoxTop) && (absColliderDx < 5.0f * aCollider.W); // A special case
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_USE_MELEE;
                    }
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Mp < StoneSwordSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    closeEnough = (0 < colliderDy && absColliderDy < 1.4f * aCollider.H) && (absColliderDx < 8.0f * aCollider.W); // A special case
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_MELEE;
                    } else {
                        return TARGET_CH_REACTION_FLEE_OPPO;
                    }
                case SPECIES_BAT:
                case SPECIES_FIREBAT:
                    if (currCharacterDownsync.Mp < BatMelee1Primer.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BatMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BatMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BatMelee1PrimerBullet.HitboxSizeX >> 1), (BatMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_RIDLEYDRAKE:
                    if (effInAir && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return TARGET_CH_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * RidleyMelee1Hit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + RidleyMelee1Hit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((RidleyMelee1Hit1.HitboxSizeX >> 1), (RidleyMelee1Hit1.HitboxSizeY >> 1));
                    break;
                default:
                    return TARGET_CH_REACTION_UNKNOWN;
            }
 
            if (0 <= colliderDx) {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                } else {
                    closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy - boxChHalf < opponentBoxTop); 
                }
            } else {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                } else {
                    closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy - boxChHalf < opponentBoxTop); 
                }
            }
            if (closeEnough) {
                return TARGET_CH_REACTION_USE_MELEE;
            } else {
                return TARGET_CH_REACTION_FOLLOW;
            }
        }

        private static int frontOpponentReachableByFireball(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider bCollider, float colliderDx, float colliderDy, float absColliderDx, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            // Whenever there's an opponent in vision, it's deemed already close enough for fireball
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_BLADEGIRL:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (!currCharacterDownsync.InAir) return TARGET_CH_REACTION_UNKNOWN; 
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * DiverImpactStarterBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DiverImpactStarterBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((DiverImpactStarterBullet.HitboxSizeX << 1), (DiverImpactStarterBullet.HitboxSizeY >> 1));
                    
                    // A special case
                    if (0 <= colliderDx) {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    } else {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    }
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_UNKNOWN;
                    }
                case SPECIES_RIDLEYDRAKE:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < DrakePrimerFireball.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * DrakePrimerFireballBl1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DrakePrimerFireballBl1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((DrakePrimerFireballBl1.HitboxSizeX << 1), (DrakePrimerFireballBl1.HitboxSizeY >> 1));
                    
                    // A special case
                    if (0 <= colliderDx) {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    } else {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCy - boxChHalf < opponentBoxTop);
                        }
                    }
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_UNKNOWN;
                    }
                case SPECIES_FIRESWORDMAN:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < FireSwordManFireballSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*FireSwordManFireballPrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + FireSwordManFireballPrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManFireballPrimerBullet.HitboxSizeX << 1), (FireSwordManFireballPrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_BOAR:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < BoarImpact.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*(chConfig.VisionOffsetX), currCharacterDownsync.VirtualGridY + BoarImpactHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((chConfig.VisionOffsetX), (BoarImpactHit1.HitboxSizeY));
                    break;
                case SPECIES_FIREBAT:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < DroppingFireballSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*DroppingFireballHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DroppingFireballHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr(0, (DroppingFireballHit1.HitboxSizeY >> 1));
                    // A special case
                    if (0 > colliderDy) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_FOLLOW;
                    }
                case SPECIES_DEMON_FIRE_SLIME:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < DemonFireBreathSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * DemonFireBreathHit.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DemonFireBreathHit.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((DemonFireBreathHit.HitboxSizeX >> 1), (DemonFireBreathHit.HitboxSizeY >> 1));
                    // A special case
                    if (0 <= colliderDx) {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy - boxChHalf < opponentBoxTop);
                        }
                    } else {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy - boxChHalf < opponentBoxTop);
                        }
                    }

                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_FOLLOW;
                    }
                case SPECIES_STONE_GOLEM:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < StoneDropperSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * StoneDropperStarterHit.HitboxOffsetX, currCharacterDownsync.VirtualGridY + StoneDropperStarterHit.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((StoneDropperStarterHit.VisionSizeX >> 1), (StoneDropperStarterHit.VisionSizeY >> 1));
                    // A special case
                    if (0 <= colliderDx) {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCx + boxCwHalf > opponentBoxLeft) && (boxCy - boxChHalf < opponentBoxTop);
                        }
                    } else {
                        if (0 <= colliderDy) {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom);
                        } else {
                            closeEnough = (boxCx - boxCwHalf < opponentBoxRight) && (boxCy - boxChHalf < opponentBoxTop);
                        }
                    }

                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_FOLLOW;
                    }
                case SPECIES_SKELEARCHER:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < FallingPurpleArrowSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 > colliderDy && absColliderDy > 0.5f * (bCollider.H-aCollider.H)); // A special case
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_FLEE_OPPO;
                    }
                case SPECIES_DARKBEAMTOWER:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < DarkTowerLowerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 > colliderDy || 0.6f * aCollider.H < aCollider.Y+aCollider.H-opponentBoxTop) && (absColliderDx < 5.0f * aCollider.W); // A special case
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_FIREBALL;
                    } else {
                        return TARGET_CH_REACTION_USE_MELEE;
                    }
                default:
                    return TARGET_CH_REACTION_UNKNOWN;
            }

            if (0 <= colliderDx) {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx + boxCwHalf < opponentBoxLeft && boxCx + 5*boxCwHalf > opponentBoxLeft) && (boxCy + boxChHalf > opponentBoxBottom); 
                } else {
                    closeEnough = (boxCx + boxCwHalf < opponentBoxLeft && boxCx + 5*boxCwHalf > opponentBoxLeft) && (boxCy - boxChHalf < opponentBoxTop); 
                }
            } else {
                if (0 <= colliderDy) {
                    closeEnough = (boxCx - boxCwHalf > opponentBoxRight && boxCx - 5*boxCwHalf < opponentBoxRight) && (boxCy + boxChHalf > opponentBoxBottom); 
                } else {
                    closeEnough = (boxCx - boxCwHalf > opponentBoxRight && boxCx - 5*boxCwHalf < opponentBoxRight) && (boxCy - boxChHalf < opponentBoxTop); 
                }
            }

            if (closeEnough) {
                return TARGET_CH_REACTION_USE_FIREBALL;
            } else {
                return TARGET_CH_REACTION_FOLLOW;
            }
        }

        public static bool isNpcDeadToDisappear(CharacterDownsync currCharacterDownsync) {
            return (0 >= currCharacterDownsync.Hp && 0 >= currCharacterDownsync.FramesToRecover);
        }

        public static bool isNpcJustDead(CharacterDownsync currCharacterDownsync) {
            return (0 >= currCharacterDownsync.Hp && DYING_FRAMES_TO_RECOVER == currCharacterDownsync.FramesToRecover);
        }

        public static (int, bool, bool, int, int, bool) deriveNpcOpPattern(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, bool currEffInAir, bool currNotDashing, bool nextEffInAIr, bool nextNoDashing, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, Collision collision, ref SatResult overlapResult, ref SatResult mvBlockerOverlapResult, InputFrameDecoded decodedInputHolder, InputFrameDecoded tempInputHolder, ILoggerBridge logger) {
            decodedInputHolder.Reset();
            /*
            [REMINDER FOR MYSELF]
            
            - All checks of oppo, ally, mvBlocker and patrolCue only happens at "visionSearchTick"
            - CachedCueCmd is only updated at "visionSearchTick", but can be executed anytime  
            - GoalAsNpc only changes at "visionSearchTick", according to checks of oppo, ally, mvBlocker and patrolCue
            - Between 2 visionSearchTicks, whenever not using skills npc cmd is either determined by (goalAsNpc, chd.dirX, chd.dirY) or cachedCueCmd 
                - cachedCueCmd is executed (which possibly holds jumping to fly if applicable)
            - Both "player" and "npc" input streamlined as 
                - somehow gets "InputFrameDecoded", e.g. "player" from physical device and "npc" from vision reaction
                - then "_deriveCharacterOpPattern(currCharacterDownsync, InputFrameDecoded) -> (patternId, jumpedOrNot, slipJumpedOrNot, effectiveDx, effectiveDy)"
                - then "_useInventory/Skill" from the result of "_deriveCharacterOpPattern"
                - then "_processJumpStartup/InertiaWalking/InertiaFlying" from the result of "_deriveCharacterOpPattern"
            */
            if (noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                updateBtnHoldingByInput(currCharacterDownsync, decodedInputHolder, thatCharacterInNextFrame);
                return (PATTERN_ID_UNABLE_TO_OP, false, false, decodedInputHolder.Dx, decodedInputHolder.Dy, false);
            }

            bool interrupted = _processDebuffDuringInput(currCharacterDownsync);
            if (interrupted) {
                updateBtnHoldingByInput(currCharacterDownsync, decodedInputHolder, thatCharacterInNextFrame);
                return (PATTERN_ID_UNABLE_TO_OP, false, false, decodedInputHolder.Dx, decodedInputHolder.Dy, false);
            }

            if (Def1 == currCharacterDownsync.CharacterState && NPC_DEF1_MIN_HOLDING_RDF_CNT > currCharacterDownsync.FramesInChState) {
                // Such that Def1 is more visible
                decodedInputHolder.Dy = +2;
                updateBtnHoldingByInput(currCharacterDownsync, decodedInputHolder, thatCharacterInNextFrame);
                return (PATTERN_ID_NO_OP, false, false, decodedInputHolder.Dx, decodedInputHolder.Dy, false);
            }

            bool isCharacterFlying = (currCharacterDownsync.OmitGravity || chConfig.OmitGravity);
            int rdfId = currRenderFrame.Id;

            switch (currCharacterDownsync.GoalAsNpc) {
                case NpcGoal.Nidle:
                case NpcGoal.NidleIfGoHuntingThenPatrol:
                    decodedInputHolder.Dx = 0;                    
                    decodedInputHolder.Dy = 0;
                    thatCharacterInNextFrame.CachedCueCmd = 0;
                break; 
                default:
                    if (0 < currCharacterDownsync.CachedCueCmd) {
                        DecodeInput(currCharacterDownsync.CachedCueCmd, decodedInputHolder);
                    } else {
                        // By default keeps the movement aligned with current facing
                        decodedInputHolder.Dx = currCharacterDownsync.DirX;
                        decodedInputHolder.Dy = currCharacterDownsync.DirY;
                    }
                break;
            }

            bool slowDownToAvoidOverlap = false;
            bool hasCancellableCombo = false;
            if (!nonAttackingSet.Contains(currCharacterDownsync.CharacterState)) {
                var (skillConfig, activeBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                if (null != activeBulletConfig && 0 <= activeBulletConfig.CancellableStFrame && 0 < activeBulletConfig.CancellableEdFrame) {
                    hasCancellableCombo = (currCharacterDownsync.FramesInChState >= ((activeBulletConfig.CancellableStFrame + activeBulletConfig.CancellableEdFrame) >> 1));
                }
            }

            int visionReaction = TARGET_CH_REACTION_UNKNOWN;
            bool visionSearchTickAlt = (0 == ((currCharacterDownsync.FramesInChState + currCharacterDownsync.Id) & VISION_SEARCH_RDF_RANDOMIZE_MASK));
            bool visionSearchTick = (0 == (currCharacterDownsync.FramesInChState & chConfig.VisionSearchIntervalPow2Minus1));
            if (!chConfig.IsKeyCh) {
                // Randomization
                if (!visionSearchTick) {
                    visionSearchTick = visionSearchTickAlt; 
                } else {
                    if (visionSearchTickAlt) {
                        visionSearchTick = false;
                    }
                }
            }
            bool visionSearchTickWhenNonAtk = (nonAttackingSet.Contains(currCharacterDownsync.CharacterState) && visionSearchTick);
            bool shouldCheckVisionCollision = (0 < chConfig.VisionSizeX && 0 < chConfig.VisionSizeY && (visionSearchTickWhenNonAtk || hasCancellableCombo));
            bool inAntiGravityGracePeriod = chConfig.AntiGravityWhenIdle && currCharacterDownsync.InAir && InAirIdle1NoJump == currCharacterDownsync.CharacterState;
            if (inAntiGravityGracePeriod) {
                shouldCheckVisionCollision = false;
                decodedInputHolder.Reset();
            }

            if (0 < currCharacterDownsync.FramesToRecover && (0 < currCharacterDownsync.CachedCueCmd && !hasCancellableCombo)) {
                shouldCheckVisionCollision = false;
            }

            if (shouldCheckVisionCollision) {
                bool inputIntentionDifferentFromCurrDir = false;
                if (0 > decodedInputHolder.Dx*currCharacterDownsync.DirX || 0 > decodedInputHolder.Dy*currCharacterDownsync.DirY) {
                    inputIntentionDifferentFromCurrDir = true;
                } /* else if (0 == decodedInputHolder.Dx && 0 != currCharacterDownsync.DirX) {
                    // Deliberately skipping this condition to favor moving 
                    inputIntentionDifferentFromCurrDir = true;
                } */ else if (0 == currCharacterDownsync.VelX && 0 != decodedInputHolder.Dx) {
                    inputIntentionDifferentFromCurrDir = true;
                } /* else if (0 == decodedInputHolder.Dy && 0 != currCharacterDownsync.DirY) {
                    // Deliberately skipping this condition to favor moving 
                    inputIntentionDifferentFromCurrDir = true;
                } */ else if (isCharacterFlying && 0 == currCharacterDownsync.VelY && 0 != decodedInputHolder.Dy) {
                    inputIntentionDifferentFromCurrDir = true;
                }
                if (0 < currCharacterDownsync.FramesCapturedByInertia && inputIntentionDifferentFromCurrDir) {
                    shouldCheckVisionCollision = false;
                }
            }

            bool inProactiveJumpOrJumpStartupOrJumpEnd = (proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState) || proactiveJumpingSet.Contains(thatCharacterInNextFrame.CharacterState) || isInJumpStartup(currCharacterDownsync, chConfig) || isInJumpStartup(thatCharacterInNextFrame, chConfig) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig));

            switch (currCharacterDownsync.GoalAsNpc) {
                case NpcGoal.NhuntThenIdle:
                case NpcGoal.NhuntThenFollowAlly:
                case NpcGoal.NhuntThenPatrol:
                    if (inProactiveJumpOrJumpStartupOrJumpEnd) {
                        // A grace period for seemingly smooth jumping
                        shouldCheckVisionCollision = false;
                    }
                    if (NPC_FLEE_GRACE_PERIOD_RDF_CNT >= currCharacterDownsync.FramesInChState) {
                        // A grace period for seemingly smooth movement
                        shouldCheckVisionCollision = false;
                    }
                    break;
            }

            bool canJumpWithinInertia = (0 == currCharacterDownsync.FramesToRecover && ((chConfig.InertiaFramesToRecover >> 1) > currCharacterDownsync.FramesCapturedByInertia));
            if (
                (isInJumpStartup(thatCharacterInNextFrame, chConfig) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig))
            ) {
                canJumpWithinInertia = false;
            }

            if (inAntiGravityGracePeriod) {
                thatCharacterInNextFrame.CachedCueCmd = 0;
            } else if (!shouldCheckVisionCollision) {
                if (_executeCachedCueCmd(currCharacterDownsync, chConfig, decodedInputHolder)) {
                    thatCharacterInNextFrame.CachedCueCmd = _sanitizeCachedCueCmd(currCharacterDownsync.CachedCueCmd);
                    DecodeInput(thatCharacterInNextFrame.CachedCueCmd, decodedInputHolder);
                }
                
                // [WARNING] Without proper release of "BtnA", an NPC might be stuck at holding it and increasing "chd.JumpHoldingRdfCnt" forever but NOT ABLE TO JUMP when needed!
                if (chConfig.JumpHoldingToFly && (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= currCharacterDownsync.JumpHoldingRdfCnt || (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 << 1) < currCharacterDownsync.FramesInChState || !inProactiveJumpOrJumpStartupOrJumpEnd)) {
                    decodedInputHolder.BtnALevel = 0;
                    ulong newCachedCueCmd = EncodeInput(decodedInputHolder);
                    thatCharacterInNextFrame.CachedCueCmd = newCachedCueCmd;
                } else if (!chConfig.JumpHoldingToFly && (JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= currCharacterDownsync.JumpHoldingRdfCnt || JUMP_HOLDING_RDF_CNT_THRESHOLD_2 < currCharacterDownsync.FramesInChState || !inProactiveJumpOrJumpStartupOrJumpEnd)) {
                    decodedInputHolder.BtnALevel = 0;
                    ulong newCachedCueCmd = EncodeInput(decodedInputHolder);
                    thatCharacterInNextFrame.CachedCueCmd = newCachedCueCmd;
                }
            } else {
                (visionReaction, slowDownToAvoidOverlap) = _decideVisionReaction(rdfId, currCharacterDownsync, currEffInAir, currNotDashing, nextEffInAIr, nextNoDashing, canJumpWithinInertia, isCharacterFlying, currRenderFrame, roomCapacity, chConfig, thatCharacterInNextFrame, dynamicRectangleColliders, colliderCnt, collisionSys, collision, ref overlapResult, ref mvBlockerOverlapResult, decodedInputHolder, tempInputHolder, logger);

                ulong oldCachedCueCmd = thatCharacterInNextFrame.CachedCueCmd;
                ulong newCachedCueCmd = EncodeInput(decodedInputHolder);
                switch (visionReaction) {
                    case TARGET_CH_REACTION_UNKNOWN:
                        DecodeInput(_sanitizeCachedCueCmd(currCharacterDownsync.CachedCueCmd), decodedInputHolder);
                        if (!isCharacterFlying && 0 != decodedInputHolder.Dy && NPC_DEF1_MIN_HOLDING_RDF_CNT < currCharacterDownsync.FramesInChState) {
                            decodedInputHolder.Dy = 0;
                        }
                        newCachedCueCmd = EncodeInput(decodedInputHolder); 
                        break;
                    case TARGET_CH_REACTION_WALK_ALONG:
                        switch (currCharacterDownsync.GoalAsNpc) {
                            case NpcGoal.NidleIfGoHuntingThenPatrol:
                                thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Npatrol;
                                break;
                        }
                        break;
                    case TARGET_CH_REACTION_TURNAROUND_MV_BLOCKER:
                        if (oldCachedCueCmd != newCachedCueCmd) {
                            thatCharacterInNextFrame.FramesToRecover = NPC_FLEE_GRACE_PERIOD_RDF_CNT;
                            //logger.LogInfo($"\t@rdfId={rdfId}, delayed to turnaround from mv blocker\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, cachedCueCmd:{thatCharacterInNextFrame.CachedCueCmd}, FramesToRecover:{thatCharacterInNextFrame.FramesToRecover}, goal:{thatCharacterInNextFrame.GoalAsNpc})");
                        }
                        decodedInputHolder.Reset();
                        break;
                    case TARGET_CH_REACTION_STOP_BY_MV_BLOCKER:
                        switch (currCharacterDownsync.GoalAsNpc) {
                            case NpcGoal.Npatrol:
                                thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NidleIfGoHuntingThenPatrol;
                                break;
                        }
                        break;
                    case TARGET_CH_REACTION_STOP_BY_PATROL_CUE_CAPTURED:
                    case TARGET_CH_REACTION_STOP_BY_PATROL_CUE_ENTER:
                        switch (currCharacterDownsync.GoalAsNpc) {
                            case NpcGoal.Npatrol:
                                thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NidleIfGoHuntingThenPatrol;
                                break;
                        }
                        decodedInputHolder.Reset();
                        break;
                }

                // Overwrite the "cachedCueCmd".
                thatCharacterInNextFrame.CachedCueCmd = newCachedCueCmd;
            }

            updateBtnHoldingByInput(currCharacterDownsync, decodedInputHolder, thatCharacterInNextFrame);

            var (patternId, jumpedOrNot, slipJumpedOrNot, effectiveDx, effectiveDy) = _deriveCharacterOpPattern(rdfId, currCharacterDownsync, decodedInputHolder, chConfig, currEffInAir, currNotDashing, logger);

            return (patternId, jumpedOrNot, slipJumpedOrNot, effectiveDx, effectiveDy, slowDownToAvoidOverlap);
        }

        private static bool _executeCachedCueCmd(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, InputFrameDecoded decodedInputHolder) {
            if (0 == currCharacterDownsync.CachedCueCmd) return false;
            // [WARNING] "canJumpWithinInertia" implies "(0 == currCharacterDownsync.FramesToRecover)"
            DecodeInput(currCharacterDownsync.CachedCueCmd, decodedInputHolder);
            return true;
        }

        private static ulong _sanitizeCachedCueCmd(ulong origCmd) {
            return (origCmd & 31u); // i.e. Only reserve directions and BtnALevel
        }

        private static void _sanitizeCachedCueCmdForPatrolCue(ulong origCmd, CharacterDownsync thatCharacterInNextFrame) {
            thatCharacterInNextFrame.CachedCueCmd = _sanitizeCachedCueCmd(origCmd); // i.e. Only reserve directions and BtnALevel
            thatCharacterInNextFrame.CapturedByPatrolCue = false;
            thatCharacterInNextFrame.FramesInPatrolCue = DEFAULT_PATROL_CUE_WAIVING_FRAMES; // re-purposed
        }

        private static void _clearCachedCueCmd(CharacterDownsync thatCharacterInNextFrame) {
            thatCharacterInNextFrame.CachedCueCmd = 0;
            thatCharacterInNextFrame.CapturedByPatrolCue = false;
            thatCharacterInNextFrame.FramesInPatrolCue = 0;
            thatCharacterInNextFrame.WaivingPatrolCueId = NO_PATROL_CUE_ID;
        }

        private static (int, bool) _decideVisionReaction(int rdfId, CharacterDownsync currCharacterDownsync, bool currEffInAir, bool currNotDashing, bool nextEffInAir, bool nextNotDashing, bool canJumpWithinInertia, bool isCharacterFlying, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, Collision collision, ref SatResult overlapResult, ref SatResult mvBlockerOverlapResult, InputFrameDecoded decodedInputHolder, InputFrameDecoded tempInputHolder, ILoggerBridge logger) {
            var aCollider = dynamicRectangleColliders[currCharacterDownsync.JoinIndex - 1]; // already added to collisionSys

            int newVisionReaction = TARGET_CH_REACTION_UNKNOWN;
            bool slowDownToAvoidOverlap = false;

            Collider? oppoChCollider = null;
            CharacterDownsync? v3 = null;

            Collider? oppoBlCollider = null;
            Bullet? v4 = null;

            Collider? allyChCollider = null;
            CharacterDownsync? v5 = null;

            Collider? mvBlockerCollider = null;
            TrapColliderAttr? v6 = null;

            Collider? standingOnCollider = null;

            float visionCx, visionCy, visionCw, visionCh;
            calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);

            var visionCollider = dynamicRectangleColliders[colliderCnt];
            UpdateRectCollider(visionCollider, visionCx, visionCy, visionCw, visionCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync, COLLISION_VISION_INDEX_PREFIX);
            collisionSys.AddSingleToCellTail(visionCollider);

            findHorizontallyClosestCharacterCollider(rdfId, currRenderFrame, currCharacterDownsync, chConfig, isCharacterFlying, visionCollider, aCollider, collision, ref overlapResult, ref mvBlockerOverlapResult, out oppoChCollider, out v3, out oppoBlCollider, out v4, out allyChCollider, out v5, out mvBlockerCollider, out v6, out standingOnCollider, logger);
            /*
            if (!isCharacterFlying && !nextEffInAir && null == standingOnCollider) {
                logger.LogInfo($"\t@rdfId={rdfId}, false==nextEffInAir but null standingOnCollider: maybe the character's vision is not low enough to cover where it stands!\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, velY:{currCharacterDownsync.VelY}, VirtualX:{currCharacterDownsync.VirtualGridX}, VirtualY:{currCharacterDownsync.VirtualGridY}, goal:{currCharacterDownsync.GoalAsNpc})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}");
            }
            */
            collisionSys.RemoveSingleFromCellTail(visionCollider); // no need to increment "colliderCnt", the visionCollider is transient
            float allyChColliderDx = 0f, allyChColliderDy = 0f;
            float allyBoxCx = 0, allyBoxCy = 0, allyBoxCw = 0, allyBoxCh = 0;
            float allyBoxLeft = 0, allyBoxRight = 0, allyBoxBottom = 0, allyBoxTop = 0;
            if (null != allyChCollider && null != v5) {
                var allyChConfig = characters[v5.SpeciesId];
                allyChColliderDx = (allyChCollider.X - aCollider.X);
                int allyDxOffsetRdfBase = ((currCharacterDownsync.FramesInChState + currCharacterDownsync.Id) & OPPO_DX_OFFSET_MOD_MINUS_1);
                float allyDxOffset = chConfig.IsKeyCh ? 0 : (OPPO_DX_OFFSET*allyDxOffsetRdfBase)/OPPO_DX_OFFSET_MOD_MINUS_1;
                if (0 < allyChColliderDx) {
                    allyChColliderDx += allyDxOffset; 
                } else if (0 > allyChColliderDx) {
                    allyChColliderDx -= allyDxOffset; 
                } 
                allyChColliderDy = (allyChCollider.Y - aCollider.Y);
                calcCharacterBoundingBoxInCollisionSpace(v5, allyChConfig, v5.VirtualGridX, v5.VirtualGridY, out allyBoxCx, out allyBoxCy, out allyBoxCw, out allyBoxCh);

                allyBoxLeft = allyBoxCx - 0.5f * allyBoxCw;     
                allyBoxRight = allyBoxCx + 0.5f * allyBoxCw;
                allyBoxBottom = allyBoxCy - 0.5f * allyBoxCh;
                allyBoxTop = allyBoxCy + 0.5f * allyBoxCh;

                if (v5.JoinIndex > currCharacterDownsync.JoinIndex && Math.Abs(v5.VirtualGridX - currCharacterDownsync.VirtualGridX) < v5.Speed && v5.SpeciesId == currCharacterDownsync.SpeciesId && v5.VelX == currCharacterDownsync.VelX && v5.VelY == currCharacterDownsync.VelY) {
                    // [WARNING] To differentiate possibly overlapping same species characters
                    slowDownToAvoidOverlap = true;
                }
            }

            float oppoChColliderDx = 0f, oppoChColliderDy = 0f;
            float opponentBoxCx = 0, opponentBoxCy = 0, opponentBoxCw = 0, opponentBoxCh = 0;
            float opponentBoxLeft = 0, opponentBoxRight = 0, opponentBoxBottom = 0, opponentBoxTop = 0;
            if (null != oppoChCollider && null != v3) {
                oppoChColliderDx = (oppoChCollider.X - aCollider.X);
                int oppoDxOffsetRdfBase = ((currCharacterDownsync.FramesInChState + currCharacterDownsync.Id) & OPPO_DX_OFFSET_MOD_MINUS_1);
                float oppoDxOffset = chConfig.IsKeyCh ? 0 : (OPPO_DX_OFFSET*oppoDxOffsetRdfBase)/OPPO_DX_OFFSET_MOD_MINUS_1;
                if (0 < oppoChColliderDx) {
                    oppoChColliderDx += oppoDxOffset; 
                } else if (0 > oppoChColliderDx) {
                    oppoChColliderDx -= oppoDxOffset; 
                } 
                oppoChColliderDy = (oppoChCollider.Y - aCollider.Y);

                var oppoChConfig = characters[v3.SpeciesId];
                calcCharacterBoundingBoxInCollisionSpace(v3, oppoChConfig, v3.VirtualGridX, v3.VirtualGridY, out opponentBoxCx, out opponentBoxCy, out opponentBoxCw, out opponentBoxCh);

                opponentBoxLeft = opponentBoxCx - 0.5f * opponentBoxCw;     
                opponentBoxRight = opponentBoxCx + 0.5f * opponentBoxCw;
                opponentBoxBottom = opponentBoxCy - 0.5f * opponentBoxCh;
                opponentBoxTop = opponentBoxCy + 0.5f * opponentBoxCh;
            }
            if (chConfig.NpcPrioritizeBulletHandling) {
                _handleOppoBl(rdfId, currCharacterDownsync, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref newVisionReaction, decodedInputHolder, logger);
                if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction) {
                    if (chConfig.NpcPrioritizeAllyHealing && null != v5) {
                        var allyChConfig = characters[v5.SpeciesId];
                        _handleAllyCh(currCharacterDownsync, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, v5, allyChConfig, allyChCollider, allyChColliderDx, allyChColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop, ref newVisionReaction, decodedInputHolder);
                        if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == newVisionReaction) {
                            _handleOppoCh(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref newVisionReaction, decodedInputHolder, logger); 
                        }
                    } else {
                        _handleOppoCh(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref newVisionReaction, decodedInputHolder, logger); 
                    }
                }
            } else if (chConfig.NpcPrioritizeAllyHealing && null != v5) {
                var allyChConfig = characters[v5.SpeciesId];
                _handleAllyCh(currCharacterDownsync, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, v5, allyChConfig, allyChCollider, allyChColliderDx, allyChColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop, ref newVisionReaction, decodedInputHolder);
                if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == newVisionReaction) {
                    if (chConfig.NpcPrioritizeBulletHandling) {
                        _handleOppoBl(rdfId, currCharacterDownsync, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref newVisionReaction, decodedInputHolder, logger);
                        if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction) {
                            _handleOppoCh(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref newVisionReaction, decodedInputHolder, logger); 
                        }
                    } else {
                        _handleOppoCh(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref newVisionReaction, decodedInputHolder, logger); 
                        if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == newVisionReaction) {
                            _handleOppoBl(rdfId, currCharacterDownsync, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref newVisionReaction, decodedInputHolder, logger);
                        }
                    }
                }
            } else {
                _handleOppoCh(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, isCharacterFlying, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref newVisionReaction, decodedInputHolder, logger); 
                if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == newVisionReaction) {
                    _handleOppoBl(rdfId, currCharacterDownsync, chConfig, aCollider, visionCollider, currEffInAir, currNotDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref newVisionReaction, decodedInputHolder, logger);
                }
            }
            visionCollider.Data = null;
    
            if (TARGET_CH_REACTION_SLIP_JUMP_TOWARDS_CH == newVisionReaction) {
                decodedInputHolder.Dx = 0;
                decodedInputHolder.Dy = -2;
                decodedInputHolder.BtnALevel = 1;
                //logger.LogInfo($"handleOppoCh/end, rdfId={rdfId}, about to slip jump, currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState}, jhrdfc:{currCharacterDownsync.JumpHoldingRdfCnt})");
            } else if (TARGET_CH_REACTION_JUMP_TOWARDS_CH == newVisionReaction) {
                decodedInputHolder.Dy = 0;
                decodedInputHolder.BtnALevel = 1;
            } else {
                // It's important to unset "BtnALevel" if no proactive jump is implied by vision reaction, otherwise its value will remain even after execution and sanitization
                decodedInputHolder.BtnALevel = 0;
            }

            if (decodedInputHolder.HasCriticalBtnLevel()) {
                _clearCachedCueCmd(thatCharacterInNextFrame);
            } else {
                // Only "decodedInputHolder.Dx & Dy" assigned by far, may need "deriveReactionForMvBlocker" to patch handling of "mvBlockerCollider & standingOnCollider", e.g. jumping over an "mvBlockerCollider" for "HasDef1 || NpcNotHuntingInAirOppoCh".
                int pendingNewVisionReaction = newVisionReaction;
                float aBoxLeft = aCollider.X;     
                float aBoxRight = aCollider.X+aCollider.W;
                float aBoxBottom = aCollider.Y;
                float aBoxTop = aCollider.Y+aCollider.H;
                // [WARNING] These "visionReaction"s need  
                if (null != mvBlockerCollider || null != standingOnCollider) {
                    newVisionReaction  = deriveReactionForMvBlocker(rdfId, pendingNewVisionReaction, currCharacterDownsync, thatCharacterInNextFrame, currEffInAir, nextEffInAir, chConfig, canJumpWithinInertia, isCharacterFlying, aCollider, aBoxLeft, aBoxRight, aBoxBottom, aBoxTop, mvBlockerCollider, v6, standingOnCollider, mvBlockerOverlapResult, decodedInputHolder, logger);
                }
                if (TARGET_CH_REACTION_UNKNOWN == pendingNewVisionReaction && !noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                    // [WARNING] "PatrolCue reaction" can overwrite that of "mvBlocker reaction"!
                     newVisionReaction = deriveReactionForPatrolCue(rdfId, newVisionReaction, currCharacterDownsync, thatCharacterInNextFrame, chConfig, canJumpWithinInertia, isCharacterFlying, aCollider, collision, aBoxLeft, aBoxRight, aBoxBottom, aBoxTop, ref overlapResult, decodedInputHolder, tempInputHolder, logger);
                }
            }

            return (newVisionReaction, slowDownToAvoidOverlap);
        }

        private static void _processNpcInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, int colliderCnt, Collision collision, CollisionSpace collisionSys, ref SatResult overlapResult, ref SatResult mvBlockerOverlapResult, InputFrameDecoded decodedInputHolder, InputFrameDecoded tempInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            bool mockSelfNotEnoughMp = false;
            int rdfId = currRenderFrame.Id;
            for (int i = roomCapacity; i < roomCapacity + npcCnt; i++) {
                var currCharacterDownsync = currRenderFrame.NpcsArr[i - roomCapacity];
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;

                var thatCharacterInNextFrame = nextRenderFrameNpcs[i - roomCapacity];
                var chConfig = characters[currCharacterDownsync.SpeciesId];

                if (TERMINATING_TRIGGER_ID != currCharacterDownsync.SubscribesToTriggerLocalId) {
                    if (chConfig.AntiGravityWhenIdle && InAirIdle1NoJump != thatCharacterInNextFrame.CharacterState) {
                        thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                        thatCharacterInNextFrame.FramesInChState = 0;
                        thatCharacterInNextFrame.VelX = 0;
                        //logger.LogInfo($"_processNpcInputs/sleepingAntiGravityHandled, currRdfId={rdfId}, setting InAirIdle1NoJump for sleeping AntiGravityWhenIdle currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                    }
                    continue;
                }

                bool currNotDashing = isNotDashing(currCharacterDownsync);
                bool currEffInAir = isEffInAir(currCharacterDownsync, currNotDashing);
                bool nextNotDashing = isNotDashing(thatCharacterInNextFrame);
                bool nextEffInAir = isEffInAir(thatCharacterInNextFrame, currNotDashing);
                var (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy, slowDownToAvoidOverlap) = deriveNpcOpPattern(currCharacterDownsync, thatCharacterInNextFrame, currEffInAir, currNotDashing, nextNotDashing, nextEffInAir, currRenderFrame, roomCapacity, chConfig, dynamicRectangleColliders, colliderCnt, collisionSys, collision, ref overlapResult, ref mvBlockerOverlapResult, decodedInputHolder, tempInputHolder, logger);

                _processSingleCharacterInput(rdfId, patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy, slowDownToAvoidOverlap, currCharacterDownsync, currEffInAir, chConfig, thatCharacterInNextFrame, true, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, MAGIC_JOIN_INDEX_INVALID, ref mockSelfNotEnoughMp, logger);
            }
        }

        protected static bool addNewNpcToNextFrame(int rdfId, int roomCapacity, int virtualGridX, int virtualGridY, int dirX, int dirY, uint characterSpeciesId, int teamId, NpcGoal initGoal, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, int publishingToTriggerLocalIdUponKilled, ulong waveNpcKilledEvtMaskCounter, int subscribesToTriggerLocalId) {
            var chConfig = characters[characterSpeciesId];
            int birthVirtualX = virtualGridX + ((chConfig.DefaultSizeX >> 2) * dirX);
            CharacterState initChState = chConfig.OmitGravity ? Walking : Idle1;
            int joinIndex = roomCapacity + npcCnt + 1;
            AssignToCharacterDownsync(npcLocalIdCounter, characterSpeciesId, birthVirtualX, virtualGridY, dirX, dirY, 0, 0, 0, 0, 0, 0, NO_SKILL, NO_SKILL_HIT, 0, chConfig.Speed, initChState, joinIndex, chConfig.Hp, true, false, 0, 0, 0, teamId, teamId, birthVirtualX, virtualGridY, dirX, dirY, false, false, false, false, 0, 0, 0, chConfig.Mp, chConfig.OmitGravity, chConfig.OmitSoftPushback, chConfig.RepelSoftPushback, initGoal, 0, false, false, false, newBirth: true, false, 0, 0, 0, defaultTemplateBuffList, defaultTemplateDebuffList, prevInventory: null, false, publishingToTriggerLocalIdUponKilled, waveNpcKilledEvtMaskCounter, subscribesToTriggerLocalId, jumpHoldingRdfCnt: 0, btnBHoldingRdfCount: 0, btnCHoldingRdfCount: 0, btnDHoldingRdfCount: 0, btnEHoldingRdfCount: 0, parryPrepRdfCntDown: 0, chConfig.DefaultAirJumpQuota, chConfig.DefaultAirDashQuota, TERMINATING_CONSUMABLE_SPECIES_ID, TERMINATING_BUFF_SPECIES_ID, NO_SKILL, defaultTemplateBulletImmuneRecords, 0, 0, 0, MAGIC_JOIN_INDEX_INVALID, TERMINATING_BULLET_TEAM_ID, rdfId, 0, chConfig.MpRegenInterval, 0, MAGIC_JOIN_INDEX_INVALID, nextRenderFrameNpcs[npcCnt]); // TODO: Support killedToDropConsumable/Buff here

            if (null != chConfig.InitInventorySlots) {
                for (int t = 0; t < chConfig.InitInventorySlots.Count; t++) {
                    var initIvSlot = chConfig.InitInventorySlots[t];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, initIvSlot.SkillIdAir, initIvSlot.GaugeCharged, initIvSlot.GaugeRequired, initIvSlot.FullChargeSkillId, initIvSlot.FullChargeBuffSpeciesId, nextRenderFrameNpcs[npcCnt].Inventory.Slots[t]);
                }
            }
            npcLocalIdCounter++;
            npcCnt++;
            if (npcCnt < nextRenderFrameNpcs.Count) nextRenderFrameNpcs[npcCnt].Id = TERMINATING_PLAYER_ID;
            return true;
        }

        public static bool PublishNpcKilledEvt(int rdfId, ulong publishingEvtMask, int offenderJoinIndex, int offenderBulletTeamId, Trigger nextRdfTrigger, ILoggerBridge logger) {
            if (EVTSUB_NO_DEMAND_MASK == nextRdfTrigger.DemandedEvtMask) return false;
            nextRdfTrigger.FulfilledEvtMask |= publishingEvtMask;
            nextRdfTrigger.OffenderJoinIndex = offenderJoinIndex;
            nextRdfTrigger.OffenderBulletTeamId = offenderBulletTeamId;
            //logger.LogInfo(String.Format("@rdfId={0}, published evtmask = {1} to trigger editor id = {2}, local id = {3}, fulfilledEvtMask = {4}, demandedEvtMask = {5} by npc killed", rdfId, publishingEvtMask, nextRdfTrigger.EditorId, nextRdfTrigger.TriggerLocalId, nextRdfTrigger.FulfilledEvtMask, nextRdfTrigger.DemandedEvtMask));
            return true;
        }

        public static void calcNpcVisionBoxInCollisionSpace(CharacterDownsync characterDownsync, CharacterConfig chConfig, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            if (noOpSet.Contains(characterDownsync.CharacterState)) {
                (boxCx, boxCy) = (0, 0);
                (boxCw, boxCh) = (0, 0);
                return;
            }

            if ((Idle1 == characterDownsync.CharacterState || InAirIdle1NoJump == characterDownsync.CharacterState) && chConfig.AntiGravityWhenIdle) {
                (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(characterDownsync.VirtualGridX, characterDownsync.VirtualGridY + (chConfig.VisionOffsetY << 1));
                (boxCw, boxCh) = VirtualGridToPolygonColliderCtr((chConfig.VisionSizeY << 1), (chConfig.VisionSizeX << 1));
            } else {
                int xfac = (0 < characterDownsync.DirX ? 1 : -1);
                (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(characterDownsync.VirtualGridX + xfac * chConfig.VisionOffsetX, characterDownsync.VirtualGridY + chConfig.VisionOffsetY);
                (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.VisionSizeX, chConfig.VisionSizeY);
            }
        }

        private static bool _hasTransformUponDeath(CharacterDownsync candidate, CharacterConfig chConfig) {
            float characterVirtualGridTop = candidate.VirtualGridY + (chConfig.DefaultSizeY >> 1);
            return (0 <= characterVirtualGridTop) && (SPECIES_NONE_CH != chConfig.TransformIntoSpeciesIdUponDeath);
        }

        private static void remapBulletOffenderJoinIndex(int roomCapacity, int nextNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRdfBullets, Dictionary<int, int> joinIndexRemap, HashSet<int> justDeadJoinIndices) {
            for (int i = 0; i < roomCapacity + nextNpcI; i++) {
                int joinIndex = i+1;
                var thatCharacterInNextFrame = getChdFromChdArrs(joinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
                int j = thatCharacterInNextFrame.LastDamagedByJoinIndex;
                if (j > roomCapacity) {
                    // no need to check remap for players
                    if (justDeadJoinIndices.Contains(j)) {
                        thatCharacterInNextFrame.LastDamagedByJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(j)) {
                        thatCharacterInNextFrame.LastDamagedByJoinIndex = joinIndexRemap[j];
                    }
                }
                int k = thatCharacterInNextFrame.LockingOnJoinIndex;
                if (k > roomCapacity) {
                    // no need to remap for players
                    if (justDeadJoinIndices.Contains(k)) {
                        thatCharacterInNextFrame.LockingOnJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(k)) {
                        thatCharacterInNextFrame.LockingOnJoinIndex = joinIndexRemap[k];
                    }
                }
            }

            for (int i = 0; i < nextRdfBullets.Count; i++) {
                var src = nextRdfBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BulletLocalId) break;
                int j = src.OffenderJoinIndex;
                if (j > roomCapacity) {
                    // no need to check remap for players
                    if (justDeadJoinIndices.Contains(j)) {
                        src.OffenderJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(j)) {
                        src.OffenderJoinIndex = joinIndexRemap[j];
                    }
                }
                int k = src.TargetCharacterJoinIndex;
                if (k > roomCapacity) {
                    // no need to remap for players
                    if (justDeadJoinIndices.Contains(k)) {
                        src.TargetCharacterJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(k)) {
                        src.TargetCharacterJoinIndex = joinIndexRemap[k];
                    }
                }
            }
        }

        private static int deriveReactionForPatrolCue(int rdfId, int visionReactionByFar, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, bool canJumpWithinInertia, bool isCharacterFlying, Collider aCollider, Collision collision, float aBoxLeft, float aBoxRight, float aBoxBottom, float aBoxTop, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, InputFrameDecoded tempInputHolder, ILoggerBridge logger) {
            bool prevCapturedByPatrolCue = currCharacterDownsync.CapturedByPatrolCue;
            bool shouldBreakCurrentPatrolCueCapture = (true == prevCapturedByPatrolCue && NO_PATROL_CUE_ID != currCharacterDownsync.WaivingPatrolCueId && 0 == currCharacterDownsync.FramesInPatrolCue);
            bool isReallyCaptured = (true == prevCapturedByPatrolCue && NO_PATROL_CUE_ID != currCharacterDownsync.WaivingPatrolCueId && 0 < currCharacterDownsync.FramesInPatrolCue);
            if (isReallyCaptured) {
                decodedInputHolder.Reset();
                return TARGET_CH_REACTION_STOP_BY_PATROL_CUE_CAPTURED;
            } 
            if (shouldBreakCurrentPatrolCueCapture) {  
                if (_executeCachedCueCmd(currCharacterDownsync, chConfig, decodedInputHolder)) {
                    _sanitizeCachedCueCmdForPatrolCue(currCharacterDownsync.CachedCueCmd, thatCharacterInNextFrame);
                } 
                return visionReactionByFar;
            } 

            // [WARNING] The field "CharacterDownsync.FramesInPatrolCue" would also be re-purposed as "patrol cue collision waiving frames" by the logic here.
            Collider? pCollider;
            PatrolCue? ptrlCue;
            tempInputHolder.Reset();
            findHorizontallyClosestPatrolCueCollider(currCharacterDownsync, aCollider, collision, ref overlapResult, out pCollider, out ptrlCue, logger);

            if (null == pCollider || null == ptrlCue) return visionReactionByFar;
            // By now we're sure that it should react to the PatrolCue
            var colliderDx = (aCollider.X - pCollider.X);
            var colliderDy = (aCollider.Y - pCollider.Y);
            bool fr = 0 < colliderDx && (0 > currCharacterDownsync.VelX || (0 == currCharacterDownsync.VelX && 0 > currCharacterDownsync.DirX));
            bool fl = 0 > colliderDx && (0 < currCharacterDownsync.VelX || (0 == currCharacterDownsync.VelX && 0 < currCharacterDownsync.DirX));
            bool fu = 0 < colliderDy && (0 > currCharacterDownsync.VelY || (0 == currCharacterDownsync.VelY && 0 > currCharacterDownsync.DirY));
            bool fd = 0 > colliderDy && (0 < currCharacterDownsync.VelY || (0 == currCharacterDownsync.VelY && 0 < currCharacterDownsync.DirY));
            if (!fr && !fl && !fu && !fd) {
                fr = 0 > currCharacterDownsync.DirX;
                fl = 0 < currCharacterDownsync.DirX;
            }

            ulong toCacheCmd = 0;
            var targetFramesInPatrolCue = 0;
            if (fr && 0 != ptrlCue.FrAct) {
                targetFramesInPatrolCue = ptrlCue.FrCaptureFrames;
                DecodeInput(ptrlCue.FrAct, tempInputHolder);
                toCacheCmd = ptrlCue.FrAct;
                //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the right", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue)); 
            } else if (fl && 0 != ptrlCue.FlAct) {
                targetFramesInPatrolCue = ptrlCue.FlCaptureFrames;
                DecodeInput(ptrlCue.FlAct, tempInputHolder);
                toCacheCmd = ptrlCue.FlAct;
                //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the left", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue));
            } else if (fu && 0 != ptrlCue.FuAct) {
                targetFramesInPatrolCue = ptrlCue.FuCaptureFrames;
                DecodeInput(ptrlCue.FuAct, tempInputHolder);
                toCacheCmd = ptrlCue.FuAct;
                //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the top", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue)); 
            } else if (fd && 0 != ptrlCue.FdAct) {
                targetFramesInPatrolCue = ptrlCue.FdCaptureFrames;
                DecodeInput(ptrlCue.FdAct, tempInputHolder);
                toCacheCmd = ptrlCue.FdAct;
                //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the bottom", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue)); 
            } else {
                //logger.LogWarn(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3}, dirX: {9} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} but direction couldn't be determined!", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue, currCharacterDownsync.DirX));
            }

            bool shouldWaivePatrolCueReaction = (false == prevCapturedByPatrolCue && 0 < currCharacterDownsync.FramesInPatrolCue && ptrlCue.Id == currCharacterDownsync.WaivingPatrolCueId && !tempInputHolder.HasCriticalBtnLevel());

            bool reversingDirectionByFar = (    
                                            0 > tempInputHolder.Dx * decodedInputHolder.Dx 
                                         || 0 > tempInputHolder.Dy * decodedInputHolder.Dy 
                                         || (0 != tempInputHolder.Dx && 0 == decodedInputHolder.Dx)
                                         || (0 != tempInputHolder.Dy && 0 == decodedInputHolder.Dy)
                                         || (0 == tempInputHolder.Dx && 0 != decodedInputHolder.Dx)
                                         || (0 == tempInputHolder.Dy && 0 != decodedInputHolder.Dy)
            );

            switch (currCharacterDownsync.GoalAsNpc) {
                case NpcGoal.NhuntThenIdle:
                case NpcGoal.NhuntThenPatrol:
                case NpcGoal.NhuntThenFollowAlly:
                    if (reversingDirectionByFar) {
                        shouldWaivePatrolCueReaction = false;
                    }  
                    break;
            }

            switch (thatCharacterInNextFrame.GoalAsNpc) {
                case NpcGoal.NhuntThenIdle:
                case NpcGoal.NhuntThenPatrol:
                case NpcGoal.NhuntThenFollowAlly:
                    if (reversingDirectionByFar) {
                        shouldWaivePatrolCueReaction = false;
                    }  
                    break;
            }

            switch (visionReactionByFar) {
                case TARGET_CH_REACTION_JUMP_TOWARDS_CH:
                case TARGET_CH_REACTION_FLEE_OPPO:
                    if (reversingDirectionByFar) {
                        shouldWaivePatrolCueReaction = false;
                    }
                    break;
            }

            if (shouldWaivePatrolCueReaction) {
                // [WARNING] It's difficult to move this "early return" block to be before the "shape overlap check" like that of the traps, because we need data from "decodedInputHolder". 
                // logger.LogInfo(String.Format("rdf.Id={0}, Npc joinIndex={1}, speciesId={2} is waived for patrolCueId={3} because it has (prevCapturedByPatrolCue={4}, framesInPatrolCue={5}, waivingPatrolCueId={6}).", currRenderFrame.Id, currCharacterDownsync.JoinIndex, currCharacterDownsync.SpeciesId, ptrlCue.Id, prevCapturedByPatrolCue, currCharacterDownsync.FramesInPatrolCue, currCharacterDownsync.WaivingPatrolCueId));
                return visionReactionByFar;
            }

            bool shouldEnterNewCapturedPeriod = (false == prevCapturedByPatrolCue); // [WARNING] "false == prevCapturedByPatrolCue" implies "false == shouldBreakCurrentPatrolCueCapture"

            if (0 >= targetFramesInPatrolCue) {
                targetFramesInPatrolCue = 1;
            }

            if (shouldEnterNewCapturedPeriod) {
                thatCharacterInNextFrame.CapturedByPatrolCue = true;
                thatCharacterInNextFrame.FramesInPatrolCue = targetFramesInPatrolCue;
                thatCharacterInNextFrame.WaivingPatrolCueId = ptrlCue.Id;
                tempInputHolder.cloneInto(decodedInputHolder);
                return TARGET_CH_REACTION_STOP_BY_PATROL_CUE_ENTER;
            }

            return visionReactionByFar;
        } 
        
        private static int deriveReactionForMvBlocker(int rdfId, int visionReactionByFar, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, bool currEffInAir, bool nextEffInAir, CharacterConfig chConfig, bool canJumpWithinInertia, bool isCharacterFlying, Collider aCollider, float aBoxLeft, float aBoxRight, float aBoxBottom, float aBoxTop, Collider? mvBlockerCollider, TrapColliderAttr? mvBlockerTpc, Collider? standingOnCollider, SatResult mvBlockerOverlapResult, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            //logger.LogInfo($"@rdfId={rdfId}, currCharacterDownsync.Id={currCharacterDownsync.Id}, canJumpWithinInertia={canJumpWithinInertia}, aBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop}) checking mvBlockerCollider={mvBlockerCollider.Shape.ToString(true)}");
            
            int effDxByFar = decodedInputHolder.Dx;
            if (0 == effDxByFar && !isCharacterFlying) {
                effDxByFar = currCharacterDownsync.DirX;
            }

            int effDyByFar = decodedInputHolder.Dy;
            bool temptingToMove = (NpcGoal.Npatrol == currCharacterDownsync.GoalAsNpc || NpcGoal.NhuntThenIdle == currCharacterDownsync.GoalAsNpc || NpcGoal.NhuntThenPatrol == currCharacterDownsync.GoalAsNpc || NpcGoal.NhuntThenFollowAlly == currCharacterDownsync.GoalAsNpc) && (canJumpWithinInertia || isCharacterFlying);

            bool canTurnaroundOrStopOrStartWalking = false;
            switch (visionReactionByFar) {
                case TARGET_CH_REACTION_UNKNOWN:
                    canTurnaroundOrStopOrStartWalking = (!isCharacterFlying && !currEffInAir && null != standingOnCollider);
                    break;
                default:
                    break;
            }

            var (effVelX, effVelY) = VirtualGridToPolygonColliderCtr(0 != currCharacterDownsync.VelX ? currCharacterDownsync.VelX : (0 < effDxByFar ? currCharacterDownsync.Speed : -currCharacterDownsync.Speed), 0 != currCharacterDownsync.VelY ? currCharacterDownsync.VelY : Math.Sign(effDyByFar)*currCharacterDownsync.Speed);
            switch (currCharacterDownsync.GoalAsNpc) {
                case NpcGoal.Nidle:
                    effVelX = 0;
                    effVelY = 0;
                    break;
                case NpcGoal.NidleIfGoHuntingThenPatrol:
                    if (NPC_FLEE_GRACE_PERIOD_RDF_CNT >= currCharacterDownsync.FramesInChState) {
                        /*
                         [WARNING] "NidleIfGoHuntingThenPatrol" is still tempting to patrol, so just give it a grace period before switching reaction to "TARGET_CH_REACTION_WALK_ALONG". Moreover, even given a temptation to resume patroling (i.e. 0 != effVelX), it'll still require passing "currBlockCanStillHoldMe" check to actually allow the transition, e.g. a Boar standing on the edge of a short hovering platform with "NidleIfGoHuntingThenPatrol" is unlikely to transit, but a SwordMan accidentally stopped at the beginning of a long platform is likely to transit.  
                        */
                        effVelX = 0;
                        effVelY = 0;
                    }
                    break;
                default:
                    break;
            }
            bool currBlockCanStillHoldMeIfWalkOn = !isCharacterFlying && (null != standingOnCollider) && ((0 < effVelX && standingOnCollider.X + standingOnCollider.W >= aBoxRight + effVelX) || (0 > effVelX && standingOnCollider.X <= aBoxLeft + effVelX));

            int newVisionReaction = visionReactionByFar;

            bool hasBlockerInXForward = false;
            bool hasBlockerInYForward = false;

            float mvBlockerColliderLeft = MAX_FLOAT32;
            float mvBlockerColliderRight = -MAX_FLOAT32;
            float mvBlockerColliderBottom = MAX_FLOAT32;
            float mvBlockerColliderTop = -MAX_FLOAT32;

            bool mvBlockerHoldableForRight = false;
            bool mvBlockerHoldableForLeft = false;
            bool mvBlockerStrictlyToTheRight = false;
            bool mvBlockerStrictlyToTheLeft = false;

            bool mvBlockerStrictlyDown = false;
            bool mvBlockerStrictlyUp = false;

            bool mvBlockerOverlapForRight = false;
            bool mvBlockerOverlapForLeft = false;
            bool mvBlockerOverlapForUp = false;
            bool mvBlockerOverlapForDown = false;

            if (null != mvBlockerCollider) {
                mvBlockerColliderLeft = mvBlockerCollider.X;
                mvBlockerColliderRight = mvBlockerCollider.X + mvBlockerCollider.W;
                mvBlockerColliderBottom = mvBlockerCollider.Y;
                mvBlockerColliderTop = mvBlockerCollider.Y + mvBlockerCollider.H;

                mvBlockerHoldableForRight = mvBlockerColliderRight > aBoxRight;
                mvBlockerOverlapForRight = (0 < mvBlockerOverlapResult.OverlapX || 0 < mvBlockerOverlapResult.SecondaryOverlapX);

                mvBlockerHoldableForLeft = mvBlockerColliderLeft < aBoxLeft;
                mvBlockerOverlapForLeft = (0 > mvBlockerOverlapResult.OverlapX || 0 > mvBlockerOverlapResult.SecondaryOverlapX);

                mvBlockerStrictlyToTheRight = (mvBlockerColliderLeft + STANDING_COLLIDER_CHECK_EPS >= aBoxRight) || (mvBlockerHoldableForRight && mvBlockerOverlapForRight);
                if (0 < effVelX) {
                    mvBlockerStrictlyToTheRight |= (mvBlockerColliderLeft + STANDING_COLLIDER_CHECK_EPS + effVelX >= aBoxRight);
                }
                mvBlockerStrictlyToTheLeft = (mvBlockerColliderRight <= aBoxLeft + STANDING_COLLIDER_CHECK_EPS) || (mvBlockerHoldableForLeft && mvBlockerOverlapForLeft);
                if (0 > effVelX) {
                    mvBlockerStrictlyToTheLeft |= (mvBlockerColliderRight + effVelX <= aBoxLeft + STANDING_COLLIDER_CHECK_EPS);
                }

                mvBlockerOverlapForUp = (0 < mvBlockerOverlapResult.OverlapY || 0 < mvBlockerOverlapResult.SecondaryOverlapY);
                mvBlockerOverlapForDown = (0 > mvBlockerOverlapResult.OverlapY || 0 > mvBlockerOverlapResult.SecondaryOverlapY);

                mvBlockerStrictlyDown = isCharacterFlying ? (mvBlockerColliderTop <= aBoxBottom + 3*STANDING_COLLIDER_CHECK_EPS) : (mvBlockerColliderTop <= aBoxBottom + STANDING_COLLIDER_CHECK_EPS);
                mvBlockerStrictlyUp = (mvBlockerColliderBottom >= aBoxTop || mvBlockerOverlapForUp); 

                hasBlockerInYForward = (0 < effDyByFar && 0 < currCharacterDownsync.VelY && mvBlockerStrictlyUp) || (0 > effDyByFar && 0 > currCharacterDownsync.VelY && mvBlockerStrictlyDown);
                hasBlockerInXForward = (0 < effVelX && (mvBlockerStrictlyToTheRight || (mvBlockerHoldableForRight && mvBlockerStrictlyDown))) || (0 > effVelX && (mvBlockerStrictlyToTheLeft || (mvBlockerHoldableForLeft && mvBlockerStrictlyDown)));

                hasBlockerInXForward &= (mvBlockerCollider != standingOnCollider) && (mvBlockerColliderBottom <= aBoxTop || mvBlockerColliderTop >= aBoxBottom + SNAP_INTO_PLATFORM_OVERLAP);
                hasBlockerInYForward &= (mvBlockerCollider != standingOnCollider);
            }

            if (!isCharacterFlying) {
                if (!canJumpWithinInertia) {
                    return visionReactionByFar;
                }
                if (hasBlockerInXForward) {
                    if (0 < effVelX * mvBlockerOverlapResult.OverlapX && 0 > mvBlockerOverlapResult.OverlapY) {
                        // Potentially a slope, by now "0 != mvBlockerOverlapResult.OverlapX" 
                        // [REMINDER] The direction (SatResult.OverlapX, SatResult.OverlapY) points perpendicularly into the slope, thus the slope value is (-OverlapX/OverlapY)!
                        bool notSteep = (0 < mvBlockerOverlapResult.OverlapX && mvBlockerOverlapResult.OverlapX <= -0.8f * mvBlockerOverlapResult.OverlapY)
                                        ||
                                        (0 > mvBlockerOverlapResult.OverlapX && (-mvBlockerOverlapResult.OverlapX) <= -0.8f * mvBlockerOverlapResult.OverlapY);
                        if (notSteep) {
                            newVisionReaction = TARGET_CH_REACTION_WALK_ALONG;
                            decodedInputHolder.Dx = effDxByFar;
                            // Deliberately NOT touching "BtnALevel" here
                            return newVisionReaction;
                        }

                        /*
                        if (SPECIES_GOBLIN == currCharacterDownsync.SpeciesId && 0 > currCharacterDownsync.VelX && !notSteep) {
                            logger.LogInfo($"@rdfId={rdfId}, currCharacterDownsync.Id={currCharacterDownsync.Id}, canJumpWithinInertia={canJumpWithinInertia}, aBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop}) checking slope={mvBlockerCollider.Shape.ToString(true)}, mvBlockerOverlapResult={mvBlockerOverlapResult.ToString()}");
                        }
                        */
                    }
                    float diffCxApprox = 0f;
                    if (0 < effDxByFar) {
                        diffCxApprox = (mvBlockerColliderLeft - aBoxRight);
                    } else if (0 > effDxByFar) {
                        diffCxApprox = (aBoxLeft - mvBlockerColliderRight);
                    }

                    if (mvBlockerColliderTop > aBoxBottom + SNAP_INTO_PLATFORM_OVERLAP) {
                        float diffCyApprox = (mvBlockerColliderTop - aBoxTop);
                        int jumpableDiffVirtualGridYApprox = (chConfig.JumpingInitVelY * chConfig.JumpingInitVelY) / (-GRAVITY_Y);
                        int jumpableDiffVirtualGridXBase = 0 <= diffCyApprox ? (chConfig.Speed * chConfig.JumpingInitVelY) / (-GRAVITY_Y) : (chConfig.Speed * (chConfig.JumpingInitVelY + (chConfig.JumpingInitVelY >> 1))) / (-GRAVITY_Y);
                        
                        int jumpableDiffVirtualGridXApprox = jumpableDiffVirtualGridXBase;
                        var (jumpableDiffCxApprox, jumpableDiffCyApprox) = VirtualGridToPolygonColliderCtr(jumpableDiffVirtualGridXApprox, jumpableDiffVirtualGridYApprox);
                        if (mvBlockerStrictlyUp) {
                            if (diffCxApprox < TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD) {
                                jumpableDiffCxApprox = 0;
                                jumpableDiffCyApprox = 0;
                            } else {
                                jumpableDiffCxApprox *= 0.5f;
                                jumpableDiffCyApprox *= 0.8f;
                            }
                        }
                        bool heightDiffJumpableApprox = (jumpableDiffCyApprox >= diffCyApprox);
                        bool widthDiffJumpableApprox = (jumpableDiffCxApprox >= diffCxApprox);
                        if (heightDiffJumpableApprox && widthDiffJumpableApprox) {
                            //logger.LogInfo($"\t@rdfId={rdfId}, jumping NPC to higher\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                            if (canTurnaroundOrStopOrStartWalking) {
                                newVisionReaction = TARGET_CH_REACTION_JUMP_TOWARDS_MV_BLOCKER;
                                decodedInputHolder.BtnALevel = 1;
                                decodedInputHolder.Dx = effDxByFar;
                                decodedInputHolder.Dy = 0;
                            } else {
                                newVisionReaction = TARGET_CH_REACTION_JUMP_TOWARDS_CH;
                                decodedInputHolder.BtnALevel = 1;
                                decodedInputHolder.Dx = effDxByFar;
                                decodedInputHolder.Dy = 0;
                            }
                        } else if (canTurnaroundOrStopOrStartWalking) {
                            if (null != mvBlockerTpc && mvBlockerTpc.ProvidesSlipJump) {
                                return visionReactionByFar;
                            } else if (0 < diffCyApprox && TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD > diffCxApprox) {
                                newVisionReaction = TARGET_CH_REACTION_TURNAROUND_MV_BLOCKER;
                                decodedInputHolder.Dx = -currCharacterDownsync.DirX;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnALevel = 0;
                            } else if (currBlockCanStillHoldMeIfWalkOn) {
                                newVisionReaction = TARGET_CH_REACTION_WALK_ALONG;
                                decodedInputHolder.Dx = effDxByFar;
                                decodedInputHolder.Dy = 0;
                                decodedInputHolder.BtnALevel = 0;
                            }
                        }
                    } else if (canTurnaroundOrStopOrStartWalking && currBlockCanStillHoldMeIfWalkOn) {
                        // [WARNING] Implies "0 != effVelX"
                        //logger.LogInfo($"\t@rdfId={rdfId}, walking along NPC\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, inAir:{currCharacterDownsync.InAir}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, inAir: {thatCharacterInNextFrame.InAir}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                        newVisionReaction = TARGET_CH_REACTION_WALK_ALONG;
                        decodedInputHolder.Dx = effDxByFar;
                        decodedInputHolder.Dy = 0;
                        decodedInputHolder.BtnALevel = 0;
                    } else {
                        // Otherwise "mvBlockerCollider" sits lower than "currCharacterDownsync" and "false == currBlockCanStillHoldMeIfWalkOn", a next but lower platform in the direction of movement, might still need jumping but the estimated dx movable by jumping is longer in this case
                        float diffCyApprox = (aBoxTop - mvBlockerColliderTop);
                        int jumpableDiffVirtualGridXBase = (chConfig.Speed * (chConfig.JumpingInitVelY << 2)) / (-GRAVITY_Y);
                        int jumpableDiffVirtualGridXApprox = jumpableDiffVirtualGridXBase + (jumpableDiffVirtualGridXBase >> 2);
                        var (jumpableDiffCxApprox, _) = VirtualGridToPolygonColliderCtr(jumpableDiffVirtualGridXApprox, 0);
                        bool widthDiffJumpableApprox = (jumpableDiffCxApprox >= diffCxApprox);
                        if (widthDiffJumpableApprox) {
                            //logger.LogInfo($"\t@rdfId={rdfId}, jumping NPC to lower\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                            newVisionReaction = (canTurnaroundOrStopOrStartWalking ? TARGET_CH_REACTION_JUMP_TOWARDS_MV_BLOCKER : TARGET_CH_REACTION_JUMP_TOWARDS_CH); // MvBlocker is found in the direction of hunting even if "false == canTurnaroundOrStopOrStartWalking", can jump anyway
                            decodedInputHolder.BtnALevel = 1;
                            decodedInputHolder.Dx = effDxByFar;
                            decodedInputHolder.Dy = 0;
                        } else if (canTurnaroundOrStopOrStartWalking) {                            
                            //logger.LogInfo($"\t@rdfId={rdfId}, stopping NPC\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, inAir:{currCharacterDownsync.InAir}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, inAir: {thatCharacterInNextFrame.InAir}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                            newVisionReaction = TARGET_CH_REACTION_STOP_BY_MV_BLOCKER;
                            decodedInputHolder.Dx = 0;
                            decodedInputHolder.Dy = 0;
                            decodedInputHolder.BtnALevel = 0;
                        }
                    }
                } else if (canTurnaroundOrStopOrStartWalking) {
                    // "false == hasBlockerInXForward", in this case blindly moving forward might fall into death
                    if (currBlockCanStillHoldMeIfWalkOn) {
                        newVisionReaction = TARGET_CH_REACTION_WALK_ALONG;
                        decodedInputHolder.Dx = effDxByFar;
                        decodedInputHolder.Dy = 0;
                        decodedInputHolder.BtnALevel = 0;
                    } else {
                        //logger.LogInfo($"\t@rdfId={rdfId}, stopping NPC\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, inAir:{currCharacterDownsync.InAir}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, inAir: {thatCharacterInNextFrame.InAir}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                        newVisionReaction = TARGET_CH_REACTION_STOP_BY_MV_BLOCKER;
                        decodedInputHolder.Dx = 0;
                        decodedInputHolder.Dy = 0;
                        decodedInputHolder.BtnALevel = 0;
                    }
                }
            } else {
                switch (currCharacterDownsync.GoalAsNpc) {
                    case NpcGoal.NhuntThenIdle:
                    case NpcGoal.NhuntThenPatrol:
                    case NpcGoal.NhuntThenFollowAlly:
                        if (0 == effDxByFar) {
                            effDxByFar = currCharacterDownsync.DirX;
                        }
                        if (0 == effDyByFar) {
                            effDyByFar = currCharacterDownsync.DirY;
                        }
                    break;
                }

                // A flying character
                hasBlockerInYForward = (0 < effDyByFar && mvBlockerStrictlyUp) || (0 > effDyByFar && mvBlockerStrictlyDown);
                hasBlockerInXForward = (0 < effDxByFar && mvBlockerStrictlyToTheRight) || (0 > effDxByFar && mvBlockerStrictlyToTheLeft);

                hasBlockerInYForward &= (null != mvBlockerCollider);
                hasBlockerInXForward &= (null != mvBlockerCollider);

                if (hasBlockerInXForward) {
                    float diffCxApprox = 0f;
                    if (0 < currCharacterDownsync.DirX) {
                        diffCxApprox = (mvBlockerColliderLeft - aBoxRight);
                    } else if (0 > currCharacterDownsync.DirX) {
                        diffCxApprox = (aBoxLeft - mvBlockerColliderRight);
                    }

                    if (TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD * TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD > (diffCxApprox * diffCxApprox)) {
                        newVisionReaction = TARGET_CH_REACTION_TURNAROUND_MV_BLOCKER;
                        decodedInputHolder.Dx = -currCharacterDownsync.DirX;
                        decodedInputHolder.BtnALevel = 0;
                    }
                }

                if (hasBlockerInYForward) {
                    float diffCyApprox = 0f;
                    if (0 < currCharacterDownsync.DirY) {
                        diffCyApprox = (mvBlockerColliderBottom - aBoxTop);
                    } else if (0 > currCharacterDownsync.DirY) {
                        diffCyApprox = (aBoxBottom - mvBlockerColliderTop);
                    }

                    if (TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD * TURNAROUND_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD > (diffCyApprox * diffCyApprox)) {
                        newVisionReaction = TARGET_CH_REACTION_TURNAROUND_MV_BLOCKER;
                        decodedInputHolder.Dy = -currCharacterDownsync.DirY;
                        decodedInputHolder.BtnALevel = 0;
                    }
                }
            }

            if (TARGET_CH_REACTION_UNKNOWN == newVisionReaction) {
                decodedInputHolder.Dx = effDxByFar;
                decodedInputHolder.Dy = effDyByFar;
            }

            return newVisionReaction; 
        }
    }
}
