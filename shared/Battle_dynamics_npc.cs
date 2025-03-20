using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        private static int TARGET_CH_REACTION_UNKNOWN = 0;
        private static int TARGET_CH_REACTION_USE_MELEE = 1;
        private static int TARGET_CH_REACTION_USE_DRAGONPUNCH = 2;
        private static int TARGET_CH_REACTION_USE_FIREBALL = 3;
        private static int TARGET_CH_REACTION_FOLLOW = 4;
        private static int TARGET_CH_REACTION_USE_SLOT_C = 5;
        private static int TARGET_CH_REACTION_NOT_ENOUGH_MP = 6;
        private static int TARGET_CH_REACTION_FLEE = 7;
        private static int TARGET_CH_REACTION_DEF1 = 8;
        private static int TARGET_CH_REACTION_STOP = 9;

        private static int TARGET_CH_REACTION_JUMP_TOWARDS = 10;
        //private static int TARGET_CH_REACTION_USE_DASHING = 11;
        private static int TARGET_CH_REACTION_SLIP_JUMP = 12;
        private static int TARGET_CH_REACTION_WALK_ALONG = 13;

        private static int NPC_DEF1_MIN_HOLDING_RDF_CNT = 90;

        private static int VISION_SEARCH_RDF_RANDOMIZE_MASK = (1 << 4) + (1 << 3) + (1 << 1);

        private static int OPPO_DX_OFFSET_MOD_MINUS_1 = (1 << 3) - 1;
        private static float OPPO_DX_OFFSET = 10.0f; // In collision space
    
        private static float FLEE_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD = 32.0f;

        private static void _handleAllyCh(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool notDashing, bool canJumpWithinInertia, CharacterDownsync v5, CharacterConfig allyConfig, Collider? allyChCollider, float allyChColliderDx, float allyChColliderDy, float allyBoxLeft, float allyBoxRight, float allyBoxBottom, float allyBoxTop, ref int patternId, ref bool jumpedOrNot, ref bool slipJumpedOrNot, ref int jumpHoldingRdfCnt, ref int effectiveDx, ref int effectiveDy, ref int visionReaction) {
            if (null == allyChCollider || null == v5) return;

            float absColliderDx = Math.Abs(allyChColliderDx), absColliderDy = Math.Abs(allyChColliderDy);

            bool allyBehindMe = (0 > (allyChColliderDx * currCharacterDownsync.DirX));
            if (!allyBehindMe) {
                // Ally is in front of me
                int s0 = TARGET_CH_REACTION_UNKNOWN, s1 = TARGET_CH_REACTION_UNKNOWN, s2 = TARGET_CH_REACTION_UNKNOWN, s3 = TARGET_CH_REACTION_UNKNOWN;

                s0 = frontAllyReachableByIvSlot(currCharacterDownsync, effInAir, chConfig, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, absColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                if (TARGET_CH_REACTION_USE_SLOT_C == s0) {
                    patternId = PATTERN_INVENTORY_SLOT_C;
                    visionReaction = s0;
                } else {
                    s1 = frontAllyReachableByDragonPunch(currCharacterDownsync, effInAir, chConfig, canJumpWithinInertia, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, absColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                    visionReaction = s1;
                    if (TARGET_CH_REACTION_USE_DRAGONPUNCH == s1) {
                        patternId = PATTERN_UP_B;
                    } else {
                        s2 = frontAllyReachableByMelee1(currCharacterDownsync, effInAir, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, absColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop);
                        visionReaction = s2;
                        if (TARGET_CH_REACTION_USE_MELEE == s2) {
                            patternId = PATTERN_B;
                        } else {
                            s3 = frontAllyReachableByFireball(currCharacterDownsync, chConfig, aCollider, v5, allyConfig, allyChCollider, allyChColliderDx, allyChColliderDy, absColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop);
                            visionReaction = s3;
                            if (TARGET_CH_REACTION_USE_FIREBALL == s3) {
                                patternId = PATTERN_DOWN_B;
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
                shouldJumpTowardsTarget |= (allyAboveMe && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState) && effInAir && chConfig.JumpHoldingToFly);
                bool shouldSlipJumpTowardsTarget = (canJumpWithinInertia && !effInAir && 0 > allyChColliderDy && currCharacterDownsync.PrimarilyOnSlippableHardPushback);
                shouldSlipJumpTowardsTarget = (!chConfig.OmitGravity && chConfig.JumpHoldingToFly && currCharacterDownsync.OmitGravity && !allyAboveMe && !allyBehindMe);
                if (0 >= chConfig.JumpingInitVelY) {
                    shouldJumpTowardsTarget = false;
                    shouldSlipJumpTowardsTarget = false;
                } else if (chConfig.HasDef1) {
                    shouldJumpTowardsTarget = false;
                }
                if (shouldSlipJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_SLIP_JUMP;
                } else if (shouldJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_JUMP_TOWARDS;
                }
            }

            if (TARGET_CH_REACTION_JUMP_TOWARDS == visionReaction) {
                jumpedOrNot = true;
                if (0 == jumpHoldingRdfCnt) {
                    jumpHoldingRdfCnt = 1;
                }
                if (0 < currCharacterDownsync.JumpHoldingRdfCnt && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                    // [warning] only proactive jumping support jump holding.
                    jumpHoldingRdfCnt = currCharacterDownsync.JumpHoldingRdfCnt + 1;
                    patternId = PATTERN_HOLD_B;
                    if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= jumpHoldingRdfCnt) {
                        jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_2;
                    } else if (!chConfig.JumpHoldingToFly && JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= jumpHoldingRdfCnt) {
                        jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_1;
                    }
                }
                if (0 != allyChColliderDx) {
                    effectiveDx = (0 < allyChColliderDx ? +2 : -2);
                }
            } else if (TARGET_CH_REACTION_SLIP_JUMP == visionReaction) {
                jumpedOrNot = false;
                jumpHoldingRdfCnt = 0;
                slipJumpedOrNot = true;
            } else if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                if (currCharacterDownsync.OmitGravity || chConfig.OmitGravity) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = allyChColliderDx * allyChColliderDx + allyChColliderDy * allyChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = allyChColliderDx * invMag, normY = allyChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY, mustHaveNonZeroX: true);
                        effectiveDx = effDx;
                        effectiveDy = effDy;
                    }
                } else {
                    if (allyBehindMe) {
                        if (0 >= currCharacterDownsync.FramesToRecover) {
                            effectiveDx = -effectiveDx;
                        }
                    } else {
                        float visionThresholdPortion = 0.95f;
                        bool veryFarAway = (0 < allyChColliderDx ? (allyChCollider.X > (visionCollider.X + visionThresholdPortion*visionCollider.W)) : (allyChCollider.X < visionCollider.X + (1-visionThresholdPortion)*visionCollider.W));
                        if (notDashing && veryFarAway) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                patternId = PATTERN_FRONT_E;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                patternId = PATTERN_FRONT_E;
                            }
                        }
                    }
                }
            }
        }

        private static void _handleOppoCh(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool notDashing, bool canJumpWithinInertia, Collider? oppoChCollider, CharacterDownsync? v3, float oppoChColliderDx, float oppoChColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop, ref int patternId, ref bool jumpedOrNot, ref bool slipJumpedOrNot, ref int jumpHoldingRdfCnt, ref int effectiveDx, ref int effectiveDy, ref int visionReaction) {
            if (null == oppoChCollider || null == v3) {
                switch (currCharacterDownsync.GoalAsNpc) {
                    case NpcGoal.NhuntThenIdle:
                        thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Nidle;
                        break;
                    case NpcGoal.NhuntThenPatrol:
                    case NpcGoal.NidleIfGoHuntingThenPatrol:
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

            float absColliderDx = Math.Abs(oppoChColliderDx), absColliderDy = Math.Abs(oppoChColliderDy);

            bool opponentBehindMe = (0 > (oppoChColliderDx * currCharacterDownsync.DirX));
            if (!opponentBehindMe) {
                // Opponent is in front of me
                int s0 = TARGET_CH_REACTION_UNKNOWN, s1 = TARGET_CH_REACTION_UNKNOWN, s2 = TARGET_CH_REACTION_UNKNOWN, s3 = TARGET_CH_REACTION_UNKNOWN;

                s0 = frontOpponentReachableByIvSlot(currCharacterDownsync, effInAir, chConfig, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                if (TARGET_CH_REACTION_USE_SLOT_C == s0) {
                    patternId = PATTERN_INVENTORY_SLOT_C;
                    visionReaction = s0;
                } else {
                    s1 = frontOpponentReachableByDragonPunch(currCharacterDownsync, effInAir, chConfig, canJumpWithinInertia, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, false); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                    visionReaction = s1;
                    if (TARGET_CH_REACTION_USE_DRAGONPUNCH == s1) {
                        patternId = PATTERN_UP_B;
                    } else {
                        s2 = frontOpponentReachableByMelee1(currCharacterDownsync, effInAir, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop);
                        visionReaction = s2;
                        if (TARGET_CH_REACTION_USE_MELEE == s2) {
                            patternId = PATTERN_B;
                        } else {
                            s3 = frontOpponentReachableByFireball(currCharacterDownsync, chConfig, aCollider, oppoChCollider, oppoChColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop);
                            visionReaction = s3;
                            if (TARGET_CH_REACTION_USE_FIREBALL == s3) {
                                patternId = PATTERN_DOWN_B;
                            } else {
                                visionReaction = TARGET_CH_REACTION_FOLLOW;
                            }
                        }
                    }
                }

                if (TARGET_CH_REACTION_NOT_ENOUGH_MP == s0 && TARGET_CH_REACTION_NOT_ENOUGH_MP == s1 && TARGET_CH_REACTION_NOT_ENOUGH_MP == s2 && TARGET_CH_REACTION_NOT_ENOUGH_MP == s3 && !chConfig.IsKeyCh) {
                    visionReaction = TARGET_CH_REACTION_FLEE;
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
                        visionReaction = TARGET_CH_REACTION_FLEE;
                    }
                }
            }
            
            if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                bool opponentAboveMe = 0 < oppoChColliderDy && (oppoChCollider.H < (1.67f*oppoChColliderDy+aCollider.H)); // i.e. "0.6f * (oppoChCollider.H - aCollider.H) < oppoChColliderDy"
                bool shouldJumpTowardsTarget = (canJumpWithinInertia && !effInAir && opponentAboveMe && (0 <= currCharacterDownsync.DirX * oppoChColliderDx));
                shouldJumpTowardsTarget |= (opponentAboveMe && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState) && effInAir && chConfig.JumpHoldingToFly);
                bool shouldSlipJumpTowardsTarget = (canJumpWithinInertia && !effInAir && 0 > oppoChColliderDy && currCharacterDownsync.PrimarilyOnSlippableHardPushback);
                shouldSlipJumpTowardsTarget = (!chConfig.OmitGravity && chConfig.JumpHoldingToFly && currCharacterDownsync.OmitGravity && !opponentAboveMe && !opponentBehindMe);
                if (0 >= chConfig.JumpingInitVelY) {
                    shouldJumpTowardsTarget = false;
                    shouldSlipJumpTowardsTarget = false;
                } else if (chConfig.HasDef1 || chConfig.NpcNotHuntingInAirOppoCh) {
                    if (shouldJumpTowardsTarget) {
                        shouldJumpTowardsTarget = false;
                        visionReaction = TARGET_CH_REACTION_UNKNOWN; // [WARNING] Leave decision to "deriveReactionForMvBlocker"!
                    }
                }

                if (shouldSlipJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_SLIP_JUMP;
                } else if (shouldJumpTowardsTarget) {
                    visionReaction = TARGET_CH_REACTION_JUMP_TOWARDS;
                }
            }

            if (TARGET_CH_REACTION_JUMP_TOWARDS == visionReaction) {
                jumpedOrNot = true;
                if (0 == jumpHoldingRdfCnt) {
                    jumpHoldingRdfCnt = 1;
                }
                if (0 < currCharacterDownsync.JumpHoldingRdfCnt && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                    // [warning] only proactive jumping support jump holding.
                    jumpHoldingRdfCnt = currCharacterDownsync.JumpHoldingRdfCnt + 1;
                    patternId = PATTERN_HOLD_B;
                    if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= jumpHoldingRdfCnt) {
                        jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_2;
                    } else if (!chConfig.JumpHoldingToFly && JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= jumpHoldingRdfCnt) {
                        jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_1;
                    }
                }
                if (0 != oppoChColliderDx) {
                    effectiveDx = (0 < oppoChColliderDx ? +2 : -2);
                }
            } else if (TARGET_CH_REACTION_SLIP_JUMP == visionReaction) {
                jumpedOrNot = false;
                jumpHoldingRdfCnt = 0;
                slipJumpedOrNot = true;
            } else if (TARGET_CH_REACTION_FOLLOW == visionReaction) {
                if (currCharacterDownsync.OmitGravity || chConfig.OmitGravity) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = oppoChColliderDx * oppoChColliderDx + oppoChColliderDy * oppoChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = oppoChColliderDx * invMag, normY = oppoChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY, mustHaveNonZeroX: true);
                        effectiveDx = effDx;
                        effectiveDy = effDy;
                    }
                } else {
                    if (opponentBehindMe) {
                        if (0 >= currCharacterDownsync.FramesToRecover) {
                            effectiveDx = -effectiveDx;
                        }
                    } else {
                        float visionThresholdPortion = 0.95f;
                        bool veryFarAway = (0 < oppoChColliderDx ? (oppoChCollider.X > (visionCollider.X + visionThresholdPortion*visionCollider.W)) : (oppoChCollider.X < visionCollider.X + (1-visionThresholdPortion)*visionCollider.W));
                        if (notDashing && veryFarAway) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                patternId = PATTERN_FRONT_E;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                patternId = PATTERN_FRONT_E;
                            }
                        }
                    }
                }
            } else if (TARGET_CH_REACTION_FLEE == visionReaction) {
                if (SPECIES_ANGEL == currCharacterDownsync.SpeciesId) {
                    if (opponentBehindMe) {
                        if (notDashing && currCharacterDownsync.Mp >= AngelDashing.MpDelta) {
                            if (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota) {
                                patternId = PATTERN_FRONT_E;
                            }
                        }
                    } else {
                        bool notBackDashing = (BackDashing != currCharacterDownsync.CharacterState);
                        if (notBackDashing && currCharacterDownsync.Mp >= AngelBackDashing.MpDelta) {
                            patternId = PATTERN_E;
                        }
                    }
                } if (currCharacterDownsync.OmitGravity || chConfig.OmitGravity) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = oppoChColliderDx * oppoChColliderDx + oppoChColliderDy * oppoChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = -oppoChColliderDx * invMag, normY = -oppoChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY);
                        effectiveDx = effDx;
                        effectiveDy = effDy;
                    }
                } else {
                    bool isBackDashingSpecies = (SPECIES_WITCHGIRL == currCharacterDownsync.SpeciesId || SPECIES_BRIGHTWITCH == currCharacterDownsync.SpeciesId || SPECIES_BOUNTYHUNTER == currCharacterDownsync.SpeciesId);
                    bool notBackDashingSpecies = !(SPECIES_WITCHGIRL == currCharacterDownsync.SpeciesId || SPECIES_BRIGHTWITCH == currCharacterDownsync.SpeciesId || SPECIES_BOUNTYHUNTER == currCharacterDownsync.SpeciesId);
                    if (opponentBehindMe) {
                        // DO NOTHING, just continue walking
                        if (notDashing && notBackDashingSpecies) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                patternId = PATTERN_FRONT_E;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                patternId = PATTERN_FRONT_E;
                            }
                        }
                    } else {
                        bool notBackDashing = (BackDashing != currCharacterDownsync.CharacterState);
                        if (!effInAir && notBackDashing && isBackDashingSpecies) {
                            patternId = PATTERN_E;
                        }
                    }
                }
            }

            if (TARGET_CH_REACTION_UNKNOWN != visionReaction) {
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
                    case NpcGoal.NidleIfGoHuntingThenPatrol:
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

        private static void _handleOppoBl(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool notDashing, bool canJumpWithinInertia, Collider? oppoChCollider, CharacterDownsync? v3, float oppoChColliderDx, float oppoChColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop, Collider? oppoBlCollider, Bullet? v4, ref int patternId, ref bool jumpedOrNot, ref bool slipJumpedOrNot, ref int jumpHoldingRdfCnt, ref int effectiveDx, ref int effectiveDy, ref int visionReaction) {
            if (null == oppoBlCollider || null == v4) return;
            float oppoBlColliderDx = oppoBlCollider.X - aCollider.X;
            bool blBehindMe = (0 > (oppoBlColliderDx * currCharacterDownsync.DirX));
            if (chConfig.HasDef1 && !effInAir && !blBehindMe) {
                if (chConfig.WalkingAutoDef1 && Walking == currCharacterDownsync.CharacterState) {
                } else {
                    effectiveDx = 0;
                    effectiveDy = +2;
                    jumpedOrNot = false;
                    slipJumpedOrNot = false;
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
                    effectiveDx = (blBehindMe ? -currCharacterDownsync.DirX : currCharacterDownsync.DirX); // turn-around to counter, regardless of being able to counter or not
                    if (!blBehindMe && effInAir && chConfig.NpcNoDefaultAirWalking) {
                        effectiveDx = 0;
                    }
                    effectiveDy = 0;
                    slipJumpedOrNot = false;
                    if (SPECIES_RIDERGUARD_RED != chConfig.SpeciesId && SPECIES_DEMON_FIRE_SLIME != chConfig.SpeciesId && !chConfig.HasDef1 && 0 < chConfig.JumpingInitVelY) {
                        visionReaction = TARGET_CH_REACTION_JUMP_TOWARDS;
                        if (canJumpWithinInertia && !effInAir) {
                            jumpedOrNot = true;
                        }
                        if (0 == jumpHoldingRdfCnt) {
                            jumpHoldingRdfCnt = 1;
                        }
                        if (0 < currCharacterDownsync.JumpHoldingRdfCnt && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                            // [WARNING] Only proactive jumping support jump holding.
                            jumpHoldingRdfCnt = currCharacterDownsync.JumpHoldingRdfCnt + 1;
                            patternId = PATTERN_HOLD_B;
                            if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= jumpHoldingRdfCnt) {
                                jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_2;
                            } else if (!chConfig.JumpHoldingToFly && JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= jumpHoldingRdfCnt) {
                                jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_1;
                            }
                        }
                        /*
                        if (SPECIES_SKELEARCHER == currCharacterDownsync.SpeciesId) {
                            logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, chState={2}, framesInChState={3}, effectiveDx={4}, effectiveDy={5}, jumpedOrNot={6}, framesToStartJump={7} to face fireball", rdfId, currCharacterDownsync.Id, currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, effectiveDx, effectiveDy, jumpedOrNot, currCharacterDownsync.FramesToStartJump));
                        }
                        */
                    } else if (!effInAir && canJumpWithinInertia && chConfig.HasDef1) {
                        effectiveDx = 0;
                        effectiveDy = +2;
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
                        patternId = PATTERN_UP_B;
                    } else if (!effInAir && canJumpWithinInertia && chConfig.HasDef1) {
                        effectiveDx = 0;
                        effectiveDy = +2;
                        visionReaction = TARGET_CH_REACTION_DEF1;
                    } else {
                        // Just don't jump if it's melee incoming bullet
                        if (notDashing) {
                            // Because dashing often has a few invinsible startup frames.
                            if (chConfig.SlidingEnabled && !effInAir) {
                                patternId = PATTERN_E;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                patternId = PATTERN_E;
                            }
                            visionReaction = TARGET_CH_REACTION_FOLLOW;
                        }
                    }
                }
            }

            if (TARGET_CH_REACTION_UNKNOWN == visionReaction) {
                if (canJumpWithinInertia) {
                    if (!effInAir && currCharacterDownsync.PrimarilyOnSlippableHardPushback) {
                        jumpedOrNot = false;
                        slipJumpedOrNot = true;
                        jumpHoldingRdfCnt = 0;
                        visionReaction = TARGET_CH_REACTION_SLIP_JUMP;
                    }
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
                    if (0 >= targetSlot.Quota) return TARGET_CH_REACTION_FLEE;

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
                        return TARGET_CH_REACTION_FLEE;
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
                case SPECIES_SWORDMAN_BOSS:
                case SPECIES_SWORDMAN:
                    if (currCharacterDownsync.Mp < SwordManDragonPunchPrimerSkill.MpDelta) {
                        bool closeEnoughAlt = canJumpWithinInertia && !effInAir && (0.6f * aCollider.H < colliderDy) && (0 <= currCharacterDownsync.DirX * colliderDx);
                        if (closeEnoughAlt) {
                            if (forBlHandling) {
                                return TARGET_CH_REACTION_DEF1;
                            } else {
                                return TARGET_CH_REACTION_JUMP_TOWARDS;
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
                                return TARGET_CH_REACTION_JUMP_TOWARDS;
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
                        return TARGET_CH_REACTION_JUMP_TOWARDS;
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
                        return TARGET_CH_REACTION_FLEE;
                    }
                case SPECIES_DARKBEAMTOWER:
                    if (currCharacterDownsync.Mp < DarkTowerPrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    return TARGET_CH_REACTION_USE_MELEE;
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Mp < StoneSwordSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;
                    closeEnough = (0 < colliderDy && absColliderDy < 1.2f * aCollider.H) && (absColliderDx < 5.0f * aCollider.W); // A special case
                    if (closeEnough) {
                        return TARGET_CH_REACTION_USE_MELEE;
                    } else {
                        return TARGET_CH_REACTION_FLEE;
                    }
                case SPECIES_BAT:
                case SPECIES_FIREBAT:
                    if (currCharacterDownsync.Mp < BatMelee1PrimerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BatMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BatMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BatMelee1PrimerBullet.HitboxSizeX >> 1), (BatMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_RIDLEYDRAKE:
                    if (effInAir && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return TARGET_CH_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * RidleyMeleeBulletHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + RidleyMeleeBulletHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((RidleyMeleeBulletHit1.HitboxSizeX >> 1), (RidleyMeleeBulletHit1.HitboxSizeY >> 1));
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

        private static int frontOpponentReachableByFireball(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider bCollider, float colliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            // Whenever there's an opponent in vision, it's deemed already close enough for fireball
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            switch (currCharacterDownsync.SpeciesId) {
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
                        return TARGET_CH_REACTION_FLEE;
                    }
                case SPECIES_DARKBEAMTOWER:
                    if (notRecovered) return TARGET_CH_REACTION_UNKNOWN;
                    if (currCharacterDownsync.Mp < DarkTowerLowerSkill.MpDelta) return TARGET_CH_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 > colliderDy && absColliderDy > 0.8f * aCollider.H); // A special case
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

        private static (int, bool, bool, int, int, int, bool) deriveNpcOpPattern(CharacterDownsync currCharacterDownsync, bool effInAir, bool notDashing, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, Collision collision, ref SatResult overlapResult, ref SatResult primaryOverlapResult, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            if (noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0, 0, false);
            }

            bool interrupted = _processDebuffDuringInput(currCharacterDownsync);
            if (interrupted) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0, 0, false);
            }

            if (Def1 == currCharacterDownsync.CharacterState && NPC_DEF1_MIN_HOLDING_RDF_CNT > currCharacterDownsync.FramesInChState) {
                // Such that Def1 is more visible
                return (PATTERN_ID_NO_OP, false, false, 0, 0, +2, false);
            }
            int rdfId = currRenderFrame.Id;
            int patternId = PATTERN_ID_NO_OP;
            bool jumpedOrNot = false;
            bool slipJumpedOrNot = false;
            var jumpHoldingRdfCnt = currCharacterDownsync.JumpHoldingRdfCnt;
            if (chConfig.JumpHoldingToFly) {
                jumpHoldingRdfCnt = (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= currCharacterDownsync.JumpHoldingRdfCnt ? JUMP_HOLDING_RDF_CNT_THRESHOLD_2 : 0);
            } else {
                jumpHoldingRdfCnt = (JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= currCharacterDownsync.JumpHoldingRdfCnt ? JUMP_HOLDING_RDF_CNT_THRESHOLD_1 : 0);
            }
            bool slowDownToAvoidOverlap = false;

            // By default keeps the movement aligned with current facing
            int effectiveDx = currCharacterDownsync.DirX;
            int effectiveDy = currCharacterDownsync.DirY;
            int visionReaction = TARGET_CH_REACTION_UNKNOWN;
            var aCollider = dynamicRectangleColliders[currCharacterDownsync.JoinIndex - 1]; // already added to collisionSys

            bool hasCancellableCombo = false;
            if (!nonAttackingSet.Contains(currCharacterDownsync.CharacterState)) {
                var (skillConfig, activeBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                if (null != activeBulletConfig && 0 <= activeBulletConfig.CancellableStFrame && 0 < activeBulletConfig.CancellableEdFrame) {
                    hasCancellableCombo = (currCharacterDownsync.FramesInChState == ((activeBulletConfig.CancellableStFrame + activeBulletConfig.CancellableEdFrame) >> 1));
                }
            }
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

            bool canJumpWithinInertia = (0 == currCharacterDownsync.FramesToRecover && ((chConfig.InertiaFramesToRecover >> 1) > currCharacterDownsync.FramesCapturedByInertia));
            if (
                (isInJumpStartup(thatCharacterInNextFrame, chConfig) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig))
            ) {
                canJumpWithinInertia = false;
            }

            Collider? oppoChCollider = null;
            CharacterDownsync? v3 = null;

            Collider? oppoBlCollider = null;
            Bullet? v4 = null;

            Collider? allyChCollider = null;
            CharacterDownsync? v5 = null;

            Collider? mvBlockerCollider = null;
            TrapColliderAttr? v6 = null;

            Collider? standingOnCollider = null;
            bool shouldCheckVisionCollision = (0 < chConfig.VisionSizeX && 0 < chConfig.VisionSizeY && (visionSearchTickWhenNonAtk || hasCancellableCombo));
            if (chConfig.AntiGravityWhenIdle && currCharacterDownsync.InAir && InAirIdle1NoJump == currCharacterDownsync.CharacterState) {
                shouldCheckVisionCollision = false;
            }
            if (shouldCheckVisionCollision) {
                float visionCx, visionCy, visionCw, visionCh;
                calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);

                var visionCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(visionCollider, visionCx, visionCy, visionCw, visionCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync, COLLISION_VISION_INDEX_PREFIX);
                collisionSys.AddSingleToCellTail(visionCollider);

                findHorizontallyClosestCharacterCollider(rdfId, currRenderFrame, currCharacterDownsync, chConfig, visionCollider, aCollider, collision, ref overlapResult, ref primaryOverlapResult, out oppoChCollider, out v3, out oppoBlCollider, out v4, out allyChCollider, out v5, out mvBlockerCollider, out v6, out standingOnCollider, logger);

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
                    _handleOppoBl(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                    if (TARGET_CH_REACTION_UNKNOWN == visionReaction) {
                        if (chConfig.NpcPrioritizeAllyHealing && null != v5) {
                            var allyChConfig = characters[v5.SpeciesId];
                            _handleAllyCh(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, v5, allyChConfig, allyChCollider, allyChColliderDx, allyChColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                            if (TARGET_CH_REACTION_UNKNOWN == visionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == visionReaction) {
                                _handleOppoCh(currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                            }
                        } else {
                            _handleOppoCh(currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                        }
                    }
                } else if (chConfig.NpcPrioritizeAllyHealing && null != v5) {
                    var allyChConfig = characters[v5.SpeciesId];
                    _handleAllyCh(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, v5, allyChConfig, allyChCollider, allyChColliderDx, allyChColliderDy, allyBoxLeft, allyBoxRight, allyBoxBottom, allyBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                    if (TARGET_CH_REACTION_UNKNOWN == visionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == visionReaction) {
                        if (chConfig.NpcPrioritizeBulletHandling) {
                            _handleOppoBl(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                            if (TARGET_CH_REACTION_UNKNOWN == visionReaction) {
                                _handleOppoCh(currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                            }
                        } else {
                            _handleOppoCh(currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                            if (TARGET_CH_REACTION_UNKNOWN == visionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == visionReaction) {
                                _handleOppoBl(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                            }
                        }
                    }
                } else {
                    _handleOppoCh(currCharacterDownsync, thatCharacterInNextFrame, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                    if (TARGET_CH_REACTION_UNKNOWN == visionReaction || TARGET_CH_REACTION_NOT_ENOUGH_MP == visionReaction) {
                        _handleOppoBl(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, notDashing, canJumpWithinInertia, oppoChCollider, v3, oppoChColliderDx, oppoChColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop, oppoBlCollider, v4, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                    }
                }

                visionCollider.Data = null;
            }

            if (TARGET_CH_REACTION_UNKNOWN != visionReaction && TARGET_CH_REACTION_FLEE != visionReaction && TARGET_CH_REACTION_FOLLOW != visionReaction && PATTERN_ID_NO_OP != patternId) {
                // [WARNING] Even if there were no vision reaction, if "PATTERN_ID_NO_OP == patternId", we still expect the NPC to make use of patrol cues to jump or turn around!
                if (!jumpedOrNot) {
                    jumpHoldingRdfCnt = 0;
                }
                thatCharacterInNextFrame.CachedCueCmd = 0; // visionReaction overrides cue cmd
                thatCharacterInNextFrame.CapturedByPatrolCue = false;
                thatCharacterInNextFrame.FramesInPatrolCue = 0;
                thatCharacterInNextFrame.WaivingPatrolCueId = NO_PATROL_CUE_ID;
            } else if (!noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                bool hasPatrolCueReaction = false;
                bool prevCapturedByPatrolCue = currCharacterDownsync.CapturedByPatrolCue;
                bool shouldBreakCurrentPatrolCueCapture = (true == prevCapturedByPatrolCue && NO_PATROL_CUE_ID != currCharacterDownsync.WaivingPatrolCueId && 0 == currCharacterDownsync.FramesInPatrolCue);
                bool isReallyCaptured = (true == prevCapturedByPatrolCue && NO_PATROL_CUE_ID != currCharacterDownsync.WaivingPatrolCueId && 0 < currCharacterDownsync.FramesInPatrolCue);
                if (isReallyCaptured) {
                    effectiveDx = 0;
                    effectiveDy = 0;
                    jumpedOrNot = false;
                    jumpHoldingRdfCnt = 0;
                    slipJumpedOrNot = false;
                    hasPatrolCueReaction = true;
                } else if (shouldBreakCurrentPatrolCueCapture) {  
                    thatCharacterInNextFrame.CachedCueCmd = 0;
                    thatCharacterInNextFrame.CapturedByPatrolCue = false;
                    thatCharacterInNextFrame.FramesInPatrolCue = DEFAULT_PATROL_CUE_WAIVING_FRAMES; // re-purposed
                    if (0 != currCharacterDownsync.CachedCueCmd) {
                        DecodeInput(currCharacterDownsync.CachedCueCmd, decodedInputHolder);
                        effectiveDx = decodedInputHolder.Dx;
                        effectiveDy = decodedInputHolder.Dy;
                        slipJumpedOrNot = (0 == currCharacterDownsync.FramesToRecover) && ((currCharacterDownsync.PrimarilyOnSlippableHardPushback || (effInAir && currCharacterDownsync.OmitGravity && !chConfig.OmitGravity)) && 0 > decodedInputHolder.Dy && 0 == decodedInputHolder.Dx) && (0 < decodedInputHolder.BtnALevel);
                        jumpedOrNot = !slipJumpedOrNot && (0 == currCharacterDownsync.FramesToRecover) && !effInAir && (0 < decodedInputHolder.BtnALevel);

                        if (0 >= chConfig.JumpingInitVelY) {
                            slipJumpedOrNot = false;
                            jumpHoldingRdfCnt = 0;
                            jumpedOrNot = false;
                        }
                        jumpHoldingRdfCnt = 0;
                        hasPatrolCueReaction = true;
                    }
                } else if (shouldCheckVisionCollision) {
                    // [WARNING] The field "CharacterDownsync.FramesInPatrolCue" would also be re-purposed as "patrol cue collision waiving frames" by the logic here.
                    Collider? pCollider;
                    PatrolCue? ptrlCue;
                    decodedInputHolder.Reset();
                    findHorizontallyClosestPatrolCueCollider(currCharacterDownsync, aCollider, collision, ref overlapResult, out pCollider, out ptrlCue, logger);

                    if (null != pCollider && null != ptrlCue) {
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
                            DecodeInput(ptrlCue.FrAct, decodedInputHolder);
                            toCacheCmd = ptrlCue.FrAct;
                            //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the right", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue)); 
                        } else if (fl && 0 != ptrlCue.FlAct) {
                            targetFramesInPatrolCue = ptrlCue.FlCaptureFrames;
                            DecodeInput(ptrlCue.FlAct, decodedInputHolder);
                            toCacheCmd = ptrlCue.FlAct;
                            //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the left", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue));
                        } else if (fu && 0 != ptrlCue.FuAct) {
                            targetFramesInPatrolCue = ptrlCue.FuCaptureFrames;
                            DecodeInput(ptrlCue.FuAct, decodedInputHolder);
                            toCacheCmd = ptrlCue.FuAct;
                            //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the top", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue)); 
                        } else if (fd && 0 != ptrlCue.FdAct) {
                            targetFramesInPatrolCue = ptrlCue.FdCaptureFrames;
                            DecodeInput(ptrlCue.FdAct, decodedInputHolder);
                            toCacheCmd = ptrlCue.FdAct;
                            //logger.LogInfo(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} from the bottom", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue)); 
                        } else {
                            //logger.LogWarn(String.Format("aCollider={{ X:{0}, Y:{1}, W:{2}, H:{3}, dirX: {9} }} collided with pCollider={{ X:{4}, Y:{5}, W:{6}, H:{7}, cue={8} }} but direction couldn't be determined!", aCollider.X, aCollider.Y, aCollider.W, aCollider.H, pCollider.X, pCollider.Y, pCollider.W, pCollider.H, ptrlCue, currCharacterDownsync.DirX));
                        }

                        bool shouldWaivePatrolCueReaction = (false == prevCapturedByPatrolCue && 0 < currCharacterDownsync.FramesInPatrolCue && ptrlCue.Id == currCharacterDownsync.WaivingPatrolCueId && (0 == decodedInputHolder.BtnALevel && 0 == decodedInputHolder.BtnBLevel)); // Don't waive if the cue contains an action button (i.e. BtnA or BtnB)

                        do {
                            if (shouldWaivePatrolCueReaction) {
                                // [WARNING] It's difficult to move this "early return" block to be before the "shape overlap check" like that of the traps, because we need data from "decodedInputHolder". 
                                // logger.LogInfo(String.Format("rdf.Id={0}, Npc joinIndex={1}, speciesId={2} is waived for patrolCueId={3} because it has (prevCapturedByPatrolCue={4}, framesInPatrolCue={5}, waivingPatrolCueId={6}).", currRenderFrame.Id, currCharacterDownsync.JoinIndex, currCharacterDownsync.SpeciesId, ptrlCue.Id, prevCapturedByPatrolCue, currCharacterDownsync.FramesInPatrolCue, currCharacterDownsync.WaivingPatrolCueId));
                                break;
                            }

                            bool shouldEnterNewCapturedPeriod = (false == prevCapturedByPatrolCue); // [WARNING] "false == prevCapturedByPatrolCue" implies "false == shouldBreakCurrentPatrolCueCapture"

                            if (0 >= targetFramesInPatrolCue) {
                                targetFramesInPatrolCue = 1;
                            }
                            if (shouldEnterNewCapturedPeriod) {
                                thatCharacterInNextFrame.CapturedByPatrolCue = true;
                                thatCharacterInNextFrame.FramesInPatrolCue = targetFramesInPatrolCue;
                                thatCharacterInNextFrame.WaivingPatrolCueId = ptrlCue.Id;
                                thatCharacterInNextFrame.CachedCueCmd = toCacheCmd; 
                                effectiveDx = 0;
                                effectiveDy = 0;
                                jumpedOrNot = false;
                                jumpHoldingRdfCnt = 0;
                                slipJumpedOrNot = false;
                                hasPatrolCueReaction = true;
                                break;
                            }
                        } while (false);
                    }
                }

                /*
                if (SPECIES_SKELEARCHER == currCharacterDownsync.SpeciesId && 1 == currCharacterDownsync.Id) { 
                    if (Walking == currCharacterDownsync.CharacterState) {
                        logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, framesInChState={2} still in Walking, shouldCheckVisionCollision={3}, visionReaction={4}, effectiveDx={5}, effectiveDy={6}, VelX={7}, VelY={8}", rdfId, currCharacterDownsync.Id, currCharacterDownsync.FramesInChState, shouldCheckVisionCollision, visionReaction, effectiveDx, effectiveDy, currCharacterDownsync.VelX, currCharacterDownsync.VelY));
                    } else {
                        logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, framesInChState={2} no longer Walking, shouldCheckVisionCollision={3}, visionReaction={4}, effectiveDx={5}, effectiveDy={6}, VelX={7}, VelY={8}", rdfId, currCharacterDownsync.Id, currCharacterDownsync.FramesInChState, shouldCheckVisionCollision, visionReaction, effectiveDx, effectiveDy, currCharacterDownsync.VelX, currCharacterDownsync.VelY));
                    }
                }
                if (SPECIES_LIGHTGUARD_RED == currCharacterDownsync.SpeciesId && Def1 == currCharacterDownsync.CharacterState) {
                    logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, framesInChState={2} still in Def1, shouldCheckVisionCollision={3}, visionReaction={4}#2", rdfId, currCharacterDownsync.Id, currCharacterDownsync.FramesInChState, shouldCheckVisionCollision, visionReaction));
                }
                */
                if (TARGET_CH_REACTION_UNKNOWN == visionReaction && false == hasPatrolCueReaction) {
                    if (0 != currCharacterDownsync.CachedCueCmd && (canJumpWithinInertia || (0 >= currCharacterDownsync.FramesToRecover && (currCharacterDownsync.OmitGravity || chConfig.OmitGravity)))) {
                        // [WARNING] "canJumpWithinInertia" implies "(0 == currCharacterDownsync.FramesToRecover)"
                        thatCharacterInNextFrame.CachedCueCmd = 0;
                        DecodeInput(currCharacterDownsync.CachedCueCmd, decodedInputHolder);
                        effectiveDx = decodedInputHolder.Dx;
                        effectiveDy = decodedInputHolder.Dy;
                        slipJumpedOrNot = (0 == currCharacterDownsync.FramesToRecover) && ((currCharacterDownsync.PrimarilyOnSlippableHardPushback || (effInAir && currCharacterDownsync.OmitGravity && !chConfig.OmitGravity)) && 0 > decodedInputHolder.Dy && 0 == decodedInputHolder.Dx) && (0 < decodedInputHolder.BtnALevel);
                        jumpedOrNot = !slipJumpedOrNot && (0 == currCharacterDownsync.FramesToRecover) && !effInAir && (0 < decodedInputHolder.BtnALevel);

                        if (0 >= chConfig.JumpingInitVelY) {
                            slipJumpedOrNot = false;
                            jumpHoldingRdfCnt = 0;
                            jumpedOrNot = false;
                        } else if (jumpedOrNot) {
                            if (0 == jumpHoldingRdfCnt) {
                                jumpHoldingRdfCnt = 1;
                            }
                            if (0 < currCharacterDownsync.JumpHoldingRdfCnt && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                                // [warning] only proactive jumping support jump holding.
                                jumpHoldingRdfCnt = currCharacterDownsync.JumpHoldingRdfCnt + 1;
                                patternId = PATTERN_HOLD_B;
                                if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= jumpHoldingRdfCnt) {
                                    jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_2;
                                } else if (!chConfig.JumpHoldingToFly && JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= jumpHoldingRdfCnt) {
                                    jumpHoldingRdfCnt = JUMP_HOLDING_RDF_CNT_THRESHOLD_1;
                                }
                            }
                        }
                        return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effectiveDx, effectiveDy, slowDownToAvoidOverlap);
                    } else if (null != mvBlockerCollider) {
                        float aBoxLeft = aCollider.X;     
                        float aBoxRight = aCollider.X+aCollider.W;
                        float aBoxBottom = aCollider.Y;
                        float aBoxTop = aCollider.Y+aCollider.H;
                        visionReaction = deriveReactionForMvBlocker(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, canJumpWithinInertia, aCollider, aBoxLeft, aBoxRight, aBoxBottom, aBoxTop, mvBlockerCollider, standingOnCollider, primaryOverlapResult, logger);
                        if (TARGET_CH_REACTION_WALK_ALONG == visionReaction) {
                            switch (currCharacterDownsync.GoalAsNpc) {
                                case NpcGoal.NidleIfGoHuntingThenPatrol:
                                    thatCharacterInNextFrame.GoalAsNpc = NpcGoal.Npatrol;
                                    break;
                            }
                            return (PATTERN_ID_NO_OP, false, false, 0, currCharacterDownsync.DirX, currCharacterDownsync.DirY, slowDownToAvoidOverlap);
                        } else if (TARGET_CH_REACTION_JUMP_TOWARDS == visionReaction) {
                            return (PATTERN_DOWN_A, true, false, currCharacterDownsync.JumpHoldingRdfCnt + 1, currCharacterDownsync.DirX, currCharacterDownsync.DirY, slowDownToAvoidOverlap);
                        } else if (TARGET_CH_REACTION_FLEE == visionReaction) {
                            effectiveDx = -currCharacterDownsync.DirX;
                            effectiveDy = -currCharacterDownsync.DirY;
                        } else if (TARGET_CH_REACTION_STOP == visionReaction) {
                            switch (currCharacterDownsync.GoalAsNpc) {
                                case NpcGoal.Npatrol:
                                    thatCharacterInNextFrame.GoalAsNpc = NpcGoal.NidleIfGoHuntingThenPatrol;
                                    break;
                            }
                            return (PATTERN_ID_NO_OP, false, false, 0, 0, 0, slowDownToAvoidOverlap);
                        }
                    }

                    if (TARGET_CH_REACTION_FLEE == visionReaction && (PATTERN_ID_NO_OP == patternId || PATTERN_ID_UNABLE_TO_OP == patternId)) {
                        var (_, _, discretizedDir) = Battle.DiscretizeDirection(effectiveDx, effectiveDy);
                        ulong oldCachedCueCmd = thatCharacterInNextFrame.CachedCueCmd;
                        ulong newCachedCueCmd = (ulong)discretizedDir;
                        if (oldCachedCueCmd != newCachedCueCmd) {
                            thatCharacterInNextFrame.CachedCueCmd = newCachedCueCmd;
                            thatCharacterInNextFrame.FramesToRecover = NPC_FLEE_GRACE_PERIOD_RDF_CNT;
                            //logger.LogInfo($"\t@rdfId={rdfId}, delayed to flee\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, cachedCueCmd:{thatCharacterInNextFrame.CachedCueCmd}, FramesToRecover:{thatCharacterInNextFrame.FramesToRecover}, goal:{thatCharacterInNextFrame.GoalAsNpc})");
                            return (PATTERN_ID_NO_OP, false, false, 0, 0, 0, slowDownToAvoidOverlap);
                        }
                    }

                    if (chConfig.JumpHoldingToFly && InAirIdle1ByJump == currCharacterDownsync.CharacterState) {
                        return (PATTERN_ID_NO_OP, false, false, currCharacterDownsync.JumpHoldingRdfCnt+1, currCharacterDownsync.DirX, currCharacterDownsync.DirY, slowDownToAvoidOverlap);
                    }
                    if (TERMINATING_TRIGGER_ID != currCharacterDownsync.SubscribesToTriggerLocalId) {
                        return (PATTERN_ID_NO_OP, false, false, 0, 0, 0, slowDownToAvoidOverlap);
                    }
                    if (chConfig.AntiGravityWhenIdle && (Idle1 == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState)) {
                        return (PATTERN_ID_NO_OP, false, false, 0, 0, 0, slowDownToAvoidOverlap);
                    }
                    bool possiblyWalking = (Walking == currCharacterDownsync.CharacterState || InAirWalking == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState || InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState || InAirIdle2ByJump == currCharacterDownsync.CharacterState); // Including walking equivalents in air
                    bool inPatrolCueCtrl = (false == currCharacterDownsync.CapturedByPatrolCue && 0 < currCharacterDownsync.FramesInPatrolCue);
                    bool possiblyCrouching = isCrouching(currCharacterDownsync.CharacterState, chConfig);
                    if (NpcGoal.Npatrol != currCharacterDownsync.GoalAsNpc) {
                        if (possiblyWalking && !visionSearchTick) {
                            // [WARNING] Such that for "NpcGoal.Patrol" it doesn't have to execute vision reaction again to trace the same opponent.
                            if (effInAir && chConfig.NpcNoDefaultAirWalking && !inPatrolCueCtrl) {
                                effectiveDx = 0;
                            }
                            /*
                            if (SPECIES_SKELEARCHER == currCharacterDownsync.SpeciesId) {
                                logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, possiblyWalking, for now effectiveDx={2}, currCharacterDownsync.CharacterState={3}, effInAir={4}, framesToStartJump={5}", rdfId, currCharacterDownsync.Id, effectiveDx, currCharacterDownsync.CharacterState, effInAir, currCharacterDownsync.FramesToStartJump));
                            }
                            */
                            return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effectiveDx, effectiveDy, slowDownToAvoidOverlap);
                        } else if (possiblyCrouching && !visionSearchTick) {
                            return (PATTERN_ID_NO_OP, false, false, 0, 0, -2, slowDownToAvoidOverlap);
                        } else {
                            return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0, 0, slowDownToAvoidOverlap);
                        }
                    } else {
                        if (possiblyCrouching) {
                            return (PATTERN_ID_NO_OP, false, false, 0, 0, -2, slowDownToAvoidOverlap);
                        } else {
                            if (effInAir && chConfig.NpcNoDefaultAirWalking && !inPatrolCueCtrl) {
                                return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, 0, currCharacterDownsync.DirY, slowDownToAvoidOverlap);
                            } else {
                                return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, currCharacterDownsync.DirX, currCharacterDownsync.DirY, slowDownToAvoidOverlap);
                            }
                        }
                    }
                }
            }

            return (patternId, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effectiveDx, effectiveDy, slowDownToAvoidOverlap);
        }

        private static void _processNpcInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, int colliderCnt, Collision collision, CollisionSpace collisionSys, ref SatResult overlapResult, ref SatResult primaryOverlapResult, InputFrameDecoded decodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = roomCapacity; i < roomCapacity + npcCnt; i++) {
                var currCharacterDownsync = currRenderFrame.NpcsArr[i - roomCapacity];
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = nextRenderFrameNpcs[i - roomCapacity];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                bool notDashing = isNotDashing(currCharacterDownsync);
                bool effInAir = isEffInAir(currCharacterDownsync, notDashing);
                var (patternId, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effDx, effDy, slowDownToAvoidOverlap) = deriveNpcOpPattern(currCharacterDownsync, effInAir, notDashing, currRenderFrame, roomCapacity, chConfig, thatCharacterInNextFrame, dynamicRectangleColliders, colliderCnt, collisionSys, collision, ref overlapResult, ref primaryOverlapResult, decodedInputHolder, logger);

                var (slotUsed, slotLockedSkillId, dodgedInBlockStun) = _useInventorySlot(currRenderFrame.Id, patternId, currCharacterDownsync, effInAir, chConfig, thatCharacterInNextFrame, logger);

                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;
                thatCharacterInNextFrame.SlipJumpTriggered |= slipJumpedOrNot;
                thatCharacterInNextFrame.JumpHoldingRdfCnt = jumpHoldingRdfCnt;

                if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 > currCharacterDownsync.JumpHoldingRdfCnt && JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= thatCharacterInNextFrame.JumpHoldingRdfCnt && !thatCharacterInNextFrame.OmitGravity && chConfig.JumpHoldingToFly) {
                    thatCharacterInNextFrame.OmitGravity = true;
                    if (0 >= thatCharacterInNextFrame.VelY) {       
                        thatCharacterInNextFrame.VelY = 0;
                    }
                }

                var existingDebuff = currCharacterDownsync.DebuffList[DEBUFF_ARR_IDX_ELEMENTAL];
                bool isParalyzed = (TERMINATING_DEBUFF_SPECIES_ID != existingDebuff.SpeciesId && 0 < existingDebuff.Stock && DebuffType.PositionLockedOnly == debuffConfigs[existingDebuff.SpeciesId].Type);
                bool notEnoughMp = false;
                bool usedSkill = dodgedInBlockStun ? false : _useSkill(effDx, effDy, patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets, slotUsed, slotLockedSkillId, ref notEnoughMp, isParalyzed, logger);
                Skill? skillConfig = null;
                if (usedSkill) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0; // The use of a skill should break "CapturedByInertia"
                    resetJumpStartupOrHolding(thatCharacterInNextFrame, true);
                    bool nextRdfChNotDashing = isNotDashing(thatCharacterInNextFrame);
                    if (nextRdfChNotDashing) {
                        thatCharacterInNextFrame.BtnBHoldingRdfCount = 0;
                    }
                    skillConfig = skills[thatCharacterInNextFrame.ActiveSkillId];
                    if (Dashing == skillConfig.BoundChState && effInAir && 0 < thatCharacterInNextFrame.RemainingAirDashQuota) {              
                        thatCharacterInNextFrame.RemainingAirDashQuota -= 1;
                        if (!chConfig.IsolatedAirJumpAndDashQuota && 0 < thatCharacterInNextFrame.RemainingAirJumpQuota) {
                            thatCharacterInNextFrame.RemainingAirJumpQuota -= 1;
                        }
                    }
                    if (isCrouching(currCharacterDownsync.CharacterState, chConfig) && Atk1 == thatCharacterInNextFrame.CharacterState) {
                        if (chConfig.CrouchingAtkEnabled) {
                            thatCharacterInNextFrame.CharacterState = CrouchAtk1;
                        }
                    }
                    continue; // Don't allow movement if skill is used
                }

                thatCharacterInNextFrame.BtnBHoldingRdfCount = (PATTERN_HOLD_B == patternId ? currCharacterDownsync.BtnBHoldingRdfCount + 1 : 0);
                _processNextFrameJumpStartup(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effInAir, chConfig, isParalyzed, logger);
                if (!currCharacterDownsync.OmitGravity && !chConfig.OmitGravity) {
                    _processInertiaWalking(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effInAir, effDx, effDy, chConfig, true, usedSkill, skillConfig, isParalyzed, logger); // TODO: When breaking free from a PatrolCue, an NPC often couldn't turn around from a cliff in time, thus using "shouldIgnoreInertia" temporarily
                    if (0 != thatCharacterInNextFrame.VelX && slowDownToAvoidOverlap) {
                        //logger.LogInfo(String.Format("@rdfId={0}, slowing down walking npc id={1}", currRenderFrame.Id, currCharacterDownsync.Id));
                        thatCharacterInNextFrame.VelX >>= 2;
                    }
                } else {
                    _processInertiaFlying(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, chConfig, true, usedSkill, skillConfig, isParalyzed, logger);
                    if (PATTERN_ID_UNABLE_TO_OP != patternId && chConfig.AntiGravityWhenIdle && (Walking == thatCharacterInNextFrame.CharacterState || InAirWalking == thatCharacterInNextFrame.CharacterState) && chConfig.AntiGravityFramesLingering < thatCharacterInNextFrame.FramesInChState) {
                        thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                        thatCharacterInNextFrame.FramesInChState = 0;
                        thatCharacterInNextFrame.VelX = 0;
                    } else {
                        if ((0 != thatCharacterInNextFrame.VelX || 0 != thatCharacterInNextFrame.VelY) && slowDownToAvoidOverlap) {
                            //logger.LogInfo(String.Format("@rdfId={0}, slowing down flying npc id={1}", currRenderFrame.Id, currCharacterDownsync.Id));
                            thatCharacterInNextFrame.VelX >>= 2;
                            thatCharacterInNextFrame.VelY >>= 2;
                        }
                    }
                }
                _processDelayedBulletSelfVel(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, chConfig, isParalyzed, logger);
            }
        }

        protected static bool addNewNpcToNextFrame(int rdfId, int roomCapacity, int virtualGridX, int virtualGridY, int dirX, int dirY, uint characterSpeciesId, int teamId, NpcGoal initGoal, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, int publishingToTriggerLocalIdUponKilled, ulong waveNpcKilledEvtMaskCounter, int subscribesToTriggerLocalId) {
            var chConfig = characters[characterSpeciesId];
            int birthVirtualX = virtualGridX + ((chConfig.DefaultSizeX >> 2) * dirX);
            CharacterState initChState = chConfig.OmitGravity ? Walking : Idle1;
            int joinIndex = roomCapacity + npcCnt + 1;
            AssignToCharacterDownsync(npcLocalIdCounter, characterSpeciesId, birthVirtualX, virtualGridY, dirX, dirY, 0, 0, 0, 0, 0, 0, NO_SKILL, NO_SKILL_HIT, 0, chConfig.Speed, initChState, joinIndex, chConfig.Hp, true, false, 0, 0, 0, teamId, teamId, birthVirtualX, virtualGridY, dirX, dirY, false, false, false, false, 0, 0, 0, chConfig.Mp, chConfig.OmitGravity, chConfig.OmitSoftPushback, chConfig.RepelSoftPushback, initGoal, 0, false, false, false, newBirth: true, false, 0, 0, 0, defaultTemplateBuffList, defaultTemplateDebuffList, prevInventory: null, false, publishingToTriggerLocalIdUponKilled, waveNpcKilledEvtMaskCounter, subscribesToTriggerLocalId, jumpHoldingRdfCnt: 0, btnBHoldingRdfCount: 0, btnEHoldingRdfCount: 0, parryPrepRdfCntDown: 0, chConfig.DefaultAirJumpQuota, chConfig.DefaultAirDashQuota, TERMINATING_CONSUMABLE_SPECIES_ID, TERMINATING_BUFF_SPECIES_ID, NO_SKILL, defaultTemplateBulletImmuneRecords, 0, 0, 0, MAGIC_JOIN_INDEX_INVALID, TERMINATING_BULLET_TEAM_ID, rdfId, 0, chConfig.MpRegenInterval, 0, MAGIC_JOIN_INDEX_INVALID, nextRenderFrameNpcs[npcCnt]); // TODO: Support killedToDropConsumable/Buff here

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
        
        public static int deriveReactionForMvBlocker(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, bool canJumpWithinInertia, Collider aCollider, float aBoxLeft, float aBoxRight, float aBoxBottom, float aBoxTop, Collider mvBlockerCollider, Collider? standingOnCollider, SatResult primaryOverlapResult, ILoggerBridge logger) {
            //logger.LogInfo($"@rdfId={rdfId}, currCharacterDownsync.Id={currCharacterDownsync.Id}, canJumpWithinInertia={canJumpWithinInertia}, aBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop}) checking mvBlockerCollider={mvBlockerCollider.Shape.ToString(true)}");
            
            float mvBlockerColliderLeft = mvBlockerCollider.X;
            float mvBlockerColliderRight = mvBlockerCollider.X + mvBlockerCollider.W;
            float mvBlockerColliderBottom = mvBlockerCollider.Y;
            float mvBlockerColliderTop = mvBlockerCollider.Y + mvBlockerCollider.H;

            bool isCharacterFlying = (currCharacterDownsync.OmitGravity || chConfig.OmitGravity);
            bool temptingToMove = (NpcGoal.Npatrol == currCharacterDownsync.GoalAsNpc && (canJumpWithinInertia || isCharacterFlying));

             bool hasBlockerInXForward = (0 < currCharacterDownsync.DirX && (0 < currCharacterDownsync.VelX || temptingToMove) && mvBlockerColliderLeft+STANDING_COLLIDER_CHECK_EPS >= aBoxRight && mvBlockerColliderRight > aBoxRight) || (0 > currCharacterDownsync.DirX && (0 > currCharacterDownsync.VelX || temptingToMove) && mvBlockerColliderRight <= aBoxLeft+ STANDING_COLLIDER_CHECK_EPS && mvBlockerColliderLeft < aBoxLeft);

            bool hasBlockerInYForward = (0 < currCharacterDownsync.DirY && (0 < currCharacterDownsync.VelY || temptingToMove) && mvBlockerColliderBottom >= aBoxTop) || (0 > currCharacterDownsync.DirY && (0 > currCharacterDownsync.VelY || temptingToMove) && mvBlockerColliderTop <= aBoxBottom);

            hasBlockerInXForward &= (mvBlockerCollider != standingOnCollider) && (mvBlockerColliderBottom <= aBoxTop || mvBlockerColliderTop >= aBoxBottom+SNAP_INTO_PLATFORM_OVERLAP);

            hasBlockerInYForward &= (mvBlockerCollider != standingOnCollider);

            if (!isCharacterFlying) {
                if (hasBlockerInXForward) {
                    if (0 < currCharacterDownsync.VelX * primaryOverlapResult.OverlapX && 0 > primaryOverlapResult.OverlapY) {
                        // Potentially a slope, by now "0 != primaryOverlapResult.OverlapX" 
                        // [REMINDER] The direction (SatResult.OverlapX, SatResult.OverlapY) points perpendicularly into the slope, thus the slope value is (-OverlapX/OverlapY)!
                        bool notSteep = (0 < primaryOverlapResult.OverlapX && primaryOverlapResult.OverlapX <= -0.8f*primaryOverlapResult.OverlapY)
                                        ||
                                        (0 > primaryOverlapResult.OverlapX && (-primaryOverlapResult.OverlapX) <= -0.8f*primaryOverlapResult.OverlapY);
                        if (notSteep) {
                            // not steep, just keep walking
                            return TARGET_CH_REACTION_UNKNOWN;
                        }

                        /*
                        if (SPECIES_GOBLIN == currCharacterDownsync.SpeciesId && 0 > currCharacterDownsync.VelX && !notSteep) {
                            logger.LogInfo($"@rdfId={rdfId}, currCharacterDownsync.Id={currCharacterDownsync.Id}, canJumpWithinInertia={canJumpWithinInertia}, aBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop}) checking slope={mvBlockerCollider.Shape.ToString(true)}, primaryOverlapResult={primaryOverlapResult.ToString()}");
                        }
                        */
                    }
                    float diffCxApprox = 0f;
                    if (0 < currCharacterDownsync.DirX) {
                        diffCxApprox = (mvBlockerColliderLeft - aBoxRight);
                    } else if (0 > currCharacterDownsync.DirX) {
                        diffCxApprox = (aBoxLeft - mvBlockerColliderRight);
                    }
                    if (mvBlockerColliderTop > aBoxBottom + SNAP_INTO_PLATFORM_OVERLAP) {
                        float diffCyApprox = (mvBlockerColliderTop - aBoxTop);
                        if (canJumpWithinInertia) {
                            int jumpableDiffVirtualGridYApprox = (chConfig.JumpingInitVelY*chConfig.JumpingInitVelY)/(-GRAVITY_Y); 
                            int jumpableDiffVirtualGridXBase = 0 < diffCyApprox ? (chConfig.Speed*chConfig.JumpingInitVelY)/(-GRAVITY_Y) : (chConfig.Speed * (chConfig.JumpingInitVelY << 1)) / (-GRAVITY_Y);
                            int jumpableDiffVirtualGridXApprox = jumpableDiffVirtualGridXBase; 
                            var (jumpableDiffCxApprox, jumpableDiffCyApprox) = VirtualGridToPolygonColliderCtr(jumpableDiffVirtualGridXApprox, jumpableDiffVirtualGridYApprox); 
                            bool heightDiffJumpableApprox = (jumpableDiffCyApprox >= diffCyApprox);
                            bool widthDiffJumpableApprox = (jumpableDiffCxApprox >= diffCxApprox);
                            if (heightDiffJumpableApprox && widthDiffJumpableApprox) {
                                //logger.LogInfo($"\t@rdfId={rdfId}, jumping NPC to higher\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                                return TARGET_CH_REACTION_JUMP_TOWARDS;
                            } else if (0 < diffCyApprox && FLEE_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD > diffCxApprox) {
                                switch (mvBlockerCollider.Data) {
                                    case TrapColliderAttr tpc:
                                        if (tpc.ProvidesSlipJump) {
                                            return TARGET_CH_REACTION_UNKNOWN;
                                        } else {
                                            return TARGET_CH_REACTION_FLEE;
                                        }
                                    default:
                                        //logger.LogInfo($"\t@rdfId={rdfId}, currCharacterDownsync.Id={currCharacterDownsync.Id}, canJumpWithinInertia={canJumpWithinInertia}, aBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop}) checking mvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop}), deciding to flee with jumpableDiffCyApprox={jumpableDiffCyApprox} v.s. diffCyApprox={diffCyApprox}, jumpableDiffCxApprox={jumpableDiffCxApprox} v.s. diffCxApprox={diffCxApprox}");
                                        return TARGET_CH_REACTION_FLEE;
                                }
                            }
                        }
                    } else {
                        // Otherwise "mvBlockerCollider" sits lower than "currCharacterDownsync", a next but lower platform in the direction of movement, might still need jumping but the estimated dx movable by jumping is longer in this case
                        float diffCyApprox = (aBoxTop - mvBlockerColliderTop);
                        if (canJumpWithinInertia) {
                            int jumpableDiffVirtualGridXBase = (chConfig.Speed * (chConfig.JumpingInitVelY << 2)) / (-GRAVITY_Y);
                            int jumpableDiffVirtualGridXApprox = jumpableDiffVirtualGridXBase + (jumpableDiffVirtualGridXBase >> 2);
                            var (jumpableDiffCxApprox, _) = VirtualGridToPolygonColliderCtr(jumpableDiffVirtualGridXApprox, 0);
                            bool widthDiffJumpableApprox = (jumpableDiffCxApprox >= diffCxApprox);
                            if (widthDiffJumpableApprox) {
                                //logger.LogInfo($"\t@rdfId={rdfId}, jumping NPC to lower\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");

                                return TARGET_CH_REACTION_JUMP_TOWARDS;
                            } else {
                                return TARGET_CH_REACTION_UNKNOWN;
                            }
                        }
                    }
                } else {
                    // !hasBlockerInXForward, in this case moving forward might fall into death
                    int effVelX = (0 != currCharacterDownsync.VelX ? currCharacterDownsync.VelX : (0 < currCharacterDownsync.DirX ? currCharacterDownsync.Speed : -currCharacterDownsync.Speed)); 
                    switch (currCharacterDownsync.GoalAsNpc) {
                        case NpcGoal.Nidle:
                            effVelX = 0;
                            break;
                        case NpcGoal.NidleIfGoHuntingThenPatrol:
                            effVelX = 0;
                            break;
                        default:
                            break;
                    }
                    bool currBlockCanStillHoldMe = (null != standingOnCollider) && ((0 < currCharacterDownsync.DirX && standingOnCollider.X+standingOnCollider.W >= aBoxRight+effVelX) || (0 > currCharacterDownsync.DirX && standingOnCollider.X <= aBoxLeft+effVelX));
                    if (currBlockCanStillHoldMe && 0 != effVelX) {
                        /*
                        if (SPECIES_BOARWARRIOR == currCharacterDownsync.SpeciesId) {
                            logger.LogInfo($"\t@rdfId={rdfId}, walking along NPC\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, inAir:{currCharacterDownsync.InAir}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, inAir: {thatCharacterInNextFrame.InAir}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                        }
                        */
                        return TARGET_CH_REACTION_WALK_ALONG;
                    } else if (!currBlockCanStillHoldMe && !currCharacterDownsync.InAir) {
                        /*
                        if (SPECIES_BOARWARRIOR == currCharacterDownsync.SpeciesId) {
                            logger.LogInfo($"\t@rdfId={rdfId}, stopping NPC\n\tcurrCharacterDownsync=(Id:{currCharacterDownsync.Id}, speciesId:{currCharacterDownsync.SpeciesId}, inAir:{currCharacterDownsync.InAir}, dirX:{currCharacterDownsync.DirX}, velX:{currCharacterDownsync.VelX}, goal:{currCharacterDownsync.GoalAsNpc})\n\tthatCharacterInNextFrame=(Id:{thatCharacterInNextFrame.Id}, speciesId:{thatCharacterInNextFrame.SpeciesId}, inAir: {thatCharacterInNextFrame.InAir}, dirX:{thatCharacterInNextFrame.DirX}, velX:{thatCharacterInNextFrame.VelX}, goal:{thatCharacterInNextFrame.GoalAsNpc})\n\taBox=(left:{aBoxLeft}, right:{aBoxRight}, bottom:{aBoxBottom}, top:{aBoxTop})\n\tmvBlockerCollider=(left:{mvBlockerColliderLeft}, right:{mvBlockerColliderRight}, bottom:{mvBlockerColliderBottom}, top:{mvBlockerColliderTop})\n\tmvBlockerColliderShape={(null == mvBlockerCollider ? "null" : mvBlockerCollider.Shape.ToString(true))}\n\tstandingOnCollider={(null == standingOnCollider ? "null" : standingOnCollider.Shape.ToString(true))}\n\ttemptingToMove:{temptingToMove}");
                        }
                        */
                        return TARGET_CH_REACTION_STOP;
                    }
                }
            } else {
                // A flying character
                if (hasBlockerInXForward && mvBlockerColliderTop >= aBoxBottom && mvBlockerColliderBottom <= aBoxTop) {
                    float diffCxApprox = 0f;
                    if (0 < currCharacterDownsync.DirX) {
                        diffCxApprox = (mvBlockerColliderLeft - aBoxRight);
                    } else if (0 > currCharacterDownsync.DirX) {
                        diffCxApprox = (aBoxLeft - mvBlockerColliderRight);
                    }

                    if (FLEE_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD * FLEE_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD > (diffCxApprox * diffCxApprox)) {
                        return TARGET_CH_REACTION_FLEE;
                    }
                } else if (hasBlockerInYForward && mvBlockerColliderLeft <= aBoxRight && mvBlockerColliderRight >= aBoxLeft) {
                    
                    float diffCyApprox = 0f;
                    if (0 < currCharacterDownsync.DirY) {
                        diffCyApprox = (mvBlockerColliderBottom - aBoxTop);
                    } else if (0 > currCharacterDownsync.DirY) {
                        diffCyApprox = (aBoxBottom - mvBlockerColliderTop);
                    }

                    if (FLEE_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD*FLEE_FROM_MV_BLOCKER_DX_COLLISION_SPACE_THRESHOLD > (diffCyApprox*diffCyApprox)) {
                        return TARGET_CH_REACTION_FLEE;
                    }
                }
            }

            return TARGET_CH_REACTION_UNKNOWN; 
        }
    }
}
