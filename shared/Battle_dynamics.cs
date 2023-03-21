using System;
using pbc = Google.Protobuf.Collections;
using static shared.CharacterState;

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

        private static (int, bool, int, int) deriveOpPattern(PlayerDownsync currPlayerDownsync, PlayerDownsync thatPlayerInNextFrame, RoomDownsyncFrame currRenderFrame, CharacterConfig chConfig, FrameRingBuffer<InputFrameDownsync> inputBuffer, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
            // returns (patternId, jumpedOrNot, effectiveDx, effectiveDy)
            int delayedInputFrameId = ConvertToDelayedInputFrameId(currRenderFrame.Id);
            int delayedInputFrameIdForPrevRdf = ConvertToDelayedInputFrameId(currRenderFrame.Id - 1);

            if (0 >= delayedInputFrameId) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            if (noOpSet.Contains(currPlayerDownsync.CharacterState)) {
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
            int joinIndex = currPlayerDownsync.JoinIndex;

            DecodeInput(delayedInputList[joinIndex - 1], decodedInputHolder);

            int effDx = 0, effDy = 0;

            if (null != delayedInputListForPrevRdf) {
                DecodeInput(delayedInputListForPrevRdf[joinIndex - 1], prevDecodedInputHolder);
            }

            // Jumping is partially allowed within "CapturedByInertia", but moving is only allowed when "0 == FramesToRecover" (constrained later in "ApplyInputFrameDownsyncDynamicsOnSingleRenderFrame")
            if (0 == currPlayerDownsync.FramesToRecover) {
                effDx = decodedInputHolder.Dx;
                effDy = decodedInputHolder.Dy;
            }

            int patternId = PATTERN_ID_NO_OP;
            var canJumpWithinInertia = (currPlayerDownsync.CapturedByInertia && ((chConfig.InertiaFramesToRecover >> 1) > currPlayerDownsync.FramesToRecover));
            if (0 == currPlayerDownsync.FramesToRecover || canJumpWithinInertia) {
                if (decodedInputHolder.BtnBLevel > prevDecodedInputHolder.BtnBLevel) {
                    if (chConfig.DashingEnabled && 0 > decodedInputHolder.Dy && Dashing != currPlayerDownsync.CharacterState) {
                        // Checking "DashingEnabled" here to allow jumping when dashing-disabled players pressed "DOWN + BtnB"
                        patternId = 5;
                    } else if (!inAirSet.Contains(currPlayerDownsync.CharacterState)) {
                        jumpedOrNot = true;
                    } else if (OnWall == currPlayerDownsync.CharacterState) {
                        jumpedOrNot = true;
                    }
                }
            }

            if (PATTERN_ID_NO_OP == patternId) {
                if (0 < decodedInputHolder.BtnALevel) {
                    if (decodedInputHolder.BtnALevel > prevDecodedInputHolder.BtnALevel) {
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


        public static void Step(FrameRingBuffer<InputFrameDownsync> inputBuffer, int currRenderFrameId, int roomCapacity, CollisionSpace collisionSys, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, ref SatResult overlapResult, Collision collision, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, bool[] jumpedOrNotList, Collider[] dynamicRectangleColliders, InputFrameDecoded decodedInputHolder, InputFrameDecoded prevDecodedInputHolder) {
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
                AssignToPlayerDownsync(src.Id, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.VelY, framesToRecover, framesInChState, src.ActiveSkillId, src.ActiveSkillHit, framesInvinsible, src.Speed, src.BattleState, src.CharacterState, src.JoinIndex, src.Hp, src.MaxHp, src.ColliderRadius, true, false, src.OnWallNormX, src.OnWallNormY, src.CapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, nextRenderFramePlayers[i]);
            }

            // 1. Process player inputs
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];
                var chConfig = characters[currPlayerDownsync.SpeciesId];
                var (patternId, jumpedOrNot, effDx, effDy) = deriveOpPattern(currPlayerDownsync, thatPlayerInNextFrame, currRenderFrame, chConfig, inputBuffer, decodedInputHolder, prevDecodedInputHolder);

                bool isWallJumping = (chConfig.OnWallEnabled && chConfig.WallJumpingInitVelX == Math.Abs(currPlayerDownsync.VelX));
                jumpedOrNotList[i] = jumpedOrNot;
                int joinIndex = currPlayerDownsync.JoinIndex;

                if (0 == currPlayerDownsync.FramesToRecover) {
                    bool prevCapturedByInertia = currPlayerDownsync.CapturedByInertia;
                    bool alignedWithInertia = true;
                    bool exactTurningAround = false;
                    bool stoppingFromWalking = false;
                    if (0 != effDx && 0 == thatPlayerInNextFrame.VelX) {
                        alignedWithInertia = false;
                    } else if (0 == effDx && 0 != thatPlayerInNextFrame.VelX) {
                        alignedWithInertia = false;
                        stoppingFromWalking = true;
                    } else if (0 > effDx * thatPlayerInNextFrame.VelX) {
                        alignedWithInertia = false;
                        exactTurningAround = true;
                    }

                    if (!jumpedOrNot && !isWallJumping && !prevCapturedByInertia && !alignedWithInertia) {
                        /*
                           [WARNING] A "turn-around", or in more generic direction schema a "change in direction" is a hurdle for our current "prediction+rollback" approach, yet applying a "FramesToRecover" for "turn-around" can alleviate the graphical inconsistence to a huge extent! For better operational experience, this is intentionally NOT APPLIED TO WALL JUMPING!

                           When "false == alignedWithInertia", we're GUARANTEED TO BE WRONG AT INPUT PREDICTION ON THE FRONTEND, but we COULD STILL BE RIGHT AT POSITION PREDICTION WITHIN "InertiaFramesToRecover" -- which together with "INPUT_DELAY_FRAMES" grants the frontend a big chance to be graphically consistent even upon wrong prediction!
                        */

                        thatPlayerInNextFrame.CapturedByInertia = true;
                        if (exactTurningAround) {
                            thatPlayerInNextFrame.CharacterState = TurnAround;
                            thatPlayerInNextFrame.FramesToRecover = chConfig.InertiaFramesToRecover;
                        } else if (stoppingFromWalking) {
                            thatPlayerInNextFrame.FramesToRecover = chConfig.InertiaFramesToRecover;
                        } else {
                            // Updates CharacterState and thus the animation to make user see graphical feedback asap.
                            thatPlayerInNextFrame.CharacterState = Walking;
                            thatPlayerInNextFrame.FramesToRecover = (chConfig.InertiaFramesToRecover >> 1);
                        }
                    } else {
                        thatPlayerInNextFrame.CapturedByInertia = false;
                        if (0 != effDx) {
                            int xfac = 1;
                            if (0 > effDx) {
                                xfac = -xfac;
                            }
                            thatPlayerInNextFrame.DirX = effDx;
                            thatPlayerInNextFrame.DirY = effDy;

                            if (isWallJumping) {
                                thatPlayerInNextFrame.VelX = xfac * Math.Abs(currPlayerDownsync.VelX);
                            } else {
                                thatPlayerInNextFrame.VelX = xfac * currPlayerDownsync.Speed;
                            }
                            thatPlayerInNextFrame.CharacterState = Walking;
                        } else {
                            thatPlayerInNextFrame.CharacterState = Idle1;
                            thatPlayerInNextFrame.VelX = 0;
                        }
                    }

                }
            }

            /*
                [WARNING]
               1. The dynamic colliders will all be removed from "Space" at the end of this function due to the need for being rollback-compatible.
               2. To achieve "zero gc" in "ApplyInputFrameDownsyncDynamicsOnSingleRenderFrame", I deliberately chose a collision system that doesn't use dynamic tree node alloc.
            */
            int colliderCnt = 0;

            // 2. Process player movement
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                int joinIndex = currPlayerDownsync.JoinIndex;
                var chConfig = characters[currPlayerDownsync.SpeciesId];
                effPushbacks[joinIndex - 1].X = 0;
                effPushbacks[joinIndex - 1].Y = 0;
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];

                // Reset playerCollider position from the "virtual grid position"
                int newVx = currPlayerDownsync.VirtualGridX + currPlayerDownsync.VelX, newVy = currPlayerDownsync.VirtualGridY + currPlayerDownsync.VelY;

                if (0 >= thatPlayerInNextFrame.Hp && 0 == thatPlayerInNextFrame.FramesToRecover) {
                    // Revive from Dying
                    (newVx, newVy) = (currPlayerDownsync.RevivalVirtualGridX, currPlayerDownsync.RevivalVirtualGridY);

                    thatPlayerInNextFrame.CharacterState = GetUp1;
                    thatPlayerInNextFrame.FramesInChState = 0;
                    thatPlayerInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                    thatPlayerInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;

                    thatPlayerInNextFrame.Hp = currPlayerDownsync.MaxHp;
                    // Hardcoded initial character orientation/facing
                    if (0 == (thatPlayerInNextFrame.JoinIndex % 2)) {
                        thatPlayerInNextFrame.DirX = -2;
                        thatPlayerInNextFrame.DirY = 0;
                    } else {
                        thatPlayerInNextFrame.DirX = +2;
                        thatPlayerInNextFrame.DirY = 0;
                    }
                }

                if (jumpedOrNotList[i]) {
                    // We haven't proceeded with "OnWall" calculation for "thatPlayerInNextFrame", thus use "currPlayerDownsync.OnWall" for checking
                    if (OnWall == currPlayerDownsync.CharacterState) {
                        if (0 < currPlayerDownsync.VelX * currPlayerDownsync.OnWallNormX) {
                            newVx -= currPlayerDownsync.VelX; // Cancel the alleged horizontal movement pointing to same direction of wall inward norm first
                        }
                        int xfac = -1;
                        if (0 > currPlayerDownsync.OnWallNormX) {
                            // Always jump to the opposite direction of wall inward norm
                            xfac = -xfac;
                        }
                        newVx += xfac * chConfig.WallJumpingInitVelX;
                        newVy += chConfig.WallJumpingInitVelY;
                        thatPlayerInNextFrame.VelX = (xfac * chConfig.WallJumpingInitVelX);
                        thatPlayerInNextFrame.VelY = (chConfig.WallJumpingInitVelY);
                        thatPlayerInNextFrame.FramesToRecover = chConfig.WallJumpingFramesToRecover;
                    } else {
                        thatPlayerInNextFrame.VelY = chConfig.JumpingInitVelY;
                        newVy += chConfig.JumpingInitVelY; // Immediately gets out of any snapping
                    }
                }

                var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(newVx, newVy);
                int colliderWidth = currPlayerDownsync.ColliderRadius * 2, colliderHeight = currPlayerDownsync.ColliderRadius * 4;

                switch (currPlayerDownsync.CharacterState) {
                    case LayDown1:
                        colliderWidth = currPlayerDownsync.ColliderRadius * 4;
                        colliderHeight = currPlayerDownsync.ColliderRadius * 2;
                        break;
                    case BlownUp1:
                    case InAirIdle1NoJump:
                    case InAirIdle1ByJump:
                    case OnWall:
                        colliderWidth = currPlayerDownsync.ColliderRadius * 2;
                        colliderHeight = currPlayerDownsync.ColliderRadius * 2;
                        break;
                }

                var (colliderWorldWidth, colliderWorldHeight) = VirtualGridToPolygonColliderCtr(colliderWidth, colliderHeight);

                Collider playerCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(playerCollider, collisionSpaceX, collisionSpaceY, colliderWorldWidth, colliderWorldHeight, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currPlayerDownsync); // the coords of all barrier boundaries are multiples of tileWidth(i.e. 16), by adding snapping y-padding when "landedOnGravityPushback" all "playerCollider.Y" would be a multiple of 1.0
                colliderCnt++;

                // Add to collision system
                collisionSys.AddSingle(playerCollider);

                if (currPlayerDownsync.InAir) {
                    if (OnWall == currPlayerDownsync.CharacterState && !jumpedOrNotList[i]) {
                        thatPlayerInNextFrame.VelX += GRAVITY_X;
                        thatPlayerInNextFrame.VelY = chConfig.WallSlidingVelY;
                    } else if (Dashing == currPlayerDownsync.CharacterState) {
                        thatPlayerInNextFrame.VelX += GRAVITY_X;
                    } else {
                        thatPlayerInNextFrame.VelX += GRAVITY_X;
                        thatPlayerInNextFrame.VelY += GRAVITY_Y;
                    }
                }
            }

            // 4. Calc pushbacks for each player (after its movement) w/o bullets
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                var chConfig = characters[currPlayerDownsync.SpeciesId];
                int joinIndex = currPlayerDownsync.JoinIndex;
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];
                int hardPushbackCnt = calcHardPushbacksNorms(joinIndex, currPlayerDownsync, thatPlayerInNextFrame, aCollider, aShape, SNAP_INTO_PLATFORM_OVERLAP, effPushbacks[joinIndex - 1], hardPushbackNormsArr[joinIndex - 1], collision, ref overlapResult);

                bool landedOnGravityPushback = false;
                bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
                if (collided) {
                    while (true) {
                        var (ok3, bCollider) = collision.PopFirstContactedCollider();
                        if (false == ok3 || null == bCollider) {
                            break;
                        }
                        bool isBarrier = false, isAnotherPlayer = false, isBullet = false;
                        switch (bCollider.Data) {
                            case PlayerDownsync v1:
                                if (Dying == v1.CharacterState) {
                                    // ignore collision with dying player
                                    continue;
                                }
                                isAnotherPlayer = true;
                                break;
                            case Bullet v2:
                                isBullet = true;
                                break;
                            default:
                                // By default it's a regular barrier, even if data is nil
                                isBarrier = true;
                                break;
                        }
                        if (isBullet) {
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
                            // [WARNING] The "zero overlap collision" might be randomly detected/missed on either frontend or backend, to have deterministic result we added paddings to all sides of a playerCollider. As each velocity component of (velX, velY) being a multiple of 0.5 at any renderFrame, each position component of (x, y) can only be a multiple of 0.5 too, thus whenever a 1-dimensional collision happens between players from [player#1: i*0.5, player#2: j*0.5, not collided yet] to [player#1: (i+k)*0.5, player#2: j*0.5, collided], the overlap becomes (i+k-j)*0.5+2*s, and after snapping subtraction the effPushback magnitude for each player is (i+k-j)*0.5, resulting in 0.5-multiples-position for the next renderFrame.
                            pushbackX = (overlapResult.OverlapMag - SNAP_INTO_PLATFORM_OVERLAP * 2) * overlapResult.OverlapX;
                            pushbackY = (overlapResult.OverlapMag - SNAP_INTO_PLATFORM_OVERLAP * 2) * overlapResult.OverlapY;
                        }
                        for (int k = 0; k < hardPushbackCnt; k++) {
                            Vector hardPushbackNorm = hardPushbackNormsArr[joinIndex - 1][k];
                            float projectedMagnitude = pushbackX * hardPushbackNorm.X + pushbackY * hardPushbackNorm.Y;
                            if (isBarrier || (isAnotherPlayer && 0 > projectedMagnitude)) {
                                pushbackX -= projectedMagnitude * hardPushbackNorm.X;
                                pushbackY -= projectedMagnitude * hardPushbackNorm.Y;
                            }
                        }
                        effPushbacks[joinIndex - 1].X += pushbackX;
                        effPushbacks[joinIndex - 1].Y += pushbackY;

                        if (SNAP_INTO_PLATFORM_THRESHOLD < normAlignmentWithGravity) {
                            landedOnGravityPushback = true;
                        }
                    }
                }

                if (landedOnGravityPushback) {
                    thatPlayerInNextFrame.InAir = false;
                    bool fallStopping = (currPlayerDownsync.InAir && 0 >= currPlayerDownsync.VelY);
                    if (fallStopping) {
                        thatPlayerInNextFrame.VelY = 0;
                        thatPlayerInNextFrame.VelX = 0;
                        if (Dying == thatPlayerInNextFrame.CharacterState) {
                            // No update needed for Dying
                        } else if (BlownUp1 == thatPlayerInNextFrame.CharacterState) {
                            thatPlayerInNextFrame.CharacterState = LayDown1;
                            thatPlayerInNextFrame.FramesToRecover = chConfig.LayDownFrames;
                        } else {
                            switch (currPlayerDownsync.CharacterState) {
                                case BlownUp1:
                                case InAirIdle1NoJump:
                                case InAirIdle1ByJump:
                                case OnWall:
                                    // [WARNING] To prevent bouncing due to abrupt change of collider shape, it's important that we check "currPlayerDownsync" instead of "thatPlayerInNextFrame" here!
                                    var halfColliderWidthDiff = 0;
                                    var halfColliderHeightDiff = currPlayerDownsync.ColliderRadius;
                                    var (_, halfColliderWorldHeightDiff) = VirtualGridToPolygonColliderCtr(halfColliderWidthDiff, halfColliderHeightDiff);
                                    effPushbacks[joinIndex - 1].Y -= halfColliderWorldHeightDiff;
                                    break;
                            }
                            thatPlayerInNextFrame.CharacterState = Idle1;
                            thatPlayerInNextFrame.FramesToRecover = 0;
                        }
                    } else {
                        // landedOnGravityPushback not fallStopping, could be in LayDown or GetUp or Dying
                        if (nonAttackingSet.Contains(thatPlayerInNextFrame.CharacterState)) {
                            if (Dying == thatPlayerInNextFrame.CharacterState) {
                                thatPlayerInNextFrame.VelY = 0;
                                thatPlayerInNextFrame.VelX = 0;
                            } else if (LayDown1 == thatPlayerInNextFrame.CharacterState) {
                                if (0 == thatPlayerInNextFrame.FramesToRecover) {
                                    thatPlayerInNextFrame.CharacterState = GetUp1;
                                    thatPlayerInNextFrame.FramesToRecover = chConfig.GetUpFramesToRecover;
                                }
                            } else if (GetUp1 == thatPlayerInNextFrame.CharacterState) {
                                if (0 == thatPlayerInNextFrame.FramesToRecover) {
                                    thatPlayerInNextFrame.CharacterState = Idle1;
                                    thatPlayerInNextFrame.FramesInvinsible = chConfig.GetUpInvinsibleFrames;
                                }
                            }
                        }
                    }
                }

                if (chConfig.OnWallEnabled) {
                    if (thatPlayerInNextFrame.InAir) {
                        // [WARNING] Sticking to wall MUST BE based on "InAir", otherwise we would get gravity reduction from ground up incorrectly!
                        if (!noOpSet.Contains(currPlayerDownsync.CharacterState)) {
                            for (int k = 0; k < hardPushbackCnt; k++) {
                                var hardPushbackNorm = hardPushbackNormsArr[joinIndex - 1][k];
                                float normAlignmentWithHorizon1 = (hardPushbackNorm.X * +1f);
                                float normAlignmentWithHorizon2 = (hardPushbackNorm.X * -1f);

                                if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1) {
                                    thatPlayerInNextFrame.OnWall = true;
                                    thatPlayerInNextFrame.OnWallNormX = +1;
                                    thatPlayerInNextFrame.OnWallNormY = 0;
                                    break;
                                }
                                if (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2) {
                                    thatPlayerInNextFrame.OnWall = true;
                                    thatPlayerInNextFrame.OnWallNormX = -1;
                                    thatPlayerInNextFrame.OnWallNormY = 0;
                                    break;

                                }
                            }
                        }
                    }
                    if (!thatPlayerInNextFrame.OnWall) {
                        thatPlayerInNextFrame.OnWallNormX = 0;
                        thatPlayerInNextFrame.OnWallNormY = 0;
                    }

                }
            }

            // 6. Get players out of stuck barriers if there's any
            for (int i = 0; i < currRenderFrame.PlayersArr.Count; i++) {
                var currPlayerDownsync = currRenderFrame.PlayersArr[i];
                int joinIndex = currPlayerDownsync.JoinIndex;
                Collider aCollider = dynamicRectangleColliders[i];
                ConvexPolygon aShape = aCollider.Shape;
                // Update "virtual grid position"
                var thatPlayerInNextFrame = nextRenderFramePlayers[i];
                (thatPlayerInNextFrame.VirtualGridX, thatPlayerInNextFrame.VirtualGridY) = PolygonColliderBLToVirtualGridPos(aCollider.X - effPushbacks[joinIndex - 1].X, aCollider.Y - effPushbacks[joinIndex - 1].Y, aCollider.W * 0.5f, aCollider.H * 0.5f, 0, 0, 0, 0, 0, 0);

                // Update "CharacterState"
                if (thatPlayerInNextFrame.InAir) {
                    CharacterState oldNextCharacterState = thatPlayerInNextFrame.CharacterState;
                    switch (oldNextCharacterState) {
                        case Idle1:
                        case Walking:
                        case TurnAround:
                            if (jumpedOrNotList[i] || InAirIdle1ByJump == currPlayerDownsync.CharacterState) {
                                thatPlayerInNextFrame.CharacterState = InAirIdle1ByJump;
                            } else {
                                thatPlayerInNextFrame.CharacterState = InAirIdle1NoJump;
                            }
                            break;
                        case Atk1:
                            thatPlayerInNextFrame.CharacterState = InAirAtk1;
                            // No inAir transition for ATK2/ATK3 for now
                            break;
                        case Atked1:
                            thatPlayerInNextFrame.CharacterState = InAirAtked1;
                            break;
                    }
                }

                if (thatPlayerInNextFrame.OnWall) {
                    switch (thatPlayerInNextFrame.CharacterState) {
                        case Walking:
                        case InAirIdle1ByJump:
                        case InAirIdle1NoJump:
                            bool hasBeenOnWallChState = (OnWall == currPlayerDownsync.CharacterState);
                            bool hasBeenOnWallCollisionResultForSameChState = (currPlayerDownsync.OnWall && MAGIC_FRAMES_TO_BE_ON_WALL <= thatPlayerInNextFrame.FramesInChState);
                            if (hasBeenOnWallChState || hasBeenOnWallCollisionResultForSameChState) {
                                thatPlayerInNextFrame.CharacterState = OnWall;
                            }
                            break;
                    }
                }

                // Reset "FramesInChState" if "CharacterState" is changed
                if (thatPlayerInNextFrame.CharacterState != currPlayerDownsync.CharacterState) {
                    thatPlayerInNextFrame.FramesInChState = 0;
                }

                // Remove any active skill if not attacking
                if (nonAttackingSet.Contains(thatPlayerInNextFrame.CharacterState)) {
                    thatPlayerInNextFrame.ActiveSkillId = NO_SKILL;
                    thatPlayerInNextFrame.ActiveSkillHit = NO_SKILL_HIT;
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

    }
}
