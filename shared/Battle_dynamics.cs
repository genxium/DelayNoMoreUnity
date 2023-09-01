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
                ulong joinMask = ((ulong)1 << i);
                if (0 < (confirmedList & joinMask)) {
                    // This in-place update is only valid when "delayed input for this player is not yet confirmed"
                    continue;
                }
                if (lastIndividuallyConfirmedInputFrameId[i] >= inputFrameId) {
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
            if (0 == currCharacterDownsync.FramesToRecover) {
                effDx = decodedInputHolder.Dx;
                effDy = decodedInputHolder.Dy;
            }

            int patternId = PATTERN_ID_NO_OP;
            var canJumpWithinInertia = (currCharacterDownsync.CapturedByInertia && ((chConfig.InertiaFramesToRecover >> 1) > currCharacterDownsync.FramesToRecover));
            if (0 == currCharacterDownsync.FramesToRecover || canJumpWithinInertia) {
                if (decodedInputHolder.BtnALevel > prevDecodedInputHolder.BtnALevel) {
                    if (chConfig.DashingEnabled && 0 > decodedInputHolder.Dy && Dashing != currCharacterDownsync.CharacterState) {
                        // Checking "DashingEnabled" here to allow jumping when dashing-disabled players pressed "DOWN + BtnB"
                        patternId = 5;
                    } else if (0 < decodedInputHolder.Dy) {
                        slipJumpedOrNot = true;
                    } else if (!inAirSet.Contains(currCharacterDownsync.CharacterState)) {
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
                            patternId = 3;
                        } else if (0 < decodedInputHolder.Dy) {
                            patternId = 2;
                        } else {
                            patternId = 1;
                        }
                    } else {
                        patternId = 4; // Holding
                    }
                }
            }

            return (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy);
        }

        private static bool inNonInertiaFramesToRecover(CharacterDownsync currCharacterDownsync) {
            return (0 < currCharacterDownsync.FramesToRecover && false == currCharacterDownsync.CapturedByInertia);
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

                if (false == hasLockVel && false == currCharacterDownsync.InAir) {
                    thatCharacterInNextFrame.VelX = 0;
                }

                thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;
                thatCharacterInNextFrame.FramesInChState = 0; // Must reset "FramesInChState" here to handle the extreme case where a same skill, e.g. "Atk1", is used right after the previous one ended

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

        private static void _processPlayerInputs(RoomDownsyncFrame currRenderFrame, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<Bullet> nextRenderFrameBullets, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currCharacterDownsync = currRenderFrame.PlayersArr[i];
                var thatCharacterInNextFrame = nextRenderFramePlayers[i];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, slipJumpedOrNot, effDx, effDy) = _derivePlayerOpPattern(currCharacterDownsync, currRenderFrame, chConfig, inputBuffer, decodedInputHolder, prevDecodedInputHolder);

                if (PATTERN_ID_UNABLE_TO_OP == patternId && 0 < currCharacterDownsync.FramesToRecover) {
                    continue;
                }

                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;
                thatCharacterInNextFrame.SlipJumpTriggered = slipJumpedOrNot;

                /*
                   if (1 == currCharacterDownsync.JoinIndex && 2 == patternId) {
                   logger.LogInfo(String.Format("DragonPunch in air! JoinIndex: {0}, chState: {1}, framesToRecover: {2}, capturedByInertia: {3}", currCharacterDownsync.JoinIndex, currCharacterDownsync.CharacterState, currCharacterDownsync.FramesToRecover, currCharacterDownsync.CapturedByInertia));
                   }
                 */
                bool usedSkill = _useSkill(patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets);
                if (usedSkill) {
                    thatCharacterInNextFrame.CapturedByInertia = false; // The use of a skill must break "CapturedByInertia"
                    continue; // Don't allow movement if skill is used
                }

                _processInertiaWalking(currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, jumpedOrNot, chConfig, false);
            }
        }

        public static void _processInertiaWalking(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, int effDx, int effDy, bool jumpedOrNot, CharacterConfig chConfig, bool shouldIgnoreInertia) {
            if (0 == currCharacterDownsync.FramesToRecover) {
                thatCharacterInNextFrame.CharacterState = Idle1; // When reaching here, the character is at least recovered from "Atked{N}" or "Atk{N}" state, thus revert back to "Idle" as a default action

                if (shouldIgnoreInertia) {
                    thatCharacterInNextFrame.CapturedByInertia = false;
                    if (0 != effDx) {
                        int xfac = (0 < effDx ? 1 : -1);
                        thatCharacterInNextFrame.DirX = effDx;
                        thatCharacterInNextFrame.DirY = effDy;
                        if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                            thatCharacterInNextFrame.VelX = xfac * chConfig.WallJumpingInitVelX;
                        } else {
                            thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                        }
                        thatCharacterInNextFrame.CharacterState = Walking;
                    } else {
                        thatCharacterInNextFrame.VelX = 0;
                    }
                } else {
                    bool prevCapturedByInertia = currCharacterDownsync.CapturedByInertia;
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

                    if (!(InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) && !jumpedOrNot && !prevCapturedByInertia && !alignedWithInertia) {
                        thatCharacterInNextFrame.CapturedByInertia = true;
                        if (exactTurningAround) {
                            thatCharacterInNextFrame.CharacterState = chConfig.HasTurnAroundAnim ? TurnAround : Walking;
                            thatCharacterInNextFrame.FramesToRecover = chConfig.InertiaFramesToRecover;
                        } else if (stoppingFromWalking) {
                            thatCharacterInNextFrame.FramesToRecover = chConfig.InertiaFramesToRecover;
                        } else {
                            // Updates CharacterState and thus the animation to make user see graphical feedback asap.
                            thatCharacterInNextFrame.CharacterState = Walking;
                            thatCharacterInNextFrame.FramesToRecover = (chConfig.InertiaFramesToRecover >> 1);
                        }
                    } else {
                        thatCharacterInNextFrame.CapturedByInertia = false;
                        if (0 != effDx) {
                            int xfac = (0 < effDx ? 1 : -1);
                            thatCharacterInNextFrame.DirX = effDx;
                            thatCharacterInNextFrame.DirY = effDy;
                            if (InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
                                thatCharacterInNextFrame.VelX = xfac * chConfig.WallJumpingInitVelX;
                            } else {
                                thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                            }
                            thatCharacterInNextFrame.CharacterState = Walking;
                        } else {
                            thatCharacterInNextFrame.VelX = 0;
                        }
                    }
                }
            } else {
                // Otherwise "thatCharacterInNextFrame.CapturedByInertia" remains unchanged
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
                var offender = (j < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);

                var skillConfig = skills[src.BattleAttr.SkillId];
                bool inTheMiddleOfMeleeMultihitTransition = (BulletType.Melee == src.Config.BType && MultiHitType.FromEmission == src.Config.MhType && offender.ActiveSkillHit + 1 < skillConfig.Hits.Count);
                bool justEndedCurrentHit = (src.BattleAttr.OriginatedRenderFrameId + src.Config.StartupFrames + src.Config.ActiveFrames == currRenderFrame.Id);

                if (inTheMiddleOfMeleeMultihitTransition && justEndedCurrentHit) {
                    // [WARNING] Different from Fireball, multihit of Melee would add a new "Bullet" to "nextRenderFrameBullets" for convenience of handling explosion! The bullet "dst" could also be exploding by reaching here!
                    var offenderNextFrame = (j < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[j] : nextRenderFrameNpcs[j - roomCapacity]);
                    offenderNextFrame.ActiveSkillHit = offender.ActiveSkillHit + 1;
                    if (offenderNextFrame.ActiveSkillHit < skillConfig.Hits.Count) {
                        // No need to worry about Mp consumption here, it was already paid at "0 == offenderNextFrame.ActiveSkillHit" in "_useSkill"
                        int xfac = (0 < offenderNextFrame.DirX ? 1 : -1);
                        addNewBulletToNextFrame(src.BattleAttr.OriginatedRenderFrameId, offender, offenderNextFrame, xfac, skillConfig, nextRenderFrameBullets, offenderNextFrame.ActiveSkillHit, src.BattleAttr.SkillId, ref bulletLocalIdCounter, ref bulletCnt, ref dummyHasLockVel);
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
                var offender = (j < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);

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

        private static void _moveAndInsertCharacterColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Vector[] effPushbacks, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, ref int colliderCnt, int iSt, int iEd, ILoggerBridge logger) {
            for (int i = iSt; i < iEd; i++) {
                var currCharacterDownsync = (i < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (i >= currRenderFrame.PlayersArr.Count && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);

                var chConfig = characters[currCharacterDownsync.SpeciesId];
                effPushbacks[i].X = 0;
                effPushbacks[i].Y = 0;

                int newVx = currCharacterDownsync.VirtualGridX + currCharacterDownsync.VelX + currCharacterDownsync.FrictionVelX, newVy = currCharacterDownsync.VirtualGridY + currCharacterDownsync.VelY;
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
                } else if (!currCharacterDownsync.InAir && currCharacterDownsync.SlipJumpTriggered) {
                    newVy -= (SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL << 2);
                }

                if (0 >= thatCharacterInNextFrame.Hp && 0 == thatCharacterInNextFrame.FramesToRecover) {
                    // Revive from Dying
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

        private static void _calcCharacterMovementPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPusbackEnabled, Collider[] dynamicRectangleColliders, int iSt, int iEd, FrameRingBuffer<Collider> residueCollided, ref BattleResult battleResult, RdfPushbackFrameLog? currPushbackFrameLog, bool pushbackFrameLogEnabled, ILoggerBridge logger) {
            // Calc pushbacks for each player (after its movement) w/o bullets
            int primaryHardOverlapIndex;
            for (int i = iSt; i < iEd; i++) {
                primaryOverlapResult.reset();
                var currCharacterDownsync = (i < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (i >= currRenderFrame.PlayersArr.Count && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
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

                if (softPusbackEnabled && Dying != currCharacterDownsync.CharacterState && false == currCharacterDownsync.OmitPushback) {
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
                        var v2 = bCollider.Data as TrapColliderAttr;
                        if (null != v2 && v2.ProvidesEscape && currCharacterDownsync.JoinIndex <= roomCapacity) {
                            var (escaped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                            // Currently only allowing "Player" to win.
                            if (escaped) {
                                battleResult.WinnerJoinIndex = currCharacterDownsync.JoinIndex;
                                return;
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
                                case OnWallIdle1:
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

        private static void _calcBulletCollisions(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, Collision collision, Collider[] dynamicRectangleColliders, int iSt, int iEd, ILoggerBridge logger) {
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
                var offender = (j < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[j] : currRenderFrame.NpcsArr[j - roomCapacity]);
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

                            if (0 >= atkedCharacterInNextFrame.Hp) {
                                // [WARNING] We don't have "dying in air" animation for now, and for better graphical recognition, play the same dying animation even in air
                                // If "atkedCharacterInCurrFrame" took multiple bullets in the same renderFrame, where a bullet in the middle of the set made it DYING, then all consecutive bullets would just take it into this small block again!
                                atkedCharacterInNextFrame.Hp = 0;
                                atkedCharacterInNextFrame.VelX = 0; // yet no need to change "VelY" because it could be falling
                                atkedCharacterInNextFrame.CharacterState = Dying;
                                atkedCharacterInNextFrame.FramesToRecover = DYING_FRAMES_TO_RECOVER;
                            } else {
                                // [WARNING] Deliberately NOT assigning to "atkedCharacterInNextFrame.X/Y" for avoiding the calculation of pushbacks in the current renderFrame.
                                if (false == atkedCharacterInCurrFrame.OmitPushback && BlownUp1 != oldNextCharacterState) {
                                    var (pushbackVelX, pushbackVelY) = (xfac * bullet.Config.PushbackVelX, bullet.Config.PushbackVelY);
                                    atkedCharacterInNextFrame.VelX = pushbackVelX;
                                    atkedCharacterInNextFrame.VelY = pushbackVelY;
                                }

                                bool shouldOmitStun = ((0 >= bullet.Config.HitStunFrames) || (atkedCharacterInCurrFrame.OmitPushback));
                                if (false == shouldOmitStun) {
                                    if (bullet.Config.BlowUp) {
                                        atkedCharacterInNextFrame.CharacterState = BlownUp1;
                                    } else if (BlownUp1 != oldNextCharacterState) {
                                        atkedCharacterInNextFrame.CharacterState = Atked1;
                                    }
                                    int oldFramesToRecover = atkedCharacterInNextFrame.FramesToRecover;
                                    if (bullet.Config.HitStunFrames > oldFramesToRecover) {
                                        atkedCharacterInNextFrame.FramesToRecover = bullet.Config.HitStunFrames;
                                    }
                                }
                            }
                            break;
                        case Bullet v4:
                            if (!COLLIDABLE_PAIRS.Contains(bullet.Config.CollisionTypeMask | v4.Config.CollisionTypeMask)) {
                                break;
                            }
                            if (bullet.BattleAttr.TeamId == v4.BattleAttr.TeamId) continue;
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

        private static void _processEffPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Trap> nextRenderFrameTraps, Vector[] effPushbacks, Collider[] dynamicRectangleColliders, int trapColliderCntOffset, int bulletColliderCntOffset, int colliderCnt, ILoggerBridge logger) {
            for (int i = 0; i < currRenderFrame.PlayersArr.Count + currRenderFrame.NpcsArr.Count; i++) {
                var currCharacterDownsync = (i < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (i >= currRenderFrame.PlayersArr.Count && TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);

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

                if (!colliderAttr.ProvidesHardPushback) continue;
                var trapInNextRenderFrame = nextRenderFrameTraps[colliderAttr.TrapLocalId];
                // Update "virtual grid position"
                var (nextColliderAttrVx, nextColliderAttrVy) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);
                trapInNextRenderFrame.VirtualGridX = nextColliderAttrVx - colliderAttr.HitboxOffsetX;
                trapInNextRenderFrame.VirtualGridY = nextColliderAttrVy - colliderAttr.HitboxOffsetY;
            }
        }

        public static bool isBattleResultSet(BattleResult battleResult) {
            return (MAGIC_JOIN_INDEX_DEFAULT != battleResult.WinnerJoinIndex);
        }

        public static void resetBattleResult(ref BattleResult battleResult) {
            battleResult.WinnerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT;
        }

        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, ref SatResult primaryOverlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, bool softPushbackEnabled, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder, FrameRingBuffer<Collider> residueCollided, Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, List<Collider> completelyStaticTrapColliders, ref BattleResult battleResult, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, bool pushbackFrameLogEnabled, ILoggerBridge logger) {
            var (ok1, currRenderFrame) = renderBuffer.GetByFrameId(currRenderFrameId);
            if (!ok1 || null == currRenderFrame) {
                throw new ArgumentNullException(String.Format("Null currRenderFrame is not allowed in `Battle.Step` for currRenderFrameId={0}", currRenderFrameId));
            }
            
            if (isBattleResultSet(battleResult)) {
                return;
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
            var nextRenderFrameTraps = candidate.TrapsArr;
            // Make a copy first
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var src = currRenderFrame.PlayersArr[i];
                var chConfig = characters[src.SpeciesId];
                int framesToRecover = src.FramesToRecover - 1;
                int framesInChState = src.FramesInChState + 1;
                int framesInvinsible = src.FramesInvinsible - 1;
                if (framesToRecover < 0) {
                    framesToRecover = 0;
                }
                if (framesInvinsible < 0) {
                    framesInvinsible = 0;
                }
                int framesInPatrolCue = src.FramesInPatrolCue - 1;
                if (framesInPatrolCue < 0) {
                    framesInPatrolCue = 0;
                }
                int mp = src.Mp + chConfig.MpRegenRate;
                if (mp >= src.MaxMp) {
                    mp = src.MaxMp;
                }
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, 0, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, true, false, src.OnWallNormX, src.OnWallNormY, src.CapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, false, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, false, nextRenderFramePlayers[i]);
            }

            int npcCnt = 0;
            while (npcCnt < currRenderFrame.NpcsArr.Count && TERMINATING_PLAYER_ID != currRenderFrame.NpcsArr[npcCnt].Id) {
                var src = currRenderFrame.NpcsArr[npcCnt];
                var chConfig = characters[src.SpeciesId];
                int framesToRecover = src.FramesToRecover - 1;
                int framesInChState = src.FramesInChState + 1;
                int framesInvinsible = src.FramesInvinsible - 1;
                if (framesToRecover < 0) {
                    framesToRecover = 0;
                }
                if (framesInvinsible < 0) {
                    framesInvinsible = 0;
                }
                int framesInPatrolCue = src.FramesInPatrolCue - 1;
                if (framesInPatrolCue < 0) {
                    framesInPatrolCue = 0;
                }
                int mp = src.Mp + chConfig.MpRegenRate;
                if (mp >= src.MaxMp) {
                    mp = src.MaxMp;
                }
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, 0, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, true, false, src.OnWallNormX, src.OnWallNormY, src.CapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, false, false, src.CapturedByPatrolCue, framesInPatrolCue, src.BeatsCnt, src.BeatenCnt, mp, src.MaxMp, src.CollisionTypeMask, src.OmitGravity, src.OmitPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, false, nextRenderFrameNpcs[npcCnt]);
                npcCnt++;
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

            _processPlayerInputs(currRenderFrame, inputBuffer, nextRenderFramePlayers, nextRenderFrameBullets, decodedInputHolder, prevDecodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _moveAndInsertCharacterColliders(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, 0, roomCapacity + npcCnt, logger);

            _processNpcInputs(currRenderFrame, roomCapacity, nextRenderFrameNpcs, nextRenderFrameBullets, dynamicRectangleColliders, colliderCnt, collision, collisionSys, ref overlapResult, decodedInputHolder, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);

            int trapColliderCntOffset = colliderCnt;
            _moveAndInsertDynamicTrapColliders(currRenderFrame, nextRenderFrameTraps, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, trapColliderCntOffset, trapLocalIdToColliderAttrs, logger);

            _calcCharacterMovementPushbacks(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, 0, roomCapacity + npcCnt, residueCollided, ref battleResult, currRdfPushbackFrameLog, pushbackFrameLogEnabled, logger);

            int bulletColliderCntOffset = colliderCnt;
            _insertFromEmissionDerivedBullets(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, ref nextRenderFrameBulletLocalIdCounter, ref bulletCnt, logger);
            _insertBulletColliders(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, currRenderFrame.Bullets, nextRenderFrameBullets, dynamicRectangleColliders, ref colliderCnt, collisionSys, ref bulletCnt, logger);

            _calcBulletCollisions(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, collision, dynamicRectangleColliders, bulletColliderCntOffset, colliderCnt, logger);

            _calcDynamicTrapMovementCollisions(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, ref overlapResult, ref primaryOverlapResult, collision, effPushbacks, hardPushbackNormsArr, decodedInputHolder, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, residueCollided, logger);

            _calcCompletelyStaticTrapDamage(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, collision, completelyStaticTrapColliders, logger);

            _processEffPushbacks(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, nextRenderFrameTraps, effPushbacks, dynamicRectangleColliders, trapColliderCntOffset, bulletColliderCntOffset, colliderCnt, logger);

            for (int i = 0; i < colliderCnt; i++) {
                Collider dynamicCollider = dynamicRectangleColliders[i];
                if (null == dynamicCollider.Space) {
                    throw new ArgumentNullException("Null dynamicCollider.Space is not allowed in `Step`!");
                }
                dynamicCollider.Space.RemoveSingle(dynamicCollider);
            }

            candidate.Id = nextRenderFrameId;
            candidate.BulletLocalIdCounter = nextRenderFrameBulletLocalIdCounter;
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
                case OnWallIdle1:
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
    }
}
