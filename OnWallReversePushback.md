In the nonpublic version, when a character is hit against a wall by melee skills, the offender will be pushed back instead to prevent unexpected infinite combos.

This feature requires a slight tweak to the definition of `CharacterDownsync.OnWall` field, such that we distinguish "collision-wise OnWall" and "special move:grabbing OnWall".

Here're the critical implementation snippets. Mind the determinism in details.

```c#
public static int calcHardPushbacksNormsForCharacter(...) {
    primaryTrap = null;
    float virtualGripToWall = 0.0f;
    if (OnWallIdle1 == currCharacterDownsync.CharacterState && 0 == thatCharacterInNextFrame.VelX && currCharacterDownsync.DirX == thatCharacterInNextFrame.DirX) {
        float xfac = 1.0f;
        if (0 > thatCharacterInNextFrame.DirX) {
            xfac = -xfac;
        }
        virtualGripToWall = xfac * currCharacterDownsync.Speed * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO;
    }
    int retCnt = 0;
    primaryOverlapIndex = -1;
    float primaryOverlapMag = float.MinValue;
    bool primaryIsWall = true; // Initialized to "true" to be updated even if there's only 1 vertical wall 
    residueCollided.Clear();
    bool collided = aCollider.CheckAllWithHolder(virtualGripToWall, 0, collision, COLLIDABLE_PAIRS);
    if (!collided) {
        //logger.LogInfo(String.Format("No collision object."));
        return retCnt;
    }

    // Get the largest to ensure determinism regardless of traversal order 
    float largestNormAlignmentWithHorizon1 = -MAX_FLOAT32;
    float largestNormAlignmentWithHorizon2 = -MAX_FLOAT32;

    while (true) {
        var (exists, bCollider) = collision.PopFirstContactedCollider();

        if (!exists || null == bCollider) {
            break;
        }
        int trapLocalId = TERMINATING_TRAP_ID;
        bool isCharacterFlying = (currCharacterDownsync.OmitGravity || chConfig.OmitGravity);
        bool isBarrier = false;
        bool onTrap = false;
        bool providesSlipJump = false;
        bool forcesCrouching = false;
        switch (bCollider.Data) {
            case CharacterDownsync v0:
            case Bullet v1:
            case PatrolCue v2:
                break;
            case Pickable v3:
                //logger.LogInfo(String.Format("Character encountered a pickable v3 = {0}", v3));
                break;
            case TrapColliderAttr v4:
                trapLocalId = v4.TrapLocalId;
                providesSlipJump = (v4.ProvidesSlipJump && !isCharacterFlying);
                forcesCrouching = (v4.ForcesCrouching && !isCharacterFlying); // Obviously you cannot crouch when flying...
                var trap = currRenderFrame.TrapsArr[trapLocalId];
                /*
                [WARNING]

                It's a bit tricky here, as currently "v4.ProvidesSlipJump" implies "v4.ProvidesHardPushback", but we want flying characters to be able to freely fly across "v4.ProvidesSlipJump & v4.ProvidesHardPushback" yet not "!v4.ProvidesSlipJump & v4.ProvidesHardPushback".
                */
                bool specialFlyingPass = (isCharacterFlying && v4.ProvidesSlipJump);
                onTrap = (v4.ProvidesHardPushback && TrapState.Tdestroyed != trap.TrapState && !specialFlyingPass); 
                isBarrier = onTrap;
                break;
            case TriggerColliderAttr v5:
                break;
            default:
                // By default it's a regular barrier, even if data is nil, note that Golang syntax of switch-case is kind of confusing, this "default" condition is met only if "!*CharacterDownsync && !*Bullet".
                isBarrier = true;
                break;
        }

        if (!isBarrier && !forcesCrouching) {
            if (residueCollided.Cnt >= residueCollided.N) {
                throw new ArgumentException(String.Format("residueCollided is already full! residueCollided.Cnt={0}, residueCollided.N={1}: trying to insert collider.Shape={4}, collider.Data={5}", residueCollided.Cnt, residueCollided.N, bCollider.Shape, bCollider.Data));
            }
            residueCollided.Put(bCollider);
            continue;
        }

        ConvexPolygon bShape = bCollider.Shape;

        var (overlapped, pushbackX, pushbackY) = calcPushbacks(0, 0, aShape, bShape, true, true, ref overlapResult);

        if (!overlapped) {
            continue;
        }

        if (overlapResult.OverlapMag < CLAMPABLE_COLLISION_SPACE_MAG) {
            /*
            [WARNING] 

            Kindly note that if I clamped by a larger threshold here, e.g. "overlapResult.OverlapMag < VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO", it would cause unintended bouncing between "DefaultSizeY & ShrinkedSizeY" on slopes!

            Moreover if I didn't clamp "pushbackX & pushbackY" here, there could be disagreed shape overlapping between backend and frontend, see comments around "shapeOverlappedOtherChCnt" in "Battle_dynamics". 
            */
            continue;
        }

        if (forcesCrouching && chConfig.CrouchingEnabled) {
            // [WARNING] If "forcesCrouching" but "false == chConfig.CrouchingEnabled", then the current "bCollider" should be deemed as a regular barrier!
            float characterTop = aCollider.Y + aCollider.H;
            float barrierTop = bCollider.Y + bCollider.H;
            if (characterTop < barrierTop) {
                thatCharacterInNextFrame.ForcedCrouching = true;
            }
            continue;
        }

        if (providesSlipJump) {
            /*
            Only provides hardPushbacks when 
            - the character is not uprising, and
            - the "bottom of the character" is higher than "top of the barrier rectangle - chConfig.SlipJumpThresHoldBelowTopFace". 
            */
            if (0 < currCharacterDownsync.VelY) {
                continue;
            }
            float characterBottom = aCollider.Y;
            float barrierTop = bCollider.Y + bCollider.H;
            if (characterBottom < (barrierTop - chConfig.SlipJumpThresHoldBelowTopFace)) {
                continue;
            }
        }

        float normAlignmentWithHorizon1 = (overlapResult.OverlapX * +1f);
        float normAlignmentWithHorizon2 = (overlapResult.OverlapX * -1f);
        bool isWall = (VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon1 || VERTICAL_PLATFORM_THRESHOLD < normAlignmentWithHorizon2);
        if (normAlignmentWithHorizon1 > largestNormAlignmentWithHorizon1) {
            largestNormAlignmentWithHorizon1 = normAlignmentWithHorizon1;
        }
        if (normAlignmentWithHorizon2 > largestNormAlignmentWithHorizon2) {
            largestNormAlignmentWithHorizon2 = normAlignmentWithHorizon2;
        }
        // [WARNING] At a corner with 1 vertical edge and 1 horizontal edge, make sure that the HORIZONTAL edge is chosen as primary!
        if (!isWall && primaryIsWall) {
            // Initial non-wall transition
            primaryOverlapIndex = retCnt;
            primaryOverlapMag = overlapResult.OverlapMag;
            overlapResult.cloneInto(ref primaryOverlapResult);
            primaryIsWall = isWall;
            if (onTrap) {
                primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
            } else {
                primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
            }
        } else if (isWall && !primaryIsWall) {
            // Just skip, once the character is checked to collide with a non-wall, any parasitic wall collision would be ignored...
        } else {
            // Same polarity
            if (overlapResult.OverlapMag > primaryOverlapMag) {
                primaryOverlapIndex = retCnt;
                primaryOverlapMag = overlapResult.OverlapMag;
                overlapResult.cloneInto(ref primaryOverlapResult);
                primaryIsWall = isWall;
                if (onTrap) {
                    primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                } else {
                    primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                }
            } else if (overlapResult.OverlapMag == primaryOverlapMag) {
                // [WARNING] Here's an important block for guaranteeing determinism regardless of traversal order.
                if (onTrap && null == primaryTrap) {       
                    // If currently straddling across a trap and a non-trap, with equal overlapMap, then the trap takes higher priority!
                    primaryOverlapIndex = retCnt;
                    primaryOverlapMag = overlapResult.OverlapMag;
                    overlapResult.cloneInto(ref primaryOverlapResult);
                    primaryIsWall = isWall;
                    primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                } else {
                    if ((overlapResult.AxisX < primaryOverlapResult.AxisX) || (overlapResult.AxisX == primaryOverlapResult.AxisX && overlapResult.AxisY < primaryOverlapResult.AxisY)) {
                        primaryOverlapIndex = retCnt;
                        primaryOverlapMag = overlapResult.OverlapMag;
                        overlapResult.cloneInto(ref primaryOverlapResult);
                        primaryIsWall = isWall;
                        if (onTrap) {
                            primaryTrap = currRenderFrame.TrapsArr[trapLocalId];
                        } else {
                            primaryTrap = null; // Don't forget to reset to null if the primary is not a trap
                        }
                    }
                }
            }
        }

        hardPushbacks[retCnt].X = pushbackX;
        hardPushbacks[retCnt].Y = pushbackY;

        retCnt++;
    }

    if (VERTICAL_PLATFORM_THRESHOLD < largestNormAlignmentWithHorizon1) {
        thatCharacterInNextFrame.OnWall = true;
        thatCharacterInNextFrame.OnWallNormX = +1;
        thatCharacterInNextFrame.OnWallNormY = 0;
    } else if (VERTICAL_PLATFORM_THRESHOLD < largestNormAlignmentWithHorizon2) {
        thatCharacterInNextFrame.OnWall = true;
        thatCharacterInNextFrame.OnWallNormX = -1;
        thatCharacterInNextFrame.OnWallNormY = 0;
    } else {
        thatCharacterInNextFrame.OnWall = false;
        thatCharacterInNextFrame.OnWallNormX = 0;
        thatCharacterInNextFrame.OnWallNormY = 0;
    }

    return retCnt;
}

private static void _calcCharacterMovementPushbacks(...) {
    //...
    if (!thatCharacterInNextFrame.OnWall) {
        // [WARNING] "false == thatCharacterInNextFrame.OnWall" by now implies primary is NOT wall!
        thatCharacterInNextFrame.OnWallNormX = 0;
        thatCharacterInNextFrame.OnWallNormY = 0;
    } else if (chConfig.OnWallEnabled && (null == primaryTrap || (null != primaryTrap && !primaryTrap.ConfigFromTiled.ProhibitsWallGrabbing))) {
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
    //...
}

private static void _moveAndInsertCharacterColliders(...) {
    //...
    bool jumpStarted = (i < roomCapacity ? thatCharacterInNextFrame.JumpStarted : currCharacterDownsync.JumpStarted); 
    if (jumpStarted) {
        // We haven't proceeded with "OnWall" calculation for "thatCharacterInNextFrame", thus use "currCharacterDownsync.OnWall" for checking
        if (currCharacterDownsync.OnWall && chConfig.OnWallEnabled && InAirIdle1ByWallJump == currCharacterDownsync.CharacterState) {
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
        } else if (currCharacterDownsync.InAir && InAirIdle2ByJump == thatCharacterInNextFrame.CharacterState) {
            thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY + effFrictionVelY;
            thatCharacterInNextFrame.CharacterState = InAirIdle2ByJump;
        } else if (currCharacterDownsync.SlipJumpTriggered) {
            newVy -= chConfig.SlipJumpCharacterDropVirtual;
            if (currCharacterDownsync.OmitGravity && !chConfig.OmitGravity && chConfig.JumpHoldingToFly) {               
                thatCharacterInNextFrame.CharacterState = InAirIdle1NoJump;
                thatCharacterInNextFrame.OmitGravity = false;
            }
        } else {
            thatCharacterInNextFrame.VelY = chConfig.JumpingInitVelY + effFrictionVelY;
            thatCharacterInNextFrame.CharacterState = InAirIdle1ByJump;
        }

        resetJumpStartupOrHolding(thatCharacterInNextFrame, false);
    }
    //...
}

public static void _processNextFrameJumpStartup(int rdfId, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame, CharacterConfig chConfig, ILoggerBridge logger) {
    //...
    if (currCharacterDownsync.OnWall && OnWallIdle1 == currCharacterDownsync.CharacterState) {
        thatCharacterInNextFrame.FramesToStartJump = (chConfig.ProactiveJumpStartupFrames >> 1);
        thatCharacterInNextFrame.CharacterState = InAirIdle1ByWallJump;
        thatCharacterInNextFrame.VelY = 0;
        thatCharacterInNextFrame.JumpHoldingRdfCnt = 1; // For continuity
    }
    //...
}

private static void _calcBulletCollisions(...) {
    //...
    if (!shouldOmitHitPushback && BlownUp1 != oldNextCharacterState) {
        var (pushbackVelX, pushbackVelY) = (xfac * bulletNextFrame.Config.PushbackVelX, bulletNextFrame.Config.PushbackVelY);
        // The traversal order of bullets is deterministic, thus the following assignment is deterministic regardless of the order of collision result popping.
        if (!atkedCharacterInNextFrame.OnWall) {    
            atkedCharacterInNextFrame.VelX = pushbackVelX;
            atkedCharacterInNextFrame.VelY = pushbackVelY;
        } else if (0 != atkedCharacterInNextFrame.OnWallNormX || 0 != atkedCharacterInNextFrame.OnWallNormY) {
            if (BulletType.Melee == bulletConfig.BType && 0 < pushbackVelX*atkedCharacterInNextFrame.OnWallNormX) {   
                offenderNextFrame.VelX = -(pushbackVelX >> 2);
            } else {
                atkedCharacterInNextFrame.VelX = pushbackVelX;
            }
            if (BulletType.Melee == bulletConfig.BType && 0 < pushbackVelY*atkedCharacterInNextFrame.OnWallNormY) {     
                offenderNextFrame.VelY = -(pushbackVelY >> 2);
            } else {
                atkedCharacterInNextFrame.VelY = pushbackVelY;
            }
        }
    }
    //...
}
```
