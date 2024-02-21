using System;
using static shared.CharacterState;
using System.Collections.Generic;
using Google.Protobuf.Collections;

namespace shared {
    public partial class Battle {
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

        public static int ConvertToDelayedInputFrameId(int renderFrameId) {
            if (renderFrameId < INPUT_DELAY_FRAMES) {
                return 0;
            }
            return ((renderFrameId - INPUT_DELAY_FRAMES) >> INPUT_SCALE_FRAMES);
        }

        public static int ConvertToNoDelayInputFrameId(int renderFrameId) {
            return (renderFrameId >> INPUT_SCALE_FRAMES);
        }

        public static int ConvertToFirstUsedRenderFrameId(int inputFrameId) {
            return ((inputFrameId << INPUT_SCALE_FRAMES) + INPUT_DELAY_FRAMES);
        }

        public static int ConvertToLastUsedRenderFrameId(int inputFrameId) {
            return ((inputFrameId << INPUT_SCALE_FRAMES) + INPUT_DELAY_FRAMES + (1 << INPUT_SCALE_FRAMES) - 1);
        }

        public static bool DecodeInput(ulong encodedInput, InputFrameDecoded holder) {
            int encodedDirection = (int)(encodedInput & 15);
            int btnALevel = (int)((encodedInput >> 4) & 1);
            int btnBLevel = (int)((encodedInput >> 5) & 1);
            int btnCLevel = (int)((encodedInput >> 6) & 1);
            int btnDLevel = (int)((encodedInput >> 7) & 1);

            holder.Dx = DIRECTION_DECODER[encodedDirection, 0];
            holder.Dy = DIRECTION_DECODER[encodedDirection, 1];
            holder.BtnALevel = btnALevel;
            holder.BtnBLevel = btnBLevel;
            holder.BtnCLevel = btnCLevel;
            holder.BtnDLevel = btnDLevel;
            return true;
        }

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

        public static bool UpdateInputFrameInPlaceUponDynamics(int inputFrameId, int roomCapacity, ulong confirmedList, RepeatedField<ulong> inputList, int[] lastIndividuallyConfirmedInputFrameId, ulong[] lastIndividuallyConfirmedInputList, int toExcludeJoinIndex) {
            bool hasInputFrameUpdatedOnDynamics = false;
            for (int i = 0; i < roomCapacity; i++) {
                if ((i + 1) == toExcludeJoinIndex) {
                    // On frontend, a "self input" is only confirmed by websocket downsync, which is quite late and might get the "self input" incorrectly overwritten if not excluded here
                    continue;
                }
                ulong joinMask = (1UL << i);
                if (0 < (confirmedList & joinMask)) {
                    // This in-place update is only valid when "delayed input for this player is not yet confirmed"
                    continue;
                }
                if (lastIndividuallyConfirmedInputFrameId[i] >= inputFrameId) {
                    // Already confirmed, no need to predict.
                    continue;
                }
                ulong newVal = (lastIndividuallyConfirmedInputList[i] & 15);
                if (newVal != inputList[i]) {
                    inputList[i] = newVal;
                    hasInputFrameUpdatedOnDynamics = true;
                }
            }
            return hasInputFrameUpdatedOnDynamics;
        }

        private static (int, bool, bool, int, int) _derivePlayerOpPattern(CharacterDownsync currCharacterDownsync, RoomDownsyncFrame currRenderFrame, CharacterConfig chConfig, FrameRingBuffer<InputFrameDownsync> inputBuffer, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
            // returns (patternId, jumpedOrNot, slipJumpedOrNot, effectiveDx, effectiveDy)
            int delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrame.Id);
            int delayedInputFrameIdForPrevRdf = ConvertToDelayedInputFrameId(currRenderFrame.Id - 1);

            if (0 >= delayedInputFrameId) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0);
            }

            if (noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0);
            }

            bool interrupted = _processDebuffDuringInput(currCharacterDownsync);
            if (interrupted) {
                return (PATTERN_ID_UNABLE_TO_OP, false, false, 0, 0);
            }

            var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            if (!ok || null == delayedInputFrameDownsync) {
                throw new ArgumentNullException(String.Format("InputFrameDownsync for delayedInputFrameId={0} is null!", delayedInputFrameId));
            }
            var delayedInputList = delayedInputFrameDownsync.InputList;

            RepeatedField<ulong>? delayedInputListForPrevRdf = null;
            if (0 < delayedInputFrameIdForPrevRdf) {
                var (_, delayedInputFrameDownsyncForPrevRdf) = inputBuffer.GetByFrameId(delayedInputFrameIdForPrevRdf);
                if (null != delayedInputFrameDownsyncForPrevRdf) {
                    delayedInputListForPrevRdf = delayedInputFrameDownsyncForPrevRdf.InputList;
                }
            }

            bool jumpedOrNot = false;
            bool slipJumpedOrNot = false;
            int joinIndex = currCharacterDownsync.JoinIndex;

            DecodeInput(delayedInputList[joinIndex - 1], decodedInputHolder);

            int effDx = 0, effDy = 0;

            if (null != delayedInputListForPrevRdf) {
                DecodeInput(delayedInputListForPrevRdf[joinIndex - 1], prevDecodedInputHolder);
            }

            // Jumping is partially allowed within "CapturedByInertia", but moving is only allowed when "0 == FramesToRecover" (constrained later in "ApplyInputFrameDownsyncDynamicsOnSingleRenderFrame")
            if (1 >= currCharacterDownsync.FramesToRecover) {
                // Direction control is respected since "1 == currCharacterDownsync.FramesToRecover" to favor smooth crouching transition
                effDx = decodedInputHolder.Dx;
                effDy = decodedInputHolder.Dy;
            } else if (WalkingAtk1 == currCharacterDownsync.CharacterState) {
                effDx = decodedInputHolder.Dx;
            }

            int patternId = PATTERN_ID_NO_OP;
            var canJumpWithinInertia = (0 == currCharacterDownsync.FramesToRecover && ((chConfig.InertiaFramesToRecover >> 1) > currCharacterDownsync.FramesCapturedByInertia));
            if (canJumpWithinInertia) {
                if (decodedInputHolder.BtnALevel > prevDecodedInputHolder.BtnALevel) {
                    if (chConfig.DashingEnabled && 0 > decodedInputHolder.Dy && Dashing != currCharacterDownsync.CharacterState) {
                        // Checking "DashingEnabled" here to allow jumping when dashing-disabled players pressed "DOWN + BtnB"
                        patternId = PATTERN_DOWN_A;
                    } else if (chConfig.SlidingEnabled && 0 > decodedInputHolder.Dy && Sliding != currCharacterDownsync.CharacterState) {  
                        patternId = PATTERN_DOWN_A;
                    } else if (currCharacterDownsync.PrimarilyOnSlippableHardPushback && (0 < decodedInputHolder.Dy && 0 == decodedInputHolder.Dx)) {
                        slipJumpedOrNot = true;
                    } else if (!inAirSet.Contains(currCharacterDownsync.CharacterState) && !isCrouching(currCharacterDownsync.CharacterState)) {
                        jumpedOrNot = true;
                    } else if (OnWallIdle1 == currCharacterDownsync.CharacterState) {
                        jumpedOrNot = true;
                    }
                }
            }

            if (PATTERN_ID_NO_OP == patternId) {
                if (0 < decodedInputHolder.BtnBLevel) {
                    if (decodedInputHolder.BtnBLevel > prevDecodedInputHolder.BtnBLevel) {
                        if (0 > decodedInputHolder.Dy) {
                            patternId = PATTERN_DOWN_B;
                        } else if (0 < decodedInputHolder.Dy) {
                            patternId = PATTERN_UP_B;
                        } else {
                            patternId = PATTERN_B;
                        }
                    } else {
                        patternId = PATTERN_HOLD_B;
                    }
                }
            }

            if (PATTERN_ID_NO_OP == patternId) {
                if (decodedInputHolder.BtnCLevel > prevDecodedInputHolder.BtnCLevel) {
                    patternId = PATTERN_INVENTORY_SLOT_C;
                } else if (decodedInputHolder.BtnDLevel > prevDecodedInputHolder.BtnDLevel) {
                    patternId = PATTERN_INVENTORY_SLOT_D;
                }
            }

            return (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy);
        }

        public static bool isTriggerClickable(Trigger trigger) {
            return (0 == trigger.FramesToRecover && 0 < trigger.Quota);
        }

        private static bool _useSkill(int patternId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, ref int bulletLocalIdCounter, ref int bulletCnt, RoomDownsyncFrame currRenderFrame, RepeatedField<Bullet> nextRenderFrameBullets, bool slotUsed) {
            bool skillUsed = false;
            if (PATTERN_ID_NO_OP == patternId || PATTERN_ID_UNABLE_TO_OP == patternId) {
                return false;
            }
            var skillId = FindSkillId(patternId, currCharacterDownsync, chConfig.SpeciesId, slotUsed);
            int xfac = (0 < thatCharacterInNextFrame.DirX ? 1 : -1);
            bool hasLockVel = false;
            if (NO_SKILL != skillId) {
                var skillConfig = skills[skillId];
                if (skillConfig.MpDelta > currCharacterDownsync.Mp) {
                    skillId = FindSkillId(1, currCharacterDownsync, chConfig.SpeciesId, slotUsed); // Fallback to basic atk
                    if (!skills.ContainsKey(skillId)) {
                        return false;
                    }
                    skillConfig = skills[skillId];
                    if (skillConfig.MpDelta > currCharacterDownsync.Mp) {
                        return false; // The basic atk also uses MP and there's not enough, return false
                    }
                } else {
                    thatCharacterInNextFrame.Mp -= skillConfig.MpDelta;
                    if (0 >= thatCharacterInNextFrame.Mp) {
                        thatCharacterInNextFrame.Mp = 0;
                    }
                }
                thatCharacterInNextFrame.ActiveSkillId = skillId;
                thatCharacterInNextFrame.FramesToRecover = skillConfig.RecoveryFrames;

                int activeSkillHit = 0;
                var pivotBulletConfig = skillConfig.Hits[activeSkillHit];
                for (int i = 0; i < pivotBulletConfig.SimultaneousMultiHitCnt + 1; i++) {
                    thatCharacterInNextFrame.ActiveSkillHit = activeSkillHit;
                    if (!addNewBulletToNextFrame(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, xfac, skillConfig, nextRenderFrameBullets, activeSkillHit, skillId, ref bulletLocalIdCounter, ref bulletCnt, ref hasLockVel, null)) break;
                    activeSkillHit++;
                }

                if (false == hasLockVel && false == currCharacterDownsync.InAir && !pivotBulletConfig.AllowsWalking) {
                    thatCharacterInNextFrame.VelX = 0;
                }

                thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;
                thatCharacterInNextFrame.FramesInChState = 0; // Must reset "FramesInChState" here to handle the extreme case where a same skill, e.g. "Atk1", is used right after the previous one ended
                if (thatCharacterInNextFrame.FramesInvinsible < pivotBulletConfig.StartupInvinsibleFrames) {
                    thatCharacterInNextFrame.FramesInvinsible = pivotBulletConfig.StartupInvinsibleFrames;
                }

                skillUsed = true;
            }

            return skillUsed;
        }

        private static bool _useInventorySlot(int rdfId, int patternId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame) {
            bool slotUsed = false;
            if (PATTERN_INVENTORY_SLOT_C != patternId && PATTERN_INVENTORY_SLOT_D != patternId) {
                return slotUsed;
            }
            if (0 < currCharacterDownsync.FramesToRecover) {
                return slotUsed;
            }
            int slotIdx = (PATTERN_INVENTORY_SLOT_C == patternId ? 0 : 1); 
            var targetSlotCurr = currCharacterDownsync.Inventory.Slots[slotIdx];
            var targetSlotNext = thatCharacterInNextFrame.Inventory.Slots[slotIdx];
            if (InventorySlotStockType.QuotaIv == targetSlotCurr.StockType) {
                if (0 < targetSlotCurr.Quota) {
                    targetSlotNext.Quota = targetSlotCurr.Quota - 1; 
                    slotUsed = true;
                }
            } else if (InventorySlotStockType.TimedIv == targetSlotCurr.StockType) {
                if (0 == targetSlotCurr.FramesToRecover) {
                    targetSlotNext.FramesToRecover = targetSlotCurr.DefaultFramesToRecover; 
                    slotUsed = true;
                }
            } else if (InventorySlotStockType.TimedMagazineIv == targetSlotCurr.StockType) {
                if (0 < targetSlotCurr.Quota) {
                    targetSlotNext.Quota = targetSlotCurr.Quota - 1; 
                    if (0 == targetSlotNext.Quota) {
                        targetSlotNext.FramesToRecover = targetSlotCurr.DefaultFramesToRecover; 
                    }
                    slotUsed = true;
                }
            }
    
            if (slotUsed) {
                if (TERMINATING_BUFF_SPECIES_ID != targetSlotCurr.BuffSpeciesId) {
                    var buffConfig = buffConfigs[targetSlotCurr.BuffSpeciesId];
                    int origChSpeciesId = SPECIES_NONE_CH;
                    if (SPECIES_NONE_CH != buffConfig.XformChSpeciesId) {
                        origChSpeciesId = currCharacterDownsync.SpeciesId;
                        var nextChConfig = characters[buffConfig.XformChSpeciesId];
                        AssignToCharacterDownsyncFromCharacterConfig(nextChConfig, thatCharacterInNextFrame);
                    }
                    // TODO: Support multi-buff simultaneously!
                    AssignToBuff(buffConfig.SpeciesId, buffConfig.Stock, rdfId, origChSpeciesId, thatCharacterInNextFrame.BuffList[0]);
                }
            }

            return slotUsed;
        }

        private static void _applyGravity(int rdfId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, ILoggerBridge logger) {
            /*
            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                logger.LogInfo("_applyGravity: rdfId=" + rdfId + ", " + stringifyPlayer(currCharacterDownsync));
            }
            */
            if (currCharacterDownsync.OmitGravity) {
                return;
            }
            if (!currCharacterDownsync.InAir) {
                return;
            }
            if (
                currCharacterDownsync.OnWall // [WARNING] It's important to have this "OnWall" criteria here, otherwise "isInJumpStartup(currCharacterDownsync)" would be confused by the "framesToRecover" assigned by "WallJumpingFramesToRecover"  
                &&
                (isInJumpStartup(thatCharacterInNextFrame) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame))
            ) {
                return; 
            }
            // TODO: The current dynamics calculation has a bug. When "true == currCharacterDownsync.InAir" and the character lands on the intersecting edge of 2 parallel rectangles, the hardPushbacks are doubled.
            if (OnWallIdle1 == currCharacterDownsync.CharacterState) {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY = chConfig.WallSlidingVelY;
            } else if (Dashing == currCharacterDownsync.CharacterState || Dashing == thatCharacterInNextFrame.CharacterState) {
                // Don't apply gravity if will enter dashing state in next frame
                thatCharacterInNextFrame.VelX += GRAVITY_X;
            } else {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY += GRAVITY_Y;
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
                }
            }
            return false;
        }

        private static void _processPlayerInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<Bullet> nextRenderFrameBullets, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = 0; i < roomCapacity; i++) {
                var currCharacterDownsync = currRenderFrame.PlayersArr[i];
                var thatCharacterInNextFrame = nextRenderFramePlayers[i];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy) = _derivePlayerOpPattern(currCharacterDownsync, currRenderFrame, chConfig, inputBuffer, decodedInputHolder, prevDecodedInputHolder);

                // Prioritize use of inventory slot over skills
                bool slotUsed = _useInventorySlot(currRenderFrame.Id, patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame);

                if (PATTERN_ID_UNABLE_TO_OP == patternId && 0 < currCharacterDownsync.FramesToRecover) {
                    _processNextFrameJumpStartup(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, chConfig, logger);
                    _processDelayedBulletSelfVel(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, chConfig, logger);
                    continue;
                }

                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;
                thatCharacterInNextFrame.SlipJumpTriggered = slipJumpedOrNot;

                bool usedSkill = _useSkill(patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets, slotUsed);
                Skill? skillConfig = null;
                if (usedSkill) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0; // The use of a skill should break "CapturedByInertia"
                    skillConfig = skills[thatCharacterInNextFrame.ActiveSkillId];
                    if (!skillConfig.Hits[0].AllowsWalking) {
                        continue; // Don't allow movement if skill is used
                    }
                }
                _processNextFrameJumpStartup(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, chConfig, logger);
                _processInertiaWalking(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, chConfig, false, usedSkill, skillConfig, logger);
                _processDelayedBulletSelfVel(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, chConfig, logger);
            }
        }
        
        public static void _resetVelocityOnRecovered(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame) {
            // [WARNING] This is a necessary cleanup before "_processInertiaWalking"!
            if (1 == currCharacterDownsync.FramesToRecover && 0 == thatCharacterInNextFrame.FramesToRecover && (Atked1 == currCharacterDownsync.CharacterState || InAirAtked1 == currCharacterDownsync.CharacterState || CrouchAtked1 == currCharacterDownsync.CharacterState)) {
                thatCharacterInNextFrame.VelX = 0;
                thatCharacterInNextFrame.VelY = 0;
            }
        }

        public static void _processNextFrameJumpStartup(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, ILoggerBridge logger) {
            /*
            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                logger.LogInfo("_processNextFrameJumpStartup: rdfId=" + rdfId + ", " + stringifyPlayer(currCharacterDownsync));
            }
            */
            if (isInJumpStartup(thatCharacterInNextFrame)) {
                return;
            }

            if (currCharacterDownsync.JumpTriggered) {
                // [WARNING] This assignment blocks a lot of CharacterState transition logic, including "_processInertiaWalking"!
                if (currCharacterDownsync.OnWall) {
                    thatCharacterInNextFrame.FramesToStartJump = (chConfig.ProactiveJumpStartupFrames >> 1);
                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
                    thatCharacterInNextFrame.VelY = 0;
                } else {
                    thatCharacterInNextFrame.FramesToStartJump = chConfig.ProactiveJumpStartupFrames;
                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                }
            } else if (isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame)) {
                thatCharacterInNextFrame.JumpStarted = true;
            }
        }
    
        public static void _processDelayedBulletSelfVel(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, ILoggerBridge logger) {
            if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) {
                return;
            }
            var skill = skills[currCharacterDownsync.ActiveSkillId];
            if (NO_SKILL_HIT == currCharacterDownsync.ActiveSkillHit || skill.Hits.Count <= currCharacterDownsync.ActiveSkillHit) {
                return;
            }
            var bulletConfig = skill.Hits[currCharacterDownsync.ActiveSkillHit];
            if (!bulletConfig.DelaySelfVelToActive) {   
                return;
            }
            if (currCharacterDownsync.CharacterState != skill.BoundChState) {
                // This shouldn't happen, but if it does, we don't proceed to set "selfLockVel"
                return;
            }
            if (currCharacterDownsync.FramesInChState != bulletConfig.StartupFrames) {
                return;
            }
            int xfac = (0 < currCharacterDownsync.DirX ? 1 : -1);
            if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
            }
            if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
            }
        }

        public static void _processInertiaWalking(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int effDx, int effDy, CharacterConfig chConfig, bool shouldIgnoreInertia, bool usedSkill, Skill? skillConfig, ILoggerBridge logger) {
            if (isInJumpStartup(thatCharacterInNextFrame) || isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame)) {
                return;
            }
            /*
            if (TurnAround == currCharacterDownsync.CharacterState) {
                logger.LogInfo(stringifyPlayer(currCharacterDownsync) + " is already turning around at rdfId=" + rdfId);
            }
            */
            bool currFreeFromInertia = (0 == currCharacterDownsync.FramesCapturedByInertia);
            bool currBreakingFromInertia = (1 == currCharacterDownsync.FramesCapturedByInertia);
            /* 
            [WARNING] 
            Special cases for turn-around inertia handling:
            1. if "true == thatCharacterInNextFrame.JumpTriggered", then we've already met the criterions of "canJumpWithinInertia" in "_derivePlayerOpPattern";
            2. if "InAirIdle1ByJump || InAirIdle1NoJump", turn-around should still be bound by inertia just like that of ground movements; 
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

            if (0 == currCharacterDownsync.FramesToRecover || (WalkingAtk1 == currCharacterDownsync.CharacterState || WalkingAtk4 == currCharacterDownsync.CharacterState)) {
                thatCharacterInNextFrame.CharacterState = Idle1; // When reaching here, the character is at least recovered from "Atked{N}" or "Atk{N}" state, thus revert back to "Idle" as a default action
                if (shouldIgnoreInertia) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0;
                    if (0 != effDx) {
                        int xfac = (0 < effDx ? 1 : -1);
                        thatCharacterInNextFrame.DirX = effDx;
                        thatCharacterInNextFrame.DirY = effDy;
                        if (!isStaticCrouching(currCharacterDownsync.CharacterState)) {
                            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                thatCharacterInNextFrame.VelX = xfac * chConfig.WallJumpingInitVelX;
                            } else {
                                thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                            }
                            thatCharacterInNextFrame.CharacterState = Walking;
                        }
                    } else {
                        thatCharacterInNextFrame.VelX = 0;
                    }
                } else {
                    if (alignedWithInertia || withInertiaBreakingState || currBreakingFromInertia) {
                        if (!alignedWithInertia) {
                            // Should reset "FramesCapturedByInertia" in this case!
                            thatCharacterInNextFrame.FramesCapturedByInertia = 0;
                        } 

                        if (0 != effDx) {
                            int xfac = (0 < effDx ? 1 : -1);
                            thatCharacterInNextFrame.DirX = effDx;
                            thatCharacterInNextFrame.DirY = effDy;
                            if (!isStaticCrouching(currCharacterDownsync.CharacterState)) {
                                if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.VelX = xfac * chConfig.WallJumpingInitVelX;
                                } else {
                                    thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                                }
                                thatCharacterInNextFrame.CharacterState = Walking;
                            }
                        } else {
                            thatCharacterInNextFrame.VelX = 0;
                        }
                    } else if (currFreeFromInertia) {
                        if (exactTurningAround) {
                            // logger.LogInfo(stringifyPlayer(currCharacterDownsync) + " is turning around at rdfId=" + rdfId);
                            thatCharacterInNextFrame.CharacterState = (chConfig.HasTurnAroundAnim && !currCharacterDownsync.InAir) ? TurnAround : Walking;
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                            if (chConfig.InertiaFramesToRecover > thatCharacterInNextFrame.FramesToRecover) {
                                // [WARNING] Deliberately not setting "thatCharacterInNextFrame.FramesToRecover" if not turning around to allow using skills!
                                thatCharacterInNextFrame.FramesToRecover = (chConfig.InertiaFramesToRecover - 1); // To favor animation playing and prevent skill use when turning-around
                            }
                        } else if (stoppingFromWalking) {
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
                        } else {
                            // Updates CharacterState and thus the animation to make user see graphical feedback asap.
                            thatCharacterInNextFrame.CharacterState = Walking;
                            thatCharacterInNextFrame.FramesCapturedByInertia = (chConfig.InertiaFramesToRecover >> 1);
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
                 * A dirty fix here just for GunGirl "Atk1 -> WalkingAtk1" transition.
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
                        thatCharacterInNextFrame.CharacterState = CrouchAtk1;
                    } else if (null != skillConfig) {
                        thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;
                    }
                }
            }
        }

        public static bool IsBulletExploding(Bullet bullet) {
            switch (bullet.Config.BType) {
                case BulletType.Melee:
                    return (BulletState.Exploding == bullet.BlState && bullet.FramesInBlState < bullet.Config.ExplosionFrames);
                case BulletType.Fireball:
                    return (BulletState.Exploding == bullet.BlState);
                default:
                    return false;
            }
        }

        public static bool IsBulletActive(Bullet bullet, int currRenderFrameId) {
            if (BulletState.Exploding == bullet.BlState) {
                if (!bullet.Config.RemainUponHit) {
                    return false;
                }
            }
            return (bullet.BattleAttr.OriginatedRenderFrameId + bullet.Config.StartupFrames < currRenderFrameId) && (bullet.BattleAttr.OriginatedRenderFrameId + bullet.Config.StartupFrames + bullet.Config.ActiveFrames > currRenderFrameId);
        }

        public static bool IsBulletAlive(Bullet bullet, int currRenderFrameId) {
            if (BulletState.Exploding == bullet.BlState) {
                return bullet.FramesInBlState < bullet.Config.ExplosionFrames;
            }
            return (bullet.BattleAttr.OriginatedRenderFrameId + bullet.Config.StartupFrames + bullet.Config.ActiveFrames > currRenderFrameId);
        }

        private static void _insertFromEmissionDerivedBullets(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> currRenderFrameBullets, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            bool dummyHasLockVel = false; // Would be ALWAYS false when used within this function bcz we're only adding subsequent multihit bullets!
            for (int i = 0; i < currRenderFrameBullets.Count; i++) {
                var src = currRenderFrameBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BattleAttr.BulletLocalId) break;
                int j = src.BattleAttr.OffenderJoinIndex - 1;
                if (j >= roomCapacity && j >= npcCnt) {
                    // Although "nextRenderFrameNpcs" is terminated by a special "id", a bullet could reference an npc instance outside of termination by "BattleAttr.OffenderJoinIndex" and thus get "contaminated data from reused memory" -- the rollback netcode implemented by this project only guarantees "eventual correctness" within the termination bounds of "playersArr/npcsArr/bulletsArr" while incorrect predictions could remain outside of the bounds.
                    continue;
                }
                var offender = (j < roomCapacity ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);

                var skillConfig = skills[src.BattleAttr.SkillId];
                bool inTheMiddleOfMeleeMultihitTransition = (BulletType.Melee == src.Config.BType && MultiHitType.FromEmission == src.Config.MhType && offender.ActiveSkillHit + 1 < skillConfig.Hits.Count);
                bool justEndedCurrentHit = (src.BattleAttr.OriginatedRenderFrameId + src.Config.StartupFrames + src.Config.ActiveFrames == currRenderFrame.Id);

                if (inTheMiddleOfMeleeMultihitTransition && justEndedCurrentHit) {
                    // [WARNING] Different from Fireball, multihit of Melee would add a new "Bullet" to "nextRenderFrameBullets" for convenience of handling explosion! The bullet "dst" could also be exploding by reaching here!
                    var offenderNextFrame = (j < roomCapacity ? nextRenderFramePlayers[j] : nextRenderFrameNpcs[j - roomCapacity]);
                    offenderNextFrame.ActiveSkillHit = offender.ActiveSkillHit + 1;
                    if (offenderNextFrame.ActiveSkillHit < skillConfig.Hits.Count) {
                        // No need to worry about Mp consumption here, it was already paid at "0 == offenderNextFrame.ActiveSkillHit" in "_useSkill"
                        int xfac = (0 < offenderNextFrame.DirX ? 1 : -1);
                        if (addNewBulletToNextFrame(src.BattleAttr.OriginatedRenderFrameId, offender, offenderNextFrame, xfac, skillConfig, nextRenderFrameBullets, offenderNextFrame.ActiveSkillHit, src.BattleAttr.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, null)) {
                            var bulletConfig = skillConfig.Hits[offenderNextFrame.ActiveSkillHit];
                            if (offenderNextFrame.FramesInvinsible < bulletConfig.StartupInvinsibleFrames) {
                                offenderNextFrame.FramesInvinsible = bulletConfig.StartupInvinsibleFrames;
                            }
                        }
                    }
                }
            }
        }

        private static void _insertBulletColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> currRenderFrameBullets, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, ref int colliderCnt, CollisionSpace collisionSys, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = 0; i < currRenderFrameBullets.Count; i++) {
                var src = currRenderFrameBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BattleAttr.BulletLocalId) break;
                var dst = nextRenderFrameBullets[bulletCnt];
                var dstVelY = src.VelY + (src.Config.TakesGravity ? GRAVITY_Y : 0);
                if (dstVelY < DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID) {
                    dstVelY = DEFAULT_MIN_FALLING_VEL_Y_VIRTUAL_GRID;
                }
                AssignToBullet(
                        src.BattleAttr.BulletLocalId,
                        src.BattleAttr.OriginatedRenderFrameId,
                        src.BattleAttr.OffenderJoinIndex,
                        src.BattleAttr.TeamId,
                        src.BlState, src.FramesInBlState + 1,
                        src.VirtualGridX, src.VirtualGridY, // virtual grid position
                        src.DirX, src.DirY, // dir
                        src.VelX, dstVelY, // velocity
                        src.BattleAttr.ActiveSkillHit, src.BattleAttr.SkillId, src.Config,
                        src.Config.RepeatQuota,
                        dst);

                int j = dst.BattleAttr.OffenderJoinIndex - 1;
                if (j >= roomCapacity && j >= npcCnt) {
                    // Although "nextRenderFrameNpcs" is terminated by a special "id", a bullet could reference an npc instance outside of termination by "BattleAttr.OffenderJoinIndex" and thus get "contaminated data from reused memory" -- the rollback netcode implemented by this project only guarantees "eventual correctness" within the termination bounds of "playersArr/npcsArr/bulletsArr" while incorrect predictions could remain outside of the bounds.
                    continue;
                }

                var offender = (j < roomCapacity ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);

                if (!IsBulletAlive(dst, currRenderFrame.Id)) {
                    continue;
                }

                if (BulletType.Melee == dst.Config.BType) {
                    if (noOpSet.Contains(offender.CharacterState)) {
                        // If a melee is alive but the offender got attacked, remove it even if it's active
                        dst.BattleAttr.BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
                        continue;
                    }
                    if (IsBulletActive(dst, currRenderFrame.Id)) {
                        var (newVx, newVy) = (offender.VirtualGridX + dst.DirX * src.Config.HitboxOffsetX, offender.VirtualGridY);
                        var (bulletCx, bulletCy) = VirtualGridToPolygonColliderCtr(newVx, newVy);
                        var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(src.Config.HitboxSizeX, src.Config.HitboxSizeY);
                        var newBulletCollider = dynamicRectangleColliders[colliderCnt];
                        UpdateRectCollider(newBulletCollider, bulletCx, bulletCy, hitboxSizeCx, hitboxSizeCy, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst);
                        colliderCnt++;

                        collisionSys.AddSingle(newBulletCollider);
                        dst.VirtualGridX = newVx;
                        dst.VirtualGridY = newVy;
                        dst.BlState = BulletState.Active;
                        if (dst.BlState != src.BlState) {
                            dst.FramesInBlState = 0;
                        }
                    }
                    bulletCnt++;
                } else if (BulletType.Fireball == src.Config.BType) {
                    if (IsBulletActive(dst, currRenderFrame.Id)) {
                        var (bulletCx, bulletCy) = VirtualGridToPolygonColliderCtr(src.VirtualGridX, src.VirtualGridY);
                        var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(src.Config.HitboxSizeX + src.Config.HitboxSizeIncX*src.FramesInBlState, src.Config.HitboxSizeY + src.Config.HitboxSizeIncY*src.FramesInBlState);
                        var newBulletCollider = dynamicRectangleColliders[colliderCnt];
                        UpdateRectCollider(newBulletCollider, bulletCx, bulletCy, hitboxSizeCx, hitboxSizeCy, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst);
                        colliderCnt++;

                        collisionSys.AddSingle(newBulletCollider);
                        if (BulletState.StartUp == src.BlState && !dst.Config.RemainUponHit) {
                            dst.BlState = BulletState.Active;
                            if (dst.BlState != src.BlState) {
                                dst.FramesInBlState = 0;
                            }
                        }
                        (dst.VirtualGridX, dst.VirtualGridY) = (dst.VirtualGridX + dst.VelX, dst.VirtualGridY + dst.VelY);
                    } else {
                        if (noOpSet.Contains(offender.CharacterState)) {
                            // If a fireball is not yet active but the offender got attacked, remove it
                            continue;
                        }
                    }
                    bulletCnt++;
                } else {
                    continue;
                }
            }

            // Explicitly specify termination of nextRenderFrameBullets
            nextRenderFrameBullets[bulletCnt].BattleAttr.BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
        }

        public static bool isStaticCrouching(CharacterState state) {
            return (CrouchIdle1 == state || CrouchAtk1 == state || CrouchAtked1 == state);
        }

        public static bool isCrouching(CharacterState state) {
            return (CrouchIdle1 == state || CrouchAtk1 == state || CrouchAtked1 == state || Sliding == state);
        }

        private static void _moveAndInsertCharacterColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Vector[] effPushbacks, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, ref int colliderCnt, int iSt, int iEd, ILoggerBridge logger) {
            for (int i = iSt; i < iEd; i++) {
                var currCharacterDownsync = (i < roomCapacity ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                var thatCharacterInNextFrame = (i < roomCapacity ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                effPushbacks[i].X = 0;
                effPushbacks[i].Y = 0;
                int vhDiffInducedByCrouching = 0;
                bool justBecameCrouching = !currCharacterDownsync.PrevWasCrouching && !currCharacterDownsync.InAir && (0 == currCharacterDownsync.FramesInChState) && isCrouching(currCharacterDownsync.CharacterState);
                if (justBecameCrouching) {
                    vhDiffInducedByCrouching -= ((chConfig.DefaultSizeY - chConfig.ShrinkedSizeY) >> 1);
                }

                int newVx = currCharacterDownsync.VirtualGridX + currCharacterDownsync.VelX + currCharacterDownsync.FrictionVelX, newVy = currCharacterDownsync.VirtualGridY + currCharacterDownsync.VelY + vhDiffInducedByCrouching;
                if (currCharacterDownsync.JumpStarted) {
                    // We haven't proceeded with "OnWall" calculation for "thatPlayerInNextFrame", thus use "currCharacterDownsync.OnWall" for checking
                    if (currCharacterDownsync.OnWall && InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                        // logger.LogInfo("rdfId=" + currRenderFrame.Id + ", wall jump started for " + stringifyPlayer(currCharacterDownsync));
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
                    } else {
                        thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY;
                        thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                    }
                } else if (!currCharacterDownsync.InAir && currCharacterDownsync.PrimarilyOnSlippableHardPushback && currCharacterDownsync.SlipJumpTriggered) {
                    // [WARNING] By now "slipJump" is deliberately implemented without any startup frame, because I haven't prepared proper animations for it :(
                    newVy -= SLIP_JUMP_CHARACTER_DROP_VIRTUAL;
                } 

                if (i < roomCapacity && 0 >= thatCharacterInNextFrame.Hp && 0 == thatCharacterInNextFrame.FramesToRecover) {
                    // Revive player-controlled character from Dying
                    (newVx, newVy) = (currCharacterDownsync.RevivalVirtualGridX, currCharacterDownsync.RevivalVirtualGridY);
                    thatCharacterInNextFrame.CharacterState = GetUp1; // No need to tune bounding box and offset for this case, because the revival location is fixed :)
                    thatCharacterInNextFrame.FramesInChState = 0;
                    thatCharacterInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                    thatCharacterInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;

                    thatCharacterInNextFrame.Hp = currCharacterDownsync.MaxHp;
                    thatCharacterInNextFrame.DirX = currCharacterDownsync.RevivalDirX;
                    thatCharacterInNextFrame.DirY = currCharacterDownsync.RevivalDirY;
                    
                    int prevBuffI = 0; 
                    while (prevBuffI < currCharacterDownsync.BuffList.Count) {
                        var cand = currCharacterDownsync.BuffList[prevBuffI++];
                        if (TERMINATING_BUFF_SPECIES_ID == cand.SpeciesId) break; 
                        revertBuff(cand, thatCharacterInNextFrame);
                    }
                    AssignToBuff(TERMINATING_BUFF_SPECIES_ID, 0, TERMINATING_RENDER_FRAME_ID, SPECIES_NONE_CH, thatCharacterInNextFrame.BuffList[0]);

                    int prevDebuffI = 0; 
                    while (prevDebuffI < currCharacterDownsync.DebuffList.Count) {
                        var cand = currCharacterDownsync.DebuffList[prevDebuffI++];
                        if (TERMINATING_DEBUFF_SPECIES_ID == cand.SpeciesId) break; 
                        revertDebuff(cand, thatCharacterInNextFrame);
                    }
                    AssignToDebuff(TERMINATING_DEBUFF_SPECIES_ID, 0, thatCharacterInNextFrame.DebuffList[0]);
                }

                float boxCx, boxCy, boxCw, boxCh;
                calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, newVx, newVy, out boxCx, out boxCy, out boxCw, out boxCh);
                Collider characterCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(characterCollider, boxCx, boxCy, boxCw, boxCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync); // the coords of all barrier boundaries are multiples of tileWidth(i.e. 16), by adding snapping y-padding when "landedOnGravityPushback" all "characterCollider.Y" would be a multiple of 1.0
                colliderCnt++;

                // Add to collision system
                collisionSys.AddSingle(characterCollider);

                _applyGravity(currRenderFrame.Id, currCharacterDownsync, chConfig, thatCharacterInNextFrame, logger);
            }
        }

        private static void _calcCharacterMovementPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trigger> nextRenderFrameTriggers, RepeatedField<EvtSubscription> nextRenderFrameEvtSubs, ref ulong fulfilledEvtSubscriptionSetMask, int[] justFulfilledEvtSubArr, ref int justFulfilledEvtSubCnt, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPushbackEnabled, Collider[] dynamicRectangleColliders, int iSt, int iEd, FrameRingBuffer<Collider> residueCollided, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, ref int justTriggeredStoryPointId, RdfPushbackFrameLog? currPushbackFrameLog, bool pushbackFrameLogEnabled, ILoggerBridge logger) {
            // Calc pushbacks for each player (after its movement) w/o bullets
            if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
                currPushbackFrameLog.Reset();
                currPushbackFrameLog.setMaxJoinIndex(roomCapacity+currNpcI);
            }
            int primaryHardOverlapIndex;
            for (int i = iSt; i < iEd; i++) {
                primaryOverlapResult.reset();
                var currCharacterDownsync = (i < roomCapacity ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (i >= roomCapacity && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < roomCapacity ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                Trap? primaryTrap;
                int hardPushbackCnt = calcHardPushbacksNormsForCharacter(currRenderFrame, chConfig, currCharacterDownsync, thatCharacterInNextFrame, aCollider, aShape, hardPushbackNormsArr[i], collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, out primaryTrap, residueCollided, logger);

                if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
                    currPushbackFrameLog.ResetJoinIndex(currCharacterDownsync.JoinIndex);
                    currPushbackFrameLog.setTouchingCellsByJoinIndex(currCharacterDownsync.JoinIndex, aCollider);
                    currPushbackFrameLog.setHardPushbacksByJoinIndex(currCharacterDownsync.JoinIndex, primaryHardOverlapIndex, hardPushbackNormsArr[i] /* [WARNING] by now "hardPushbackNormsArr[i]" is not yet normalized */, hardPushbackCnt);
                }

                if (null != primaryTrap) {
                    thatCharacterInNextFrame.FrictionVelX = primaryTrap.VelX;
                }

                if (0 < hardPushbackCnt) {
                    /* 
                       if (2 <= hardPushbackCnt && 1 == currCharacterDownsync.JoinIndex) {
                       logger.LogInfo(String.Format("Before processing hardpushbacks with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString()));
                       }
                     */
                    processPrimaryAndImpactEffPushback(effPushbacks[i], hardPushbackNormsArr[i], hardPushbackCnt, primaryHardOverlapIndex, SNAP_INTO_PLATFORM_OVERLAP, false);
                    /* 
                       if (2 <= hardPushbackCnt && 1 == currCharacterDownsync.JoinIndex) {
                       logger.LogInfo(String.Format("After processing hardpushbacks with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString()));
                       }
                     */
                }

                bool landedOnGravityPushback = false;
                float normAlignmentWithGravity = (primaryOverlapResult.OverlapY * -1f);
                // Hold wall alignments of the primaryOverlapResult of hardPushbacks first, it'd be used later 
                float normAlignmentWithHorizon1 = (primaryOverlapResult.OverlapX * +1f);
                float normAlignmentWithHorizon2 = (primaryOverlapResult.OverlapX * -1f);
                thatCharacterInNextFrame.OnSlope = (!thatCharacterInNextFrame.OnWall && 0 != primaryOverlapResult.OverlapY && 0 != primaryOverlapResult.OverlapX);
                // Kindly remind that (primaryOverlapResult.OverlapX, primaryOverlapResult.OverlapY) points INTO the slope :) 
                float projectedVel = (thatCharacterInNextFrame.VelX * primaryOverlapResult.OverlapX + thatCharacterInNextFrame.VelY * primaryOverlapResult.OverlapY); // This value is actually in VirtualGrid unit, but converted to float, thus it'd be eventually rounded 
                bool goingDown = (thatCharacterInNextFrame.OnSlope && !currCharacterDownsync.JumpStarted && thatCharacterInNextFrame.VelY <= 0 && 0 > projectedVel); // We don't care about going up, it's already working...  
                if (goingDown) {
                    /*
                       if (2 == currCharacterDownsync.SpeciesId) {
                       logger.LogInfo(String.Format("Rdf.id={0} BEFOER, chState={1}, velX={2}, velY={3}, virtualGridX={4}, virtualGridY={5}: going down", currRenderFrame.Id, currCharacterDownsync.CharacterState, thatCharacterInNextFrame.VelX, thatCharacterInNextFrame.VelY, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY));
                       }
                     */
                    float newVelXApprox = thatCharacterInNextFrame.VelX - primaryOverlapResult.OverlapX * projectedVel;
                    float newVelYApprox = thatCharacterInNextFrame.VelY - primaryOverlapResult.OverlapY * projectedVel;
                    thatCharacterInNextFrame.VelX = (int)Math.Floor(newVelXApprox);
                    thatCharacterInNextFrame.VelY = (int)Math.Floor(newVelYApprox); // "VelY" here is < 0, take the floor to get a larger absolute value!
                    /*
                       if (2 == currCharacterDownsync.SpeciesId) {
                       logger.LogInfo(String.Format("Rdf.id={0} AFTER, chState={1}, velX={2}, velY={3}, virtualGridX={4}, virtualGridY={5}: going down", currRenderFrame.Id, currCharacterDownsync.CharacterState, thatCharacterInNextFrame.VelX, thatCharacterInNextFrame.VelY, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY));
                       }
                     */
                } else if (thatCharacterInNextFrame.OnSlope && Idle1 == thatCharacterInNextFrame.CharacterState && 0 == thatCharacterInNextFrame.VelX) {
                    // [WARNING] Prevent down-slope sliding, might not be preferred for some game designs, disable this if you need sliding on the slope
                    thatCharacterInNextFrame.VelY = 0;
                }

                if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                    landedOnGravityPushback = true;
                    /*
                       if (0 == currCharacterDownsync.SpeciesId) {
                       logger.LogInfo(String.Format("Landed with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString()));
                       }
                     */
                }
    
                bool shouldOmitSoftPushbackForSelf = (currCharacterDownsync.RepelSoftPushback || chOmittingSoftPushback(currCharacterDownsync));
                if (softPushbackEnabled && Dying != currCharacterDownsync.CharacterState && false == shouldOmitSoftPushbackForSelf) {
                    int softPushbacksCnt = 0, primarySoftOverlapIndex = -1;
                    int totOtherChCnt = 0, cellOverlappedOtherChCnt = 0, shapeOverlappedOtherChCnt = 0;
                    int origResidueCollidedSt = residueCollided.StFrameId, origResidueCollidedEd = residueCollided.EdFrameId; 
                    float primarySoftOverlapMagSqr = float.MinValue, primarySoftPushbackX = float.MinValue, primarySoftPushbackY = float.MinValue;
                    /*
                       if (0 == currCharacterDownsync.SpeciesId) {
                       logger.LogInfo(String.Format("Has {6} residueCollided with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}, primaryOverlapResult={5}", i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, primaryOverlapResult.ToString(), residueCollided.Cnt));
                       }
                     */
                    while (true) {
                        var (ok3, bCollider) = residueCollided.Pop();
                        if (false == ok3 || null == bCollider) {
                            break;
                        }
                        ConvexPolygon bShape = bCollider.Shape;
                        var v3 = bCollider.Data as TriggerColliderAttr;  
                        if (null != v3 && currCharacterDownsync.JoinIndex <= roomCapacity) {
                            // By now only "Player" can click "Trigger"s.
                            var atkedTrigger = currRenderFrame.TriggersArr[v3.TriggerLocalId];
                            var triggerConfig = atkedTrigger.Config;
                            if (0 == (triggerConfig.TriggerMask & TRIGGER_MASK_BY_MOVEMENT)) continue;
                            if (!isTriggerClickable(atkedTrigger)) continue;
                            var (clicked, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                            if (clicked) {
                                // Currently only allowing "Player" to click.
                                var atkedTriggerInNextFrame = nextRenderFrameTriggers[v3.TriggerLocalId];
                                var triggerConfigFromTiled = atkedTrigger.ConfigFromTiled;
                                atkedTriggerInNextFrame.Quota = atkedTrigger.Quota - 1;
                                atkedTriggerInNextFrame.FramesToFire = triggerConfigFromTiled.DelayedFrames;
                                atkedTriggerInNextFrame.FramesToRecover = triggerConfigFromTiled.RecoveryFrames;

                                if (StoryPoint.SpeciesId == atkedTriggerInNextFrame.ConfigFromTiled.SpeciesId) {
                                    justTriggeredStoryPointId = atkedTriggerInNextFrame.ConfigFromTiled.StoryPointId;
                                }

                                var nextExhaustEvtSub = nextRenderFrameEvtSubs[atkedTriggerInNextFrame.ConfigFromTiled.PublishingToEvtSubIdUponExhaust-1];
                                nextExhaustEvtSub.FulfilledEvtMask |= atkedTriggerInNextFrame.ConfigFromTiled.PublishingEvtMaskUponExhaust;
                                if (nextExhaustEvtSub.DemandedEvtMask == nextExhaustEvtSub.FulfilledEvtMask) {
                                    fulfilledEvtSubscriptionSetMask |= (1ul << (nextExhaustEvtSub.Id-1));
                                    nextExhaustEvtSub.DemandedEvtMask = (MAGIC_EVTSUB_ID_STORYPOINT == nextExhaustEvtSub.Id ? nextExhaustEvtSub.DemandedEvtMask : EVTSUB_NO_DEMAND_MASK);
                                    nextExhaustEvtSub.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                                    justFulfilledEvtSubArr[justFulfilledEvtSubCnt++] = nextExhaustEvtSub.Id;
                                }
                            }
                        }
                        var v2 = bCollider.Data as TrapColliderAttr;
                        if (null != v2 && v2.ProvidesEscape && currCharacterDownsync.JoinIndex <= roomCapacity) {
                            var (escaped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                            // Currently only allowing "Player" to win.
                            if (escaped) {
                                if (1 == roomCapacity) {
                                    confirmedBattleResult.WinnerJoinIndex = currCharacterDownsync.JoinIndex;
                                    continue;
                                } 
                                var (rdfAllConfirmed, delayedInputFrameId) = isRdfAllConfirmed(currRenderFrame.Id, inputBuffer, roomCapacity); 
                                if (rdfAllConfirmed) {
                                    confirmedBattleResult.WinnerJoinIndex = currCharacterDownsync.JoinIndex;
                                    continue;
                                } else {
                                    // [WARNING] This cached information could be created by a CORRECTLY PREDICTED "delayedInputFrameDownsync", thus we need a rollback from there on to finally consolidate the result later!
                                    unconfirmedBattleResults[delayedInputFrameId] = confirmedBattleResult; // The "value" here is actually not useful, it's just stuffed here for type-correctness :)
                                    continue;
                                }
                            } else {
                                continue;
                            }
                        }
                        var v1 = bCollider.Data as CharacterDownsync;
                        if (null == v1) {
                            continue;
                        } 
                        ++totOtherChCnt;
                        if (!COLLIDABLE_PAIRS.Contains(v1.CollisionTypeMask | currCharacterDownsync.CollisionTypeMask)) {
                            continue;
                        }
                        if (Dying == v1.CharacterState) {
                            continue;
                        }
                        if (chOmittingSoftPushback(v1)) {
                            continue;
                        }
                        if (currCharacterDownsync.ChCollisionTeamId == v1.ChCollisionTeamId) {
                            // ignore collision within same collisionTeam, rarely used
                            continue;
                        }

                        cellOverlappedOtherChCnt++;

                        var (overlapped, softPushbackX, softPushbackY) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                        if (!overlapped) {
                            continue;
                        }

                        // [WARNING] Due to yet unknown reason, the resultant order of "hardPushbackNormsArr[i]" could be random for different characters in the same battle (maybe due to rollback not recovering the existing StaticCollider-TouchingCell information which could've been swapped by "TouchingCell.unregister(...)", please generate FrameLog and see the PushbackFrameLog part for details), the following traversal processing MUST BE ORDER-INSENSITIVE for softPushbackX & softPushbackY!
                        float softPushbackXReduction = 0f, softPushbackYReduction = 0f; 
                        for (int k = 0; k < hardPushbackCnt; k++) {
                            Vector hardPushbackNorm = hardPushbackNormsArr[i][k];
                            float projectedMagnitude = softPushbackX * hardPushbackNorm.X + softPushbackY * hardPushbackNorm.Y;
                            if (0 > projectedMagnitude || (thatCharacterInNextFrame.OnSlope && k == primaryHardOverlapIndex)) {
                                // [WARNING] We don't want a softPushback to push an on-slope character either "into" or "outof" the slope!
                                softPushbackXReduction += projectedMagnitude * hardPushbackNorm.X; 
                                softPushbackYReduction += projectedMagnitude * hardPushbackNorm.Y; 
                            }
                        }

                        softPushbackX -= softPushbackXReduction;
                        softPushbackY -= softPushbackYReduction;

                        if (Math.Abs(softPushbackX) < VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO) {
                            // Clamp to zero if it does not move at least 1 virtual grid step
                            softPushbackX = 0;
                        }
                        if (Math.Abs(softPushbackY) < VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO) {
                            // Clamp to zero if it does not move at least 1 virtual grid step
                            softPushbackY = 0;
                        }

                        var magSqr = softPushbackX * softPushbackX + softPushbackY * softPushbackY;
                        if (0 >= magSqr) {
                            // [WARNING] In field test, the backend (.net 7.0) and frontend (.net 2.1/4.0) might disagree on whether or not 2 colliders have overlapped by shape check (due to possibly different treatment of floating errors -- no direct evidence can be provided but from pushbackFrameLogs it's most suspicious), and if one party doesn't recognize any softPushback while the other does, the latter would proceed with "processPrimaryAndImpactEffPushback", resulting in different SNAP_INTO_CHARACTER_OVERLAP usage, thus different RoomDownsyncFrame!   
                            // [WARNING] Hereby we skip recognizing effectively zero softPushbacks, yet a closed-loop control on frontend by "onRoomDownsyncFrame & useOthersForcedDownsyncRenderFrameDict" is required because such (suspicious) floating errors are too difficult to completely avoid.
                            continue;
                        }
                        
                        shapeOverlappedOtherChCnt++;

                        normAlignmentWithGravity = (overlapResult.OverlapY * -1f);
                        if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                            landedOnGravityPushback = true;
                        }

                        if (primarySoftOverlapMagSqr < magSqr) {
                            primarySoftOverlapMagSqr = magSqr;
                            primarySoftPushbackX = softPushbackX;
                            primarySoftPushbackY = softPushbackY;
                            primarySoftOverlapIndex = softPushbacksCnt;
                        } else if ((softPushbackX < primarySoftPushbackX) || (softPushbackX == primarySoftPushbackX && softPushbackY < primarySoftPushbackY)) {
                            primarySoftOverlapMagSqr = magSqr;
                            primarySoftPushbackX = softPushbackX;
                            primarySoftPushbackY = softPushbackY;
                            primarySoftOverlapIndex = softPushbacksCnt;
                        }

                        // [WARNING] Don't skip here even if both "softPushbackX" and "softPushbackY" are zero, because we'd like to record them in "pushbackFrameLog"
                        softPushbacks[softPushbacksCnt].X = softPushbackX;
                        softPushbacks[softPushbacksCnt].Y = softPushbackY;
                        softPushbacksCnt++;
                    }

                    if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
                        currPushbackFrameLog.setSoftPushbacksByJoinIndex(currCharacterDownsync.JoinIndex, primarySoftOverlapIndex, softPushbacks /* [WARNING] by now "softPushbacks" is not yet normalized */, softPushbacksCnt, totOtherChCnt, cellOverlappedOtherChCnt, shapeOverlappedOtherChCnt, origResidueCollidedSt, origResidueCollidedEd);
                    }
                    // logger.LogInfo(String.Format("Before processing softPushbacks: effPushback={0}, softPushbacks={1}, primarySoftOverlapIndex={2}", effPushbacks[i].ToString(), Vector.VectorArrToString(softPushbacks, softPushbacksCnt), primarySoftOverlapIndex));

                    processPrimaryAndImpactEffPushback(effPushbacks[i], softPushbacks, softPushbacksCnt, primarySoftOverlapIndex, SNAP_INTO_CHARACTER_OVERLAP, true);

                    //logger.LogInfo(String.Format("After processing softPushbacks: effPushback={0}, softPushbacks={1}, primarySoftOverlapIndex={2}", effPushbacks[i].ToString(), Vector.VectorArrToString(softPushbacks, softPushbacksCnt), primarySoftOverlapIndex));                         
                }

                /*
                if (!landedOnGravityPushback && !currCharacterDownsync.InAir && 0 >= currCharacterDownsync.VelY) {
                    logger.LogInfo(String.Format("Rdf.Id={0}, character {1} slipped with aShape={2}: hardPushbackNormsArr[i:{3}]={4}, effPushback={5}, touchCells=\n{6}", currRenderFrame.Id, currCharacterDownsync, aShape.ToString(false), i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), aCollider.TouchingCellsStaticColliderStr()));
                }
                */

                if (landedOnGravityPushback) {
                    thatCharacterInNextFrame.InAir = false;
                    if (null != primaryTrap) {
                        List<TrapColliderAttr> colliderAttrs = trapLocalIdToColliderAttrs[primaryTrap.TrapLocalId];
                        for (int j = 0; j < colliderAttrs.Count; j++) {
                            var colliderAttr = colliderAttrs[j];
                            if (colliderAttr.ProvidesSlipJump) {
                                thatCharacterInNextFrame.PrimarilyOnSlippableHardPushback = true;
                                break;
                            }
                        }
                    }
                    bool fallStopping = (currCharacterDownsync.InAir && 0 >= currCharacterDownsync.VelY);
                    if (fallStopping) {
                        thatCharacterInNextFrame.VelX = 0;
                        thatCharacterInNextFrame.VelY = (thatCharacterInNextFrame.OnSlope ? 0 : chConfig.DownSlopePrimerVelY);
                        if (Dying == thatCharacterInNextFrame.CharacterState) {
                            // No update needed for Dying
                        } else if (BlownUp1 == thatCharacterInNextFrame.CharacterState) {
                            thatCharacterInNextFrame.VelY = 0;
                            thatCharacterInNextFrame.CharacterState = LayDown1;
                            thatCharacterInNextFrame.FramesToRecover = chConfig.LayDownFrames;
                        } else {
                            switch (currCharacterDownsync.CharacterState) {
                                case BlownUp1:
                                case LayDown1:
                                case InAirIdle1NoJump:
                                case InAirIdle1ByJump:
                                case InAirIdle1ByWallJump:
                                case InAirAtk1:
                                case InAirAtked1:
                                case OnWallIdle1:
                                case Sliding:
                                case CrouchIdle1:
                                case CrouchAtk1:
                                case CrouchAtked1:
                                    // [WARNING] To prevent bouncing due to abrupt change of collider shape, it's important that we check "currCharacterDownsync" instead of "thatPlayerInNextFrame" here!
                                    int extraSafeGapToPreventBouncing = (chConfig.DefaultSizeY >> 2);
                                    var halfColliderVhDiff = ((chConfig.DefaultSizeY - (chConfig.ShrinkedSizeY + extraSafeGapToPreventBouncing)) >> 1);
                                    var (_, halfColliderChDiff) = VirtualGridToPolygonColliderCtr(0, halfColliderVhDiff);
                                    effPushbacks[i].Y -= halfColliderChDiff;
                                    /*
                                       if (0 == currCharacterDownsync.SpeciesId) {
                                           logger.LogInfo(String.Format("Rdf.Id={6}, Fall stopped with chState={3}, vy={4}, halfColliderChDiff={5}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}", i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY, halfColliderChDiff, currRenderFrame.Id));
                                       }
                                     */
                                    break;
                            }
                            if (InAirAtk1 == currCharacterDownsync.CharacterState) {
                                thatCharacterInNextFrame.FramesToRecover = 0;
                            }
                        }
                    } else {
                        // landedOnGravityPushback not fallStopping, could be in LayDown or GetUp or Dying
                        if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                            if (Dying == thatCharacterInNextFrame.CharacterState) {
                                // No update needed for Dying
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
                                    effPushbacks[i].Y -= halfColliderChDiff;
                                }
                            } else if (0 >= thatCharacterInNextFrame.VelY && !thatCharacterInNextFrame.OnSlope) {
                                // [WARNING] Covers 2 situations:
                                // 1. Walking up to a flat ground then walk back down, note that it could occur after a jump on the slope, thus should recover "DownSlopePrimerVelY";
                                // 2. Dashing down to a flat ground then walk back up. 
                                thatCharacterInNextFrame.VelY = chConfig.DownSlopePrimerVelY;
                            }
                        }
                        /*
                           if (0 == currCharacterDownsync.SpeciesId) {
                           logger.LogInfo(String.Format("Landed without fallstopping with chState={3}, vy={4}: hardPushbackNormsArr[i:{0}]={1}, effPushback={2}", i, Vector.VectorArrToString(hardPushbackNormsArr[i], hardPushbackCnt), effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY));
                           }
                         */
                    }
                }

                if (chConfig.OnWallEnabled) {

                    if (null == primaryTrap || (null != primaryTrap && !primaryTrap.ConfigFromTiled.ProhibitsWallGrabbing)) {

                        if (thatCharacterInNextFrame.InAir) {
                            // [WARNING] Grabbing to wall MUST BE based on "InAir", otherwise we would get gravity reduction from ground up incorrectly!
                            if (!noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                                if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1) {
                                    thatCharacterInNextFrame.OnWall = true;
                                    thatCharacterInNextFrame.OnWallNormX = +1;
                                    thatCharacterInNextFrame.OnWallNormY = 0;
                                }
                                if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2) {
                                    thatCharacterInNextFrame.OnWall = true;
                                    thatCharacterInNextFrame.OnWallNormX = -1;
                                    thatCharacterInNextFrame.OnWallNormY = 0;
                                }
                            }
                        }
                        if (!thatCharacterInNextFrame.OnWall) {
                            thatCharacterInNextFrame.OnWallNormX = 0;
                            thatCharacterInNextFrame.OnWallNormY = 0;
                        }
                    }
                }
            }
        }

        private static void _calcBulletCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, RepeatedField<Trigger> nextRenderFrameTriggers, ref SatResult overlapResult, Collision collision, Collider[] dynamicRectangleColliders, int iSt, int iEd, Dictionary<int, int> triggerTrackingIdToTrapLocalId, ref int bulletLocalIdCounter, ref int bulletCnt, EvtSubscription waveNpcKilledEvtSub, ref ulong fulfilledEvtSubscriptionSetMask, ILoggerBridge logger) {
            // [WARNING] Bullet collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
            // Check bullet-anything collisions
            for (int i = iSt; i < iEd; i++) {
                Collider bulletCollider = dynamicRectangleColliders[i];
                if (null == bulletCollider.Data) continue;
                var bulletNextFrame = bulletCollider.Data as Bullet; // [WARNING] See "_insertBulletColliders", the bound data in each collider is already belonging to "nextRenderFrameBullets"!
                if (null == bulletNextFrame || TERMINATING_BULLET_LOCAL_ID == bulletNextFrame.BattleAttr.BulletLocalId) {
                    logger.LogWarn(String.Format("dynamicRectangleColliders[i:{0}] is not having bullet type! iSt={1}, iEd={2}", i, iSt, iEd));
                    continue;
                }
                bool collided = bulletCollider.CheckAllWithHolder(0, 0, collision);
                if (!collided) continue;

                bool exploded = false;
                bool explodedOnAnotherCharacter = false;

                var bulletShape = bulletCollider.Shape;
                int j = bulletNextFrame.BattleAttr.OffenderJoinIndex - 1;
                var offender = (j < roomCapacity ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);
                var offenderNextFrame = (j < roomCapacity ? nextRenderFramePlayers[j] : nextRenderFrameNpcs[j - roomCapacity]);
                int effDirX = (BulletType.Melee == bulletNextFrame.Config.BType ? offender.DirX : bulletNextFrame.DirX);
                int xfac = (0 < effDirX ? 1 : -1);
                var skillConfig = skills[bulletNextFrame.BattleAttr.SkillId];
                while (true) {
                    var (ok, bCollider) = collision.PopFirstContactedCollider();
                    if (false == ok || null == bCollider) {
                        break;
                    }
                    var defenderShape = bCollider.Shape;
                    var (overlapped, _, _) = calcPushbacks(0, 0, bulletShape, defenderShape, false, ref overlapResult);
                    if (!overlapped) continue;

                    switch (bCollider.Data) {
                        case PatrolCue v:
                            break;
                        case TriggerColliderAttr atkedTriggerColliderAttr:
                            var atkedTrigger = currRenderFrame.TriggersArr[atkedTriggerColliderAttr.TriggerLocalId];
                            var triggerConfig = atkedTrigger.Config;
                            if (0 == (triggerConfig.TriggerMask & TRIGGER_MASK_BY_ATK)) continue;
                            if (!isTriggerClickable(atkedTrigger)) continue;
                            if (bulletNextFrame.BattleAttr.OffenderJoinIndex <= roomCapacity) {
                                // Only allowing Player to click
                                var atkedTriggerInNextFrame = nextRenderFrameTriggers[atkedTriggerColliderAttr.TriggerLocalId];
                                var triggerConfigFromTiled = atkedTrigger.ConfigFromTiled;
                                exploded = true;
                                atkedTriggerInNextFrame.Quota = atkedTrigger.Quota - 1;
                                atkedTriggerInNextFrame.FramesToFire = triggerConfigFromTiled.DelayedFrames;
                                atkedTriggerInNextFrame.FramesToRecover = triggerConfigFromTiled.RecoveryFrames;
                            }
                            break;
                        case CharacterDownsync atkedCharacterInCurrFrame:
                            if (bulletNextFrame.BattleAttr.OffenderJoinIndex == atkedCharacterInCurrFrame.JoinIndex) continue;
                            if (bulletNextFrame.BattleAttr.TeamId == atkedCharacterInCurrFrame.BulletTeamId) continue;
                            if (invinsibleSet.Contains(atkedCharacterInCurrFrame.CharacterState)) continue;
                            if (0 < atkedCharacterInCurrFrame.FramesInvinsible) continue;
                            exploded = true;
                            explodedOnAnotherCharacter = true;
                            //logger.LogWarn(String.Format("MeleeBullet with collider:[blx:{0}, bly:{1}, w:{2}, h:{3}], bullet:{8} exploded on bCollider: [blx:{4}, bly:{5}, w:{6}, h:{7}], atkedCharacterInCurrFrame: {9}", bulletCollider.X, bulletCollider.Y, bulletCollider.W, bulletCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, bullet, atkedCharacterInCurrFrame));
                            int atkedJ = atkedCharacterInCurrFrame.JoinIndex - 1;
                            var atkedCharacterInNextFrame = (atkedJ < roomCapacity ? nextRenderFramePlayers[atkedJ] : nextRenderFrameNpcs[atkedJ - roomCapacity]);
                            CharacterState oldNextCharacterState = atkedCharacterInNextFrame.CharacterState;
                            atkedCharacterInNextFrame.Hp -= bulletNextFrame.Config.Damage;
                            atkedCharacterInNextFrame.FramesCapturedByInertia = 0; // Being attacked breaks movement inertia.
                            if (0 >= atkedCharacterInNextFrame.Hp) {
                                // [WARNING] We don't have "dying in air" animation for now, and for better graphical recognition, play the same dying animation even in air
                                // If "atkedCharacterInCurrFrame" took multiple bullets in the same renderFrame, where a bullet in the middle of the set made it DYING, then all consecutive bullets would just take it into this small block again!
                                atkedCharacterInNextFrame.Hp = 0;
                                atkedCharacterInNextFrame.VelX = 0; // yet no need to change "VelY" because it could be falling
                                atkedCharacterInNextFrame.CharacterState = Dying;
                                atkedCharacterInNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
                            } else {
                                // [WARNING] Deliberately NOT assigning to "atkedCharacterInNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
                                var atkedCharacterConfig = characters[atkedCharacterInNextFrame.SpeciesId];
                                bool shouldOmitHitPushback = (atkedCharacterConfig.Hardness > bulletNextFrame.Config.Hardness);   
                                if (false == shouldOmitHitPushback && BlownUp1 != oldNextCharacterState) {
                                    var (pushbackVelX, pushbackVelY) = (xfac * bulletNextFrame.Config.PushbackVelX, bulletNextFrame.Config.PushbackVelY);
                                    // The traversal order of bullets is deterministic, thus the following assignment is deterministic regardless of the order of collision result popping.
                                    atkedCharacterInNextFrame.VelX = pushbackVelX;
                                    atkedCharacterInNextFrame.VelY = pushbackVelY;
                                }

                                bool shouldOmitStun = ((0 >= bulletNextFrame.Config.HitStunFrames) || shouldOmitHitPushback);
                                if (false == shouldOmitStun) {
                                    var existingDebuff = atkedCharacterInNextFrame.DebuffList[DEBUFF_ARR_IDX_FROZEN];
                                    bool isFrozen = (TERMINATING_DEBUFF_SPECIES_ID != existingDebuff.SpeciesId && 0 < existingDebuff.Stock && DebuffType.FrozenPositionLocked == debuffConfigs[existingDebuff.SpeciesId].Type); // [WARNING] It's important to check against TERMINATING_DEBUFF_SPECIES_ID such that we're safe from array reuse contamination
                                    if (!isFrozen && bulletNextFrame.Config.BlowUp) {
                                        atkedCharacterInNextFrame.CharacterState = BlownUp1;
                                    } else if (isFrozen || BlownUp1 != oldNextCharacterState) {
                                        if (isCrouching(atkedCharacterInNextFrame.CharacterState)) {
                                            atkedCharacterInNextFrame.CharacterState = CrouchAtked1;
                                        } else {
                                            atkedCharacterInNextFrame.CharacterState = Atked1;
                                        }
                                    }
                                    int oldFramesToRecover = atkedCharacterInNextFrame.FramesToRecover;
                                    if (bulletNextFrame.Config.HitStunFrames > oldFramesToRecover) {
                                        atkedCharacterInNextFrame.FramesToRecover = bulletNextFrame.Config.HitStunFrames;
                                    }
                                }
                                if (atkedCharacterInNextFrame.FramesInvinsible < bulletNextFrame.Config.HitInvinsibleFrames) {
                                    atkedCharacterInNextFrame.FramesInvinsible = bulletNextFrame.Config.HitInvinsibleFrames;
                                }

                                if (null != offender.BuffList) {
                                    for (int w = 0; w < offender.BuffList.Count; w++) {
                                        Buff buff = offender.BuffList[w];
                                        if (TERMINATING_BUFF_SPECIES_ID == buff.SpeciesId) break;   
                                        if (0 >= buff.Stock) continue;
                                        if (buff.OriginatedRenderFrameId > bulletNextFrame.BattleAttr.OriginatedRenderFrameId) continue;
                                        BuffConfig buffConfig = buffConfigs[buff.SpeciesId];
                                        if (null == buffConfig.AssociatedDebuffs) continue;  
                                        for (int q = 0; q < buffConfig.AssociatedDebuffs.Count; q++) {
                                            DebuffConfig associatedDebuffConfig = debuffConfigs[buffConfig.AssociatedDebuffs[q]];
                                            if (null == associatedDebuffConfig || TERMINATING_BUFF_SPECIES_ID == associatedDebuffConfig.SpeciesId) break;
                                            switch (associatedDebuffConfig.Type) {
                                                case DebuffType.ColdSpeedDown:
                                                case DebuffType.FrozenPositionLocked:
                                                    // Overwrite existing debuff for now
                                                    int debuffArrIdx = associatedDebuffConfig.ArrIdx;
                                                    AssignToDebuff(associatedDebuffConfig.SpeciesId, associatedDebuffConfig.Stock, atkedCharacterInNextFrame.DebuffList[debuffArrIdx]);
                                                    // The following transition is deterministic because we checked "atkedCharacterInNextFrame.DebuffList" before transiting into BlownUp1.
                                                    if (isCrouching(atkedCharacterInNextFrame.CharacterState)) {
                                                        atkedCharacterInNextFrame.CharacterState = CrouchAtked1;
                                                    } else {
                                                        atkedCharacterInNextFrame.CharacterState = Atked1;
                                                    }
                                                    switch (associatedDebuffConfig.StockType) {
                                                        case BuffStockType.Timed:
                                                            atkedCharacterInNextFrame.FramesToRecover = associatedDebuffConfig.Stock;
                                                            break;
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        case Bullet v4:
                            if (!COLLIDABLE_PAIRS.Contains(bulletNextFrame.Config.CollisionTypeMask | v4.Config.CollisionTypeMask)) {
                                break;
                            }
                            if (bulletNextFrame.BattleAttr.TeamId == v4.BattleAttr.TeamId) continue;
                            if (bulletNextFrame.Config.Hardness > v4.Config.Hardness) continue; 
                            exploded = true;
                            break;
                        case TrapColliderAttr v5:
                            if (!v5.ProvidesHardPushback) {
                                break;
                            }
                            if (!COLLIDABLE_PAIRS.Contains(bulletNextFrame.Config.CollisionTypeMask | COLLISION_BARRIER_INDEX_PREFIX)) {
                                break;
                            }
                            exploded = true;
                            break;
                        default:
                            if (!COLLIDABLE_PAIRS.Contains(bulletNextFrame.Config.CollisionTypeMask | COLLISION_BARRIER_INDEX_PREFIX)) {
                                break;
                            }
                            exploded = true;
                            break;
                    }
                }

                bool inTheMiddleOfMultihitTransition = false;
                if (MultiHitType.None != bulletNextFrame.Config.MhType) {
                    if (bulletNextFrame.BattleAttr.ActiveSkillHit + 1 < skillConfig.Hits.Count) {
                        inTheMiddleOfMultihitTransition = true;
                    }
                }

                if (exploded) {
                    if (BulletType.Melee == bulletNextFrame.Config.BType) {
                        bulletNextFrame.BlState = BulletState.Exploding;
                        if (explodedOnAnotherCharacter) {
                            bulletNextFrame.FramesInBlState = 0;
                        } else {
                            // When hitting a barrier, don't play explosion anim
                            bulletNextFrame.FramesInBlState = bulletNextFrame.Config.ExplosionFrames + 1;
                        }
                    } else if (BulletType.Fireball == bulletNextFrame.Config.BType) {
                        if (BulletState.Exploding != bulletNextFrame.BlState) {
                            bulletNextFrame.BlState = BulletState.Exploding;
                            bulletNextFrame.FramesInBlState = 0;
                        }
                        if (inTheMiddleOfMultihitTransition) {
                            bool dummyHasLockVel = false;
                            if (addNewBulletToNextFrame(currRenderFrame.Id, offender, offenderNextFrame, xfac, skillConfig, nextRenderFrameBullets, bulletNextFrame.BattleAttr.ActiveSkillHit+1, bulletNextFrame.BattleAttr.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel, bulletNextFrame)) {
                                var targetNewBullet = nextRenderFrameBullets[bulletCnt-1];
                                if (offenderNextFrame.FramesInvinsible < targetNewBullet.Config.StartupInvinsibleFrames) {
                                    offenderNextFrame.FramesInvinsible = targetNewBullet.Config.StartupInvinsibleFrames;
                                }
                                // TODO: Support "MultiHitType.FromPrevHitAnyway"
                            }
                        }
                    } else {
                        // Nothing to do
                    }
                } else {
                    if (BulletType.Fireball == bulletNextFrame.Config.BType && SPEED_NOT_HIT_NOT_SPECIFIED != bulletNextFrame.Config.SpeedIfNotHit && bulletNextFrame.Config.Speed != bulletNextFrame.Config.SpeedIfNotHit) {
                        var bulletDirMagSq = bulletNextFrame.Config.DirX * bulletNextFrame.Config.DirX + bulletNextFrame.Config.DirY * bulletNextFrame.Config.DirY;
                        var invBulletDirMag = InvSqrt32(bulletDirMagSq);
                        var bulletSpeedXfac = xfac * invBulletDirMag * bulletNextFrame.Config.DirX;
                        var bulletSpeedYfac = invBulletDirMag * bulletNextFrame.Config.DirY;
                        bulletNextFrame.VelX = (int)(bulletSpeedXfac * bulletNextFrame.Config.SpeedIfNotHit);
                        bulletNextFrame.VelY = (int)(bulletSpeedYfac * bulletNextFrame.Config.SpeedIfNotHit);
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

        private static void _tickSingleSubCycle(RoomDownsyncFrame currRenderFrame, Trigger currTrigger, Trigger triggerInNextFrame, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, ref ulong nextWaveNpcKilledEvtMaskCounter) {
            if (0 < currTrigger.SubCycleQuotaLeft) {
                triggerInNextFrame.SubCycleQuotaLeft = currTrigger.SubCycleQuotaLeft - 1;
                
                var chSpawnerConfig = (TRIGGER_MASK_BY_SUBSCRIPTION == currTrigger.Config.TriggerMask ? lowerBoundForSpawnerConfig(currTrigger.ConfigFromTiled.QuotaCap - currTrigger.Quota, currTrigger.ConfigFromTiled.CharacterSpawnerTimeSeq) : lowerBoundForSpawnerConfig(currRenderFrame.Id, currTrigger.ConfigFromTiled.CharacterSpawnerTimeSeq));  
                var spawnerSpeciesIdList = chSpawnerConfig.SpeciesIdList;
                if (0 < spawnerSpeciesIdList.Count) {
                    int idx = currTrigger.ConfigFromTiled.SubCycleQuota - triggerInNextFrame.SubCycleQuotaLeft -1;
                    if (TRIGGER_MASK_BY_SUBSCRIPTION == currTrigger.Config.TriggerMask && (idx < 0 || idx >= spawnerSpeciesIdList.Count)) return;
                    if (idx < 0) idx = 0;
                    if (idx >= spawnerSpeciesIdList.Count) idx = spawnerSpeciesIdList.Count-1;
                    ulong candNextWaveNpcKilledEvtMaskCounter = (0 == nextWaveNpcKilledEvtMaskCounter ? 1 : (nextWaveNpcKilledEvtMaskCounter << 1));
                    // [WARNING] Trigger-spawned NPCs wouldn't subscribe to any evtsub for initial movement.
                    if (addNewNpcToNextFrame(currTrigger.VirtualGridX, currTrigger.VirtualGridY, currTrigger.ConfigFromTiled.InitVelX, currTrigger.ConfigFromTiled.InitVelY, spawnerSpeciesIdList[idx], currTrigger.BulletTeamId, false, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt, MAGIC_EVTSUB_ID_WAVER, candNextWaveNpcKilledEvtMaskCounter, MAGIC_EVTSUB_ID_NONE)) {
                        nextWaveNpcKilledEvtMaskCounter = candNextWaveNpcKilledEvtMaskCounter;
                        triggerInNextFrame.State = TriggerState.TcoolingDown;
                        triggerInNextFrame.FramesInState = 0;
                        triggerInNextFrame.FramesToFire = triggerInNextFrame.ConfigFromTiled.SubCycleTriggerFrames;
                    }
                }
            } else {
                // Wait for "FramesToRecover" to become 0
                triggerInNextFrame.State = TriggerState.Tready;
                triggerInNextFrame.SubCycleQuotaLeft = currTrigger.ConfigFromTiled.SubCycleQuota; // Refill to be fired by the next "0 == currTrigger.FramesToRecover"
                triggerInNextFrame.FramesToFire = MAX_INT;
            }
        }

        private static void _calcTriggerReactions(RoomDownsyncFrame currRenderFrame, RoomDownsyncFrame nextRenderFrame, int roomCapacity, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Trigger> nextRenderFrameTriggers, Dictionary<int, int> triggerTrackingIdToTrapLocalId, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, ref ulong nextWaveNpcKilledEvtMaskCounter, EvtSubscription currRdfWaveNpcKilledEvtSub, EvtSubscription nextRdfWaveNpcKilledEvtSub, ref ulong fulfilledEvtSubscriptionSetMask, int[] justFulfilledEvtSubArr, ref int justFulfilledEvtSubCnt, ILoggerBridge logger) {
            bool nextWaveTriggerJustFulfilled = (
                                                    EVTSUB_NO_DEMAND_MASK != currRdfWaveNpcKilledEvtSub.DemandedEvtMask 
                                                    && 
                                                    (
                                                        currRdfWaveNpcKilledEvtSub.FulfilledEvtMask == currRdfWaveNpcKilledEvtSub.DemandedEvtMask
                                                        ||
                                                        (0 < (fulfilledEvtSubscriptionSetMask & (1ul << (currRdfWaveNpcKilledEvtSub.Id - 1)))) // In this case, "currRdfWaveNpcKilledEvtSub" might've been marked as "EVTSUB_NO_DEMAND_MASK == .DemandedEvtMask == .FulfilledEvtMask"
                                                    )); ;
            int nextWaveNpcCnt = 0;
            for (int i = 0; i < currRenderFrame.TriggersArr.Count; i++) {
                var currTrigger = currRenderFrame.TriggersArr[i];
                if (TERMINATING_TRIGGER_ID == currTrigger.TriggerLocalId) break;
                var triggerInNextFrame = nextRenderFrameTriggers[i];
                if (TRIGGER_MASK_BY_CYCLIC_TIMER == currTrigger.Config.TriggerMask) {
                    // The ORDER of zero checks of "currTrigger.FramesToRecover" and "currTrigger.FramesToFire" below is important, because we want to avoid "wrong SubCycleQuotaLeft replenishing when 0 == currTrigger.FramesToRecover"!
                    if (0 == currTrigger.FramesToFire) {
                        _tickSingleSubCycle(currRenderFrame, currTrigger, triggerInNextFrame, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt, ref nextWaveNpcKilledEvtMaskCounter);
                    } else if (0 == currTrigger.FramesToRecover) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.Quota = currTrigger.Quota - 1;
                            triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                            _tickSingleSubCycle(currRenderFrame, currTrigger, triggerInNextFrame, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt, ref nextWaveNpcKilledEvtMaskCounter);
                        } else {
                            triggerInNextFrame.State = TriggerState.Tready;
                            triggerInNextFrame.FramesToFire = MAX_INT;
                            triggerInNextFrame.FramesToRecover = MAX_INT;
                        }
                    }
                } else if (TRIGGER_MASK_BY_SUBSCRIPTION == currTrigger.Config.TriggerMask) {
                    EvtSubscription triggerSubscribedInstance = currRenderFrame.EvtSubsArr[currTrigger.ConfigFromTiled.SubscriptionId - 1];
                    bool fulfilled = (
                        EVTSUB_NO_DEMAND_MASK != triggerSubscribedInstance.DemandedEvtMask 
                        && 
                        (
                            triggerSubscribedInstance.FulfilledEvtMask == triggerSubscribedInstance.DemandedEvtMask 
                            || 
                            (0 < (fulfilledEvtSubscriptionSetMask & (1ul << (currTrigger.ConfigFromTiled.SubscriptionId - 1)))) // In this case, "triggerSubscribedInstance" might've been marked as "EVTSUB_NO_DEMAND_MASK == .DemandedEvtMask == .FulfilledEvtMask"
                        )
                    );
                    if (fulfilled && 0 < currTrigger.Quota) {
                        triggerInNextFrame.Quota = currTrigger.Quota - 1;
                        triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                        triggerInNextFrame.FramesToFire = currTrigger.ConfigFromTiled.DelayedFrames;
                        if (currTrigger.ConfigFromTiled.SubscriptionId == nextRdfWaveNpcKilledEvtSub.Id) {
                            var chSpawnerConfig = lowerBoundForSpawnerConfig(triggerInNextFrame.ConfigFromTiled.QuotaCap - triggerInNextFrame.Quota, triggerInNextFrame.ConfigFromTiled.CharacterSpawnerTimeSeq);
                            nextWaveNpcCnt += (chSpawnerConfig.SpeciesIdList.Count < triggerInNextFrame.ConfigFromTiled.SubCycleQuota ? chSpawnerConfig.SpeciesIdList.Count : triggerInNextFrame.ConfigFromTiled.SubCycleQuota);
                            nextWaveNpcKilledEvtMaskCounter = 0;
                        }
                    } else if (0 <= currTrigger.FramesToFire && MAX_INT != currTrigger.FramesToFire) {
                        // [WARNING] The information of "justFulfilled" will be lost after then just-fulfilled renderFrame, thus temporarily using "FramesToFire" to keep track of subsequent spawning
                        if (0 == currTrigger.FramesToFire) {
                            _tickSingleSubCycle(currRenderFrame, currTrigger, triggerInNextFrame, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt, ref nextWaveNpcKilledEvtMaskCounter);
                        }
                    } else {
                        triggerInNextFrame.State = TriggerState.Tready;
                        triggerInNextFrame.FramesToFire = MAX_INT;
                        triggerInNextFrame.FramesToRecover = MAX_INT;
                        if (fulfilled) {
                            var nextExhaustEvtSub = nextRenderFrame.EvtSubsArr[currTrigger.ConfigFromTiled.PublishingToEvtSubIdUponExhaust-1];
                            nextExhaustEvtSub.FulfilledEvtMask |= currTrigger.ConfigFromTiled.PublishingEvtMaskUponExhaust;
                            if (nextExhaustEvtSub.DemandedEvtMask == nextExhaustEvtSub.FulfilledEvtMask) {
                                fulfilledEvtSubscriptionSetMask |= (1ul << (nextExhaustEvtSub.Id-1));
                                nextExhaustEvtSub.DemandedEvtMask = EVTSUB_NO_DEMAND_MASK;
                                nextExhaustEvtSub.FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK;
                                justFulfilledEvtSubArr[justFulfilledEvtSubCnt++] = nextExhaustEvtSub.Id;
                            }
                        }
                    }
                } else {
                    if (0 != currTrigger.FramesToFire) continue;
                    var configFromTiled = currTrigger.ConfigFromTiled;
                    var trackingIdList = currTrigger.ConfigFromTiled.TrackingIdList;
                    foreach (int trackingId in trackingIdList) {
                        if (triggerTrackingIdToTrapLocalId.ContainsKey(trackingId)) {
                            int trapLocalId = triggerTrackingIdToTrapLocalId[trackingId];
                            var trapInNextFrame = nextRenderFrameTraps[trapLocalId];
                            trapInNextFrame.VelX = configFromTiled.InitVelX;
                            trapInNextFrame.VelY = configFromTiled.InitVelY;
                            trapInNextFrame.CapturedByPatrolCue = false; // [WARNING] Important to help this trap escape its currently capturing PatrolCue!
                            var dirMagSq = configFromTiled.InitVelX * configFromTiled.InitVelX + configFromTiled.InitVelY * configFromTiled.InitVelY;
                            var invDirMag = InvSqrt32(dirMagSq);
                            var speedXfac = invDirMag * configFromTiled.InitVelX;
                            var speedYfac = invDirMag * configFromTiled.InitVelY;
                            var speedVal = trapInNextFrame.ConfigFromTiled.Speed;
                            trapInNextFrame.VelX = (int)(speedXfac * speedVal);
                            trapInNextFrame.VelY = (int)(speedYfac * speedVal);
                        }
                    }
                    triggerInNextFrame.FramesToFire = MAX_INT;
                }
            }

            if (nextWaveTriggerJustFulfilled) {
                fulfilledEvtSubscriptionSetMask |= (1ul << (nextRdfWaveNpcKilledEvtSub.Id - 1));
                nextRdfWaveNpcKilledEvtSub.DemandedEvtMask = ((1ul << nextWaveNpcCnt) - 1);
                nextRdfWaveNpcKilledEvtSub.FulfilledEvtMask = 0;
                justFulfilledEvtSubArr[justFulfilledEvtSubCnt++] = nextRdfWaveNpcKilledEvtSub.Id;
            }
        }

        private static void _calcFallenDeath(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ILoggerBridge logger) {
            for (int i = 0; i < roomCapacity + currNpcI; i++) {
                var currCharacterDownsync = (i < roomCapacity ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (i >= roomCapacity && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < roomCapacity ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
                var chConfig = characters[currCharacterDownsync.SpeciesId];

                float characterVirtualGridTop = currCharacterDownsync.VirtualGridY + (chConfig.DefaultSizeY >> 1);
                if (0 > characterVirtualGridTop && Dying != currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.Hp = 0;
                    thatCharacterInNextFrame.VelX = 0;
                    thatCharacterInNextFrame.CharacterState = Dying;
                    thatCharacterInNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
                }
            }
        }

        private static void _processEffPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, Vector[] effPushbacks, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, int colliderCnt, ILoggerBridge logger) {
            for (int i = 0; i < roomCapacity + currNpcI; i++) {
                var currCharacterDownsync = (i < roomCapacity ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                var thatCharacterInNextFrame = (i < roomCapacity ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                Collider aCollider = dynamicRectangleColliders[i];
                // Update "virtual grid position"
                (thatCharacterInNextFrame.VirtualGridX, thatCharacterInNextFrame.VirtualGridY) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                /*
                   if (0 == currCharacterDownsync.SpeciesId) {
                   logger.LogInfo(String.Format("Will move to nextChState={0}, nextVy={1}: effPushback={2}: from chState={3}, vy={4}", thatCharacterInNextFrame.CharacterState, thatCharacterInNextFrame.VirtualGridY, effPushbacks[i].ToString(), currCharacterDownsync.CharacterState, currCharacterDownsync.VirtualGridY));
                   }
                 */
                // Update "CharacterState"
                /*
                TODO: Implement transition into "Crouching CharacterStates"
                - CharacterState.CrouchIdle1
                - CharacterState.CrouchWalking
                - CharacterState.CrouchAtk1 
                - CharacterState.CrouchAtked1
                by inspecting field "CharacterDownsync.Crouching", where "CharacterDownsync.Crouching" is set during "calcHardPushbacksNormsForCharacter(......)" by checking collision with a special type of "Trap" where "Trap.IsCompletelyStatic && Trap.PushToCrouchIfOnTop".
                */
                if (thatCharacterInNextFrame.InAir) {
                    /*
                       if (0 == currCharacterDownsync.SpeciesId && false == currCharacterDownsync.InAir) {
                       logger.LogInfo(String.Format("Rdf.id={0}, chState={1}, framesInChState={6}, velX={2}, velY={3}, virtualGridX={4}, virtualGridY={5}: transitted to InAir", currRenderFrame.Id, currCharacterDownsync.CharacterState, currCharacterDownsync.VelX, currCharacterDownsync.VelY, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, currCharacterDownsync.FramesInChState));
                       }
                     */
                    CharacterState oldNextCharacterState = thatCharacterInNextFrame.CharacterState;
                    if (!inAirSet.Contains(oldNextCharacterState)) {
                        switch (oldNextCharacterState) {
                            case Idle1:
                            case Walking:
                            case TurnAround:
                                if ((currCharacterDownsync.OnWall && currCharacterDownsync.JumpTriggered) || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
                                } else if ((!currCharacterDownsync.OnWall && currCharacterDownsync.JumpTriggered) || InAirIdle1ByJump == currCharacterDownsync.CharacterState) {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                                } else {
                                    thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
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
                    CharacterState oldNextCharacterState = thatCharacterInNextFrame.CharacterState;
                    if (inAirSet.Contains(oldNextCharacterState) && BlownUp1 != oldNextCharacterState && OnWallIdle1 != oldNextCharacterState && Dashing != oldNextCharacterState) {
                        switch (oldNextCharacterState) {
                            case InAirIdle1NoJump:
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                            case InAirIdle1ByJump:
                            case InAirIdle1ByWallJump:
                                if ( isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame) || isInJumpStartup(thatCharacterInNextFrame) ) {
                                    // [WARNING] Don't change CharacterState in this special case!
                                    break;
                                }
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                            case InAirAtked1:
                                thatCharacterInNextFrame.CharacterState = Atked1;
                                break;
                            default:
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                        }
                    } else if (thatCharacterInNextFrame.ForcedCrouching && chConfig.CrouchingEnabled) {
                        if (!isCrouching(thatCharacterInNextFrame.CharacterState)) {
                            switch (thatCharacterInNextFrame.CharacterState) {
                                case Idle1:
                                case InAirIdle1ByJump:
                                case InAirIdle1NoJump:
                                case InAirIdle1ByWallJump:
                                case Walking:
                                case GetUp1:
                                case TurnAround:
                                    thatCharacterInNextFrame.CharacterState = CrouchIdle1;
                                    break;
                                case Atk1:
                                case Atk2:
                                    thatCharacterInNextFrame.CharacterState = CrouchAtk1;
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
                }

                if (thatCharacterInNextFrame.OnWall) {
                    switch (thatCharacterInNextFrame.CharacterState) {
                        case Walking:
                        case InAirIdle1NoJump:
                        case InAirIdle1ByJump:
                        case InAirIdle1ByWallJump:
                            bool hasBeenOnWallChState = (OnWallIdle1 == currCharacterDownsync.CharacterState);
                            bool hasBeenOnWallCollisionResultForSameChState = (chConfig.OnWallEnabled && currCharacterDownsync.OnWall && MAGIC_FRAMES_TO_BE_ON_WALL <= thatCharacterInNextFrame.FramesInChState);
                            if (!isInJumpStartup(thatCharacterInNextFrame) && !isJumpStartupJustEnded(currCharacterDownsync, thatCharacterInNextFrame) && (hasBeenOnWallChState || hasBeenOnWallCollisionResultForSameChState)) {
                                thatCharacterInNextFrame.CharacterState = OnWallIdle1;
                            }
                            break;
                    }
                }

                // Reset "FramesInChState" if "CharacterState" is changed
                if (thatCharacterInNextFrame.CharacterState != currCharacterDownsync.CharacterState) {
                    if (Walking == currCharacterDownsync.CharacterState && (WalkingAtk1 == thatCharacterInNextFrame.CharacterState || WalkingAtk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.LowerPartFramesInChState = currCharacterDownsync.LowerPartFramesInChState + 1;
                        thatCharacterInNextFrame.FramesInChState = 0;
                    } else if ((WalkingAtk1 == currCharacterDownsync.CharacterState || WalkingAtk4 == currCharacterDownsync.CharacterState) && Walking == thatCharacterInNextFrame.CharacterState) {
                        thatCharacterInNextFrame.LowerPartFramesInChState = currCharacterDownsync.LowerPartFramesInChState + 1;
                        thatCharacterInNextFrame.FramesInChState = currCharacterDownsync.LowerPartFramesInChState + 1;
                    } else if ((Atk1 == currCharacterDownsync.CharacterState && WalkingAtk1 == thatCharacterInNextFrame.CharacterState) || (Atk4 == currCharacterDownsync.CharacterState && WalkingAtk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.FramesInChState = currCharacterDownsync.FramesInChState + 1;
                        thatCharacterInNextFrame.LowerPartFramesInChState = 0;
                    } else if ((WalkingAtk1 == thatCharacterInNextFrame.CharacterState && Atk1 == thatCharacterInNextFrame.CharacterState) || (WalkingAtk4 == thatCharacterInNextFrame.CharacterState && Atk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.FramesInChState = currCharacterDownsync.FramesInChState + 1;
                        thatCharacterInNextFrame.LowerPartFramesInChState = 0;
                    } else if (SPECIES_GUNGIRL == thatCharacterInNextFrame.SpeciesId && (Atk1 == thatCharacterInNextFrame.CharacterState || Atk4 == thatCharacterInNextFrame.CharacterState)) {
                        thatCharacterInNextFrame.FramesInChState = 0;
                        thatCharacterInNextFrame.LowerPartFramesInChState = 0;
                    } else {
                        thatCharacterInNextFrame.FramesInChState = 0;
                        thatCharacterInNextFrame.LowerPartFramesInChState = INVALID_FRAMES_IN_CH_STATE; // not showing isolated lower part in other ChStates
                    }
                } else {
                    switch (thatCharacterInNextFrame.CharacterState) {
                        case Walking:
                        case WalkingAtk1:
                        case WalkingAtk4:
                        case Atk1:
                        case Atk4:
                            if (INVALID_FRAMES_IN_CH_STATE == thatCharacterInNextFrame.LowerPartFramesInChState) {
                                thatCharacterInNextFrame.LowerPartFramesInChState = 0;
                            }
                            break;
                        default:
                            thatCharacterInNextFrame.LowerPartFramesInChState = INVALID_FRAMES_IN_CH_STATE; // not showing isolated lower part in other ChStates
                            break;
                    }
                }
                thatCharacterInNextFrame.PrevWasCrouching = isCrouching(currCharacterDownsync.CharacterState);

                // Remove any active skill if not attacking
                if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState) && Dashing != thatCharacterInNextFrame.CharacterState) {
                    thatCharacterInNextFrame.ActiveSkillId = NO_SKILL;
                    thatCharacterInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                }

                if (Atked1 == thatCharacterInNextFrame.CharacterState && (MAX_INT >> 1) < thatCharacterInNextFrame.FramesToRecover) {
                    logger.LogWarn(String.Format("thatCharacterInNextFrame has invalid frameToRecover={0} and chState={1}! Re-assigning characterState to BlownUp1 for recovery!", thatCharacterInNextFrame.FramesToRecover, thatCharacterInNextFrame.CharacterState));
                    thatCharacterInNextFrame.CharacterState = BlownUp1;
                }
            }

            for (int i = trapColliderCntOffset; i < bulletColliderCntOffset; i++) {
                var aCollider = dynamicRectangleColliders[i];
                TrapColliderAttr? colliderAttr = aCollider.Data as TrapColliderAttr;
                if (null == colliderAttr) {
                    throw new ArgumentNullException("Data field shouldn't be null for dynamicRectangleColliders[i=" + i + "], where trapColliderCntOffset=" + trapColliderCntOffset + ", bulletColliderCntOffset=" + bulletColliderCntOffset);
                }

                // Update "virtual grid position"
                var trapInNextRenderFrame = nextRenderFrameTraps[colliderAttr.TrapLocalId];
                int nextColliderAttrVx, nextColliderAttrVy;
                if (colliderAttr.ProvidesHardPushback) {
                    (nextColliderAttrVx, nextColliderAttrVy) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                } else {
                    (nextColliderAttrVx, nextColliderAttrVy) = PolygonColliderBLToVirtualGridPos(aCollider.X, aCollider.Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                }
                trapInNextRenderFrame.VirtualGridX = nextColliderAttrVx - colliderAttr.HitboxOffsetX;
                trapInNextRenderFrame.VirtualGridY = nextColliderAttrVy - colliderAttr.HitboxOffsetY;
            }
        }

        private static void _leftShiftDeadNpcs(int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<EvtSubscription> nextRdfEvtSubsArr, EvtSubscription waveNpcKilledEvtSub, ref ulong fulfilledEvtSubscriptionSetMask, Dictionary<int, int> joinIndexRemap, out bool isRemapNeeded, ref int nextNpcI, ILoggerBridge logger) {
            isRemapNeeded = false;
            int aliveSlotI = 0, candidateI = 0;
            while (candidateI < nextRenderFrameNpcs.Count && TERMINATING_PLAYER_ID != nextRenderFrameNpcs[candidateI].Id) {
                while (candidateI < nextRenderFrameNpcs.Count && TERMINATING_PLAYER_ID != nextRenderFrameNpcs[candidateI].Id && isNpcDeadToDisappear(nextRenderFrameNpcs[candidateI])) {
                    if (MAGIC_EVTSUB_ID_NONE != nextRenderFrameNpcs[candidateI].PublishingEvtSubIdUponKilled) {
                        UpdateWaveNpcKilledEvtSub(nextRenderFrameNpcs[candidateI].PublishingEvtMaskUponKilled, nextRdfEvtSubsArr[nextRenderFrameNpcs[candidateI].PublishingEvtSubIdUponKilled-1], ref fulfilledEvtSubscriptionSetMask);
                    } else {
                        UpdateWaveNpcKilledEvtSub(nextRenderFrameNpcs[candidateI].PublishingEvtMaskUponKilled, waveNpcKilledEvtSub, ref fulfilledEvtSubscriptionSetMask);
                    }
                    candidateI++;
                }
                if (candidateI >= nextRenderFrameNpcs.Count || TERMINATING_PLAYER_ID == nextRenderFrameNpcs[candidateI].Id) {
                    break;
                }
                if (aliveSlotI != candidateI && !isRemapNeeded) {
                    isRemapNeeded = true;
                    joinIndexRemap.Clear();
                }
                
                var src = nextRenderFrameNpcs[candidateI];
                var dst = nextRenderFrameNpcs[aliveSlotI];
                int newJoinIndex = roomCapacity + aliveSlotI + 1;

                if (isRemapNeeded) {
                    joinIndexRemap[src.JoinIndex] = newJoinIndex;
                }
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FramesToRecover, src.FramesInChState, src.ActiveSkillId, src.ActiveSkillHit, src.FramesInvinsible, src.Speed, src.CharacterState, newJoinIndex, src.Hp, src.MaxHp, src.InAir, src.OnWall, src.OnWallNormX, src.OnWallNormY, src.FramesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, src.PrimarilyOnSlippableHardPushback, src.CapturedByPatrolCue, src.FramesInPatrolCue, src.BeatsCnt, src.BeatenCnt, src.Mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, src.OnSlope, src.ForcedCrouching, src.NewBirth, src.LowerPartFramesInChState, src.JumpStarted, src.FramesToStartJump, src.BuffList, src.DebuffList, src.Inventory, false, src.PublishingEvtSubIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscriptionId, dst);
                candidateI++;
                aliveSlotI++;
            }
            if (aliveSlotI < nextRenderFrameNpcs.Count) {
                nextRenderFrameNpcs[aliveSlotI].Id = TERMINATING_PLAYER_ID;
            }
            nextNpcI = aliveSlotI;
        }

        private static void remapBulletOffenderJoinIndex(int roomCapacity, int npcCnt, RepeatedField<Bullet> nextRdfBullets, Dictionary<int, int> joinIndexRemap) {
            for (int i = 0; i < nextRdfBullets.Count; i++) {
                var src = nextRdfBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BattleAttr.BulletLocalId) break;
                int j = src.BattleAttr.OffenderJoinIndex;
                if (j < roomCapacity) continue; // no need to remap for players
                if (!joinIndexRemap.ContainsKey(j)) continue;
                src.BattleAttr.OffenderJoinIndex = joinIndexRemap[j];
            }
        }

        public static bool isBattleResultSet(BattleResult battleResult) {
            return (MAGIC_JOIN_INDEX_DEFAULT != battleResult.WinnerJoinIndex);
        }

        public static void resetBattleResult(ref BattleResult battleResult) {
            battleResult.WinnerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT;
        }

        /*
        [TODO] 

        The "Step" function has become way more complicated than what it was back in the days only simple movements and hardpushbacks were supported. 
        
        Someday in the future, profiling result on low-end hardware might complain that this function is taking too much time in the "Script" portion, thus need one or all of the following optimization techniques to help it go further.
        - Make use of CPU parallelization -- better by using some libraries with sub-kernel-thread granularity(e.g. Goroutine or Greenlet equivalent) -- or GPU parallelization. It's not trivial to make an improvement because by dispatching smaller tasks to other resources other than the current kernel-thread, overhead I/O and synchronization/locking time is introduced. Moreover, we need guarantee that the dispatched smaller tasks can yield deterministic outputs regardless of processing order, e.g. that each "i" in "_calcCharacterMovementPushbacks" can be traversed earlier than another and same "effPushbacks" for the next render frame is obtained.   
        - Enable "IL2CPP" when building client application.  
        */
        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPushbackEnabled, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder, FrameRingBuffer<Collider> residueCollided, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, Dictionary<int, int> triggerTrackingIdToTrapLocalId, List<Collider> completelyStaticTrapColliders, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, bool pushbackFrameLogEnabled, int playingRdfId, bool shouldDetectRealtimeRenderHistoryCorrection, out bool hasIncorrectlyPredictedRenderFrame, RoomDownsyncFrame historyRdfHolder, int[] justFulfilledEvtSubArr, ref int justFulfilledEvtSubCnt, int missionEvtSubId, int selfPlayerJoinIndex, Dictionary<int, int> joinIndexRemap, ref int justTriggeredStoryPointId, ILoggerBridge logger) {
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
                AssignToRdfDeep(candidate, historyRdfHolder, roomCapacity);
            }
            // [WARNING] On backend this function MUST BE called while "InputsBufferLock" is locked!
            var nextRenderFramePlayers = candidate.PlayersArr;
            var nextRenderFrameNpcs = candidate.NpcsArr;
            var nextRenderFrameBullets = candidate.Bullets;
            int nextRenderFrameBulletLocalIdCounter = currRenderFrame.BulletLocalIdCounter;
            int nextRenderFrameNpcLocalIdCounter = currRenderFrame.NpcLocalIdCounter;
            var nextRenderFrameTraps = candidate.TrapsArr;
            var nextRenderFrameTriggers = candidate.TriggersArr;
            var nextEvtSubs = candidate.EvtSubsArr; 
            // Make a copy first
            for (int i = 0; i < roomCapacity; i++) {
                var src = currRenderFrame.PlayersArr[i];
                var chConfig = characters[src.SpeciesId];
                int framesToRecover = src.FramesToRecover - 1;
                if (0 > framesToRecover) {
                    framesToRecover = 0;
                }
                int framesCapturedByInertia = src.FramesCapturedByInertia - 1; 
                if (0 > framesCapturedByInertia) {
                    framesCapturedByInertia = 0;
                }
                int framesInChState = src.FramesInChState + 1;
                int lowerPartFramesInChState = (INVALID_FRAMES_IN_CH_STATE == src.LowerPartFramesInChState ? INVALID_FRAMES_IN_CH_STATE : src.LowerPartFramesInChState + 1);
                int framesInvinsible = src.FramesInvinsible - 1;
                if (0 > framesInvinsible) {
                    framesInvinsible = 0;
                }
                int framesInPatrolCue = src.FramesInPatrolCue - 1;
                if (0 > framesInPatrolCue) {
                    framesInPatrolCue = 0;
                }
                int mp = src.Mp + chConfig.MpRegenRate;
                if (mp >= src.MaxMp) {
                    mp = src.MaxMp;
                }
                int framesToStartJump = src.FramesToStartJump - 1;
                if (0 > framesToStartJump) {
                    framesToStartJump = 0;
                } 
                var dst = nextRenderFramePlayers[i];
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, 0, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, true, false, src.OnWallNormX, src.OnWallNormY, framesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, false, false, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, false, false, false, lowerPartFramesInChState, false, framesToStartJump, src.BuffList, src.DebuffList, src.Inventory, true, src.PublishingEvtSubIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscriptionId, dst);
                _resetVelocityOnRecovered(src, dst);
            }

            int currNpcI = 0;
            while (currNpcI < currRenderFrame.NpcsArr.Count && TERMINATING_PLAYER_ID != currRenderFrame.NpcsArr[currNpcI].Id) {
                var src = currRenderFrame.NpcsArr[currNpcI];
                var chConfig = characters[src.SpeciesId];
                int framesToRecover = src.FramesToRecover - 1;
                if (0 > framesToRecover) {
                    framesToRecover = 0;
                }
                int framesInChState = src.FramesInChState + 1;
                int lowerPartFramesInChState = (INVALID_FRAMES_IN_CH_STATE == src.LowerPartFramesInChState ? INVALID_FRAMES_IN_CH_STATE : src.LowerPartFramesInChState + 1);
                int framesCapturedByInertia = src.FramesCapturedByInertia - 1; 
                if (0 > framesCapturedByInertia) {
                    framesCapturedByInertia = 0;
                }
                int framesInvinsible = src.FramesInvinsible - 1;
                if (0 > framesInvinsible) {
                    framesInvinsible = 0;
                }
                int framesInPatrolCue = src.FramesInPatrolCue - 1;
                if (0 > framesInPatrolCue) {
                    framesInPatrolCue = 0;
                }
                int mp = src.Mp + chConfig.MpRegenRate;
                if (mp >= src.MaxMp) {
                    mp = src.MaxMp;
                }
                int framesToStartJump = src.FramesToStartJump - 1;
                if (0 > framesToStartJump) {
                    framesToStartJump = 0;
                } 
                var dst = nextRenderFrameNpcs[currNpcI];
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, 0, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, true, false, src.OnWallNormX, src.OnWallNormY, framesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, false, false, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, false, false, false, lowerPartFramesInChState, false, framesToStartJump, src.BuffList, src.DebuffList, src.Inventory, true, src.PublishingEvtSubIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscriptionId, dst);
                _resetVelocityOnRecovered(src, dst);
                currNpcI++;
            }
            nextRenderFrameNpcs[currNpcI].Id = TERMINATING_PLAYER_ID; // [WARNING] This is a CRITICAL assignment because "renderBuffer" is a ring, hence when cycling across "renderBuffer.StFrameId", we must ensure that the trailing NPCs existed from the startRdf wouldn't contaminate later calculation

            int currEvtSubI = 0;
            while (currEvtSubI < currRenderFrame.EvtSubsArr.Count && TERMINATING_EVTSUB_ID != currRenderFrame.EvtSubsArr[currEvtSubI].Id) {
                var src = currRenderFrame.EvtSubsArr[currEvtSubI];
                AssignToEvtSubscription(src.Id, src.DemandedEvtMask, src.FulfilledEvtMask, nextEvtSubs[currEvtSubI]);
                currEvtSubI++;
            } 

            int k = 0;
            while (k < currRenderFrame.TrapsArr.Count && TERMINATING_TRAP_ID != currRenderFrame.TrapsArr[k].TrapLocalId) {
                var src = currRenderFrame.TrapsArr[k];
                int framesInTrapState = src.FramesInTrapState + 1;
                int framesInPatrolCue = src.FramesInPatrolCue - 1;
                if (framesInPatrolCue < 0) {
                    framesInPatrolCue = 0;
                }
                AssignToTrap(src.TrapLocalId, src.Config, src.ConfigFromTiled, src.TrapState, framesInTrapState, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.VelY, src.IsCompletelyStatic, src.CapturedByPatrolCue, framesInPatrolCue, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, nextRenderFrameTraps[k]);
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
                AssignToTrigger(src.TriggerLocalId, framesToFire, framesToRecover, src.Quota, src.BulletTeamId, src.SubCycleQuotaLeft, src.State, framesInState, src.VirtualGridX, src.VirtualGridY, src.Config, src.ConfigFromTiled, nextRenderFrameTriggers[l]);
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
            justFulfilledEvtSubCnt = 0;
            int colliderCnt = 0, bulletCnt = 0;
            _processPlayerInputs(currRenderFrame, roomCapacity, inputBuffer, nextRenderFramePlayers, nextRenderFrameBullets, decodedInputHolder, prevDecodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _moveAndInsertCharacterColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, 0, roomCapacity + currNpcI, logger);
            _processNpcInputs(currRenderFrame, roomCapacity, currNpcI, nextRenderFrameNpcs, nextRenderFrameBullets, dynamicRectangleColliders, colliderCnt, collision, collisionSys, ref overlapResult, decodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            int trapColliderCntOffset = colliderCnt;
            _moveAndInsertDynamicTrapColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFrameTraps, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, trapColliderCntOffset, trapLocalIdToColliderAttrs, logger);
            ulong fulfilledEvtSubscriptionSetMask = 0; // By default no EvtSub is fulfilled yet
            _calcCharacterMovementPushbacks(currRenderFrame, roomCapacity, currNpcI, inputBuffer, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTriggers, nextEvtSubs, ref fulfilledEvtSubscriptionSetMask, justFulfilledEvtSubArr, ref justFulfilledEvtSubCnt, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, 0, roomCapacity + currNpcI, residueCollided, unconfirmedBattleResults, ref confirmedBattleResult, trapLocalIdToColliderAttrs, ref justTriggeredStoryPointId, currRdfPushbackFrameLog, pushbackFrameLogEnabled, logger);

            // Trigger subscription-based NPC movements
            for (int i = 0; i < currNpcI; i++) {
                var src = currRenderFrame.NpcsArr[i];
                if (TERMINATING_PLAYER_ID == src.Id) break;
                if (MAGIC_EVTSUB_ID_NONE == src.SubscriptionId) continue; // No subscription or already triggered
                if (0 >= (fulfilledEvtSubscriptionSetMask & (1ul << (src.SubscriptionId - 1)))) continue; // Subscription not fulfilled
                var dst = nextRenderFrameNpcs[i];
                dst.SubscriptionId = MAGIC_EVTSUB_ID_NONE;
            }

            int bulletColliderCntOffset = colliderCnt;
            _insertFromEmissionDerivedBullets(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _insertBulletColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, dynamicRectangleColliders, ref colliderCnt, collisionSys, ref bulletCnt, logger);
            
            ulong nextWaveNpcKilledEvtMaskCounter = currRenderFrame.WaveNpcKilledEvtMaskCounter;
            EvtSubscription currRdfWaveNpcKilledEvtSub = currRenderFrame.EvtSubsArr[MAGIC_EVTSUB_ID_WAVER - 1];
            EvtSubscription nextRdfWaveNpcKilledEvtSub = nextEvtSubs[MAGIC_EVTSUB_ID_WAVER - 1];

            _calcBulletCollisions(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameBullets, nextRenderFrameTriggers, ref overlapResult, collision, dynamicRectangleColliders, bulletColliderCntOffset, colliderCnt, triggerTrackingIdToTrapLocalId, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, nextRdfWaveNpcKilledEvtSub, ref fulfilledEvtSubscriptionSetMask, logger);
            
            int nextNpcI = currNpcI;
            // [WARNING] Deliberately put "_calcTriggerReactions" after "_calcBulletCollisions", "_calcDynamicTrapMovementCollisions" and "_calcCompletelyStaticTrapDamage", such that it could capture the just-fulfilled-evtsub. 
            _calcTriggerReactions(currRenderFrame, candidate, roomCapacity, nextRenderFrameTraps, nextRenderFrameTriggers, triggerTrackingIdToTrapLocalId, nextRenderFrameNpcs, ref nextRenderFrameNpcLocalIdCounter, ref nextNpcI, ref nextWaveNpcKilledEvtMaskCounter, currRdfWaveNpcKilledEvtSub, nextRdfWaveNpcKilledEvtSub, ref fulfilledEvtSubscriptionSetMask, justFulfilledEvtSubArr, ref justFulfilledEvtSubCnt, logger);

            _calcDynamicTrapMovementCollisions(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, decodedInputHolder, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, residueCollided, logger);
            
            _calcCompletelyStaticTrapDamage(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, collision, completelyStaticTrapColliders, logger);

            _processEffPushbacks(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, effPushbacks, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, colliderCnt, logger);

            _calcFallenDeath(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, logger);

            bool isRemapNeeded = false;
            _leftShiftDeadNpcs(roomCapacity, nextRenderFrameNpcs, nextEvtSubs, nextRdfWaveNpcKilledEvtSub, ref fulfilledEvtSubscriptionSetMask, joinIndexRemap, out isRemapNeeded, ref nextNpcI, logger);

            if (isRemapNeeded) {
                remapBulletOffenderJoinIndex(roomCapacity, nextNpcI, nextRenderFrameBullets, joinIndexRemap);
            }

            if (0 < (fulfilledEvtSubscriptionSetMask & (1ul << (missionEvtSubId - 1)))) {
                if (1 == roomCapacity) {
                    confirmedBattleResult.WinnerJoinIndex = selfPlayerJoinIndex;
                } else {
                    var (rdfAllConfirmed, delayedInputFrameId) = isRdfAllConfirmed(currRenderFrame.Id, inputBuffer, roomCapacity);
                    if (rdfAllConfirmed) {
                        confirmedBattleResult.WinnerJoinIndex = selfPlayerJoinIndex;
                    } else {
                        // [WARNING] This cached information could be created by a CORRECTLY PREDICTED "delayedInputFrameDownsync", thus we need a rollback from there on to finally consolidate the result later!
                        unconfirmedBattleResults[delayedInputFrameId] = confirmedBattleResult; // The "value" here is actually not useful, it's just stuffed here for type-correctness :)
                    }
                }
            }

            for (int i = 0; i < colliderCnt; i++) {
                Collider dynamicCollider = dynamicRectangleColliders[i];
                if (null == dynamicCollider.Space) {
                    throw new ArgumentNullException("Null dynamicCollider.Space is not allowed in `Step`!");
                }
                dynamicCollider.Space.RemoveSingle(dynamicCollider);
            }

            candidate.Id = nextRenderFrameId;
            candidate.BulletLocalIdCounter = nextRenderFrameBulletLocalIdCounter;
            candidate.NpcLocalIdCounter = nextRenderFrameNpcLocalIdCounter;
            candidate.WaveNpcKilledEvtMaskCounter = nextWaveNpcKilledEvtMaskCounter;

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
                case InAirIdle1ByWallJump:
                case InAirAtk1:
                case InAirAtked1:
                case OnWallIdle1:
                case Sliding:
                case CrouchIdle1:
                case CrouchAtk1:
                case CrouchAtked1:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.ShrinkedSizeX, chConfig.ShrinkedSizeY);
                    break;
                default:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.DefaultSizeX, chConfig.DefaultSizeY);
                    break;
            }
        }

        protected static bool addNewBulletToNextFrame(int originatedRdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int xfac, Skill skillConfig, RepeatedField<Bullet> nextRenderFrameBullets, int activeSkillHit, int activeSkillId, ref int bulletLocalIdCounter, ref int bulletCnt, ref bool hasLockVel, Bullet? referencePrevHitBullet) {
            if (activeSkillHit >= skillConfig.Hits.Count) return false;
            var bulletConfig = skillConfig.Hits[activeSkillHit];
            var bulletDirMagSq = bulletConfig.DirX * bulletConfig.DirX + bulletConfig.DirY * bulletConfig.DirY;
            var invBulletDirMag = InvSqrt32(bulletDirMagSq);
            var bulletSpeedXfac = xfac * invBulletDirMag * bulletConfig.DirX;
            var bulletSpeedYfac = invBulletDirMag * bulletConfig.DirY;
            int newVirtualX = null == referencePrevHitBullet ? currCharacterDownsync.VirtualGridX + xfac * bulletConfig.HitboxOffsetX : referencePrevHitBullet.VirtualGridX;
            int newVirtualY = null == referencePrevHitBullet ? currCharacterDownsync.VirtualGridY + bulletConfig.HitboxOffsetY : referencePrevHitBullet.VirtualGridY;

            AssignToBullet(
                    bulletLocalIdCounter,
                    originatedRdfId,
                    currCharacterDownsync.JoinIndex,
                    currCharacterDownsync.BulletTeamId,
                    BulletState.StartUp, 0,
                    newVirtualX, 
                    newVirtualY, 
                    xfac * bulletConfig.DirX, bulletConfig.DirY, // dir
                    (int)(bulletSpeedXfac * bulletConfig.Speed), (int)(bulletSpeedYfac * bulletConfig.Speed), // velocity
                    activeSkillHit, activeSkillId, bulletConfig, bulletConfig.RepeatQuota,
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // [WARNING] This part locks velocity by the last bullet in the simultaneous array
            if (!bulletConfig.DelaySelfVelToActive) {
                if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                    hasLockVel = true;
                    thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
                }
                if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                    hasLockVel = true;
                    thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
                }
            }

            // Explicitly specify termination of nextRenderFrameBullets
            nextRenderFrameBullets[bulletCnt].BattleAttr.BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            return true;
        }

        protected static bool addNewNpcToNextFrame(int virtualGridX, int virtualGridY, int dirX, int dirY, int characterSpeciesId, int teamId, bool isStatic, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, int evtSubIdUponKilled, ulong waveNpcKilledEvtMaskCounter, int subscriptionId) {
            var chConfig = characters[characterSpeciesId];
            int birthVirtualX = virtualGridX + ((chConfig.DefaultSizeX >> 2) * dirX);
            AssignToCharacterDownsync(npcLocalIdCounter, characterSpeciesId, birthVirtualX, virtualGridY, dirX, dirY, 0, 0, 0, 0, 0, NO_SKILL, NO_SKILL_HIT, 0, chConfig.Speed, Idle1, npcCnt, chConfig.Hp, chConfig.Hp, true, false, 0, 0, 0, teamId, teamId, birthVirtualX, virtualGridY, dirX, dirY, false, false, false, false, 0, 0, 0, 1000, 1000, COLLISION_CHARACTER_INDEX_PREFIX, chConfig.OmitGravity, chConfig.OmitSoftPushback, chConfig.RepelSoftPushback, isStatic, 0, false, false, true, 0, false, 0, defaultTemplateBuffList, defaultTemplateDebuffList, null, false, evtSubIdUponKilled, waveNpcKilledEvtMaskCounter, subscriptionId, nextRenderFrameNpcs[npcCnt]);
            npcLocalIdCounter++;
            npcCnt++;
            nextRenderFrameNpcs[npcCnt].Id = TERMINATING_PLAYER_ID;
            return true;
        }
        
        public static bool isInJumpStartup(CharacterDownsync cd) {
            return (InAirIdle1ByJump == cd.CharacterState || InAirIdle1ByWallJump == cd.CharacterState) && (0 < cd.FramesToStartJump);
        } 

        public static bool isJumpStartupJustEnded(CharacterDownsync currCd, CharacterDownsync nextCd) {
            return ((InAirIdle1ByJump == currCd.CharacterState && InAirIdle1ByJump == nextCd.CharacterState) || (InAirIdle1ByWallJump == currCd.CharacterState && InAirIdle1ByWallJump == nextCd.CharacterState)) && (1 == currCd.FramesToStartJump) && (0 == nextCd.FramesToStartJump);
        } 

        public static bool isAllConfirmed(ulong confirmedList, int roomCapacity) {
            return (confirmedList+1 == (1UL << roomCapacity));
        }
        
        public static (bool, int) isRdfAllConfirmed(int rdfId, FrameRingBuffer<InputFrameDownsync> inputBuffer, int roomCapacity) {
            int delayedInputFrameId = ConvertToDelayedInputFrameId(rdfId);
            if (0 >= delayedInputFrameId) {
                throw new ArgumentNullException(String.Format("rdfId={0}, delayedInputFrameId={0} is invalid when escaped!", rdfId, delayedInputFrameId));
            }

            var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            if (!ok || null == delayedInputFrameDownsync) {
                throw new ArgumentNullException(String.Format("InputFrameDownsync for delayedInputFrameId={0} is null when escaped!", delayedInputFrameId));
            }
            return (isAllConfirmed(delayedInputFrameDownsync.ConfirmedList, roomCapacity), delayedInputFrameId);
        }

        public static bool chOmittingSoftPushback(CharacterDownsync ch) {
            if (ch.OmitSoftPushback) return true;
            if (NO_SKILL != ch.ActiveSkillId && NO_SKILL_HIT != ch.ActiveSkillHit) {
                var skillConfig = skills[ch.ActiveSkillId];
                if (0 <= ch.ActiveSkillHit && ch.ActiveSkillHit < skillConfig.Hits.Count) {
                    var bulletConfig = skillConfig.Hits[ch.ActiveSkillHit];
                    return (BulletType.Melee == bulletConfig.BType && bulletConfig.OmitSoftPushback);
                }
            } 
            return false;
        }
    }
}
