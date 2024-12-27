using System;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        private static int OPPONENT_REACTION_UNKNOWN = 0;
        private static int OPPONENT_REACTION_USE_MELEE = 1;
        private static int OPPONENT_REACTION_USE_DRAGONPUNCH = 2;
        private static int OPPONENT_REACTION_USE_FIREBALL = 3;
        private static int OPPONENT_REACTION_FOLLOW = 4;
        private static int OPPONENT_REACTION_USE_SLOT_C = 5;
        private static int OPPONENT_REACTION_NOT_ENOUGH_MP = 6;
        private static int OPPONENT_REACTION_FLEE = 7;
        private static int OPPONENT_REACTION_DEF1 = 8;
        // private static int OPPONENT_REACTION_USE_SLOT_D = 9;

        private static int OPPONENT_REACTION_JUMP_TOWARDS = 10;
        //private static int OPPONENT_REACTION_USE_DASHING = 11;
        private static int OPPONENT_REACTION_SLIP_JUMP = 12;

        private static int NPC_DEF1_MIN_HOLDING_RDF_CNT = 90;

        private static int VISION_SEARCH_RDF_RANDOMIZE_MASK = (1 << 4) + (1 << 3) + (1 << 1);

        private static int OPPO_DX_OFFSET_MOD_MINUS_1 = (1 << 3) - 1;
        private static float OPPO_DX_OFFSET = 10.0f; // In collision space

        private static void _handleOppoCh(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool canJumpWithinInertia, Collider? oppoChCollider, CharacterDownsync? v3, ref float oppoChColliderDx, ref float oppoChColliderDy, ref int patternId, ref bool jumpedOrNot, ref bool slipJumpedOrNot, ref int jumpHoldingRdfCnt, ref int effectiveDx, ref int effectiveDy, ref int visionReaction) {
            if (null == oppoChCollider || null == v3) return;
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
            float absColliderDx = Math.Abs(oppoChColliderDx), absColliderDy = Math.Abs(oppoChColliderDy);

            bool opponentBehindMe = (0 > (oppoChColliderDx * currCharacterDownsync.DirX));
            if (!opponentBehindMe) {
                // Opponent is in front of me
                float opponentBoxCx, opponentBoxCy, opponentBoxCw, opppnentBoxCh;
                calcCharacterBoundingBoxInCollisionSpace(v3, oppoChConfig, v3.VirtualGridX, v3.VirtualGridY, out opponentBoxCx, out opponentBoxCy, out opponentBoxCw, out opppnentBoxCh);
                float opponentBoxLeft = opponentBoxCx - 0.5f * opponentBoxCw, opponentBoxRight = opponentBoxCx + 0.5f * opponentBoxCw, opponentBoxBottom = opponentBoxCy - 0.5f * opppnentBoxCh, opponentBoxTop = opponentBoxCy + 0.5f * opppnentBoxCh;
                int s0 = OPPONENT_REACTION_UNKNOWN, s1 = OPPONENT_REACTION_UNKNOWN, s2 = OPPONENT_REACTION_UNKNOWN, s3 = OPPONENT_REACTION_UNKNOWN;

                s0 = frontOpponentReachableByIvSlot(currCharacterDownsync, effInAir, chConfig, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                if (OPPONENT_REACTION_USE_SLOT_C == s0) {
                    patternId = PATTERN_INVENTORY_SLOT_C;
                    visionReaction = s0;
                } else {
                    s1 = frontOpponentReachableByDragonPunch(currCharacterDownsync, effInAir, chConfig, canJumpWithinInertia, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop); // [WARNING] When just transited from GetUp1 to Idle1, dragonpunch might be triggered due to the delayed virtualGridY bouncing back.
                    visionReaction = s1;
                    if (OPPONENT_REACTION_USE_DRAGONPUNCH == s1) {
                        patternId = PATTERN_UP_B;
                    } else {
                        s2 = frontOpponentReachableByMelee1(currCharacterDownsync, effInAir, aCollider, oppoChCollider, oppoChColliderDx, absColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop);
                        visionReaction = s2;
                        if (OPPONENT_REACTION_USE_MELEE == s2) {
                            patternId = PATTERN_B;
                        } else {
                            s3 = frontOpponentReachableByFireball(currCharacterDownsync, chConfig, aCollider, oppoChCollider, oppoChColliderDx, oppoChColliderDy, absColliderDy, opponentBoxLeft, opponentBoxRight, opponentBoxBottom, opponentBoxTop);
                            visionReaction = s3;
                            if (OPPONENT_REACTION_USE_FIREBALL == s3) {
                                patternId = PATTERN_DOWN_B;
                            } else {
                                visionReaction = OPPONENT_REACTION_FOLLOW;
                            }
                        }
                    }
                }

                if (OPPONENT_REACTION_NOT_ENOUGH_MP == s0 && OPPONENT_REACTION_NOT_ENOUGH_MP == s1 && OPPONENT_REACTION_NOT_ENOUGH_MP == s2 && OPPONENT_REACTION_NOT_ENOUGH_MP == s3 && !chConfig.IsKeyCh) {
                    visionReaction = OPPONENT_REACTION_FLEE;
                } else if (OPPONENT_REACTION_UNKNOWN == s0 && OPPONENT_REACTION_UNKNOWN == s1 && OPPONENT_REACTION_UNKNOWN == s2 && OPPONENT_REACTION_UNKNOWN == s3) {
                    visionReaction = OPPONENT_REACTION_FOLLOW;
                }
            } else {
                // Opponent is behind me
                if (0 >= chConfig.Speed) {
                    // e.g. Tower
                    visionReaction = OPPONENT_REACTION_UNKNOWN;
                } else {
                    CharacterState opCh = v3.CharacterState;
                    bool opponenetIsAttacking = (InAirAtk1 == opCh || InAirAtk2 == opCh || Atk1 == opCh || Atk2 == opCh || Atk3 == opCh || Atk4 == opCh || Atk5 == opCh || Atk6 == opCh || Atk7 == opCh);
                    bool opponenetIsFacingMe = (0 > (oppoChColliderDx * v3.DirX)) && (absColliderDy < 0.2f * aCollider.H);
                    bool farEnough = (absColliderDx > 0.6f * (aCollider.W + oppoChCollider.W)); // To avoid bouncing turn-arounds
                    if ((opponenetIsAttacking && opponenetIsFacingMe) || farEnough) {
                        visionReaction = OPPONENT_REACTION_FOLLOW;
                    } else {
                        visionReaction = OPPONENT_REACTION_FLEE;
                    }
                }
            }
            
            if (OPPONENT_REACTION_FOLLOW == visionReaction) {
                bool shouldJumpTowardsTarget = (canJumpWithinInertia && !effInAir && (0.6f * aCollider.H < oppoChColliderDy) && (0 <= currCharacterDownsync.DirX * oppoChColliderDx));
                bool shouldSlipJumpTowardsTarget = (canJumpWithinInertia && !effInAir && 0 > oppoChColliderDy && currCharacterDownsync.PrimarilyOnSlippableHardPushback);
                if (0 >= chConfig.JumpingInitVelY) {
                    shouldJumpTowardsTarget = false;
                    shouldSlipJumpTowardsTarget = false;
                } else if (chConfig.HasDef1) {
                    shouldJumpTowardsTarget = false;
                }
                if (shouldSlipJumpTowardsTarget) {
                    visionReaction = OPPONENT_REACTION_SLIP_JUMP;
                } else if (shouldJumpTowardsTarget) {
                    visionReaction = OPPONENT_REACTION_JUMP_TOWARDS;
                }
            }

            if (OPPONENT_REACTION_JUMP_TOWARDS == visionReaction) {
                jumpedOrNot = true;
                if (0 == jumpHoldingRdfCnt) {
                    jumpHoldingRdfCnt = 1;
                }
                if (0 < currCharacterDownsync.JumpHoldingRdfCnt && (InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState || InAirIdle2ByJump == currCharacterDownsync.CharacterState)) {
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
            } else if (OPPONENT_REACTION_SLIP_JUMP == visionReaction) {
                jumpedOrNot = false;
                jumpHoldingRdfCnt = 0;
                slipJumpedOrNot = true;
            } else if (OPPONENT_REACTION_FOLLOW == visionReaction) {
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
                        bool notDashing = (Dashing != currCharacterDownsync.CharacterState && Sliding != currCharacterDownsync.CharacterState && BackDashing != currCharacterDownsync.CharacterState);
                        float visionThresholdPortion = 0.95f;
                        bool veryFarAway = (0 < oppoChColliderDx ? (oppoChCollider.X > (visionCollider.X + visionThresholdPortion*visionCollider.W)) : (oppoChCollider.X < visionCollider.X + (1-visionThresholdPortion)*visionCollider.W));
                        if (notDashing && veryFarAway) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                patternId = PATTERN_DOWN_A;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                patternId = PATTERN_DOWN_A;
                            }
                        }
                    }
                }
            } else if (OPPONENT_REACTION_FLEE == visionReaction) {
                if (currCharacterDownsync.OmitGravity || chConfig.OmitGravity) {
                    if (0 >= currCharacterDownsync.FramesToRecover) {
                        var magSqr = oppoChColliderDx * oppoChColliderDx + oppoChColliderDy * oppoChColliderDy;
                        var invMag = InvSqrt32(magSqr);

                        float normX = -oppoChColliderDx * invMag, normY = -oppoChColliderDy * invMag;
                        var (effDx, effDy, _) = DiscretizeDirection(normX, normY);
                        effectiveDx = effDx;
                        effectiveDy = effDy;
                    }
                } else {
                    if (opponentBehindMe) {
                        // DO NOTHING, just continue walking
                        bool notDashing = (Dashing != currCharacterDownsync.CharacterState && Sliding != currCharacterDownsync.CharacterState && BackDashing != currCharacterDownsync.CharacterState);
                        bool notBackDashingSpecies = !(SPECIES_WITCHGIRL == currCharacterDownsync.SpeciesId || SPECIES_BRIGHTWITCH == currCharacterDownsync.SpeciesId);
                        if (notDashing && notBackDashingSpecies) {
                            if (chConfig.SlidingEnabled && !effInAir) {
                                patternId = PATTERN_DOWN_A;
                            } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                                patternId = PATTERN_DOWN_A;
                            }
                        }
                    } else {
                        bool notBackDashing = (BackDashing != currCharacterDownsync.CharacterState);
                        if (!effInAir && notBackDashing && (SPECIES_WITCHGIRL == currCharacterDownsync.SpeciesId || SPECIES_BRIGHTWITCH == currCharacterDownsync.SpeciesId)) {
                            patternId = PATTERN_DOWN_A;
                        }
                    }
                }
            }
        }

        private static void _handleOppoBl(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider visionCollider, bool effInAir, bool canJumpWithinInertia, Collider? oppoBlCollider, Bullet? v4, ref int patternId, ref bool jumpedOrNot, ref bool slipJumpedOrNot, ref int jumpHoldingRdfCnt, ref int effectiveDx, ref int effectiveDy, ref int visionReaction) {
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
                    visionReaction = OPPONENT_REACTION_DEF1;
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
                        visionReaction = OPPONENT_REACTION_JUMP_TOWARDS;
                        if (canJumpWithinInertia && !effInAir) {
                            jumpedOrNot = true;
                        }
                        if (0 == jumpHoldingRdfCnt) {
                            jumpHoldingRdfCnt = 1;
                        }
                        if (0 < currCharacterDownsync.JumpHoldingRdfCnt && (InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState || InAirIdle2ByJump == currCharacterDownsync.CharacterState)) {
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
                    } else {
                        visionReaction = OPPONENT_REACTION_FOLLOW;
                    }

                } else {
                    // Just don't jump if it's melee incoming bullet
                    bool notDashing = (Dashing != currCharacterDownsync.CharacterState && Sliding != currCharacterDownsync.CharacterState && BackDashing != currCharacterDownsync.CharacterState);
                    if (notDashing) {
                        // Because dashing often has a few invinsible startup frames.
                        if (chConfig.SlidingEnabled && !effInAir) {
                            patternId = PATTERN_DOWN_A;
                        } else if (chConfig.DashingEnabled && (!effInAir || 0 < currCharacterDownsync.RemainingAirDashQuota)) {
                            patternId = PATTERN_DOWN_A;
                        }
                        visionReaction = OPPONENT_REACTION_FOLLOW;
                    }
                }
            }

            if (OPPONENT_REACTION_UNKNOWN == visionReaction) {
                if (canJumpWithinInertia) {
                    if (!effInAir && currCharacterDownsync.PrimarilyOnSlippableHardPushback) {
                        jumpedOrNot = false;
                        slipJumpedOrNot = true;
                        jumpHoldingRdfCnt = 0;
                        visionReaction = OPPONENT_REACTION_SLIP_JUMP;
                    }
                }
            }
        }
        
        private static int frontOpponentReachableByIvSlot(CharacterDownsync currCharacterDownsync, bool effInAir, CharacterConfig chConfig, Collider aCollider, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            InventorySlot? targetSlot = null;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_FIRESWORDMAN:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return OPPONENT_REACTION_UNKNOWN;
                    if (notRecovered) return OPPONENT_REACTION_UNKNOWN;
                    if (effInAir) return OPPONENT_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * FireSwordManFireBreathBl1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + FireSwordManFireBreathBl1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManFireBreathBl1.HitboxSizeX >> 1), (FireSwordManFireBreathBl1.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    break;
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return OPPONENT_REACTION_UNKNOWN;
                    if (notRecovered) return OPPONENT_REACTION_UNKNOWN;
                    if (effInAir) return OPPONENT_REACTION_FOLLOW;
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return OPPONENT_REACTION_NOT_ENOUGH_MP;

                    // A special case
                    return OPPONENT_REACTION_USE_SLOT_C;
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Hp > (chConfig.Hp >> 1)) return OPPONENT_REACTION_UNKNOWN;
                    if (notRecovered) return OPPONENT_REACTION_UNKNOWN;
                    if (effInAir) return OPPONENT_REACTION_FOLLOW;
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return OPPONENT_REACTION_NOT_ENOUGH_MP;

                    return OPPONENT_REACTION_USE_SLOT_C;
                case SPECIES_BOMBERGOBLIN:
                    if (notRecovered) return OPPONENT_REACTION_UNKNOWN;
                    if (effInAir) return OPPONENT_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * (GoblinMelee1PrimerBullet.HitboxOffsetX << 1), currCharacterDownsync.VirtualGridY + GoblinMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((GoblinMelee1PrimerBullet.HitboxSizeX), (GoblinMelee1PrimerBullet.HitboxSizeY >> 1));
                    targetSlot = (currCharacterDownsync.Inventory.Slots[0]);
                    if (0 >= targetSlot.Quota) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    return OPPONENT_REACTION_USE_SLOT_C;
                default:
                    return OPPONENT_REACTION_UNKNOWN;
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
                return OPPONENT_REACTION_USE_SLOT_C;
            } else {
                return OPPONENT_REACTION_FOLLOW;
            }
        }

        private static int frontOpponentReachableByDragonPunch(CharacterDownsync currCharacterDownsync, bool effInAir, CharacterConfig chConfig, bool canJumpWithinInertia, Collider aCollider, Collider bCollider, float colliderDx, float absColliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_SWORDMAN_BOSS:
                case SPECIES_SWORDMAN:
                    if (currCharacterDownsync.Mp < SwordManDragonPunchPrimerSkill.MpDelta) {
                        bool closeEnoughAlt = canJumpWithinInertia && !effInAir && (0.6f * aCollider.H < colliderDy) && (0 <= currCharacterDownsync.DirX * colliderDx);
                        if (closeEnoughAlt) {
                            return OPPONENT_REACTION_JUMP_TOWARDS;
                        } else {
                            return OPPONENT_REACTION_NOT_ENOUGH_MP;
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
                            return OPPONENT_REACTION_JUMP_TOWARDS;
                        } else {
                            return OPPONENT_REACTION_NOT_ENOUGH_MP;
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
                    if (currCharacterDownsync.Mp < RisingPurpleArrowSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 < colliderDy && absColliderDy > 0.8f * (bCollider.H-aCollider.H)); // A special case
                    break;
                case SPECIES_DARKBEAMTOWER:
                    if (currCharacterDownsync.Mp < RisingPurpleArrowSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 < colliderDy && absColliderDy > 1.8f * chConfig.DefaultSizeY); // A special case
                    break;
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Mp < StoneRollSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 < colliderDy && absColliderDy > 2.2f*aCollider.H); // A special case
                    break;
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Mp < DemonDiverImpactSkill.MpDelta) {
                        return OPPONENT_REACTION_NOT_ENOUGH_MP;
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
                        return OPPONENT_REACTION_JUMP_TOWARDS;
                    } else {
                        return OPPONENT_REACTION_FOLLOW;
                    }
                case SPECIES_BOAR:
                    closeEnough = canJumpWithinInertia && (0.6f*aCollider.H < colliderDy) && (0 <= currCharacterDownsync.DirX*colliderDx);
                    if (closeEnough) {
                        return OPPONENT_REACTION_JUMP_TOWARDS;
                    } else {
                        return OPPONENT_REACTION_FOLLOW;
                    }
                default:
                    return OPPONENT_REACTION_UNKNOWN;
            }
            if (closeEnough) {
                return OPPONENT_REACTION_USE_DRAGONPUNCH;
            } else {
                return OPPONENT_REACTION_FOLLOW;
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
                    if (currCharacterDownsync.Mp < RiderGuardMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * (RiderGuardMelee1PrimerBullet.HitboxOffsetX << 1), currCharacterDownsync.VirtualGridY + RiderGuardMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr(RiderGuardMelee1PrimerBullet.HitboxSizeX, (RiderGuardMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_SWORDMAN_BOSS:
                case SPECIES_SWORDMAN:
                    if (currCharacterDownsync.Mp < SwordManMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    if ((InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return OPPONENT_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * SwordManMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + SwordManMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((SwordManMelee1PrimerBullet.HitboxSizeX >> 1), (SwordManMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_FIRESWORDMAN:
                    if (currCharacterDownsync.Mp < FireSwordManMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    if ((InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return OPPONENT_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * FireSwordManMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + FireSwordManMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManMelee1PrimerBullet.HitboxSizeX >> 1), (FireSwordManMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Mp < DemonFireSlimeMelee1PrimarySkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * DemonFireSlimeMelee1PrimaryBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DemonFireSlimeMelee1PrimaryBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((DemonFireSlimeMelee1PrimaryBullet.HitboxSizeX >> 1), (DemonFireSlimeMelee1PrimaryBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_GOBLIN:
                case SPECIES_BOMBERGOBLIN:
                    if (currCharacterDownsync.Mp < GoblinMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * GoblinMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + GoblinMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((GoblinMelee1PrimerBullet.HitboxSizeX >> 1), (GoblinMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_BOARWARRIOR:
                    if (currCharacterDownsync.Mp < BoarWarriorMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BoarWarriorMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BoarWarriorMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BoarWarriorMelee1PrimerBullet.HitboxSizeX >> 1), (BoarWarriorMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_BOAR:
                    if (currCharacterDownsync.Mp < BoarMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BoarMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BoarMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BoarMelee1PrimerBullet.HitboxSizeX >> 1), (BoarMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_SKELEARCHER:
                    if (currCharacterDownsync.Mp < PurpleArrowPrimarySkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    closeEnough = (absColliderDy < 1.2f * (bCollider.H - aCollider.H)); // A special case
                    if (closeEnough) {
                        return OPPONENT_REACTION_USE_MELEE;
                    } else {
                        return OPPONENT_REACTION_FLEE;
                    }
                case SPECIES_DARKBEAMTOWER:
                    if (currCharacterDownsync.Mp < DarkTowerPrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    return OPPONENT_REACTION_USE_MELEE;
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Mp < StoneSwordSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    closeEnough = (0 < colliderDy && absColliderDy < 1.2f * aCollider.H) && (absColliderDx < 5.0f * aCollider.W); // A special case
                    if (closeEnough) {
                        return OPPONENT_REACTION_USE_MELEE;
                    } else {
                        return OPPONENT_REACTION_FLEE;
                    }
                case SPECIES_BAT:
                case SPECIES_FIREBAT:
                    if (currCharacterDownsync.Mp < BatMelee1PrimerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * BatMelee1PrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + BatMelee1PrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BatMelee1PrimerBullet.HitboxSizeX >> 1), (BatMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_RIDLEYDRAKE:
                    if (effInAir && (IN_AIR_DASH_GRACE_PERIOD_RDF_CNT << 1) > currCharacterDownsync.FramesInChState) return OPPONENT_REACTION_FOLLOW;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac * RidleyMeleeBulletHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + RidleyMeleeBulletHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((RidleyMeleeBulletHit1.HitboxSizeX >> 1), (RidleyMeleeBulletHit1.HitboxSizeY >> 1));
                    break;
                default:
                    return OPPONENT_REACTION_UNKNOWN;
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
                return OPPONENT_REACTION_USE_MELEE;
            } else {
                return OPPONENT_REACTION_FOLLOW;
            }
        }

        private static int frontOpponentReachableByFireball(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, Collider aCollider, Collider bCollider, float colliderDx, float colliderDy, float absColliderDy, float opponentBoxLeft, float opponentBoxRight, float opponentBoxBottom, float opponentBoxTop) {
            // Whenever there's an opponent in vision, it's deemed already close enough for fireball
            int xfac = (0 < colliderDx ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            bool closeEnough = false;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_RIDLEYDRAKE:
                    if (currCharacterDownsync.Mp < HeatBeamSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*WaterballBulletAirHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + WaterballBulletAirHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((WaterballBulletAirHit1.HitboxSizeX >> 1), (WaterballBulletAirHit1.HitboxSizeY >> 1));
                    break;
                case SPECIES_FIRESWORDMAN:
                    if (currCharacterDownsync.Mp < FireSwordManFireballSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*FireSwordManFireballPrimerBullet.HitboxOffsetX, currCharacterDownsync.VirtualGridY + FireSwordManFireballPrimerBullet.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManFireballPrimerBullet.HitboxSizeX << 1), (FireSwordManFireballPrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_FIREBAT:
                    if (currCharacterDownsync.Mp < DroppingFireballSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX + xfac*DroppingFireballHit1.HitboxOffsetX, currCharacterDownsync.VirtualGridY + DroppingFireballHit1.HitboxOffsetY);
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr(0, (DroppingFireballHit1.HitboxSizeY >> 1));
                    // A special case
                    if (0 > colliderDy) {
                        return OPPONENT_REACTION_USE_FIREBALL;
                    } else {
                        return OPPONENT_REACTION_FOLLOW;
                    }
                case SPECIES_DEMON_FIRE_SLIME:
                    if (currCharacterDownsync.Mp < DemonFireBreathSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
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
                        return OPPONENT_REACTION_USE_FIREBALL;
                    } else {
                        return OPPONENT_REACTION_FOLLOW;
                    }
                case SPECIES_STONE_GOLEM:
                    if (currCharacterDownsync.Mp < StoneDropperSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;
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
                        return OPPONENT_REACTION_USE_FIREBALL;
                    } else {
                        return OPPONENT_REACTION_FOLLOW;
                    }
                case SPECIES_SKELEARCHER:
                    if (currCharacterDownsync.Mp < FallingPurpleArrowSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 > colliderDy && absColliderDy > 0.5f * (bCollider.H-aCollider.H)); // A special case
                    if (closeEnough) {
                        return OPPONENT_REACTION_USE_FIREBALL;
                    } else {
                        return OPPONENT_REACTION_FLEE;
                    }
                case SPECIES_DARKBEAMTOWER:
                    if (currCharacterDownsync.Mp < DarkTowerLowerSkill.MpDelta) return OPPONENT_REACTION_NOT_ENOUGH_MP;         
                    closeEnough = (0 > colliderDy && absColliderDy > 1.8f * chConfig.DefaultSizeY); // A special case
                    if (closeEnough) {
                        return OPPONENT_REACTION_USE_FIREBALL;
                    } else {
                        return OPPONENT_REACTION_USE_MELEE;
                    }
                default:
                    return OPPONENT_REACTION_UNKNOWN;
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
                return OPPONENT_REACTION_USE_FIREBALL;
            } else {
                return OPPONENT_REACTION_FOLLOW;
            }
        }

        public static bool isNpcDeadToDisappear(CharacterDownsync currCharacterDownsync) {
            return (0 >= currCharacterDownsync.Hp && 0 >= currCharacterDownsync.FramesToRecover);
        }

        public static bool isNpcJustDead(CharacterDownsync currCharacterDownsync) {
            return (0 >= currCharacterDownsync.Hp && DYING_FRAMES_TO_RECOVER == currCharacterDownsync.FramesToRecover);
        }

        private static (int, bool, bool, int, int, int) deriveNpcOpPattern(CharacterDownsync currCharacterDownsync, bool effInAir, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, Collision collision, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            if (noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0, 0);
            }

            bool interrupted = _processDebuffDuringInput(currCharacterDownsync);
            if (interrupted) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0, 0);
            }

            if (Def1 == currCharacterDownsync.CharacterState && NPC_DEF1_MIN_HOLDING_RDF_CNT > currCharacterDownsync.FramesInChState) {
                // Such that Def1 is more visible
                return (PATTERN_ID_NO_OP, false, false, 0, 0, +2);
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

            // By default keeps the movement aligned with current facing
            int effectiveDx = currCharacterDownsync.DirX;
            int effectiveDy = currCharacterDownsync.DirY;
            int visionReaction = OPPONENT_REACTION_UNKNOWN;
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

            bool shouldCheckVisionCollision = (0 < chConfig.VisionSizeX && 0 < chConfig.VisionSizeY && (visionSearchTickWhenNonAtk || hasCancellableCombo));
            if (shouldCheckVisionCollision) {
                float visionCx, visionCy, visionCw, visionCh;
                calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);

                var visionCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(visionCollider, visionCx, visionCy, visionCw, visionCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync, COLLISION_VISION_INDEX_PREFIX);
                collisionSys.AddSingleToCellTail(visionCollider);

                Collider? oppoChCollider;
                CharacterDownsync? v3;

                Collider? oppoBlCollider;
                Bullet? v4;
                findHorizontallyClosestCharacterCollider(rdfId, currCharacterDownsync, visionCollider, aCollider, collision, ref overlapResult, out oppoChCollider, out v3, out oppoBlCollider, out v4, logger);

                float oppoChColliderDx = 0f, oppoChColliderDy = 0f;
                if (chConfig.NpcPrioritizeBulletHandling) {
                    _handleOppoBl(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, canJumpWithinInertia, oppoBlCollider, v4, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                    if (OPPONENT_REACTION_UNKNOWN == visionReaction) {
                        _handleOppoCh(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, canJumpWithinInertia, oppoChCollider, v3, ref oppoChColliderDx, ref oppoChColliderDy, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                    }
                } else {
                    _handleOppoCh(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, canJumpWithinInertia, oppoChCollider, v3, ref oppoChColliderDx, ref oppoChColliderDy, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction); 
                    if (OPPONENT_REACTION_UNKNOWN == visionReaction) {
                        _handleOppoBl(currCharacterDownsync, chConfig, aCollider, visionCollider, effInAir, canJumpWithinInertia, oppoBlCollider, v4, ref patternId, ref jumpedOrNot, ref slipJumpedOrNot, ref jumpHoldingRdfCnt, ref effectiveDx, ref effectiveDy, ref visionReaction);
                    }
                }

                collisionSys.RemoveSingleFromCellTail(visionCollider); // no need to increment "colliderCnt", the visionCollider is transient
                visionCollider.Data = null;
            }

            if (OPPONENT_REACTION_UNKNOWN != visionReaction && OPPONENT_REACTION_FLEE != visionReaction && OPPONENT_REACTION_FOLLOW != visionReaction && PATTERN_ID_NO_OP != patternId) {
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
                        slipJumpedOrNot = (0 == currCharacterDownsync.FramesToRecover) && ((currCharacterDownsync.PrimarilyOnSlippableHardPushback || (effInAir && currCharacterDownsync.OmitGravity && !chConfig.OmitGravity)) && 0 < decodedInputHolder.Dy && 0 == decodedInputHolder.Dx) && (0 < decodedInputHolder.BtnALevel);
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
                    findHorizontallyClosestPatrolCueCollider(currCharacterDownsync, aCollider, collision, ref overlapResult, out pCollider, out ptrlCue); 

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
                if (OPPONENT_REACTION_UNKNOWN == visionReaction && false == hasPatrolCueReaction) {
                    if (0 != currCharacterDownsync.CachedCueCmd && canJumpWithinInertia) {
                        // [WARNING] "canJumpWithinInertia" implies "(0 == currCharacterDownsync.FramesToRecover)"
                        thatCharacterInNextFrame.CachedCueCmd = 0;
                        DecodeInput(currCharacterDownsync.CachedCueCmd, decodedInputHolder);
                        effectiveDx = decodedInputHolder.Dx;
                        effectiveDy = decodedInputHolder.Dy;
                        slipJumpedOrNot = (0 == currCharacterDownsync.FramesToRecover) && ((currCharacterDownsync.PrimarilyOnSlippableHardPushback || (effInAir && currCharacterDownsync.OmitGravity && !chConfig.OmitGravity)) && 0 < decodedInputHolder.Dy && 0 == decodedInputHolder.Dx) && (0 < decodedInputHolder.BtnALevel);
                        jumpedOrNot = !slipJumpedOrNot && (0 == currCharacterDownsync.FramesToRecover) && !effInAir && (0 < decodedInputHolder.BtnALevel);

                        if (0 >= chConfig.JumpingInitVelY) {
                            slipJumpedOrNot = false;
                            jumpHoldingRdfCnt = 0;
                            jumpedOrNot = false;
                        } else if (jumpedOrNot) {
                            if (0 == jumpHoldingRdfCnt) {
                                jumpHoldingRdfCnt = 1;
                            }
                            if (0 < currCharacterDownsync.JumpHoldingRdfCnt && (InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState || InAirIdle2ByJump == currCharacterDownsync.CharacterState)) {
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
                        return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effectiveDx, effectiveDy);
                    }
                    if (chConfig.JumpHoldingToFly && InAirIdle1ByJump == currCharacterDownsync.CharacterState) {
                        return (PATTERN_ID_NO_OP, false, false, currCharacterDownsync.JumpHoldingRdfCnt+1, currCharacterDownsync.DirX, currCharacterDownsync.DirY);
                    }
                    if (TERMINATING_TRIGGER_ID != currCharacterDownsync.SubscribesToTriggerLocalId) {
                        return (PATTERN_ID_NO_OP, false, false, 0, 0, 0);
                    }
                    if (chConfig.AntiGravityWhenIdle && (Idle1 == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState)) {
                        return (PATTERN_ID_NO_OP, false, false, 0, 0, 0);
                    }
                    bool possiblyWalking = (Walking == currCharacterDownsync.CharacterState || InAirWalking == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState || InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState || InAirIdle2ByJump == currCharacterDownsync.CharacterState); // Including walking equivalents in air
                    bool inPatrolCueCtrl = (false == currCharacterDownsync.CapturedByPatrolCue && 0 < currCharacterDownsync.FramesInPatrolCue);
                    bool possiblyCrouching = isCrouching(currCharacterDownsync.CharacterState, chConfig);
                    if (currCharacterDownsync.WaivingSpontaneousPatrol) {
                        if (possiblyWalking && !visionSearchTick) {
                            // [WARNING] Such that for "currCharacterDownsync.WaivingSpontaneousPatrol" it doesn't have to execute vision reaction again to trace the same opponent.
                            if (effInAir && chConfig.NpcNoDefaultAirWalking && !inPatrolCueCtrl) {
                                effectiveDx = 0;
                            }
                            /*
                            if (SPECIES_SKELEARCHER == currCharacterDownsync.SpeciesId) {
                                logger.LogInfo(String.Format("@rdfId={0}, ch.Id={1}, possiblyWalking, for now effectiveDx={2}, currCharacterDownsync.CharacterState={3}, effInAir={4}, framesToStartJump={5}", rdfId, currCharacterDownsync.Id, effectiveDx, currCharacterDownsync.CharacterState, effInAir, currCharacterDownsync.FramesToStartJump));
                            }
                            */
                            return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effectiveDx, effectiveDy);
                        } else if (possiblyCrouching && !visionSearchTick) {
                            return (PATTERN_ID_NO_OP, false, false, 0, 0, -2);
                        } else {
                            return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0, 0);
                        }
                    } else {
                        if (possiblyCrouching) {
                            return (PATTERN_ID_NO_OP, false, false, 0, 0, -2);
                        } else {
                            if (effInAir && chConfig.NpcNoDefaultAirWalking && !inPatrolCueCtrl) {
                                return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, 0, currCharacterDownsync.DirY);
                            } else {
                                return (PATTERN_ID_NO_OP, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, currCharacterDownsync.DirX, currCharacterDownsync.DirY);
                            } 
                        }
                    }
                }
            }

            return (patternId, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effectiveDx, effectiveDy);
        }

        private static void _processNpcInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, int colliderCnt, Collision collision, CollisionSpace collisionSys, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = roomCapacity; i < roomCapacity + npcCnt; i++) {
                var currCharacterDownsync = currRenderFrame.NpcsArr[i - roomCapacity];
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = nextRenderFrameNpcs[i - roomCapacity];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                bool effInAir = (currCharacterDownsync.InAir || inAirSet.Contains(currCharacterDownsync.CharacterState));
                var (patternId, jumpedOrNot, slipJumpedOrNot, jumpHoldingRdfCnt, effDx, effDy) = deriveNpcOpPattern(currCharacterDownsync, effInAir, currRenderFrame, roomCapacity, chConfig, thatCharacterInNextFrame, dynamicRectangleColliders, colliderCnt, collisionSys, collision, ref overlapResult, decodedInputHolder, logger);

                var (slotUsed, slotLockedSkillId) = _useInventorySlot(currRenderFrame.Id, patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, logger);

                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;
                thatCharacterInNextFrame.SlipJumpTriggered |= slipJumpedOrNot;
                thatCharacterInNextFrame.JumpHoldingRdfCnt = jumpHoldingRdfCnt;

                if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 > currCharacterDownsync.JumpHoldingRdfCnt && JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= thatCharacterInNextFrame.JumpHoldingRdfCnt && !thatCharacterInNextFrame.OmitGravity && chConfig.JumpHoldingToFly) {
                    thatCharacterInNextFrame.OmitGravity = true;
                    if (0 >= thatCharacterInNextFrame.VelY) {       
                        thatCharacterInNextFrame.VelY = 0;
                    }
                }

                bool usedSkill = _useSkill(effDx, effDy, patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets, slotUsed, slotLockedSkillId, logger);
                Skill? skillConfig = null;
                if (usedSkill) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0; // The use of a skill should break "CapturedByInertia"
                    resetJumpStartupOrHolding(thatCharacterInNextFrame, true);
                    if (Dashing != thatCharacterInNextFrame.CharacterState && BackDashing != thatCharacterInNextFrame.CharacterState && Sliding != thatCharacterInNextFrame.CharacterState) {
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
                _processNextFrameJumpStartup(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effInAir, chConfig, logger);
                if (!currCharacterDownsync.OmitGravity && !chConfig.OmitGravity) {
                    _processInertiaWalking(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, chConfig, true, usedSkill, skillConfig, logger); // TODO: When breaking free from a PatrolCue, an NPC often couldn't turn around from a cliff in time, thus using "shouldIgnoreInertia" temporarily
                } else {
                    _processInertiaFlying(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, chConfig, true, usedSkill, skillConfig, logger);
                    if (PATTERN_ID_UNABLE_TO_OP != patternId && chConfig.AntiGravityWhenIdle && (Walking == thatCharacterInNextFrame.CharacterState || InAirWalking == thatCharacterInNextFrame.CharacterState) && chConfig.AntiGravityFramesLingering < thatCharacterInNextFrame.FramesInChState) {
                        thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                        thatCharacterInNextFrame.FramesInChState = 0;
                        thatCharacterInNextFrame.VelX = 0;
                    }
                }
                _processDelayedBulletSelfVel(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, chConfig, logger);
            }
        }

        protected static bool addNewNpcToNextFrame(int rdfId, int virtualGridX, int virtualGridY, int dirX, int dirY, uint characterSpeciesId, int teamId, bool isStatic, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, int publishingToTriggerLocalIdUponKilled, ulong waveNpcKilledEvtMaskCounter, int subscribesToTriggerLocalId) {
            var chConfig = characters[characterSpeciesId];
            int birthVirtualX = virtualGridX + ((chConfig.DefaultSizeX >> 2) * dirX);
            CharacterState initChState = chConfig.OmitGravity ? Walking : Idle1;
            AssignToCharacterDownsync(npcLocalIdCounter, characterSpeciesId, birthVirtualX, virtualGridY, dirX, dirY, 0, 0, 0, 0, 0, 0, NO_SKILL, NO_SKILL_HIT, 0, chConfig.Speed, initChState, npcCnt, chConfig.Hp, true, false, 0, 0, 0, teamId, teamId, birthVirtualX, virtualGridY, dirX, dirY, false, false, false, false, 0, 0, 0, chConfig.Mp, chConfig.OmitGravity, chConfig.OmitSoftPushback, chConfig.RepelSoftPushback, isStatic, 0, false, false, false, newBirth: true, false, 0, 0, 0, defaultTemplateBuffList, defaultTemplateDebuffList, prevInventory: null, false, publishingToTriggerLocalIdUponKilled, waveNpcKilledEvtMaskCounter, subscribesToTriggerLocalId, 0, 0, chConfig.DefaultAirJumpQuota, chConfig.DefaultAirDashQuota, TERMINATING_CONSUMABLE_SPECIES_ID, TERMINATING_BUFF_SPECIES_ID, NO_SKILL, defaultTemplateBulletImmuneRecords, 0, 0, 0, MAGIC_JOIN_INDEX_INVALID, TERMINATING_BULLET_TEAM_ID, rdfId, 0, nextRenderFrameNpcs[npcCnt]); // TODO: Support killedToDropConsumable/Buff here

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
                var thatCharacterInNextFrame = (i < roomCapacity ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
                int j = thatCharacterInNextFrame.LastDamagedByJoinIndex;
                if (j >= roomCapacity) {
                    // no need to remap for players
                    if (justDeadJoinIndices.Contains(j)) {
                        thatCharacterInNextFrame.LastDamagedByJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(j)) {
                        thatCharacterInNextFrame.LastDamagedByJoinIndex = joinIndexRemap[j];
                    }
                }
            }

            for (int i = 0; i < nextRdfBullets.Count; i++) {
                var src = nextRdfBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BulletLocalId) break;
                int j = src.OffenderJoinIndex;
                if (j >= roomCapacity) {
                    // no need to remap for players
                    if (justDeadJoinIndices.Contains(j)) {
                        src.OffenderJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(j)) {
                        src.OffenderJoinIndex = joinIndexRemap[j];
                    }
                }
                int k = src.TargetCharacterJoinIndex;
                if (k >= roomCapacity) {    
                    // no need to remap for players
                    if (justDeadJoinIndices.Contains(k)) {
                        src.TargetCharacterJoinIndex = MAGIC_JOIN_INDEX_INVALID;
                    } else if (joinIndexRemap.ContainsKey(k)) {
                        src.TargetCharacterJoinIndex = joinIndexRemap[k];
                    }
                }
            }
        }
    }
}
