using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;

namespace shared {
    public partial class Battle {
        public static RepeatedField<Buff> defaultTemplateBuffList = new RepeatedField<Buff>(); 
        public static RepeatedField<Debuff> defaultTemplateDebuffList = new RepeatedField<Debuff>(); 
        public static Inventory defaultTemplateInventory = new Inventory(); 
        public static RepeatedField<BulletImmuneRecord> defaultTemplateBulletImmuneRecords = new RepeatedField<BulletImmuneRecord>(); 

        public static Collider NewConvexPolygonCollider(ConvexPolygon srcPolygon, float spaceOffsetX, float spaceOffsetY, int aMaxTouchingCellsCnt, object? data) {
            if (null == srcPolygon) throw new ArgumentNullException("Null srcPolygon is not allowed in `NewConvexPolygonCollider`");
            AlignPolygon2DToBoundingBox(srcPolygon);
            float w = 0, h = 0;

            for (int i = 0; i < srcPolygon.Points.Cnt; i++) {
                for (int j = 0; j < srcPolygon.Points.Cnt; j++) {
                    if (i == j) {
                        continue;
                    }
                    Vector? pi = srcPolygon.GetPointByOffset(i);
                    if (null == pi) {
                        throw new ArgumentNullException("Null pi is not allowed in `NewConvexPolygonCollider`!");
                    }
                    Vector? pj = srcPolygon.GetPointByOffset(j);
                    if (null == pj) {
                        throw new ArgumentNullException("Null pj is not allowed in `NewConvexPolygonCollider`!");
                    }
                    w = Math.Max(w, Math.Abs(pj.X - pi.X));
                    h = Math.Max(h, Math.Abs(pj.Y - pi.Y));
                }
            }

            return new Collider(srcPolygon.X + spaceOffsetX, srcPolygon.Y + spaceOffsetY, w, h, srcPolygon, aMaxTouchingCellsCnt, data, 0UL);
        }

        public static ConvexPolygon NewRectPolygon(float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding) {
            // [WARNING] (spaceOffsetX, spaceOffsetY) are taken into consideration while calling "NewConvexPolygonCollider" -- because "NewConvexPolygonCollider" might also be called for "polylines extracted from Tiled", it's more convenient to organized the codes this way.
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, 0, 0);
            float effW = leftPadding + w + rightPadding, effH = bottomPadding + h + topPadding;
            return new ConvexPolygon(blX, blY, new float[] {
                0, 0,
                0 + effW, 0,
                0 + effW, 0 + effH,
                0, 0 + effH
            });
        }

        public static void UpdateRectCollider(Collider collider, float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float spaceOffsetX, float spaceOffsetY, object data, ulong mask) {
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, spaceOffsetX, spaceOffsetY);

            float effW = leftPadding + w + rightPadding;
            float effH = bottomPadding + h + topPadding;
            (collider.X, collider.Y, collider.W, collider.H) = (blX, blY, effW, effH);
            collider.Shape.UpdateAsRectangle(0, 0, effW, effH);

            collider.Data = data;
            collider.Mask = mask;
        }

        public static bool UpdateWaveNpcKilledEvtSub(ulong publishingEvtMaskUponKilled, EvtSubscription nextExhaustEvtSub, ref ulong fulfilledEvtSubscriptionSetMask) {
            if (EVTSUB_NO_DEMAND_MASK != nextExhaustEvtSub.DemandedEvtMask) {
                nextExhaustEvtSub.FulfilledEvtMask |= publishingEvtMaskUponKilled;
                if (nextExhaustEvtSub.DemandedEvtMask == nextExhaustEvtSub.FulfilledEvtMask) {
                    fulfilledEvtSubscriptionSetMask |= (1ul << (nextExhaustEvtSub.Id-1));
                    return true;
                }
            }
            return false;
        }

        public static void AssignToEvtSubscription(int id, ulong demandedEvtMask, ulong fulfilledEvtMask, EvtSubscription dst) {
            dst.Id = id;
            dst.DemandedEvtMask = demandedEvtMask;
            dst.FulfilledEvtMask = fulfilledEvtMask;
        }

        public static void AssignToBuff(int speciesId, int stock, int originatedRenderFrameId, int origChSpeciesId, Buff dst) {
            dst.SpeciesId = speciesId; 
            dst.Stock = stock;
            dst.OriginatedRenderFrameId = originatedRenderFrameId;
            dst.OrigChSpeciesId = origChSpeciesId;
        }

        public static void AssignToDebuff(int speciesId, int stock, Debuff dst) {
            dst.SpeciesId = speciesId; 
            dst.Stock = stock;
        }

        public static void AssignToInventorySlot(InventorySlotStockType stockType, int quota, int framesToRecover, int defaultQuota, int defaultFramesToRecover, int buffSpeciesId, int skillId, InventorySlot dst) {
            dst.StockType = stockType; 
            dst.Quota = quota; 
            dst.FramesToRecover = framesToRecover; 
            dst.DefaultQuota = defaultQuota;
            dst.DefaultFramesToRecover = defaultFramesToRecover;
            dst.BuffSpeciesId = buffSpeciesId;
            dst.SkillId = skillId;
        }

        public static void AssignToBulletImmuneRecord(int bulletLocalId, int remainingLifetimeRdfCount, BulletImmuneRecord dst) {
            dst.BulletLocalId = bulletLocalId; 
            dst.RemainingLifetimeRdfCount = remainingLifetimeRdfCount; 
        }

        public static void AssignToCharacterDownsyncFromCharacterConfig(CharacterConfig chConfig, CharacterDownsync dst) {
            dst.SpeciesId = chConfig.SpeciesId;
            dst.OmitGravity = chConfig.OmitGravity;
            dst.OmitSoftPushback = chConfig.OmitSoftPushback;
            dst.RepelSoftPushback = chConfig.RepelSoftPushback;
            dst.RemainingAirJumpQuota = chConfig.DefaultAirJumpQuota;
            dst.RemainingAirDashQuota = chConfig.DefaultAirDashQuota;
            dst.Speed = chConfig.Speed;
        }

        public static void AssignToCharacterDownsync(int id, int speciesId, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int frictionVelX, int velY, int framesToRecover, int framesInChState, int activeSkillId, int activeSkillHit, int framesInvinsible, int speed, CharacterState characterState, int joinIndex, int hp, bool inAir, bool onWall, int onWallNormX, int onWallNormY, int framesCapturedByInertia, int bulletTeamId, int chCollisionTeamId, int revivalVirtualGridX, int revivalVirtualGridY, int revivalDirX, int revivalDirY, bool jumpTriggered, bool slipJumpTriggered, bool primarilyOnSlippableHardPushback, bool capturedByPatrolCue, int framesInPatrolCue, int beatsCnt, int beatenCnt, int mp, bool omitGravity, bool omitSoftPushback, bool repelSoftPushback, bool waivingSpontaneousPatrol, int waivingPatrolCueId, bool onSlope, bool forcedCrouching, bool newBirth, int lowerPartFramesInChState, bool jumpStarted, int framesToStartJump, RepeatedField<Buff> prevBuffList, RepeatedField<Debuff> prevDebuffList, Inventory? prevInventory, bool isRdfFrameElapsing, int publishingEvtSubIdUponKilled, ulong publishingEvtMaskUponKilled, int subscriptionId, bool jumpHolding, int btnBHoldingRdfCount, int remainingAirJumpQuota, int remainingAirDashQuota, int killedToDropConsumableSpeciesId, int killedToDropBuffSpeciesId, RepeatedField<BulletImmuneRecord> prevBulletImmuneRecords, CharacterDownsync dst) {
            dst.Id = id;
            dst.SpeciesId = speciesId;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.FrictionVelX = frictionVelX;
            dst.VelY = velY;
            dst.FramesToRecover = framesToRecover;
            dst.FramesInChState = framesInChState;
            dst.ActiveSkillId = activeSkillId;
            dst.ActiveSkillHit = activeSkillHit;
            dst.FramesInvinsible = framesInvinsible;
            dst.Speed = speed;
            dst.CharacterState = characterState;
            dst.JoinIndex = joinIndex;
            dst.Hp = hp;
            dst.InAir = inAir;
            dst.OnWall = onWall;
            dst.OnWallNormX = onWallNormX;
            dst.OnWallNormY = onWallNormY;
            dst.FramesCapturedByInertia = framesCapturedByInertia;
            dst.BulletTeamId = bulletTeamId;
            dst.ChCollisionTeamId = chCollisionTeamId;
            dst.RevivalVirtualGridX = revivalVirtualGridX;
            dst.RevivalVirtualGridY = revivalVirtualGridY;
            dst.RevivalDirX = revivalDirX;
            dst.RevivalDirY = revivalDirY;
            dst.JumpTriggered = jumpTriggered;
            dst.SlipJumpTriggered = slipJumpTriggered;
            dst.PrimarilyOnSlippableHardPushback = primarilyOnSlippableHardPushback; 

            dst.CapturedByPatrolCue = capturedByPatrolCue;
            dst.FramesInPatrolCue = framesInPatrolCue;

            dst.BeatsCnt = beatsCnt;
            dst.BeatenCnt = beatenCnt;

            dst.Mp = mp;

            dst.OmitGravity = omitGravity;
            dst.OmitSoftPushback = omitSoftPushback;
            dst.RepelSoftPushback = repelSoftPushback;
            dst.WaivingSpontaneousPatrol = waivingSpontaneousPatrol;
            dst.WaivingPatrolCueId = waivingPatrolCueId;

            dst.OnSlope = onSlope;
            dst.ForcedCrouching = forcedCrouching;
            
            dst.NewBirth = newBirth;
            dst.LowerPartFramesInChState = lowerPartFramesInChState;

            dst.JumpStarted = jumpStarted;
            dst.FramesToStartJump = framesToStartJump;
            dst.PublishingEvtSubIdUponKilled = publishingEvtSubIdUponKilled;
            dst.PublishingEvtMaskUponKilled = publishingEvtMaskUponKilled;
            dst.SubscriptionId = subscriptionId;
        
            dst.JumpHolding = jumpHolding;
            dst.BtnBHoldingRdfCount = btnBHoldingRdfCount;

            dst.RemainingAirJumpQuota = remainingAirJumpQuota;
            dst.RemainingAirDashQuota = remainingAirDashQuota;

            dst.KilledToDropConsumableSpeciesId = killedToDropConsumableSpeciesId;
            dst.KilledToDropBuffSpeciesId = killedToDropBuffSpeciesId;

            // [WARNING] When "defaultTemplateBuffList" is passed in, it's equivalent to just TERMINATING at the very beginning.
            int newBuffCnt = 0, prevBuffI = 0; 
            while (prevBuffI < prevBuffList.Count) {
                var cand = prevBuffList[prevBuffI++];
                if (TERMINATING_BUFF_SPECIES_ID == cand.SpeciesId) break; 
                var buffConfig  = buffConfigs[cand.SpeciesId];
                if (BuffStockType.Timed == buffConfig.StockType && isRdfFrameElapsing) {
                    int nextStock = cand.Stock - 1;
                    if (0 >= nextStock) {
                        if (noOpSet.Contains(characterState) || 0 >= framesToRecover) {
                            // [WARNING] It's very unnatural to transform back when the character is actively using a skill!
                            revertBuff(cand, dst);
                            continue;
                        }
                    }
                    AssignToBuff(cand.SpeciesId, nextStock, cand.OriginatedRenderFrameId, cand.OrigChSpeciesId, dst.BuffList[newBuffCnt]);
                } else {
                    AssignToBuff(cand.SpeciesId, cand.Stock, cand.OriginatedRenderFrameId, cand.OrigChSpeciesId, dst.BuffList[newBuffCnt]);
                }
                ++newBuffCnt;
            }
            if (newBuffCnt < dst.BuffList.Count) {
                AssignToBuff(TERMINATING_BUFF_SPECIES_ID, 0, TERMINATING_RENDER_FRAME_ID, SPECIES_NONE_CH, dst.BuffList[newBuffCnt]);
            }

            // [WARNING] When "defaultTemplateDebuffList" is passed in, it's equivalent to just TERMINATING at the very beginning.
            int newDebuffCnt = 0, prevDebuffI = 0; 
            while (prevDebuffI < prevDebuffList.Count) {
                var cand = prevDebuffList[prevDebuffI++];
                if (TERMINATING_DEBUFF_SPECIES_ID == cand.SpeciesId) break; 
                if (newDebuffCnt >= dst.DebuffList.Count) {
                    throw new ArgumentException("newDebuffCnt:" + newDebuffCnt + " is out of range while dst.DebuffList.Count:" + dst.DebuffList.Count);
                }
                var debuffConfig = debuffConfigs[cand.SpeciesId];
                if (BuffStockType.Timed == debuffConfig.StockType && isRdfFrameElapsing) {
                    int nextStock = cand.Stock - 1;
                    int nextSpeciesId = cand.SpeciesId;
                    if (0 >= nextStock) {
                        continue;
                    }
                    AssignToDebuff(nextSpeciesId, nextStock, dst.DebuffList[newDebuffCnt]);
                } else {
                    AssignToDebuff(cand.SpeciesId, cand.Stock, dst.DebuffList[newDebuffCnt]);
                }
                ++newDebuffCnt;
            }
            if (newDebuffCnt < dst.DebuffList.Count) {
                AssignToDebuff(TERMINATING_DEBUFF_SPECIES_ID, 0, dst.DebuffList[newDebuffCnt]);
            }

            if (null != prevInventory) {
                // [WARNING] When "defaultTemplateInventory" is passed in, it's equivalent to just TERMINATING at the very beginning.
                int newInventoryCnt = 0, prevInventoryI = 0;
                while (prevInventoryI < prevInventory.Slots.Count) {
                    var cand = prevInventory.Slots[prevInventoryI++];
                    if (InventorySlotStockType.NoneIv == cand.StockType) break;
                    if (InventorySlotStockType.TimedIv == cand.StockType && isRdfFrameElapsing) {
                        int nextFramesToRecover = cand.FramesToRecover - 1;
                        if (0 > nextFramesToRecover) nextFramesToRecover = 0;
                        AssignToInventorySlot(cand.StockType, cand.Quota, nextFramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, dst.Inventory.Slots[newInventoryCnt]);
                    } else if (InventorySlotStockType.TimedMagazineIv == cand.StockType && isRdfFrameElapsing) {
                        int nextFramesToRecover = cand.FramesToRecover - 1;
                        if (0 > nextFramesToRecover) nextFramesToRecover = 0;
                        if (0 == nextFramesToRecover && 1 == cand.FramesToRecover) {
                            AssignToInventorySlot(cand.StockType, cand.DefaultQuota, nextFramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, dst.Inventory.Slots[newInventoryCnt]);
                        } else {
                            AssignToInventorySlot(cand.StockType, cand.Quota, nextFramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, dst.Inventory.Slots[newInventoryCnt]);
                        }
                    } else {
                        AssignToInventorySlot(cand.StockType, cand.Quota, cand.FramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, dst.Inventory.Slots[newInventoryCnt]);
                    }
                    ++newInventoryCnt;
                }
                if (newInventoryCnt < dst.Inventory.Slots.Count) dst.Inventory.Slots[newInventoryCnt].StockType = InventorySlotStockType.NoneIv;
            }

            if (null != prevBulletImmuneRecords) {
                // [WARNING] When "defaultTemplateBulletImmuneRecords" is passed in, it's equivalent to just TERMINATING at the very beginning.
                int newCnt = 0, prevCnt = 0;
                while (prevCnt < prevBulletImmuneRecords.Count) {
                    var cand = prevBulletImmuneRecords[prevCnt++];
                    if (TERMINATING_BULLET_LOCAL_ID == cand.BulletLocalId) break;
                    if (isRdfFrameElapsing) {
                        if (0 >= cand.RemainingLifetimeRdfCount) {
                            continue;
                        }
                        int newRemainingLifetimeRdfCount = cand.RemainingLifetimeRdfCount - 1;
                        if (0 > newRemainingLifetimeRdfCount) newRemainingLifetimeRdfCount = 0;
                        AssignToBulletImmuneRecord(cand.BulletLocalId, newRemainingLifetimeRdfCount, dst.BulletImmuneRecords[newCnt]);
                    } else {
                        AssignToBulletImmuneRecord(cand.BulletLocalId, cand.RemainingLifetimeRdfCount, dst.BulletImmuneRecords[newCnt]);
                    }
                    ++newCnt;
                }
                if (newCnt < dst.BulletImmuneRecords.Count) dst.BulletImmuneRecords[newCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
            }
        }

        public static Bullet NewBullet(int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int teamId, BulletState blState, int framesInBlState) {
            return new Bullet {
                BlState = blState,
                FramesInBlState = framesInBlState,
                BattleAttr = new BulletBattleAttr {
                    BulletLocalId = bulletLocalId,
                    OriginatedRenderFrameId = originatedRenderFrameId,
                    OffenderJoinIndex = offenderJoinIndex,
                    TeamId = teamId
                },
                VirtualGridX = 0,
                VirtualGridY = 0,
                DirX = 0,
                DirY = 0,
                VelX = 0,
                VelY = 0
            };
        }

        public static void AssignToBullet(int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int teamId, BulletState blState, int framesInBlState, int origVx, int origVy, int vx, int vy, int dirX, int dirY, int velX, int velY, int activeSkillHit, int skillId, BulletConfig staticBulletConfig, int repeatQuotaLeft, int remainingHardPushbackBounceQuota, int targetCharacterJoinIndex, Bullet dst) {
            dst.BlState = blState;
            dst.FramesInBlState = framesInBlState;
            dst.Config = staticBulletConfig;

            dst.BattleAttr.BulletLocalId = bulletLocalId;
            dst.BattleAttr.OriginatedRenderFrameId = originatedRenderFrameId;
            dst.BattleAttr.OffenderJoinIndex = offenderJoinIndex;
            dst.BattleAttr.TeamId = teamId;
            dst.BattleAttr.ActiveSkillHit = activeSkillHit;
            dst.BattleAttr.SkillId = skillId;

            dst.OriginatedVirtualGridX = origVx;
            dst.OriginatedVirtualGridY = origVy;
            dst.VirtualGridX = vx;
            dst.VirtualGridY = vy;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;

            dst.RepeatQuotaLeft = repeatQuotaLeft;
            dst.RemainingHardPushbackBounceQuota = remainingHardPushbackBounceQuota;
            dst.TargetCharacterJoinIndex = targetCharacterJoinIndex;
        }

        public static void AssignToTrap(int trapLocalId, TrapConfig config, TrapConfigFromTiled configFromTiled, TrapState trapState, int framesInTrapState, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int velY, bool isCompletelyStatic, bool capturedByPatrolCue, int framesInPatrolCue, bool waivingSpontaneousPatrol, int waivingPatrolCueId, bool locked, Trap dst) {
            dst.TrapLocalId = trapLocalId;
            dst.Config = config;
            dst.ConfigFromTiled = configFromTiled;

            // Only the fields below would change w.r.t. time
            dst.TrapState = trapState;
            dst.FramesInTrapState = framesInTrapState;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;
            dst.IsCompletelyStatic = isCompletelyStatic;

            dst.CapturedByPatrolCue = capturedByPatrolCue;
            dst.FramesInPatrolCue = framesInPatrolCue;
            dst.WaivingSpontaneousPatrol = waivingSpontaneousPatrol;
            dst.WaivingPatrolCueId = waivingPatrolCueId;
            
            dst.Locked = locked;
        }

        public static void AssignToTrigger(int triggerLocalId, int framesToFire, int framesToRecover, int quota, int bulletTeamId, int subCycleQuotaLeft, TriggerState state, int framesInState, int virtualGridX, int virtualGridY, bool locked, TriggerConfig config, TriggerConfigFromTiled configFromTiled, Trigger dst) {
            dst.TriggerLocalId = triggerLocalId;
            dst.FramesToFire = framesToFire;
            dst.FramesToRecover = framesToRecover;
            dst.Quota = quota;
            dst.BulletTeamId = bulletTeamId;
            dst.SubCycleQuotaLeft = subCycleQuotaLeft;
            dst.State = state;
            dst.FramesInState = framesInState;
            dst.Config = config;
            dst.ConfigFromTiled = configFromTiled;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.Locked = locked;
        }

        public static void AssignToPickableConfigFromTile(int initVirtualGridX, int initVirtualGridY, bool takesGravity, int firstShowRdfId, int initRecurQuota, int recurIntervalRdfCount, int lifetimeRdfCountPerOccurrence, PickupType pkType, int stockQuotaPerOccurrence, int subscriptionId, int consumableSpeciesId, int buffSpeciesId, PickableConfigFromTiled dst) {
            dst.InitVirtualGridX = initVirtualGridX;
            dst.InitVirtualGridY = initVirtualGridY;
            dst.TakesGravity = takesGravity;
            dst.FirstShowRdfId = firstShowRdfId;
            dst.RecurQuota = initRecurQuota;
            dst.RecurIntervalRdfCount = recurIntervalRdfCount;
            dst.LifetimeRdfCountPerOccurrence = lifetimeRdfCountPerOccurrence;
            dst.PickupType = pkType;
            dst.StockQuotaPerOccurrence = stockQuotaPerOccurrence;
            dst.SubscriptionId = subscriptionId;
            dst.ConsumableSpeciesId = consumableSpeciesId;
            dst.BuffSpeciesId = buffSpeciesId;
        }

        public static void AssignToPickable(int pickableLocalId, int virtualGridX, int virtualGridY, int velY, int remainingLifetimeRdfCount, int remainingRecurQuota, PickableState pkState, int framesInPkState, int pickedByJoinIndex, int initVirtualGridX, int initVirtualGridY, bool takesGravity, int firstShowRdfId, int initRecurQuota, int recurIntervalRdfCount, int lifetimeRdfCountPerOccurrence, PickupType pkType, int stockQuotaPerOccurrence, int subscriptionId, int consumableSpeciesId, int buffSpeciesId, Pickable dst) {
            dst.PickableLocalId = pickableLocalId;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.VelY = velY;
            
            dst.RemainingLifetimeRdfCount = remainingLifetimeRdfCount; 
            dst.RemainingRecurQuota = remainingRecurQuota; 
            dst.PkState = pkState;
            dst.FramesInPkState = framesInPkState;
            dst.PickedByJoinIndex = pickedByJoinIndex;

            AssignToPickableConfigFromTile(initVirtualGridX, initVirtualGridY, takesGravity, firstShowRdfId, initRecurQuota, recurIntervalRdfCount, lifetimeRdfCountPerOccurrence, pkType, stockQuotaPerOccurrence, subscriptionId, consumableSpeciesId, buffSpeciesId, dst.ConfigFromTiled);
        }

        public static Pickable NewPreallocatedPickable() {
            var single = new Pickable {
                PickableLocalId = TERMINATING_PICKABLE_LOCAL_ID,
                ConfigFromTiled = new PickableConfigFromTiled {
                    SubscriptionId = MAGIC_EVTSUB_ID_NONE,  
                    BuffSpeciesId = TERMINATING_BUFF_SPECIES_ID,
                    ConsumableSpeciesId = TERMINATING_CONSUMABLE_SPECIES_ID,
                },
                PkState = PickableState.Pidle,
                PickedByJoinIndex = MAGIC_JOIN_INDEX_INVALID,
            };

            return single;
        }

        public static CharacterDownsync NewPreallocatedCharacterDownsync(int buffCapacity, int debuffCapacity, int inventoryCapacity, int bulletImmuneRecordCapacity) {
            var single = new CharacterDownsync();
            single.Id = TERMINATING_PLAYER_ID;
            single.LowerPartFramesInChState = INVALID_FRAMES_IN_CH_STATE;
            single.KilledToDropBuffSpeciesId = TERMINATING_BUFF_SPECIES_ID;
            single.KilledToDropConsumableSpeciesId = TERMINATING_CONSUMABLE_SPECIES_ID;
            for (int i = 0; i < buffCapacity; i++) {
                var singleBuff = new Buff();
                singleBuff.SpeciesId = TERMINATING_BUFF_SPECIES_ID;
                singleBuff.OriginatedRenderFrameId = TERMINATING_RENDER_FRAME_ID;
                singleBuff.OrigChSpeciesId = SPECIES_NONE_CH;
                single.BuffList.Add(singleBuff);
            }
            for (int i = 0; i < debuffCapacity; i++) {
                var singleDebuff = new Debuff();
                singleDebuff.SpeciesId = TERMINATING_DEBUFF_SPECIES_ID;
                single.DebuffList.Add(singleDebuff);
            }
            if (0 < inventoryCapacity) {
                single.Inventory = new Inventory();
                for (int i = 0; i < inventoryCapacity; i++) {
                    var singleSlot = new InventorySlot();
                    singleSlot.StockType = InventorySlotStockType.NoneIv;
                    single.Inventory.Slots.Add(singleSlot);
                }
            }
            for (int i = 0; i < bulletImmuneRecordCapacity; i++) {
                var singleRecord = new BulletImmuneRecord {
                    BulletLocalId = TERMINATING_BULLET_LOCAL_ID,
                    RemainingLifetimeRdfCount = 0,
                };
                single.BulletImmuneRecords.Add(singleRecord);
            }
            
            return single;
        }

        public static RoomDownsyncFrame NewPreallocatedRoomDownsyncFrame(int roomCapacity, int preallocNpcCount, int preallocBulletCount, int preallocateTrapCount, int preallocateTriggerCount, int preallocateEvtSubCount, int preallocatePickableCount) {
            var ret = new RoomDownsyncFrame();
            ret.Id = TERMINATING_RENDER_FRAME_ID;
            ret.BulletLocalIdCounter = 0;

            for (int i = 0; i < roomCapacity; i++) {
                var single = NewPreallocatedCharacterDownsync(DEFAULT_PER_CHARACTER_BUFF_CAPACITY, DEFAULT_PER_CHARACTER_DEBUFF_CAPACITY, DEFAULT_PER_CHARACTER_INVENTORY_CAPACITY, DEFAULT_PER_CHARACTER_IMMUNE_BULLET_RECORD_CAPACITY);
                ret.PlayersArr.Add(single);
            }

            for (int i = 0; i < preallocNpcCount; i++) {
                var single = NewPreallocatedCharacterDownsync(DEFAULT_PER_CHARACTER_BUFF_CAPACITY, DEFAULT_PER_CHARACTER_DEBUFF_CAPACITY, 0, DEFAULT_PER_CHARACTER_IMMUNE_BULLET_RECORD_CAPACITY);
                ret.NpcsArr.Add(single);
            }

            for (int i = 0; i < preallocBulletCount; i++) {
                var single = NewBullet(TERMINATING_BULLET_LOCAL_ID, 0, 0, 0, BulletState.StartUp, 0);
                ret.Bullets.Add(single);
            }

            for (int i = 0; i < preallocateTrapCount; i++) {
                var single = new Trap {
                    TrapLocalId = TERMINATING_TRAP_ID
                };
                ret.TrapsArr.Add(single);
            }

            for (int i = 0; i < preallocateTriggerCount; i++) {
                var single = new Trigger {
                    TriggerLocalId = TERMINATING_TRIGGER_ID,  
                    Config = new TriggerConfig {}, 
                };
                ret.TriggersArr.Add(single);
            }

            for (int i = 0; i < preallocateEvtSubCount; i++) {
                var single = new EvtSubscription {
                    Id = TERMINATING_EVTSUB_ID,  
                };
                ret.EvtSubsArr.Add(single);
            }

            for (int i = 0; i < preallocatePickableCount; i++) {
                var single = NewPreallocatedPickable();
                ret.Pickables.Add(single);
            }

            return ret;
        }

        public static InputFrameDownsync NewPreallocatedInputFrameDownsync(int roomCapacity) {
            var ret = new InputFrameDownsync();
            ret.InputFrameId = TERMINATING_INPUT_FRAME_ID;
            ret.ConfirmedList = 0;
            for (int i = 0; i < roomCapacity; i++) {
                ret.InputList.Add(0);
            }

            return ret;
        }

        public static void AssignToRdfDeep(RoomDownsyncFrame src, RoomDownsyncFrame dst, int roomCapacity) {
            // [WARNING] Deliberately ignoring backend-only fields, e.g. "backendUnconfirmedMask", "shouldForceResync", or "participantChangeId". 
            dst.Id = src.Id;
            dst.BulletLocalIdCounter = src.BulletLocalIdCounter;
            dst.NpcLocalIdCounter = src.NpcLocalIdCounter;

            for (int i = 0; i < roomCapacity; i++) {
                var srcCh = src.PlayersArr[i];
                AssignToCharacterDownsync(srcCh.Id, srcCh.SpeciesId, srcCh.VirtualGridX, srcCh.VirtualGridY, srcCh.DirX, srcCh.DirY, srcCh.VelX, srcCh.FrictionVelX, srcCh.VelY, srcCh.FramesToRecover, srcCh.FramesInChState, srcCh.ActiveSkillId, srcCh.ActiveSkillHit, srcCh.FramesInvinsible, srcCh.Speed, srcCh.CharacterState, srcCh.JoinIndex, srcCh.Hp, srcCh.InAir, srcCh.OnWall, srcCh.OnWallNormX, srcCh.OnWallNormY, srcCh.FramesCapturedByInertia, srcCh.BulletTeamId, srcCh.ChCollisionTeamId, srcCh.RevivalVirtualGridX, srcCh.RevivalVirtualGridY, srcCh.RevivalDirX, srcCh.RevivalDirY, srcCh.JumpTriggered, srcCh.SlipJumpTriggered, srcCh.PrimarilyOnSlippableHardPushback, srcCh.CapturedByPatrolCue, srcCh.FramesInPatrolCue, srcCh.BeatsCnt, srcCh.BeatenCnt, srcCh.Mp, srcCh.OmitGravity, srcCh.OmitSoftPushback, srcCh.RepelSoftPushback, srcCh.WaivingSpontaneousPatrol, srcCh.WaivingPatrolCueId, srcCh.OnSlope, srcCh.ForcedCrouching, srcCh.NewBirth, srcCh.LowerPartFramesInChState, srcCh.JumpStarted, srcCh.FramesToStartJump, srcCh.BuffList, srcCh.DebuffList, srcCh.Inventory, false, srcCh.PublishingEvtSubIdUponKilled, srcCh.PublishingEvtMaskUponKilled, srcCh.SubscriptionId, srcCh.JumpHolding, srcCh.BtnBHoldingRdfCount, srcCh.RemainingAirJumpQuota, srcCh.RemainingAirDashQuota, srcCh.KilledToDropConsumableSpeciesId, srcCh.KilledToDropBuffSpeciesId, srcCh.BulletImmuneRecords, dst.PlayersArr[i]);
            }

            int npcCnt = 0;
            while (npcCnt < src.NpcsArr.Count) {
                var srcCh = src.NpcsArr[npcCnt];
                if (TERMINATING_PLAYER_ID == srcCh.Id) break;
                AssignToCharacterDownsync(srcCh.Id, srcCh.SpeciesId, srcCh.VirtualGridX, srcCh.VirtualGridY, srcCh.DirX, srcCh.DirY, srcCh.VelX, srcCh.FrictionVelX, srcCh.VelY, srcCh.FramesToRecover, srcCh.FramesInChState, srcCh.ActiveSkillId, srcCh.ActiveSkillHit, srcCh.FramesInvinsible, srcCh.Speed, srcCh.CharacterState, srcCh.JoinIndex, srcCh.Hp, srcCh.InAir, srcCh.OnWall, srcCh.OnWallNormX, srcCh.OnWallNormY, srcCh.FramesCapturedByInertia, srcCh.BulletTeamId, srcCh.ChCollisionTeamId, srcCh.RevivalVirtualGridX, srcCh.RevivalVirtualGridY, srcCh.RevivalDirX, srcCh.RevivalDirY, srcCh.JumpTriggered, srcCh.SlipJumpTriggered, srcCh.PrimarilyOnSlippableHardPushback, srcCh.CapturedByPatrolCue, srcCh.FramesInPatrolCue, srcCh.BeatsCnt, srcCh.BeatenCnt, srcCh.Mp, srcCh.OmitGravity, srcCh.OmitSoftPushback, srcCh.RepelSoftPushback, srcCh.WaivingSpontaneousPatrol, srcCh.WaivingPatrolCueId, srcCh.OnSlope, srcCh.ForcedCrouching, srcCh.NewBirth, srcCh.LowerPartFramesInChState, srcCh.JumpStarted, srcCh.FramesToStartJump, srcCh.BuffList, srcCh.DebuffList, srcCh.Inventory, false, srcCh.PublishingEvtSubIdUponKilled, srcCh.PublishingEvtMaskUponKilled, srcCh.SubscriptionId, srcCh.JumpHolding, srcCh.BtnBHoldingRdfCount, srcCh.RemainingAirJumpQuota, srcCh.RemainingAirDashQuota, srcCh.KilledToDropConsumableSpeciesId, srcCh.KilledToDropBuffSpeciesId, srcCh.BulletImmuneRecords, dst.NpcsArr[npcCnt]);
                npcCnt++;
            }
            dst.NpcsArr[npcCnt].Id = TERMINATING_PLAYER_ID;

            int bulletCnt = 0;
            while (bulletCnt < src.Bullets.Count) {
                var srcBullet = src.Bullets[bulletCnt];
                if (TERMINATING_BULLET_LOCAL_ID == srcBullet.BattleAttr.BulletLocalId) break;
                AssignToBullet(
                        srcBullet.BattleAttr.BulletLocalId,
                        srcBullet.BattleAttr.OriginatedRenderFrameId,
                        srcBullet.BattleAttr.OffenderJoinIndex,
                        srcBullet.BattleAttr.TeamId,
                        srcBullet.BlState, srcBullet.FramesInBlState,
                        srcBullet.OriginatedVirtualGridX, srcBullet.OriginatedVirtualGridY,
                        srcBullet.VirtualGridX, srcBullet.VirtualGridY,
                        srcBullet.DirX, srcBullet.DirY,
                        srcBullet.VelX, srcBullet.VelY,
                        srcBullet.BattleAttr.ActiveSkillHit, srcBullet.BattleAttr.SkillId, srcBullet.Config, srcBullet.RepeatQuotaLeft, srcBullet.RemainingHardPushbackBounceQuota, srcBullet.TargetCharacterJoinIndex,
                        dst.Bullets[bulletCnt]);
                 bulletCnt++;
            }
            dst.Bullets[bulletCnt].BattleAttr.BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
            
            int trapCnt = 0;
            while (trapCnt < src.TrapsArr.Count) {
                var srcTrap = src.TrapsArr[trapCnt];
                if (TERMINATING_TRAP_ID == srcTrap.TrapLocalId) break;
                AssignToTrap(srcTrap.TrapLocalId, srcTrap.Config, srcTrap.ConfigFromTiled, srcTrap.TrapState, srcTrap.FramesInTrapState, srcTrap.VirtualGridX, srcTrap.VirtualGridY, srcTrap.DirX, srcTrap.DirY, srcTrap.VelX, srcTrap.VelY, srcTrap.IsCompletelyStatic, srcTrap.CapturedByPatrolCue, srcTrap.FramesInPatrolCue, srcTrap.WaivingSpontaneousPatrol, srcTrap.WaivingPatrolCueId, srcTrap.Locked, dst.TrapsArr[trapCnt]);
                trapCnt++;
            }
            dst.TrapsArr[trapCnt].TrapLocalId = TERMINATING_TRAP_ID;

            int triggerCnt = 0;
            while (triggerCnt < src.TriggersArr.Count) {
                var srcTrigger = src.TriggersArr[triggerCnt];
                if (TERMINATING_TRIGGER_ID == srcTrigger.TriggerLocalId) break;
                AssignToTrigger(srcTrigger.TriggerLocalId, srcTrigger.FramesToFire, srcTrigger.FramesToRecover, srcTrigger.Quota, srcTrigger.BulletTeamId, srcTrigger.SubCycleQuotaLeft, srcTrigger.State, srcTrigger.FramesInState, srcTrigger.VirtualGridX, srcTrigger.VirtualGridY, srcTrigger.Locked, srcTrigger.Config, srcTrigger.ConfigFromTiled, dst.TriggersArr[triggerCnt]);
                triggerCnt++;
            }
            dst.TriggersArr[triggerCnt].TriggerLocalId = TERMINATING_TRIGGER_ID;
        }

        public static bool EqualRdfs(RoomDownsyncFrame lhs, RoomDownsyncFrame rhs, int roomCapacity) {
            // [WARNING] Deliberately ignoring backend-only fields, e.g. "backendUnconfirmedMask", "shouldForceResync", or "participantChangeId". 
            // [WARNING] Though some new attributes like "buff/debuff/bulletImmuneRecord" are not added here, those fields being out-of-sync will soon cause inequality in the already tracking fields. 
            if (lhs.Id != rhs.Id) return false;
            if (lhs.BulletLocalIdCounter != rhs.BulletLocalIdCounter) return false;
            if (lhs.NpcLocalIdCounter != rhs.NpcLocalIdCounter) return false;

            for (int i = 0; i < roomCapacity; i++) {
                if (!lhs.PlayersArr[i].Equals(rhs.PlayersArr[i])) return false;
            }

            int npcCnt = 0;
            while (npcCnt < lhs.NpcsArr.Count) {
                // This also compares field "Id" which is used to terminate the arr
                if (!lhs.NpcsArr[npcCnt].Equals(rhs.NpcsArr[npcCnt])) return false;
                if (lhs.NpcsArr[npcCnt].Id == TERMINATING_PLAYER_ID) break;
                npcCnt++;
            }

            int bulletCnt = 0;
            while (bulletCnt < lhs.Bullets.Count) {
                var lBullet = lhs.Bullets[bulletCnt];
                var rBullet = rhs.Bullets[bulletCnt];
                if (lBullet.BattleAttr.BulletLocalId != rBullet.BattleAttr.BulletLocalId) return false;
                if (lBullet.BattleAttr.BulletLocalId == TERMINATING_BULLET_LOCAL_ID) break;
                if (lBullet.BattleAttr.OriginatedRenderFrameId != rBullet.BattleAttr.OriginatedRenderFrameId) return false;
                if (lBullet.BattleAttr.OffenderJoinIndex != rBullet.BattleAttr.OffenderJoinIndex) return false;
                if (lBullet.BattleAttr.TeamId != rBullet.BattleAttr.TeamId) return false;
                if (lBullet.BlState != rBullet.BlState) return false;
                if (lBullet.FramesInBlState != rBullet.FramesInBlState) return false;
                if (lBullet.VirtualGridX != rBullet.VirtualGridY) return false;
                if (lBullet.DirX != rBullet.DirX) return false;
                if (lBullet.DirY != rBullet.DirY) return false;
                if (lBullet.BattleAttr.ActiveSkillHit != rBullet.BattleAttr.ActiveSkillHit) return false;
                if (lBullet.BattleAttr.SkillId != rBullet.BattleAttr.SkillId) return false;
                if (lBullet.Config != rBullet.Config) return false;  // Should be exactly the same ptr
                if (lBullet.RepeatQuotaLeft != rBullet.RepeatQuotaLeft) return false;
                 bulletCnt++;
            }
            
            int trapCnt = 0;
            while (trapCnt < lhs.TrapsArr.Count) {
                // This also compares field "TrapLocalId" which is used to terminate the arr
                if (!lhs.TrapsArr[trapCnt].Equals(rhs.TrapsArr[trapCnt])) return false;
                if (lhs.TrapsArr[trapCnt].TrapLocalId == TERMINATING_TRAP_ID) break;
                trapCnt++;
            }

            int triggerCnt = 0;
            while (triggerCnt < lhs.TriggersArr.Count) {
                var lTrigger = lhs.TriggersArr[triggerCnt];
                var rTrigger = rhs.TriggersArr[triggerCnt];
                if (lTrigger.TriggerLocalId != rTrigger.TriggerLocalId) return false;
                if (lTrigger.TriggerLocalId == TERMINATING_TRIGGER_ID) break;
                if (lTrigger.FramesToFire != rTrigger.FramesToFire) return false;
                if (lTrigger.FramesToRecover != rTrigger.FramesToRecover) return false;
                if (lTrigger.Quota != rTrigger.Quota) return false;
                if (lTrigger.BulletTeamId != rTrigger.BulletTeamId) return false;
                if (lTrigger.SubCycleQuotaLeft != rTrigger.SubCycleQuotaLeft) return false;
                if (lTrigger.State != rTrigger.State) return false;
                if (lTrigger.FramesInState != rTrigger.FramesInState) return false;
                if (lTrigger.VirtualGridX != rTrigger.VirtualGridX) return false;
                if (lTrigger.VirtualGridY != rTrigger.VirtualGridY) return false;
                if (lTrigger.Config != rTrigger.Config) return false; // Should be exactly the same ptr
                if (lTrigger.ConfigFromTiled != rTrigger.ConfigFromTiled) return false; // Should be exactly the same ptr
                triggerCnt++;
            }
            return true;
        }

        public static void revertBuff(Buff cand, CharacterDownsync thatCharacterInNextFrame) {
            var buffConfig = buffConfigs[cand.SpeciesId];
            if (SPECIES_NONE_CH != buffConfig.XformChSpeciesId) {
                var nextChConfig = characters[cand.OrigChSpeciesId];
                AssignToCharacterDownsyncFromCharacterConfig(nextChConfig, thatCharacterInNextFrame);
            }
        }

        public static void revertDebuff(Debuff cand, CharacterDownsync thatCharacterInNextFrame) {
            // TBD
        }

        public static void preallocateStepHolders(int roomCapacity, int renderBufferSize, int preallocNpcCapacity, int preallocBulletCapacity, int preallocTrapCapacity, int preallocTriggerCapacity, int preallocEvtSubCapacity, int preallocPickableCount, out int justFulfilledEvtSubCnt, out int[] justFulfilledEvtSubArr, out FrameRingBuffer<Collider> residueCollided, out FrameRingBuffer<RoomDownsyncFrame> renderBuffer, out FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, out FrameRingBuffer<InputFrameDownsync> inputBuffer, out int[] lastIndividuallyConfirmedInputFrameId, out ulong[] lastIndividuallyConfirmedInputList, out Vector[] effPushbacks, out Vector[][] hardPushbackNormsArr, out Vector[] softPushbacks, out Collider[] dynamicRectangleColliders, out Collider[] staticColliders, out InputFrameDecoded decodedInputHolder, out InputFrameDecoded prevDecodedInputHolder, out BattleResult confirmedBattleResult, out bool softPushbackEnabled, bool frameLogEnabled) {
            if (0 >= roomCapacity) {
                throw new ArgumentException(String.Format("roomCapacity={0} is non-positive, please initialize it first!", roomCapacity));
            }

            justFulfilledEvtSubCnt = 0;
            justFulfilledEvtSubArr = new int[16]; // TODO: Remove this hardcoded capacity 

            int residueCollidedCap = 256;
            residueCollided = new FrameRingBuffer<shared.Collider>(residueCollidedCap);

            renderBuffer = new FrameRingBuffer<RoomDownsyncFrame>(renderBufferSize);
            for (int i = 0; i < renderBufferSize; i++) {
                renderBuffer.Put(NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocEvtSubCapacity, preallocPickableCount));
            }
            renderBuffer.Clear(); // Then use it by "DryPut"

            int softPushbacksCap = 16;
            if (frameLogEnabled) {
                pushbackFrameLogBuffer = new FrameRingBuffer<RdfPushbackFrameLog>(renderBufferSize);
                for (int i = 0; i < renderBufferSize; i++) {
                    pushbackFrameLogBuffer.Put(new RdfPushbackFrameLog(TERMINATING_RENDER_FRAME_ID, roomCapacity + preallocNpcCapacity, softPushbacksCap));
                }
                pushbackFrameLogBuffer.Clear(); // Then use it by "DryPut"
            } else {
                pushbackFrameLogBuffer = new FrameRingBuffer<RdfPushbackFrameLog>(1);
            }

            int inputBufferSize = (renderBufferSize >> 1) + 1;
            inputBuffer = new FrameRingBuffer<InputFrameDownsync>(inputBufferSize);
            for (int i = 0; i < inputBufferSize; i++) {
                inputBuffer.Put(NewPreallocatedInputFrameDownsync(roomCapacity));
            }
            inputBuffer.Clear(); // Then use it by "DryPut"

            lastIndividuallyConfirmedInputFrameId = new int[roomCapacity];
            Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);

            lastIndividuallyConfirmedInputList = new ulong[roomCapacity];
            Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);

            effPushbacks = new Vector[roomCapacity + preallocBulletCapacity + preallocNpcCapacity + preallocTrapCapacity];
            for (int i = 0; i < effPushbacks.Length; i++) {
                effPushbacks[i] = new Vector(0, 0);
            }
            hardPushbackNormsArr = new Vector[roomCapacity + preallocBulletCapacity + preallocNpcCapacity + preallocTrapCapacity][];
            for (int i = 0; i < hardPushbackNormsArr.Length; i++) {
                int cap = 5;
                hardPushbackNormsArr[i] = new Vector[cap];
                for (int j = 0; j < cap; j++) {
                    hardPushbackNormsArr[i][j] = new Vector(0, 0);
                }
            }

            softPushbacks = new Vector[softPushbacksCap];
            for (int i = 0; i < softPushbacks.Length; i++) {
                softPushbacks[i] = new Vector(0, 0);
            }
            softPushbackEnabled = true;

            int dynamicRectangleCollidersCap = 192;
            dynamicRectangleColliders = new Collider[dynamicRectangleCollidersCap];
            staticColliders = new Collider[128];

            decodedInputHolder = new InputFrameDecoded();
            prevDecodedInputHolder = new InputFrameDecoded();

            confirmedBattleResult = new BattleResult {
                WinnerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT
            };
        }

        public static void refreshColliders(RoomDownsyncFrame startRdf, RepeatedField<SerializableConvexPolygon> serializedBarrierPolygons, RepeatedField<SerializedCompletelyStaticPatrolCueCollider> serializedStaticPatrolCues, RepeatedField<SerializedCompletelyStaticTrapCollider> serializedCompletelyStaticTraps, RepeatedField<SerializedCompletelyStaticTriggerCollider> serializedStaticTriggers, SerializedTrapLocalIdToColliderAttrs serializedTrapLocalIdToColliderAttrs, SerializedTriggerTrackingIdToTrapLocalId serializedTriggerTrackingIdToTrapLocalId, int spaceOffsetX, int spaceOffsetY, ref CollisionSpace collisionSys, ref int maxTouchingCellsCnt, ref Collider[] dynamicRectangleColliders, ref Collider[] staticColliders, ref Collision collisionHolder, ref List<Collider> completelyStaticTrapColliders, ref Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, ref Dictionary<int, int> triggerTrackingIdToTrapLocalId) {

            int cellWidth = 64;
            int cellHeight = 128; // To avoid dynamic trap as a standing point to slip when moving down along with the character
            collisionSys = new CollisionSpace(spaceOffsetX << 1, spaceOffsetY << 1, cellWidth, cellHeight);
            maxTouchingCellsCnt = (((spaceOffsetX << 1) + cellWidth) / cellWidth) * (((spaceOffsetY << 1) + cellHeight) / cellHeight) + 1;
            for (int i = 0; i < dynamicRectangleColliders.Length; i++) {
                var srcPolygon = NewRectPolygon(0, 0, 0, 0, 0, 0, 0, 0);
                dynamicRectangleColliders[i] = NewConvexPolygonCollider(srcPolygon, 0, 0, maxTouchingCellsCnt, null);
            }

            collisionHolder = new Collision();

            int staticCollidersCnt = 0;
            for (int i = 0; i < serializedBarrierPolygons.Count; i++) {
                var serializedPolygon = serializedBarrierPolygons[i];
                float[] points = new float[serializedPolygon.Points.Count];
                serializedPolygon.Points.CopyTo(points, 0);
                var barrierPolygon = new ConvexPolygon(serializedPolygon.AnchorX, serializedPolygon.AnchorY, points);
                var barrierCollider = NewConvexPolygonCollider(barrierPolygon, 0, 0, maxTouchingCellsCnt, null);
                barrierCollider.Mask = COLLISION_BARRIER_INDEX_PREFIX;
                staticColliders[staticCollidersCnt++] = barrierCollider;
            }

            for (int i = 0; i < serializedStaticPatrolCues.Count; i++) {
                var s = serializedStaticPatrolCues[i];
                float[] points = new float[s.Polygon.Points.Count];
                s.Polygon.Points.CopyTo(points, 0);
                var cuePolygon = new ConvexPolygon(s.Polygon.AnchorX, s.Polygon.AnchorY, points);
                var cueCollider = NewConvexPolygonCollider(cuePolygon, 0, 0, maxTouchingCellsCnt, s.Attr);
                cueCollider.Mask = s.Attr.CollisionTypeMask;
                staticColliders[staticCollidersCnt++] = cueCollider;
            }

            completelyStaticTrapColliders = new List<Collider>();
            for (int i = 0; i < serializedCompletelyStaticTraps.Count; i++) {
                var s = serializedCompletelyStaticTraps[i];
                float[] points = new float[s.Polygon.Points.Count];
                s.Polygon.Points.CopyTo(points, 0);
                var trapPolygon = new ConvexPolygon(s.Polygon.AnchorX, s.Polygon.AnchorY, points);
                var trapCollider = NewConvexPolygonCollider(trapPolygon, 0, 0, maxTouchingCellsCnt, s.Attr);
                trapCollider.Mask = COLLISION_TRAP_INDEX_PREFIX; // [WARNING] NOT using "s.Attr.CollisionTypeMask", see comments in "room_downsync_frame.proto > SerializedCompletelyStaticTrapCollider"  
                staticColliders[staticCollidersCnt++] = trapCollider;
                completelyStaticTrapColliders.Add(trapCollider);
            }

            for (int i = 0; i < serializedStaticTriggers.Count; i++) {
                var s = serializedStaticTriggers[i];
                float[] points = new float[s.Polygon.Points.Count];
                s.Polygon.Points.CopyTo(points, 0);
                var triggerPolygon = new ConvexPolygon(s.Polygon.AnchorX, s.Polygon.AnchorY, points);
                var triggerCollider = NewConvexPolygonCollider(triggerPolygon, 0, 0, maxTouchingCellsCnt, s.Attr);
                var trigger = startRdf.TriggersArr[s.Attr.TriggerLocalId];
                var triggerConfig = trigger.Config;
                triggerCollider.Mask = triggerConfig.TriggerMask;
                staticColliders[staticCollidersCnt++] = triggerCollider;
            }

            trapLocalIdToColliderAttrs = new Dictionary<int, List<TrapColliderAttr>>();
            foreach (var entry in serializedTrapLocalIdToColliderAttrs.Dict) {
                var trapLocalId = entry.Key;
                var attrs = entry.Value.List;
                var colliderAttrs = new List<TrapColliderAttr>();
                colliderAttrs.AddRange(attrs);
                trapLocalIdToColliderAttrs[trapLocalId] = colliderAttrs;
            }

            triggerTrackingIdToTrapLocalId = new Dictionary<int, int>();
            foreach (var entry in serializedTriggerTrackingIdToTrapLocalId.Dict) {
                triggerTrackingIdToTrapLocalId[entry.Key] = entry.Value; 
            }

            for (int i = 0; i < staticCollidersCnt; i++) {
                collisionSys.AddSingle(staticColliders[i]);
            }
        }
    }

    public sealed partial class BulletConfig {
        
        public BulletConfig UpsertCancelTransit(int patternId, int skillId) {
            this.CancelTransit.Add(patternId, skillId);
            return this;
        }
        
        public BulletConfig SetAllowsWalking(bool val) {
            this.AllowsWalking = val;
            return this;
        }

        public BulletConfig SetAllowsCrouching(bool val) {
            this.AllowsCrouching = val;
            return this;
        }

        public BulletConfig SetStartupFrames(int val) {
            this.StartupFrames = val;
            return this;
        }

        public BulletConfig SetSpeed(int val) {
            this.Speed = val;
            return this;
        }

        public BulletConfig SetSpeedIfNotHit(int val) {
            this.SpeedIfNotHit = val;
            return this;
        }

        public BulletConfig SetDir(int dirX, int dirY) {
            this.DirX = dirX;
            this.DirY = dirY;
            return this;
        }

        public BulletConfig SetHitboxOffsets(int hitboxOffsetX, int hitboxOffsetY) {
            this.HitboxOffsetX = hitboxOffsetX;
            this.HitboxOffsetY = hitboxOffsetY;
            return this;
        }

        public BulletConfig SetPushbacks(int pushbackVelX, int pushbackVelY) {
            this.PushbackVelX = pushbackVelX;
            this.PushbackVelY = pushbackVelY;
            return this;
        }
    
        public BulletConfig SetSimultaneousMultiHitCnt(uint simultaneousMultiHitCnt) {
            this.SimultaneousMultiHitCnt = simultaneousMultiHitCnt;
            return this;
        }

        public BulletConfig SetTakesGravity(bool yesOrNo) {
            this.TakesGravity = yesOrNo;
            return this;
        }

        public BulletConfig SetRotateAlongVelocity(bool yesOrNo) {
            RotatesAlongVelocity = yesOrNo;
            return this;
        }

        public BulletConfig SetMhType(MultiHitType mhType) {
            MhType = mhType; 
            return this;
        }

        public BulletConfig SetRemainsUponHit(bool val) {
            RemainsUponHit = val; 
            return this;
        }

        public BulletConfig SetIsPixelatedActiveVfx(bool val) {
            IsPixelatedActiveVfx = val;
            return this;
        }
            
        public BulletConfig SetActiveVfxSpeciesId(int val) {
            ActiveVfxSpeciesId = val;
            return this;
        }

        public bool isEmissionInducedMultiHit() {
            return (MultiHitType.FromEmission == MhType);
        }
    }

    public sealed partial class Skill {
    
        public Skill AddHit(BulletConfig val) {
            Hits.Add(val);
            return this;
        }
    }
    
    public sealed partial class BuffConfig {
        public BuffConfig AddAssociatedDebuff(DebuffConfig val) {
            AssociatedDebuffs.Add(val.SpeciesId);
            return this;
        }
    }
}
