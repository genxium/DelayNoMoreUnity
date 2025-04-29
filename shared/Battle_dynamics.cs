using System;
using static shared.CharacterState;
using System.Collections.Generic;
using Google.Protobuf.Collections;

namespace shared {
    public partial class Battle {
        private static void transitToGroundDodgedChState(CharacterDownsync chdNextFrame, CharacterConfig chConfig, bool isParalyzed) {
            CharacterState oldNextChState = chdNextFrame.CharacterState; 
            chdNextFrame.CharacterState = GroundDodged;
            chdNextFrame.FramesInChState = 0;
            chdNextFrame.FramesToRecover = chConfig.GroundDodgedFramesToRecover;
            chdNextFrame.FramesInvinsible = chConfig.GroundDodgedFramesInvinsible;
            chdNextFrame.FramesCapturedByInertia = 0;
            chdNextFrame.ActiveSkillId = NO_SKILL;
            chdNextFrame.ActiveSkillHit = NO_SKILL_HIT;
            if (0 == chdNextFrame.VelX) {
                var effSpeed = (0 >= chConfig.GroundDodgedSpeed ? chConfig.Speed : chConfig.GroundDodgedSpeed);                
                if (BackDashing == oldNextChState) {
                    chdNextFrame.VelX = (0 > chdNextFrame.DirX ? effSpeed : -effSpeed);
                } else {
                    chdNextFrame.VelX = (0 < chdNextFrame.DirX ? effSpeed : -effSpeed);
                }
            }

            if (isParalyzed) {
                chdNextFrame.VelX = 0;
            }

            resetJumpStartup(chdNextFrame);
        }

        public static bool ShouldGenerateInputFrameUpsync(int renderFrameId) {
            return ((renderFrameId & ((1 << INPUT_SCALE_FRAMES) - 1)) == 0);
        }

        public static (bool, int) ShouldPrefabInputFrameDownsync(int prevRenderFrameId, int renderFrameId) {
            for (int i = prevRenderFrameId + 1; i <= renderFrameId; i++) {
                if ((0 <= i) && ShouldGenerateInputFrameUpsync(i)) {
                    return (true, i);
                }
            }
            return (false, -1);
        }

        // "inputFrameId" generation with dynamic "localExtraInputDelayFrames" starts 
        public static int ConvertToDynamicallyGeneratedDelayInputFrameId(int renderFrameId, int localExtraInputDelayFrames) {
            return ((renderFrameId+localExtraInputDelayFrames) >> INPUT_SCALE_FRAMES);
        }
        // "inputFrameId" generation with dynamic "localExtraInputDelayFrames" ends 

        // "renderFrameId" <-> "to use inputFrameId" with fixed "(standard) INPUT_DELAY_FRAMES" starts
        public static int ConvertToDelayedInputFrameId(int renderFrameId) {
            if (renderFrameId < INPUT_DELAY_FRAMES) {
                return 0;
            }
            return ((renderFrameId - INPUT_DELAY_FRAMES) >> INPUT_SCALE_FRAMES);
        }

        public static int ConvertToFirstUsedRenderFrameId(int inputFrameId) {
            return ((inputFrameId << INPUT_SCALE_FRAMES) + INPUT_DELAY_FRAMES);
        }

        public static int ConvertToLastUsedRenderFrameId(int inputFrameId) {
            return ((inputFrameId << INPUT_SCALE_FRAMES) + INPUT_DELAY_FRAMES + (1 << INPUT_SCALE_FRAMES) - 1);
        }
        // "renderFrameId" <-> "to use inputFrameId" with fixed "(standard) INPUT_DELAY_FRAMES" ends

        public static bool EqualInputLists(RepeatedField<ulong> lhs, RepeatedField<ulong> rhs) {
            if (null == lhs || null == rhs) return false;
            if (lhs.Count != rhs.Count) return false;
            for (int i = 0; i < lhs.Count; i++) {
                if (lhs[i] == rhs[i]) continue;
                return false;
            }
            return true;
        }

        public static bool EqualInputLists(RepeatedField<ulong> lhs, ulong[] rhs) {
            if (null == lhs) return false;
            if (lhs.Count != rhs.Length) return false;
            for (int i = 0; i < lhs.Count; i++) {
                if (lhs[i] == rhs[i]) continue;
                return false;
            }
            return true;
        }

        public static bool UpdateInputFrameInPlaceUponDynamics(RoomDownsyncFrame currRdf, FrameRingBuffer<InputFrameDownsync> inputBuffer, int inputFrameId, int lastAllConfirmedInputFrameId, int roomCapacity, ulong confirmedList, RepeatedField<ulong> inputList, int[] lastIndividuallyConfirmedInputFrameId, ulong[] lastIndividuallyConfirmedInputList, int toExcludeJoinIndex, HashSet<int> disconnectedPeerJoinIndices, ILoggerBridge logger) {
            bool hasInputFrameUpdatedOnDynamics = false;
            if (inputFrameId <= lastAllConfirmedInputFrameId) {
                return hasInputFrameUpdatedOnDynamics;
            }
            for (int i = 0; i < roomCapacity; i++) {
                int joinIndex = (i + 1); 
                if (joinIndex == toExcludeJoinIndex) {
                    // On frontend, a "self input" is only confirmed by websocket downsync, which is quite late and might get the "self input" incorrectly overwritten if not excluded here
                    continue;
                }
                ulong newVal = inputList[i];
                ulong joinMask = (1UL << i);
                var chDownsync = currRdf.PlayersArr[i];
                // [WARNING] Try not to predict either a "rising edge" or a "falling edge" of any critical button on frontend.
                bool shouldPredictBtnAHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.JumpHoldingRdfCnt) || (0 < chDownsync.JumpHoldingRdfCnt);
                bool shouldPredictBtnBHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnBHoldingRdfCount) || (0 < chDownsync.BtnBHoldingRdfCount);
                bool shouldPredictBtnCHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnCHoldingRdfCount) || (0 < chDownsync.BtnCHoldingRdfCount);
                bool shouldPredictBtnDHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnDHoldingRdfCount) || (0 < chDownsync.BtnDHoldingRdfCount);
                bool shouldPredictBtnEHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnEHoldingRdfCount) || (0 < chDownsync.BtnEHoldingRdfCount);
              
                if (0 < (confirmedList & joinMask)) {
                    // This in-place update is only valid when "delayed input for this player is not yet confirmed"
                } else if (lastIndividuallyConfirmedInputFrameId[i] == inputFrameId) {
                    // Received from UDP, better than local prediction though "inputFrameDownsync.ConfirmedList" is not set till confirmed by TCP path
                    newVal = lastIndividuallyConfirmedInputList[i];
                } else {
                    // Local prediction
                    ulong refCmd = 0;
                    var (_, previousInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId - 1);
                    if (lastIndividuallyConfirmedInputFrameId[i] < inputFrameId) {
                        refCmd = lastIndividuallyConfirmedInputList[i];
                    } else if (null != previousInputFrameDownsync) {
                        // lastIndividuallyConfirmedInputFrameId[i] > inputFrameId
                        refCmd = previousInputFrameDownsync.InputList[i];
                    }

                    newVal = (refCmd & 15UL);
                    if (shouldPredictBtnAHold) newVal |= (refCmd & 16UL);
                    if (shouldPredictBtnBHold) newVal |= (refCmd & 32UL);
                    if (shouldPredictBtnCHold) newVal |= (refCmd & 64UL);
                    if (shouldPredictBtnDHold) newVal |= (refCmd & 128UL);
                    if (shouldPredictBtnEHold) newVal |= (refCmd & 256UL);
                }

                /*
                if (shouldPredictBtnBHold && 0 == (newVal & 32UL)) {
                    logger.LogInfo($"currRdfId={currRdf.Id}, inputFrameId={inputFrameId}, lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, orig inputList[jidx-1:{i}]={inputList[i]}, confirmedList={confirmedList}, newVal={newVal}, lastIndividuallyConfirmedInputFrameId[jidx-1:{i}]={lastIndividuallyConfirmedInputFrameId[i]}, lastIndividuallyConfirmedInputList[jidx-1:{i}]={lastIndividuallyConfirmedInputList[i]}, predicted a falling edge!\n\tchDownsync={stringifyPlayer(chDownsync)}");
                }
                */

                if (newVal != inputList[i]) {
                    inputList[i] = newVal;
                    hasInputFrameUpdatedOnDynamics = true;
                }
            }
            return hasInputFrameUpdatedOnDynamics;
        }
    
        private static bool IsInBlockStun(CharacterDownsync currCharacterDownsync) {
            return (Def1 == currCharacterDownsync.CharacterState && 0 < currCharacterDownsync.FramesToRecover);
        }

        private static bool cmdPatternContainsEdgeTriggeredBtnE(int patternId) {
            switch (patternId) {
            case PATTERN_E:
            case PATTERN_DOWN_E:
            case PATTERN_UP_E:
            case PATTERN_FRONT_E:
            case PATTERN_BACK_E:
            case PATTERN_E_HOLD_B:
            case PATTERN_DOWN_E_HOLD_B:
            case PATTERN_UP_E_HOLD_B:
            case PATTERN_FRONT_E_HOLD_B:
            case PATTERN_BACK_E_HOLD_B:
                return true;
            default:
                return false;
            }
        }

        private static (int, bool, bool, int, int) _deriveCharacterOpPattern(int rdfId, CharacterDownsync currCharacterDownsync, InputFrameDecoded decodedInputHolder, CharacterConfig chConfig, bool currEffInAir, bool notDashing, ILoggerBridge logger) {
            bool jumpedOrNot = false;
            bool slipJumpedOrNot = false;
            int effDx = 0, effDy = 0;

            // Jumping is partially allowed within "CapturedByInertia", but moving is only allowed when "0 == FramesToRecover" (constrained later in "Step")
            if (0 >= currCharacterDownsync.FramesToRecover) {
                effDx = decodedInputHolder.Dx;
                effDy = decodedInputHolder.Dy;
            } else if (!currCharacterDownsync.InAir && 1 >= currCharacterDownsync.FramesToRecover && 0 > decodedInputHolder.Dy && chConfig.CrouchingEnabled) {
                // Direction control is respected since "1 == currCharacterDownsync.FramesToRecover" to favor smooth crouching transition
                effDx = decodedInputHolder.Dx;
                effDy = decodedInputHolder.Dy;
            } else if (WalkingAtk1 == currCharacterDownsync.CharacterState) {
                effDx = decodedInputHolder.Dx;
            } else if (IsInBlockStun(currCharacterDownsync)) {
                // Reserve only "effDy" for later use by "_useSkill", e.g. to break free from block-stun by certain skills.
                effDy = decodedInputHolder.Dy;
            }

            int patternId = PATTERN_ID_NO_OP;
            int effFrontOrBack = (decodedInputHolder.Dx*currCharacterDownsync.DirX); // [WARNING] Deliberately using "decodedInputHolder.Dx" instead of "effDx (which could be 0 in block stun)" here!
            var canJumpWithinInertia = (0 == currCharacterDownsync.FramesToRecover && ((chConfig.InertiaFramesToRecover >> 1) > currCharacterDownsync.FramesCapturedByInertia)) || !notDashing;

            if (0 < decodedInputHolder.BtnALevel) {
                if (0 == currCharacterDownsync.JumpHoldingRdfCnt && canJumpWithinInertia) {
                    if ((currCharacterDownsync.PrimarilyOnSlippableHardPushback || (currCharacterDownsync.InAir && currCharacterDownsync.OmitGravity && !chConfig.OmitGravity)) && (0 > decodedInputHolder.Dy && 0 == decodedInputHolder.Dx)) {
                        slipJumpedOrNot = true;
                    } else if ((!currEffInAir || 0 < currCharacterDownsync.RemainingAirJumpQuota) && (!isCrouching(currCharacterDownsync.CharacterState, chConfig) || !notDashing)) {
                        jumpedOrNot = true;
                    } else if (OnWallIdle1 == currCharacterDownsync.CharacterState) {
                        jumpedOrNot = true;
                    }
                }
            }

            if (PATTERN_ID_NO_OP == patternId) {
                if (0 < decodedInputHolder.BtnBLevel) {
                    if (0 == currCharacterDownsync.BtnBHoldingRdfCount) {
                        if (0 < decodedInputHolder.BtnCLevel) {
                            patternId = PATTERN_INVENTORY_SLOT_BC;
                        } else if (0 > decodedInputHolder.Dy) {
                            patternId = PATTERN_DOWN_B;
                        } else if (0 < decodedInputHolder.Dy) {
                            patternId = PATTERN_UP_B;
                        } else {
                            patternId = PATTERN_B;
                        }
                    } else {
                        patternId = PATTERN_HOLD_B;
                    }
                } else {
                    // 0 >= decodedInputHolder.BtnBLevel
                    if (BTN_B_HOLDING_RDF_CNT_THRESHOLD_2 <= currCharacterDownsync.BtnBHoldingRdfCount) {
                        patternId = PATTERN_RELEASED_B;
                    }
                }
            }

            if (PATTERN_HOLD_B == patternId || PATTERN_ID_NO_OP == patternId) {
                if (0 < decodedInputHolder.BtnELevel && (chConfig.DashingEnabled || chConfig.SlidingEnabled)) {
                    if (0 == currCharacterDownsync.BtnEHoldingRdfCount) {
                        if (notDashing) {
                            if (0 < effFrontOrBack) {
                                patternId = (PATTERN_HOLD_B == patternId ? PATTERN_FRONT_E_HOLD_B : PATTERN_FRONT_E);
                            } else if (0 > effFrontOrBack) {
                                patternId = (PATTERN_HOLD_B == patternId ? PATTERN_BACK_E_HOLD_B : PATTERN_BACK_E);
                                effDx = 0; // [WARNING] Otherwise the character will turn around
                            } else if (0 > decodedInputHolder.Dy) {
                                patternId = (PATTERN_HOLD_B == patternId ? PATTERN_DOWN_E_HOLD_B : PATTERN_DOWN_E);
                            } else if (0 < decodedInputHolder.Dy) {
                                patternId = (PATTERN_HOLD_B == patternId ? PATTERN_UP_E_HOLD_B : PATTERN_UP_E);
                            } else {
                                patternId = (PATTERN_HOLD_B == patternId ? PATTERN_E_HOLD_B : PATTERN_E);
                            }
                        }
                    } else {
                        patternId = (PATTERN_HOLD_B == patternId ? PATTERN_HOLD_E_HOLD_B : PATTERN_HOLD_E);
                    }
                }
            }

            if (PATTERN_ID_NO_OP == patternId) {
                if (0 < decodedInputHolder.BtnCLevel) {
                    if (0 == currCharacterDownsync.BtnCHoldingRdfCount) {
                        patternId = PATTERN_INVENTORY_SLOT_C;
                        if (0 < decodedInputHolder.BtnBLevel) {
                            patternId = PATTERN_INVENTORY_SLOT_BC;
                        }
                    } else {
                        patternId = PATTERN_HOLD_INVENTORY_SLOT_C;
                        if (0 < decodedInputHolder.BtnBLevel && 0 == currCharacterDownsync.BtnBHoldingRdfCount) {
                            patternId = PATTERN_INVENTORY_SLOT_BC;
                        }
                    }
                } else if (0 < decodedInputHolder.BtnDLevel) {
                    if (0 == currCharacterDownsync.BtnDHoldingRdfCount) {
                        patternId = PATTERN_INVENTORY_SLOT_D;
                    } else {
                        patternId = PATTERN_HOLD_INVENTORY_SLOT_D;
                    }
                }
            }

            return (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy);
        }

        private static (int, bool, bool, int, int) _derivePlayerOpPattern(int rdfId, InputFrameDownsync delayedInputFrameDownsync, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, FrameRingBuffer<InputFrameDownsync> inputBuffer, InputFrameDecoded decodedInputHolder, bool currEffInAir, bool notDashing, int selfPlayerJoinIndex, ILoggerBridge logger) {
            // returns (patternId, jumpedOrNot, slipJumpedOrNot, effectiveDx, effectiveDy)
            int j = (currCharacterDownsync.JoinIndex - 1);
            var delayedInputList = delayedInputFrameDownsync.InputList;
            DecodeInput(delayedInputList[j], decodedInputHolder);
            var joinIndexMask = (1u << j);
    
            var delayedConfirmedList = delayedInputFrameDownsync.ConfirmedList;
            var delayedUdpConfirmedList = delayedInputFrameDownsync.UdpConfirmedList; // [WARNING] Only used by frontend, see comment in proto file.
            if (0 == (delayedConfirmedList & joinIndexMask) && 0 == (delayedUdpConfirmedList & joinIndexMask)) {
                removePredictedRisingAndFallingEdgesOfPlayerInput(currCharacterDownsync, decodedInputHolder);
            }

            updateBtnHoldingByInput(currCharacterDownsync, decodedInputHolder, thatCharacterInNextFrame);

            if (noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0);
            }

            bool interrupted = _processDebuffDuringInput(currCharacterDownsync);
            if (interrupted) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0);
            }

            return _deriveCharacterOpPattern(rdfId, currCharacterDownsync, decodedInputHolder, chConfig, currEffInAir, notDashing, logger);
        }

        public static bool isTriggerClickableByMovement(Trigger trigger, TriggerConfigFromTiled configFromTiled, CharacterDownsync ch, int roomCapacity) {
            var triggerConfig = triggerConfigs[configFromTiled.SpeciesId];
            if (TriggerType.TtMovement != triggerConfig.TriggerType) return false;
            if (0 < trigger.FramesToRecover || 0 >= trigger.Quota) return false;
            bool npcTriggerable = false;
            if (TRIGGER_SPECIES_TIMED_WAVE_GROUP_TRIGGER_MV == triggerConfig.SpeciesId ||
                TRIGGER_SPECIES_INDI_WAVE_GROUP_TRIGGER_MV == triggerConfig.SpeciesId ||
                TRIGGER_SPECIES_SYNC_WAVE_GROUP_TRIGGER_MV == triggerConfig.SpeciesId
               ) {
               if (TERMINATING_BULLET_TEAM_ID != trigger.BulletTeamId) {
                    if (trigger.BulletTeamId == ch.BulletTeamId) return false;
                    npcTriggerable = true;
                }
            }
            if (!npcTriggerable && ch.JoinIndex > roomCapacity) {
                return false; 
            }
            return true;
        }

        private static bool _useSkill(int rdfId, int effDx, int effDy, int patternId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, ref int bulletLocalIdCounter, ref int bulletCnt, RepeatedField<Bullet> nextRenderFrameBullets, bool slotUsed, uint slotLockedSkillId, ref bool notEnoughMp, bool isParalyzed, ILoggerBridge logger) {
            if (PATTERN_ID_NO_OP == patternId || PATTERN_ID_UNABLE_TO_OP == patternId) {
                return false;
            }
            if (PATTERN_HOLD_B == patternId) {
                if (NO_SKILL != currCharacterDownsync.ActiveSkillId && chConfig.HasBtnBCharging && IsChargingAtkChState(currCharacterDownsync.CharacterState)) {
                    if (0 >= thatCharacterInNextFrame.FramesToRecover) {
                        var activeSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                        thatCharacterInNextFrame.FramesToRecover = activeSkillConfig.RecoveryFrames;
                    }
                }
                return false;
            }
            var skillId = FindSkillId(patternId, currCharacterDownsync, chConfig, chConfig.SpeciesId, slotUsed, slotLockedSkillId, logger);
            if (NO_SKILL == skillId) return false;

            var skillConfig = skills[skillId];
            if (skillConfig.MpDelta > currCharacterDownsync.Mp) {
                notEnoughMp = true;
                return false;
            }

            thatCharacterInNextFrame.Mp -= skillConfig.MpDelta;
            if (0 >= thatCharacterInNextFrame.Mp) {
                thatCharacterInNextFrame.Mp = 0;
            }

            thatCharacterInNextFrame.DirX = (0 == effDx ? thatCharacterInNextFrame.DirX : effDx); // Upon successful skill use, allow abrupt turn-around regardless of inertia!
            int xfac = (0 < thatCharacterInNextFrame.DirX ? 1 : -1);
            bool hasLockVel = false;

            thatCharacterInNextFrame.ActiveSkillId = skillId;
            thatCharacterInNextFrame.FramesToRecover = skillConfig.RecoveryFrames;

            int activeSkillHit = 1;
            var pivotBulletConfig = skillConfig.Hits[activeSkillHit-1];
            for (int i = 0; i < pivotBulletConfig.SimultaneousMultiHitCnt + 1; i++) {
                if (!addNewBulletToNextFrame(rdfId, rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, isParalyzed, xfac, skillConfig, nextRenderFrameBullets, activeSkillHit, skillId, ref bulletLocalIdCounter, ref bulletCnt, ref hasLockVel, null, null, null, null, currCharacterDownsync.JoinIndex, currCharacterDownsync.BulletTeamId, logger)) break;
                thatCharacterInNextFrame.ActiveSkillHit = activeSkillHit;
                activeSkillHit++;
            }

            if (false == hasLockVel && false == currCharacterDownsync.InAir && !pivotBulletConfig.AllowsWalking) {
                thatCharacterInNextFrame.VelX = 0;
            }

            if (isParalyzed) {
                thatCharacterInNextFrame.VelX = 0;
            }

            thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;
            thatCharacterInNextFrame.FramesInChState = 0; // Must reset "FramesInChState" here to handle the extreme case where a same skill, e.g. "Atk1", is used right after the previous one ended
            if (thatCharacterInNextFrame.FramesInvinsible < pivotBulletConfig.StartupInvinsibleFrames) {
                thatCharacterInNextFrame.FramesInvinsible = pivotBulletConfig.StartupInvinsibleFrames;
            }

            return true;
        }

        private static (bool, uint, bool) _useInventorySlot(int rdfId, int patternId, CharacterDownsync currCharacterDownsync, bool currEffInAir, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, ILoggerBridge logger) {
            bool slotUsed = false;
            bool intendToDodgeInBlockStun = false;
            bool dodgedInBlockStun = false;
            var slotLockedSkillId = NO_SKILL;

            int slotIdx = -1;
            if (PATTERN_INVENTORY_SLOT_C == patternId || PATTERN_INVENTORY_SLOT_BC == patternId) {
                slotIdx = 0;
            } else if (PATTERN_INVENTORY_SLOT_D == patternId) {
                slotIdx = 1;
            } else if (chConfig.UseInventoryBtnB && (PATTERN_B == patternId || PATTERN_DOWN_B == patternId || PATTERN_RELEASED_B == patternId)) {
                slotIdx = 2;
            } else if (IsInBlockStun(currCharacterDownsync)     
                       &&
                       cmdPatternContainsEdgeTriggeredBtnE(patternId)
                       &&
                       !currEffInAir
                      ) {
                slotIdx = 0;
                intendToDodgeInBlockStun = true;
            } else {
                return (false, NO_SKILL, false);
            }

            var targetSlotCurr = currCharacterDownsync.Inventory.Slots[slotIdx];
            var targetSlotNext = thatCharacterInNextFrame.Inventory.Slots[slotIdx];
            if (PATTERN_INVENTORY_SLOT_BC == patternId) {
                // Handle full charge skill usage 
                if (InventorySlotStockType.GaugedMagazineIv != targetSlotCurr.StockType || targetSlotCurr.Quota != targetSlotCurr.DefaultQuota) {
                    return (false, NO_SKILL, false);
                }
                slotLockedSkillId = targetSlotCurr.FullChargeSkillId;

                if (NO_SKILL == slotLockedSkillId && TERMINATING_BUFF_SPECIES_ID == targetSlotCurr.FullChargeBuffSpeciesId) {
                    return (false, NO_SKILL, false);
                }

                // [WARNING] Deliberately allowing full charge skills to be used in "notRecovered" cases
                targetSlotNext.Quota = 0; 
                slotUsed = true;

                // [WARNING] Revert all debuffs
                AssignToDebuff(TERMINATING_DEBUFF_SPECIES_ID, 0, thatCharacterInNextFrame.DebuffList[0]);

                if (TERMINATING_BUFF_SPECIES_ID != targetSlotCurr.FullChargeBuffSpeciesId) {
                    var buffConfig = buffConfigs[targetSlotCurr.FullChargeBuffSpeciesId];
                    ApplyBuffToCharacter(rdfId, buffConfig, currCharacterDownsync, thatCharacterInNextFrame);
                }

                if (NO_SKILL != slotLockedSkillId) {
                    var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                    if (null == currSkillConfig || null == currBulletConfig) return (false, NO_SKILL, false);

                    if (!currBulletConfig.CancellableByInventorySlotC) return (false, NO_SKILL, false);
                    if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return (false, NO_SKILL, false);
                }

                return (slotUsed, slotLockedSkillId, false);
            } else {
                slotLockedSkillId = intendToDodgeInBlockStun ? NO_SKILL : (currCharacterDownsync.InAir ? targetSlotCurr.SkillIdAir : targetSlotCurr.SkillId);

                if (!intendToDodgeInBlockStun && NO_SKILL == slotLockedSkillId && TERMINATING_BUFF_SPECIES_ID == targetSlotCurr.BuffSpeciesId) {
                    return (false, NO_SKILL, false);
                }

                bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
                if (notRecovered && !intendToDodgeInBlockStun) {
                    var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                    if (null == currSkillConfig || null == currBulletConfig) return (false, NO_SKILL, false);

                    if (PATTERN_INVENTORY_SLOT_C == patternId && !currBulletConfig.CancellableByInventorySlotC) return (false, NO_SKILL, false);
                    if (PATTERN_INVENTORY_SLOT_D == patternId && !currBulletConfig.CancellableByInventorySlotD) return (false, NO_SKILL, false);
                    if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return (false, NO_SKILL, false);
                }

                if (InventorySlotStockType.GaugedMagazineIv == targetSlotCurr.StockType) {
                    if (0 < targetSlotCurr.Quota) {
                        targetSlotNext.Quota = targetSlotCurr.Quota - 1; 
                        slotUsed = true;
                        dodgedInBlockStun = intendToDodgeInBlockStun;
                    }
                } else if (InventorySlotStockType.QuotaIv == targetSlotCurr.StockType) {
                    if (0 < targetSlotCurr.Quota) {
                        targetSlotNext.Quota = targetSlotCurr.Quota - 1; 
                        slotUsed = true;
                        dodgedInBlockStun = intendToDodgeInBlockStun;
                    }
                } else if (InventorySlotStockType.TimedIv == targetSlotCurr.StockType) {
                    if (0 == targetSlotCurr.FramesToRecover) {
                        targetSlotNext.FramesToRecover = targetSlotCurr.DefaultFramesToRecover; 
                        slotUsed = true;
                        dodgedInBlockStun = intendToDodgeInBlockStun;
                    }
                } else if (InventorySlotStockType.TimedMagazineIv == targetSlotCurr.StockType) {
                    if (0 < targetSlotCurr.Quota) {
                        targetSlotNext.Quota = targetSlotCurr.Quota - 1; 
                        if (0 == targetSlotNext.Quota) {
                            targetSlotNext.FramesToRecover = targetSlotCurr.DefaultFramesToRecover; 
                            //logger.LogInfo(String.Format("At currRdfId={0}, player joinIndex={1} starts reloading inventoryBtnB", currRdfId, currCharacterDownsync.JoinIndex));
                        }
                        slotUsed = true;
                        dodgedInBlockStun = intendToDodgeInBlockStun;
                    }
                }
        
                if (slotUsed && !intendToDodgeInBlockStun) {
                    if (TERMINATING_BUFF_SPECIES_ID != targetSlotCurr.BuffSpeciesId) {
                        var buffConfig = buffConfigs[targetSlotCurr.BuffSpeciesId];
                        ApplyBuffToCharacter(rdfId, buffConfig, currCharacterDownsync, thatCharacterInNextFrame);
                    }
                }

                return (slotUsed, slotLockedSkillId, dodgedInBlockStun);
            }
        }

        private static void _applyGravity(int rdfId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, ILoggerBridge logger) {
            /*
            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                logger.LogInfo("_applyGravity: currRdfId=" + currRdfId + ", " + stringifyPlayer(currCharacterDownsync));
            }
            */
            if ((Idle1 == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && chConfig.AntiGravityWhenIdle) {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY -= GRAVITY_Y;
                if (thatCharacterInNextFrame.VelY > chConfig.MaxAscendingVelY) {
                    thatCharacterInNextFrame.VelY = chConfig.MaxAscendingVelY;
                }
                return;
            }
            if ((currCharacterDownsync.OmitGravity || chConfig.OmitGravity) && !(Dying == currCharacterDownsync.CharacterState)) {
                return;
            }
            if (!currCharacterDownsync.InAir) {
                return;
            }
            if (
                (isInJumpStartup(thatCharacterInNextFrame, chConfig) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig))
            ) {
                return; 
            }

            if (NO_SKILL != currCharacterDownsync.ActiveSkillId && skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) {
                var skillConfig = skills[currCharacterDownsync.ActiveSkillId];
                if (null != skillConfig.SelfNonStockBuff && skillConfig.SelfNonStockBuff.OmitGravity) {
                    return;
                }
            }

            if (NO_SKILL != thatCharacterInNextFrame.ActiveSkillId && skills.ContainsKey(thatCharacterInNextFrame.ActiveSkillId)) {
                var skillConfig = skills[thatCharacterInNextFrame.ActiveSkillId];
                if (null != skillConfig.SelfNonStockBuff && skillConfig.SelfNonStockBuff.OmitGravity) {
                    return;
                }
            }

            if (OnWallIdle1 == currCharacterDownsync.CharacterState || OnWallAtk1 == currCharacterDownsync.CharacterState) {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY = chConfig.WallSlidingVelY;
            } else if (Dashing == currCharacterDownsync.CharacterState || Dashing == thatCharacterInNextFrame.CharacterState) {
                // Don't apply gravity if will enter dashing state in next frame
                thatCharacterInNextFrame.VelX += GRAVITY_X;
            } else {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY += JUMP_HOLDING_RDF_CNT_THRESHOLD_1 <= currCharacterDownsync.JumpHoldingRdfCnt ? GRAVITY_Y_JUMP_HOLDING : GRAVITY_Y;
                if (thatCharacterInNextFrame.VelY < chConfig.MinFallingVelY) {
                    thatCharacterInNextFrame.VelY = chConfig.MinFallingVelY;
                }
            }
        }

        private static bool _processDebuffDuringInput(CharacterDownsync currCharacterDownsync) {
            if (null == currCharacterDownsync.DebuffList) return false;
            for (int i = 0; i < currCharacterDownsync.DebuffList.Count; i++) {
                Debuff debuff = currCharacterDownsync.DebuffList[i];
                if (TERMINATING_DEBUFF_SPECIES_ID == debuff.SpeciesId) break;
                var debuffConfig = debuffConfigs[debuff.SpeciesId];
                switch (debuffConfig.Type) {
                    case DebuffType.FrozenPositionLocked:
                        if (0 < debuff.Stock) {
                            return true;
                        }
                        break;
                    // [WARNING] PositionLockedOnly (e.g. paralyzed) doesn't stop inputs from propagating!
                }
            }
            return false;
        }

        public static bool isNotDashing(CharacterDownsync chd) {
            return (Dashing != chd.CharacterState && Sliding != chd.CharacterState && BackDashing != chd.CharacterState);
        }

        public static bool isEffInAir(CharacterDownsync chd, bool notDashing) {
            return (chd.InAir || (inAirSet.Contains(chd.CharacterState) && notDashing));
        }
        
        private static void _processSingleCharacterInput(int rdfId, int patternId, bool jumpedOrNot, bool slipJumpedOrNot, int effDx, int effDy, bool slowDownToAvoidOverlap, CharacterDownsync currCharacterDownsync, bool currEffInAir, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, bool shouldIgnoreInertia, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, int selfPlayerJoinIndex, ref bool selfNotEnoughMp, ILoggerBridge logger) {
            // Prioritize use of inventory slot over skills
            var (slotUsed, slotLockedSkillId, dodgedInBlockStun) = _useInventorySlot(rdfId, patternId, currCharacterDownsync, currEffInAir, chConfig, thatCharacterInNextFrame, logger);

            thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;
            thatCharacterInNextFrame.SlipJumpTriggered |= slipJumpedOrNot;
    
            if (JUMP_HOLDING_RDF_CNT_THRESHOLD_2 > currCharacterDownsync.JumpHoldingRdfCnt && JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= thatCharacterInNextFrame.JumpHoldingRdfCnt && !thatCharacterInNextFrame.OmitGravity && chConfig.JumpHoldingToFly && proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                /*
                (a.) The original "hold-only-to-fly" is prone to "falsely predicted flying" due to not being edge-triggered; 
                (b.) However, "releasing BtnA at JUMP_HOLDING_RDF_CNT_THRESHOLD_2 <= currCharacterDownsync.JumpHoldingRdfCnt" makes it counter-intuitive to use when playing, the trade-off is not easy for me...
                */
                //logger.LogInfo($"_processSingleCharacterInput/start, currRdfId={rdfId}, about to fly currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, JumpHoldingRdfCnt: {currCharacterDownsync.JumpHoldingRdfCnt}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, chS: {currCharacterDownsync.CharacterState})");
                thatCharacterInNextFrame.OmitGravity = true;
                thatCharacterInNextFrame.InAir = true;
                thatCharacterInNextFrame.PrimarilyOnSlippableHardPushback = false;
                thatCharacterInNextFrame.FlyingRdfCountdown = chConfig.FlyingQuotaRdfCnt;
                if (0 >= thatCharacterInNextFrame.VelY) { 
                    thatCharacterInNextFrame.VelY = 0;
                }
            }

            var existingDebuff = currCharacterDownsync.DebuffList[DEBUFF_ARR_IDX_ELEMENTAL];
            bool isParalyzed = (TERMINATING_DEBUFF_SPECIES_ID != existingDebuff.SpeciesId && 0 < existingDebuff.Stock && DebuffType.PositionLockedOnly == debuffConfigs[existingDebuff.SpeciesId].Type);
            if (dodgedInBlockStun) {
                transitToGroundDodgedChState(thatCharacterInNextFrame, chConfig, isParalyzed);
            }

            bool notEnoughMp = false;
            bool usedSkill = dodgedInBlockStun ? false : _useSkill(rdfId, effDx, effDy, patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, nextRenderFrameBullets, slotUsed, slotLockedSkillId, ref notEnoughMp, isParalyzed, logger);
            Skill? skillConfig = null;
            
            if (null != chConfig.BtnBAutoUnholdChStates && chConfig.BtnBAutoUnholdChStates.Contains(thatCharacterInNextFrame.CharacterState)) {
                // [WARNING] For "autofire" skills.
                thatCharacterInNextFrame.BtnBHoldingRdfCount = 0;
            }

            if (usedSkill) {
                thatCharacterInNextFrame.FramesCapturedByInertia = 0; // The use of a skill should break "CapturedByInertia"
                thatCharacterInNextFrame.CachedCueCmd = 0; // The use of a skill should clear "CachedCueCmd"
                resetJumpStartup(thatCharacterInNextFrame);
                skillConfig = skills[thatCharacterInNextFrame.ActiveSkillId];
                /*
                if (2 == thatCharacterInNextFrame.ActiveSkillId) {
                    logger.LogInfo(String.Format("@rdfId={0}, used skillId=2 when FramesInChState={1}", currRenderFrame.Id, currCharacterDownsync.FramesInChState));
                }
                */
                if (Dashing == skillConfig.BoundChState && currCharacterDownsync.InAir) {              
                    if (!currCharacterDownsync.OmitGravity && 0 < thatCharacterInNextFrame.RemainingAirDashQuota) {
                        thatCharacterInNextFrame.RemainingAirDashQuota  -= 1;
                        if (!chConfig.IsolatedAirJumpAndDashQuota && 0 < thatCharacterInNextFrame.RemainingAirJumpQuota) {
                            thatCharacterInNextFrame.RemainingAirJumpQuota -= 1;
                        }
                    }
                }
                if (isCrouching(currCharacterDownsync.CharacterState, chConfig) && Atk1 == thatCharacterInNextFrame.CharacterState) {
                    if (chConfig.CrouchingAtkEnabled) {
                        thatCharacterInNextFrame.CharacterState = CrouchAtk1;
                    }
                }
                if (!skillConfig.Hits[0].AllowsWalking) {
                    return; // Don't allow movement if skill is used
                }
            } else if (notEnoughMp && selfPlayerJoinIndex == currCharacterDownsync.JoinIndex) {
                selfNotEnoughMp = true; 
            }

            _processNextFrameJumpStartup(rdfId, currCharacterDownsync, thatCharacterInNextFrame, currEffInAir, chConfig, isParalyzed, logger);
            if (!currCharacterDownsync.OmitGravity && !chConfig.OmitGravity) {
                _processInertiaWalking(rdfId, currCharacterDownsync, thatCharacterInNextFrame, currEffInAir, effDx, effDy, chConfig, shouldIgnoreInertia, usedSkill, skillConfig, isParalyzed, logger);
            } else {
                _processInertiaFlying(rdfId, currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, chConfig, shouldIgnoreInertia, usedSkill, skillConfig, isParalyzed, logger);
            }
            _processDelayedBulletSelfVel(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, isParalyzed, logger);

            if (PATTERN_ID_UNABLE_TO_OP != patternId && chConfig.AntiGravityWhenIdle && (Walking == thatCharacterInNextFrame.CharacterState || InAirWalking == thatCharacterInNextFrame.CharacterState) && chConfig.AntiGravityFramesLingering < thatCharacterInNextFrame.FramesInChState) {
                thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                thatCharacterInNextFrame.FramesInChState = 0;
                thatCharacterInNextFrame.VelX = 0;
                //logger.LogInfo($"_processSingleCharacterInput/end, currRdfId={rdfId}, setting InAirIdle1NoJump after AntiGravityFramesLingering currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, VelX: {currCharacterDownsync.VelX}, VelY: {currCharacterDownsync.VelY}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
            } else if (slowDownToAvoidOverlap) {
                thatCharacterInNextFrame.VelX >>= 2;
                thatCharacterInNextFrame.VelY >>= 2;
            }
        }
        
        private static void _processPlayerInputs(RoomDownsyncFrame currRenderFrame, InputFrameDownsync? delayedInputFrameDownsync, int roomCapacity, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<Bullet> nextRenderFrameBullets, InputFrameDecoded decodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, int selfPlayerJoinIndex, ref bool selfNotEnoughMp, ILoggerBridge logger) {
            if (null == delayedInputFrameDownsync) {
                return;
            }
            for (int i = 0; i < roomCapacity; i++) {
                var currCharacterDownsync = currRenderFrame.PlayersArr[i];
                bool notDashing = isNotDashing(currCharacterDownsync);
                bool currEffInAir = isEffInAir(currCharacterDownsync, notDashing);
                var thatCharacterInNextFrame = nextRenderFramePlayers[i];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy) = _derivePlayerOpPattern(currRenderFrame.Id, delayedInputFrameDownsync, currCharacterDownsync, chConfig, thatCharacterInNextFrame, inputBuffer, decodedInputHolder, currEffInAir, notDashing, selfPlayerJoinIndex, logger);
                /*
                if (PATTERN_RELEASED_B == patternId) {
                    int delayedInputFrameId = ConvertToDelayedInputFrameId(rdfId);
                    var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
                    var joinIndexMask = (1u << i);
                    if (null != delayedInputFrameDownsync && 0 == (delayedInputFrameDownsync.ConfirmedList & joinIndexMask) && (0 == delayedInputFrameDownsync.UdpConfirmedList)) {
                        logger.LogInfo($"PATTERN_RELEASED_B from prediction: currRdfId={rdfId}, delayedIfd={delayedInputFrameId}, op={decodedInputHolder}\n\topEncoded={stringifyIfd(delayedInputFrameDownsync, false)}\n\tch={stringifyPlayer(currCharacterDownsync)}\n\tnextch={stringifyPlayer(thatCharacterInNextFrame)}");
                    }
                }
                */
                _processSingleCharacterInput(currRenderFrame.Id, patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy, false, currCharacterDownsync, currEffInAir, chConfig, thatCharacterInNextFrame, false, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, selfPlayerJoinIndex, ref selfNotEnoughMp, logger);
            }
        }
        
        public static void _resetVelocityOnRecovered(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame) {
            // [WARNING] This is a necessary cleanup before "_processInertiaWalking"!
            if (1 == currCharacterDownsync.FramesToRecover && 0 == thatCharacterInNextFrame.FramesToRecover && (Atked1 == currCharacterDownsync.CharacterState || InAirAtked1 == currCharacterDownsync.CharacterState || CrouchAtked1 == currCharacterDownsync.CharacterState)) {
                thatCharacterInNextFrame.VelX = 0;
                thatCharacterInNextFrame.VelY = 0;
            }
        }

        public static void _processNextFrameJumpStartup(int currRdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, bool currEffInAir, CharacterConfig chConfig, bool isParalyzed, ILoggerBridge logger) {
            /*
            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                logger.LogInfo("_processNextFrameJumpStartup: currRdfId=" + currRdfId + ", " + stringifyPlayer(currCharacterDownsync));
            }
            */
            if ((TransformingInto == currCharacterDownsync.CharacterState && 0 < currCharacterDownsync.FramesToRecover) || (TransformingInto == thatCharacterInNextFrame.CharacterState && 0 < thatCharacterInNextFrame.FramesToRecover)) {
                return;
            }
    
            if (isParalyzed) {
                return;
            }

            if (0 == chConfig.JumpingInitVelY) {
                return;
            }

            if (isInJumpStartup(thatCharacterInNextFrame, chConfig)) {
                return;
            }

            if (isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig)) {
                thatCharacterInNextFrame.JumpStarted = true;
            } else if ((thatCharacterInNextFrame.JumpTriggered || thatCharacterInNextFrame.SlipJumpTriggered) && (!currCharacterDownsync.JumpStarted && !thatCharacterInNextFrame.JumpStarted)) {
                // [WARNING] This assignment blocks a lot of CharacterState transition logic, including "_processInertiaWalking"!
                if (currCharacterDownsync.OnWall && OnWallIdle1 == currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.FramesToStartJump = (chConfig.ProactiveJumpStartupFrames >> 1);
                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
                    thatCharacterInNextFrame.VelY = 0;
                } else if (currEffInAir && !currCharacterDownsync.OmitGravity) {
                    if (0 < currCharacterDownsync.RemainingAirJumpQuota) {
                        thatCharacterInNextFrame.FramesToStartJump = IN_AIR_JUMP_GRACE_PERIOD_RDF_CNT;
                        thatCharacterInNextFrame.CharacterState = InAirIdle2ByJump;
                        thatCharacterInNextFrame.VelY = 0;
                        thatCharacterInNextFrame.RemainingAirJumpQuota = currCharacterDownsync.RemainingAirJumpQuota - 1; 
                        if (!chConfig.IsolatedAirJumpAndDashQuota && 0 < thatCharacterInNextFrame.RemainingAirDashQuota) {
                            thatCharacterInNextFrame.RemainingAirDashQuota -= 1;
                        }
                    }
                } else {
                    // [WARNING] Including "SlipJumpTriggered" here
                    thatCharacterInNextFrame.FramesToStartJump = chConfig.ProactiveJumpStartupFrames;
                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                }
            }
        }

        private static void _processInertiaWalkingHandleZeroEffDx(int currRdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int effDy, CharacterConfig chConfig, bool recoveredFromAirAtk, bool isParalyzed, ILoggerBridge logger) {
            if (proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                // [WARNING] In general a character is not permitted to just stop velX during proactive jumping.
                return;
            }

            if (!isParalyzed && recoveredFromAirAtk) {
                // [WARNING] This is to help "_processEffPushbacks" correct "0 != VelX but Idle1" case.
                thatCharacterInNextFrame.VelX = currCharacterDownsync.VelX;
            } else {
                thatCharacterInNextFrame.VelX = 0;
            }

            if (0 < currCharacterDownsync.FramesToRecover || currCharacterDownsync.InAir || !chConfig.HasDef1 || 0 >= effDy) {
                return;
            }

            /*
               if (SPECIES_HEAVYGUARD_RED == currCharacterDownsync.SpeciesId && Dashing == currCharacterDownsync.CharacterState) {
               logger.LogInfo(String.Format("@currRdfId={0}, ch.Id={1}, thatCharacterInNextFrame turned Def1", currRdfId, currCharacterDownsync.Id));
               }
             */
            thatCharacterInNextFrame.CharacterState = Def1;
            if (Def1 == currCharacterDownsync.CharacterState) return; 
            thatCharacterInNextFrame.FramesInChState = 0; 
            thatCharacterInNextFrame.RemainingDef1Quota = chConfig.DefaultDef1Quota; 
            /*  
            if (SPECIES_HEAVYGUARD_RED == currCharacterDownsync.SpeciesId) { 
                logger.LogInfo(String.Format("@rdfId={0}, HeavyGuardRed id={1} starts defending", currRdfId, currCharacterDownsync.Id)); 
            } 
            */ 
        } 

        public static void _processInertiaWalking(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, bool currEffInAir, int effDx, int effDy, CharacterConfig chConfig, bool shouldIgnoreInertia, bool usedSkill, Skill? skillConfig, bool isParalyzed, ILoggerBridge logger) { 
            if ((TransformingInto == currCharacterDownsync.CharacterState && 0 < currCharacterDownsync.FramesToRecover) || (TransformingInto == thatCharacterInNextFrame.CharacterState && 0 < thatCharacterInNextFrame.FramesToRecover)) { 
                return; 
            } 

            if (isInJumpStartup(thatCharacterInNextFrame, chConfig) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig)) { 
                return; 
            } 
            
            if (IsInBlockStun(currCharacterDownsync)) { 
                return; 
            }

            bool currFreeFromInertia = (0 == currCharacterDownsync.FramesCapturedByInertia); bool currBreakingFromInertia = (1 == currCharacterDownsync.FramesCapturedByInertia);
            /* 
               [WARNING] 
               Special cases for turn-around inertia handling:
               1. if "true == thatCharacterInNextFrame.JumpTriggered", then we've already met the criterions of "canJumpWithinInertia" in "_derivePlayerOpPattern";
               2. if "InAirIdle1ByJump || InAirIdle2ByJump || InAirIdle1NoJump", turn-around should still be bound by inertia just like that of ground movements; 
               3. if "InAirIdle1ByWallJump", turn-around is NOT bound by inertia because in most cases characters couldn't perform wall-jump and even if it could, "WallJumpingFramesToRecover+ProactiveJumpStartupFrames" already dominates most of the time.
             */
            bool withInertiaBreakingState = (thatCharacterInNextFrame.JumpTriggered || (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState));
            bool alignedWithInertia = true;
            bool exactTurningAround = false;
            bool stoppingFromWalking = false;
            if (0 != effDx && 0 == thatCharacterInNextFrame.VelX) {
                alignedWithInertia = false;
            } else if (0 == effDx && 0 != thatCharacterInNextFrame.VelX) {
                alignedWithInertia = false;
                stoppingFromWalking = true;
            } else if (0 > effDx * thatCharacterInNextFrame.VelX) {
                alignedWithInertia = false;
                exactTurningAround = true;
            }

            bool hasNonZeroSpeed = !(0 == chConfig.Speed && 0 == currCharacterDownsync.Speed);
            if (0 == currCharacterDownsync.FramesToRecover || (WalkingAtk1 == currCharacterDownsync.CharacterState || WalkingAtk4 == currCharacterDownsync.CharacterState)) {
                var oldNextChState = thatCharacterInNextFrame.CharacterState;
                bool isOldNextChStateDimmed = (Dimmed == thatCharacterInNextFrame.CharacterState);
                bool isOldNextChStateInAirIdle2ByJump = (InAirIdle2ByJump == thatCharacterInNextFrame.CharacterState);
                bool recoveredFromAirAtk = (0 < currCharacterDownsync.FramesToRecover && currEffInAir && !nonAttackingSet.Contains(currCharacterDownsync.CharacterState) && 0 == thatCharacterInNextFrame.FramesToRecover && !thatCharacterInNextFrame.InAir);
                if (!isOldNextChStateInAirIdle2ByJump && !isOldNextChStateDimmed) {
                    thatCharacterInNextFrame.CharacterState = Idle1; // When reaching here, the character is at least recovered from "Atked{N}" or "Atk{N}" state, thus revert back to "Idle" as a default action
                }
                if (shouldIgnoreInertia) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0;
                    if (0 != effDx && hasNonZeroSpeed) {
                        int xfac = (0 < effDx ? 1 : -1);
                        thatCharacterInNextFrame.DirX = effDx;
                        thatCharacterInNextFrame.DirY = effDy;
                        if (!isStaticCrouching(currCharacterDownsync.CharacterState)) {
                            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                thatCharacterInNextFrame.VelX = isParalyzed ? 0 : xfac * chConfig.WallJumpingInitVelX;
                            } else {
                                thatCharacterInNextFrame.VelX = isParalyzed ? 0 : xfac * currCharacterDownsync.Speed;
                            }
                            if (!isOldNextChStateInAirIdle2ByJump) {
                                thatCharacterInNextFrame.CharacterState = Walking;
                            }
                        }
                    } else {
                        // 0 == effDx or speed is zero
                        _processInertiaWalkingHandleZeroEffDx(rdfId, currCharacterDownsync, thatCharacterInNextFrame, effDy, chConfig, recoveredFromAirAtk, isParalyzed, logger);
                    }
                } else {
                    if (alignedWithInertia || withInertiaBreakingState || currBreakingFromInertia) {
                        if (!alignedWithInertia) {
                            // Should reset "FramesCapturedByInertia" in this case!
                            thatCharacterInNextFrame.FramesCapturedByInertia = 0;
                        }

                        if (0 != effDx && hasNonZeroSpeed) {
                            int xfac = (0 < effDx ? 1 : -1);
                            thatCharacterInNextFrame.DirX = effDx;
                            thatCharacterInNextFrame.DirY = effDy;
                            if (!isStaticCrouching(currCharacterDownsync.CharacterState)) {
                                if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.VelX = isParalyzed ? 0 : xfac * chConfig.WallJumpingInitVelX;
                                } else {
                                    thatCharacterInNextFrame.VelX = isParalyzed ? 0 : xfac * currCharacterDownsync.Speed;
                                }
                                if (!isOldNextChStateInAirIdle2ByJump) {
                                    thatCharacterInNextFrame.CharacterState = Walking;
                                }
                            }
                        } else {
                            // 0 == effDx or speed is zero
                            _processInertiaWalkingHandleZeroEffDx(rdfId, currCharacterDownsync, thatCharacterInNextFrame, effDy, chConfig, recoveredFromAirAtk, isParalyzed, logger);
                        }
                    } else if (currFreeFromInertia) {
                        if (exactTurningAround) {
                            // logger.LogInfo(stringifyPlayer(currCharacterDownsync) + " is turning around at currRdfId=" + currRdfId);
                            thatCharacterInNextFrame.CharacterState = isOldNextChStateInAirIdle2ByJump ? InAirIdle2ByJump : ((chConfig.HasTurnAroundAnim && !currCharacterDownsync.InAir) ? TurnAround : Walking);
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                            if (chConfig.InertiaFramesToRecover > thatCharacterInNextFrame.FramesToRecover) {
                                // [WARNING] Deliberately not setting "thatCharacterInNextFrame.FramesToRecover" if not turning around to allow using skills!
                                thatCharacterInNextFrame.FramesToRecover = (chConfig.InertiaFramesToRecover - 1); // To favor animation playing and prevent skill use when turning-around
                            }
                        } else if (stoppingFromWalking) {
                            // Keeps CharacterState and thus the animation to make user see graphical feedback asap.
                            thatCharacterInNextFrame.CharacterState = isOldNextChStateInAirIdle2ByJump ? InAirIdle2ByJump : (chConfig.HasInAirWalkStoppingAnim ? WalkStopping : Walking);
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                        } else {
                            // Updates CharacterState and thus the animation to make user see graphical feedback asap.
                            thatCharacterInNextFrame.CharacterState = isOldNextChStateInAirIdle2ByJump ? InAirIdle2ByJump : Walking;
                            thatCharacterInNextFrame.FramesCapturedByInertia = 0 < (chConfig.InertiaFramesToRecover >> 3) ? (chConfig.InertiaFramesToRecover >> 3) : 1;
                        }
                    } else {
                        // [WARNING] Not free from inertia, just set proper next chState
                        if (0 != thatCharacterInNextFrame.VelX) {
                            thatCharacterInNextFrame.CharacterState = isOldNextChStateInAirIdle2ByJump ? InAirIdle2ByJump : Walking;
                        }
                    }
                }
            }

            if (!thatCharacterInNextFrame.JumpTriggered && !currCharacterDownsync.InAir && 0 > effDy && chConfig.CrouchingEnabled) {
                // [WARNING] This particular condition is set to favor a smooth "Sliding -> CrouchIdle1" & "CrouchAtk1 -> CrouchAtk1" transitions, we couldn't use "0 == thatCharacterInNextFrame.FramesToRecover" for checking here because "CrouchAtk1 -> CrouchAtk1" transition would break by 1 frame. 
                if (1 >= currCharacterDownsync.FramesToRecover) {
                    thatCharacterInNextFrame.VelX = 0;
                    thatCharacterInNextFrame.CharacterState = CrouchIdle1;
                }
            }

            if (usedSkill || (WalkingAtk1 == currCharacterDownsync.CharacterState || WalkingAtk4 == currCharacterDownsync.CharacterState)) {
                /*
                 * [WARNING]
                 * 
                 * A dirty fix here just for "Atk1 -> WalkingAtk1" transition.
                 * 
                 * In this case "thatCharacterInNextFrame.FramesToRecover" is already set by the skill in use, and transition to "TurnAround" should NOT be supported!
                 */
                if (0 < thatCharacterInNextFrame.FramesToRecover) {
                    if (0 != thatCharacterInNextFrame.VelX) {
                        if ((null != skillConfig && Atk1 == skillConfig.BoundChState) || WalkingAtk1 == currCharacterDownsync.CharacterState) {
                            thatCharacterInNextFrame.CharacterState = WalkingAtk1;
                        }
                        if ((null != skillConfig && Atk4 == skillConfig.BoundChState) || WalkingAtk4 == currCharacterDownsync.CharacterState) {
                            thatCharacterInNextFrame.CharacterState = WalkingAtk4;
                        }
                    } else if (CrouchIdle1 == thatCharacterInNextFrame.CharacterState) {
                        if (chConfig.CrouchingAtkEnabled) {     
                            // TODO: Is it necessary to check "chConfig.CrouchingAtkEnabled" here?
                            thatCharacterInNextFrame.CharacterState = CrouchAtk1;
                        }
                    } else if (null != skillConfig) {
                        thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;
                    }
                }
            }

            /*
               if (77 == thatCharacterInNextFrame.ActiveSkillId) {
               logger.LogInfo("_processInertiaWalking/end, currRdfId=" + rdfId  + ", used DiverImpact, next VelX = " + thatCharacterInNextFrame.VelX);
               }
             */
        }

        private static void _processInertiaFlyingHandleZeroEffDxAndDy(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, bool isParalyzed, ILoggerBridge logger) {
                thatCharacterInNextFrame.VelX = 0;
                if (!chConfig.AntiGravityWhenIdle || InAirIdle1NoJump != currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.VelY = 0;
                    thatCharacterInNextFrame.DirY = 0;
                }
        }

        public static void _processInertiaFlying(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int effDx, int effDy, CharacterConfig chConfig, bool shouldIgnoreInertia, bool usedSkill, Skill? skillConfig, bool isParalyzed, ILoggerBridge logger) {
            if ((TransformingInto == currCharacterDownsync.CharacterState && 0 < currCharacterDownsync.FramesToRecover) || (TransformingInto == thatCharacterInNextFrame.CharacterState && 0 < thatCharacterInNextFrame.FramesToRecover)) {
                return;
            }
            if (IsInBlockStun(currCharacterDownsync)) {
                return;
            }

            bool currFreeFromInertia = (0 == currCharacterDownsync.FramesCapturedByInertia);
            bool currBreakingFromInertia = (1 == currCharacterDownsync.FramesCapturedByInertia);
          
            bool withInertiaBreakingState = (thatCharacterInNextFrame.JumpTriggered || (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState));
            bool alignedWithInertia = true;
            bool exactTurningAround = false;
            bool stoppingFromWalking = false;
            if ((0 != effDx && 0 == thatCharacterInNextFrame.VelX) || (0 != effDy && 0 == thatCharacterInNextFrame.VelY)) {
                alignedWithInertia = false;
            } else if ((0 == effDx && 0 != thatCharacterInNextFrame.VelX) || (0 == effDy && 0 != thatCharacterInNextFrame.VelY)) {
                alignedWithInertia = false;
                stoppingFromWalking = true;
            } else if ((0 > effDx * thatCharacterInNextFrame.VelX) || (0 > effDy * thatCharacterInNextFrame.VelY)) {
                alignedWithInertia = false;
                exactTurningAround = true;
            }

            bool hasNonZeroSpeed = !(0 == chConfig.Speed && 0 == currCharacterDownsync.Speed);
            if (0 == currCharacterDownsync.FramesToRecover) {
                var defaultInAirIdleChState = chConfig.UseIdle1AsFlyingIdle ? Idle1 : Walking;
                thatCharacterInNextFrame.CharacterState = ((Idle1 == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && chConfig.AntiGravityWhenIdle) ? currCharacterDownsync.CharacterState : defaultInAirIdleChState; // When reaching here, the character is at least recovered from "Atked{N}" or "Atk{N}" state, thus revert back to a default action
                
                if (shouldIgnoreInertia) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0;
                    if ((0 != effDx || 0 != effDy) && hasNonZeroSpeed) {
                        if (SPECIES_FIREBAT == currCharacterDownsync.SpeciesId && InAirIdle1NoJump == currCharacterDownsync.CharacterState) {
                            logger.LogInfo($"_processInertiaFlying/start, currRdfId={rdfId}, setting InAirIdle1NoJump to Walking currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, DirX: {currCharacterDownsync.DirX}, DirY: {currCharacterDownsync.DirY}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                        }
                        thatCharacterInNextFrame.DirX = (0 == effDx ? currCharacterDownsync.DirX : (0 > effDx ? -2 : +2));
                        thatCharacterInNextFrame.DirY = (0 == effDy ? currCharacterDownsync.DirY : (0 > effDy ? -1 : +1));
                        int xfac = 0 == effDx ? 0 : 0 > effDx ? -1 : +1;
                        int yfac = 0 == effDy ? 0 : 0 > effDy ? -1 : +1;
                        thatCharacterInNextFrame.VelX = isParalyzed ? 0 : xfac * currCharacterDownsync.Speed;
                        thatCharacterInNextFrame.VelY = isParalyzed ? 0 : yfac * currCharacterDownsync.Speed;
                        thatCharacterInNextFrame.CharacterState = Walking;
                    } else {
                        // (0 == effDx && 0 == effDy) or speed is zero
                        _processInertiaFlyingHandleZeroEffDxAndDy(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, isParalyzed, logger);
                    }
                } else {
                    if (alignedWithInertia || withInertiaBreakingState || currBreakingFromInertia) {
                        if (!alignedWithInertia) {
                            // Should reset "FramesCapturedByInertia" in this case!
                            thatCharacterInNextFrame.FramesCapturedByInertia = 0;
                        }

                        if ((0 != effDx || 0 != effDy) && hasNonZeroSpeed) {
                            thatCharacterInNextFrame.DirX = (0 == effDx ? currCharacterDownsync.DirX : (0 > effDx ? -2 : +2));
                            thatCharacterInNextFrame.DirY = (0 == effDy ? currCharacterDownsync.DirY : (0 > effDy ? -1 : +1));
                            int xfac = 0 == effDx ? 0 : 0 > effDx ? -1 : +1;
                            int yfac = 0 == effDy ? 0 : 0 > effDy ? -1 : +1;
                            thatCharacterInNextFrame.VelX = isParalyzed ? 0 : xfac * currCharacterDownsync.Speed;
                            thatCharacterInNextFrame.VelY = isParalyzed ? 0 : yfac * currCharacterDownsync.Speed;
                            thatCharacterInNextFrame.CharacterState = Walking;
                        } else {
                            // (0 == effDx && 0 == effDy) or speed is zero
                            _processInertiaFlyingHandleZeroEffDxAndDy(rdfId, currCharacterDownsync, thatCharacterInNextFrame, chConfig, isParalyzed, logger);
                        }
                    } else if (currFreeFromInertia) {
                        if (exactTurningAround) {
                            // logger.LogInfo(stringifyPlayer(currCharacterDownsync) + " is turning around at currRdfId=" + currRdfId);
                            thatCharacterInNextFrame.CharacterState = (chConfig.HasTurnAroundAnim && !currCharacterDownsync.InAir) ? TurnAround : Walking;
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                            if (chConfig.InertiaFramesToRecover > thatCharacterInNextFrame.FramesToRecover) {
                                // [WARNING] Deliberately not setting "thatCharacterInNextFrame.FramesToRecover" if not turning around to allow using skills!
                                thatCharacterInNextFrame.FramesToRecover = (chConfig.InertiaFramesToRecover - 1); // To favor animation playing and prevent skill use when turning-around
                            }
                        } else if (stoppingFromWalking) {
                            // Keeps CharacterState and thus the animation to make user see graphical feedback asap.
                            thatCharacterInNextFrame.CharacterState = chConfig.HasWalkStoppingAnim ? WalkStopping : Walking;
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                        } else {
                            // Updates CharacterState and thus the animation to make user see graphical feedback asap.
                            thatCharacterInNextFrame.FramesCapturedByInertia = 0 < (chConfig.InertiaFramesToRecover >> 3) ? (chConfig.InertiaFramesToRecover >> 3) : 1;
                            thatCharacterInNextFrame.CharacterState = Walking;
                        }
                    } else {
                        // [WARNING] Not free from inertia, just set proper next chState
                        if ((0 != thatCharacterInNextFrame.VelX || 0 != thatCharacterInNextFrame.VelY) && (0 != thatCharacterInNextFrame.DirX || 0 != thatCharacterInNextFrame.DirY)) {
                            thatCharacterInNextFrame.CharacterState = Walking;
                        }
                    }
                }
            }
        }

        public static bool isStaticCrouching(CharacterState state) {
            return (CrouchIdle1 == state || CrouchAtk1 == state || CrouchAtked1 == state);
        }

        public static bool isCrouching(CharacterState state, CharacterConfig chConfig) {
            return (CrouchIdle1 == state || CrouchAtk1 == state || CrouchAtked1 == state || (Sliding == state && chConfig.CrouchingEnabled));
        }

        private static void _moveAndInsertCharacterColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Vector[] effPushbacks, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, ref int colliderCnt, int iSt, int iEd, ILoggerBridge logger) {
            for (int i = iSt; i < iEd; i++) {
                int joinIndex = i+1;
                var currCharacterDownsync = getChdFromRdf(joinIndex, roomCapacity, currRenderFrame);
                if (i >= roomCapacity && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = getChdFromChdArrs(joinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                effPushbacks[i].X = 0;
                effPushbacks[i].Y = 0;
                int vhDiffInducedByCrouching = 0;
                bool justBecameCrouching = !currCharacterDownsync.PrevWasCrouching && !currCharacterDownsync.InAir && (0 == currCharacterDownsync.FramesInChState) && isCrouching(currCharacterDownsync.CharacterState, chConfig);
                if (justBecameCrouching) {
                    vhDiffInducedByCrouching -= ((chConfig.DefaultSizeY - chConfig.ShrinkedSizeY) >> 1);
                }
                /* 
                if (SPECIES_STONE_GOLEM == currCharacterDownsync.SpeciesId && Atk2 == currCharacterDownsync.CharacterState && 40 <= currCharacterDownsync.FramesInChState) {
                    logger.LogInfo("_moveAndInsertCharacterColliders, currRdfId=" + currRenderFrame.Id + ", VelY = " + currCharacterDownsync.VelY);
                } 
                */

                int jumpAssistFrictionVelY = (0 < currCharacterDownsync.FrictionVelY ? currCharacterDownsync.FrictionVelY : 0);
                int effVelX = currCharacterDownsync.VelX + currCharacterDownsync.FrictionVelX;
                if (currCharacterDownsync.OnWall) {
                    if (0 < effVelX*currCharacterDownsync.OnWallNormX) {
                        effVelX = 0 < currCharacterDownsync.DirX ? SNAP_INTO_PLATFORM_OVERLAP_VIRTUAL_GRID : -SNAP_INTO_PLATFORM_OVERLAP_VIRTUAL_GRID;
                    }
                }
                int newVx = currCharacterDownsync.VirtualGridX + effVelX, newVy = currCharacterDownsync.VirtualGridY + currCharacterDownsync.VelY + currCharacterDownsync.FrictionVelY + vhDiffInducedByCrouching;

                // [WARNING] Due to the current ordering of "_processPlayerInputs -> _moveAndInsertCharacterColliders -> _processNpcInputs", I have no better choice of deciding "jumpStarted" besides this ugly way for now
                bool jumpStarted = (i < roomCapacity ? thatCharacterInNextFrame.JumpStarted : currCharacterDownsync.JumpStarted); 
                if (jumpStarted) {
                    // We haven't proceeded with "OnWall" calculation for "thatCharacterInNextFrame", thus use "currCharacterDownsync.OnWall" for checking
                    if (currCharacterDownsync.OnWall && chConfig.OnWallEnabled && InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                        // logger.LogInfo("currRdfId=" + currRenderFrame.Id + ", wall jump started for " + stringifyPlayer(currCharacterDownsync));
                        if (0 < currCharacterDownsync.VelX * currCharacterDownsync.OnWallNormX) {
                            newVx -= currCharacterDownsync.VelX; // Cancel the alleged horizontal movement pointing to same direction of wall inward norm first
                        }
                        // Always jump to the opposite direction of wall inward norm
                        int xfac = (0 > currCharacterDownsync.OnWallNormX ? 1 : -1);
                        newVx += xfac * chConfig.WallJumpingInitVelX; // Immediately gets out of the snap
                        thatCharacterInNextFrame.VelX = (xfac * chConfig.WallJumpingInitVelX);
                        thatCharacterInNextFrame.VelY = (chConfig.WallJumpingInitVelY);
                        thatCharacterInNextFrame.FramesToRecover = chConfig.WallJumpingFramesToRecover;
                        thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
                    } else if (currCharacterDownsync.InAir && InAirIdle2ByJump == thatCharacterInNextFrame.CharacterState) {
                        thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY + jumpAssistFrictionVelY;
                        thatCharacterInNextFrame.CharacterState = InAirIdle2ByJump;
                    } else if (currCharacterDownsync.SlipJumpTriggered) {
                        newVy = (currCharacterDownsync.VirtualGridY - chConfig.SlipJumpCharacterDropVirtual); // [WARNING] Regardless of "currCharacterDownsync.VelY"
                        thatCharacterInNextFrame.VelY = 0;
                        if (currCharacterDownsync.OmitGravity && !chConfig.OmitGravity && chConfig.JumpHoldingToFly) {
                            thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                            thatCharacterInNextFrame.OmitGravity = false;
                            thatCharacterInNextFrame.FlyingRdfCountdown = 0;
                        }
                    } else {
                        thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY + jumpAssistFrictionVelY;
                        thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                    }

                    resetJumpStartup(thatCharacterInNextFrame);
                } else if (!chConfig.OmitGravity && chConfig.JumpHoldingToFly && currCharacterDownsync.OmitGravity && 0 >= currCharacterDownsync.FlyingRdfCountdown) {
                    thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                    thatCharacterInNextFrame.OmitGravity = false;
                }

                if (i < roomCapacity && 0 >= thatCharacterInNextFrame.Hp && 0 == thatCharacterInNextFrame.FramesToRecover) {
                    // Revive player-controlled character from Dying
                    (newVx, newVy) = (currCharacterDownsync.RevivalVirtualGridX, currCharacterDownsync.RevivalVirtualGridY);
                    revertAllBuffsAndDebuffs(currCharacterDownsync, thatCharacterInNextFrame);
                    chConfig = characters[thatCharacterInNextFrame.SpeciesId];
                    thatCharacterInNextFrame.CharacterState = GetUp1; // No need to tune bounding box and offset for this case, because the revival location is fixed :)
                    thatCharacterInNextFrame.FramesInChState = 0;
                    thatCharacterInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                    thatCharacterInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;

                    thatCharacterInNextFrame.Hp = chConfig.Hp;
                    thatCharacterInNextFrame.Mp = chConfig.Mp;
                    thatCharacterInNextFrame.DirX = currCharacterDownsync.RevivalDirX;
                    thatCharacterInNextFrame.DirY = currCharacterDownsync.RevivalDirY;
                    thatCharacterInNextFrame.VelX = 0;
                    thatCharacterInNextFrame.VelY = 0;
                    thatCharacterInNextFrame.NewBirth = true;
                }

                float boxCx, boxCy, boxCw, boxCh;
                calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, newVx, newVy, out boxCx, out boxCy, out boxCw, out boxCh);
                Collider characterCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(characterCollider, boxCx, boxCy, boxCw, boxCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync, chConfig.CollisionTypeMask); // the coords of all barrier boundaries are multiples of tileWidth(i.e. 16), by adding snapping y-padding when "landedOnGravityPushback" all "characterCollider.Y" would be a multiple of 1.0
                colliderCnt++;

                // Add to collision system
                collisionSys.AddSingleToCellTail(characterCollider);

                _applyGravity(currRenderFrame.Id, currCharacterDownsync, chConfig, thatCharacterInNextFrame, logger);
            }
        }

        private static void _handleSingleChResidualPushbacks(RoomDownsyncFrame currRenderFrame, FrameRingBuffer<InputFrameDownsync> inputBuffer, int roomCapacity, Collider aCollider, ConvexPolygon aShape, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, Skill? skillConfig, BulletConfig? bulletConfig, Vector[] hardPushbackNormsOfSingleCh, int hardPushbackCnt, int primaryHardOverlapIndex, bool repelSoftPushback, Vector[] softPushbacks, ref int softPushbacksCnt, ref int primarySoftOverlapIndex, ref float primarySoftPushbackX, ref float primarySoftPushbackY, bool softPushbackEnabled, FrameRingBuffer<Collider> residueCollided, ref int bulletLocalIdCounter, ref int bulletCnt, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector effPushback, RepeatedField<Bullet> nextRenderFrameBullets, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Trigger> nextRenderFrameTriggers, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToTiledConfig, ref float normAlignmentWithGravity, ref bool landedOnGravityPushback, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, RdfPushbackFrameLog? currPushbackFrameLog, bool pushbackFrameLogEnabled, ILoggerBridge logger) {
            if (Dying == currCharacterDownsync.CharacterState) return;
            bool shouldOmitSoftPushbackForSelf = (repelSoftPushback || chOmittingSoftPushback(currCharacterDownsync, skillConfig, bulletConfig));
            int totOtherChCnt = 0, cellOverlappedOtherChCnt = 0, shapeOverlappedOtherChCnt = 0;
            int origResidueCollidedSt = residueCollided.StFrameId, origResidueCollidedEd = residueCollided.EdFrameId; 
            float primarySoftOverlapMagSquared = float.MinValue; 
            /*
               if (1 == currCharacterDownsync.JoinIndex) {
               logger.LogInfo(String.Format("Has {0} residueCollided with chState={3}, vy={4}: hardPushbackNormsOfSingleCh={1}, effPushback={2}, primaryOverlapResult={5}", residueCollided.Cnt, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString()));
               }
             */
            while (true) {
                var (ok3, bCollider) = residueCollided.Pop();
                if (false == ok3 || null == bCollider) {
                    break;
                }
                ConvexPolygon bShape = bCollider.Shape;
                var v4 = bCollider.Data as Pickable;
                if (null != v4 && currCharacterDownsync.JoinIndex <= roomCapacity) {
                    if ((TERMINATING_CONSUMABLE_SPECIES_ID != v4.ConfigFromTiled.ConsumableSpeciesId || NO_SKILL != v4.ConfigFromTiled.SkillId) && PickableState.Pidle == v4.PkState && 0 < v4.RemainingLifetimeRdfCount && DEFAULT_PICKABLE_NONPICKABLE_STARTUP_FRAMES < v4.FramesInPkState) {
                        if (PickupType.Immediate == v4.ConfigFromTiled.PickupType) {
                            var (clicked, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult, logger);
                            if (clicked) {
                                uint effConsumableId = v4.ConfigFromTiled.ConsumableSpeciesId;
                                if (pickableConsumableIdMapper.ContainsKey(v4.ConfigFromTiled.ConsumableSpeciesId) && pickableConsumableIdMapper[v4.ConfigFromTiled.ConsumableSpeciesId].ContainsKey(currCharacterDownsync.SpeciesId)) {
                                    effConsumableId = pickableConsumableIdMapper[v4.ConfigFromTiled.ConsumableSpeciesId][currCharacterDownsync.SpeciesId];
                                }
                                var consumableConfig = consumableConfigs[effConsumableId];
                                if (HpRefillSmall.SpeciesId == effConsumableId || HpRefillMiddle.SpeciesId == effConsumableId) {  
                                    thatCharacterInNextFrame.Hp += consumableConfig.RefillDelta;
                                    if (thatCharacterInNextFrame.Hp > chConfig.Hp) {
                                        thatCharacterInNextFrame.Hp = chConfig.Hp;
                                    }
                                } else if (MpRefillSmall.SpeciesId == effConsumableId || MpRefillMiddle.SpeciesId == effConsumableId) {
                                    thatCharacterInNextFrame.Mp += consumableConfig.RefillDelta;
                                    if (thatCharacterInNextFrame.Mp > chConfig.Mp) {
                                        thatCharacterInNextFrame.Mp = chConfig.Mp;
                                    }
                                }
                                v4.PkState = PickableState.Pconsumed;
                                v4.FramesInPkState = 0;
                                v4.RemainingLifetimeRdfCount = DEFAULT_PICKABLE_CONSUMED_ANIM_FRAMES; // [WARNING] Prohibit concurrent pick-up, the character with smaller join index will win in case of a tie.
                                v4.PickedByJoinIndex = currCharacterDownsync.JoinIndex;
                            }
                        } else if (PickupType.PutIntoInventory == v4.ConfigFromTiled.PickupType && null != chConfig.InitInventorySlots && 0 < chConfig.InitInventorySlots.Count) {
                            var (clicked, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult, logger);
                            if (clicked) {
                                if (NO_SKILL != v4.ConfigFromTiled.SkillId) {
                                    if (2 <= chConfig.InitInventorySlots.Count && InventorySlotStockType.PocketIv == chConfig.InitInventorySlots[1].StockType) {
                                        uint newQuota = v4.ConfigFromTiled.StockQuotaPerOccurrence;
                                        uint effSkillId = pickableSkillIdMapper[v4.ConfigFromTiled.SkillId][currCharacterDownsync.SpeciesId];
                                        uint effSkillIdAir = pickableSkillIdAirMapper[v4.ConfigFromTiled.SkillId][currCharacterDownsync.SpeciesId];
                                        var existingIvSlot = currCharacterDownsync.Inventory.Slots[1];
                                        if (InventorySlotStockType.QuotaIv == existingIvSlot.StockType && existingIvSlot.SkillId == effSkillId) {
                                            newQuota += existingIvSlot.Quota;
                                        }
                                        // Currently only skill would be configured for "PickupType.PutIntoInventory", and only "InventorySlotStockType.QuotaIv" is supported.  
                                        AssignToInventorySlot(InventorySlotStockType.QuotaIv, newQuota, 0, newQuota, 0, TERMINATING_BUFF_SPECIES_ID, effSkillId, effSkillIdAir, existingIvSlot.GaugeCharged, existingIvSlot.GaugeRequired, existingIvSlot.FullChargeSkillId, existingIvSlot.FullChargeBuffSpeciesId, thatCharacterInNextFrame.Inventory.Slots[1]);
                                    } else {
                                        // 1 == chConfig.InitInventorySlots.Count
                                        var existingIvSlot = currCharacterDownsync.Inventory.Slots[0];
                                        if (InventorySlotStockType.TimedMagazineIv == existingIvSlot.StockType) {
                                            thatCharacterInNextFrame.Inventory.Slots[0].FramesToRecover = 1;
                                        } else if (InventorySlotStockType.GaugedMagazineIv == existingIvSlot.StockType) {
                                            accumulateGauge(DEFAULT_GAUGE_INC_BY_HIT*50, null, thatCharacterInNextFrame);
                                        }
                                    }
                                }
                                v4.PkState = PickableState.Pconsumed;
                                v4.FramesInPkState = 0;
                                v4.RemainingLifetimeRdfCount = DEFAULT_PICKABLE_CONSUMED_ANIM_FRAMES; // [WARNING] Prohibit concurrent pick-up, the character with smaller join index will win in case of a tie.
                                v4.PickedByJoinIndex = currCharacterDownsync.JoinIndex;
                            }
                        }
                    }
                    continue;
                }
                var v3 = bCollider.Data as TriggerColliderAttr;  
                if (null != v3) {
                    var atkedTrigger = currRenderFrame.TriggersArr[v3.TriggerLocalId-1];
                    var triggerConfigFromTiled = triggerEditorIdToTiledConfig[atkedTrigger.EditorId];
                    if (!isTriggerClickableByMovement(atkedTrigger, triggerConfigFromTiled, currCharacterDownsync, roomCapacity)) continue;
                    var (clicked, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult, logger);
                    if (clicked) {
                        // Currently only allowing "Player" to click.
                        var atkedTriggerInNextFrame = nextRenderFrameTriggers[v3.TriggerLocalId-1];
                        atkedTriggerInNextFrame.FulfilledEvtMask = atkedTriggerInNextFrame.DemandedEvtMask; // then fired in "_calcTriggerReactions"
                        atkedTriggerInNextFrame.OffenderJoinIndex = currCharacterDownsync.JoinIndex;
                        atkedTriggerInNextFrame.OffenderBulletTeamId = currCharacterDownsync.BulletTeamId;
                    }
                    continue;
                }
                var v2 = bCollider.Data as TrapColliderAttr;
                if (null != v2) {
                    if (Jumper1.SpeciesId == v2.SpeciesId) {
                        float characterBottom = aCollider.Y;
                        float trapTop = bCollider.Y + bCollider.H;
                        float trapBottom = bCollider.Y;
                        bool isValidIntersection = (!isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig) && !isInJumpStartup(currCharacterDownsync, chConfig) && !isInJumpStartup(thatCharacterInNextFrame, chConfig)  && currCharacterDownsync.InAir && 0 > currCharacterDownsync.VelY && characterBottom > trapBottom && characterBottom < trapTop);
                        var trapNextFrame = nextRenderFrameTraps[v2.TrapLocalId-1];
                        var effFramesToRecover = (JumperImpact1.StartupFrames+JumperImpact1.ActiveFrames);
                        bool isValidTrapState = (TrapState.Tidle == trapNextFrame.TrapState || (effFramesToRecover < trapNextFrame.FramesInTrapState));
                        if (isValidIntersection && isValidTrapState) {
                            var (clicked, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult, logger);
                            if (clicked || overlapResult.AContainedInB || overlapResult.BContainedInA) {
                                if (addNewTrapBulletToNextFrame(currRenderFrame.Id, currRenderFrame, trapNextFrame, Jumper1, JumperImpact1, JumperImpact1Skill, trapNextFrame.DirX, trapNextFrame.DirY, nextRenderFrameBullets, ref bulletLocalIdCounter, ref bulletCnt, logger)) {
                                    trapNextFrame.TrapState = TrapState.Tdeactivated;
                                    trapNextFrame.FramesInTrapState = 0;
                                }
                            }
                        }
                    } else if (v2.ProvidesEscape && currCharacterDownsync.JoinIndex <= roomCapacity) {
                        var (escaped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, false, ref overlapResult, logger);
                        // Currently only allowing "Player" to win.
                        if (escaped) {
                            if (1 == roomCapacity) {
                                confirmedBattleResult.WinnerJoinIndex = currCharacterDownsync.JoinIndex;
                                confirmedBattleResult.WinnerBulletTeamId = currCharacterDownsync.BulletTeamId;
                                continue;
                            } 
                            var (rdfAllConfirmed, delayedInputFrameId) = isRdfAllConfirmed(currRenderFrame.Id, inputBuffer, roomCapacity); 
                            if (rdfAllConfirmed) {
                                confirmedBattleResult.WinnerJoinIndex = currCharacterDownsync.JoinIndex;
                                confirmedBattleResult.WinnerBulletTeamId = currCharacterDownsync.BulletTeamId;
                                continue;
                            } else {
                                // [WARNING] This cached information could be created by a CORRECTLY PREDICTED "delayedInputFrameDownsync", thus we need a rollback from there on to finally consolidate the result later!
                                unconfirmedBattleResults[delayedInputFrameId] = confirmedBattleResult; // The "value" here is actually not useful, it's just stuffed here for type-correctness :)
                                continue;
                            }
                        }
                    } else {
                        continue;
                    }
                }
                var v1 = bCollider.Data as CharacterDownsync;
                if (null == v1) {
                    continue;
                } 
                if (softPushbackEnabled && !shouldOmitSoftPushbackForSelf) {
                    ++totOtherChCnt;
                    if (Dying == v1.CharacterState) {
                        continue;
                    }
                    var (v1SkillConfig, v1BulletConfig) = FindBulletConfig(v1.ActiveSkillId, v1.ActiveSkillHit);
                    if (chOmittingSoftPushback(v1, v1SkillConfig, v1BulletConfig)) {
                        continue;
                    }

                    if (currCharacterDownsync.ChCollisionTeamId == v1.ChCollisionTeamId) {
                        continue;
                    }

                    cellOverlappedOtherChCnt++;

                    var (overlapped, softPushbackX, softPushbackY) = calcPushbacks(0, 0, aShape, bShape, true, true, ref overlapResult, logger);
                    if (!overlapped) {
                        continue;
                    }

                    softPushbackX *= .5f;
                    softPushbackY *= .5f;

                    // [WARNING] Due to yet unknown reason, the resultant order of "hardPushbackNormsOfSingleCh" could be random for different characters in the same battle (maybe due to rollback not recovering the existing StaticCollider-TouchingCell information which could've been swapped by "TouchingCell.unregister(...)", please generate FrameLog and see the PushbackFrameLog part for details), the following traversal processing MUST BE ORDER-INSENSITIVE for softPushbackX & softPushbackY!
                    float softPushbackXReduction = 0f, softPushbackYReduction = 0f; 
                    for (int k = 0; k < hardPushbackCnt; k++) {
                        Vector hardPushbackNorm = hardPushbackNormsOfSingleCh[k];
                        float projectedMagnitude = softPushbackX * hardPushbackNorm.X + softPushbackY * hardPushbackNorm.Y;
                        if (0 > projectedMagnitude || (thatCharacterInNextFrame.OnSlope && k == primaryHardOverlapIndex)) {
                            // [WARNING] We don't want a softPushback to push an on-slope character either "into" or "outof" the slope!
                            softPushbackXReduction += projectedMagnitude * hardPushbackNorm.X; 
                            softPushbackYReduction += projectedMagnitude * hardPushbackNorm.Y; 
                        }
                    }

                    softPushbackX -= softPushbackXReduction;
                    softPushbackY -= softPushbackYReduction;


                    if (v1.InAir && !currCharacterDownsync.InAir) {
                        // [WARNING] An "InAir Character" shouldn't be able to push an "OnGround Character" horizontally -- reducing some unnecessary bouncing.
                        softPushbackX = 0;
                    }

                    var magSquared = (softPushbackX * softPushbackX + softPushbackY * softPushbackY);

                    if (magSquared < CLAMPABLE_COLLISION_SPACE_MAG_SQUARED) {
                        /*
                           [WARNING] 

                           Clamp to zero if it does not contribute to at least 1 virtual grid step by rounding. 

                           In field test, the backend (.net 7.0) and frontend (.net 2.1/4.0) might disagree on whether or not 2 colliders have overlapped by shape check (due to possibly different treatment of floating errors -- no direct evidence can be provided but from pushbackFrameLogs it's most suspicious), and if one party doesn't recognize any softPushback while the other does, the latter would proceed with "processPrimaryAndImpactEffPushback", resulting in different SNAP_INTO_CHARACTER_OVERLAP usage, thus different RoomDownsyncFrame!   

                           Hereby we SKIP recognizing "effectively zero softPushbacks", yet a closed-loop control on frontend by "onRoomDownsyncFrame & useOthersForcedDownsyncRenderFrameDict" is required because such (suspicious) floating errors are too difficult to completely avoid.

                           A similar clamping is used in "Battle_geometry.calcHardPushbacksNormsForCharacter" -- and there's an explanation for why this clamping magnitude is chosen.
                         */
                        continue;
                    }

                    normAlignmentWithGravity = (overlapResult.OverlapY * -1f);
                    if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                        /*
                           if (                
                           Atk1         == v1.CharacterState ||
                           Atk2         == v1.CharacterState ||
                           Atk3         == v1.CharacterState ||
                           Atk4         == v1.CharacterState ||
                           Atk5         == v1.CharacterState ||
                           InAirAtk1    == v1.CharacterState || 
                           WalkingAtk1  == v1.CharacterState ||
                           WalkingAtk4  == v1.CharacterState ||
                           OnWallAtk1   == v1.CharacterState 
                           ) {
                        // [WARNING] Prohibit landing on attacking characters.
                        continue;
                        } else {
                        landedOnGravityPushback = true;
                        }
                         */
                        if (!currCharacterDownsync.OmitGravity && !chConfig.OmitGravity) {
                            // [WARNING] Flying character doesn't land on softPushbacks even if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithAntiGravity)!
                            landedOnGravityPushback = true;
                            if (0 < v1.FrictionVelY && thatCharacterInNextFrame.FrictionVelY < v1.FrictionVelY) {
                                thatCharacterInNextFrame.FrictionVelY = v1.FrictionVelY;
                            }
                        }
                    }

                    shapeOverlappedOtherChCnt++;

                    if (primarySoftOverlapMagSquared < magSquared) {
                        primarySoftOverlapMagSquared = magSquared;
                        primarySoftPushbackX = softPushbackX;
                        primarySoftPushbackY = softPushbackY;
                        primarySoftOverlapIndex = softPushbacksCnt;
                    } else if ((softPushbackX < primarySoftPushbackX) || (softPushbackX == primarySoftPushbackX && softPushbackY < primarySoftPushbackY)) {
                        primarySoftOverlapMagSquared = magSquared;
                        primarySoftPushbackX = softPushbackX;
                        primarySoftPushbackY = softPushbackY;
                        primarySoftOverlapIndex = softPushbacksCnt;
                    }

                    // [WARNING] Don't skip here even if both "softPushbackX" and "softPushbackY" are zero, because we'd like to record them in "pushbackFrameLog"
                    softPushbacks[softPushbacksCnt].X = softPushbackX;
                    softPushbacks[softPushbacksCnt].Y = softPushbackY;
                    softPushbacksCnt++;
                }
            }

            if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
                currPushbackFrameLog.setSoftPushbacksByJoinIndex(currCharacterDownsync.JoinIndex, primarySoftOverlapIndex, softPushbacks /* [WARNING] by now "softPushbacks" is not yet normalized */, softPushbacksCnt, totOtherChCnt, cellOverlappedOtherChCnt, shapeOverlappedOtherChCnt, origResidueCollidedSt, origResidueCollidedEd);
            }
            // logger.LogInfo(String.Format("Before processing softPushbacks: effPushback={0}, softPushbacks={1}, primarySoftOverlapIndex={2}", effPushback.ToString(), Vector.VectorArrToString(softPushbacks, softPushbacksCnt), primarySoftOverlapIndex));

            processPrimaryAndImpactEffPushback(effPushback, softPushbacks, softPushbacksCnt, primarySoftOverlapIndex, SNAP_INTO_CHARACTER_OVERLAP, true);

            //logger.LogInfo(String.Format("After processing softPushbacks: effPushback={0}, softPushbacks={1}, primarySoftOverlapIndex={2}", effPushback.ToString(), Vector.VectorArrToString(softPushbacks, softPushbacksCnt), primarySoftOverlapIndex));                         
        }

        private static void _calcAllCharactersCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, RepeatedField<Trigger> nextRenderFrameTriggers, RepeatedField<Trap> nextRenderFrameTraps, ref int bulletLocalIdCounter, ref int bulletCnt, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPushbackEnabled, Collider[] dynamicRectangleColliders, int iSt, int iEd, FrameRingBuffer<Collider> residueCollided, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToTiledConfig, RdfPushbackFrameLog? currPushbackFrameLog, bool pushbackFrameLogEnabled, ILoggerBridge logger) { // Calc pushbacks for each player (after its movement) w/o bullets
            if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
                currPushbackFrameLog.Reset();
                currPushbackFrameLog.setMaxJoinIndex(roomCapacity+currNpcI);
            }
            int currRenderFrameId = currRenderFrame.Id;
            int primaryHardOverlapIndex;
            for (int i = iSt; i < iEd; i++) {
                primaryOverlapResult.reset();
                int joinIndex = i+1;
                var currCharacterDownsync = getChdFromRdf(joinIndex, roomCapacity, currRenderFrame);
                if (i >= roomCapacity && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = getChdFromChdArrs(joinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);

                var (skillConfig, bulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                /*
                if (null != skillConfig && null != bulletConfig && HunterSlidingId == skillConfig.Id && HunterSlidingHit1.StartupFrames < currCharacterDownsync.FramesInChState) {
                    logger.LogInfo(String.Format("rdfId={0}, using hunter sliding skill at ch.FramesInChState={1}", currRenderFrameId, currCharacterDownsync.FramesInChState));  
                }
                */
                bool activeBlIsMeleeBouncer = (null != bulletConfig && BulletType.Melee == bulletConfig.BType && 0 < bulletConfig.DefaultHardPushbackBounceQuota);

                var chConfig = characters[currCharacterDownsync.SpeciesId];
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                Trap? primaryTrap;
                TrapColliderAttr? primaryTrapColliderAttr;
                Bullet? primaryBlHardPushbackProvider;
                var hardPushbackNormsOfSingleCh = hardPushbackNormsArr[i];
                var effPushback = effPushbacks[i];
                bool isCharacterFlying = (currCharacterDownsync.OmitGravity || chConfig.OmitGravity);
                int hardPushbackCnt = calcHardPushbacksNormsForCharacter(currRenderFrame, chConfig, currCharacterDownsync, thatCharacterInNextFrame, isCharacterFlying, aCollider, aShape, hardPushbackNormsOfSingleCh, collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, out primaryTrap, out primaryTrapColliderAttr, out primaryBlHardPushbackProvider, residueCollided, logger);

                if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
                    currPushbackFrameLog.ResetJoinIndex(currCharacterDownsync.JoinIndex);
                    currPushbackFrameLog.setTouchingCellsByJoinIndex(currCharacterDownsync.JoinIndex, aCollider);
                    currPushbackFrameLog.setHardPushbacksByJoinIndex(currCharacterDownsync.JoinIndex, primaryHardOverlapIndex, hardPushbackNormsOfSingleCh /* [WARNING] by now "hardPushbackNormsOfSingleCh" is not yet normalized */, hardPushbackCnt);
                }

                if (0 < hardPushbackCnt) {
                    /*
                    if (2 <= hardPushbackCnt && 1 == currCharacterDownsync.JoinIndex) {
                       logger.LogInfo(String.Format("Rdf.Id={6}, before processing hardpushbacks with chState={3}, vx={7}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString(), currRenderFrame.Id, currCharacterDownsync.VirtualGridX));
                    }
                    */
                    processPrimaryAndImpactEffPushback(effPushback, hardPushbackNormsOfSingleCh, hardPushbackCnt, primaryHardOverlapIndex, SNAP_INTO_PLATFORM_OVERLAP, false);
                    /*
                    if (2 <= hardPushbackCnt && 1 == currCharacterDownsync.JoinIndex) {
                       logger.LogInfo(String.Format("Rdf.Id={6}, after processing hardpushbacks with chState={3}, vx={7}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString(), currRenderFrame.Id, currCharacterDownsync.VirtualGridX));
                    }
                    */
                }

                if (null != primaryBlHardPushbackProvider) {
                    if (0 > primaryOverlapResult.OverlapY) {
                        thatCharacterInNextFrame.FrictionVelX = primaryBlHardPushbackProvider.VelX;
                        thatCharacterInNextFrame.FrictionVelY = primaryBlHardPushbackProvider.VelY;
                    }
                } else if (null != primaryTrap) {
                    var trapConfig = trapConfigs[primaryTrap.ConfigFromTiled.SpeciesId];
                    if (0 > primaryOverlapResult.OverlapY) {
                        thatCharacterInNextFrame.FrictionVelX = primaryTrap.VelX;
                        thatCharacterInNextFrame.FrictionVelY = primaryTrap.VelY;
                        if (0 != primaryTrap.AngularFrameVelCos || 0 != primaryTrap.AngularFrameVelSin) {
                            var (trapCx, trapCy) = VirtualGridToPolygonColliderCtr(primaryTrap.VirtualGridX, primaryTrap.VirtualGridY); 
                            var (spinAnchorCx, spinAnchorCy) = (trapCx - .5f*primaryTrap.ConfigFromTiled.BoxCw + trapConfig.SpinAnchorX, trapCy - .5f*primaryTrap.ConfigFromTiled.BoxCh + trapConfig.SpinAnchorY); // Definition of spinAnchor .r.t. trap tile object bottom-left, while (trap.VirtualGridX, trap.VirtualGridY) is at the center of the trap tile object 
                            var (chCx, chCy) = (aCollider.X + .5f*aCollider.W, aCollider.Y + .5f*aCollider.H); 
                            var (chOffsetX, chOffsetY) = (chCx - spinAnchorCx, chCy - spinAnchorCy);
                            var (effTrapSpinCos, effTrapSpinSin) = (primaryTrap.SpinCos, primaryTrap.SpinSin);
                            if (0 != trapConfig.IntrinsicSpinCos || 0 != trapConfig.IntrinsicSpinSin) {
                                Vector.Rotate(primaryTrap.SpinCos, primaryTrap.SpinSin, trapConfig.IntrinsicSpinCos, trapConfig.IntrinsicSpinSin, out effTrapSpinCos, out effTrapSpinSin);
                            }
                            var (chOffsetProjectedX, chOffsetProjectedY) = (chOffsetX* effTrapSpinCos, chOffsetY* effTrapSpinCos); 
                            float tz = chOffsetProjectedX*primaryOverlapResult.OverlapY - chOffsetProjectedY*primaryOverlapResult.OverlapX; // cross-product
                            if (0 < tz * primaryTrap.AngularFrameVelSin) {
                                float chOffsetProjectedSquared = chOffsetProjectedX*chOffsetProjectedX + chOffsetProjectedY*chOffsetProjectedY;
                                float chOffsetProjectedInv = InvSqrt32(chOffsetProjectedSquared);
                                float chOffsetProjected = chOffsetProjectedSquared * chOffsetProjectedInv;
                                float angularFrictionVelCy = -Math.Abs(chOffsetProjected*primaryTrap.AngularFrameVelSin*effTrapSpinCos);
                                int angularFrictionVelY = (int) Math.Floor(angularFrictionVelCy * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
                                if (angularFrictionVelY > chConfig.DownSlopePrimerVelY) {
                                    angularFrictionVelY = chConfig.DownSlopePrimerVelY;
                                }
                                thatCharacterInNextFrame.FrictionVelY += angularFrictionVelY;
                            }
                        }
                    }
                } else {
                    if (isInJumpStartup(currCharacterDownsync, chConfig) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig)) {
                        thatCharacterInNextFrame.FrictionVelX = currCharacterDownsync.FrictionVelX;
                        thatCharacterInNextFrame.FrictionVelY = currCharacterDownsync.FrictionVelY;
                    } else {
                        thatCharacterInNextFrame.FrictionVelX = 0;
                        thatCharacterInNextFrame.FrictionVelY = 0;
                    }
                }

                bool landedOnGravityPushback = false;
                float normAlignmentWithGravity = (primaryOverlapResult.OverlapY * -1f);
                float normAlignmentWithAntiGravity = (primaryOverlapResult.OverlapY * +1f);
                // Hold wall alignments of the primaryOverlapResult of hardPushbacks first, it'd be used later 
                float normAlignmentWithHorizon1 = (primaryOverlapResult.OverlapX * +1f);
                float normAlignmentWithHorizon2 = (primaryOverlapResult.OverlapX * -1f);
                thatCharacterInNextFrame.OnSlope = ((InAirIdle1ByWallJump != thatCharacterInNextFrame.CharacterState && OnWallIdle1 != thatCharacterInNextFrame.CharacterState && OnWallAtk1 != thatCharacterInNextFrame.CharacterState) && 0 != primaryOverlapResult.OverlapY && 0 != primaryOverlapResult.OverlapX);

                float projectedDirVel = (thatCharacterInNextFrame.DirX * primaryOverlapResult.OverlapX);
                thatCharacterInNextFrame.OnSlopeFacingDown = (thatCharacterInNextFrame.OnSlope && 0 > projectedDirVel);

                // Kindly remind that (primaryOverlapResult.OverlapX, primaryOverlapResult.OverlapY) points INTO the slope :) 
                float projectedVel = (thatCharacterInNextFrame.VelX * primaryOverlapResult.OverlapX + thatCharacterInNextFrame.VelY * primaryOverlapResult.OverlapY); // This value is actually in VirtualGrid unit, but converted to float, thus it'd be eventually rounded 
                // [WARNING] The condition "0 > projectedVel" is just to prevent character from unintended sliding on slope due to "CharacterConfig.DownSlopePrimerVelY" -- it's NOT applicable for bullets!
                bool goingDown = (thatCharacterInNextFrame.OnSlope && !currCharacterDownsync.JumpStarted && thatCharacterInNextFrame.VelY <= 0 && 0 > projectedVel); // We don't care about going up, it's already working...  
                if (!activeBlIsMeleeBouncer) {
                    if (goingDown) {
                        /*
                        if (SPECIES_SKELEARCHER == currCharacterDownsync.SpeciesId && 1 == currCharacterDownsync.Id) {
                            logger.LogInfo(String.Format("Rdf.id={0} BEFOER, chState={1}, velX={2}, velY={3}, virtualGridX={4}, virtualGridY={5}: going down", currRenderFrame.Id, currCharacterDownsync.CharacterState, thatCharacterInNextFrame.VelX, thatCharacterInNextFrame.VelY, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY));
                        }
                        */
                        float newVelXApprox = thatCharacterInNextFrame.VelX - primaryOverlapResult.OverlapX * projectedVel;
                        float newVelYApprox = thatCharacterInNextFrame.VelY - primaryOverlapResult.OverlapY * projectedVel;
                        thatCharacterInNextFrame.VelX = 0 > newVelXApprox ? (int)Math.Floor(newVelXApprox) : (int)Math.Ceiling(newVelXApprox);
                        thatCharacterInNextFrame.VelY = (int)Math.Floor(newVelYApprox); // "VelY" here is < 0, take the floor to get a larger absolute value!
                        /*
                        if (SPECIES_SKELEARCHER == currCharacterDownsync.SpeciesId && 1 == currCharacterDownsync.Id) {
                            logger.LogInfo(String.Format("Rdf.id={0} AFTER, chState={1}, velX={2}, velY={3}, virtualGridX={4}, virtualGridY={5}: going down", currRenderFrame.Id, currCharacterDownsync.CharacterState, thatCharacterInNextFrame.VelX, thatCharacterInNextFrame.VelY, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY));
                        }
                        */
                    } else if ((!currCharacterDownsync.OmitGravity && !chConfig.OmitGravity) && thatCharacterInNextFrame.OnSlope && nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState) && 0 == thatCharacterInNextFrame.VelX) {
                        // [WARNING] Prevent down-slope sliding, might not be preferred for some game designs, disable this if you need sliding on the slope
                        if (!proactiveJumpingSet.Contains(currCharacterDownsync.CharacterState)) {
                            thatCharacterInNextFrame.VelY = 0;
                        }
                    }
                }

                if (!chConfig.OmitGravity && !currCharacterDownsync.OmitGravity) {
                    if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                        landedOnGravityPushback = true;
                        if (null != primaryTrapColliderAttr && 0 == primaryOverlapResult.OverlapX && 0 > primaryOverlapResult.OverlapY && 0 >= thatCharacterInNextFrame.FramesCapturedByInertia) {
                            var trapConfig = trapConfigs[primaryTrapColliderAttr.SpeciesId];
                            thatCharacterInNextFrame.FrictionVelX += trapConfig.ConstFrictionVelXTop;       
                        }
                        /*
                           if (1 == currCharacterDownsync.JoinIndex) {
                           logger.LogInfo(String.Format("Landed with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString()));
                           }
                         */
                    }
                } else if ((Idle1 == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && chConfig.AntiGravityWhenIdle) {
                    if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithAntiGravity) {
                        landedOnGravityPushback = true;
                        if (null != primaryTrapColliderAttr && 0 == primaryOverlapResult.OverlapX && 0 < primaryOverlapResult.OverlapY && 0 >= thatCharacterInNextFrame.FramesCapturedByInertia) {
                            var trapConfig = trapConfigs[primaryTrapColliderAttr.SpeciesId];
                            thatCharacterInNextFrame.FrictionVelX += trapConfig.ConstFrictionVelXBottom;       
                        }
                        /*
                           if (1 == currCharacterDownsync.JoinIndex) {
                           logger.LogInfo(String.Format("Landed with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString()));
                           }
                         */
                    }
                }

                bool repelSoftPushback = currCharacterDownsync.RepelSoftPushback;
                if (skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) {
                    var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                    if (null != currSkillConfig.SelfNonStockBuff) {
                        repelSoftPushback |= currSkillConfig.SelfNonStockBuff.RepelSoftPushback; 
                    }
                }
    
                int softPushbacksCnt = 0, primarySoftOverlapIndex = -1;
                float primarySoftPushbackX = float.MinValue, primarySoftPushbackY = float.MinValue;
                _handleSingleChResidualPushbacks(currRenderFrame, inputBuffer, roomCapacity, aCollider, aShape, currCharacterDownsync, thatCharacterInNextFrame, chConfig, skillConfig, bulletConfig, hardPushbackNormsOfSingleCh, hardPushbackCnt, primaryHardOverlapIndex, repelSoftPushback, softPushbacks, ref softPushbacksCnt, ref primarySoftOverlapIndex, ref primarySoftPushbackX, ref primarySoftPushbackY, softPushbackEnabled, residueCollided, ref bulletLocalIdCounter, ref bulletCnt, ref overlapResult, ref primaryOverlapResult, collision, effPushback, nextRenderFrameBullets, nextRenderFrameTraps, nextRenderFrameTriggers, trapLocalIdToColliderAttrs, triggerEditorIdToTiledConfig, ref normAlignmentWithGravity, ref landedOnGravityPushback, unconfirmedBattleResults, ref confirmedBattleResult, currPushbackFrameLog, pushbackFrameLogEnabled, logger);

                if (!landedOnGravityPushback && !currCharacterDownsync.InAir && !activeBlIsMeleeBouncer) {
                    /*
                    if (1 == currCharacterDownsync.JoinIndex) {
                        logger.LogInfo(String.Format("Rdf.Id={0}, character vx={1},vy={2} slipped with aShape={3}: hardPushbackNormsOfSingleCh={4}, effPushback={5}, touchCells=\n{6}", currRenderFrame.Id, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, aShape.ToString(false), Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), aCollider.TouchingCellsStaticColliderStr()));
                    }
                    */
                    if (0 > thatCharacterInNextFrame.VelY) {
                        thatCharacterInNextFrame.VelY = 0;
                        if (primaryOverlapResult.SideSuppressingTop) {
                            // [WARNING] Would block character air-walking in "_processInertiaWalking"!
                            if (chConfig.InertiaFramesToRecover > thatCharacterInNextFrame.FramesCapturedByInertia) {
                                thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                            }
                            thatCharacterInNextFrame.VelX = 0;
                        }
                    }
                }

                if (landedOnGravityPushback && !activeBlIsMeleeBouncer) {
                    if (!currCharacterDownsync.OmitGravity && !chConfig.OmitGravity) {
                        thatCharacterInNextFrame.InAir = false;
                        thatCharacterInNextFrame.RemainingAirJumpQuota = chConfig.DefaultAirJumpQuota;
                        thatCharacterInNextFrame.RemainingAirDashQuota = chConfig.DefaultAirDashQuota;
                        if (TERMINATING_TRIGGER_ID != currCharacterDownsync.SubscribesToTriggerLocalId) {
                            if (chConfig.HasDimmedAnim) {
                                thatCharacterInNextFrame.CharacterState = Dimmed;
                            } else {
                                thatCharacterInNextFrame.CharacterState = LayDown1;
                            }
                            thatCharacterInNextFrame.FramesToRecover = MAX_INT;
                        }
                        if (null != primaryTrapColliderAttr && primaryTrapColliderAttr.ProvidesSlipJump) {
                            thatCharacterInNextFrame.PrimarilyOnSlippableHardPushback = true;
                        }
                        bool fallStopping = (currCharacterDownsync.InAir && 0 >= currCharacterDownsync.VelY && !isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig) && !isInJumpStartup(thatCharacterInNextFrame, chConfig));
                        if (fallStopping) {
                            resetJumpStartup(thatCharacterInNextFrame);
                            if (Dying == thatCharacterInNextFrame.CharacterState) {
                                thatCharacterInNextFrame.VelX = 0;
                                thatCharacterInNextFrame.VelY = 0;
                                if (SPECIES_NONE_CH != chConfig.TransformIntoSpeciesIdUponDeath && 0 >= thatCharacterInNextFrame.FramesToRecover) {
                                    var nextChConfig = characters[chConfig.TransformIntoSpeciesIdUponDeath];
                                    AssignToCharacterDownsyncFromCharacterConfig(nextChConfig, thatCharacterInNextFrame, true);
                                    thatCharacterInNextFrame.CharacterState = TransformingIntoFromDeath;
                                    thatCharacterInNextFrame.FramesToRecover = BATTLE_DYNAMICS_FPS; // Temporarily hardcoded

                                    revertAllBuffsAndDebuffs(currCharacterDownsync, thatCharacterInNextFrame);
                                }
                            } else if (BlownUp1 == thatCharacterInNextFrame.CharacterState) {
                                thatCharacterInNextFrame.VelX = 0;
                                thatCharacterInNextFrame.VelY = 0;
                                thatCharacterInNextFrame.CharacterState = LayDown1;
                                thatCharacterInNextFrame.FramesToRecover = chConfig.LayDownFrames;
                            } else {
                                // [WARNING] Other "chState transitions" are later handled by "_processEffPushbacks".
                            }

                            if (!thatCharacterInNextFrame.OnSlope) {
                                thatCharacterInNextFrame.VelY = chConfig.DownSlopePrimerVelY;
                            } else {
                                if (thatCharacterInNextFrame.OnSlopeFacingDown && !nonAttackingSet.Contains(currCharacterDownsync.CharacterState)) {
                                    thatCharacterInNextFrame.VelY = chConfig.DownSlopePrimerVelY;
                                } else {
                                    thatCharacterInNextFrame.VelY = 0;
                                }
                            }

                            if (shrinkedSizeSet.Contains(currCharacterDownsync.CharacterState) && !shrinkedSizeSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                                // [WARNING] To prevent bouncing due to abrupt change of collider shape, it's important that we check "currCharacterDownsync" instead of "thatCharacterInNextFrame" here!
                                int extraSafeGapToPreventBouncing = (chConfig.DefaultSizeY >> 2);
                                var halfColliderVhDiff = ((chConfig.DefaultSizeY - (chConfig.ShrinkedSizeY + extraSafeGapToPreventBouncing)) >> 1);
                                var (_, halfColliderChDiff) = VirtualGridToPolygonColliderCtr(0, halfColliderVhDiff);
                                effPushback.Y -= halfColliderChDiff;
                                    
                                /*
                                if (1 == currCharacterDownsync.JoinIndex) {
                                    logger.LogInfo(String.Format("rdf.Id={0}, Fall stopped with chState={1}, virtualGridY={2}: hardPushbackNormsArr[i:{3}]={4}, effPushback={5}, halfColliderChDiff={6}", currRenderFrame.Id, currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), halfColliderChDiff));
                                }
                                */
                            }
                        } else {
                            // landedOnGravityPushback not fallStopping, could be in LayDown or GetUp or Dying
                            if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                                if (Dying == thatCharacterInNextFrame.CharacterState) {
                                    if (SPECIES_NONE_CH != chConfig.TransformIntoSpeciesIdUponDeath && 0 >= thatCharacterInNextFrame.FramesToRecover) {
                                        var nextChConfig = characters[chConfig.TransformIntoSpeciesIdUponDeath];
                                        AssignToCharacterDownsyncFromCharacterConfig(nextChConfig, thatCharacterInNextFrame, true);
                                        thatCharacterInNextFrame.Hp = nextChConfig.Hp;
                                        thatCharacterInNextFrame.CharacterState = TransformingIntoFromDeath;
                                        thatCharacterInNextFrame.FramesToRecover = BATTLE_DYNAMICS_FPS; // Temporarily hardcoded
                                    }
                                } else if (BlownUp1 == thatCharacterInNextFrame.CharacterState) {
                                    thatCharacterInNextFrame.VelX = 0;
                                    thatCharacterInNextFrame.VelY = 0;
                                    thatCharacterInNextFrame.CharacterState = LayDown1;
                                    thatCharacterInNextFrame.FramesToRecover = chConfig.LayDownFrames;
                                } else if (LayDown1 == thatCharacterInNextFrame.CharacterState) {
                                    if (0 == thatCharacterInNextFrame.FramesToRecover) {
                                        thatCharacterInNextFrame.CharacterState = GetUp1;
                                        thatCharacterInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                                    }
                                } else if (GetUp1 == thatCharacterInNextFrame.CharacterState) {
                                    if (0 == thatCharacterInNextFrame.FramesToRecover) {
                                        thatCharacterInNextFrame.CharacterState = Idle1;
                                        thatCharacterInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;

                                        int extraSafeGapToPreventBouncing = (chConfig.DefaultSizeY >> 2);
                                        var halfColliderVhDiff = ((chConfig.DefaultSizeY - (chConfig.LayDownSizeY + extraSafeGapToPreventBouncing)) >> 1);
                                        var (_, halfColliderChDiff) = VirtualGridToPolygonColliderCtr(0, halfColliderVhDiff);
                                        effPushback.Y -= halfColliderChDiff;
                                    }
                                } else if (0 >= thatCharacterInNextFrame.VelY && !thatCharacterInNextFrame.OnSlope) {
                                    // [WARNING] Covers 2 situations:
                                    // 1. Walking up to a flat ground then walk back down, note that it could occur after a jump on the slope, thus should recover "DownSlopePrimerVelY";
                                    // 2. Dashing down to a flat ground then walk back up. 
                                    thatCharacterInNextFrame.VelY = chConfig.DownSlopePrimerVelY;
                                }
                            }
                            /*
                               if (1 == currCharacterDownsync.JoinIndex) {
                               logger.LogInfo(String.Format("Landed without fallstopping with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY));
                               }
                             */
                        }
                    } else if (chConfig.AntiGravityWhenIdle) {
                        thatCharacterInNextFrame.InAir = false;
                        bool fallStopping = (currCharacterDownsync.InAir && 0 <= currCharacterDownsync.VelY);
                        if (fallStopping) {
                            thatCharacterInNextFrame.VelX = 0;
                            thatCharacterInNextFrame.VelY = 0;
                            resetJumpStartup(thatCharacterInNextFrame);
                            if (Dying == thatCharacterInNextFrame.CharacterState) {
                                // No update needed for Dying
                            } else {
                                // [WARNING] Deliberately left blank, it's well understood that there're other possibilities and they're later handled by "_processEffPushbacks", the handling here is just for helping edge cases!
                            }

                            if (shrinkedSizeSet.Contains(currCharacterDownsync.CharacterState) && !shrinkedSizeSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                                // [WARNING] To prevent bouncing due to abrupt change of collider shape, it's important that we check "currCharacterDownsync" instead of "thatCharacterInNextFrame" here!
                                int extraSafeGapToPreventBouncing = (chConfig.DefaultSizeY >> 2);
                                var halfColliderVhDiff = ((chConfig.DefaultSizeY - (chConfig.ShrinkedSizeY + extraSafeGapToPreventBouncing)) >> 1);
                                var (_, halfColliderChDiff) = VirtualGridToPolygonColliderCtr(0, halfColliderVhDiff);
                                effPushback.Y -= halfColliderChDiff;
                                /*
                                if (1 == currCharacterDownsync.JoinIndex) {
                                    logger.LogInfo(String.Format("Rdf.Id={6}, Fall stopped with chState={3}, vy={4}, halfColliderChDiff={5}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, halfColliderChDiff, currRenderFrame.Id));
                                }
                                */
                            }
                        } else {
                            // landedOnGravityPushback not fallStopping, could only be Dying
                            if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                                if (Dying == thatCharacterInNextFrame.CharacterState) {
                                    // No update needed for Dying
                                } else if (0 <= thatCharacterInNextFrame.VelY && !thatCharacterInNextFrame.OnSlope) {
                                    thatCharacterInNextFrame.VelY = chConfig.DownSlopePrimerVelY;
                                }
                            }
                            /*
                               if (1 == currCharacterDownsync.JoinIndex) {
                               logger.LogInfo(String.Format("Landed without fallstopping with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}", i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt), effPushback.ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY));
                               }
                             */
                        }
                    }
                }

                /*
                [WARNING] There's not much concern about "wall dynamics" on (currCharacterDownsync.OmitGravity || chConfig.OmitGravity), by far they're mutually exclusive. 
                */
                if (!thatCharacterInNextFrame.OnWall) {
                    // [WARNING] "false == thatCharacterInNextFrame.OnWall" by now implies primary is NOT wall!
                    thatCharacterInNextFrame.OnWallNormX = 0;
                    thatCharacterInNextFrame.OnWallNormY = 0;
                    /*
                    if (0 < thatCharacterInNextFrame.VelX && currCharacterDownsync.OnWall && 1 == currCharacterDownsync.JoinIndex) {
                        logger.LogInfo(String.Format("Rdf.Id={0}, dropped from OnWall with currChState={1}, primaryOverlapResult={2}: hardPushbackNormsArr[i:{3}]={4}", currRenderFrame.Id, currCharacterDownsync.CharacterState, primaryOverlapResult.ToString(), i, Vector.VectorArrToString(hardPushbackNormsOfSingleCh, hardPushbackCnt)));
                    }
                    */
                } else if (chConfig.OnWallEnabled && (null == primaryTrapColliderAttr || (null != primaryTrapColliderAttr && !primaryTrapColliderAttr.ProhibitsWallGrabbing))) {
                    // [WARNING] To grab on a wall, the wall must be the primary hard-pushback for character!
                    bool shouldGrabWall = false;
                    if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1) {
                        shouldGrabWall = true;
                        thatCharacterInNextFrame.OnWallNormX = +1;
                        thatCharacterInNextFrame.OnWallNormY = 0;
                    } else if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2) {
                        shouldGrabWall = true;
                        thatCharacterInNextFrame.OnWallNormX = -1;
                        thatCharacterInNextFrame.OnWallNormY = 0;
                    } else {
                        // [WARNING] Deliberately NOT overwriting!
                    }

                    if (shouldGrabWall) {
                        switch (thatCharacterInNextFrame.CharacterState) {
                            case Walking:
                            case InAirIdle1NoJump:
                            case InAirIdle1ByJump:
                            case InAirIdle2ByJump:
                            case InAirIdle1ByWallJump:
                                bool hasBeenOnWallChState = (OnWallIdle1 == currCharacterDownsync.CharacterState || OnWallAtk1 == currCharacterDownsync.CharacterState);
                                // [WARNING] "MAGIC_FRAMES_TO_BE_ON_WALL" allows "InAirIdle1ByWallJump" to leave the current wall within a reasonable count of renderFrames, instead of always forcing "InAirIdle1ByWallJump" to immediately stick back to the wall!
                                bool hasBeenOnWallCollisionResultForSameChState = (chConfig.OnWallEnabled && currCharacterDownsync.OnWall && MAGIC_FRAMES_TO_BE_ON_WALL <= thatCharacterInNextFrame.FramesInChState);
                                if (!isInJumpStartup(thatCharacterInNextFrame, chConfig) && !isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig) && (hasBeenOnWallChState || hasBeenOnWallCollisionResultForSameChState)) {
                                    thatCharacterInNextFrame.CharacterState = OnWallIdle1;
                                    resetJumpStartup(thatCharacterInNextFrame);
                                }
                                break;
                        }

                        if (!currCharacterDownsync.OnWall) {
                            // [WARNING] Transition of "wall grabbing: false -> true" should also help reset these quotas!
                            thatCharacterInNextFrame.RemainingAirJumpQuota = chConfig.DefaultAirJumpQuota;
                            thatCharacterInNextFrame.RemainingAirDashQuota = chConfig.DefaultAirDashQuota;
                        }
                    }
                }

                /* 
                if (SPECIES_STONE_GOLEM == currCharacterDownsync.SpeciesId && Atk2 == currCharacterDownsync.CharacterState && 40 <= currCharacterDownsync.FramesInChState) {
                    logger.LogInfo("_calcAllCharactersCollisions/end, currRdfId=" + currRenderFrame.Id + "(VelX = " + currCharacterDownsync.VelX + ", VelY = " + currCharacterDownsync.VelY + "), (NextVelX = " + thatCharacterInNextFrame.VelX + ", NextVelY = " + thatCharacterInNextFrame.VelY + "). landedOnGravity = " + landedOnGravityPushback);
                }
                */
            }
        }

        private static void _calcFallenDeath(RoomDownsyncFrame currRenderFrame, bool rdfAllConfirmed, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Pickable> nextRenderFramePickables, ILoggerBridge logger) {
            for (int i = 0; i < roomCapacity + currNpcI; i++) {
                int joinIndex = i+1;
                var currCharacterDownsync = getChdFromRdf(joinIndex, roomCapacity, currRenderFrame);
                if (i >= roomCapacity && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = getChdFromChdArrs(joinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
                var chConfig = characters[currCharacterDownsync.SpeciesId];

                float characterVirtualGridTop = currCharacterDownsync.VirtualGridY + (chConfig.DefaultSizeY >> 1);
                if (0 > characterVirtualGridTop && Dying != currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.Hp = 0;
                    thatCharacterInNextFrame.VelX = 0;
                    thatCharacterInNextFrame.CharacterState = Dying;
                    thatCharacterInNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
                    resetJumpStartup(thatCharacterInNextFrame);
                }
            }

            for (int i = 0; i < nextRenderFramePickables.Count; i++) {
                var nextPickable = nextRenderFramePickables[i];
                if (TERMINATING_PICKABLE_LOCAL_ID == nextPickable.PickableLocalId) break;
                float pickableVirtualGridTop = nextPickable.VirtualGridY + (DEFAULT_PICKABLE_HITBOX_SIZE_Y >> 1);
                if (0 > pickableVirtualGridTop && PickableState.Pidle == nextPickable.PkState) {
                    nextPickable.PkState = PickableState.Pdisappearing;
                    nextPickable.FramesInPkState = 0;
                    nextPickable.RemainingLifetimeRdfCount = DEFAULT_PICKABLE_DISAPPEARING_ANIM_FRAMES; // When ended, will be reclaimed by "_moveAndInsertPickableColliders"
                }
            }
        }

        public static CharacterDownsync getChdFromRdf(int joinIndex, int roomCapacity, RoomDownsyncFrame rdf) {
            if (roomCapacity >= joinIndex) {
                return rdf.PlayersArr[joinIndex-1];
            } else {
                return rdf.NpcsArr[joinIndex-roomCapacity-1];
            }
        }

        public static CharacterDownsync getChdFromChdArrs(int joinIndex, int roomCapacity, RepeatedField<CharacterDownsync> players, RepeatedField<CharacterDownsync> npcs) {
            if (roomCapacity >= joinIndex) {
                return players[joinIndex-1];
            } else {
                return npcs[joinIndex-roomCapacity-1];
            }
        }

        private static void _processEffPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Pickable> nextRenderFramePickables, Vector[] effPushbacks, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, int pickableColliderCntOffset, int colliderCnt, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, ILoggerBridge logger) {
            for (int i = 0; i < roomCapacity + currNpcI; i++) {
                int joinIndex = i+1;
                var currCharacterDownsync = getChdFromRdf(joinIndex, roomCapacity, currRenderFrame);
                if (i >= roomCapacity && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = getChdFromChdArrs(joinIndex, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs);
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                Collider aCollider = dynamicRectangleColliders[i];
                
                /*
                if (77 == thatCharacterInNextFrame.ActiveSkillId) {
                    logger.LogInfo("_processEffPushbacks/begin, currRdfId=" + currRenderFrame.Id  + ", used DiverImpact, next VelX = " + thatCharacterInNextFrame.VelX);
                }
                if (SPECIES_STONE_GOLEM == currCharacterDownsync.SpeciesId && Atk2 == currCharacterDownsync.CharacterState && 40 <= currCharacterDownsync.FramesInChState) {
                    logger.LogInfo("_processEffPushbacks/begin, currRdfId=" + currRenderFrame.Id + ", VelY = " + currCharacterDownsync.VelY + ", NextVelY = " + thatCharacterInNextFrame.VelY);
                }
                if (InAirIdle2ByJump == currCharacterDownsync.CharacterState && InAirIdle2ByJump != thatCharacterInNextFrame.CharacterState) {
                    logger.LogInfo("_processEffPushbacks/begin, currRdfId=" + currRenderFrame.Id + ", transitting from InAirIdle2ByJump to " + thatCharacterInNextFrame.CharacterState + ", currVelX=" + currCharacterDownsync.VelX + ", nextVelX=" + thatCharacterInNextFrame.VelX);
                }
                */

                // Update "virtual grid position"
                (thatCharacterInNextFrame.VirtualGridX, thatCharacterInNextFrame.VirtualGridY) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                // Update "CharacterState"
                CharacterState oldNextCharacterState = thatCharacterInNextFrame.CharacterState;
                var (activeSkill, activeBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                BuffConfig? activeSkillBuff = null;
                if (null != activeSkill) {
                    activeSkillBuff = activeSkill.SelfNonStockBuff;
                }

                if (TERMINATING_TRIGGER_ID != currCharacterDownsync.SubscribesToTriggerLocalId) {
                } else if (thatCharacterInNextFrame.InAir) {
                    /*
                       if (0 == currCharacterDownsync.SpeciesId && false == currCharacterDownsync.InAir) {
                       logger.LogInfo(String.Format("Rdf.id={0}, chState={1}, framesInChState={6}, velX={2}, velY={3}, virtualGridX={4}, virtualGridY={5}: transitted to InAir", currRenderFrame.Id, currCharacterDownsync.CharacterState, currCharacterDownsync.VelX, currCharacterDownsync.VelY, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, currCharacterDownsync.FramesInChState));
                       }
                     */
                    if (!inAirSet.Contains(oldNextCharacterState)) {
                        switch (oldNextCharacterState) {
                            case Idle1:
                            case Def1:  
                            case Def1Broken:  
                            case Walking:
                            case TurnAround:
                                if (Walking == oldNextCharacterState) {
                                    if (chConfig.OmitGravity) { 
                                        // [WARNING] No need to distinguish in this case.
                                        break;
                                    } else if (thatCharacterInNextFrame.OmitGravity) {
                                        thatCharacterInNextFrame.CharacterState = InAirWalking;
                                        break;
                                    }
                                }
                                if (Idle1 == oldNextCharacterState) {
                                    var defaultInAirIdleChState = chConfig.UseIdle1AsFlyingIdle ? Idle1 : InAirWalking;
                                    if (chConfig.OmitGravity) {

                                    } else if (thatCharacterInNextFrame.OmitGravity) {
                                        thatCharacterInNextFrame.CharacterState = defaultInAirIdleChState;
                                        break;
                                    }
                                }
                                if ((currCharacterDownsync.OnWall && currCharacterDownsync.JumpTriggered && chConfig.OnWallEnabled) || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
                                } else if (currCharacterDownsync.JumpTriggered || InAirIdle1ByJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                                } else if (currCharacterDownsync.JumpTriggered || InAirIdle2ByJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle2ByJump;
                                } else {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                                }
                                if (Def1 == oldNextCharacterState) {
                                    thatCharacterInNextFrame.RemainingDef1Quota = 0;
                                }
                                break;
                            case WalkStopping:
                                if (chConfig.OmitGravity) { 
                                    // [WARNING] No need to distinguish in this case.
                                    break;
                                } else if (thatCharacterInNextFrame.OmitGravity) {
                                    thatCharacterInNextFrame.CharacterState = (chConfig.HasInAirWalkStoppingAnim ? InAirWalkStopping : InAirWalking);
                                    break;
                                }
                                break;
                            case Atk1:
                                thatCharacterInNextFrame.CharacterState = InAirAtk1;
                                // No inAir transition for ATK2/ATK3 for now
                                break;
                            case Atked1:
                                thatCharacterInNextFrame.CharacterState = InAirAtked1;
                                break;
                        }
                    }
                } else {
                    // next frame NOT in air
                    if (inAirSet.Contains(oldNextCharacterState) && BlownUp1 != oldNextCharacterState && OnWallIdle1 != oldNextCharacterState && Dashing != oldNextCharacterState) {
                        switch (oldNextCharacterState) {
                            case InAirIdle1NoJump:
                            case InAirIdle2ByJump:
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                            case InAirIdle1ByJump:
                            case InAirIdle1ByWallJump:
                                if ( isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame, chConfig) || isInJumpStartup(thatCharacterInNextFrame, chConfig) ) {
                                    // [WARNING] Don't change CharacterState in this special case!
                                    break;
                                }
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                            case InAirAtked1:
                                thatCharacterInNextFrame.CharacterState = Atked1;
                                break;
                            case InAirAtk1:
                            case InAirAtk2:
                                if (null != activeBulletConfig && activeBulletConfig.RemainsUponHit) {
                                    thatCharacterInNextFrame.FramesToRecover = currCharacterDownsync.FramesToRecover - 1;
                                }
                                break;
                            case Def1:  
                            case Def1Broken:  
                                // Not changing anything.
                                break;
                            default:
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                        }
                    } else if (thatCharacterInNextFrame.ForcedCrouching && chConfig.CrouchingEnabled && !isCrouching(oldNextCharacterState, chConfig)) {
                        switch (oldNextCharacterState) {
                            case Idle1:
                            case InAirIdle1ByJump:
                            case InAirIdle2ByJump:
                            case InAirIdle1NoJump:
                            case InAirIdle1ByWallJump:
                            case Walking:
                            case GetUp1:
                            case TurnAround:
                                thatCharacterInNextFrame.CharacterState = CrouchIdle1;
                                break;
                            case Atk1:
                            case Atk2:
                                if (chConfig.CrouchingAtkEnabled) {
                                    thatCharacterInNextFrame.CharacterState = CrouchAtk1;
                                } else {
                                    thatCharacterInNextFrame.CharacterState = CrouchIdle1;
                                }
                                break;
                            case Atked1:
                            case InAirAtked1:
                                thatCharacterInNextFrame.CharacterState = CrouchAtked1;
                                break;
                            case BlownUp1:
                            case LayDown1:
                            case Dying:
                                break;
                            default:
                                throw new ArgumentException(String.Format("At rdf.Id={0}, unable to force crouching for character {1}", currRenderFrame.Id, i < roomCapacity ? stringifyPlayer(thatCharacterInNextFrame) : stringifyNpc(thatCharacterInNextFrame) ));
                        }
                    }
                }

                // Reset "FramesInChState" if "CharacterState" is changed
                if (thatCharacterInNextFrame.CharacterState != currCharacterDownsync.CharacterState) {
                    if (Walking == currCharacterDownsync.CharacterState && (WalkingAtk1 == thatCharacterInNextFrame.CharacterState || WalkingAtk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.FramesInChState = 0;
                    } else if ((WalkingAtk1 == currCharacterDownsync.CharacterState || WalkingAtk4 == currCharacterDownsync.CharacterState) && Walking == thatCharacterInNextFrame.CharacterState) {
                        thatCharacterInNextFrame.FramesInChState = currCharacterDownsync.FramesInChState + 1;
                    } else if ((Atk1 == currCharacterDownsync.CharacterState && WalkingAtk1 == thatCharacterInNextFrame.CharacterState) || (Atk4 == currCharacterDownsync.CharacterState && WalkingAtk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.FramesInChState = currCharacterDownsync.FramesInChState + 1;
                    } else if ((WalkingAtk1 == thatCharacterInNextFrame.CharacterState && Atk1 == thatCharacterInNextFrame.CharacterState) || (WalkingAtk4 == thatCharacterInNextFrame.CharacterState && Atk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.FramesInChState = currCharacterDownsync.FramesInChState + 1;
                    } else {
                        bool isAtk1Transition = (Atk1 == currCharacterDownsync.CharacterState && InAirAtk1 == thatCharacterInNextFrame.CharacterState) || (InAirAtk1 == currCharacterDownsync.CharacterState && Atk1 == thatCharacterInNextFrame.CharacterState);
                        bool isAtked1Transition = (Atked1 == currCharacterDownsync.CharacterState && InAirAtked1 == thatCharacterInNextFrame.CharacterState) || (InAirAtked1 == currCharacterDownsync.CharacterState && Atked1 == thatCharacterInNextFrame.CharacterState);  
                        if (!isAtk1Transition && !isAtked1Transition) {
                            thatCharacterInNextFrame.FramesInChState = 0;
                        }
                    }
                }

                thatCharacterInNextFrame.PrevWasCrouching = isCrouching(currCharacterDownsync.CharacterState, chConfig);

                // Remove any active skill if not attacking
                bool notDashing = isNotDashing(thatCharacterInNextFrame);
                if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState) && notDashing) {
                    thatCharacterInNextFrame.ActiveSkillId = NO_SKILL;
                    thatCharacterInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                }

                if ((InAirAtked1 == thatCharacterInNextFrame.CharacterState || CrouchAtked1 == thatCharacterInNextFrame.CharacterState || Atked1 == thatCharacterInNextFrame.CharacterState) && (MAX_UINT >> 1) < thatCharacterInNextFrame.FramesToRecover) {
                    //logger.LogWarn(String.Format("thatCharacterInNextFrame has invalid frameToRecover={0} and chState={1}! Re-assigning characterState to BlownUp1 for recovery!", thatCharacterInNextFrame.FramesToRecover, thatCharacterInNextFrame.CharacterState));
                    thatCharacterInNextFrame.CharacterState = BlownUp1;
                    if (thatCharacterInNextFrame.OmitGravity) {
                        if (chConfig.OmitGravity) {
                            thatCharacterInNextFrame.FramesToRecover = DEFAULT_BLOWNUP_FRAMES_FOR_FLYING; 
                        } else {
                            thatCharacterInNextFrame.OmitGravity = false; 
                        }
                    }
                }

                if (Def1 != thatCharacterInNextFrame.CharacterState) {
                    thatCharacterInNextFrame.RemainingDef1Quota = 0;
                } else {
                    bool isWalkingAutoDef1 = (Walking == currCharacterDownsync.CharacterState && chConfig.WalkingAutoDef1);
                    bool isSkillAutoDef1 = (null != activeSkillBuff && activeSkillBuff.AutoDef1);
                    if (Def1 != currCharacterDownsync.CharacterState && !isWalkingAutoDef1 && !isSkillAutoDef1) {
                        thatCharacterInNextFrame.FramesSinceLastDamaged = 0; // Clean up for correctly animating "Def1Atked1"
                        thatCharacterInNextFrame.DamageElementalAttrs = 0;
                    }
                }

                if (thatCharacterInNextFrame.InAir) {
                    bool omitGravity = (currCharacterDownsync.OmitGravity || chConfig.OmitGravity);
                    if (!omitGravity && NO_SKILL != currCharacterDownsync.ActiveSkillId) {     
                        var chSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                        if (null != chSkillConfig) {
                            omitGravity |= (null != chSkillConfig.SelfNonStockBuff && chSkillConfig.SelfNonStockBuff.OmitGravity); 
                        }
                    }

                    if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState) && omitGravity && 0 < thatCharacterInNextFrame.VelY && !chConfig.AntiGravityWhenIdle) {
                        if (thatCharacterInNextFrame.VelY > chConfig.MaxAscendingVelY) {
                            thatCharacterInNextFrame.VelY = chConfig.MaxAscendingVelY;
                        }
                    }
                }
        
                if (!thatCharacterInNextFrame.OmitGravity && !chConfig.OmitGravity) {
                    if (Idle1 == thatCharacterInNextFrame.CharacterState && 0 != thatCharacterInNextFrame.VelX && 0 >= thatCharacterInNextFrame.FramesCapturedByInertia) {
                        thatCharacterInNextFrame.CharacterState = Walking;
                    } else if (Walking == thatCharacterInNextFrame.CharacterState && 0 == thatCharacterInNextFrame.VelX && 0 >= thatCharacterInNextFrame.FramesCapturedByInertia) {
                        thatCharacterInNextFrame.CharacterState = Idle1;
                    }
                }

                if (!currCharacterDownsync.InAir && null != activeSkill && activeSkill.BoundChState == thatCharacterInNextFrame.CharacterState && null != activeBulletConfig && activeBulletConfig.GroundImpactMeleeCollision) {
                    // [WARNING] The "bulletCollider" for "activeBulletConfig" in this case might've been annihilated, we should end this bullet regardless of landing on character or hardPushback.
                    //logger.LogInfo($"_processEffPushbacks/end, currRdfId={currRenderFrame.Id}, clearing obsolete GroundImpactMeleeCollision state for currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, aSid: {currCharacterDownsync.ActiveSkillId}, aSht: {currCharacterDownsync.ActiveSkillHit}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                    int origFramesInActiveState = (thatCharacterInNextFrame.FramesInChState - activeBulletConfig.StartupFrames); // correct even for "DemonDiverImpactPreJumpBullet -> DemonDiverImpactStarterBullet" sequence
                    var shiftedRdfCnt = (activeBulletConfig.ActiveFrames - origFramesInActiveState);
                    if (0 < shiftedRdfCnt) {
                        thatCharacterInNextFrame.FramesInChState += shiftedRdfCnt;
                        thatCharacterInNextFrame.FramesToRecover -= shiftedRdfCnt;
                    }
                    if (0 > origFramesInActiveState) {
                        thatCharacterInNextFrame.ActiveSkillId = NO_SKILL;
                        thatCharacterInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                    }
                    // [WARNING] Leave velocity handling to other code snippets.
                } else if (currCharacterDownsync.OnWall && null != activeSkill && activeSkill.BoundChState == thatCharacterInNextFrame.CharacterState && null != activeBulletConfig && activeBulletConfig.WallImpactMeleeCollision) {
                    //logger.LogInfo($"_processEffPushbacks/end, currRdfId={currRenderFrame.Id}, clearing obsolete WallImpactMeleeCollision state for currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, aSid: {currCharacterDownsync.ActiveSkillId}, aSht: {currCharacterDownsync.ActiveSkillHit}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState})");
                    // [WARNING] The "bulletCollider" for "activeBulletConfig" in this case might've been annihilated, we should end this bullet regardless of landing on character or hardPushback.
                    int origFramesInActiveState = (thatCharacterInNextFrame.FramesInChState - activeBulletConfig.StartupFrames); // correct even for "DemonDiverImpactPreJumpBullet -> DemonDiverImpactStarterBullet" sequence
                    var shiftedRdfCnt = (activeBulletConfig.ActiveFrames - origFramesInActiveState);
                    if (0 < shiftedRdfCnt) {
                        thatCharacterInNextFrame.FramesInChState += shiftedRdfCnt;
                        thatCharacterInNextFrame.FramesToRecover -= shiftedRdfCnt;
                    }
                    if (0 > origFramesInActiveState) {
                        thatCharacterInNextFrame.ActiveSkillId = NO_SKILL;
                        thatCharacterInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                    }
                    // [WARNING] Leave velocity handling to other code snippets.
                } else if (null != activeSkill && activeSkill.BoundChState == thatCharacterInNextFrame.CharacterState && null != activeBulletConfig && (MultiHitType.FromEmission == activeBulletConfig.MhType || MultiHitType.FromEmissionJustActive == activeBulletConfig.MhType) && currCharacterDownsync.FramesInChState > activeBulletConfig.StartupFrames+activeBulletConfig.ActiveFrames+activeBulletConfig.FinishingFrames) {
                    //logger.LogInfo($"_processEffPushbacks/end, currRdfId={currRenderFrame.Id}, clearing obsolete FromEmission state for currChd = (id:{currCharacterDownsync.Id}, spId: {currCharacterDownsync.SpeciesId}, jidx: {currCharacterDownsync.JoinIndex}, aSid: {currCharacterDownsync.ActiveSkillId}, aSht: {currCharacterDownsync.ActiveSkillHit}, fchs:{currCharacterDownsync.FramesInChState}, inAir:{currCharacterDownsync.InAir}, onWall: {currCharacterDownsync.OnWall}, chS: {currCharacterDownsync.CharacterState}), mhType: {activeBulletConfig.MhType}");
                    int origFramesInActiveState = (thatCharacterInNextFrame.FramesInChState - activeBulletConfig.StartupFrames); // correct even for "DemonDiverImpactPreJumpBullet -> DemonDiverImpactStarterBullet" sequence
                    var shiftedRdfCnt = (activeBulletConfig.ActiveFrames - origFramesInActiveState);
                    if (0 < shiftedRdfCnt) {
                        thatCharacterInNextFrame.FramesInChState += shiftedRdfCnt;
                        thatCharacterInNextFrame.FramesToRecover -= shiftedRdfCnt;
                    }
                    if (0 > origFramesInActiveState) {
                        thatCharacterInNextFrame.ActiveSkillId = NO_SKILL;
                        thatCharacterInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                    }
                } 

                if (Def1 == thatCharacterInNextFrame.CharacterState || Def1Atked1 == thatCharacterInNextFrame.CharacterState || Def1Broken == thatCharacterInNextFrame.CharacterState) {
                    if (0 != thatCharacterInNextFrame.VelX) {
                        thatCharacterInNextFrame.VelX = 0;
                    }
                }

                /*
                if (77 == thatCharacterInNextFrame.ActiveSkillId) {
                    logger.LogInfo("_processEffPushbacks/end, currRdfId=" + currRenderFrame.Id + ", used DiverImpact, next VelX = " + thatCharacterInNextFrame.VelX);
                }
                if (SPECIES_STONE_GOLEM == currCharacterDownsync.SpeciesId && Atk2 == currCharacterDownsync.CharacterState && 40 <= currCharacterDownsync.FramesInChState) {
                    logger.LogInfo("_processEffPushbacks/end, currRdfId=" + currRenderFrame.Id + ", VelY = " + currCharacterDownsync.VelY + ", NextVelY = " + thatCharacterInNextFrame.VelY);
                }
                if (InAirIdle2ByJump == currCharacterDownsync.CharacterState && InAirIdle2ByJump != thatCharacterInNextFrame.CharacterState) {
                    logger.LogInfo("_processEffPushbacks/end, currRdfId=" + currRenderFrame.Id + ", transitting from InAirIdle2ByJump to " + thatCharacterInNextFrame.CharacterState + ", currVelX=" + currCharacterDownsync.VelX + ", nextVelX=" + thatCharacterInNextFrame.VelX);
                }
                */
            }

            for (int i = trapColliderCntOffset; i < pickableColliderCntOffset; i++) {
                var aCollider = dynamicRectangleColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;
                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where trapColliderCntOffset=" + trapColliderCntOffset + ", bulletColliderCntOffset=" + bulletColliderCntOffset);
                }
                if (!colliderAttr.ProvidesHardPushback) continue;
                // Update "virtual grid position"
                var trapInNextRenderFrame = nextRenderFrameTraps[colliderAttr.TrapLocalId];
                if (0 != effPushbacks[i].X || 0 != effPushbacks[i].Y) {
                    var (effPushbackVx, effPushbackVy) = PolygonColliderCtrToVirtualGridPos(effPushbacks[i].X, effPushbacks[i].Y); 
                    trapInNextRenderFrame.VirtualGridX -= effPushbackVx;
                    trapInNextRenderFrame.VirtualGridY -= effPushbackVy;
                }
            }

            for (int i = pickableColliderCntOffset; i < bulletColliderCntOffset; i++) {
                var aCollider = dynamicRectangleColliders[i];
                Pickable? pickableNextRenderFrame = aCollider.Data as Pickable;
                if (null == pickableNextRenderFrame) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where pickableColliderCntOffset=" + pickableColliderCntOffset + ", colliderCnt=" + colliderCnt);
                }

                // Update "virtual grid position"
                var (nextColliderVx, nextColliderVy) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                pickableNextRenderFrame.VirtualGridX = nextColliderVx;
                pickableNextRenderFrame.VirtualGridY = nextColliderVy;
            }

            for (int i = bulletColliderCntOffset; i < colliderCnt; i++) {
                var aCollider = dynamicRectangleColliders[i];
                Bullet? bulletNextFrame = aCollider.Data as Bullet;
                if (null == bulletNextFrame) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where bulletColliderCntOffset=" + bulletColliderCntOffset + ", trapColliderCntOffset=" + trapColliderCntOffset);
                }
                
                var (_, bulletConfig) = FindBulletConfig(bulletNextFrame.SkillId, bulletNextFrame.ActiveSkillHit);
                if (null == bulletConfig) {
                    continue;
                }
                // Update "virtual grid position"
                if (0 != effPushbacks[i].X || 0 != effPushbacks[i].Y) {
                    if (BulletType.GroundWave == bulletConfig.BType) {
                        int nextColliderAttrVx, nextColliderAttrVy;
                        (nextColliderAttrVx, nextColliderAttrVy) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                        bulletNextFrame.VirtualGridX = nextColliderAttrVx;
                        bulletNextFrame.VirtualGridY = nextColliderAttrVy;
                    } else if (bulletConfig.BeamCollision) {
                        var (effPushbackVx, effPushbackVy) = PolygonColliderCtrToVirtualGridPos(effPushbacks[i].X, effPushbacks[i].Y);
                        bulletNextFrame.VirtualGridX -= effPushbackVx;
                        bulletNextFrame.VirtualGridY -= effPushbackVy;
                        if (0 < bulletNextFrame.DirX) {
                            if (bulletNextFrame.VirtualGridX < bulletNextFrame.OriginatedVirtualGridX) {
                                bulletNextFrame.VirtualGridX = bulletNextFrame.OriginatedVirtualGridX;
                            }
                        } else if (0 > bulletNextFrame.DirX) {
                            if (bulletNextFrame.VirtualGridX > bulletNextFrame.OriginatedVirtualGridX) {
                                bulletNextFrame.VirtualGridX = bulletNextFrame.OriginatedVirtualGridX;
                            }
                        }
                    }
                }
            }
        }

        /*
        [TODO] 

        The "Step" function has become way more complicated than what it was back in the days only simple movements and hardpushbacks were supported. 
        
        Someday in the future, profiling result on low-end hardware might complain that this function is taking too much time in the "Script" portion, thus need one or all of the following optimization techniques to help it go further.
        - Make use of CPU parallelization -- better by using some libraries with sub-kernel-thread granularity(e.g. Goroutine or Greenlet equivalent) -- or GPU parallelization. It's not trivial to make an improvement because by dispatching smaller tasks to other resources other than the current kernel-thread, overhead I/O and synchronization/locking time is introduced. Moreover, we need guarantee that the dispatched smaller tasks can yield deterministic outputs regardless of processing order, e.g. that each "i" in "_calcAllCharactersCollisions" can be traversed earlier than another and same "effPushbacks" for the next render frame is obtained.   
        - Enable "IL2CPP" when building client application.  
        */
        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPushbackEnabled, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded tempInputHolder, FrameRingBuffer<Collider> residueCollided, Dictionary<int, int> triggerEditorIdToLocalId, Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToTiledConfig, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, List<Collider> completelyStaticTrapColliders, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, bool pushbackFrameLogEnabled, int playingRdfId, bool shouldDetectRealtimeRenderHistoryCorrection, out bool hasIncorrectlyPredictedRenderFrame, RoomDownsyncFrame historyRdfHolder, int missionTriggerLocalId, int selfPlayerJoinIndex, Dictionary<int, int> joinIndexRemap, ref int justTriggeredStoryPointId, ref int justTriggeredBgmId, HashSet<int> justDeadJoinIndices, out ulong fulfilledTriggerSetMask, ref bool selfNotEnoughMp, ILoggerBridge logger, bool inArenaPracticeMode=false) {
            var (ok1, currRenderFrame) = renderBuffer.GetByFrameId(currRenderFrameId);
            if (!ok1 || null == currRenderFrame) {
                throw new ArgumentNullException(String.Format("Null currRenderFrame is not allowed in `Battle.Step` for currRenderFrameId={0}", currRenderFrameId));
            }

            int nextRenderFrameId = currRenderFrameId + 1;
            var (ok2, candidate) = renderBuffer.GetByFrameId(nextRenderFrameId);
            if (!ok2 || null == candidate) {
                if (nextRenderFrameId == renderBuffer.EdFrameId) {
                    renderBuffer.DryPut();
                    (_, candidate) = renderBuffer.GetByFrameId(nextRenderFrameId);
                }
            }
            if (null == candidate) {
                throw new ArgumentNullException(String.Format("renderBuffer was not fully pre-allocated for nextRenderFrameId={0}!", nextRenderFrameId));
            }

            bool ok3 = false;
            RdfPushbackFrameLog? currRdfPushbackFrameLog = null;
            if (pushbackFrameLogEnabled) {
                (ok3, currRdfPushbackFrameLog) = pushbackFrameLogBuffer.GetByFrameId(currRenderFrameId);
                if (!ok3 || null == currRdfPushbackFrameLog) {
                    while (currRenderFrameId >= pushbackFrameLogBuffer.EdFrameId) {
                        pushbackFrameLogBuffer.DryPut();
                    }
                    (_, currRdfPushbackFrameLog) = pushbackFrameLogBuffer.GetByFrameId(currRenderFrameId);
                }
                if (null == currRdfPushbackFrameLog) {
                    // Get the pointer to currRdfPushbackFrameLog anyway, but don't throw error if it's null but not required!
                    throw new ArgumentNullException(String.Format("pushbackFrameLogBuffer was not fully pre-allocated for currRenderFrameId={0}! pushbackFrameLogBuffer:{1}", currRenderFrameId, pushbackFrameLogBuffer.toSimpleStat()));
                }
                currRdfPushbackFrameLog.RdfId = currRenderFrameId;
            }

            hasIncorrectlyPredictedRenderFrame = false;
            if (shouldDetectRealtimeRenderHistoryCorrection && nextRenderFrameId <= playingRdfId && candidate.Id == nextRenderFrameId) {
                AssignToRdfDeep(candidate, historyRdfHolder, roomCapacity, logger);
            }
            // [WARNING] On backend this function MUST BE called while "InputsBufferLock" is locked!
            var nextRenderFramePlayers = candidate.PlayersArr;
            var nextRenderFrameNpcs = candidate.NpcsArr;
            int nextRenderFrameNpcLocalIdCounter = currRenderFrame.NpcLocalIdCounter;
            var nextRenderFrameBullets = candidate.Bullets;
            int nextRenderFrameBulletLocalIdCounter = currRenderFrame.BulletLocalIdCounter;
            var nextRenderFrameTraps = candidate.TrapsArr;
            var nextRenderFrameTriggers = candidate.TriggersArr;
            var nextRenderFramePickables = candidate.Pickables; 
            int nextRenderFramePickableLocalIdCounter = currRenderFrame.PickableLocalIdCounter;
            // Make a copy first
            // [WARNING] For "nextRenderFrameBullets" and "nextRenderFramePickables", their "copy from currRenderFrame" operations are embedded into "_moveAndInsertBulletColliders(...)" and "_moveAndInsertPickableColliders" respectively.
            for (int i = 0; i < roomCapacity; i++) {
                var src = currRenderFrame.PlayersArr[i];
                var chConfig = characters[src.SpeciesId];
                var framesToRecover = (0 < src.FramesToRecover ? src.FramesToRecover-1 : 0);
                var framesCapturedByInertia = (0 < src.FramesCapturedByInertia ? src.FramesCapturedByInertia - 1 : 0); 
                var framesInChState = src.FramesInChState + 1;
                var framesInvinsible = (0 < src.FramesInvinsible ? src.FramesInvinsible - 1 : 0);
                var framesInPatrolCue = (0 < src.FramesInPatrolCue ? src.FramesInPatrolCue - 1 : 0);
                var mpRegenRdfCountdown = (0 < src.MpRegenRdfCountdown ? src.MpRegenRdfCountdown-1 : 0);
                var mp = src.Mp;
                if (0 >= mpRegenRdfCountdown) {
                    mp += chConfig.MpRegenPerInterval;
                    if (mp >= chConfig.Mp) {
                        mp = chConfig.Mp;
                    }
                    mpRegenRdfCountdown = chConfig.MpRegenInterval;
                }
                var framesToStartJump = (0 < src.FramesToStartJump ? src.FramesToStartJump - 1 : 0);
                var framesSinceLastDamaged = (0 < src.FramesSinceLastDamaged ? src.FramesSinceLastDamaged - 1 : 0);
                uint damageEleAttrs = src.DamageElementalAttrs;
                if (0 >= framesSinceLastDamaged) {
                    damageEleAttrs = 0;
                }
                uint comboHitCnt = src.ComboHitCnt;
                var comboFramesRemained = (0 < src.ComboFramesRemained ? src.ComboFramesRemained - 1 : 0);
                if (0 >= comboFramesRemained) {
                    comboFramesRemained = 0;
                    comboHitCnt = 0;
                } 
                var flyingRdfCountdown = (MAX_INT == chConfig.FlyingQuotaRdfCnt ? MAX_INT : (0 < src.FlyingRdfCountdown ? src.FlyingRdfCountdown-1 : 0));
                var dst = nextRenderFramePlayers[i];
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FrictionVelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, true, false, src.OnWallNormX, src.OnWallNormY, framesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.GoalAsNpc, src.WaivingPatrolCueId, false, false, false, false, false, framesToStartJump, framesSinceLastDamaged, src.RemainingDef1Quota, src.BuffList, src.DebuffList, src.Inventory, true, src.PublishingToTriggerLocalIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscribesToTriggerLocalId, src.JumpHoldingRdfCnt, src.BtnBHoldingRdfCount, src.BtnCHoldingRdfCount, src.BtnDHoldingRdfCount, src.BtnEHoldingRdfCount, src.ParryPrepRdfCntDown, src.RemainingAirJumpQuota, src.RemainingAirDashQuota, src.KilledToDropConsumableSpeciesId, src.KilledToDropBuffSpeciesId, src.KilledToDropPickupSkillId, src.BulletImmuneRecords, comboHitCnt, comboFramesRemained, damageEleAttrs, src.LastDamagedByJoinIndex, src.LastDamagedByBulletTeamId, src.ActivatedRdfId, src.CachedCueCmd, mpRegenRdfCountdown, flyingRdfCountdown, src.LockingOnJoinIndex, dst);
                _resetVelocityOnRecovered(src, dst);
            }

            int currNpcI = 0;
            while (currNpcI < currRenderFrame.NpcsArr.Count && TERMINATING_PLAYER_ID != currRenderFrame.NpcsArr[currNpcI].Id) {
                var src = currRenderFrame.NpcsArr[currNpcI];
                var chConfig = characters[src.SpeciesId];
                var framesToRecover = (0 < src.FramesToRecover ? src.FramesToRecover - 1 : 0);
                var framesCapturedByInertia = (0 < src.FramesCapturedByInertia ? src.FramesCapturedByInertia - 1 : 0);
                var framesInChState = src.FramesInChState + 1;
                var framesInvinsible = (0 < src.FramesInvinsible ? src.FramesInvinsible - 1 : 0);
                var framesInPatrolCue = (0 < src.FramesInPatrolCue ? src.FramesInPatrolCue - 1 : 0);
                var mpRegenRdfCountdown = (0 < src.MpRegenRdfCountdown ? src.MpRegenRdfCountdown-1 : 0);
                var mp = src.Mp;
                if (0 >= mpRegenRdfCountdown) {
                    mp += chConfig.MpRegenPerInterval;
                    if (mp >= chConfig.Mp) {
                        mp = chConfig.Mp;
                    }
                    mpRegenRdfCountdown = chConfig.MpRegenInterval;
                }
                var framesToStartJump = (0 < src.FramesToStartJump ? src.FramesToStartJump - 1 : 0);
                var framesSinceLastDamaged = (0 < src.FramesSinceLastDamaged ? src.FramesSinceLastDamaged - 1 : 0);
                uint damageEleAttrs = src.DamageElementalAttrs;
                if (0 >= framesSinceLastDamaged) {
                    damageEleAttrs = 0;
                }
                uint comboHitCnt = src.ComboHitCnt;
                var comboFramesRemained = (0 < src.ComboFramesRemained ? src.ComboFramesRemained - 1 : 0);
                if (0 >= comboFramesRemained) {
                    comboFramesRemained = 0;
                    comboHitCnt = 0;
                }
                var flyingRdfCountdown = (MAX_INT == chConfig.FlyingQuotaRdfCnt ? MAX_INT : (0 < src.FlyingRdfCountdown ? src.FlyingRdfCountdown-1 : 0));
                var dst = nextRenderFrameNpcs[currNpcI];
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FrictionVelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, true, false, src.OnWallNormX, src.OnWallNormY, framesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.GoalAsNpc, src.WaivingPatrolCueId, false, false, false, false, false, framesToStartJump, framesSinceLastDamaged, src.RemainingDef1Quota, src.BuffList, src.DebuffList, src.Inventory, true, src.PublishingToTriggerLocalIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscribesToTriggerLocalId, src.JumpHoldingRdfCnt, src.BtnBHoldingRdfCount, src.BtnCHoldingRdfCount, src.BtnDHoldingRdfCount, src.BtnEHoldingRdfCount, src.ParryPrepRdfCntDown, src.RemainingAirJumpQuota, src.RemainingAirDashQuota, src.KilledToDropConsumableSpeciesId, src.KilledToDropBuffSpeciesId, src.KilledToDropPickupSkillId, src.BulletImmuneRecords, comboHitCnt, comboFramesRemained, damageEleAttrs, src.LastDamagedByJoinIndex, src.LastDamagedByBulletTeamId, src.ActivatedRdfId, src.CachedCueCmd, mpRegenRdfCountdown, flyingRdfCountdown, src.LockingOnJoinIndex, dst);
                _resetVelocityOnRecovered(src, dst);
                currNpcI++;
            }
            nextRenderFrameNpcs[currNpcI].Id = TERMINATING_PLAYER_ID; // [WARNING] This is a CRITICAL assignment because "renderBuffer" is a ring, hence when cycling across "renderBuffer.StFrameId", we must ensure that the trailing NPCs existed from the startRdf wouldn't contaminate later calculation
            int nextNpcI = currNpcI;

            fulfilledTriggerSetMask = 0; // By default no EvtSub is fulfilled yet

            int k = 0;
            while (k < currRenderFrame.TrapsArr.Count && TERMINATING_TRAP_ID != currRenderFrame.TrapsArr[k].TrapLocalId) {
                var src = currRenderFrame.TrapsArr[k];
                var framesInTrapState = src.FramesInTrapState + 1;
                var framesInPatrolCue = (0 < src.FramesInPatrolCue ? src.FramesInPatrolCue - 1 : 0);
                AssignToTrap(src.TrapLocalId, src.ConfigFromTiled, src.TrapState, framesInTrapState, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.VelY, src.SpinCos, src.SpinSin, src.AngularFrameVelCos, src.AngularFrameVelSin, src.PatrolCueAngularVelFlipMark, src.IsCompletelyStatic, src.CapturedByPatrolCue, framesInPatrolCue, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, src.SubscribesToTriggerLocalId, src.SubscribesToTriggerLocalIdAlt, nextRenderFrameTraps[k]);
                k++;
            }
            nextRenderFrameTraps[k].TrapLocalId = TERMINATING_TRAP_ID;

            int l = 0;
            while (l < currRenderFrame.TriggersArr.Count && TERMINATING_TRIGGER_ID != currRenderFrame.TriggersArr[l].TriggerLocalId) {
                var src = currRenderFrame.TriggersArr[l];
                int framesToFire = src.FramesToFire - 1; 
                if (framesToFire < 0) {
                    framesToFire = 0;
                }
                int framesToRecover = src.FramesToRecover - 1; 
                if (framesToRecover < 0) {
                    framesToRecover = 0;
                }
                int framesInState = src.FramesInState + 1;
                ulong fulfilledEvtMask = src.FulfilledEvtMask;
                AssignToTrigger(src.EditorId, src.TriggerLocalId, framesToFire, framesToRecover, src.Quota, src.BulletTeamId, src.OffenderJoinIndex, src.OffenderBulletTeamId, src.SubCycleQuotaLeft, src.State, framesInState, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DemandedEvtMask, fulfilledEvtMask, src.WaveNpcKilledEvtMaskCounter, src.SubscriberLocalIdsMask, src.ExhaustSubscriberLocalIdsMask, nextRenderFrameTriggers[l]);
                l++;
            }
            nextRenderFrameTriggers[l].TriggerLocalId = TERMINATING_TRIGGER_ID;

            /*
               [WARNING]
               1. The dynamic colliders will all be removed from "Space" at the end of this function due to the need for being rollback-compatible.

               2. To achieve "zero gc" in "ApplyInputFrameDownsyncDynamicsOnSingleRenderFrame", I deliberately chose a collision system that doesn't use dynamic tree node alloc.

               3. Before generating inputs for Npcs, the colliders for "Players" should be inserted such that "Npc Visions" can interact with the players in collision system. 

               4. For a true "player", each "Step" moves it by: 
               [a] taking "proposed movement" in the "virtual grid" (w/ velocity from previous "Step" or "_processPlayerInputs");    
               [b] adding a collider of it w.r.t. the "virtual grid position after proposed movement";
               [c] calculating pushbacks for the collider;
               [d] confirming "new virtual grid position" by "collider position & pushbacks".

               Kindly note that we never "move the collider in the collisionSys", because that's a costly operation in terms of time-complexity.

               5. For an "Npc", it's a little tricky to move it because the inputs of an "Npc" are not performed by a human (or another machine with heuristic logic, e.g. a trained neural network w/ possibly "RoomDownsyncFrame" as input). Moreover an "Npc" should behave deterministically -- especially when encountering a "PatrolCue" or a "Player Character in vision", thus we should insert some "Npc input generation" between "4.[b]" and "4.[c]" such that it can collide with a "PatrolCue" or a "Player Character".      
             */
            int colliderCnt = 0, bulletCnt = 0, pickableCnt = 0;
            int delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrameId);
            if (0 < delayedInputFrameId) {
                var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
                if (!ok || null == delayedInputFrameDownsync) {
                    throw new ArgumentNullException($"Null delayedInputFrameDownsync for delayedInputFrameId={delayedInputFrameId} in `Step`!");
                }
                _processPlayerInputs(currRenderFrame, delayedInputFrameDownsync, roomCapacity, inputBuffer, nextRenderFramePlayers, nextRenderFrameBullets, decodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, selfPlayerJoinIndex, ref selfNotEnoughMp, logger);
            }

            var (rdfAllConfirmed, _) = isRdfAllConfirmed(currRenderFrame.Id, inputBuffer, roomCapacity);
            _moveAndInsertCharacterColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, 0, roomCapacity + currNpcI, logger);
            
            int trapColliderCntOffset = colliderCnt;
            _moveAndInsertDynamicTrapColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFrameTraps, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, trapColliderCntOffset, trapLocalIdToColliderAttrs, logger);

            // ---------[WARNING] "_calcDynamicTrapMovementCollisions" and "_calcCompletelyStaticTrapDamage" only handle "trap-barrier", "trap-trap", "trap-ch" and "trap-patrolCue" collisions, thus can be executed as early as possible to reduce duplicate collision detections. ---------
            _calcDynamicTrapMovementCollisions(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, decodedInputHolder, dynamicRectangleColliders, trapColliderCntOffset, colliderCnt, residueCollided, logger);
            
            _calcCompletelyStaticTrapDamage(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, collision, completelyStaticTrapColliders, logger);
            
            int pickableColliderCntOffset = colliderCnt;
            _moveAndInsertPickableColliders(currRenderFrame, roomCapacity, nextRenderFramePickables, collisionSys, dynamicRectangleColliders, effPushbacks, ref colliderCnt, ref pickableCnt, logger);

            _calcPickableMovementPushbacks(currRenderFrame, roomCapacity, nextRenderFramePickables, ref overlapResult, ref primaryOverlapResult, collision, dynamicRectangleColliders, effPushbacks, hardPushbackNormsArr, pickableColliderCntOffset, colliderCnt, logger);

            // ---------[WARNING] "bullet-character" collisions are the most computationally expensive, need find a way to put bullet collider insertion after "_calcAllCharactersCollisions" while enabling "ch-bullet collision with X/Y hard pushback provider" --------- 
            int bulletColliderCntOffset = colliderCnt;
            _insertFromEmissionDerivedBullets(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _moveAndInsertBulletColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, currRenderFrame.Bullets, nextRenderFrameBullets, dynamicRectangleColliders, ref colliderCnt, collisionSys, ref bulletCnt, effPushbacks, ref overlapResult, collision, logger);

            // ---------[WARNING] Deliberately put "_processNpcInputs" after "_moveAndInsertBulletColliders" such that NPC vision can see bullets; also deliberately put "_processNpcInputs" before "_calcAllCharactersCollisions" to avoid overwriting "onSlope velocities" ---------
            if (inArenaPracticeMode) {
                int noDelayInputFrameId = ConvertToDynamicallyGeneratedDelayInputFrameId(currRenderFrameId, 0);
                var (ok, noDelayIfd) = inputBuffer.GetByFrameId(noDelayInputFrameId);
                if (ok && null != noDelayIfd) {
                    for (int i = 0; i < roomCapacity; i++) {
                        if (i + 1 == selfPlayerJoinIndex) continue;
                        ulong joinIndexMask = (1u << i);
                        if (0 < (noDelayIfd.ConfirmedList & joinIndexMask)) continue;
                        var currCharacterDownsync = currRenderFrame.PlayersArr[i];
                        var thatCharacterInNextFrame = nextRenderFramePlayers[i];
                        var chConfig = characters[currCharacterDownsync.SpeciesId];

                        bool currNotDashing = isNotDashing(currCharacterDownsync);
                        bool currEffInAir = isEffInAir(currCharacterDownsync, currNotDashing);
                        bool nextNotDashing = isNotDashing(thatCharacterInNextFrame);
                        bool nextEffInAir = isEffInAir(thatCharacterInNextFrame, currNotDashing);
                        var (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy, slowDownToAvoidOverlap) = deriveNpcOpPattern(currCharacterDownsync, thatCharacterInNextFrame, currEffInAir, currNotDashing, nextNotDashing, nextEffInAir, currRenderFrame, roomCapacity, chConfig, dynamicRectangleColliders, colliderCnt: colliderCnt, collisionSys, collision, ref overlapResult, ref primaryOverlapResult, decodedInputHolder, tempInputHolder, logger);
                        noDelayIfd.InputList[i] = EncodeInput(decodedInputHolder);
                        noDelayIfd.ConfirmedList |= joinIndexMask;
                    }
                }
            }

            _processNpcInputs(currRenderFrame, roomCapacity, currNpcI, nextRenderFrameNpcs, nextRenderFrameBullets, dynamicRectangleColliders, colliderCnt, collision, collisionSys, ref overlapResult, mvBlockerOverlapResult: ref primaryOverlapResult, decodedInputHolder, tempInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);

            _calcAllCharactersCollisions(currRenderFrame, roomCapacity, currNpcI, inputBuffer, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameBullets, nextRenderFrameTriggers, nextRenderFrameTraps, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, 0, roomCapacity + currNpcI, residueCollided, unconfirmedBattleResults, ref confirmedBattleResult, trapLocalIdToColliderAttrs, triggerEditorIdToTiledConfig, currRdfPushbackFrameLog, pushbackFrameLogEnabled, logger);

            _calcAllBulletsCollisions(currRenderFrame, rdfAllConfirmed, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, nextRenderFrameBullets, nextRenderFrameTriggers, ref overlapResult, collisionSys, collision, dynamicRectangleColliders, effPushbacks, hardPushbackNormsArr, residueCollided, ref primaryOverlapResult, bulletColliderCntOffset, colliderCnt, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, ref fulfilledTriggerSetMask, colliderCnt, triggerEditorIdToTiledConfig, logger);
         
            // ---------[WARNING] Deliberately put "_calcTriggerReactions" after "_calcAllBulletsCollisions", "_calcDynamicTrapMovementCollisions" and "_calcCompletelyStaticTrapDamage", such that it could capture the just-fulfilled ones. --------- 
            _calcTriggerReactions(currRenderFrame, candidate, roomCapacity, nextRenderFrameTriggers, nextRenderFrameNpcs, triggerEditorIdToLocalId, triggerEditorIdToTiledConfig, decodedInputHolder, ref nextRenderFrameNpcLocalIdCounter, ref nextNpcI, ref nextRenderFramePickableLocalIdCounter, ref pickableCnt, nextRenderFramePickables, ref fulfilledTriggerSetMask, ref justTriggeredStoryPointId, ref justTriggeredBgmId, logger);

            // ---------[WARNING] Deliberately put "_calcTrapReaction" after "_calcTriggerReactions" such that latest "fulfilledTriggerSetMask" is respected. --------- 
            _calcTrapReaction(currRenderFrame, roomCapacity, nextRenderFrameTraps, triggerEditorIdToLocalId, fulfilledTriggerSetMask, nextRenderFrameBullets, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);

            // Trigger subscription-based NPC movements
            for (int i = 0; i < currNpcI; i++) {
                var src = currRenderFrame.NpcsArr[i];
                if (TERMINATING_PLAYER_ID == src.Id) break;
                if (TERMINATING_TRIGGER_ID == src.SubscribesToTriggerLocalId) continue; // No subscription or already triggered
                if (MAGIC_EVTSUB_ID_DUMMY == src.SubscribesToTriggerLocalId) continue; // For non-interactable NPCs
                var subscribedToTriggerMask = (1UL << (src.SubscribesToTriggerLocalId - 1));
                if (0 == (fulfilledTriggerSetMask & subscribedToTriggerMask)) continue; // Subscription not fulfilled
                var trigger = currRenderFrame.TriggersArr[src.SubscribesToTriggerLocalId-1];
                var triggerConfigFromTiled = triggerEditorIdToTiledConfig[trigger.EditorId];
                var dst = nextRenderFrameNpcs[i];
                var chConfig = characters[dst.SpeciesId];
                if (0 != triggerConfigFromTiled.InitDirX) { 
                    dst.DirX = triggerConfigFromTiled.InitDirX;
                } 
                if (0 != triggerConfigFromTiled.InitDirY) {
                    dst.DirY = triggerConfigFromTiled.InitDirY;
                }
                dst.SubscribesToTriggerLocalId = TERMINATING_TRIGGER_ID;
                //logger.LogInfo($"Awaking NPC@currRdfId={currRenderFrame.Id}, srcChd = (id:{src.Id}, spId: {src.SpeciesId}, jidx: {src.JoinIndex}, VelX: {src.VelX}, VelY: {src.VelY}, DirX: {src.DirX}, DirY: {src.DirY}, fchs:{src.FramesInChState}, inAir:{src.InAir}, onWall: {src.OnWall}, chS: {src.CharacterState})\n\tdstChd = (id:{dst.Id}, spId: {dst.SpeciesId}, jidx: {dst.JoinIndex}, VelX: {dst.VelX}, VelY: {dst.VelY}, DirX: {dst.DirX}, DirY: {dst.DirY})");
                
                if (chConfig.HasDimmedAnim) {
                    if (chConfig.HasAwakingAnim) {
                        dst.CharacterState = Awaking;
                        dst.FramesToRecover = chConfig.AwakingFramesToRecover;
                        dst.FramesInvinsible = chConfig.AwakingFramesInvinsible;
                    } else if (chConfig.LayDownToRecoverFromDimmed) {
                        dst.CharacterState = LayDown1;
                        dst.FramesToRecover = chConfig.LayDownFramesToRecover;
                    } else {
                        dst.CharacterState = Idle1;
                        dst.FramesToRecover = 0;
                    }
                } else {
                    if (!chConfig.AntiGravityWhenIdle) {
                        dst.CharacterState = GetUp1;
                        dst.FramesToRecover = chConfig.GetUpFramesToRecover;
                    } else {
                        dst.CharacterState = InAirIdle1NoJump;
                        dst.FramesToRecover = 0;
                    }
                }
                dst.ActivatedRdfId = currRenderFrame.Id;
                //logger.LogInfo(String.Format("@rdfId={0}, npc id={1} awaken by trigger editor id = {2}, local id = {3}", currRenderFrame.Id, dst.Id, trigger.EditorId ,trigger.TriggerLocalId));
            }

            _processEffPushbacks(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, nextRenderFramePickables, effPushbacks, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, pickableColliderCntOffset, colliderCnt, trapLocalIdToColliderAttrs, logger);

            _calcFallenDeath(currRenderFrame, rdfAllConfirmed, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFramePickables, logger);

            bool isRemapNeeded = false;

            _leftShiftDeadNpcs(currRenderFrame.Id, roomCapacity, nextRenderFrameNpcs, ref nextRenderFramePickableLocalIdCounter, nextRenderFramePickables, nextRenderFrameTriggers, joinIndexRemap, out isRemapNeeded, justDeadJoinIndices, ref nextNpcI, ref pickableCnt, false, logger);

            if (isRemapNeeded) {
                remapBulletOffenderJoinIndex(roomCapacity, nextNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameBullets, joinIndexRemap, justDeadJoinIndices);
            }

            if (TERMINATING_TRIGGER_ID != missionTriggerLocalId) {
                ulong missionTriggerMask = (1UL << (missionTriggerLocalId-1));
                if (0 < (fulfilledTriggerSetMask & missionTriggerMask)) {
                    var targetTrigger = nextRenderFrameTriggers[missionTriggerLocalId - 1];
                    if (1 == roomCapacity) {
                        confirmedBattleResult.WinnerJoinIndex = targetTrigger.OffenderJoinIndex;
                        confirmedBattleResult.WinnerBulletTeamId = targetTrigger.OffenderBulletTeamId;
                    } else {
                        if (rdfAllConfirmed) {
                            confirmedBattleResult.WinnerJoinIndex = targetTrigger.OffenderJoinIndex;
                            confirmedBattleResult.WinnerBulletTeamId = targetTrigger.OffenderBulletTeamId;
                        } else {
                            // [WARNING] This cached information could be created by a CORRECTLY PREDICTED "delayedInputFrameDownsync", thus we need a rollback from there on to finally consolidate the result later!
                            unconfirmedBattleResults[delayedInputFrameId] = confirmedBattleResult; // The "value" here is actually not useful, it's just stuffed here for type-correctness :)
                        }
                    }
                }
            }

            for (int i = colliderCnt-1; i >=0; i--) {
                Collider dynamicCollider = dynamicRectangleColliders[i];
                if (null == dynamicCollider.Space) {
                    throw new ArgumentNullException("Null dynamicCollider.Space is not allowed in `Step`!");
                }
                dynamicCollider.Space.RemoveSingleFromCellTail(dynamicCollider);
            }

            candidate.Id = nextRenderFrameId;
            candidate.BulletLocalIdCounter = nextRenderFrameBulletLocalIdCounter;
            candidate.NpcLocalIdCounter = nextRenderFrameNpcLocalIdCounter;
            candidate.PickableLocalIdCounter = nextRenderFramePickableLocalIdCounter;

            if (shouldDetectRealtimeRenderHistoryCorrection && nextRenderFrameId <= playingRdfId && candidate.Id == nextRenderFrameId) {
                if (!EqualRdfs(historyRdfHolder, candidate, roomCapacity)) {
                    hasIncorrectlyPredictedRenderFrame = true; 
                }
            }
        }

        public static void calcCharacterBoundingBoxInCollisionSpace(CharacterDownsync characterDownsync, CharacterConfig chConfig, int newVx, int newVy, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {

            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(newVx, newVy);

            switch (characterDownsync.CharacterState) {
                case LayDown1:
                case GetUp1:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.LayDownSizeX, chConfig.LayDownSizeY);
                    break;
                case Dying:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.DyingSizeX, chConfig.DyingSizeY);
                    break;
                case BlownUp1:
                case InAirIdle1NoJump:
                case InAirIdle1ByJump:
                case InAirIdle2ByJump:
                case InAirIdle1ByWallJump:
                case InAirWalking:
                case InAirAtk1:
                case InAirAtked1:
                case OnWallIdle1:
                case OnWallAtk1:
                case Sliding:
                case GroundDodged:
                case CrouchIdle1:
                case CrouchAtk1:
                case CrouchAtked1:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.ShrinkedSizeX, chConfig.ShrinkedSizeY);
                    break;
                case Dashing:
                    if (characterDownsync.InAir) {
                        (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.ShrinkedSizeX, chConfig.ShrinkedSizeY);
                    } else {
                        (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.DefaultSizeX, chConfig.DefaultSizeY);
                    }
                    break;
                case Dimmed:
                    if (0 != chConfig.DimmedSizeX && 0 != chConfig.DimmedSizeY) {
                        (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.DimmedSizeX, chConfig.DimmedSizeY);
                    } else {
                        (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.DefaultSizeX, chConfig.DefaultSizeY);
                    }
                    break;
                default:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.DefaultSizeX, chConfig.DefaultSizeY);
                    break;
            }
        }
        
        public static bool isInJumpStartup(CharacterDownsync cd, CharacterConfig chConfig) { 
            if (cd.OmitGravity && !chConfig.OmitGravity) {
                return (InAirIdle1ByJump == cd.CharacterState || InAirIdle1NoJump == cd.CharacterState || InAirWalking == cd.CharacterState) && (0 < cd.FramesToStartJump);
            }

            return proactiveJumpingSet.Contains(cd.CharacterState) && (0 < cd.FramesToStartJump);
        }

        public static bool isJumpStartupJustEnded(CharacterDownsync currCd, CharacterDownsync nextCd, CharacterConfig chConfig) {
            if (currCd.OmitGravity && !chConfig.OmitGravity) {
                return ((InAirIdle1NoJump == currCd.CharacterState && InAirIdle1NoJump == nextCd.CharacterState) || (InAirWalking == currCd.CharacterState && InAirWalking == nextCd.CharacterState) || (InAirIdle1ByJump == currCd.CharacterState && InAirIdle1ByJump == nextCd.CharacterState)) && (1 == currCd.FramesToStartJump) && (0 == nextCd.FramesToStartJump);
            }

            return ((InAirIdle1ByJump == currCd.CharacterState && InAirIdle1ByJump == nextCd.CharacterState) || (InAirIdle1ByWallJump == currCd.CharacterState && InAirIdle1ByWallJump == nextCd.CharacterState) || (InAirIdle2ByJump == currCd.CharacterState && InAirIdle2ByJump == nextCd.CharacterState)) && (1 == currCd.FramesToStartJump) && (0 == nextCd.FramesToStartJump);
        } 

        public static void resetJumpStartup(CharacterDownsync cd, bool putBtnHoldingJammed = false) {
            cd.JumpStarted = false;
            cd.JumpTriggered = false;
            cd.SlipJumpTriggered = false;
            cd.FramesToStartJump = 0;
            if (putBtnHoldingJammed) {
                jamBtnHolding(cd);
            }
        }

        public static void jamBtnHolding(CharacterDownsync thatCharacterInNextFrame) {
            if (0 < thatCharacterInNextFrame.JumpHoldingRdfCnt) {
                thatCharacterInNextFrame.JumpHoldingRdfCnt = JAMMED_BTN_HOLDING_RDF_CNT;
            }
            if (0 < thatCharacterInNextFrame.BtnBHoldingRdfCount) {
                thatCharacterInNextFrame.BtnBHoldingRdfCount = JAMMED_BTN_HOLDING_RDF_CNT;
            }
            if (0 < thatCharacterInNextFrame.BtnCHoldingRdfCount) {
                thatCharacterInNextFrame.BtnCHoldingRdfCount = JAMMED_BTN_HOLDING_RDF_CNT;
            } 
            if (0 < thatCharacterInNextFrame.BtnDHoldingRdfCount) { 
                thatCharacterInNextFrame.BtnDHoldingRdfCount = JAMMED_BTN_HOLDING_RDF_CNT;
            }
            if (0 < thatCharacterInNextFrame.BtnEHoldingRdfCount) { 
                thatCharacterInNextFrame.BtnEHoldingRdfCount = JAMMED_BTN_HOLDING_RDF_CNT;
            }
        }

        public static void removePredictedRisingAndFallingEdgesOfPlayerInput(CharacterDownsync chDownsync, InputFrameDecoded decodedInputHolder) {
            /*
            [WARNING] Any "predicted rising/falling edge" is harmful -- even if in "frontend rollbackAndChase(..., isChasing: true)", i.e. a "false rising/falling edge" might trigger a "false start of a skill" in "historic renderBuffer" which in turn would be picked up by "front rollbackAndChase(..., isChasing: false)" to render "false bullets" as well as deal "false damage". 
            */

            bool shouldPredictBtnAHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.JumpHoldingRdfCnt) || (0 < chDownsync.JumpHoldingRdfCnt);
            bool shouldPredictBtnBHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnBHoldingRdfCount) || (0 < chDownsync.BtnBHoldingRdfCount);
            bool shouldPredictBtnCHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnCHoldingRdfCount) || (0 < chDownsync.BtnCHoldingRdfCount);
            bool shouldPredictBtnDHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnDHoldingRdfCount) || (0 < chDownsync.BtnDHoldingRdfCount);
            bool shouldPredictBtnEHold = (JAMMED_BTN_HOLDING_RDF_CNT == chDownsync.BtnEHoldingRdfCount) || (0 < chDownsync.BtnEHoldingRdfCount);
            decodedInputHolder.BtnALevel = shouldPredictBtnAHold ? 1 : 0;  
            decodedInputHolder.BtnBLevel = shouldPredictBtnBHold ? 1 : 0;
            decodedInputHolder.BtnCLevel = shouldPredictBtnCHold ? 1 : 0;
            decodedInputHolder.BtnDLevel = shouldPredictBtnDHold ? 1 : 0;
            decodedInputHolder.BtnELevel = shouldPredictBtnEHold ? 1 : 0;
        }

        public static void updateBtnHoldingByInput(CharacterDownsync currCharacterDownsync, InputFrameDecoded decodedInputHolder, CharacterDownsync thatCharacterInNextFrame) {
            if (0 == decodedInputHolder.BtnALevel) {
                thatCharacterInNextFrame.JumpHoldingRdfCnt = 0;
            } else if (JAMMED_BTN_HOLDING_RDF_CNT != currCharacterDownsync.JumpHoldingRdfCnt && 0 < decodedInputHolder.BtnALevel) {
                thatCharacterInNextFrame.JumpHoldingRdfCnt = currCharacterDownsync.JumpHoldingRdfCnt+1;
                if (thatCharacterInNextFrame.JumpHoldingRdfCnt > MAX_INT) {
                    thatCharacterInNextFrame.JumpHoldingRdfCnt = MAX_INT;
                }
            }

            if (0 == decodedInputHolder.BtnBLevel) {
                thatCharacterInNextFrame.BtnBHoldingRdfCount = 0;
            } else if (JAMMED_BTN_HOLDING_RDF_CNT != currCharacterDownsync.BtnBHoldingRdfCount && 0 < decodedInputHolder.BtnBLevel) {
                thatCharacterInNextFrame.BtnBHoldingRdfCount = currCharacterDownsync.BtnBHoldingRdfCount+1;
                if (thatCharacterInNextFrame.BtnBHoldingRdfCount > MAX_INT) {
                    thatCharacterInNextFrame.BtnBHoldingRdfCount = MAX_INT;
                }
            }

            if (0 == decodedInputHolder.BtnCLevel) {
                thatCharacterInNextFrame.BtnCHoldingRdfCount = 0;
            } else if (JAMMED_BTN_HOLDING_RDF_CNT != currCharacterDownsync.BtnCHoldingRdfCount && 0 < decodedInputHolder.BtnCLevel) {
                thatCharacterInNextFrame.BtnCHoldingRdfCount = currCharacterDownsync.BtnCHoldingRdfCount+1;
                if (thatCharacterInNextFrame.BtnCHoldingRdfCount > MAX_INT) {
                    thatCharacterInNextFrame.BtnCHoldingRdfCount = MAX_INT;
                }
            }

            if (0 == decodedInputHolder.BtnDLevel) {
                thatCharacterInNextFrame.BtnDHoldingRdfCount = 0;
            } else if (JAMMED_BTN_HOLDING_RDF_CNT != currCharacterDownsync.BtnDHoldingRdfCount && 0 < decodedInputHolder.BtnDLevel) {
                thatCharacterInNextFrame.BtnDHoldingRdfCount = currCharacterDownsync.BtnDHoldingRdfCount+1;
                if (thatCharacterInNextFrame.BtnDHoldingRdfCount > MAX_INT) {
                    thatCharacterInNextFrame.BtnDHoldingRdfCount = MAX_INT;
                }
            }

            if (0 == decodedInputHolder.BtnELevel) {
                thatCharacterInNextFrame.BtnEHoldingRdfCount = 0;
            } else if (JAMMED_BTN_HOLDING_RDF_CNT != currCharacterDownsync.BtnEHoldingRdfCount && 0 < decodedInputHolder.BtnELevel) {
                thatCharacterInNextFrame.BtnEHoldingRdfCount = currCharacterDownsync.BtnEHoldingRdfCount+1;
                if (thatCharacterInNextFrame.BtnEHoldingRdfCount > MAX_INT) {
                    thatCharacterInNextFrame.BtnEHoldingRdfCount = MAX_INT;
                }
            }
        }

        public static bool isAllConfirmed(ulong confirmedList, int roomCapacity) {
            return (confirmedList+1 == (1UL << roomCapacity));
        }
        
        public static (bool, int) isRdfAllConfirmed(int rdfId, FrameRingBuffer<InputFrameDownsync> inputBuffer, int roomCapacity) {
            int delayedInputFrameId = ConvertToDelayedInputFrameId(rdfId-1);
            if (0 >= delayedInputFrameId) {
                return (false, delayedInputFrameId);
                //throw new ArgumentNullException(String.Format("currRdfId={0}, delayedInputFrameId={0} is invalid when checking all-confirmed!", currRdfId, delayedInputFrameId));
            }

            var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            if (!ok || null == delayedInputFrameDownsync) {
                return (false, delayedInputFrameId);
                //throw new ArgumentNullException(String.Format("InputFrameDownsync for delayedInputFrameId={0} is invalid when checking all-confirmed!", delayedInputFrameId));
            }
            return (isAllConfirmed(delayedInputFrameDownsync.ConfirmedList, roomCapacity), delayedInputFrameId);
        }

        public static bool chOmittingSoftPushback(CharacterDownsync ch, Skill? skillConfig, BulletConfig? bulletConfig) {
            if (Dimmed == ch.CharacterState || BlownUp1 == ch.CharacterState) return true;
            if (GroundDodged == ch.CharacterState) return true;
            if (ch.OmitSoftPushback) return true;
            if (null != bulletConfig) {
                return (BulletType.Melee == bulletConfig.BType && bulletConfig.OmitSoftPushback);
            }
            return false;
        }    
    }
}
