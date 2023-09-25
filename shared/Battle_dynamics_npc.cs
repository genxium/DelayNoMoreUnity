using System;
using Google.Protobuf.Collections;

namespace shared {
    public partial class Battle {
        private static bool frontOpponentReachableByDragonPunch(CharacterDownsync currCharacterDownsync, Collider aCollider, Collider bCollider, float absColliderDx, float colliderDy, float absColliderDy, CharacterConfig opponentChConfig) {
            int yfac = (0 < colliderDy ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            // No need to calculate the exact bounding box of opponent based on ChState, this is just an estimation.
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_SWORDMAN:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(SwordManDragonPunchPrimerBullet.HitboxOffsetX + (opponentChConfig.ShrinkedSizeX >> 1), yfac * SwordManDragonPunchPrimerBullet.HitboxOffsetY + (opponentChConfig.ShrinkedSizeX >> 1));
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((SwordManDragonPunchPrimerBullet.HitboxSizeX >> 1), (SwordManDragonPunchPrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_FIRESWORDMAN:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(FireSwordManDragonPunchPrimerBullet.HitboxOffsetX + (opponentChConfig.ShrinkedSizeX >> 1), yfac * FireSwordManDragonPunchPrimerBullet.HitboxOffsetY + (opponentChConfig.ShrinkedSizeX >> 1));
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManDragonPunchPrimerBullet.HitboxSizeX >> 1), (FireSwordManDragonPunchPrimerBullet.HitboxSizeY >> 1));
                    break;
                default:
                    return false;
            }
            return (boxCx + boxCwHalf >= 0.5f*absColliderDx) && (0.1f*aCollider.H < colliderDy && boxCy + boxChHalf > 0.2f*colliderDy);
        }

        private static bool frontOpponentReachableByMelee1(CharacterDownsync currCharacterDownsync, Collider aCollider, Collider bCollider, float absColliderDx, float colliderDy, float absColliderDy, CharacterConfig opponentChConfig) {
            int yfac = (0 < colliderDy ? 1 : -1);
            float boxCx, boxCy, boxCwHalf, boxChHalf;
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_SWORDMAN:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(SwordManMelee1PrimerBullet.HitboxOffsetX + (opponentChConfig.DefaultSizeX >> 1), yfac * SwordManMelee1PrimerBullet.HitboxOffsetY + (opponentChConfig.DefaultSizeY >> 1));
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((SwordManMelee1PrimerBullet.HitboxSizeX >> 1), (SwordManMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_FIRESWORDMAN:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(FireSwordManMelee1PrimerBullet.HitboxOffsetX + (opponentChConfig.DefaultSizeX >> 1), yfac * FireSwordManMelee1PrimerBullet.HitboxOffsetY + (opponentChConfig.DefaultSizeY >> 1));
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((FireSwordManMelee1PrimerBullet.HitboxSizeX >> 1), (FireSwordManMelee1PrimerBullet.HitboxSizeY >> 1));
                    break;
                case SPECIES_BULLWARRIOR:
                    (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(BullWarriorMelee1PrimaryBullet.HitboxOffsetX + (opponentChConfig.DefaultSizeX >> 1), yfac * BullWarriorMelee1PrimaryBullet.HitboxOffsetY + (opponentChConfig.DefaultSizeY >> 1));
                    (boxCwHalf, boxChHalf) = VirtualGridToPolygonColliderCtr((BullWarriorMelee1PrimaryBullet.HitboxSizeX >> 1), (BullWarriorMelee1PrimaryBullet.HitboxSizeY >> 1));
                    break;
                default:
                    return false;
            }
            return (boxCx + boxCwHalf >= absColliderDx) && (boxCy + boxChHalf >= absColliderDy);
        }

        private static bool frontOpponentReachableByFireball(CharacterDownsync currCharacterDownsync, Collider aCollider, Collider bCollider, float colliderDx, float colliderDy, CharacterConfig opponentChConfig) {
            switch (currCharacterDownsync.SpeciesId) {
                case SPECIES_FIRESWORDMAN:
                    return currCharacterDownsync.Mp >= FireSwordManFireballSkill.MpDelta;
                case SPECIES_BULLWARRIOR:
                    return currCharacterDownsync.Mp >= BullWarriorFireballSkill.MpDelta;
            }
            return false;
        }

        private static void findHorizontallyClosestCharacterCollider(CharacterDownsync currCharacterDownsync, Collider aCollider, Collision collision, ref SatResult overlapResult, out Collider? res1, out CharacterDownsync? res2) {
            res1 = null;
            res2 = null;

            // [WARNING] Finding only the closest non-self character to react to for avoiding any randomness. 
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
            if (!collided) return;

            float minAbsColliderDx = MAX_FLOAT32;
            float minAbsColliderDy = MAX_FLOAT32;

            ConvexPolygon aShape = aCollider.Shape;
            while (true) {
                var (ok3, bCollider) = collision.PopFirstContactedCollider();
                if (false == ok3 || null == bCollider) {
                    break;
                }

                CharacterDownsync? v3 = bCollider.Data as CharacterDownsync;
                if (null == v3) {
                    // Only check shape collision (which is relatively expensive) if it's CharacterDownsync 
                    continue;
                }

                if (!COLLIDABLE_PAIRS.Contains(v3.CollisionTypeMask | currCharacterDownsync.CollisionTypeMask)) {
                    continue;
                }

                if (v3.JoinIndex == currCharacterDownsync.JoinIndex || v3.BulletTeamId == currCharacterDownsync.BulletTeamId) {
                    continue;
                }

                if (invinsibleSet.Contains(v3.CharacterState) || 0 < v3.FramesInvinsible) continue; // Target is invinsible, nothing can be done

                ConvexPolygon bShape = bCollider.Shape;
                var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                if (!overlapped) {
                    continue;
                }

                // By now we're sure that it should react to the PatrolCue
                var colliderDx = (aCollider.X - bCollider.X);
                var absColliderDx = Math.Abs(colliderDx);
                if (absColliderDx > minAbsColliderDx) {
                    continue;
                }

                var colliderDy = (aCollider.Y - bCollider.Y);
                var absColliderDy = Math.Abs(colliderDy);
                if (absColliderDx == minAbsColliderDx && absColliderDy > minAbsColliderDy) {
                    continue;
                }
                minAbsColliderDx = absColliderDx;
                minAbsColliderDy = absColliderDy;
                res1 = bCollider;
                res2 = v3;
            }
        }
        private static int countNpcI(RepeatedField<CharacterDownsync> npcsArr) {
            int npcI = 0;
            while (npcI < npcsArr.Count && TERMINATING_PLAYER_ID != npcsArr[npcI].Id) npcI++;
            return npcI;
        }

        public static bool isNpcDeadToDisappear(CharacterDownsync currCharacterDownsync) {
            return (0 >= currCharacterDownsync.Hp && 0 >= currCharacterDownsync.FramesToRecover);
        }

        private static void findHorizontallyClosestPatrolCueCollider(CharacterDownsync currCharacterDownsync, Collider aCollider, Collision collision, ref SatResult overlapResult, out Collider? res1, out PatrolCue? res2) {
            res1 = null;
            res2 = null;

            // [WARNING] Finding only the closest patrol cue to react to for avoiding any randomness. 
            bool collided = aCollider.CheckAllWithHolder(0, 0, collision);
            if (!collided) return;

            float minAbsColliderDx = MAX_FLOAT32;
            float minAbsColliderDy = MAX_FLOAT32;

            ConvexPolygon aShape = aCollider.Shape;
            while (true) {
                var (ok3, bCollider) = collision.PopFirstContactedCollider();
                if (false == ok3 || null == bCollider) {
                    break;
                }

                PatrolCue? v3 = bCollider.Data as PatrolCue;
                if (null == v3) {
                    // Only check shape collision (which is relatively expensive) if it's PatrolCue 
                    continue;
                }

                if (!COLLIDABLE_PAIRS.Contains(v3.CollisionTypeMask | currCharacterDownsync.CollisionTypeMask)) {
                    continue;
                }

                ConvexPolygon bShape = bCollider.Shape;
                var (overlapped, _, _) = calcPushbacks(0, 0, aShape, bShape, false, ref overlapResult);
                if (!overlapped) {
                    continue;
                }

                // By now we're sure that it should react to the PatrolCue
                var colliderDx = (aCollider.X - bCollider.X);
                var absColliderDx = Math.Abs(colliderDx);
                if (absColliderDx > minAbsColliderDx) {
                    continue;
                }

                var colliderDy = (aCollider.Y - bCollider.Y);
                var absColliderDy = Math.Abs(colliderDy);
                if (absColliderDx == minAbsColliderDx && absColliderDy > minAbsColliderDy) {
                    continue;
                }
                minAbsColliderDx = absColliderDx;
                minAbsColliderDy = absColliderDy;
                res1 = bCollider;
                res2 = v3;
            }
        }

        private static (int, bool, int, int) deriveNpcOpPattern(CharacterDownsync currCharacterDownsync, RoomDownsyncFrame currRenderFrame, int roomCapacity, CharacterConfig chConfig, CharacterDownsync thatCharacterInNextFrame, Collider[] dynamicRectangleColliders, int colliderCnt, CollisionSpace collisionSys, Collision collision, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, ILoggerBridge logger) {
            // return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);

            //This function returns (patternId, jumpedOrNot, effectiveDx, effectiveDy)
            if (0 < currCharacterDownsync.FramesToRecover) {
                return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
            }

            int patternId = PATTERN_ID_NO_OP;
            bool jumpedOrNot = false;

            // By default keeps the movement aligned with current facing
            int effectiveDx = currCharacterDownsync.DirX;
            int effectiveDy = currCharacterDownsync.DirY;

            bool hasVisionReaction = false;
            bool hasEnemyBehindMe = false;
            var aCollider = dynamicRectangleColliders[currCharacterDownsync.JoinIndex - 1]; // already added to collisionSys
            if (!currCharacterDownsync.InAir) {
                // TODO: There's no InAir vision reaction yet.
                float visionCx, visionCy, visionCw, visionCh;
                calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);

                var visionCollider = dynamicRectangleColliders[colliderCnt];
                UpdateRectCollider(visionCollider, visionCx, visionCy, visionCw, visionCh, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, SNAP_INTO_PLATFORM_OVERLAP, 0, 0, currCharacterDownsync);
                collisionSys.AddSingle(visionCollider);

                Collider? bCollider;
                CharacterDownsync? v3;
                findHorizontallyClosestCharacterCollider(currCharacterDownsync, visionCollider, collision, ref overlapResult, out bCollider, out v3);

                if (null != bCollider && null != v3) {
                    var colliderDx = (bCollider.X - aCollider.X);
                    var colliderDy = (bCollider.Y - aCollider.Y);
                    if (0 > (colliderDx * currCharacterDownsync.DirX)) {
                        hasEnemyBehindMe = true;
                    } else {
                        var atkedChConfig = characters[v3.SpeciesId];
                        float absColliderDx = Math.Abs(colliderDx), absColliderDy = Math.Abs(colliderDy);
                        // Opponent is in front of me
                        if (frontOpponentReachableByDragonPunch(currCharacterDownsync, aCollider, bCollider, absColliderDx, colliderDy, absColliderDy, atkedChConfig)) {
                            patternId = PATTERN_UP_B;
                            hasVisionReaction = true;
                        } else if (frontOpponentReachableByMelee1(currCharacterDownsync, aCollider, bCollider, absColliderDx, colliderDy, absColliderDy, atkedChConfig)) {
                            patternId = PATTERN_B;
                            hasVisionReaction = true;
                        } else if (frontOpponentReachableByFireball(currCharacterDownsync, aCollider, bCollider, colliderDx, colliderDy, atkedChConfig)) {
                            patternId = PATTERN_DOWN_B;
                            hasVisionReaction = true;
                        }
                    }
                }

                collisionSys.RemoveSingle(visionCollider); // no need to increment "colliderCnt", the visionCollider is transient
                visionCollider.Data = null;
            }

            if (!hasVisionReaction && hasEnemyBehindMe) {
                effectiveDx = -effectiveDx;
                hasVisionReaction = true;
            }

            if (hasVisionReaction && PATTERN_ID_NO_OP != patternId) {
                // [WARNING] Even if "hasVisionReaction", if "PATTERN_ID_NO_OP == patternId", we still expect the NPC to make use of patrol cues to jump or turn around!
                thatCharacterInNextFrame.CapturedByPatrolCue = false;
                thatCharacterInNextFrame.FramesInPatrolCue = 0;
                thatCharacterInNextFrame.WaivingPatrolCueId = NO_PATROL_CUE_ID;
            } else if (!noOpSet.Contains(currCharacterDownsync.CharacterState)) {
                bool hasPatrolCueReaction = false;
                // [WARNING] The field "CharacterDownsync.FramesInPatrolCue" would also be re-purposed as "patrol cue collision waiving frames" by the logic here.
                Collider? bCollider;
                PatrolCue? v3;
                findHorizontallyClosestPatrolCueCollider(currCharacterDownsync, aCollider, collision, ref overlapResult, out bCollider, out v3); 
                
                if (null != bCollider && null != v3) {
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

                    do {
                        if (shouldWaivePatrolCueReaction) {
                            // [WARNING] It's difficult to move this "early return" block to be before the "shape overlap check" like that of the traps, because we need data from "decodedInputHolder". 
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
                        } else {
                            effectiveDx = decodedInputHolder.Dx;
                            effectiveDy = decodedInputHolder.Dy;
                            jumpedOrNot = (0 == currCharacterDownsync.FramesToRecover) && !inAirSet.Contains(currCharacterDownsync.CharacterState) && (0 < decodedInputHolder.BtnALevel);
                            hasPatrolCueReaction = true;
                            thatCharacterInNextFrame.CapturedByPatrolCue = false;
                            thatCharacterInNextFrame.FramesInPatrolCue = DEFAULT_PATROL_CUE_WAIVING_FRAMES; // re-purposed
                            thatCharacterInNextFrame.WaivingPatrolCueId = v3.Id;
                        }
                    } while (false);
                }

                if (false == hasVisionReaction && false == hasPatrolCueReaction && currCharacterDownsync.WaivingSpontaneousPatrol) {
                    return (PATTERN_ID_UNABLE_TO_OP, false, 0, 0);
                }
            }

            return (patternId, jumpedOrNot, effectiveDx, effectiveDy);
        }

        private static void _processNpcInputs(RoomDownsyncFrame currRenderFrame, int roomCapacity, int npcCnt, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, RepeatedField<Bullet> nextRenderFrameBullets, Collider[] dynamicRectangleColliders, int colliderCnt, Collision collision, CollisionSpace collisionSys, ref SatResult overlapResult, InputFrameDecoded decodedInputHolder, ref int bulletLocalIdCounter, ref int bulletCnt, ILoggerBridge logger) {
            for (int i = roomCapacity; i < roomCapacity + npcCnt; i++) {
                var currCharacterDownsync = currRenderFrame.NpcsArr[i - roomCapacity];
                if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                var thatCharacterInNextFrame = nextRenderFrameNpcs[i - roomCapacity];
                var chConfig = characters[currCharacterDownsync.SpeciesId];
                var (patternId, jumpedOrNot, effDx, effDy) = deriveNpcOpPattern(currCharacterDownsync, currRenderFrame, roomCapacity, chConfig, thatCharacterInNextFrame, dynamicRectangleColliders, colliderCnt, collisionSys, collision, ref overlapResult, decodedInputHolder, logger);

                if (PATTERN_ID_UNABLE_TO_OP == patternId && 0 < currCharacterDownsync.FramesToRecover) {
                    continue;
                }

                thatCharacterInNextFrame.JumpTriggered = jumpedOrNot;

                bool usedSkill = _useSkill(patternId, currCharacterDownsync, chConfig, thatCharacterInNextFrame, ref bulletLocalIdCounter, ref bulletCnt, currRenderFrame, nextRenderFrameBullets);
                if (usedSkill) {
                    continue; // Don't allow movement if skill is used
                }

                _processInertiaWalking(currCharacterDownsync, thatCharacterInNextFrame, effDx, effDy, jumpedOrNot, chConfig, true, false);
            }
        }

        public static void calcNpcVisionBoxInCollisionSpace(CharacterDownsync characterDownsync, CharacterConfig chConfig, out float boxCx, out float boxCy, out float boxCw, out float boxCh) {
            if (noOpSet.Contains(characterDownsync.CharacterState)) {
                (boxCx, boxCy) = (0, 0);
                (boxCw, boxCh) = (0, 0);
                return;
            }

            int xfac = (0 < characterDownsync.DirX ? 1 : -1);
            (boxCx, boxCy) = VirtualGridToPolygonColliderCtr(characterDownsync.VirtualGridX + xfac * chConfig.VisionOffsetX, characterDownsync.VirtualGridY + chConfig.VisionOffsetY);
            (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(chConfig.VisionSizeX, chConfig.VisionSizeY);
        }
    }
}
