using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

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

        public static void UpdateRectCollider(Collider collider, float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float spaceOffsetX, float spaceOffsetY, object data, ulong mask, bool flipX = false, bool isRotary = false, float spinAnchorX = 0f, float spinAnchorY = 0f, float cosDelta = 1f, float sinDelta = 0f) {
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, spaceOffsetX, spaceOffsetY);

            float effW = leftPadding + w + rightPadding;
            float effH = bottomPadding + h + topPadding;
            (collider.X, collider.Y, collider.W, collider.H) = (blX, blY, effW, effH);
            float effSpinAnchorX = (!flipX ? spinAnchorX : effW - spinAnchorX);
            if (0 == sinDelta && 0 == cosDelta) {
                cosDelta = 1;
            }
            collider.Shape.UpdateAsRectangle(effW, effH, isRotary, effSpinAnchorX, spinAnchorY, cosDelta, sinDelta);
            
            // [WARNING] No channge to (collider.X, collider.Y, collider.W, collider.H) after rotation. 
            collider.Data = data;
            collider.Mask = mask;
        }

        public static void AssignToBuff(uint speciesId, int stock, int originatedRenderFrameId, uint origChSpeciesId, bool origRepelSoftPushback, bool origOmitGravity, Buff dst) {
            dst.SpeciesId = speciesId; 
            dst.Stock = stock;
            dst.OriginatedRenderFrameId = originatedRenderFrameId;
            dst.OrigChSpeciesId = origChSpeciesId;
            dst.OrigRepelSoftPushback = origRepelSoftPushback;
            dst.OrigOmitGravity = origOmitGravity;
        }

        public static void AssignToDebuff(uint speciesId, int stock, Debuff dst) {
            dst.SpeciesId = speciesId; 
            dst.Stock = stock;
        }

        public static void AssignToInventorySlot(InventorySlotStockType stockType, uint quota, int framesToRecover, uint defaultQuota, int defaultFramesToRecover, uint buffSpeciesId, uint skillId, uint skillIdAir, int gaugeCharged, int gaugeRequired, uint fullChargeSkillId, uint fullChargeBuffSpeciesId, InventorySlot dst) {
            dst.StockType = stockType; 
            dst.Quota = quota; 
            dst.FramesToRecover = framesToRecover; 
            dst.DefaultQuota = defaultQuota;
            dst.DefaultFramesToRecover = defaultFramesToRecover;
            dst.BuffSpeciesId = buffSpeciesId;
            dst.SkillId = skillId;
            dst.SkillIdAir = skillIdAir;
            dst.GaugeCharged = gaugeCharged;
            dst.GaugeRequired = gaugeRequired;
            dst.FullChargeSkillId = fullChargeSkillId;
            dst.FullChargeBuffSpeciesId = fullChargeBuffSpeciesId;
        }

        public static void AssignToBulletImmuneRecord(int bulletLocalId, int remainingLifetimeRdfCount, BulletImmuneRecord dst) {
            dst.BulletLocalId = bulletLocalId; 
            dst.RemainingLifetimeRdfCount = remainingLifetimeRdfCount; 
        }

        public static void AssignToCharacterDownsyncFromCharacterConfig(CharacterConfig chConfig, CharacterDownsync dst, bool allowHpAndMpRecover) {
            dst.SpeciesId = chConfig.SpeciesId;
            dst.OmitGravity = chConfig.OmitGravity;
            dst.OmitSoftPushback = chConfig.OmitSoftPushback;
            dst.RepelSoftPushback = chConfig.RepelSoftPushback;
            dst.RemainingAirJumpQuota = chConfig.DefaultAirJumpQuota;
            dst.RemainingAirDashQuota = chConfig.DefaultAirDashQuota;
            dst.Speed = chConfig.Speed;
            
            dst.OmitGravity = chConfig.OmitGravity;
            dst.OmitSoftPushback = chConfig.OmitSoftPushback;
            dst.ComboHitCnt = 0;
            dst.ComboFramesRemained = 0;
            dst.DamageElementalAttrs = 0;
            dst.ActiveSkillId = NO_SKILL;
            dst.ActiveSkillHit = NO_SKILL_HIT;
            dst.FramesSinceLastDamaged = 0;
            dst.RemainingDef1Quota = 0;
            dst.JumpHoldingRdfCnt = 0;
            dst.BtnBHoldingRdfCount = 0;
            dst.BtnCHoldingRdfCount = 0;
            dst.BtnDHoldingRdfCount = 0;
            dst.BtnEHoldingRdfCount = 0;
            dst.ParryPrepRdfCntDown = 0;
            dst.MpRegenRdfCountdown = chConfig.MpRegenInterval;

            if (allowHpAndMpRecover) {
                dst.Hp = chConfig.Hp;
                dst.Mp = chConfig.Mp;
            }

            AssignToBuff(TERMINATING_BUFF_SPECIES_ID, 0, TERMINATING_RENDER_FRAME_ID, SPECIES_NONE_CH, false, false, dst.BuffList[0]);
            AssignToDebuff(TERMINATING_DEBUFF_SPECIES_ID, 0, dst.DebuffList[0]);
            dst.BulletImmuneRecords[0].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            int newInventoryCnt = 0;
            if (null != chConfig.InitInventorySlots) {
                for (; newInventoryCnt < chConfig.InitInventorySlots.Count && newInventoryCnt < dst.Inventory.Slots.Count; newInventoryCnt++) {
                    var initIvSlot = chConfig.InitInventorySlots[newInventoryCnt];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, initIvSlot.SkillIdAir, initIvSlot.GaugeCharged, initIvSlot.GaugeRequired, initIvSlot.FullChargeSkillId, initIvSlot.FullChargeBuffSpeciesId, dst.Inventory.Slots[newInventoryCnt]);
                }
            }
            if (newInventoryCnt < dst.Inventory.Slots.Count) {
                dst.Inventory.Slots[newInventoryCnt].StockType = InventorySlotStockType.NoneIv;
            }
        }

        public static void AssignToCharacterDownsync(int id, uint speciesId, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int frictionVelX, int velY, int frictionVelY, int framesToRecover, int framesInChState, uint activeSkillId, int activeSkillHit, int framesInvinsible, int speed, CharacterState characterState, int joinIndex, int hp, bool inAir, bool onWall, int onWallNormX, int onWallNormY, int framesCapturedByInertia, int bulletTeamId, int chCollisionTeamId, int revivalVirtualGridX, int revivalVirtualGridY, int revivalDirX, int revivalDirY, bool jumpTriggered, bool slipJumpTriggered, bool primarilyOnSlippableHardPushback, bool capturedByPatrolCue, int framesInPatrolCue, uint beatsCnt, uint beatenCnt, int mp, bool omitGravity, bool omitSoftPushback, bool repelSoftPushback, NpcGoal goalAsNpc, int waivingPatrolCueId, bool onSlope, bool onSlopeFacingDown, bool forcedCrouching, bool newBirth, bool jumpStarted, int framesToStartJump, int framesSinceLastDamaged, uint remainingDef1Quota, RepeatedField<Buff> prevBuffList, RepeatedField<Debuff> prevDebuffList, Inventory? prevInventory, bool isRdfFrameElapsing, int publishingToTriggerLocalIdUponKilled, ulong publishingEvtMaskUponKilled, int subscribesToTriggerLocalId, int jumpHoldingRdfCnt, int btnBHoldingRdfCount, int btnCHoldingRdfCount, int btnDHoldingRdfCount, int btnEHoldingRdfCount, int parryPrepRdfCntDown, uint remainingAirJumpQuota, uint remainingAirDashQuota, uint killedToDropConsumableSpeciesId, uint killedToDropBuffSpeciesId, uint killedToDropPickupSkillId, RepeatedField<BulletImmuneRecord> prevBulletImmuneRecords, uint comboHitCnt, int comboFramesRemained, uint damageElementalAttrs, int lastDamagedByJoinIndex, int lastDamagedByBulletTeamId, int activatedRdfId, ulong cachedCueCmd, int mpRegenRdfCountdown, int flyingRdfCountdown, int lockingOnJoinIndex, CharacterDownsync dst) {
            dst.Id = id;
            dst.SpeciesId = speciesId;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.FrictionVelX = frictionVelX;
            dst.FrictionVelY = frictionVelY;
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
            dst.GoalAsNpc = goalAsNpc;
            dst.WaivingPatrolCueId = waivingPatrolCueId;

            dst.OnSlope = onSlope;
            dst.OnSlopeFacingDown = onSlopeFacingDown;
            dst.ForcedCrouching = forcedCrouching;
            
            dst.NewBirth = newBirth;

            dst.JumpStarted = jumpStarted;
            dst.FramesToStartJump = framesToStartJump;
            dst.FramesSinceLastDamaged = framesSinceLastDamaged;
            dst.RemainingDef1Quota = remainingDef1Quota;

            dst.PublishingToTriggerLocalIdUponKilled = publishingToTriggerLocalIdUponKilled;
            dst.PublishingEvtMaskUponKilled = publishingEvtMaskUponKilled;
            dst.SubscribesToTriggerLocalId = subscribesToTriggerLocalId;
        
            dst.JumpHoldingRdfCnt = jumpHoldingRdfCnt;
            dst.BtnBHoldingRdfCount = btnBHoldingRdfCount;
            dst.BtnCHoldingRdfCount = btnCHoldingRdfCount;
            dst.BtnDHoldingRdfCount = btnDHoldingRdfCount;
            dst.BtnEHoldingRdfCount = btnEHoldingRdfCount;
            dst.ParryPrepRdfCntDown = parryPrepRdfCntDown;

            dst.RemainingAirJumpQuota = remainingAirJumpQuota;
            dst.RemainingAirDashQuota = remainingAirDashQuota;

            dst.KilledToDropConsumableSpeciesId = killedToDropConsumableSpeciesId;
            dst.KilledToDropBuffSpeciesId = killedToDropBuffSpeciesId;
            dst.KilledToDropPickupSkillId = killedToDropPickupSkillId;

            dst.LastDamagedByJoinIndex = lastDamagedByJoinIndex;
            dst.LastDamagedByBulletTeamId = lastDamagedByBulletTeamId;

            dst.ActivatedRdfId = activatedRdfId;
            dst.CachedCueCmd = cachedCueCmd;
            dst.MpRegenRdfCountdown = mpRegenRdfCountdown;
            dst.FlyingRdfCountdown = flyingRdfCountdown;
            dst.LockingOnJoinIndex = lockingOnJoinIndex;

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
                    var nextStock = cand.Stock - 1;
                    uint nextSpeciesId = cand.SpeciesId;
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

            int newInventoryCnt = 0;
            if (null != prevInventory) {
                // [WARNING] When "defaultTemplateInventory" or null is passed in, it's equivalent to just TERMINATING at the very beginning.
                int prevInventoryI = 0;
                while (prevInventoryI < prevInventory.Slots.Count) {
                    var cand = prevInventory.Slots[prevInventoryI++];
                    if (InventorySlotStockType.NoneIv == cand.StockType) break;
                    if (InventorySlotStockType.TimedIv == cand.StockType && isRdfFrameElapsing) {
                        var nextFramesToRecover = 0 < cand.FramesToRecover ? cand.FramesToRecover - 1 : 0;
                        AssignToInventorySlot(cand.StockType, cand.Quota, nextFramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, cand.SkillIdAir, cand.GaugeCharged, cand.GaugeRequired, cand.FullChargeSkillId, cand.FullChargeBuffSpeciesId, dst.Inventory.Slots[newInventoryCnt]);
                    } else if (InventorySlotStockType.TimedMagazineIv == cand.StockType && isRdfFrameElapsing) {
                        var nextFramesToRecover = 0 < cand.FramesToRecover ? cand.FramesToRecover - 1 : 0;
                        if (0 > nextFramesToRecover) nextFramesToRecover = 0;
                        if (0 == nextFramesToRecover && 1 == cand.FramesToRecover) {
                            AssignToInventorySlot(cand.StockType, cand.DefaultQuota, nextFramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, cand.SkillIdAir, cand.GaugeCharged, cand.GaugeRequired, cand.FullChargeSkillId, cand.FullChargeBuffSpeciesId, dst.Inventory.Slots[newInventoryCnt]);
                        } else {
                            AssignToInventorySlot(cand.StockType, cand.Quota, nextFramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, cand.SkillIdAir, cand.GaugeCharged, cand.GaugeRequired, cand.FullChargeSkillId, cand.FullChargeBuffSpeciesId, dst.Inventory.Slots[newInventoryCnt]);
                        }
                    } else {
                        AssignToInventorySlot(cand.StockType, cand.Quota, cand.FramesToRecover, cand.DefaultQuota, cand.DefaultFramesToRecover, cand.BuffSpeciesId, cand.SkillId, cand.SkillIdAir, cand.GaugeCharged, cand.GaugeRequired, cand.FullChargeSkillId, cand.FullChargeBuffSpeciesId, dst.Inventory.Slots[newInventoryCnt]);
                    }
                    ++newInventoryCnt;
                }
            }
            if (newInventoryCnt < dst.Inventory.Slots.Count) {
                var dstSlot = dst.Inventory.Slots[newInventoryCnt];
                AssignToInventorySlot(InventorySlotStockType.NoneIv, 0, 0, 0, 0, TERMINATING_BUFF_SPECIES_ID, NO_SKILL, NO_SKILL, 0, 0, NO_SKILL, TERMINATING_BUFF_SPECIES_ID, dstSlot);
            }

            // [WARNING] Deliberately put "revertBuff" after "inventory slots assignment from prev rdf". When "defaultTemplateBuffList" is passed in, it's equivalent to just TERMINATING at the very beginning.
            int newBuffCnt = 0, prevBuffI = 0;
            while (prevBuffI < prevBuffList.Count) {
                var cand = prevBuffList[prevBuffI++];
                if (TERMINATING_BUFF_SPECIES_ID == cand.SpeciesId) break;
                var buffConfig = buffConfigs[cand.SpeciesId];
                if (BuffStockType.Timed == buffConfig.StockType && isRdfFrameElapsing) {
                    var nextStock = cand.Stock - 1;
                    if (0 >= nextStock) {
                        if (noOpSet.Contains(characterState) || 0 >= framesToRecover) {
                            // [WARNING] It's very unnatural to transform back when the character is actively using a skill!
                            revertBuff(cand, dst);
                            continue;
                        }
                    }
                    AssignToBuff(cand.SpeciesId, nextStock, cand.OriginatedRenderFrameId, cand.OrigChSpeciesId, cand.OrigRepelSoftPushback, cand.OrigOmitGravity, dst.BuffList[newBuffCnt]);
                } else {
                    AssignToBuff(cand.SpeciesId, cand.Stock, cand.OriginatedRenderFrameId, cand.OrigChSpeciesId, cand.OrigRepelSoftPushback, cand.OrigOmitGravity, dst.BuffList[newBuffCnt]);
                }
                ++newBuffCnt;
            }
            if (newBuffCnt < dst.BuffList.Count) {
                AssignToBuff(TERMINATING_BUFF_SPECIES_ID, 0, TERMINATING_RENDER_FRAME_ID, SPECIES_NONE_CH, false, false, dst.BuffList[newBuffCnt]);
            }

            int newBulletImmuneRcdCnt = 0;
            if (null != prevBulletImmuneRecords) {
                int prevCnt = 0;
                while (prevCnt < prevBulletImmuneRecords.Count) {
                    var cand = prevBulletImmuneRecords[prevCnt++];
                    if (TERMINATING_BULLET_LOCAL_ID == cand.BulletLocalId) break;
                    if (isRdfFrameElapsing) {
                        if (0 >= cand.RemainingLifetimeRdfCount) {
                            continue;
                        }
                        var newRemainingLifetimeRdfCount = (0 < cand.RemainingLifetimeRdfCount ? cand.RemainingLifetimeRdfCount - 1 : 0);
                        AssignToBulletImmuneRecord(cand.BulletLocalId, newRemainingLifetimeRdfCount, dst.BulletImmuneRecords[newBulletImmuneRcdCnt]);
                    } else {
                        AssignToBulletImmuneRecord(cand.BulletLocalId, cand.RemainingLifetimeRdfCount, dst.BulletImmuneRecords[newBulletImmuneRcdCnt]);
                    }
                    ++newBulletImmuneRcdCnt;
                }
            }
            // [WARNING] When "defaultTemplateBulletImmuneRecords" or null is passed in, it's equivalent to just TERMINATING at the very beginning.
            if (newBulletImmuneRcdCnt < dst.BulletImmuneRecords.Count) dst.BulletImmuneRecords[newBulletImmuneRcdCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            dst.ComboHitCnt = comboHitCnt;
            dst.ComboFramesRemained = comboFramesRemained;
            dst.DamageElementalAttrs = damageElementalAttrs;
        }

        public static Bullet NewBullet(int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int teamId, BulletState blState, int framesInBlState) {
            return new Bullet {
                BlState = blState,
                FramesInBlState = framesInBlState,
                BulletLocalId = bulletLocalId,
                OriginatedRenderFrameId = originatedRenderFrameId,
                OffenderJoinIndex = offenderJoinIndex,
                TeamId = teamId,
                VirtualGridX = 0,
                VirtualGridY = 0,
                DirX = 0,
                DirY = 0,
                VelX = 0,
                VelY = 0
            };
        }

        public static void AssignToBullet(int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int offenderTrapLocalId, int teamId, BulletState blState, int framesInBlState, int origVx, int origVy, int vx, int vy, int dirX, int dirY, int velX, int velY, int activeSkillHit, uint skillId, int vertMovingTrapLocalIdUponActive, int repeatQuotaLeft, int remainingHardPushbackBounceQuota, int targetCharacterJoinIndex, float spinCos, float spinSin, int damageDealed, IfaceCat explodedOnIfc, Bullet dst) {
            dst.BlState = blState;
            dst.FramesInBlState = framesInBlState;

            dst.BulletLocalId = bulletLocalId;
            dst.OriginatedRenderFrameId = originatedRenderFrameId;
            dst.OffenderJoinIndex = offenderJoinIndex;
            dst.OffenderTrapLocalId = offenderTrapLocalId;
            dst.TeamId = teamId;
            dst.ActiveSkillHit = activeSkillHit;
            dst.SkillId = skillId;
            dst.VertMovingTrapLocalIdUponActive = vertMovingTrapLocalIdUponActive;
            dst.DamageDealed = damageDealed;
            dst.ExplodedOnIfc = explodedOnIfc;

            dst.OriginatedVirtualGridX = origVx;
            dst.OriginatedVirtualGridY = origVy;
            dst.VirtualGridX = vx;
            dst.VirtualGridY = vy;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;

            dst.SpinCos = spinCos;
            dst.SpinSin = spinSin;

            dst.RepeatQuotaLeft = repeatQuotaLeft;
            dst.RemainingHardPushbackBounceQuota = remainingHardPushbackBounceQuota;
            dst.TargetCharacterJoinIndex = targetCharacterJoinIndex;
        }

        public static void AssignToTrap(int trapLocalId, TrapConfigFromTiled configFromTiled, TrapState trapState, int framesInTrapState, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int velY, float spinCos, float spinSin, float angularFrameVelCos, float angularFrameVelSin, int patrolCueAngularVelFlipMark, bool isCompletelyStatic, bool capturedByPatrolCue, int framesInPatrolCue, bool waivingSpontaneousPatrol, int waivingPatrolCueId, int subscribesToTriggerLocalId, int subscribesToTriggerLocalIdAlt, Trap dst) {
            dst.TrapLocalId = trapLocalId;
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
            dst.SpinCos = spinCos;
            dst.SpinSin = spinSin;
            dst.AngularFrameVelCos = angularFrameVelCos;
            dst.AngularFrameVelSin = angularFrameVelSin;
            dst.PatrolCueAngularVelFlipMark = patrolCueAngularVelFlipMark;
            dst.IsCompletelyStatic = isCompletelyStatic;

            dst.CapturedByPatrolCue = capturedByPatrolCue;
            dst.FramesInPatrolCue = framesInPatrolCue;
            dst.WaivingSpontaneousPatrol = waivingSpontaneousPatrol;
            dst.WaivingPatrolCueId = waivingPatrolCueId;
            
            dst.SubscribesToTriggerLocalId = subscribesToTriggerLocalId;
            dst.SubscribesToTriggerLocalIdAlt = subscribesToTriggerLocalIdAlt;
        }

        public static void AssignToTrigger(int editorId, int triggerLocalId, int framesToFire, int framesToRecover, int quota, int bulletTeamId, int offenderJoinIndex, int offenderBulletTeamId, int subCycleQuotaLeft, TriggerState state, int framesInState, int virtualGridX, int virtualGridY, int dirX, ulong demandedEvtMask, ulong fulfilledEvtMask, ulong waveNpcKilledEvtMaskCounter, ulong subscriberLocalIdsMask, ulong exhaustSubscriberLocalIdsMask, Trigger dst) {
            dst.EditorId = editorId;
            dst.TriggerLocalId = triggerLocalId;
            dst.FramesToFire = framesToFire;
            dst.FramesToRecover = framesToRecover;
            dst.Quota = quota;
            dst.BulletTeamId = bulletTeamId;
            dst.OffenderJoinIndex = offenderJoinIndex;
            dst.OffenderBulletTeamId = offenderBulletTeamId;
            dst.SubCycleQuotaLeft = subCycleQuotaLeft;
            dst.State = state;
            dst.FramesInState = framesInState;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DemandedEvtMask = demandedEvtMask;
            dst.FulfilledEvtMask = fulfilledEvtMask;
            dst.WaveNpcKilledEvtMaskCounter = waveNpcKilledEvtMaskCounter;
            dst.SubscriberLocalIdsMask = subscriberLocalIdsMask;
            dst.ExhaustSubscriberLocalIdsMask = exhaustSubscriberLocalIdsMask;
        }

        public static void AssignToPickableConfigFromTile(int initVirtualGridX, int initVirtualGridY, bool takesGravity, int firstShowRdfId, int initRecurQuota, uint recurIntervalRdfCount, uint lifetimeRdfCountPerOccurrence, PickupType pkType, uint stockQuotaPerOccurrence, int subscriptionId, uint consumableSpeciesId, uint buffSpeciesId, uint skillId, PickableConfigFromTiled dst) {
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
            dst.SkillId = skillId;
        }

        public static void AssignToPickable(int pickableLocalId, int virtualGridX, int virtualGridY, int velX, int velY, int remainingLifetimeRdfCount, int remainingRecurQuota, PickableState pkState, int framesInPkState, int pickedByJoinIndex, int initVirtualGridX, int initVirtualGridY, bool takesGravity, int firstShowRdfId, int initRecurQuota, uint recurIntervalRdfCount, uint lifetimeRdfCountPerOccurrence, PickupType pkType, uint stockQuotaPerOccurrence, int subscriptionId, uint consumableSpeciesId, uint buffSpeciesId, uint skillId, Pickable dst) {
            dst.PickableLocalId = pickableLocalId;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.VelX = velX;
            dst.VelY = velY;
            
            dst.RemainingLifetimeRdfCount = remainingLifetimeRdfCount; 
            dst.RemainingRecurQuota = remainingRecurQuota; 
            dst.PkState = pkState;
            dst.FramesInPkState = framesInPkState;
            dst.PickedByJoinIndex = pickedByJoinIndex;

            AssignToPickableConfigFromTile(initVirtualGridX, initVirtualGridY, takesGravity, firstShowRdfId, initRecurQuota, recurIntervalRdfCount, lifetimeRdfCountPerOccurrence, pkType, stockQuotaPerOccurrence, subscriptionId, consumableSpeciesId, buffSpeciesId, skillId, dst.ConfigFromTiled);
        }

        public static Pickable NewPreallocatedPickable() {
            var single = new Pickable {
                PickableLocalId = TERMINATING_PICKABLE_LOCAL_ID,
                ConfigFromTiled = new PickableConfigFromTiled {
                    SubscriptionId = TERMINATING_EVTSUB_ID_INT,  
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
            single.KilledToDropBuffSpeciesId = TERMINATING_BUFF_SPECIES_ID;
            single.KilledToDropConsumableSpeciesId = TERMINATING_CONSUMABLE_SPECIES_ID;
            single.LastDamagedByJoinIndex = MAGIC_JOIN_INDEX_INVALID;
            single.LastDamagedByBulletTeamId = TERMINATING_BULLET_TEAM_ID;
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

        public static RoomDownsyncFrame NewPreallocatedRoomDownsyncFrame(int roomCapacity, int preallocNpcCount, int preallocBulletCount, int preallocateTrapCount, int preallocateTriggerCount, int preallocatePickableCount) {
            var ret = new RoomDownsyncFrame();
            ret.Id = TERMINATING_RENDER_FRAME_ID;
            ret.BulletLocalIdCounter = 0;

            for (int i = 0; i < roomCapacity; i++) {
                var single = NewPreallocatedCharacterDownsync(DEFAULT_PER_CHARACTER_BUFF_CAPACITY, DEFAULT_PER_CHARACTER_DEBUFF_CAPACITY, DEFAULT_PER_CHARACTER_INVENTORY_CAPACITY, DEFAULT_PER_CHARACTER_IMMUNE_BULLET_RECORD_CAPACITY);
                ret.PlayersArr.Add(single);
            }

            for (int i = 0; i < preallocNpcCount; i++) {
                var single = NewPreallocatedCharacterDownsync(DEFAULT_PER_CHARACTER_BUFF_CAPACITY, DEFAULT_PER_CHARACTER_DEBUFF_CAPACITY, 1, DEFAULT_PER_CHARACTER_IMMUNE_BULLET_RECORD_CAPACITY);
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
                };
                ret.TriggersArr.Add(single);
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

        public static void _leftShiftDeadNpcs(int rdfId, int roomCapacity, RepeatedField<CharacterDownsync> nextRenderFrameNpcs, ref int pickableLocalIdCounter, RepeatedField<Pickable> nextRenderFramePickables, RepeatedField<Trigger> nextRenderFrameTriggers, Dictionary<int, int> joinIndexRemap, out bool isRemapNeeded, HashSet<int> justDeadJoinIndices, ref int nextNpcI, ref int nextRdfPickableCnt, bool forPlayerRevivalInStory, ILoggerBridge logger) {
            isRemapNeeded = false;
            int aliveSlotI = 0, candidateI = 0;
            justDeadJoinIndices.Clear();
            while (candidateI < nextRenderFrameNpcs.Count && TERMINATING_PLAYER_ID != nextRenderFrameNpcs[candidateI].Id) {
                var candidate = nextRenderFrameNpcs[candidateI];
                var candidateConfig = characters[candidate.SpeciesId];
                float characterVirtualGridTop = candidate.VirtualGridY + (candidateConfig.DefaultSizeY >> 1);
                bool hasTransformUponDeath = _hasTransformUponDeath(candidate, candidateConfig);
                if (isNpcJustDead(candidate)) {
                    if (forPlayerRevivalInStory || !hasTransformUponDeath) {
                        justDeadJoinIndices.Add(candidate.JoinIndex);
                        isRemapNeeded = true;
                        if (!forPlayerRevivalInStory && (TERMINATING_CONSUMABLE_SPECIES_ID != candidate.KilledToDropConsumableSpeciesId || TERMINATING_BUFF_SPECIES_ID != candidate.KilledToDropBuffSpeciesId || NO_SKILL != candidate.KilledToDropPickupSkillId)) {
                            addNewPickableToNextFrame(rdfId, candidate.VirtualGridX, candidate.VirtualGridY, 0, +1, MAX_INT, 0, true, MAX_UINT, MAX_UINT, (NO_SKILL == candidate.KilledToDropPickupSkillId ? PickupType.Immediate : PickupType.PutIntoInventory), 1, nextRenderFramePickables, candidate.KilledToDropConsumableSpeciesId, candidate.KilledToDropBuffSpeciesId, candidate.KilledToDropPickupSkillId, ref pickableLocalIdCounter, ref nextRdfPickableCnt);
                        }
                    }
                }

                while (candidateI < nextRenderFrameNpcs.Count && TERMINATING_PLAYER_ID != nextRenderFrameNpcs[candidateI].Id && isNpcDeadToDisappear(nextRenderFrameNpcs[candidateI])) {
                    candidate = nextRenderFrameNpcs[candidateI];
                    candidateConfig = characters[candidate.SpeciesId];
                    hasTransformUponDeath = _hasTransformUponDeath(candidate, candidateConfig);
                    if (!forPlayerRevivalInStory && hasTransformUponDeath) {
                        // This is an "alive" case!
                        break;
                    }
                    if (!forPlayerRevivalInStory && TERMINATING_TRIGGER_ID != candidate.PublishingToTriggerLocalIdUponKilled) {
                        var targetTriggerInNextFrame = nextRenderFrameTriggers[candidate.PublishingToTriggerLocalIdUponKilled-1];
                        if (MAGIC_JOIN_INDEX_INVALID == candidate.LastDamagedByJoinIndex && TERMINATING_BULLET_TEAM_ID == candidate.LastDamagedByBulletTeamId) {
                            if (1 == roomCapacity) {
                                PublishNpcKilledEvt(rdfId, candidate.PublishingEvtMaskUponKilled, 1, 1, targetTriggerInNextFrame, logger);
                            } else {
                                logger.LogWarn("@rdfId=" + rdfId + " publishing evtMask=" + candidate.PublishingEvtMaskUponKilled + " to trigger " + targetTriggerInNextFrame.ToString() + " with no join index and no bullet team id!");
                                PublishNpcKilledEvt(rdfId, candidate.PublishingEvtMaskUponKilled, candidate.LastDamagedByJoinIndex, candidate.LastDamagedByBulletTeamId, targetTriggerInNextFrame, logger);
                            }
                        } else {
                            PublishNpcKilledEvt(rdfId, candidate.PublishingEvtMaskUponKilled, candidate.LastDamagedByJoinIndex, candidate.LastDamagedByBulletTeamId, targetTriggerInNextFrame, logger);
                        }
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
                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FrictionVelY, src.FramesToRecover, src.FramesInChState, src.ActiveSkillId, src.ActiveSkillHit, src.FramesInvinsible, src.Speed, src.CharacterState, newJoinIndex, src.Hp, src.InAir, src.OnWall, src.OnWallNormX, src.OnWallNormY, src.FramesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, src.PrimarilyOnSlippableHardPushback, src.CapturedByPatrolCue, src.FramesInPatrolCue, src.BeatsCnt, src.BeatenCnt, src.Mp, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.GoalAsNpc, src.WaivingPatrolCueId, src.OnSlope, src.OnSlopeFacingDown, src.ForcedCrouching, src.NewBirth, src.JumpStarted, src.FramesToStartJump, src.FramesSinceLastDamaged, src.RemainingDef1Quota, src.BuffList, src.DebuffList, src.Inventory, false, src.PublishingToTriggerLocalIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscribesToTriggerLocalId, src.JumpHoldingRdfCnt, src.BtnBHoldingRdfCount, src.BtnCHoldingRdfCount, src.BtnDHoldingRdfCount, src.BtnEHoldingRdfCount, src.ParryPrepRdfCntDown, src.RemainingAirJumpQuota, src.RemainingAirDashQuota, src.KilledToDropConsumableSpeciesId, src.KilledToDropBuffSpeciesId, src.KilledToDropPickupSkillId, src.BulletImmuneRecords, src.ComboHitCnt, src.ComboFramesRemained, src.DamageElementalAttrs, src.LastDamagedByJoinIndex, src.LastDamagedByBulletTeamId, src.ActivatedRdfId, src.CachedCueCmd, src.MpRegenRdfCountdown, src.FlyingRdfCountdown, src.LockingOnJoinIndex, dst);
                candidateI++;
                aliveSlotI++;
            }
            if (aliveSlotI < nextRenderFrameNpcs.Count) {
                nextRenderFrameNpcs[aliveSlotI].Id = TERMINATING_PLAYER_ID;
            }
            nextNpcI = aliveSlotI;
        }

        public static void AssignToBossSavepoint(RoomDownsyncFrame srcCurrRdf, RoomDownsyncFrame srcNextRdf, RoomDownsyncFrame dst, int roomCapacity, ILoggerBridge logger) {
            AssignToRdfDeep(srcNextRdf, dst, roomCapacity, logger);
        }

        public static void AssignFromBossSavepoint(RoomDownsyncFrame src, RoomDownsyncFrame dst, int roomCapacity, Dictionary<int, int> joinIndexRemap, HashSet<int> justDeadJoinIndices, HashSet<uint> bossSpeciesSet, ILoggerBridge logger) {
            int dstNpcI = 0;
            while (dstNpcI < dst.NpcsArr.Count) {
                var dstCh = dst.NpcsArr[dstNpcI];
                if (TERMINATING_PLAYER_ID == dstCh.Id) break;
                if (dstCh.ActivatedRdfId >= src.Id) {
                    dstCh.CharacterState = CharacterState.Dying;
                    dstCh.Hp = 0;
                    dstCh.FramesToRecover = 0;
                }
                ++dstNpcI;
            }
            int fooPickableLocalIdCounter = 0;
            int fooPickableCnt = 0;
            bool isRemapNeeded = false;
            _leftShiftDeadNpcs(dst.Id, roomCapacity, dst.NpcsArr, ref fooPickableLocalIdCounter, dst.Pickables, dst.TriggersArr, joinIndexRemap, out isRemapNeeded, justDeadJoinIndices, ref dstNpcI, ref fooPickableCnt, true, logger);

            /*
            Handling living bosses 
            */
            int srcNpcI = 0;
            while (srcNpcI < src.NpcsArr.Count) {
                var srcCh = src.NpcsArr[srcNpcI];
                if (TERMINATING_PLAYER_ID == srcCh.Id) {
                    break;
                }
                ++srcNpcI;
                if (!bossSpeciesSet.Contains(srcCh.SpeciesId)) {
                    continue;
                }

                // By now it's a boss NPC
                if (TERMINATING_TRIGGER_ID == srcCh.SubscribesToTriggerLocalId) {
                    // If it's a boss NPC without an awaker, then just keep it as-is (actually this is equal to just any other NPC)
                    continue; 
                }
                
                var dstCh = dst.NpcsArr[dstNpcI];
                AssignToCharacterDownsync(srcCh.Id, srcCh.SpeciesId, srcCh.VirtualGridX, srcCh.VirtualGridY, srcCh.DirX, srcCh.DirY, srcCh.VelX, srcCh.FrictionVelX, srcCh.VelY, srcCh.FrictionVelY, srcCh.FramesToRecover, srcCh.FramesInChState, srcCh.ActiveSkillId, srcCh.ActiveSkillHit, srcCh.FramesInvinsible, srcCh.Speed, srcCh.CharacterState, roomCapacity+dstNpcI+1, srcCh.Hp, srcCh.InAir, srcCh.OnWall, srcCh.OnWallNormX, srcCh.OnWallNormY, srcCh.FramesCapturedByInertia, srcCh.BulletTeamId, srcCh.ChCollisionTeamId, srcCh.RevivalVirtualGridX, srcCh.RevivalVirtualGridY, srcCh.RevivalDirX, srcCh.RevivalDirY, srcCh.JumpTriggered, srcCh.SlipJumpTriggered, srcCh.PrimarilyOnSlippableHardPushback, srcCh.CapturedByPatrolCue, srcCh.FramesInPatrolCue, srcCh.BeatsCnt, srcCh.BeatenCnt, srcCh.Mp, srcCh.OmitGravity, srcCh.OmitSoftPushback, srcCh.RepelSoftPushback, srcCh.GoalAsNpc, srcCh.WaivingPatrolCueId, srcCh.OnSlope, srcCh.OnSlopeFacingDown, srcCh.ForcedCrouching, srcCh.NewBirth, srcCh.JumpStarted, srcCh.FramesToStartJump, srcCh.FramesSinceLastDamaged, srcCh.RemainingDef1Quota, srcCh.BuffList, srcCh.DebuffList, srcCh.Inventory, false, srcCh.PublishingToTriggerLocalIdUponKilled, srcCh.PublishingEvtMaskUponKilled, srcCh.SubscribesToTriggerLocalId, srcCh.JumpHoldingRdfCnt, srcCh.BtnBHoldingRdfCount, srcCh.BtnCHoldingRdfCount, srcCh.BtnDHoldingRdfCount, srcCh.BtnEHoldingRdfCount, srcCh.ParryPrepRdfCntDown, srcCh.RemainingAirJumpQuota, srcCh.RemainingAirDashQuota, srcCh.KilledToDropConsumableSpeciesId, srcCh.KilledToDropBuffSpeciesId, srcCh.KilledToDropPickupSkillId, srcCh.BulletImmuneRecords, srcCh.ComboHitCnt, srcCh.ComboFramesRemained, srcCh.DamageElementalAttrs, srcCh.LastDamagedByJoinIndex, srcCh.LastDamagedByBulletTeamId, srcCh.ActivatedRdfId, srcCh.CachedCueCmd, srcCh.MpRegenRdfCountdown, srcCh.FlyingRdfCountdown, srcCh.LockingOnJoinIndex, dstCh);
                dstNpcI++;
            }
            // [WARNING] No need to remap bullet offender join indices because "excludePlayers" would simply remove all existing bullets
            if (dstNpcI < dst.NpcsArr.Count) {
                dst.NpcsArr[dstNpcI].Id = TERMINATING_PLAYER_ID;
            }

            // [WARNING] Should remove all bullets too.
            dst.Bullets[0].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;

            int trapCnt = 0;
            while (trapCnt < src.TrapsArr.Count) {
                var srcTrap = src.TrapsArr[trapCnt];
                if (TERMINATING_TRAP_ID == srcTrap.TrapLocalId) break;
                AssignToTrap(srcTrap.TrapLocalId, srcTrap.ConfigFromTiled, srcTrap.TrapState, srcTrap.FramesInTrapState, srcTrap.VirtualGridX, srcTrap.VirtualGridY, srcTrap.DirX, srcTrap.DirY, srcTrap.VelX, srcTrap.VelY, srcTrap.SpinCos, srcTrap.SpinSin, srcTrap.AngularFrameVelCos, srcTrap.AngularFrameVelSin, srcTrap.PatrolCueAngularVelFlipMark, srcTrap.IsCompletelyStatic, srcTrap.CapturedByPatrolCue, srcTrap.FramesInPatrolCue, srcTrap.WaivingSpontaneousPatrol, srcTrap.WaivingPatrolCueId, srcTrap.SubscribesToTriggerLocalId, srcTrap.SubscribesToTriggerLocalIdAlt, dst.TrapsArr[trapCnt]);
                trapCnt++;
            }
            if (trapCnt < dst.TrapsArr.Count) {
                dst.TrapsArr[trapCnt].TrapLocalId = TERMINATING_TRAP_ID;
            }

            int triggerCnt = 0;
            while (triggerCnt < src.TriggersArr.Count) {
                var srcTrigger = src.TriggersArr[triggerCnt];
                if (TERMINATING_TRIGGER_ID == srcTrigger.TriggerLocalId) break;
                var dstTrigger = dst.TriggersArr[triggerCnt];
                AssignToTrigger(srcTrigger.EditorId, srcTrigger.TriggerLocalId, srcTrigger.FramesToFire, srcTrigger.FramesToRecover, srcTrigger.Quota, srcTrigger.BulletTeamId, srcTrigger.OffenderJoinIndex, srcTrigger.OffenderBulletTeamId, srcTrigger.SubCycleQuotaLeft, srcTrigger.State, srcTrigger.FramesInState, srcTrigger.VirtualGridX, srcTrigger.VirtualGridY, srcTrigger.DirX, srcTrigger.DemandedEvtMask, srcTrigger.FulfilledEvtMask, srcTrigger.WaveNpcKilledEvtMaskCounter, srcTrigger.SubscriberLocalIdsMask, srcTrigger.ExhaustSubscriberLocalIdsMask, dstTrigger);
            
                triggerCnt++;
            }
            if (triggerCnt < dst.TriggersArr.Count) {
                dst.TriggersArr[triggerCnt].TriggerLocalId = TERMINATING_TRIGGER_ID;
            }
        }

        public static void AssignToRdfDeep(RoomDownsyncFrame src, RoomDownsyncFrame dst, int roomCapacity, ILoggerBridge logger) {
            // [WARNING] Deliberately ignoring backend-only fields, e.g. "backendUnconfirmedMask", "shouldForceResync", or "participantChangeId". 
            dst.Id = src.Id;

            for (int i = 0; i < roomCapacity; i++) {
                var srcCh = src.PlayersArr[i];
                AssignToCharacterDownsync(srcCh.Id, srcCh.SpeciesId, srcCh.VirtualGridX, srcCh.VirtualGridY, srcCh.DirX, srcCh.DirY, srcCh.VelX, srcCh.FrictionVelX, srcCh.VelY, srcCh.FrictionVelY, srcCh.FramesToRecover, srcCh.FramesInChState, srcCh.ActiveSkillId, srcCh.ActiveSkillHit, srcCh.FramesInvinsible, srcCh.Speed, srcCh.CharacterState, srcCh.JoinIndex, srcCh.Hp, srcCh.InAir, srcCh.OnWall, srcCh.OnWallNormX, srcCh.OnWallNormY, srcCh.FramesCapturedByInertia, srcCh.BulletTeamId, srcCh.ChCollisionTeamId, srcCh.RevivalVirtualGridX, srcCh.RevivalVirtualGridY, srcCh.RevivalDirX, srcCh.RevivalDirY, srcCh.JumpTriggered, srcCh.SlipJumpTriggered, srcCh.PrimarilyOnSlippableHardPushback, srcCh.CapturedByPatrolCue, srcCh.FramesInPatrolCue, srcCh.BeatsCnt, srcCh.BeatenCnt, srcCh.Mp, srcCh.OmitGravity, srcCh.OmitSoftPushback, srcCh.RepelSoftPushback, srcCh.GoalAsNpc, srcCh.WaivingPatrolCueId, srcCh.OnSlope, srcCh.OnSlopeFacingDown, srcCh.ForcedCrouching, srcCh.NewBirth, srcCh.JumpStarted, srcCh.FramesToStartJump, srcCh.FramesSinceLastDamaged, srcCh.RemainingDef1Quota, srcCh.BuffList, srcCh.DebuffList, srcCh.Inventory, false, srcCh.PublishingToTriggerLocalIdUponKilled, srcCh.PublishingEvtMaskUponKilled, srcCh.SubscribesToTriggerLocalId, srcCh.JumpHoldingRdfCnt, srcCh.BtnBHoldingRdfCount, srcCh.BtnCHoldingRdfCount, srcCh.BtnDHoldingRdfCount, srcCh.BtnEHoldingRdfCount, srcCh.ParryPrepRdfCntDown, srcCh.RemainingAirJumpQuota, srcCh.RemainingAirDashQuota, srcCh.KilledToDropConsumableSpeciesId, srcCh.KilledToDropBuffSpeciesId, srcCh.KilledToDropPickupSkillId, srcCh.BulletImmuneRecords, srcCh.ComboHitCnt, srcCh.ComboFramesRemained, srcCh.DamageElementalAttrs, srcCh.LastDamagedByJoinIndex, srcCh.LastDamagedByBulletTeamId, srcCh.ActivatedRdfId, srcCh.CachedCueCmd, srcCh.MpRegenRdfCountdown, srcCh.FlyingRdfCountdown, srcCh.LockingOnJoinIndex, dst.PlayersArr[i]);
            }

            int npcCnt = 0;
            while (npcCnt < src.NpcsArr.Count) {
                var srcCh = src.NpcsArr[npcCnt];
                if (TERMINATING_PLAYER_ID == srcCh.Id) break;
                AssignToCharacterDownsync(srcCh.Id, srcCh.SpeciesId, srcCh.VirtualGridX, srcCh.VirtualGridY, srcCh.DirX, srcCh.DirY, srcCh.VelX, srcCh.FrictionVelX, srcCh.VelY, srcCh.FrictionVelY, srcCh.FramesToRecover, srcCh.FramesInChState, srcCh.ActiveSkillId, srcCh.ActiveSkillHit, srcCh.FramesInvinsible, srcCh.Speed, srcCh.CharacterState, srcCh.JoinIndex, srcCh.Hp, srcCh.InAir, srcCh.OnWall, srcCh.OnWallNormX, srcCh.OnWallNormY, srcCh.FramesCapturedByInertia, srcCh.BulletTeamId, srcCh.ChCollisionTeamId, srcCh.RevivalVirtualGridX, srcCh.RevivalVirtualGridY, srcCh.RevivalDirX, srcCh.RevivalDirY, srcCh.JumpTriggered, srcCh.SlipJumpTriggered, srcCh.PrimarilyOnSlippableHardPushback, srcCh.CapturedByPatrolCue, srcCh.FramesInPatrolCue, srcCh.BeatsCnt, srcCh.BeatenCnt, srcCh.Mp, srcCh.OmitGravity, srcCh.OmitSoftPushback, srcCh.RepelSoftPushback, srcCh.GoalAsNpc, srcCh.WaivingPatrolCueId, srcCh.OnSlope, srcCh.OnSlopeFacingDown, srcCh.ForcedCrouching, srcCh.NewBirth, srcCh.JumpStarted, srcCh.FramesToStartJump, srcCh.FramesSinceLastDamaged, srcCh.RemainingDef1Quota, srcCh.BuffList, srcCh.DebuffList, srcCh.Inventory, false, srcCh.PublishingToTriggerLocalIdUponKilled, srcCh.PublishingEvtMaskUponKilled, srcCh.SubscribesToTriggerLocalId, srcCh.JumpHoldingRdfCnt, srcCh.BtnBHoldingRdfCount, srcCh.BtnCHoldingRdfCount, srcCh.BtnDHoldingRdfCount, srcCh.BtnEHoldingRdfCount, srcCh.ParryPrepRdfCntDown, srcCh.RemainingAirJumpQuota, srcCh.RemainingAirDashQuota, srcCh.KilledToDropConsumableSpeciesId, srcCh.KilledToDropBuffSpeciesId, srcCh.KilledToDropPickupSkillId, srcCh.BulletImmuneRecords, srcCh.ComboHitCnt, srcCh.ComboFramesRemained, srcCh.DamageElementalAttrs, srcCh.LastDamagedByJoinIndex, srcCh.LastDamagedByBulletTeamId, srcCh.ActivatedRdfId, srcCh.CachedCueCmd, srcCh.MpRegenRdfCountdown, srcCh.FlyingRdfCountdown, srcCh.LockingOnJoinIndex, dst.NpcsArr[npcCnt]);
                npcCnt++;
            }
            if (npcCnt < dst.NpcsArr.Count) {
                dst.NpcsArr[npcCnt].Id = TERMINATING_PLAYER_ID;
            }
            dst.NpcLocalIdCounter = src.NpcLocalIdCounter;

            int bulletCnt = 0;
            while (bulletCnt < src.Bullets.Count) {
                var srcBullet = src.Bullets[bulletCnt];
                if (TERMINATING_BULLET_LOCAL_ID == srcBullet.BulletLocalId) break;
                AssignToBullet(
                        srcBullet.BulletLocalId,
                        srcBullet.OriginatedRenderFrameId,
                        srcBullet.OffenderJoinIndex,
                        srcBullet.OffenderTrapLocalId,
                        srcBullet.TeamId,
                        srcBullet.BlState, srcBullet.FramesInBlState,
                        srcBullet.OriginatedVirtualGridX, srcBullet.OriginatedVirtualGridY,
                        srcBullet.VirtualGridX, srcBullet.VirtualGridY,
                        srcBullet.DirX, srcBullet.DirY,
                        srcBullet.VelX, srcBullet.VelY,
                        srcBullet.ActiveSkillHit, srcBullet.SkillId, srcBullet.VertMovingTrapLocalIdUponActive, srcBullet.RepeatQuotaLeft, srcBullet.RemainingHardPushbackBounceQuota, srcBullet.TargetCharacterJoinIndex,
                        srcBullet.SpinCos, srcBullet.SpinSin,
                        srcBullet.DamageDealed,
                        srcBullet.ExplodedOnIfc,
                        dst.Bullets[bulletCnt]);
                 bulletCnt++;
            }
            if (bulletCnt < dst.Bullets.Count) {
                dst.Bullets[bulletCnt].BulletLocalId = TERMINATING_BULLET_LOCAL_ID;
            }
            dst.BulletLocalIdCounter = src.BulletLocalIdCounter;
            
            int trapCnt = 0;
            while (trapCnt < src.TrapsArr.Count) {
                var srcTrap = src.TrapsArr[trapCnt];
                if (TERMINATING_TRAP_ID == srcTrap.TrapLocalId) break;
                AssignToTrap(srcTrap.TrapLocalId, srcTrap.ConfigFromTiled, srcTrap.TrapState, srcTrap.FramesInTrapState, srcTrap.VirtualGridX, srcTrap.VirtualGridY, srcTrap.DirX, srcTrap.DirY, srcTrap.VelX, srcTrap.VelY, srcTrap.SpinCos, srcTrap.SpinSin, srcTrap.AngularFrameVelCos, srcTrap.AngularFrameVelSin, srcTrap.PatrolCueAngularVelFlipMark, srcTrap.IsCompletelyStatic, srcTrap.CapturedByPatrolCue, srcTrap.FramesInPatrolCue, srcTrap.WaivingSpontaneousPatrol, srcTrap.WaivingPatrolCueId, srcTrap.SubscribesToTriggerLocalId, srcTrap.SubscribesToTriggerLocalIdAlt, dst.TrapsArr[trapCnt]);
                trapCnt++;
            }
            if (trapCnt < dst.TrapsArr.Count) {
                dst.TrapsArr[trapCnt].TrapLocalId = TERMINATING_TRAP_ID;
            }

            int triggerCnt = 0;
            while (triggerCnt < src.TriggersArr.Count) {
                var srcTrigger = src.TriggersArr[triggerCnt];
                if (TERMINATING_TRIGGER_ID == srcTrigger.TriggerLocalId) break;
                var dstTrigger = dst.TriggersArr[triggerCnt];
                    AssignToTrigger(srcTrigger.EditorId, srcTrigger.TriggerLocalId, srcTrigger.FramesToFire, srcTrigger.FramesToRecover, srcTrigger.Quota, srcTrigger.BulletTeamId, srcTrigger.OffenderJoinIndex, srcTrigger.OffenderBulletTeamId, srcTrigger.SubCycleQuotaLeft, srcTrigger.State, srcTrigger.FramesInState, srcTrigger.VirtualGridX, srcTrigger.VirtualGridY, srcTrigger.DirX, srcTrigger.DemandedEvtMask, srcTrigger.FulfilledEvtMask, srcTrigger.WaveNpcKilledEvtMaskCounter, srcTrigger.SubscriberLocalIdsMask, srcTrigger.ExhaustSubscriberLocalIdsMask, dstTrigger);
            
                triggerCnt++;
            }
            if (triggerCnt < dst.TriggersArr.Count) {
                dst.TriggersArr[triggerCnt].TriggerLocalId = TERMINATING_TRIGGER_ID;
            }
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
                if (lBullet.BulletLocalId != rBullet.BulletLocalId) return false;
                if (lBullet.BulletLocalId == TERMINATING_BULLET_LOCAL_ID) break;
                if (lBullet.OriginatedRenderFrameId != rBullet.OriginatedRenderFrameId) return false;
                if (lBullet.OffenderJoinIndex != rBullet.OffenderJoinIndex) return false;
                if (lBullet.TeamId != rBullet.TeamId) return false;
                if (lBullet.BlState != rBullet.BlState) return false;
                if (lBullet.FramesInBlState != rBullet.FramesInBlState) return false;
                if (lBullet.VirtualGridX != rBullet.VirtualGridY) return false;
                if (lBullet.DirX != rBullet.DirX) return false;
                if (lBullet.DirY != rBullet.DirY) return false;
                if (lBullet.ActiveSkillHit != rBullet.ActiveSkillHit) return false;
                if (lBullet.SkillId != rBullet.SkillId) return false;
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
                triggerCnt++;
            }
            return true;
        }

        public static void revertBuff(Buff cand, CharacterDownsync thatCharacterInNextFrame) {
            var buffConfig = buffConfigs[cand.SpeciesId];
            if (SPECIES_NONE_CH != buffConfig.XformChSpeciesId) {
                var nextChConfig = characters[cand.OrigChSpeciesId];
                AssignToCharacterDownsyncFromCharacterConfig(nextChConfig, thatCharacterInNextFrame, false);
                if (thatCharacterInNextFrame.Mp > nextChConfig.Mp) {
                    thatCharacterInNextFrame.Mp = nextChConfig.Mp;
                }
            }
            if (buffConfig.OmitGravity) {
                thatCharacterInNextFrame.OmitGravity = cand.OrigOmitGravity;
            }
            if (buffConfig.RepelSoftPushback) {
                thatCharacterInNextFrame.RepelSoftPushback = cand.OrigRepelSoftPushback;
            }
        }

        public static void revertAllBuffsAndDebuffs(CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame) {
            int prevBuffI = 0;
            while (prevBuffI < currCharacterDownsync.BuffList.Count) {
                var cand = currCharacterDownsync.BuffList[prevBuffI++];
                if (TERMINATING_BUFF_SPECIES_ID == cand.SpeciesId) break;
                revertBuff(cand, thatCharacterInNextFrame);
            }
            AssignToBuff(TERMINATING_BUFF_SPECIES_ID, 0, TERMINATING_RENDER_FRAME_ID, SPECIES_NONE_CH, false, false, thatCharacterInNextFrame.BuffList[0]);

            int prevDebuffI = 0;
            while (prevDebuffI < currCharacterDownsync.DebuffList.Count) {
                var cand = currCharacterDownsync.DebuffList[prevDebuffI++];
                if (TERMINATING_DEBUFF_SPECIES_ID == cand.SpeciesId) break;
                revertDebuff(cand, thatCharacterInNextFrame);
            }
            AssignToDebuff(TERMINATING_DEBUFF_SPECIES_ID, 0, thatCharacterInNextFrame.DebuffList[0]);
        }

        public static void revertDebuff(Debuff cand, CharacterDownsync thatCharacterInNextFrame) {
            // TBD
        }

        public static void ApplyBuffToCharacter(int rdfId, BuffConfig buffConfig, CharacterDownsync currCharacterDownsync, CharacterDownsync thatCharacterInNextFrame) {
            uint origChSpeciesId = SPECIES_NONE_CH;
            if (SPECIES_NONE_CH != buffConfig.XformChSpeciesId) {
                origChSpeciesId = currCharacterDownsync.SpeciesId;
                var nextChConfig = characters[buffConfig.XformChSpeciesId];
                AssignToCharacterDownsyncFromCharacterConfig(nextChConfig, thatCharacterInNextFrame, false);
                if (0 < nextChConfig.Mp) {
                    thatCharacterInNextFrame.Mp = nextChConfig.Mp; 
                }
                if (0 != nextChConfig.TransformIntoFramesToRecover) {
                    thatCharacterInNextFrame.CharacterState = CharacterState.TransformingInto; 
                    thatCharacterInNextFrame.FramesToRecover = nextChConfig.TransformIntoFramesToRecover;
                    thatCharacterInNextFrame.VelX = 0;
                }
                if (0 != nextChConfig.TransformIntoFramesInvinsible) {
                    if (thatCharacterInNextFrame.FramesInvinsible < nextChConfig.TransformIntoFramesInvinsible)
                    thatCharacterInNextFrame.FramesInvinsible = nextChConfig.TransformIntoFramesInvinsible; 
                }
            }
            bool origRepelSoftPushback = currCharacterDownsync.RepelSoftPushback;
            if (buffConfig.RepelSoftPushback) {
                thatCharacterInNextFrame.RepelSoftPushback = buffConfig.RepelSoftPushback;
            }
            bool origOmitGravity = currCharacterDownsync.OmitGravity;
            if (buffConfig.OmitGravity) {
                thatCharacterInNextFrame.OmitGravity = buffConfig.OmitGravity;
            }
            // TODO: Support multi-buff simultaneously!
            AssignToBuff(buffConfig.SpeciesId, buffConfig.Stock, rdfId, origChSpeciesId, origRepelSoftPushback, origOmitGravity, thatCharacterInNextFrame.BuffList[0]);
        }

        public static bool isBattleResultSet(BattleResult battleResult) {
            return (TERMINATING_BULLET_TEAM_ID != battleResult.WinnerBulletTeamId);
        }

        public static void resetBattleResult(ref BattleResult battleResult) {
            battleResult.WinnerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT;
            battleResult.WinnerBulletTeamId = TERMINATING_BULLET_TEAM_ID;
        }


        public static void preallocateStepHolders(int roomCapacity, int renderBufferSize, int preallocNpcCapacity, int preallocBulletCapacity, int preallocTrapCapacity, int preallocTriggerCapacity, int preallocPickableCount, out FrameRingBuffer<RoomDownsyncFrame> renderBuffer, out FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, out FrameRingBuffer<InputFrameDownsync> inputBuffer, out int[] lastIndividuallyConfirmedInputFrameId, out ulong[] lastIndividuallyConfirmedInputList, out Vector[] effPushbacks, out Vector[][] hardPushbackNormsArr, out Vector[] softPushbacks, out InputFrameDecoded decodedInputHolder, out InputFrameDecoded prevDecodedInputHolder, out BattleResult confirmedBattleResult, out bool softPushbackEnabled, bool frameLogEnabled) {
            /*
            [WARNING] 

            The allocation of "CollisionSpace" instance and individual "Collider" instances are done in "refreshColliders" instead.
            */
            if (0 >= roomCapacity) {
                throw new ArgumentException(String.Format("roomCapacity={0} is non-positive, please initialize it first!", roomCapacity));
            }

            renderBuffer = new FrameRingBuffer<RoomDownsyncFrame>(renderBufferSize);
            for (int i = 0; i < renderBufferSize; i++) {
                renderBuffer.Put(NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocPickableCount));
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

            // [WARNING] An "inputBufferSize" too small would make backend "Room.OnBattleCmdReceived" trigger "clientInputFrameId < inputBuffer.StFrameId (a.k.a. obsolete inputFrameUpsync#1)" too frequently!
            bool renderBufferSizeTooSmall = (128 >= renderBufferSize);
            int inputBufferSize = renderBufferSizeTooSmall ? renderBufferSize : (renderBufferSize >> 1) + 1;
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

            decodedInputHolder = new InputFrameDecoded();

            prevDecodedInputHolder = new InputFrameDecoded();

            confirmedBattleResult = new BattleResult {
                WinnerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT,
                WinnerBulletTeamId = TERMINATING_BULLET_TEAM_ID
            };
        }

        public static void provisionStepHolders(int roomCapacity, FrameRingBuffer<RoomDownsyncFrame> renderBuffer, FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer, FrameRingBuffer<InputFrameDownsync> inputBuffer, int[] lastIndividuallyConfirmedInputFrameId, ulong[] lastIndividuallyConfirmedInputList, Vector[] effPushbacks, Vector[][] hardPushbackNormsArr, Vector[] softPushbacks, BattleResult confirmedBattleResult) {
            if (0 >= roomCapacity) {
                throw new ArgumentException(String.Format("roomCapacity={0} is non-positive, please initialize it first!", roomCapacity));
            }

            renderBuffer.Clear(); // Then use it by "DryPut"
            pushbackFrameLogBuffer.Clear(); // Then use it by "DryPut"

            inputBuffer.Clear(); // Then use it by "DryPut"

            Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
            Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);

            for (int i = 0; i < effPushbacks.Length; i++) {
                effPushbacks[i].X = 0;
                effPushbacks[i].Y = 0;
            }

            for (int i = 0; i < hardPushbackNormsArr.Length; i++) {
                for (int j = 0; j < hardPushbackNormsArr[i].Length; j++) {
                    hardPushbackNormsArr[i][j].X = 0;
                    hardPushbackNormsArr[i][j].Y = 0;
                }
            }

            for (int i = 0; i < softPushbacks.Length; i++) {
                softPushbacks[i].X = 0;
                softPushbacks[i].Y = 0;
            }

            resetBattleResult(ref confirmedBattleResult);
        }

        public static void clearColliders(ref CollisionSpace collisionSys, ref Collider[] dynamicRectangleColliders, ref Collider[] staticColliders, ref Collision collisionHolder, ref List<Collider> completelyStaticTrapColliders, ref FrameRingBuffer<Collider> residueCollided) {
            if (null != collisionSys) {
                // [WARNING] Explicitly cutting potential cyclic referencing among [CollisionSpace, Cell, Collider, Protoc generated CharacterDownsync/Bullet/XxxColliderAttr/...]. The "Protoc generated CharacterDownsync/Bullet/XxxColliderAttr/..." instances would NOT have any reference to "CollisionSpace/Cell/Collider", hence a unidirectional cleanup should be possible.
                collisionSys.RemoveAll();
            }

            if (null != dynamicRectangleColliders) {
                for (int i = 0; i < dynamicRectangleColliders.Length; i++) {
                    var c = dynamicRectangleColliders[i];
                    if (null == c) continue;
                    c.clearTouchingCellsAndData();
                }
                dynamicRectangleColliders = new Collider[0]; // dereferencing existing "Colliders"
            }

            if (null != staticColliders) {
                for (int i = 0; i < staticColliders.Length; i++) {
                    var c = staticColliders[i];
                    if (null == c) continue;
                    c.clearTouchingCellsAndData();
                }
                staticColliders = new Collider[0]; // dereferencing existing "Colliders"
            }

            if (null != completelyStaticTrapColliders) {
                for (int i = 0; i < completelyStaticTrapColliders.Count; i++) {
                    var c = completelyStaticTrapColliders[i];
                    if (null == c) continue;
                    c.clearTouchingCellsAndData();
                }
                completelyStaticTrapColliders = new List<Collider>(); // dereferencing existing "Colliders"
            }

            if (null != residueCollided) {
                for (int i = 0; i < residueCollided.Eles.Length; i++) {
                    var c = residueCollided.Eles[i];
                    if (null == c) continue;
                    c.clearTouchingCellsAndData();
                }  
                residueCollided = new FrameRingBuffer<shared.Collider>(0); // dereferencing existing "Colliders"
            }

            collisionHolder.ClearDeep();
        }

        public static void refreshColliders(RoomDownsyncFrame startRdf, RepeatedField<SerializableConvexPolygon> serializedBarrierPolygons, RepeatedField<SerializedCompletelyStaticPatrolCueCollider> serializedStaticPatrolCues, RepeatedField<SerializedCompletelyStaticTrapCollider> serializedCompletelyStaticTraps, RepeatedField<SerializedCompletelyStaticTriggerCollider> serializedStaticTriggers, SerializedTrapLocalIdToColliderAttrs serializedTrapLocalIdToColliderAttrs, SerializedTriggerEditorIdToLocalId serializedTriggerEditorIdToLocalId, int spaceOffsetX, int spaceOffsetY, ref CollisionSpace collisionSys, ref int maxTouchingCellsCnt, ref Collider[] dynamicRectangleColliders, ref Collider[] staticColliders, out int staticCollidersCnt, ref Collision collisionHolder, ref FrameRingBuffer<Collider> residueCollided, ref List<Collider> completelyStaticTrapColliders, ref Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs, ref Dictionary<int, int> triggerEditorIdToLocalId, ref Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToConfigFromTiled) {
            /*
            [WARNING] 
    
            Deliberately still re-allocating heap RAM for each individual "Collider" instance, because the number of points in "Collider.Shape" is variable and hence not trivial to reuse.

            It's possible though to just limit "ConvexPolygon.Points" by a certain upper cap number, then reset/clear it each time we call "refreshColliders" -- yet breaching code readibility to such an extent for just little gain in performance is quite inconvenient, i.e. "refreshColliders" is only called once per battle, and no memory leak by the current approach.  
            */

            int cellWidth = 64;
            int cellHeight = 128; // To avoid dynamic trap as a standing point to slip when moving down along with the character
            collisionSys = new CollisionSpace((spaceOffsetX << 1), (spaceOffsetY << 1), cellWidth, cellHeight); // spaceOffsetX, spaceOffsetY might change for each map, so not reusing memory here due to similar reason given above for "Collider"

            collisionHolder = new Collision();

            int residueCollidedCap = 256;
            residueCollided = new FrameRingBuffer<shared.Collider>(residueCollidedCap); // Would be cleared each time it's used in a collision

            int dynamicRectangleCollidersCap = 192;
            dynamicRectangleColliders = new Collider[dynamicRectangleCollidersCap];

            maxTouchingCellsCnt = (((spaceOffsetX << 1) + cellWidth) / cellWidth) * (((spaceOffsetY << 1) + cellHeight) / cellHeight) + 1;
            for (int i = 0; i < dynamicRectangleColliders.Length; i++) {
                var srcPolygon = NewRectPolygon(0, 0, 0, 0, 0, 0, 0, 0);
                dynamicRectangleColliders[i] = NewConvexPolygonCollider(srcPolygon, 0, 0, maxTouchingCellsCnt, null);
            }

            staticColliders = new Collider[128];
            staticCollidersCnt = 0;
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
                if (0 < (s.Attr.CollisionTypeMask & COLLISION_REFRACTORY_INDEX_PREFIX)) {   
                    trapCollider.Mask = s.Attr.CollisionTypeMask;   
                } else {
                    trapCollider.Mask = COLLISION_TRAP_INDEX_PREFIX; // [WARNING] NOT using "s.Attr.CollisionTypeMask", see comments in "room_downsync_frame.proto > SerializedCompletelyStaticTrapCollider"  
                }
                staticColliders[staticCollidersCnt++] = trapCollider;
                completelyStaticTrapColliders.Add(trapCollider);
            }

            for (int i = 0; i < serializedStaticTriggers.Count; i++) {
                var s = serializedStaticTriggers[i];
                float[] points = new float[s.Polygon.Points.Count];
                s.Polygon.Points.CopyTo(points, 0);
                var triggerPolygon = new ConvexPolygon(s.Polygon.AnchorX, s.Polygon.AnchorY, points);
                var triggerCollider = NewConvexPolygonCollider(triggerPolygon, 0, 0, maxTouchingCellsCnt, s.Attr);
                var trigger = startRdf.TriggersArr[s.Attr.TriggerLocalId-1];
                var triggerConfig = triggerConfigs[s.Attr.SpeciesId];
                triggerCollider.Mask = triggerConfig.CollisionTypeMask;
                staticColliders[staticCollidersCnt++] = triggerCollider;
            }

            if (null == trapLocalIdToColliderAttrs) {
                trapLocalIdToColliderAttrs = new Dictionary<int, List<TrapColliderAttr>>();
            } else {
                trapLocalIdToColliderAttrs.Clear();
            }
            foreach (var entry in serializedTrapLocalIdToColliderAttrs.Dict) {
                var trapLocalId = entry.Key;
                var attrs = entry.Value.List;
                var colliderAttrs = new List<TrapColliderAttr>();
                colliderAttrs.AddRange(attrs);
                trapLocalIdToColliderAttrs[trapLocalId] = colliderAttrs;
            }

            triggerEditorIdToLocalId = new Dictionary<int, int>();
            foreach (var entry in serializedTriggerEditorIdToLocalId.Dict) {
                triggerEditorIdToLocalId[entry.Key] = entry.Value;
            }
            triggerEditorIdToConfigFromTiled = new Dictionary<int, TriggerConfigFromTiled>();
            foreach (var entry in serializedTriggerEditorIdToLocalId.Dict2) {
                triggerEditorIdToConfigFromTiled[entry.Key] = entry.Value;
            }

            for (int i = 0; i < staticCollidersCnt; i++) {
                collisionSys.AddSingleToCellTail(staticColliders[i]);
            }
        }
    }

    public sealed partial class BulletConfig {
        
        public BulletConfig UpsertCancelTransit(int patternId, uint skillId) {
            if (this.CancelTransit.ContainsKey(patternId)) {
                this.CancelTransit.Remove(patternId);
            }
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

        public BulletConfig SetStartupVfxSpeciesId(int val) {
            this.StartupVfxSpeciesId = val;
            return this;
        }

        public BulletConfig SetStartupFrames(int val) {
            this.StartupFrames = val;
            return this;
        }

        public BulletConfig SetStartupInvinsibleFrames(int val) {
            this.StartupInvinsibleFrames = val;
            return this;
        }

        public BulletConfig SetActiveFrames(int val) {
            this.ActiveFrames = val;
            return this;
        }

        public BulletConfig SetHitStunFrames(int val) {
            this.HitStunFrames = val;
            return this;
        }

        public BulletConfig SetHitInvinsibleFrames(int val) {
            this.HitInvinsibleFrames = val;
            return this;
        }

        public BulletConfig SetFinishingFrames(int val) {
            this.FinishingFrames = val;
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

        public BulletConfig SetHitboxSizes(int hitboxSizeX, int hitboxSizeY) {
            this.HitboxSizeX = hitboxSizeX;
            this.HitboxSizeY = hitboxSizeY;
            return this;
        }

        public BulletConfig SetSelfLockVel(int x, int y, int yWhenFlying) {
            this.SelfLockVelX = x;
            this.SelfLockVelY = y;
            this.SelfLockVelYWhenFlying = yWhenFlying;
            return this;
        }

        public BulletConfig SetDamage(int val) {
            this.Damage = val;
            return this;
        }

        public BulletConfig SetCancellableFrames(int st, int ed) {
            this.CancellableStFrame = st;
            this.CancellableEdFrame = ed;
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

        public BulletConfig SetCollisionTypeMask(ulong val) {
            CollisionTypeMask = val;
            return this;
        }

        public BulletConfig SetOmitSoftPushback(bool val) {
            OmitSoftPushback = val;
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

        public BulletConfig SetBlowUp(bool val) {
            BlowUp = val;
            return this;
        }

        public BulletConfig SetSpinAnchor(float x, float y) {
            SpinAnchorX = x;
            SpinAnchorY = y;
            return this;
        }

        public BulletConfig SetAngularVel(float cosVal, float sinVal) {
            AngularFrameVelCos = cosVal;
            AngularFrameVelSin = sinVal;
            return this;
        }

        public BulletConfig SetMhInheritsSpin(bool val) {
            MhInheritsSpin = val;
            return this;
        }

        public BulletConfig SetElementalAttrs(uint val) {
            ElementalAttrs = val;
            return this;
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

    public sealed partial class StoryPointStep {
        public StoryPointStep(StoryPointDialogLine[] lines) {
            this.Lines.AddRange(lines);
        }
        public StoryPointStep AddLine(StoryPointDialogLine line) {
            Lines.Add(line);
            return this;
        }
    }

    public sealed partial class StoryPoint {
        public StoryPoint(StoryPointStep[] steps) {
            this.Steps.AddRange(steps);
        }
        public StoryPoint AddStep(StoryPointStep step) {
            Steps.Add(step);
            return this;
        }
    }
    
    public sealed partial class LevelStory {
        public LevelStory(StoryPoint[] seqPoints) {
            for (int i = 1; i < seqPoints.Length; i++) {
                Points.Add(i, seqPoints[i-1]);
            }
        }
        public LevelStory UpdatePoint(int pointId, StoryPoint point) {
            Points.Add(pointId, point);
            return this;
        }

    }
    
    public sealed partial class InputFrameDecoded {
        public void Reset() {
            Dx = 0;
            Dy = 0;
            BtnALevel = 0;
            BtnBLevel = 0;
            BtnCLevel = 0;
            BtnDLevel = 0;
            BtnELevel = 0;
        }
    
        public bool HasCriticalBtnLevel() {
            return 0 < BtnALevel || 0 < BtnBLevel || 0 < BtnCLevel || 0 < BtnDLevel || 0 < BtnELevel;
        }
        
        public void cloneInto(InputFrameDecoded holder) {
            holder.Dx = Dx;
            holder.Dy = Dy;
            holder.BtnALevel = BtnALevel;
            holder.BtnBLevel = BtnBLevel;
            holder.BtnCLevel = BtnCLevel;
            holder.BtnDLevel = BtnDLevel;
            holder.BtnELevel = BtnELevel;
        }
    }
}
