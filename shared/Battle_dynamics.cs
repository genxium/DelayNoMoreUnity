using System;
using static shared.CharacterState;
using System.Collections.Generic;
using Google.Protobuf.Collections;
using System.Security.Cryptography;

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

            holder.Dx = DIRECTION_DECODER[encodedDirection, 0];
            holder.Dy = DIRECTION_DECODER[encodedDirection, 1];
            holder.BtnALevel = btnALevel;
            holder.BtnBLevel = btnBLevel;
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

            return (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy);
        }

        public static bool isTriggerClickable(Trigger trigger) {
            return (0 == trigger.FramesToRecover && 0 < trigger.Quota);
        }

        private static bool _useSkill(int patternId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, ref int bulletLocalIdCounter, ref int bulletCnt, RoomDownsyncFrame currRenderFrame, RepeatedField<Bullet> nextRenderFrameBullets) {
            bool skillUsed = false;
            if (PATTERN_ID_NO_OP == patternId || PATTERN_ID_UNABLE_TO_OP == patternId) {
                return false;
            }
            var skillId = FindSkillId(patternId, currCharacterDownsync, chConfig.SpeciesId);
            int xfac = (0 < thatCharacterInNextFrame.DirX ? 1 : -1);
            bool hasLockVel = false;
            if (skills.ContainsKey(skillId)) {
                var skillConfig = skills[skillId];
                if (skillConfig.MpDelta > currCharacterDownsync.Mp) {
                    skillId = FindSkillId(1, currCharacterDownsync, chConfig.SpeciesId); // Fallback to basic atk
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
                    if (!addNewBulletToNextFrame(currRenderFrame.Id, currCharacterDownsync, thatCharacterInNextFrame, xfac, skillConfig, nextRenderFrameBullets, activeSkillHit, skillId, ref bulletLocalIdCounter, ref bulletCnt, ref hasLockVel)) break;
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

        private static void _applyGravity(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame) {
            if (currCharacterDownsync.OmitGravity) {
                return;
            }
            if (!currCharacterDownsync.InAir) {
                return;
            }
            // TODO: The current dynamics calculation has a bug. When "true == currCharacterDownsync.InAir" and the character lands on the intersecting edge of 2 parallel rectangles, the hardPushbacks are doubled.
            if (!currCharacterDownsync.JumpTriggered && OnWallIdle1 == currCharacterDownsync.CharacterState) {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY = chConfig.WallSlidingVelY;
            } else if (Dashing == currCharacterDownsync.CharacterState || Dashing == thatCharacterInNextFrame.CharacterState) {
                // Don't apply gravity if will enter dashing state in next frame
                thatCharacterInNextFrame.VelX += GRAVITY_X;
            } else {
                thatCharacterInNextFrame.VelX += GRAVITY_X;
                thatCharacterInNextFrame.VelY += GRAVITY_Y;
            }
        }

        private static void _processPlayerInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<Bullet> nextRenderFrameBullets, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = 0; i < roomCapacity; i++) {
                var currCharacterDownsync = currRenderFrame.PlayersArr[i];
                var thatCharacterInNextFrame = nextRenderFramePlayers[i];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy) = _derivePlayerOpPattern(currCharacterDownsync, currRenderFrame, chConfig, inputBuffer, decodedInputHolder, prevDecodedInputHolder);

                if (PATTERN_ID_UNABLE_TO_OP == patternId && 0 < currCharacterDownsync.FramesToRecover) {
                    continue;
                }

                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;
                thatCharacterInNextFrame.SlipJumpTriggered = slipJumpedOrNot;

                bool usedSkill = _useSkill(patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets);
                if (usedSkill) {
                    thatCharacterInNextFrame.FramesCapturedByInertia = 0; // The use of a skill should break "CapturedByInertia"
                    var skillConfig = skills[thatCharacterInNextFrame.ActiveSkillId];
                    if (!skillConfig.Hits[0].AllowsWalking) {
                        continue; // Don't allow movement if skill is used
                    }
                }

                _processInertiaWalking(currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, jumpedOrNot, chConfig, false, usedSkill);
            }
        }
        
        public static void _resetVelocityOnRecovered(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame) {
            // [WARNING] This is a necessary cleanup before "_processInertiaWalking"!
            if (1 == currCharacterDownsync.FramesToRecover && 0 == thatCharacterInNextFrame.FramesToRecover && (Atked1 == currCharacterDownsync.CharacterState || InAirAtked1 == currCharacterDownsync.CharacterState || CrouchAtked1 == currCharacterDownsync.CharacterState)) {
                thatCharacterInNextFrame.VelX = 0;
                thatCharacterInNextFrame.VelY = 0;
            }
        }

        public static void _processInertiaWalking(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int effDx, int effDy, bool jumpedOrNot, CharacterConfig chConfig, bool shouldIgnoreInertia, bool usedSkill) {
            bool currFreeFromInertia = (0 == currCharacterDownsync.FramesCapturedByInertia);
            bool currBreakingFromInertia = (1 == currCharacterDownsync.FramesCapturedByInertia);
            bool withInertiaBreakingState = (jumpedOrNot || (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState));
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

            if (0 == currCharacterDownsync.FramesToRecover || WalkingAtk1 == currCharacterDownsync.CharacterState) {
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
                            thatCharacterInNextFrame.CharacterState = chConfig.HasTurnAroundAnim ? TurnAround : Walking;
                            thatCharacterInNextFrame.FramesCapturedByInertia = chConfig.InertiaFramesToRecover;
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

            if (!jumpedOrNot && 0 > effDy && !currCharacterDownsync.InAir && chConfig.CrouchingEnabled) {
                if (1 >= currCharacterDownsync.FramesToRecover) {
                    thatCharacterInNextFrame.VelX = 0;
                    thatCharacterInNextFrame.CharacterState = CrouchIdle1;
                }
            }

            if (usedSkill || WalkingAtk1 == currCharacterDownsync.CharacterState) {
                /*
                 * [WARNING]
                 * 
                 * A dirty fix here just for GunGirl "Atk1 -> WalkingAtk1" transition.
                 * 
                 * In this case "thatCharacterInNextFrame.FramesToRecover" is already set by the skill in use, and transition to "TurnAround" should NOT be supported!
                 */
                if (0 < thatCharacterInNextFrame.FramesToRecover) {
                    if (0 != thatCharacterInNextFrame.VelX) {
                        thatCharacterInNextFrame.CharacterState = WalkingAtk1;
                    } else if (CrouchIdle1 == thatCharacterInNextFrame.CharacterState) {
                        thatCharacterInNextFrame.CharacterState = CrouchAtk1;
                    } else {
                        thatCharacterInNextFrame.CharacterState = Atk1;
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
                return false;
            }
            return (bullet.BattleAttr.OriginatedRenderFrameId + bullet.Config.StartupFrames < currRenderFrameId) && (bullet.BattleAttr.OriginatedRenderFrameId + bullet.Config.StartupFrames + bullet.Config.ActiveFrames > currRenderFrameId);
        }

        public static bool IsBulletAlive(Bullet bullet, int currRenderFrameId) {
            if (BulletState.Exploding == bullet.BlState) {
                return bullet.FramesInBlState < bullet.Config.ExplosionFrames;
            }
            return (bullet.BattleAttr.OriginatedRenderFrameId + bullet.Config.StartupFrames + bullet.Config.ActiveFrames > currRenderFrameId);
        }

        private static void _insertFromEmissionDerivedBullets(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> currRenderFrameBullets, RepeatedField<Bullet> nextRenderFrameBullets, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            bool dummyHasLockVel = false; // Would be ALWAYS false when used within this function bcz we're only adding subsequent multihit bullets!
            for (int i = 0; i < currRenderFrameBullets.Count; i++) {
                var src = currRenderFrameBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BattleAttr.BulletLocalId) break;
                int j = src.BattleAttr.OffenderJoinIndex - 1;
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
                        if (addNewBulletToNextFrame(src.BattleAttr.OriginatedRenderFrameId, offender, offenderNextFrame, xfac, skillConfig, nextRenderFrameBullets, offenderNextFrame.ActiveSkillHit, src.BattleAttr.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel)) {
                            var bulletConfig = skillConfig.Hits[offenderNextFrame.ActiveSkillHit];
                            if (offenderNextFrame.FramesInvinsible < bulletConfig.StartupInvinsibleFrames) {
                                offenderNextFrame.FramesInvinsible = bulletConfig.StartupInvinsibleFrames;
                            }
                        }
                    }
                }
            }
        }

        private static void _insertBulletColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> currRenderFrameBullets, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, ref int colliderCnt, CollisionSpace collisionSys, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = 0; i < currRenderFrameBullets.Count; i++) {
                var src = currRenderFrameBullets[i];
                if (TERMINATING_BULLET_LOCAL_ID == src.BattleAttr.BulletLocalId) break;
                var dst = nextRenderFrameBullets[bulletCnt];
                AssignToBullet(
                        src.BattleAttr.BulletLocalId,
                        src.BattleAttr.OriginatedRenderFrameId,
                        src.BattleAttr.OffenderJoinIndex,
                        src.BattleAttr.TeamId,
                        src.BlState, src.FramesInBlState + 1,
                        src.VirtualGridX, src.VirtualGridY, // virtual grid position
                        src.DirX, src.DirY, // dir
                        src.VelX, src.VelY, // velocity
                        src.BattleAttr.ActiveSkillHit, src.BattleAttr.SkillId, src.Config,
                        dst);

                int j = dst.BattleAttr.OffenderJoinIndex - 1;
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
                        var (hitboxSizeCx, hitboxSizeCy) = VirtualGridToPolygonColliderCtr(src.Config.HitboxSizeX, src.Config.HitboxSizeY);
                        var newBulletCollider = dynamicRectangleColliders[colliderCnt];
                        UpdateRectCollider(newBulletCollider, bulletCx, bulletCy, hitboxSizeCx, hitboxSizeCy, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, dst);
                        colliderCnt++;

                        collisionSys.AddSingle(newBulletCollider);
                        dst.BlState = BulletState.Active;
                        if (dst.BlState != src.BlState) {
                            dst.FramesInBlState = 0;
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
                if (currCharacterDownsync.JumpTriggered) {
                    // We haven't proceeded with "OnWall" calculation for "thatPlayerInNextFrame", thus use "currCharacterDownsync.OnWall" for checking
                    if (OnWallIdle1 == currCharacterDownsync.CharacterState) {
                        if (0 < currCharacterDownsync.VelX * currCharacterDownsync.OnWallNormX) {
                            newVx -= currCharacterDownsync.VelX; // Cancel the alleged horizontal movement pointing to same direction of wall inward norm first
                        }
                        // Always jump to the opposite direction of wall inward norm
                        int xfac = (0 > currCharacterDownsync.OnWallNormX ? 1 : -1);
                        newVx += xfac * chConfig.WallJumpingInitVelX; // Immediately gets out of the snap
                        newVy += chConfig.WallJumpingInitVelY;
                        thatCharacterInNextFrame.VelX = (xfac * chConfig.WallJumpingInitVelX);
                        thatCharacterInNextFrame.VelY = (chConfig.WallJumpingInitVelY);
                        thatCharacterInNextFrame.FramesToRecover = chConfig.WallJumpingFramesToRecover;
                        thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
                    } else {
                        thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY;
                        thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                    }
                } else if (!currCharacterDownsync.InAir && currCharacterDownsync.PrimarilyOnSlippableHardPushback && currCharacterDownsync.SlipJumpTriggered) {
                    newVy -= SLIP_JUMP_CHARACTER_DROP_VIRTUAL;
                } 

                if (i < roomCapacity && 0 >= thatCharacterInNextFrame.Hp && 0 == thatCharacterInNextFrame.FramesToRecover) {
                    // Revive player-controlled character from Dying
                    (newVx, newVy) = (currCharacterDownsync.RevivalVirtualGridX, currCharacterDownsync.RevivalVirtualGridY);
                    thatCharacterInNextFrame.CharacterState = GetUp1;
                    thatCharacterInNextFrame.FramesInChState = 0;
                    thatCharacterInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                    thatCharacterInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;

                    thatCharacterInNextFrame.Hp = currCharacterDownsync.MaxHp;
                    thatCharacterInNextFrame.DirX = currCharacterDownsync.RevivalDirX;
                    thatCharacterInNextFrame.DirY = currCharacterDownsync.RevivalDirY;
                }

                float boxCx, boxCy, boxCw, boxCh;
                calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, newVx, newVy, out boxCx, out boxCy, out boxCw, out boxCh);
                Collider characterCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(characterCollider, boxCx, boxCy, boxCw, boxCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync); // the coords of all barrier boundaries are multiples of tileWidth(i.e. 16), by adding snapping y-padding when "landedOnGravityPushback" all "characterCollider.Y" would be a multiple of 1.0
                colliderCnt++;

                // Add to collision system
                collisionSys.AddSingle(characterCollider);

                _applyGravity(currCharacterDownsync, chConfig, thatCharacterInNextFrame);
            }
        }

        private static void _calcCharacterMovementPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, int currNpcI, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trigger> nextRenderFrameTriggers, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPusbackEnabled, Collider[] dynamicRectangleColliders, int iSt, int iEd, FrameRingBuffer<Collider> residueCollided, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, RdfPushbackFrameLog? currPushbackFrameLog, bool pushbackFrameLogEnabled, ILoggerBridge logger) {
            // Calc pushbacks for each player (after its movement) w/o bullets
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
                int hardPushbackCnt = calcHardPushbacksNormsForCharacter(currRenderFrame, currCharacterDownsync, thatCharacterInNextFrame, aCollider, aShape, hardPushbackNormsArr[i], collision, ref overlapResult, ref primaryOverlapResult, out primaryHardOverlapIndex, out primaryTrap, residueCollided, logger);

                if (pushbackFrameLogEnabled && null != currPushbackFrameLog) {
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
                    processPrimaryAndImpactEffPushback(effPushbacks[i], hardPushbackNormsArr[i], hardPushbackCnt, primaryHardOverlapIndex, SNAP_INTO_PLATFORM_OVERLAP);
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
                bool goingDown = (thatCharacterInNextFrame.OnSlope && !currCharacterDownsync.JumpTriggered && thatCharacterInNextFrame.VelY <= 0 && 0 > projectedVel); // We don't care about going up, it's already working...  
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

                if (softPusbackEnabled && Dying != currCharacterDownsync.CharacterState && false == currCharacterDownsync.OmitSoftPushback) {
                    int softPushbacksCnt = 0, primarySoftOverlapIndex = -1;
                    int totOtherChCnt = 0, cellOverlappedOtherChCnt = 0, shapeOverlappedOtherChCnt = 0;
                    int origResidueCollidedSt = residueCollided.StFrameId, origResidueCollidedEd = residueCollided.EdFrameId; 
                    float primarySoftOverlapMagSqr = float.MinValue;
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
                            }
                        }
                        var v2 = bCollider.Data as TrapColliderAttr;
                        if (null != v2 && v2.ProvidesEscape && currCharacterDownsync.JoinIndex <= roomCapacity) {
                            var (escaped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                            // Currently only allowing "Player" to win.
                            if (escaped) {
                                int delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrame.Id);
                                if (0 >= delayedInputFrameId) {
                                    throw new ArgumentNullException(String.Format("currRenderFrame.Id={0}, delayedInputFrameId={0} is invalid when escaped!", currRenderFrame.Id, delayedInputFrameId));
                                }

                                var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
                                if (!ok || null == delayedInputFrameDownsync) {
                                    throw new ArgumentNullException(String.Format("InputFrameDownsync for delayedInputFrameId={0} is null when escaped!", delayedInputFrameId));
                                }
                                if (1 == roomCapacity || (delayedInputFrameDownsync.ConfirmedList+1 == (1UL << roomCapacity))) {
                                    confirmedBattleResult.WinnerJoinIndex = currCharacterDownsync.JoinIndex;
                                    continue;
                                } else {
                                    // [WARNING] This cached information could be created by a CORRECTLY PREDICTED "delayedInputFrameDownsync", thus we need a rollback from there on to finally consilidate the result later!
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
                        if (currCharacterDownsync.ChCollisionTeamId == v1.ChCollisionTeamId) {
                            // ignore collision within same collisionTeam, rarely used
                            continue;
                        }

                        cellOverlappedOtherChCnt++;

                        var (overlapped, softPushbackX, softPushbackY) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                        if (!overlapped) {
                            continue;
                        }

                        shapeOverlappedOtherChCnt++;

                        normAlignmentWithGravity = (overlapResult.OverlapY * -1f);
                        if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                            landedOnGravityPushback = true;
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
                        if (primarySoftOverlapMagSqr < magSqr) {
                            primarySoftOverlapMagSqr = magSqr;
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

                    processPrimaryAndImpactEffPushback(effPushbacks[i], softPushbacks, softPushbacksCnt, primarySoftOverlapIndex, SNAP_INTO_CHARACTER_OVERLAP);

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
                            thatCharacterInNextFrame.CharacterState = LayDown1;
                            thatCharacterInNextFrame.FramesToRecover = chConfig.LayDownFrames;
                        } else {
                            switch (currCharacterDownsync.CharacterState) {
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
                                thatCharacterInNextFrame.VelY = (thatCharacterInNextFrame.OnSlope ? 0 : chConfig.DownSlopePrimerVelY);
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
                    if (thatCharacterInNextFrame.InAir) {
                        // [WARNING] Sticking to wall MUST BE based on "InAir", otherwise we would get gravity reduction from ground up incorrectly!
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

        private static void _calcBulletCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trigger> nextRenderFrameTriggers, ref SatResult overlapResult, Collision collision, Collider[] dynamicRectangleColliders, int iSt, int iEd, Dictionary<int, int> triggerTrackingIdToTrapLocalId, ILoggerBridge logger) {
            // [WARNING] Bullet collision doesn't result in immediate pushbacks but instead imposes a "velocity" on the impacted characters to simplify pushback handling! 
            // Check bullet-anything collisions
            for (int i = iSt; i < iEd; i++) {
                Collider bulletCollider = dynamicRectangleColliders[i];
                if (null == bulletCollider.Data) continue;
                var bullet = bulletCollider.Data as Bullet;
                if (null == bullet || TERMINATING_BULLET_LOCAL_ID == bullet.BattleAttr.BulletLocalId) {
                    logger.LogWarn(String.Format("dynamicRectangleColliders[i:{0}] is not having bullet type! iSt={1}, iEd={2}", i, iSt, iEd));
                    continue;
                }
                bool collided = bulletCollider.CheckAllWithHolder(0, 0, collision);
                if (!collided) continue;

                bool exploded = false;
                bool explodedOnAnotherCharacter = false;

                var bulletShape = bulletCollider.Shape;
                int j = bullet.BattleAttr.OffenderJoinIndex - 1;
                var offender = (j < roomCapacity ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);
                int effDirX = (BulletType.Melee == bullet.Config.BType ? offender.DirX : bullet.DirX);
                int xfac = (0 < effDirX ? 1 : -1);
                var skillConfig = skills[bullet.BattleAttr.SkillId];
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
                            if (bullet.BattleAttr.OffenderJoinIndex <= roomCapacity) {
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
                            if (bullet.BattleAttr.OffenderJoinIndex == atkedCharacterInCurrFrame.JoinIndex) continue;
                            if (bullet.BattleAttr.TeamId == atkedCharacterInCurrFrame.BulletTeamId) continue;
                            if (invinsibleSet.Contains(atkedCharacterInCurrFrame.CharacterState)) continue;
                            if (0 < atkedCharacterInCurrFrame.FramesInvinsible) continue;
                            exploded = true;
                            explodedOnAnotherCharacter = true;
                            //logger.LogWarn(String.Format("MeleeBullet with collider:[blx:{0}, bly:{1}, w:{2}, h:{3}], bullet:{8} exploded on bCollider: [blx:{4}, bly:{5}, w:{6}, h:{7}], atkedCharacterInCurrFrame: {9}", bulletCollider.X, bulletCollider.Y, bulletCollider.W, bulletCollider.H, bCollider.X, bCollider.Y, bCollider.W, bCollider.H, bullet, atkedCharacterInCurrFrame));
                            int atkedJ = atkedCharacterInCurrFrame.JoinIndex - 1;
                            var atkedCharacterInNextFrame = (atkedJ < roomCapacity ? nextRenderFramePlayers[atkedJ] : nextRenderFrameNpcs[atkedJ - roomCapacity]);
                            CharacterState oldNextCharacterState = atkedCharacterInNextFrame.CharacterState;
                            atkedCharacterInNextFrame.Hp -= bullet.Config.Damage;
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
                                bool shouldOmitHitPushback = (atkedCharacterConfig.Hardness > bullet.Config.Hardness);   
                                if (false == shouldOmitHitPushback && BlownUp1 != oldNextCharacterState) {
                                    var (pushbackVelX, pushbackVelY) = (xfac * bullet.Config.PushbackVelX, bullet.Config.PushbackVelY);
                                    // The traversal order of bullets is deterministic, thus the following assignment is deterministic regardless of the order of collision result popping.
                                    atkedCharacterInNextFrame.VelX = pushbackVelX;
                                    atkedCharacterInNextFrame.VelY = pushbackVelY;
                                }

                                bool shouldOmitStun = ((0 >= bullet.Config.HitStunFrames) || shouldOmitHitPushback);
                                if (false == shouldOmitStun) {
                                    if (bullet.Config.BlowUp) {
                                        atkedCharacterInNextFrame.CharacterState = BlownUp1;
                                    } else if (BlownUp1 != oldNextCharacterState) {
                                        if (isCrouching(atkedCharacterInNextFrame.CharacterState)) {
                                            atkedCharacterInNextFrame.CharacterState = CrouchAtked1;
                                        } else {
                                            atkedCharacterInNextFrame.CharacterState = Atked1;
                                        }
                                    }
                                    int oldFramesToRecover = atkedCharacterInNextFrame.FramesToRecover;
                                    if (bullet.Config.HitStunFrames > oldFramesToRecover) {
                                        atkedCharacterInNextFrame.FramesToRecover = bullet.Config.HitStunFrames;
                                    }
                                }
                                if (atkedCharacterInNextFrame.FramesInvinsible < bullet.Config.HitInvinsibleFrames) {
                                    atkedCharacterInNextFrame.FramesInvinsible = bullet.Config.HitInvinsibleFrames;
                                }
                            }
                            break;
                        case Bullet v4:
                            if (!COLLIDABLE_PAIRS.Contains(bullet.Config.CollisionTypeMask | v4.Config.CollisionTypeMask)) {
                                break;
                            }
                            if (bullet.BattleAttr.TeamId == v4.BattleAttr.TeamId) continue;
                            if (bullet.Config.Hardness > v4.Config.Hardness) continue; 
                            exploded = true;
                            break;
                        case TrapColliderAttr v5:
                            if (!v5.ProvidesHardPushback) {
                                break;
                            }
                            if (!COLLIDABLE_PAIRS.Contains(bullet.Config.CollisionTypeMask | COLLISION_BARRIER_INDEX_PREFIX)) {
                                break;
                            }
                            exploded = true;
                            break;
                        default:
                            if (!COLLIDABLE_PAIRS.Contains(bullet.Config.CollisionTypeMask | COLLISION_BARRIER_INDEX_PREFIX)) {
                                break;
                            }
                            exploded = true;
                            break;
                    }
                }

                bool inTheMiddleOfMultihitTransition = false;
                if (MultiHitType.None != bullet.Config.MhType) {
                    if (bullet.BattleAttr.ActiveSkillHit + 1 < skillConfig.Hits.Count) {
                        inTheMiddleOfMultihitTransition = true;
                    }
                }

                if (exploded) {
                    if (BulletType.Melee == bullet.Config.BType) {
                        bullet.BlState = BulletState.Exploding;
                        if (explodedOnAnotherCharacter) {
                            bullet.FramesInBlState = 0;
                        } else {
                            // When hitting a barrier, don't play explosion anim
                            bullet.FramesInBlState = bullet.Config.ExplosionFrames + 1;
                        }
                    } else if (BulletType.Fireball == bullet.Config.BType) {
                        if (inTheMiddleOfMultihitTransition) {
                            bullet.BattleAttr.ActiveSkillHit += 1;
                            if (MultiHitType.FromPrevHitActual == bullet.Config.MhType) {
                                bullet.FramesInBlState = 0;
                                bullet.BattleAttr.OriginatedRenderFrameId = currRenderFrame.Id;
                            }
                            // TODO: Support "MultiHitType.FromPrevHitAnyway"
                            bullet.Config = skillConfig.Hits[bullet.BattleAttr.ActiveSkillHit];
                        } else {
                            bullet.BlState = BulletState.Exploding;
                            bullet.FramesInBlState = 0;
                        }
                    } else {
                        // Nothing to do
                    }
                }
            }
        }

        private static void _tickSingleSubCycle(RoomDownsyncFrame currRenderFrame, Trigger currTrigger, Trigger triggerInNextFrame, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt) {
            if (0 < currTrigger.SubCycleQuotaLeft) {
                triggerInNextFrame.SubCycleQuotaLeft = currTrigger.SubCycleQuotaLeft - 1;
                triggerInNextFrame.State = TriggerState.TcoolingDown;
                triggerInNextFrame.FramesInState = 0;
                triggerInNextFrame.FramesToFire = triggerInNextFrame.ConfigFromTiled.SubCycleTriggerFrames;

                if (0 < currTrigger.ConfigFromTiled.SpawnChSpeciesIdList.Count) {
                    int pseudoRandomIdx = (currRenderFrame.Id % currTrigger.ConfigFromTiled.SpawnChSpeciesIdList.Count);
                    addNewNpcToNextFrame(currTrigger.VirtualGridX, currTrigger.VirtualGridY, currTrigger.ConfigFromTiled.InitVelX, currTrigger.ConfigFromTiled.InitVelY, currTrigger.ConfigFromTiled.SpawnChSpeciesIdList[pseudoRandomIdx], currTrigger.BulletTeamId, false, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt);
                }
            } else {
                // Wait for "FramesToRecover" to become 0
                triggerInNextFrame.State = TriggerState.Tready;
                triggerInNextFrame.SubCycleQuotaLeft = currTrigger.ConfigFromTiled.SubCycleQuota; // Refill to be fired by the next "0 == currTrigger.FramesToRecover"
                triggerInNextFrame.FramesToFire = MAX_INT;
            }
        }

        private static void _calcTriggerReactions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<Trap> nextRenderFrameTraps, RepeatedField<Trigger> nextRenderFrameTriggers, Dictionary<int, int> triggerTrackingIdToTrapLocalId, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt, ILoggerBridge logger) {
            for (int i = 0; i < currRenderFrame.TriggersArr.Count; i++) {
                var currTrigger = currRenderFrame.TriggersArr[i];
                if (TERMINATING_TRIGGER_ID == currTrigger.TriggerLocalId) break;
                var triggerInNextFrame = nextRenderFrameTriggers[i];
                if (TRIGGER_MASK_BY_CYCLIC_TIMER == currTrigger.Config.TriggerMask) {
                    // The ORDER of zero checks of "currTrigger.FramesToRecover" and "currTrigger.FramesToFire" below is important, because we want to avoid "wrong SubCycleQuotaLeft replenishing when 0 == currTrigger.FramesToRecover"!
                    if (0 == currTrigger.FramesToFire) {
                        _tickSingleSubCycle(currRenderFrame, currTrigger, triggerInNextFrame, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt);
                    } else if (0 == currTrigger.FramesToRecover) {
                        if (0 < currTrigger.Quota) {
                            triggerInNextFrame.Quota = currTrigger.Quota - 1;
                            triggerInNextFrame.FramesToRecover = currTrigger.ConfigFromTiled.RecoveryFrames;
                            _tickSingleSubCycle(currRenderFrame, currTrigger, triggerInNextFrame, nextRenderFrameNpcs, ref npcLocalIdCounter, ref npcCnt);
                        } else {
                            triggerInNextFrame.State = TriggerState.Tready;
                            triggerInNextFrame.FramesToFire = MAX_INT;
                            triggerInNextFrame.FramesToRecover = MAX_INT;
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
                    CharacterState oldNextCharacterState = thatCharacterInNextFrame.CharacterState;
                    if (inAirSet.Contains(oldNextCharacterState) && BlownUp1 != oldNextCharacterState && OnWallIdle1 != oldNextCharacterState && Dashing != oldNextCharacterState) {
                        switch (oldNextCharacterState) {
                            case InAirIdle1NoJump:
                                thatCharacterInNextFrame.CharacterState = Idle1;
                                break;
                            case InAirIdle1ByJump:
                            case InAirIdle1ByWallJump:
                                if (!currCharacterDownsync.InAir && currCharacterDownsync.JumpTriggered) {
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
                    } else if (thatCharacterInNextFrame.ForcedCrouching) {
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
                            if (hasBeenOnWallChState || hasBeenOnWallCollisionResultForSameChState) {
                                thatCharacterInNextFrame.CharacterState = OnWallIdle1;
                            }
                            break;
                    }
                }

                // Reset "FramesInChState" if "CharacterState" is changed
                if (thatCharacterInNextFrame.CharacterState != currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.FramesInChState = 0;
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

        private static void _leftShiftDeadNpcs(int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ILoggerBridge logger) {
            int aliveSlotI = 0, candidateI = 0;
            while (candidateI < nextRenderFrameNpcs.Count && TERMINATING_PLAYER_ID != nextRenderFrameNpcs[candidateI].Id) {
                while (candidateI < nextRenderFrameNpcs.Count && TERMINATING_PLAYER_ID != nextRenderFrameNpcs[candidateI].Id && isNpcDeadToDisappear(nextRenderFrameNpcs[candidateI])) {
                    candidateI++;
                }
                if (candidateI >= nextRenderFrameNpcs.Count || TERMINATING_PLAYER_ID == nextRenderFrameNpcs[candidateI].Id) {
                    break;
                }
                var src = nextRenderFrameNpcs[candidateI];
                var dst = nextRenderFrameNpcs[aliveSlotI];
                int joinIndex = roomCapacity + aliveSlotI + 1;

                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FramesToRecover, src.FramesInChState, src.ActiveSkillId, src.ActiveSkillHit, src.FramesInvinsible, src.Speed, src.CharacterState, joinIndex, src.Hp, src.MaxHp, src.InAir, src.OnWall, src.OnWallNormX, src.OnWallNormY, src.FramesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, src.PrimarilyOnSlippableHardPushback, src.CapturedByPatrolCue, src.FramesInPatrolCue, src.BeatsCnt, src.BeatenCnt, src.Mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, src.OnSlope, src.ForcedCrouching, src.NewBirth, dst);
                candidateI++;
                aliveSlotI++;
            }
            if (aliveSlotI < nextRenderFrameNpcs.Count) {
                nextRenderFrameNpcs[aliveSlotI].Id = TERMINATING_PLAYER_ID;
            }
        }

        public static bool isBattleResultSet(BattleResult battleResult) {
            return (MAGIC_JOIN_INDEX_DEFAULT != battleResult.WinnerJoinIndex);
        }

        public static void resetBattleResult(ref BattleResult battleResult) {
            battleResult.WinnerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT;
        }

        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPushbackEnabled, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder, FrameRingBuffer<Collider> residueCollided, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, Dictionary<int, int> triggerTrackingIdToTrapLocalId, List<Collider> completelyStaticTrapColliders, Dictionary<int, BattleResult> unconfirmedBattleResults, ref BattleResult confirmedBattleResult, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, bool pushbackFrameLogEnabled, ILoggerBridge logger) {
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
                    if (currRenderFrameId == pushbackFrameLogBuffer.EdFrameId) {
                        pushbackFrameLogBuffer.DryPut();
                        (_, currRdfPushbackFrameLog) = pushbackFrameLogBuffer.GetByFrameId(currRenderFrameId);
                    }
                }
                if (null == currRdfPushbackFrameLog) {
                    // Get the pointer to currRdfPushbackFrameLog anyway, but don't throw error if it's null but not required!
                    throw new ArgumentNullException(String.Format("pushbackFrameLogBuffer was not fully pre-allocated for currRenderFrameId={0}!", currRenderFrameId));
                }
                currRdfPushbackFrameLog.RdfId = currRenderFrameId;
            }

            // [WARNING] On backend this function MUST BE called while "InputsBufferLock" is locked!
            var nextRenderFramePlayers = candidate.PlayersArr;
            var nextRenderFrameNpcs = candidate.NpcsArr;
            var nextRenderFrameBullets = candidate.Bullets;
            int nextRenderFrameBulletLocalIdCounter = currRenderFrame.BulletLocalIdCounter;
            int nextRenderFrameNpcLocalIdCounter = currRenderFrame.NpcLocalIdCounter;
            var nextRenderFrameTraps = candidate.TrapsArr;
            var nextRenderFrameTriggers = candidate.TriggersArr;
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
                int framesInvinsible = src.FramesInvinsible - 1;
                if (0 > framesInvinsible ) {
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
                var dst = nextRenderFramePlayers[i];
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, 0, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, true, false, src.OnWallNormX, src.OnWallNormY, framesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, false, false, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, false, false, false, dst);
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
                var dst = nextRenderFrameNpcs[currNpcI];
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, 0, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, true, false, src.OnWallNormX, src.OnWallNormY, framesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, false, false, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, false, false, false, dst);
                _resetVelocityOnRecovered(src, dst);
                currNpcI++;
            }
            nextRenderFrameNpcs[currNpcI].Id = TERMINATING_PLAYER_ID; // [WARNING] This is a CRITICAL assignment because "renderBuffer" is a ring, hence when cycling across "renderBuffer.StFrameId", we must ensure that the trailing NPCs existed from the startRdf wouldn't contaminate later calculation

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
            int colliderCnt = 0, bulletCnt = 0;
            _processPlayerInputs(currRenderFrame, roomCapacity, inputBuffer, nextRenderFramePlayers, nextRenderFrameBullets, decodedInputHolder, prevDecodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _moveAndInsertCharacterColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, 0, roomCapacity + currNpcI, logger);
            _processNpcInputs(currRenderFrame, roomCapacity, currNpcI, nextRenderFrameNpcs, nextRenderFrameBullets, dynamicRectangleColliders, colliderCnt, collision, collisionSys, ref overlapResult, decodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            int trapColliderCntOffset = colliderCnt;
            _moveAndInsertDynamicTrapColliders(currRenderFrame, roomCapacity, currNpcI, nextRenderFrameTraps, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, trapColliderCntOffset, trapLocalIdToColliderAttrs, logger);
            _calcCharacterMovementPushbacks(currRenderFrame, roomCapacity, currNpcI, inputBuffer, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTriggers, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, 0, roomCapacity + currNpcI, residueCollided, unconfirmedBattleResults, ref confirmedBattleResult, trapLocalIdToColliderAttrs, currRdfPushbackFrameLog, pushbackFrameLogEnabled, logger);
            int bulletColliderCntOffset = colliderCnt;
            _insertFromEmissionDerivedBullets(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _insertBulletColliders(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, dynamicRectangleColliders, ref colliderCnt, collisionSys, ref bulletCnt, logger);
            
            _calcBulletCollisions(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTriggers, ref overlapResult, collision, dynamicRectangleColliders, bulletColliderCntOffset, colliderCnt, triggerTrackingIdToTrapLocalId, logger);
            
            int nextNpcI = currNpcI;
            _calcTriggerReactions(currRenderFrame, roomCapacity, nextRenderFrameTraps, nextRenderFrameTriggers, triggerTrackingIdToTrapLocalId, nextRenderFrameNpcs, ref nextRenderFrameNpcLocalIdCounter, ref nextNpcI, logger);
            
            _calcDynamicTrapMovementCollisions(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, decodedInputHolder, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, residueCollided, logger);
            
            _calcCompletelyStaticTrapDamage(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, collision, completelyStaticTrapColliders, logger);
            _processEffPushbacks(currRenderFrame, roomCapacity, currNpcI, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, effPushbacks, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, colliderCnt, logger);
            _leftShiftDeadNpcs(roomCapacity, nextRenderFrameNpcs, logger);
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
        }

        public static void calcCharacterBoundingBoxInCollisionSpace(CharacterDownsync characterDownsync, CharacterConfig chConfig, int newVx, int newVy, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {

            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(newVx, newVy);

            switch (characterDownsync.CharacterState) {
                case LayDown1:
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

        protected static bool addNewBulletToNextFrame(int originatedRdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int xfac, Skill skillConfig, RepeatedField<Bullet> nextRenderFrameBullets, int activeSkillHit, int activeSkillId, ref int bulletLocalIdCounter, ref int bulletCnt, ref bool hasLockVel) {
            if (activeSkillHit >= skillConfig.Hits.Count) return false;
            var bulletConfig = skillConfig.Hits[activeSkillHit];
            var bulletDirMagSq = bulletConfig.DirX * bulletConfig.DirX + bulletConfig.DirY * bulletConfig.DirY;
            var invBulletDirMag = InvSqrt32(bulletDirMagSq);
            var bulletSpeedXfac = xfac * invBulletDirMag * bulletConfig.DirX;
            var bulletSpeedYfac = invBulletDirMag * bulletConfig.DirY;
            AssignToBullet(
                    bulletLocalIdCounter,
                    originatedRdfId,
                    currCharacterDownsync.JoinIndex,
                    currCharacterDownsync.BulletTeamId,
                    BulletState.StartUp, 0,
                    currCharacterDownsync.VirtualGridX + xfac * bulletConfig.HitboxOffsetX, currCharacterDownsync.VirtualGridY + bulletConfig.HitboxOffsetY, // virtual grid position
                    xfac * bulletConfig.DirX, bulletConfig.DirY, // dir
                    (int)(bulletSpeedXfac * bulletConfig.Speed), (int)(bulletSpeedYfac * bulletConfig.Speed), // velocity
                    activeSkillHit, activeSkillId, bulletConfig,
                    nextRenderFrameBullets[bulletCnt]);

            bulletLocalIdCounter++;
            bulletCnt++;

            // [WARNING] This part locks velocity by the last bullet in the simultaneous array
            if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                hasLockVel = true;
                thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
            }
            if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                hasLockVel = true;
                thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
            }

            return true;
        }

        protected static bool addNewNpcToNextFrame(int virtualGridX, int virtualGridY, int dirX, int dirY, int characterSpeciesId, int teamId, bool isStatic, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int npcLocalIdCounter, ref int npcCnt) {
            var chConfig = Battle.characters[characterSpeciesId];
            int birthVirtualX = virtualGridX + ((chConfig.DefaultSizeX >> 2) * dirX);
            AssignToCharacterDownsync(npcLocalIdCounter, characterSpeciesId, birthVirtualX, virtualGridY, dirX, dirY, 0, 0, 0, 0, 0, NO_SKILL, NO_SKILL_HIT, 0, chConfig.Speed, Idle1, npcCnt, chConfig.Hp, chConfig.Hp, true, false, 0, 0, 0, teamId, teamId, birthVirtualX, virtualGridY, dirX, dirY, false, false, false, false, 0, 0, 0, 1000, 1000, COLLISION_CHARACTER_INDEX_PREFIX, chConfig.OmitGravity, chConfig.OmitSoftPushback, isStatic, 0, false, false, true, nextRenderFrameNpcs[npcCnt]);
            npcLocalIdCounter++;
            npcCnt++;
            return true;
        }
    }
}
