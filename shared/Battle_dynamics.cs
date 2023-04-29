using System;
using pbc = Google.Protobuf.Collections;
using static shared.CharacterState;
using System.Collections.Generic;
using System.IO;
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

        public static bool EqualInputLists(pbc.RepeatedField<ulong> lhs, pbc.RepeatedField<ulong> rhs) {
            if (null == lhs || null == rhs) return false;
            if (lhs.Count != rhs.Count) return false;
            for (int i = 0; i < lhs.Count; i++) {
                if (lhs[i] == rhs[i]) continue;
                return false;
            }
            return true;
        }

        public static bool EqualInputLists(pbc.RepeatedField<ulong> lhs, ulong[] rhs) {
            if (null == lhs) return false;
            if (lhs.Count != rhs.Length) return false;
            for (int i = 0; i < lhs.Count; i++) {
                if (lhs[i] == rhs[i]) continue;
                return false;
            }
            return true;
        }

        public static bool UpdateInputFrameInPlaceUponDynamics(int inputFrameId, int roomCapacity, ulong confirmedList, pbc::RepeatedField<ulong> inputList, int[] lastIndividuallyConfirmedInputFrameId, ulong[] lastIndividuallyConfirmedInputList, int toExcludeJoinIndex) {
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

        private static (int, bool, int, int) _derivePlayerOpPattern(CharacterDownsync currCharacterDownsync, RoomDownsyncFrame currRenderFrame, CharacterConfig chConfig, FrameRingBuffer<InputFrameDownsync> inputBuffer, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
            // returns (patternId, jumpedOrNot, effectiveDx, effectiveDy)
            int delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrame.Id);
            int delayedInputFrameIdForPrevRdf = ConvertToDelayedInputFrameId(currRenderFrame.Id - 1);

            if (0 >= delayedInputFrameId) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            if (noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            var (ok, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            if (!ok || null == delayedInputFrameDownsync) {
                throw new ArgumentNullException(String.Format("InputFrameDownsync for delayedInputFrameId={0} is null!", delayedInputFrameId));
            }
            var delayedInputList = delayedInputFrameDownsync.InputList;

            pbc::RepeatedField<ulong>? delayedInputListForPrevRdf = null;
            if (0 < delayedInputFrameIdForPrevRdf) {
                var (_, delayedInputFrameDownsyncForPrevRdf) = inputBuffer.GetByFrameId(delayedInputFrameIdForPrevRdf);
                if (null != delayedInputFrameDownsyncForPrevRdf) {
                    delayedInputListForPrevRdf = delayedInputFrameDownsyncForPrevRdf.InputList;
                }
            }

            bool jumpedOrNot = false;
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
                    } else if (!inAirSet.Contains(currCharacterDownsync.CharacterState)) {
                        jumpedOrNot = true;
                    } else if (OnWall == currCharacterDownsync.CharacterState) {
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

            return (patternId, jumpedOrNot, effDx, effDy);
        }

        private static (int, bool, int, int) deriveNpcOpPattern(CharacterDownsync currCharacterDownsync, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, Collider[] dynamicRectangleColliders, ref int colliderCnt, CollisionSpace collisionSys, Collision collision) {
            // returns (patternId, jumpedOrNot, effectiveDx, effectiveDy)

            var aCollider = dynamicRectangleColliders[roomCapacity + currCharacterDownsync.JoinIndex - 1];
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
            if (collided) {
                while (true) {
                    var (ok3, bCollider) = collision.PopFirstContactedCollider();
                    if (false == ok3 || null == bCollider) {
                        break;
                    }
                    bool isPatrolCue = false;
                    switch (bCollider.Data) {
                        case PatrolCue v3:
                            isPatrolCue = true;
                            break;
                        default:
                            break;
                    }
                    if (!isPatrolCue) {
                        // ignore bullets for this step
                        continue;
                    }
                }
            }

            // TODO: Create Npc visions (and remove before exiting this method), colllide with players and patrolCues to derive proper input
            if (0 < currCharacterDownsync.FramesToRecover) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            } else {
                return (PATTERN_ID_NO_OP, false, currCharacterDownsync.DirX, currCharacterDownsync.DirY);
            }
        }

        private static void _applyGravity(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame) {
            /*
             [WARNING] 
             Deliberately calling this method in "_processXxxInputs" instead of "_moveAndInsertCharacterColliders", because it's important for the assignment "thatCharacterInNextFrame.VelY = chConfig.WallSlidingVelY" to be disabled upon jumping!
             */
            if (currCharacterDownsync.InAir) {
                // TODO: The current dynamics calculation has a bug. When "true == currCharacterDownsync.InAir" and the character lands on the intersecting edge of 2 parallel rectangles, the hardPushbacks are doubled.
                if (OnWall == currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.VelX += GRAVITY_X;
                    thatCharacterInNextFrame.VelY = chConfig.WallSlidingVelY;
                } else if (Dashing == currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.VelX += GRAVITY_X;
                } else {
                    thatCharacterInNextFrame.VelX += GRAVITY_X;
                    thatCharacterInNextFrame.VelY += GRAVITY_Y;
                }
            }
        }

        private static void _processPlayerInputs(RoomDownsyncFrame currRenderFrame, FrameRingBuffer<InputFrameDownsync> inputBuffer, RepeatedField<CharacterDownsync> nextRenderFramePlayers, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currCharacterDownsync = currRenderFrame.PlayersArr[i];
                var thatCharacterInNextFrame = nextRenderFramePlayers[i];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, effDx, effDy) = _derivePlayerOpPattern(currCharacterDownsync, currRenderFrame, chConfig, inputBuffer, decodedInputHolder, prevDecodedInputHolder);

                var skillId = FindSkillId(patternId, currCharacterDownsync, chConfig.SpeciesId);
                bool skillUsed = false;

                if (skills.ContainsKey(skillId)) {
                    var skillConfig = skills[skillId];
                    if (Dashing == skillConfig.BoundChState) {
                        // TODO: Currently only "Dashing" is processed in C# version, add processing of bullets (including collision) later!
                        thatCharacterInNextFrame.ActiveSkillId = skillId;
                        thatCharacterInNextFrame.ActiveSkillHit = 0;
                        thatCharacterInNextFrame.FramesToRecover = skillConfig.RecoveryFrames;

                        int xfac = 1;
                        if (0 > thatCharacterInNextFrame.DirX) {
                            xfac = -xfac;
                        }
                        bool hasLockVel = false;

                        // Hardcoded to use only the first hit for now
                        var bulletConfig = skillConfig.Hits[thatCharacterInNextFrame.ActiveSkillHit];

                        if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                            hasLockVel = true;
                            thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
                        }
                        if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                            hasLockVel = true;
                            thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
                        }

                        if (false == hasLockVel && false == currCharacterDownsync.InAir) {
                            thatCharacterInNextFrame.VelX = 0;
                        }
                        thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;

                        skillUsed = true;
                    }

                }

                if (skillUsed) {
                    continue; // Don't allow movement if skill is used
                }

                if (0 == currCharacterDownsync.FramesToRecover) {
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

                    if (!jumpedOrNot && !prevCapturedByInertia && !alignedWithInertia) {
                        thatCharacterInNextFrame.CapturedByInertia = true;
                        if (exactTurningAround) {
                            thatCharacterInNextFrame.CharacterState = Walking; // Most NPCs don't have TurnAround animation clip!
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
                            int xfac = 1;
                            if (0 > effDx) {
                                xfac = -xfac;
                            }
                            thatCharacterInNextFrame.DirX = effDx;
                            thatCharacterInNextFrame.DirY = effDy;
                            thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                            thatCharacterInNextFrame.CharacterState = Walking;
                        } else {
                            thatCharacterInNextFrame.CharacterState = Idle1;
                            thatCharacterInNextFrame.VelX = 0;
                        }

                        if (jumpedOrNot) {
                            // We haven't proceeded with "OnWall" calculation for "thatPlayerInNextFrame", thus use "currCharacterDownsync.OnWall" for checking
                            if (OnWall == currCharacterDownsync.CharacterState) {
                                int xfac = -1;
                                if (0 > currCharacterDownsync.OnWallNormX) {
                                    // Always jump to the opposite direction of wall inward norm
                                    xfac = -xfac;
                                }
                                thatCharacterInNextFrame.VelX = (xfac * chConfig.WallJumpingInitVelX);
                                thatCharacterInNextFrame.VelY = (chConfig.WallJumpingInitVelY);
                                thatCharacterInNextFrame.FramesToRecover = chConfig.WallJumpingFramesToRecover;
                            } else {
                                thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY;
                            }
                            thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                        } else {
                            _applyGravity(currCharacterDownsync, chConfig, thatCharacterInNextFrame);
                        }
                    }

                }
            }
        }

        private static void _processNpcInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Collider[] dynamicRectangleColliders, ref int colliderCnt, Collision collision, CollisionSpace collisionSys) {
            for (int i = roomCapacity; i < roomCapacity + currRenderFrame.NpcsArr.Count; i++) {
                var currCharacterDownsync = currRenderFrame.NpcsArr[i - roomCapacity];
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = nextRenderFrameNpcs[i - roomCapacity];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, effDx, effDy) = deriveNpcOpPattern(currCharacterDownsync, currRenderFrame, roomCapacity, chConfig, dynamicRectangleColliders, ref colliderCnt, collisionSys, collision);

                var skillId = FindSkillId(patternId, currCharacterDownsync, chConfig.SpeciesId);
                bool skillUsed = false;

                if (skills.ContainsKey(skillId)) {
                    var skillConfig = skills[skillId];
                    if (Dashing == skillConfig.BoundChState) {
                        // TODO: Currently only "Dashing" is processed in C# version, add processing of bullets (including collision) later!
                        thatCharacterInNextFrame.ActiveSkillId = skillId;
                        thatCharacterInNextFrame.ActiveSkillHit = 0;
                        thatCharacterInNextFrame.FramesToRecover = skillConfig.RecoveryFrames;

                        int xfac = 1;
                        if (0 > thatCharacterInNextFrame.DirX) {
                            xfac = -xfac;
                        }
                        bool hasLockVel = false;

                        // Hardcoded to use only the first hit for now
                        var bulletConfig = skillConfig.Hits[thatCharacterInNextFrame.ActiveSkillHit];

                        if (NO_LOCK_VEL != bulletConfig.SelfLockVelX) {
                            hasLockVel = true;
                            thatCharacterInNextFrame.VelX = xfac * bulletConfig.SelfLockVelX;
                        }
                        if (NO_LOCK_VEL != bulletConfig.SelfLockVelY) {
                            hasLockVel = true;
                            thatCharacterInNextFrame.VelY = bulletConfig.SelfLockVelY;
                        }

                        if (false == hasLockVel && false == currCharacterDownsync.InAir) {
                            thatCharacterInNextFrame.VelX = 0;
                        }
                        thatCharacterInNextFrame.CharacterState = skillConfig.BoundChState;

                        skillUsed = true;
                    }

                }

                if (skillUsed) {
                    continue; // Don't allow movement if skill is used
                }

                bool isWallJumping = (chConfig.OnWallEnabled && chConfig.WallJumpingInitVelX == Math.Abs(currCharacterDownsync.VelX));

                if (0 == currCharacterDownsync.FramesToRecover) {
                    // No inertia capture for Npcs 
                    if (0 != effDx) {
                        int xfac = 1;
                        if (0 > effDx) {
                            xfac = -xfac;
                        }
                        thatCharacterInNextFrame.DirX = effDx;
                        thatCharacterInNextFrame.DirY = effDy;

                        if (isWallJumping) {
                            thatCharacterInNextFrame.VelX = xfac * Math.Abs(currCharacterDownsync.VelX);
                        } else {
                            thatCharacterInNextFrame.VelX = xfac * currCharacterDownsync.Speed;
                        }
                        thatCharacterInNextFrame.CharacterState = Walking;
                    } else {
                        thatCharacterInNextFrame.CharacterState = Idle1;
                        thatCharacterInNextFrame.VelX = 0;
                    }

                    if (jumpedOrNot) {
                        thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY;
                        thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
                    } else {
                        _applyGravity(currCharacterDownsync, chConfig, thatCharacterInNextFrame);
                    }
                }
            }
        }

        private static void _moveAndInsertCharacterColliders(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Vector[] effPushbacks, CollisionSpace collisionSys, Collider[] dynamicRectangleColliders, ref int colliderCnt, int iSt, int iEd) {
            for (int i = iSt; i < iEd; i++) {
                var currCharacterDownsync = (i < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);

                var chConfig = characters[currCharacterDownsync.SpeciesId];
                effPushbacks[i].X = 0;
                effPushbacks[i].Y = 0;

                int newVx = currCharacterDownsync.VirtualGridX + currCharacterDownsync.VelX, newVy = currCharacterDownsync.VirtualGridY + currCharacterDownsync.VelY;

                if (0 >= thatCharacterInNextFrame.Hp && 0 == thatCharacterInNextFrame.FramesToRecover) {
                    // Revive from Dying
                    (newVx, newVy) = (currCharacterDownsync.RevivalVirtualGridX, currCharacterDownsync.RevivalVirtualGridY);

                    thatCharacterInNextFrame.CharacterState = GetUp1;
                    thatCharacterInNextFrame.FramesInChState = 0;
                    thatCharacterInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                    thatCharacterInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;

                    thatCharacterInNextFrame.Hp = currCharacterDownsync.MaxHp;
                    // Hardcoded initial character orientation/facing
                    if (0 == (thatCharacterInNextFrame.JoinIndex % 2)) {
                        thatCharacterInNextFrame.DirX = -2;
                        thatCharacterInNextFrame.DirY = 0;
                    } else {
                        thatCharacterInNextFrame.DirX = +2;
                        thatCharacterInNextFrame.DirY = 0;
                    }
                }

                float boxCx, boxCy, boxCw, boxCh;
                calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, newVx, newVy, out boxCx, out boxCy, out boxCw, out boxCh);
                Collider characterCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(characterCollider, boxCx, boxCy, boxCw, boxCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync); // the coords of all barrier boundaries are multiples of tileWidth(i.e. 16), by adding snapping y-padding when "landedOnGravityPushback" all "characterCollider.Y" would be a multiple of 1.0
                colliderCnt++;

                // Add to collision system
                collisionSys.AddSingle(characterCollider);
            }
        }

        private static void _calcPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref SatResult overlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Collider[] dynamicRectangleColliders, int iSt, int iEd) {
            // Calc pushbacks for each player (after its movement) w/o bullets
            for (int i = iSt; i < iEd; i++) {
                var currCharacterDownsync = (i < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                int hardPushbackCnt = calcHardPushbacksNorms(currCharacterDownsync, thatCharacterInNextFrame, aCollider, aShape, SNAP_INTO_PLATFORM_OVERLAP, effPushbacks[i], hardPushbackNormsArr[i], collision, ref overlapResult);

                bool landedOnGravityPushback = false;
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
                if (collided) {
                    while (true) {
                        var (ok3, bCollider) = collision.PopFirstContactedCollider();
                        if (false == ok3 || null == bCollider) {
                            break;
                        }
                        bool isBarrier = false, isAnotherPlayer = false, isBullet = false, isPatrolCue = false;
                        switch (bCollider.Data) {
                            case CharacterDownsync v1:
                                if (Dying == v1.CharacterState) {
                                    // ignore collision with dying player
                                    continue;
                                }
                                isAnotherPlayer = true;
                                break;
                            case Bullet v2:
                                isBullet = true;
                                break;
                            case PatrolCue v3:
                                isPatrolCue = true;
                                break;
                            default:
                                // By default it's a regular barrier, even if data is nil
                                isBarrier = true;
                                break;
                        }
                        if (isBullet || isPatrolCue) {
                            // ignore bullets for this step
                            continue;
                        }

                        ConvexPolygon bShape = bCollider.Shape;
                        var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, ref overlapResult);
                        if (!overlapped) {
                            continue;
                        }
                        var normAlignmentWithGravity = (overlapResult.OverlapX * 0 + overlapResult.OverlapY * (-1.0));
                        if (isAnotherPlayer) {
                            // [WARNING] The "zero overlap collision" might be randomly detected/missed on either frontend or backend, to have deterministic result we added paddings to all sides of a characterCollider. As each velocity component of (velX, velY) being a multiple of 0.5 at any renderFrame, each position component of (x, y) can only be a multiple of 0.5 too, thus whenever a 1-dimensional collision happens between players from [player#1: i*0.5, player#2: j*0.5, not collided yet] to [player#1: (i+k)*0.5, player#2: j*0.5, collided], the overlap becomes (i+k-j)*0.5+2*s, and after snapping subtraction the effPushback magnitude for each player is (i+k-j)*0.5, resulting in 0.5-multiples-position for the next renderFrame.
                            pushbackX = (overlapResult.OverlapMag - SNAP_INTO_PLATFORM_OVERLAP * 2) * overlapResult.OverlapX;
                            pushbackY = (overlapResult.OverlapMag - SNAP_INTO_PLATFORM_OVERLAP * 2) * overlapResult.OverlapY;
                        }
                        for (int k = 0; k < hardPushbackCnt; k++) {
                            Vector hardPushbackNorm = hardPushbackNormsArr[i][k];
                            float projectedMagnitude = pushbackX * hardPushbackNorm.X + pushbackY * hardPushbackNorm.Y;
                            if (isBarrier || (isAnotherPlayer && 0 > projectedMagnitude)) {
                                pushbackX -= projectedMagnitude * hardPushbackNorm.X;
                                pushbackY -= projectedMagnitude * hardPushbackNorm.Y;
                            }
                        }
                        effPushbacks[i].X += pushbackX;
                        effPushbacks[i].Y += pushbackY;

                        if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                            landedOnGravityPushback = true;
                        }
                    }
                }

                if (landedOnGravityPushback) {
                    thatCharacterInNextFrame.InAir = false;
                    bool fallStopping = (currCharacterDownsync.InAir && 0 >= currCharacterDownsync.VelY);
                    if (fallStopping) {
                        thatCharacterInNextFrame.VelY = 0;
                        thatCharacterInNextFrame.VelX = 0;
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
                                case OnWall:
                                    // [WARNING] To prevent bouncing due to abrupt change of collider shape, it's important that we check "currCharacterDownsync" instead of "thatPlayerInNextFrame" here!
                                    var halfColliderWidthDiff = 0;
                                    var halfColliderHeightDiff = currCharacterDownsync.ColliderRadius;
                                    var (_, halfColliderWorldHeightDiff) = VirtualGridToPolygonColliderCtr(halfColliderWidthDiff, halfColliderHeightDiff);
                                    effPushbacks[i].Y -= halfColliderWorldHeightDiff;
                                    break;
                            }
                            thatCharacterInNextFrame.CharacterState = Idle1;
                            thatCharacterInNextFrame.FramesToRecover = 0;
                        }
                    } else {
                        // landedOnGravityPushback not fallStopping, could be in LayDown or GetUp or Dying
                        if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                            if (Dying == thatCharacterInNextFrame.CharacterState) {
                                thatCharacterInNextFrame.VelY = 0;
                                thatCharacterInNextFrame.VelX = 0;
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
                            }
                        }
                    }
                }

                if (chConfig.OnWallEnabled) {
                    if (thatCharacterInNextFrame.InAir) {
                        // [WARNING] Sticking to wall MUST BE based on "InAir", otherwise we would get gravity reduction from ground up incorrectly!
                        if (!noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                            for (int k = 0; k < hardPushbackCnt; k++) {
                                var hardPushbackNorm = hardPushbackNormsArr[i][k];
                                float normAlignmentWithHorizon1 = (hardPushbackNorm.X * +1f);
                                float normAlignmentWithHorizon2 = (hardPushbackNorm.X * -1f);

                                if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1) {
                                    thatCharacterInNextFrame.OnWall = true;
                                    thatCharacterInNextFrame.OnWallNormX = +1;
                                    thatCharacterInNextFrame.OnWallNormY = 0;
                                    break;
                                }
                                if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2) {
                                    thatCharacterInNextFrame.OnWall = true;
                                    thatCharacterInNextFrame.OnWallNormX = -1;
                                    thatCharacterInNextFrame.OnWallNormY = 0;
                                    break;

                                }
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

        private static void _processEffPushbacks(RoomDownsyncFrame currRenderFrame, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFramePlayers, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, Vector[] effPushbacks, Collider[] dynamicRectangleColliders) {
            for (int i = 0; i < currRenderFrame.PlayersArr.Count + currRenderFrame.NpcsArr.Count; i++) {
                var currCharacterDownsync = (i < currRenderFrame.PlayersArr.Count ? currRenderFrame.PlayersArr[i] : currRenderFrame.NpcsArr[i - roomCapacity]);
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = (i < currRenderFrame.PlayersArr.Count ? nextRenderFramePlayers[i] : nextRenderFrameNpcs[i - roomCapacity]);

                int joinIndex = currCharacterDownsync.JoinIndex;
                Collider aCollider = dynamicRectangleColliders[i];
                // Update "virtual grid position"
                (thatCharacterInNextFrame.VirtualGridX, thatCharacterInNextFrame.VirtualGridY) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[i].X, aCollider.Y - effPushbacks[i].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);

                // Update "CharacterState"
                if (thatCharacterInNextFrame.InAir) {
                    CharacterState oldNextCharacterState = thatCharacterInNextFrame.CharacterState;
                    switch (oldNextCharacterState) {
                        case Idle1:
                        case Walking:
                        case TurnAround:
                            if (InAirIdle1ByJump == currCharacterDownsync.CharacterState) {
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

                if (thatCharacterInNextFrame.OnWall) {
                    switch (thatCharacterInNextFrame.CharacterState) {
                        case Walking:
                        case InAirIdle1ByJump:
                        case InAirIdle1NoJump:
                            bool hasBeenOnWallChState = (OnWall == currCharacterDownsync.CharacterState);
                            bool hasBeenOnWallCollisionResultForSameChState = (currCharacterDownsync.OnWall && MAGIC_FRAMES_TO_BE_ON_WALL <= thatCharacterInNextFrame.FramesInChState);
                            if (hasBeenOnWallChState || hasBeenOnWallCollisionResultForSameChState) {
                                thatCharacterInNextFrame.CharacterState = OnWall;
                            }
                            break;
                    }
                }

                // Reset "FramesInChState" if "CharacterState" is changed
                if (thatCharacterInNextFrame.CharacterState != currCharacterDownsync.CharacterState) {
                    thatCharacterInNextFrame.FramesInChState = 0;
                }

                // Remove any active skill if not attacking
                if (nonAttackingSet.Contains(thatCharacterInNextFrame.CharacterState)) {
                    thatCharacterInNextFrame.ActiveSkillId = NO_SKILL;
                    thatCharacterInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
                }
            }
        }

        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
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

            // [WARNING] On backend this function MUST BE called while "InputsBufferLock" is locked!
            var nextRenderFramePlayers = candidate.PlayersArr;
            var nextRenderFrameNpcs = candidate.NpcsArr;
            // Make a copy first
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var src = currRenderFrame.PlayersArr[i];
                int framesToRecover = src.FramesToRecover - 1;
                int framesInChState = src.FramesInChState + 1;
                int framesInvinsible = src.FramesInvinsible - 1;
                if (framesToRecover < 0) {
                    framesToRecover = 0;
                }
                if (framesInvinsible < 0) {
                    framesInvinsible = 0;
                }
                AssignToCharacterDownsync(src.Id, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, src.ColliderRadius, true, false, src.OnWallNormX, src.OnWallNormY, src.CapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, nextRenderFramePlayers[i]);
            }

            int j = 0;
            while (j < currRenderFrame.NpcsArr.Count && TERMINATING_PLAYER_ID != currRenderFrame.NpcsArr[j].Id) {
                var src = currRenderFrame.NpcsArr[j];
                int framesToRecover = src.FramesToRecover - 1;
                int framesInChState = src.FramesInChState + 1;
                int framesInvinsible = src.FramesInvinsible - 1;
                if (framesToRecover < 0) {
                    framesToRecover = 0;
                }
                if (framesInvinsible < 0) {
                    framesInvinsible = 0;
                }
                AssignToCharacterDownsync(src.Id, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, src.ColliderRadius, true, false, src.OnWallNormX, src.OnWallNormY, src.CapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, nextRenderFrameNpcs[j]);
                j++;
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
            int colliderCnt = 0;
            _processPlayerInputs(currRenderFrame, inputBuffer, nextRenderFramePlayers, decodedInputHolder, prevDecodedInputHolder);
            _moveAndInsertCharacterColliders(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, 0, roomCapacity);

            _moveAndInsertCharacterColliders(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, collisionSys, dynamicRectangleColliders, ref colliderCnt, roomCapacity, roomCapacity + j);
            _processNpcInputs(currRenderFrame, roomCapacity, nextRenderFrameNpcs, dynamicRectangleColliders, ref colliderCnt, collision, collisionSys);

            _calcPushbacks(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, ref overlapResult, collision, effPushbacks, hardPushbackNormsArr, dynamicRectangleColliders, 0, roomCapacity + j);

            _processEffPushbacks(currRenderFrame, roomCapacity, nextRenderFramePlayers, nextRenderFrameNpcs, effPushbacks, dynamicRectangleColliders);

            for (int i = 0; i < colliderCnt; i++) {
                Collider dynamicCollider = dynamicRectangleColliders[i];
                if (null == dynamicCollider.Space) {
                    throw new ArgumentNullException("Null dynamicCollider.Space is not allowed in `Step`!");
                }
                dynamicCollider.Space.RemoveSingle(dynamicCollider);
            }

            candidate.Id = nextRenderFrameId;
        }

        public static void AlignPolygon2DToBoundingBox(ConvexPolygon input) {
            // Transform again to put "anchor" at the "bottom-left point (w.r.t. world space)" of the bounding box for "resolv"
            float boundingBoxBLX = MAX_FLOAT32, boundingBoxBLY = MAX_FLOAT32;
            for (int i = 0; i < input.Points.Cnt; i++) {
                var (exists, p) = input.Points.GetByOffset(i);
                if (!exists || null == p) throw new ArgumentNullException("Unexpected null point in ConvexPolygon when calling `AlignPolygon2DToBoundingBox`#1!");

                boundingBoxBLX = Math.Min(p.X, boundingBoxBLX);
                boundingBoxBLY = Math.Min(p.Y, boundingBoxBLY);
            }

            // Now "input.Anchor" should move to "input.Anchor+boundingBoxBL", thus "boundingBoxBL" is also the value of the negative diff for all "input.Points"
            input.X += boundingBoxBLX;
            input.Y += boundingBoxBLY;
            for (int i = 0; i < input.Points.Cnt; i++) {
                var (exists, p) = input.Points.GetByOffset(i);
                if (!exists || null == p) throw new ArgumentNullException("Unexpected null point in ConvexPolygon when calling `AlignPolygon2DToBoundingBox`#2!");
                p.X -= boundingBoxBLX;
                p.Y -= boundingBoxBLY;
                boundingBoxBLX = Math.Min(p.X, boundingBoxBLX);
                boundingBoxBLY = Math.Min(p.Y, boundingBoxBLY);
            }
        }

        public static (float, float) TiledLayerPositionToCollisionSpacePosition(float tiledLayerX, float tiledLayerY, float spaceOffsetX, float spaceOffsetY) {
            return (tiledLayerX, spaceOffsetY + spaceOffsetY - tiledLayerY);
        }

        public static (float, float) CollisionSpacePositionToWorldPosition(float collisionSpaceX, float collisionSpaceY, float spaceOffsetX, float spaceOffsetY) {
            // [WARNING] This conversion is specifically added for Unity+SuperTiled2Unity
            return (collisionSpaceX, -spaceOffsetY - spaceOffsetY + collisionSpaceY);
        }

        public static void trimRdfInPlace(RoomDownsyncFrame rdf) {
            // Removed bullets with TERMINATING_ID
            while (null != rdf.Bullets && 0 < rdf.Bullets.Count && TERMINATING_BULLET_LOCAL_ID == rdf.Bullets[rdf.Bullets.Count - 1].BattleAttr.BulletLocalId) {
                rdf.Bullets.RemoveAt(rdf.Bullets.Count - 1);
            }
        }

        public static void trimIfdInPlace(InputFrameDownsync ifd) {
            // Removed bullets with TERMINATING_ID
            ifd.ConfirmedList = 0;
        }

        public static string stringifyCharacterDownsync(CharacterDownsync pd) {
            if (null == pd) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6}", pd.JoinIndex, pd.VirtualGridX, pd.VirtualGridY, pd.VelX, pd.VelY, pd.FramesToRecover, pd.InAir, pd.OnWall);
        }

        public static string stringifyBullet(Bullet bt) {
            if (null == bt) return "";
            return String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}", bt.BattleAttr.BulletLocalId, bt.BattleAttr.OriginatedRenderFrameId, bt.VirtualGridX, bt.VirtualGridY, bt.VelX, bt.VelY, bt.DirX, bt.DirY, bt.BlState, bt.FramesInBlState, bt.Config.HitboxSizeX, bt.Config.HitboxSizeY);
        }

        public static string stringifyRdf(RoomDownsyncFrame rdf) {
            var playerSb = new List<String>();
            for (int k = 0; k < rdf.PlayersArr.Count; k++) {
                playerSb.Add(stringifyCharacterDownsync(rdf.PlayersArr[k]));
            }

            var bulletSb = new List<String>();
            for (int k = 0; k < rdf.Bullets.Count; k++) {
                var bt = rdf.Bullets[k];
                if (null == bt || TERMINATING_BULLET_LOCAL_ID == bt.BattleAttr.BulletLocalId) break;
                bulletSb.Add(stringifyBullet(bt));
            }

            if (0 >= bulletSb.Count) {
                return String.Format("{{ id:{0}\nps:{1} }}", rdf.Id, String.Join(',', playerSb));
            } else {
                return String.Format("{{ id:{0}\nps:{1}\nbs:{2} }}", rdf.Id, String.Join(',', playerSb), String.Join(',', bulletSb));
            }
        }

        public static string stringifyIfd(InputFrameDownsync ifd, bool trimConfirmedList) {
            var inputListSb = new List<String>();
            for (int k = 0; k < ifd.InputList.Count; k++) {
                inputListSb.Add(String.Format("{0}", ifd.InputList[k]));
            }
            if (trimConfirmedList) {
                return String.Format("{{ ifId:{0},ipts:{1} }}", ifd.InputFrameId, String.Join(',', inputListSb));
            } else {
                return String.Format("{{ ifId:{0},ipts:{1},cfd:{2} }}", ifd.InputFrameId, String.Join(',', inputListSb), ifd.ConfirmedList);
            }
        }

        public static string stringifyFrameLog(FrameLog fl, bool trimConfirmedList) {
            // Why do we need an extra class definition of "FrameLog" while having methods "stringifyRdf" & "stringifyIfd"? That's because we might need put "FrameLog" on transmission, i.e. sending to backend upon battle stopped, thus a wrapper class would provide some convenience though not 100% necessary.
            return String.Format("{0}\n{1}", stringifyRdf(fl.Rdf), stringifyIfd(fl.ActuallyUsedIdf, trimConfirmedList));
        }

        public static void wrapUpFrameLogs(FrameRingBuffer<RoomDownsyncFrame> renderBuffer, FrameRingBuffer<InputFrameDownsync> inputBuffer, Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput, bool trimConfirmedList, string dirPath, string filename) {
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(dirPath, filename))) {
                for (int i = renderBuffer.StFrameId; i < renderBuffer.EdFrameId; i++) {
                    var (ok1, rdf) = renderBuffer.GetByFrameId(i);
                    if (!ok1 || null == rdf) {
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#1 rdf for i={0} doesn't exist! renderBuffer[StFrameId, EdFrameId)=[{1}, {2})", i, renderBuffer.StFrameId, renderBuffer.EdFrameId));
                    }
                    trimRdfInPlace(rdf);
                    InputFrameDownsync ifd;
                    if (!rdfIdToActuallyUsedInput.TryGetValue(i, out ifd)) {
                        if (i + 1 == renderBuffer.EdFrameId) {
                            // It's OK that "InputFrameDownsync for the latest RoomDownsyncFrame" HASN'T BEEN USED YET. 
                            outputFile.WriteLine(String.Format("[{0}]", stringifyRdf(rdf)));
                            break;
                        }
                        var j = ConvertToDelayedInputFrameId(i);
                        throw new ArgumentNullException(String.Format("wrapUpFrameLogs#2 ifd for i={0}, j={1} doesn't exist! renderBuffer[StFrameId, EdFrameId)=[{2}, {3}), inputBuffer[StFrameId, EdFrameId)=[{4}, {5})", i, j, renderBuffer.StFrameId, renderBuffer.EdFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId));
                    }
                    if (trimConfirmedList) {
                        trimIfdInPlace(ifd);
                    }
                    var frameLog = new FrameLog {
                        Rdf = rdf,
                        ActuallyUsedIdf = ifd
                    };
                    outputFile.WriteLine(String.Format("[{0}]", stringifyFrameLog(frameLog, trimConfirmedList)));
                }
            }
        }

        public static void calcCharacterBoundingBoxInCollisionSpace(CharacterDownsync characterDownsync, int newVx, int newVy, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(newVx, newVy);

            switch (characterDownsync.CharacterState) {
                case LayDown1:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(characterDownsync.ColliderRadius * 4, characterDownsync.ColliderRadius * 2);
                    break;
                case BlownUp1:
                case InAirIdle1NoJump:
                case InAirIdle1ByJump:
                case OnWall:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(characterDownsync.ColliderRadius * 2, characterDownsync.ColliderRadius * 2);
                    break;
                default:
                    (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(characterDownsync.ColliderRadius * 2, characterDownsync.ColliderRadius * 4);
                    break;
            }
        }
    }
}
