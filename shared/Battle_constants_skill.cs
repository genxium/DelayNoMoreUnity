using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        public static int PATTERN_ID_UNABLE_TO_OP = -2;
        public static int PATTERN_ID_NO_OP = -1;
        public const int PATTERN_B = 1;
        public const int PATTERN_UP_B = 2;
        public const int PATTERN_DOWN_B = 3;
        public const int PATTERN_HOLD_B = 4;
        public const int PATTERN_DOWN_A = 5;
        public const int PATTERN_RELEASED_B = 6;

        public const int PATTERN_E = 7;
        public const int PATTERN_FRONT_E = 8;
        public const int PATTERN_BACK_E = 9;
        public const int PATTERN_UP_E = 10;
        public const int PATTERN_DOWN_E = 11;
        public const int PATTERN_HOLD_E = 12;
        
        public const int PATTERN_E_HOLD_B = 13;
        public const int PATTERN_FRONT_E_HOLD_B = 14;
        public const int PATTERN_BACK_E_HOLD_B = 15;
        public const int PATTERN_UP_E_HOLD_B = 16;
        public const int PATTERN_DOWN_E_HOLD_B = 17;
        public const int PATTERN_HOLD_E_HOLD_B = 18;

        public static HashSet<int> btnBHoldingPatternSet = new HashSet<int>() {
            PATTERN_HOLD_B,
            PATTERN_E_HOLD_B,
            PATTERN_FRONT_E_HOLD_B,
            PATTERN_BACK_E_HOLD_B,
            PATTERN_UP_E_HOLD_B,
            PATTERN_DOWN_E_HOLD_B,
            PATTERN_HOLD_E_HOLD_B
        };

        public const int PATTERN_INVENTORY_SLOT_C = 1024;
        public const int PATTERN_INVENTORY_SLOT_D = 1025;
        public const int PATTERN_INVENTORY_SLOT_BC = 1026;

        public const uint ELE_NONE = 0;
        public const uint ELE_FIRE = 1;
        public const uint ELE_WATER = 2;
        public const uint ELE_THUNDER = 4;
        public const uint ELE_ROCK = 8;
        public const uint ELE_WIND = 16;
        public const uint ELE_ICE = 32;

        public const float ELE_WEAKNESS_DEFAULT_YIELD = 1.5f;
        public const float ELE_RESISTANCE_DEFAULT_YIELD = 0.5f;

        public static uint BladeGirlGroundSlash1Id = 1, BladeGirlGroundSlash2Id = 2, BladeGirlGroundSlash3Id = 3, BladeGirlGroundSuperSlashId = 4, BladeGirlDashingId = 11, BladeGirlDiverImpactId = 77, BladeGirlSlidingSlashId = 84, BladeGirlCrouchSlashId = 94, BladeGirlAirSlash1Id = 134, BladeGirlAirSlash2Id = 135, BladeGirlDragonPunchId = 136; // For easier cancellation config

        public static BulletConfig BasicDashingHit1 = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 7,
            ActiveFrames = 4,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DelaySelfVelToActive = true,
            OmitSoftPushback = true,
            CancellableStFrame = 5,
            CancellableEdFrame = 10,
            BType = BulletType.Melee,
            MhType = MultiHitType.FromEmission,
        };

        public static BulletConfig BasicDashingHit2 = new BulletConfig {
            StartupFrames = BasicDashingHit1.StartupFrames + BasicDashingHit1.ActiveFrames,
            ActiveFrames = 13,
            OmitSoftPushback = true,
            CancellableStFrame = BasicDashingHit1.StartupFrames + BasicDashingHit1.ActiveFrames,
            CancellableEdFrame = 19,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(4.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            BType = BulletType.Melee,
        };

        public static Skill BasicDashing = new Skill {
            Id = 52,
               RecoveryFrames = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
               RecoveryFramesOnBlock = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
               RecoveryFramesOnHit = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Dashing
        }
        .AddHit(BasicDashingHit1)
        .AddHit(BasicDashingHit2);

        public static BulletConfig BasicSlidingHit1 = new BulletConfig {
            StartupFrames = 4,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 7,
            OmitSoftPushback = true,
            CancellableStFrame = 6,
            CancellableEdFrame = 15,
            CancellableByInventorySlotC = true,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            StartupVfxSpeciesId = VfxSmokeNDust1.SpeciesId,
            ActiveVfxSpeciesId = VfxSmokeNDust1.SpeciesId,
            IsPixelatedActiveVfx = true,
            MhType = MultiHitType.FromEmission,
        };

        public static BulletConfig BasicSlidingHit2 = new BulletConfig {
            StartupFrames = BasicSlidingHit1.StartupFrames + BasicSlidingHit1.ActiveFrames,
            ActiveFrames = 12,
            OmitSoftPushback = true,
            CancellableStFrame = BasicSlidingHit1.StartupFrames + BasicSlidingHit1.ActiveFrames,
            CancellableEdFrame = 40,
            CancellableByInventorySlotC = true,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            BType = BulletType.Melee,
            StartupVfxSpeciesId = VfxSmokeNDust1.SpeciesId,
            ActiveVfxSpeciesId = VfxSmokeNDust1.SpeciesId,
            IsPixelatedActiveVfx = true,
        };
        
        public static Skill BasicSliding = new Skill {
            Id = 53,
               RecoveryFrames = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
               RecoveryFramesOnBlock = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
               RecoveryFramesOnHit = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Sliding
        }
        .AddHit(BasicSlidingHit1)
        .AddHit(BasicSlidingHit2);

        public static BulletConfig BatMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 2,
            ActiveFrames = 5,
            HitStunFrames = 1,
            HitInvinsibleFrames = 1,
            BlockStunFrames = 1,
            Damage = 5,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = 0,
            HitboxSizeX = (int)(28 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill BatMelee1PrimerSkill = new Skill {
            Id = 260,
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 60,
            RecoveryFramesOnHit = 60,
            MpDelta = 30,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
            .AddHit(BatMelee1PrimerBullet);

        public static BulletConfig SwordManMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 16,
            HitStunFrames = 10,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 9,
            Damage = 16,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            CancellableStFrame = 19,
            CancellableEdFrame = 40,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, 118);

        public static BulletConfig SwordManMelee1GroundHit2Bl = new BulletConfig(SwordManMelee1PrimerBullet)
                                                               .SetStartupFrames(9)
                                                               .SetActiveFrames(20)
                                                               .SetHitStunFrames(12)
                                                               .SetCancellableFrames(18, 35)
                                                               .UpsertCancelTransit(PATTERN_B, 119)
                                                               .SetDamage(10);

        public static BulletConfig SwordManMelee1GroundHit3Bl = new BulletConfig(SwordManMelee1PrimerBullet)
                                                                .SetStartupFrames(7)
                                                                .SetActiveFrames(22)
                                                                .SetHitStunFrames(12)
                                                                .SetRemainsUponHit(true)
                                                                .SetPushbacks((int)(1.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSelfLockVel((int)(2.4f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0) 
                                                                .SetCancellableFrames(0, 0)
                                                                .SetMhType(MultiHitType.FromEmission)
                                                                .UpsertCancelTransit(PATTERN_B, NO_SKILL)
                                                                .SetDamage(9);

        public static BulletConfig SwordManMelee1GroundHit3BlStopper = new BulletConfig(SwordManMelee1GroundHit3Bl)
                                                                .SetStartupFrames(SwordManMelee1GroundHit3Bl.StartupFrames + SwordManMelee1GroundHit3Bl.ActiveFrames)
                                                                .SetStartupInvinsibleFrames(2)
                                                                .SetHitboxSizes(0, 0)
                                                                .SetHitboxOffsets(0, 0)
                                                                .SetActiveFrames(11)
                                                                .SetRemainsUponHit(true)
                                                                .SetSelfLockVel(0, 0, 0) 
                                                                .SetCancellableFrames(0, 0)
                                                                .SetMhType(MultiHitType.None)
                                                                .SetDamage(0);
        
        public static Skill SwordManMelee1PrimerSkill = new Skill {
            Id = 5,
            RecoveryFrames = 50,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(SwordManMelee1PrimerBullet);

        public static Skill SwordManMelee1GroundHit2 = new Skill {
            RecoveryFrames = 40,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(SwordManMelee1GroundHit2Bl);

        public static Skill SwordManMelee1GroundHit3 = new Skill {
            RecoveryFrames = 50,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3
        }
        .AddHit(SwordManMelee1GroundHit3Bl)
        .AddHit(SwordManMelee1GroundHit3BlStopper);

        public static BulletConfig SwordManMelee1PrimerBulletAir = new BulletConfig(SwordManMelee1PrimerBullet)
                                                                    .SetStartupFrames(11)
                                                                    .SetDamage(15);

        public static Skill SwordManMelee1PrimerSkillAir = new Skill {
            RecoveryFrames = 40,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            MpDelta = 0,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk1,
        }
        .AddHit(SwordManMelee1PrimerBulletAir);

        public static BulletConfig SwordManDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 11,
            ActiveFrames = 20,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            DirX = 1,
            DirY = 0,
            ExplosionFrames = 25,
            Hardness = 5,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig FireSwordManMelee1PrimerBullet = new BulletConfig(SwordManMelee1PrimerBullet)
        .SetStartupFrames(9)
        .SetActiveFrames(20)
        .SetCancellableFrames(17, 35)
        .SetHitStunFrames(15)
        .SetDamage(22)
        .SetElementalAttrs(ELE_FIRE)
        .UpsertCancelTransit(PATTERN_B, 120);

        public static Skill FireSwordManMelee1PrimerSkill = new Skill{
            Id = 16,
            RecoveryFrames = 35,
            RecoveryFramesOnBlock = 35,
            RecoveryFramesOnHit = 35,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(FireSwordManMelee1PrimerBullet);

        public static BulletConfig FireSwordManMelee1GroundHit2Bl = new BulletConfig(FireSwordManMelee1PrimerBullet)
                                                               .SetStartupFrames(9)
                                                               .SetActiveFrames(20)
                                                               .SetHitStunFrames(12)
                                                               .SetCancellableFrames(18, 35)
                                                               .UpsertCancelTransit(PATTERN_B, 121)
                                                               .SetDamage(15);

        public static BulletConfig FireSwordManMelee1GroundHit3Bl = new BulletConfig(FireSwordManMelee1PrimerBullet)
                                                                .SetStartupFrames(7)
                                                                .SetActiveFrames(22)
                                                                .SetHitStunFrames(12)
                                                                .SetRemainsUponHit(true)
                                                                .SetPushbacks((int)(1.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSelfLockVel((int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0, 0) 
                                                                .SetCancellableFrames(0, 0)
                                                                .SetMhType(MultiHitType.FromEmission)
                                                                .UpsertCancelTransit(PATTERN_B, NO_SKILL)
                                                                .SetDamage(12);

        public static BulletConfig FireSwordManMelee1GroundHit3BlStopper = new BulletConfig(FireSwordManMelee1GroundHit3Bl)
                                                                .SetStartupFrames(29)
                                                                .SetActiveFrames(11)
                                                                .SetRemainsUponHit(true)
                                                                .SetSelfLockVel(0, 0, 0) 
                                                                .SetCancellableFrames(0, 0)
                                                                .SetMhType(MultiHitType.None)
                                                                .SetDamage(12);

         public static Skill FireSwordManMelee1GroundHit2 = new Skill {
                RecoveryFrames = 35,
                RecoveryFramesOnBlock = 30,
                RecoveryFramesOnHit = 30,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = Atk2
        }
        .AddHit(FireSwordManMelee1GroundHit2Bl);

        public static Skill FireSwordManMelee1GroundHit3 = new Skill {
                RecoveryFrames = 45,
                RecoveryFramesOnBlock = 40,
                RecoveryFramesOnHit = 40,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = Atk3
        }
        .AddHit(FireSwordManMelee1GroundHit3Bl)
        .AddHit(FireSwordManMelee1GroundHit3BlStopper);

        public static BulletConfig FireSwordManMelee1PrimerBulletAir = new BulletConfig(FireSwordManMelee1PrimerBullet).SetDamage(24);

        public static Skill FireSwordManMelee1PrimerSkillAir = new Skill {
            RecoveryFrames = 35,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk1,
        }
        .AddHit(FireSwordManMelee1PrimerBulletAir);

        public static BulletConfig FireSwordManDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 13,
            ActiveFrames = 20,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlockStunFrames = 9,
            Damage = 28,
            PushbackVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_FIRE,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill FireSwordManDragonPunchPrimerSkill = new Skill{
            Id = 17,
            RecoveryFrames = 40,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk6
        }
        .AddHit(FireSwordManDragonPunchPrimerBullet);

        public static BulletConfig FireSwordManFireballPrimerBullet = new BulletConfig {
                StartupFrames = 21,
                ActiveFrames = 360,
                HitStunFrames = 3,
                HitInvinsibleFrames = 8,
                BlockStunFrames = 3,
                Damage = 25,
                PushbackVelX = (int)(.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                PushbackVelY = NO_LOCK_VEL,
                SelfLockVelX = NO_LOCK_VEL,
                SelfLockVelY = NO_LOCK_VEL,
                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                HitboxOffsetX = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxOffsetY = (int)(9 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeX = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeY = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                ElementalAttrs = ELE_FIRE,
                BlowUp = false,
                SpeciesId = 4,
                Speed = (int)(4.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                DirX = 1,
                DirY = 0,
                ExplosionFrames = 25,
                Hardness = 4,
                BType = BulletType.Fireball,
                CharacterEmitSfxName = "FlameEmit1",
                ExplosionSfxName = "Explosion4",
                CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill FireSwordManFireballSkill = new Skill {
            Id = 13,
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            MpDelta = 600,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk4
        }
        .AddHit(FireSwordManFireballPrimerBullet);

        public static BulletConfig FireSwordManFireBreathBl1 = new BulletConfig {
            StartupFrames = 13,
            ActiveFrames = 33,
            HitStunFrames = 20,
            BlockStunFrames = 9,
            Damage = 12,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(64 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_FIRE,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            RemainsUponHit = true,
            NoExplosionOnHardPushback = true,
            CharacterEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "FlameBurning1",
            MhType = MultiHitType.FromEmission,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig FireSwordManFireBreathBl2 = new BulletConfig(FireSwordManFireBreathBl1)
                                                                .SetStartupFrames(FireSwordManFireBreathBl1.StartupFrames + FireSwordManFireBreathBl1.ActiveFrames);

        public static BulletConfig FireSwordManFireBreathBl3 = new BulletConfig(FireSwordManFireBreathBl2)
                                                                .SetStartupFrames(FireSwordManFireBreathBl2.StartupFrames + FireSwordManFireBreathBl2.ActiveFrames)
                                                                .SetMhType(MultiHitType.None);

        public static Skill FireSwordManFireBreathSkill = new Skill{
            RecoveryFrames = 120,
                           RecoveryFramesOnBlock = 120,
                           RecoveryFramesOnHit = 120,
                           TriggerType = SkillTriggerType.RisingEdge,
                           BoundChState = Atk5
        }
        .AddHit(FireSwordManFireBreathBl1)
        .AddHit(FireSwordManFireBreathBl2)
        .AddHit(FireSwordManFireBreathBl3)
        ;

        public static BulletConfig DemonFireBreathHit = new BulletConfig {
            StartupFrames = 24,
            StartupInvinsibleFrames = 15,
            ActiveFrames = 10,
            HitStunFrames = 12,
            HitInvinsibleFrames = 18,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(45f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(30f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(120f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 6,
            ExplosionSpeciesId = 2,
            ElementalAttrs = ELE_FIRE,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "FlameBurning1",
            IsPixelatedActiveVfx = true,
            SpinAnchorX = 0f,
            SpinAnchorY = 6.0f, // [WARNING] Half of "HitboxSizeY"; kindly note that "SpinAnchorX & SpinAnchorY" for non-melee bullets are constrained by the "pivot points" set on the sprites
            AngularFrameVelCos = (float)Math.Cos(4f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            AngularFrameVelSin = (float)Math.Sin(-4f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            InitSpinCos = (float)Math.Cos(0.135f * Math.PI),
            InitSpinSin = (float)Math.Sin(0.135f * Math.PI),
            MhType = MultiHitType.FromEmission,
            NoExplosionOnHardPushback = true, // [WARNING] Such that it doesn't vanish on hardpushback!
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig DemonFireBreathHitRepeating1 = new BulletConfig(DemonFireBreathHit)
                                                              //.SetHitboxOffsets(0, 0)
                                                              .SetStartupFrames(DemonFireBreathHit.StartupFrames + DemonFireBreathHit.ActiveFrames)
                                                              .SetMhInheritsSpin(true);

        public static BulletConfig DemonFireBreathHitRepeating2 = new BulletConfig(DemonFireBreathHitRepeating1)
                                                              .SetStartupFrames(DemonFireBreathHitRepeating1.StartupFrames + DemonFireBreathHitRepeating1.ActiveFrames);

        public static BulletConfig DemonFireBreathHitRepeating3 = new BulletConfig(DemonFireBreathHitRepeating2)
                                                              .SetFinishingFrames(12)
                                                              .SetStartupFrames(DemonFireBreathHitRepeating2.StartupFrames + DemonFireBreathHitRepeating2.ActiveFrames);

        public static Skill DemonFireBreathSkill = new Skill {
            Id = 82,
            RecoveryFrames = DemonFireBreathHitRepeating3.StartupFrames + DemonFireBreathHitRepeating3.ActiveFrames + DemonFireBreathHitRepeating3.FinishingFrames,
            RecoveryFramesOnBlock = DemonFireBreathHitRepeating3.StartupFrames + DemonFireBreathHitRepeating3.ActiveFrames + DemonFireBreathHitRepeating3.FinishingFrames,
            RecoveryFramesOnHit = DemonFireBreathHitRepeating3.StartupFrames + DemonFireBreathHitRepeating3.ActiveFrames + DemonFireBreathHitRepeating3.FinishingFrames,
            MpDelta = 750,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5
        }
        .AddHit(DemonFireBreathHit)
        .AddHit(DemonFireBreathHitRepeating1)
        .AddHit(DemonFireBreathHitRepeating2)
        .AddHit(DemonFireBreathHitRepeating3)
        ;

        public static BulletConfig DemonFireSlimeMelee1PrimaryBullet = new BulletConfig {
            StartupFrames = 40,
            ActiveFrames = 5,
            HitStunFrames = 15,
            HitInvinsibleFrames = 16,
            BlockStunFrames = 3,
            Damage = 30,
            PushbackVelX = (int)(2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(-8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = 0,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(64 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-20 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(80 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = false,
            SpeciesId = 3,
            MhType = MultiHitType.FromEmission,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill DemonFireSlimeMelee1PrimarySkill = new Skill {
            Id = 14,
            RecoveryFrames = 110,
            RecoveryFramesOnBlock = 110,
            RecoveryFramesOnHit = 110,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(DemonFireSlimeMelee1PrimaryBullet);

        private static BulletConfig FireTornadoStarterBullet = new BulletConfig {
            StartupFrames = 25,
            ActiveFrames = 192,
            HitStunFrames = 25,
            BlockStunFrames = 60,
            Damage = 30,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 12,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_FIRE,
            Speed = (int)(3 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 15,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            BType = BulletType.GroundWave,
            CharacterEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "Explosion4",
            MhType = MultiHitType.FromPrevHitActual,
            MhVanishOnMeleeHit = false, // Makes it more powerful on ground than the SlashNova
            RemainsUponHit = true,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX, 
        };

        private static BulletConfig FireTornadoRepeatingBullet = new BulletConfig {
            StartupFrames = 0,
            ActiveFrames = 30,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 90,
            BlockStunFrames = 60,
            Damage = 15,
            PushbackVelX = (int)(4f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 22,
            ElementalAttrs = ELE_FIRE,
            ExplosionSpeciesId = 15,
            ExplosionFrames = 25,
            Speed = 0,
            DirX = 1,
            DirY = 0,
            Hardness = 15,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion4",
            BlowUp = true,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig DemonFireSlimeFireballWeakBullet = new BulletConfig {
            StartupFrames = 25,
            ActiveFrames = 360,
            HitStunFrames = 25,
            BlockStunFrames = 9,
            Damage = 20,
            PushbackVelX = (int)(0.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ElementalAttrs = ELE_FIRE,
            Speed = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            FireballEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "Explosion4",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX,
            SimultaneousMultiHitCnt = 2
        };

        public static BulletConfig DemonFireSlimeFireballPivotBullet = FireTornadoStarterBullet;

        public static Skill DemonFireSlimeFireballSkill = new Skill {
            Id = 15,
            RecoveryFrames = 120,
            RecoveryFramesOnBlock = 120,
            RecoveryFramesOnHit = 120,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(DemonFireSlimeFireballPivotBullet)
        .AddHit(FireTornadoRepeatingBullet);

        private static BulletConfig WaterSpikeStarterBullet = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 640,
            HitStunFrames = 40,
            BlockStunFrames = 40,
            Damage = 30,
            PushbackVelX = (int)(-2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(6.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 15,
            ExplosionSpeciesId = 5,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_WATER,
            Speed = (int)(2.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 9,
            CancellableStFrame = 12,
            CancellableEdFrame = 36,
            CancellableByInventorySlotC = true,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            BType = BulletType.GroundWave,
            CharacterEmitSfxName = "WaterEmitSpd1",
            ExplosionSfxName = "Explosion4",
            MhType = MultiHitType.FromPrevHitActual,
            RemainsUponHit = true,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX, 
        }.UpsertCancelTransit(PATTERN_B, 47).UpsertCancelTransit(PATTERN_DOWN_B, 47).UpsertCancelTransit(PATTERN_UP_B, 47);

        private static BulletConfig MagicPistolBulletAir = new BulletConfig {
            StartupFrames = 2,
            ActiveFrames = 180,
            HitStunFrames = 14,
            BlockStunFrames = 12,
            Damage = 15,
            PushbackVelX = (int)(0.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 19,
            Speed = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "PistolEmit",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig MagicPistolBulletGround = new BulletConfig(MagicPistolBulletAir)
                                                                .SetHitboxOffsets((int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetAllowsWalking(true) 
                                                                .SetAllowsCrouching(true);

        private static BulletConfig MagicPistolBulletCrouch = new BulletConfig(MagicPistolBulletAir)
                                                                .SetHitboxOffsets((int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSelfLockVel(0, 0, 0);

        private static BulletConfig MagicCannonBulletAir = new BulletConfig {
            StartupFrames = 2,
            ActiveFrames = 120,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlockStunFrames = 2,
            BlowUp = true,
            Damage = 22,
            PushbackVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 20,
            ExplosionSpeciesId = 19,
            Speed = (int)(13 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 8,
            RemainsUponHit = true,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            RejectsReflectionFromAnotherBullet = true,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Explosion4",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig MagicCannonBulletGround = new BulletConfig(MagicCannonBulletAir)
                                                                .SetHitboxOffsets((int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetAllowsWalking(true) 
                                                                .SetAllowsCrouching(true);

        private static BulletConfig BasicGunBulletAir = new BulletConfig {
            StartupFrames = 4,
            ActiveFrames = 180,
            HitStunFrames = 7,
            BlockStunFrames = 7,
            Damage = 10,
            PushbackVelX = (int)(0.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            Speed = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 4,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "PistolEmit",
            ActiveVfxSpeciesId = VfxPistolSpark.SpeciesId,
            IsPixelatedActiveVfx = true,
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig BasicGunBulletGround = new BulletConfig(BasicGunBulletAir)
                                                                .SetHitboxOffsets((int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetAllowsWalking(true) 
                                                                .SetAllowsCrouching(true);

        private static BulletConfig BasicPistolBulletCrouch = new BulletConfig(BasicGunBulletAir)
                                                                .SetHitboxOffsets((int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSelfLockVel(0, 0, 0);

        private static BulletConfig SlashNovaRepeatingBullet = new BulletConfig {
            StartupFrames = 10,
            ActiveFrames = 600,
            HitStunFrames = 20,
            BlockStunFrames = 9,
            Damage = 5,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = 0,
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(28 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionOffsetX = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(72 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            AngularFrameVelCos = (float)Math.Cos(15.0/(Math.PI * BATTLE_DYNAMICS_FPS)), // human readable number is in degrees/second
            AngularFrameVelSin = (float)Math.Sin(15.0/(Math.PI * BATTLE_DYNAMICS_FPS)),
            SpeciesId = 9,
            ExplosionSpeciesId = 9,
            ExplosionFrames = 25,
            Speed = (int)(.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeedIfNotHit = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ElementalAttrs = ELE_WIND,
            BType = BulletType.MissileLinear,
            MissileSearchIntervalPow2Minus1 = (1u << 3) - 1u,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActual,
            MhVanishOnMeleeHit = true,
            GaugeIncReductionRatio = 0.5f,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig SlashNovaStarterBullet = new BulletConfig(SlashNovaRepeatingBullet).SetStartupFrames(9)
                                                            .SetSelfLockVel(0, 0, 0)
                                                            .SetHitboxOffsets((int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                            .SetSpeed((int)(4.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                            .SetActiveVfxSpeciesId(VfxMovingTornado.SpeciesId)      
                                                            .SetIsPixelatedActiveVfx(true);

        private static BulletConfig SlashNovaEnderBullet = new BulletConfig(SlashNovaRepeatingBullet).SetStartupFrames(9).SetMhType(MultiHitType.None)
                                                            .SetSpeedIfNotHit(0)
                                                            .SetSpeed(SlashNovaRepeatingBullet.SpeedIfNotHit)
                                                            .SetPushbacks(
                                                                (int)(0.3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // The last hit has some pushback 
                                                                NO_LOCK_VEL
                                                            );

        private static BulletConfig FirePillarBullet = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 4,
            ActiveFrames = 49,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 90,
            BlockStunFrames = 60,
            BlowUp = true,
            Damage = 20,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = (int)(8.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeIncY = 5,
            SpeciesId = 10,
            ExplosionSpeciesId = 2,
            ElementalAttrs = ELE_FIRE,
            Speed = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="FlameEmit1",
            ExplosionSfxName="Explosion4",
            RemainsUponHit = true,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX, 
        };

        private static BulletConfig IcePillarStarterBullet = new BulletConfig {
            StartupFrames = 35,
            StartupInvinsibleFrames = 20,
            ActiveFrames = 600,
            HitStunFrames = 60,
            BlockStunFrames = 60,
            Damage = 5,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(52 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 3,
            ExplosionSpeciesId = 13,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_ICE,
            Speed = (int)(3.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 20,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            BType = BulletType.GroundWave,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActual,
            MhVanishOnMeleeHit = false, // Makes it more powerful on ground than the SlashNova
            RemainsUponHit = true,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX, 
            BuffConfig = LongFreezer,
        };

        private static BulletConfig IcePillarRepeatingBullet = new BulletConfig {
            StartupFrames = 0,
            ActiveFrames = 30,
            HitStunFrames = 30,
            BlockStunFrames = 30,
            Damage = 30,
            // No pushbacks
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(52 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 13,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_ICE,
            Speed = 0,
            DirX = 1,
            DirY = 0,
            Hardness = 20,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX
        };
            
        private static Skill IcePillarSkill = new Skill {
            RecoveryFrames = 80,
            RecoveryFramesOnBlock = 80,
            RecoveryFramesOnHit = 80,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5,
            SelfNonStockBuff = new BuffConfig {
                CharacterHardnessDelta = 5,
            }
        }
        .AddHit(IcePillarStarterBullet)
        .AddHit(IcePillarRepeatingBullet);

        private static BulletConfig PurpleArrowBullet = new BulletConfig {
            StartupFrames = 30,
            ActiveFrames = 180,
            HitStunFrames = 10,
            BlockStunFrames = 10,
            Damage = 12,
            PushbackVelX = (int)(0.05f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(15 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 11,
            Speed = (int)(5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            ExplosionSpeciesId = 11,
            BType = BulletType.Fireball,
            Ifc = IfaceCat.Wood,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill PurpleArrowPrimarySkill = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 220,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1,
        }
        .AddHit(PurpleArrowBullet); 

        private static BulletConfig PurpleArrowBulletAir = new BulletConfig(PurpleArrowBullet).SetSelfLockVel(0, (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0);
        public static Skill PurpleArrowPrimarySkillAir = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 220,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1,
        }
        .AddHit(PurpleArrowBulletAir); 

        private static BulletConfig PurpleArrowRainBullet1 = new BulletConfig(PurpleArrowBullet)
                                                                .SetSpeed((int)(6.0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetDamage(8)
                                                                .SetHitboxOffsets((int)(22 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSimultaneousMultiHitCnt(2u)
                                                                .SetDir(+2, +1)
                                                                .SetTakesGravity(true)
                                                                .SetRotateAlongVelocity(true);
        private static BulletConfig PurpleArrowRainBullet2 = new BulletConfig(PurpleArrowRainBullet1)
                                                                .SetDamage(7)
                                                                .SetSpeed((int)(9.0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSimultaneousMultiHitCnt(1u)
                                                                .SetDir(+1, +1)
                                                                .SetHitboxOffsets((int)(20 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));
        private static BulletConfig PurpleArrowRainBullet3 = new BulletConfig(PurpleArrowRainBullet1)
                                                                .SetDamage(6)
                                                                .SetSpeed((int)(10.5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSimultaneousMultiHitCnt(0u)
                                                                .SetDir(+1, +2)
                                                                .SetHitboxOffsets((int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));


        public static Skill PurpleArrowRainSkill = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 440,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2,
        }
        .AddHit(PurpleArrowRainBullet1)
            .AddHit(PurpleArrowRainBullet2)
            .AddHit(PurpleArrowRainBullet3);

        private static BulletConfig FallingPurpleArrowRainBullet1 = new BulletConfig(PurpleArrowRainBullet1)
                                                                        .SetDir(+2, 0)
                                                                        .SetSpeed((int)(8.0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                        .SetStartupFrames(30);

        private static BulletConfig FallingPurpleArrowRainBullet2 = new BulletConfig(PurpleArrowRainBullet2)
                                                                        .SetSpeed((int)(7.3 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                        .SetDir(+2, -1)
                                                                        .SetStartupFrames(30);
        public static Skill FallingPurpleArrowSkill = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 400,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1, 
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
            }
        }
        .AddHit(FallingPurpleArrowRainBullet1)
        .AddHit(FallingPurpleArrowRainBullet2);

        private static BulletConfig FallingPurpleArrowRainBullet1Air = new BulletConfig(FallingPurpleArrowRainBullet1)
                                                                        .SetSelfLockVel(0, (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0);

        private static BulletConfig FallingPurpleArrowRainBullet2Air = new BulletConfig(FallingPurpleArrowRainBullet2)
                                                                        .SetSelfLockVel(0, (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0);
        public static Skill FallingPurpleArrowSkillAir = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 400,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1, 
        }
        .AddHit(FallingPurpleArrowRainBullet1Air)
        .AddHit(FallingPurpleArrowRainBullet2Air);

        private static BulletConfig RisingPurpleArrowRainBullet1 = new BulletConfig(PurpleArrowRainBullet1)
                                                                .SetSpeed((int)(8.0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetDir(+3, +2)
                                                                .SetStartupFrames(40);

        private static BulletConfig RisingPurpleArrowRainBullet2 = new BulletConfig(PurpleArrowRainBullet2)
                                                                .SetSpeed((int)(9.0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetDir(+2, 0)
                                                                .SetStartupFrames(40);

        public static Skill RisingPurpleArrowSkill = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 400,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2,
        }
        .AddHit(RisingPurpleArrowRainBullet1)
        .AddHit(RisingPurpleArrowRainBullet2);

        private static BulletConfig RisingPurpleArrowRainBullet1Air = new BulletConfig(RisingPurpleArrowRainBullet1)
                                                                        .SetSelfLockVel(0, (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0);

        private static BulletConfig RisingPurpleArrowRainBullet2Air = new BulletConfig(RisingPurpleArrowRainBullet2)
                                                                        .SetSelfLockVel(0, (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 0);
        public static Skill RisingPurpleArrowSkillAir = new Skill{
            RecoveryFrames = 100,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 400,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1, 
        }
        .AddHit(RisingPurpleArrowRainBullet1Air)
        .AddHit(RisingPurpleArrowRainBullet2Air);


        public static BulletConfig BladeGirlDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 3,
            ActiveFrames = 20,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = (int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(30 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            RemainsUponHit = true,
            ReflectFireballXIfNotHarder = true,
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill BladeGirlDragonPunch = new Skill {
            Id = BladeGirlDragonPunchId,
            RecoveryFrames = 26,
            RecoveryFramesOnBlock = 26,
            RecoveryFramesOnHit = 26,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5
        }.
            AddHit(BladeGirlDragonPunchPrimerBullet);

        public static BulletConfig HunterDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 15,
            HitStunFrames = 20,
            CancellableStFrame = 9, 
            CancellableEdFrame = 35, 
            CancellableByInventorySlotC = true,
            BlockStunFrames = 9,
            Damage = 7,
            PushbackVelX = (int)(-0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(28 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            OmitSoftPushback = true,
            Hardness = 6,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_UP_B, 86);

        public static Skill HunterDragonPunch = new Skill {
            Id = 73,
            RecoveryFrames = 30,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5
        }.
            AddHit(HunterDragonPunchPrimerBullet);

        public static BulletConfig HunterDragonPunchSecondaryBullet = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 14,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 45,
            BlockStunFrames = 9,
            Damage = 10,
            PushbackVelX = (int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(2.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(6.7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(26 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            RemainsUponHit = true,
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill HunterDragonPunchSecondarySkill = new Skill {
            Id = 86,
            RecoveryFrames = 38,
            RecoveryFramesOnBlock = 38,
            RecoveryFramesOnHit = 38,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk8
        }.
            AddHit(HunterDragonPunchSecondaryBullet);
        
        public static Skill MagicPistolGround = new Skill {
            Id = 29,
               RecoveryFrames = 14,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Atk1
        }
            .AddHit(MagicPistolBulletGround);
        
        public static Skill MagicPistolAir = new Skill {
            Id = 30,
                RecoveryFrames = 14,
                RecoveryFramesOnBlock = 10,
                RecoveryFramesOnHit = 10,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = InAirAtk1
        }
            .AddHit(MagicPistolBulletAir);

        public static Skill MagicPistolCrouch = new Skill{
            Id = 34, 
            RecoveryFrames = 14,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = CrouchAtk1
        }
            .AddHit(MagicPistolBulletCrouch);

        public static Skill MagicCannonGround = new Skill{
            Id = 87, 
            RecoveryFrames = 14,
            RecoveryFramesOnBlock = 10,
            RecoveryFramesOnHit = 10,
            MpDelta = 300,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(MagicCannonBulletGround);

        public static Skill MagicCannonAir = new Skill{
            Id = 88,
               RecoveryFrames = 14,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               MpDelta = 300,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = InAirAtk1
        }
        .AddHit(MagicCannonBulletAir);
    
        public static Skill MagicCannonCrouch = new Skill{
            Id = 89,
               RecoveryFrames = 14,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               MpDelta = 300,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = CrouchAtk1
        }
        .AddHit(MagicCannonBulletAir);

        public static Skill MagSwordGirlSliding = new Skill {
            Id = 33,
            RecoveryFrames = 22,
            RecoveryFramesOnBlock = 22,
            RecoveryFramesOnHit = 22,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Sliding
        }
            .AddHit(
                new BulletConfig(BasicSlidingHit1)
                .UpsertCancelTransit(PATTERN_B, MagicPistolGround.Id)
                .UpsertCancelTransit(PATTERN_UP_B, MagicPistolGround.Id)
                .UpsertCancelTransit(PATTERN_DOWN_B, MagicPistolCrouch.Id)
                .UpsertCancelTransit(PATTERN_RELEASED_B, MagicCannonGround.Id)
            )
            .AddHit(
                new BulletConfig(BasicSlidingHit2)
                .UpsertCancelTransit(PATTERN_B, MagicPistolGround.Id)
                .UpsertCancelTransit(PATTERN_UP_B, MagicPistolGround.Id)
                .UpsertCancelTransit(PATTERN_DOWN_B, MagicPistolCrouch.Id)
                .UpsertCancelTransit(PATTERN_RELEASED_B, MagicCannonGround.Id)
            );

        public static uint MagSwordGirlAirSlashId = 28;
        public static Skill MagSwordGirlAirSlash = new Skill {
            Id = MagSwordGirlAirSlashId,
            RecoveryFrames = 28,
            RecoveryFramesOnBlock = 28,
            RecoveryFramesOnHit = 28,
            MpDelta = 200,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing
        }
            .AddHit(
                new BulletConfig {
                    StartupFrames = 5,
                    StartupInvinsibleFrames = 9,
                    ActiveFrames = 23,
                    HitStunFrames = 12,
                    BlockStunFrames = 9,
                    Damage = 13,
                    PushbackVelX = NO_LOCK_VEL,
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = (int)(7.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelY = (int)(-0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelYWhenFlying = NO_LOCK_VEL,
                    CancellableStFrame = 7,
                    CancellableEdFrame = 28,
                    CancellableByInventorySlotC = true,
                    HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SpeciesId = 2,
                    ExplosionFrames = 25,
                    DirX = 1,
                    DirY = 0,
                    Hardness = 5,
                    BType = BulletType.Melee,
                    FireballEmitSfxName = "SlashEmitSpd2",
                    ExplosionSfxName = "Melee_Explosion2",
                    CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX,
                }
                .UpsertCancelTransit(PATTERN_B, MagicPistolAir.Id)
                .UpsertCancelTransit(PATTERN_UP_B, MagicPistolAir.Id)
                .UpsertCancelTransit(PATTERN_DOWN_B, MagicPistolAir.Id)
                .UpsertCancelTransit(PATTERN_RELEASED_B, MagicCannonAir.Id)
                .UpsertCancelTransit(PATTERN_E, MagSwordGirlAirSlashId).UpsertCancelTransit(PATTERN_E_HOLD_B, MagSwordGirlAirSlashId)
            );

        public static BulletConfig HunterAirSlashBullet = new BulletConfig {
            StartupFrames = 5,
            ActiveFrames = 15,
            HitStunFrames = 18,
            BlockStunFrames = 9,
            Damage = 14,
            PushbackVelX = (int)(1.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ReflectFireballXIfNotHarder = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };
        
        public static Skill HunterAirSlash = new Skill {
            Id = 74,
            RecoveryFrames = 20,
            RecoveryFramesOnBlock = 20,
            RecoveryFramesOnHit = 20,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk2
        }
        .AddHit(HunterAirSlashBullet);

        public static BulletConfig WitchGirlFireballBulletHit1 = new BulletConfig {
            StartupFrames = 15,
            ActiveFrames = 600,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 28,
            PushbackVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(28f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(28*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            ElementalAttrs = ELE_FIRE,
            SpeciesId = 2,
            Speed = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            AnimLoopingRdfOffset = 35,
            CancellableStFrame = 18,
            CancellableEdFrame = 52,
            CancellableByInventorySlotC = true,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="FlameEmit1",
            ExplosionSfxName="Explosion4",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_UP_B, 55);

        public static BulletConfig WitchGirlFireballBulletAirHit1 = new BulletConfig(WitchGirlFireballBulletHit1).SetDamage(32).SetSelfLockVel(NO_LOCK_VEL, (int)(5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL);

        public static BulletConfig GoblinMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 50,
            ActiveFrames = 15,
            HitStunFrames = 6,
            HitInvinsibleFrames = 0,
            BlockStunFrames = 2,
            Damage = 12,
            PushbackVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill GoblinMelee1PrimerSkill = new Skill{
            RecoveryFrames = 70,
            RecoveryFramesOnBlock = 70,
            RecoveryFramesOnHit = 70,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(GoblinMelee1PrimerBullet);

        public static BulletConfig BoarWarriorMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 43,
            ActiveFrames = 2,
            HitStunFrames = 17,
            HitInvinsibleFrames = 13,
            BlockStunFrames = 2,
            Damage = 25,
            PushbackVelX = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 3,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill BoarWarriorMelee1PrimerSkill = new Skill {
            RecoveryFrames = 90,
            RecoveryFramesOnBlock = 90,
            RecoveryFramesOnHit = 90,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(BoarWarriorMelee1PrimerBullet);


        public static BulletConfig HeatBeamBulletHit1 = new BulletConfig {
            StartupFrames = 30,
            StartupInvinsibleFrames = 12,
            ActiveFrames = 180,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 6,
            PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(18f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(20*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BeamVisualSizeY = (int)(22*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] DON'T hardcode in Unity "AnimationCilp.GetComponent<SpriteRenderer>().size" or it'd cause weird rendering!
            SpeciesId = 24,
            ExplosionSpeciesId = 15,
            Speed = (int)(0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeedIfNotHit = (int)(7.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 10,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="FlameEmit1",
            ExplosionSfxName="Explosion4",
            StartupVfxSpeciesId = VfxHeatBeam.SpeciesId,
            ActiveVfxSpeciesId = VfxHeatBeam.SpeciesId,
            IsPixelatedActiveVfx = true,
            BeamCollision = true,
            MhType = MultiHitType.FromPrevHitActual,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig HeatBeamBulletRepeatingHit = new BulletConfig(HeatBeamBulletHit1)
                                                              .SetHitboxOffsets(0, 0)
                                                              .SetStartupFrames(8)
                                                              .SetStartupInvinsibleFrames(0);

        public static BulletConfig HeatBeamBulletEnderHit = new BulletConfig(HeatBeamBulletRepeatingHit)
                                                              .SetMhType(MultiHitType.None);

        public static Skill HeatBeamSkill = new Skill{
            Id = 35, 
            RecoveryFrames = 160,
            RecoveryFramesOnBlock = 160,
            RecoveryFramesOnHit = 160,
            TriggerType = SkillTriggerType.RisingEdge,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
                RepelSoftPushback = true,
                CharacterHardnessDelta = 3,
            },
            BoundChState = Atk6
        }
        .AddHit(HeatBeamBulletHit1)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletEnderHit)
        ;

        public static Skill HeatBeamAirSkill = new Skill{
            Id = 51, 
            RecoveryFrames = 160,
            RecoveryFramesOnBlock = 160,
            RecoveryFramesOnHit = 160,
            TriggerType = SkillTriggerType.RisingEdge,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
                RepelSoftPushback = true,
                CharacterHardnessDelta = 3,
            },
            BoundChState = InAirAtk6
        }
        .AddHit(HeatBeamBulletHit1)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletRepeatingHit)
        .AddHit(HeatBeamBulletEnderHit)
        ;

        public static BulletConfig RidleyMeleeBulletAirHit1 = new BulletConfig {
            StartupFrames = 6,
            ActiveFrames = 14,
            HitStunFrames = 10,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = (int)(1.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(22*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName="SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill RidleyMeleeAirSkill1 = new Skill {
            RecoveryFrames = 35,
            RecoveryFramesOnBlock = 35,
            RecoveryFramesOnHit = 35,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk2
        }
        .AddHit(RidleyMeleeBulletAirHit1);

        public static BulletConfig RidleyMeleeBulletHit1 = new BulletConfig {
            StartupFrames = 7,
            ActiveFrames = 24,
            HitStunFrames = 8,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = (int)(-0.1f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CancellableStFrame = 16,
            CancellableEdFrame = 32,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            RemainsUponHit = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, 39);

        public static Skill RidleyMeleeSkill1 = new Skill{
            RecoveryFrames = 35,
            RecoveryFramesOnBlock = 35,
            RecoveryFramesOnHit = 35,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(RidleyMeleeBulletHit1);

        public static BulletConfig BoarMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 40,
            ActiveFrames = 13,
            HitStunFrames = 5,
            HitInvinsibleFrames = 12,
            BlockStunFrames = 2,
            Damage = 20,
            PushbackVelX = (int)(5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = (int)(.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(20 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill BoarMelee1PrimerSkill = new Skill {
            Id = 45,
            RecoveryFrames = 70,
            RecoveryFramesOnBlock = 70,
            RecoveryFramesOnHit = 70,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(BoarMelee1PrimerBullet);
        
        public static BulletConfig BoarImpactHit1 = new BulletConfig {
            StartupFrames = 50,
            StartupInvinsibleFrames = 12,
            ActiveFrames = 50,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = (int)(6.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(5.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(20*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(28*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DelaySelfVelToActive = true,
            BlowUp = true,
            SpeciesId = 3,
            ExplosionFrames = 25,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            OmitSoftPushback = true,
            BType = BulletType.Melee,
            FireballEmitSfxName="SlashEmitSpd2",
            ExplosionSfxName="Melee_Explosion1",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX,
        };

        public static Skill BoarImpact = new Skill{
            Id = 46,
               RecoveryFrames = 100,
               RecoveryFramesOnBlock = 100,
               RecoveryFramesOnHit = 100,
               MpDelta = 7*BATTLE_DYNAMICS_FPS,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Atk2
        }
        .AddHit(BoarImpactHit1);

        public static BulletConfig WaterballBulletAirHit1 = new BulletConfig {
            StartupFrames = 10,
            ActiveFrames = 480,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(12f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 5,
            ElementalAttrs = ELE_WATER,
            Speed = (int)(5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            TakesGravity = true,
            RotatesAlongVelocity = true,
            DefaultHardPushbackBounceQuota = 3,
            HardPushbackBounceNormFactor = 1.0f,
            HardPushbackBounceSheerFactor = 1.0f,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion1",
            ActiveVfxSpeciesId = VfxWaterBallCast1.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };

        public static Skill WaterballAirSkill = new Skill{
            RecoveryFrames = 32,
            RecoveryFramesOnBlock = 32,
            RecoveryFramesOnHit = 32,
            MpDelta = 300,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk2
        }
        .AddHit(WaterballBulletAirHit1);

        public static BulletConfig StandardWaterballBulletGroundHit1 = new BulletConfig {
            StartupFrames = 13,
            ActiveFrames = 480,
            HitStunFrames = 16,
            BlockStunFrames = 9,
            Damage = 12,
            PushbackVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(12f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 5,
            Speed = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_WATER,
            BType = BulletType.Fireball,
            TakesGravity = true,
            RotatesAlongVelocity = true,
            DefaultHardPushbackBounceQuota = 3,
            HardPushbackBounceNormFactor = 1.0f,
            HardPushbackBounceSheerFactor = 1.0f,
            CharacterEmitSfxName ="SlashEmitSpd1",
            ExplosionSfxName="Explosion1",
            ActiveVfxSpeciesId = VfxWaterBallCast1.SpeciesId,
            IsPixelatedActiveVfx = true,
            CancellableStFrame = 16,
            CancellableEdFrame = 40, 
            CancellableByInventorySlotC = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };

        public static Skill WaterballGroundSkill1 = new Skill{
            RecoveryFrames = WaterballAirSkill.RecoveryFrames + 10,
            RecoveryFramesOnBlock = WaterballAirSkill.RecoveryFramesOnBlock + 10,
            RecoveryFramesOnHit = WaterballAirSkill.RecoveryFramesOnHit + 10,
            MpDelta = WaterballAirSkill.MpDelta,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(new BulletConfig(StandardWaterballBulletGroundHit1).UpsertCancelTransit(PATTERN_B, 48).UpsertCancelTransit(PATTERN_DOWN_B, 48).UpsertCancelTransit(PATTERN_UP_B, 48));

        public static BulletConfig StandardWaterballBulletGroundHit2 = new BulletConfig(StandardWaterballBulletGroundHit1).SetStartupFrames(11).SetHitboxOffsets((int)(12f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));

        public static Skill WaterballGroundSkill2 = new Skill{
            RecoveryFrames = WaterballAirSkill.RecoveryFrames,
            RecoveryFramesOnBlock = WaterballAirSkill.RecoveryFramesOnBlock,
            RecoveryFramesOnHit = WaterballAirSkill.RecoveryFramesOnHit,
            MpDelta = WaterballGroundSkill1.MpDelta,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3
        }
        .AddHit(new BulletConfig(StandardWaterballBulletGroundHit2).UpsertCancelTransit(PATTERN_B, 47).UpsertCancelTransit(PATTERN_DOWN_B, 47).UpsertCancelTransit(PATTERN_UP_B, 47));

        public static BulletConfig TriWaterballBulletGroundHit1 = new BulletConfig(StandardWaterballBulletGroundHit1)
            .SetSimultaneousMultiHitCnt(3);

        public static BulletConfig TriWaterballBulletGroundHit2 = new BulletConfig(StandardWaterballBulletGroundHit1)
            .SetDir(+2, +1)
            .SetHitboxOffsets((int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetSimultaneousMultiHitCnt(2);

        public static BulletConfig TriWaterballBulletGroundHit3 = new BulletConfig(StandardWaterballBulletGroundHit1)
            .SetDir(+2, -1)
            .SetHitboxOffsets((int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));

        public static Skill RisingWaterballPair = new Skill {
            RecoveryFrames = WaterballGroundSkill1.RecoveryFrames,
            RecoveryFramesOnBlock = WaterballGroundSkill1.RecoveryFramesOnBlock,
            RecoveryFramesOnHit = WaterballGroundSkill1.RecoveryFramesOnHit,
            MpDelta = 500,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(TriWaterballBulletGroundHit1)
        .AddHit(TriWaterballBulletGroundHit2);

        public static Skill FallingWaterballPair = new Skill {
            RecoveryFrames = WaterballGroundSkill1.RecoveryFrames,
            RecoveryFramesOnBlock = WaterballGroundSkill1.RecoveryFramesOnBlock,
            RecoveryFramesOnHit = WaterballGroundSkill1.RecoveryFramesOnHit,
            MpDelta = RisingWaterballPair.MpDelta,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3
        }
        .AddHit(TriWaterballBulletGroundHit1)
        .AddHit(TriWaterballBulletGroundHit3);

        public static BulletConfig SpinLightBeamBulletHit1 = new BulletConfig {
            StartupFrames = 12,
            StartupInvinsibleFrames = 12,
            ActiveFrames = 320,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(13f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BeamVisualSizeY = (int)(26*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] DON'T hardcode in Unity "AnimationCilp.GetComponent<SpriteRenderer>().size" or it'd cause weird rendering!
            SpeciesId = 6,
            ExplosionSpeciesId = 5,
            Speed = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeedIfNotHit = (int)(4.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="WaterEmitSpd1",
            ExplosionSfxName = "Explosion1",
            StartupVfxSpeciesId = VfxLightBeam.SpeciesId,
            ActiveVfxSpeciesId = VfxLightBeam.SpeciesId,
            IsPixelatedActiveVfx = true,
            BeamCollision = true,
            SpinAnchorX = 0f,
            SpinAnchorY = 6.0f, // [WARNING] Half of "HitboxSizeY"; kindly note that "SpinAnchorX & SpinAnchorY" for non-melee bullets are constrained by the "pivot points" set on the sprites
            GaugeIncReductionRatio = 0.3f,
            AngularFrameVelCos = (float)Math.Cos(+0.9f / (Math.PI * BATTLE_DYNAMICS_FPS)), 
            AngularFrameVelSin = (float)Math.Sin(+0.9f / (Math.PI * BATTLE_DYNAMICS_FPS)), 
            InitSpinCos = (float)Math.Cos(0.15f * Math.PI), 
            InitSpinSin = (float)Math.Sin(-0.15f * Math.PI), 
            MhType = MultiHitType.FromPrevHitActual,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SpinLightBeamBulletRepeatingHitPositive = new BulletConfig(SpinLightBeamBulletHit1)
                                                              .SetStartupFrames(8)
                                                              .SetDamage(9)
                                                              .SetHitboxOffsets(0, 0)
                                                              .SetStartupInvinsibleFrames(0)
                                                              //.SetAngularVel((float)Math.Cos(2f / (Math.PI * BATTLE_DYNAMICS_FPS)), (float)Math.Sin(+2f / (Math.PI * BATTLE_DYNAMICS_FPS)))
                                                              .SetMhInheritsSpin(true);

        public static BulletConfig SpinLightBeamBulletRepeatingHitNegative = new BulletConfig(SpinLightBeamBulletHit1)
                                                              .SetStartupFrames(8)
                                                              .SetDamage(9)
                                                              .SetHitboxOffsets(0, 0)
                                                              .SetStartupInvinsibleFrames(0)
                                                              //.SetAngularVel((float)Math.Cos(2f / (Math.PI * BATTLE_DYNAMICS_FPS)), (float)Math.Sin(-2f / (Math.PI * BATTLE_DYNAMICS_FPS)))
                                                              .SetMhInheritsSpin(true);

        public static Skill SpinLightBeamSkill = new Skill {
            RecoveryFrames = 120,
            RecoveryFramesOnBlock = 120,
            RecoveryFramesOnHit = 120,
            TriggerType = SkillTriggerType.RisingEdge,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
                RepelSoftPushback = true,
                CharacterHardnessDelta = 3,
            },
            BoundChState = Atk1
        }
        .AddHit(SpinLightBeamBulletHit1)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        ;

        public static Skill SpinLightBeamAirSkill = new Skill {
            RecoveryFrames = 120,
            RecoveryFramesOnBlock = 120,
            RecoveryFramesOnHit = 120,
            TriggerType = SkillTriggerType.RisingEdge,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
                RepelSoftPushback = true,
                CharacterHardnessDelta = 3,
            },
            BoundChState = InAirAtk1
        }
        .AddHit(SpinLightBeamBulletHit1)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitNegative)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        .AddHit(SpinLightBeamBulletRepeatingHitPositive)
        ;

        public static BulletConfig WitchGirlMeleeBulletAirHit1 = new BulletConfig {
            StartupFrames = 6,
            ActiveFrames = 14,
            HitStunFrames = 10,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = (int)(1.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            ElementalAttrs = ELE_FIRE,
            ReflectFireballXIfNotHarder = true,
            CharacterEmitSfxName ="SlashEmitSpd1",
            ExplosionSfxName="FlameBurning1",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill WitchGirlMeleeAir1 = new Skill {
            RecoveryFrames = 21,
            RecoveryFramesOnBlock = 21,
            RecoveryFramesOnHit = 21,
            MpDelta = 300,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk2
        }
        .AddHit(WitchGirlMeleeBulletAirHit1);

        public static BulletConfig bouncingTouchExplosionBombStarter = new BulletConfig {
            StartupFrames = 12,
            StartupInvinsibleFrames = 8,
            ActiveFrames = 800,
            HitStunFrames = 3,
            PushbackVelX = 0,
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(+6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 7,
            TakesGravity = true, 
            ExplosionSpeciesId = 1,
            ExplosionFrames = 25,
            RotatesAlongVelocity = true,
            DefaultHardPushbackBounceQuota = 10,
            HardPushbackBounceNormFactor = 0.98f,
            HardPushbackBounceSheerFactor = 0.95f,
            Speed = (int)(6.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = +1,
            DirY = +1,
            Hardness = 3,
            TouchExplosionBombCollision = true, 
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActual,
            RejectsReflectionFromAnotherBullet = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX, 
        };

        public static BulletConfig touchExplosionBombShock = new BulletConfig {
            StartupFrames = 6,
            ActiveFrames = 10,
            HitStunFrames = MAX_INT,
            BlockStunFrames = 60,
            Damage = 45,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(33 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 17,
            ExplosionSpeciesId = 15,
            ExplosionFrames = 31,
            InplaceVanishExplosionSpeciesId = 27,
            RemainsUponHit = true,
            DirX = 1,
            DirY = 0,
            Hardness = 99,
            ElementalAttrs = ELE_FIRE,
            BType = BulletType.Fireball,
            ExplosionSfxName = "Explosion4",
            RejectsReflectionFromAnotherBullet = true,
            MhType = MultiHitType.FromEmission,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX, 
        };

        public static BulletConfig touchExplosionBombAmber = new BulletConfig {
            StartupFrames = 0,
            ActiveFrames = 34,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = 0,
            HitboxSizeY = 0,
            SpeciesId = 17,
            InplaceVanishExplosionSpeciesId = 27,
            DirX = 1,
            DirY = 0,
            BType = BulletType.Fireball,
            MhInheritsFramesInBlState = true,
        };

        public static Skill bouncingTouchExplosionBomb = new Skill {
            RecoveryFrames = 21,
            RecoveryFramesOnBlock = 21,
            RecoveryFramesOnHit = 21,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk6
        }
        .AddHit(bouncingTouchExplosionBombStarter)
        .AddHit(touchExplosionBombShock)
        .AddHit(touchExplosionBombAmber);

        public static BulletConfig timedBouncingBombStarter = new BulletConfig {
            StartupFrames = 12,
            StartupInvinsibleFrames = 8,
            ActiveFrames = 3*BATTLE_DYNAMICS_FPS, // matching that of anim clip
            HitStunFrames = 3,
            PushbackVelX = 0,
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(+6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 29,
            TakesGravity = true, 
            ExplosionSpeciesId = 1,
            ExplosionFrames = 25,
            RotatesAlongVelocity = true,
            DefaultHardPushbackBounceQuota = MAX_INT,
            HardPushbackBounceNormFactor = 0.75f,
            HardPushbackBounceSheerFactor = 0.70f,
            Speed = (int)(5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = +1,
            DirY = +1,
            Hardness = 3,
            TouchExplosionBombCollision = true, 
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActualOrActiveTimeUp,
            RejectsReflectionFromAnotherBullet = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX, 
        };

        public static BulletConfig goblinBombStarter = new BulletConfig {
            StartupFrames = 54,
            StartupInvinsibleFrames = 30,
            ActiveFrames = 3*BATTLE_DYNAMICS_FPS, // matching that of anim clip
            HitStunFrames = 3,
            PushbackVelX = 0,
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(+6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 29,
            TakesGravity = true, 
            ExplosionSpeciesId = 1,
            ExplosionFrames = 25,
            RotatesAlongVelocity = true,
            DefaultHardPushbackBounceQuota = MAX_INT,
            HardPushbackBounceNormFactor = 0.75f,
            HardPushbackBounceSheerFactor = 0.70f,
            Speed = (int)(5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = +1,
            DirY = +1,
            Hardness = 3,
            TouchExplosionBombCollision = true, 
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActualOrActiveTimeUp,
            RejectsReflectionFromAnotherBullet = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX, 
        };

        public static BulletConfig goblinBombShock = new BulletConfig {
            StartupFrames = 6,
            ActiveFrames = 10,
            HitStunFrames = MAX_INT,
            BlockStunFrames = 60,
            Damage = 32,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(33 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 34,
            ExplosionSpeciesId = 15,
            ExplosionFrames = 35,
            InplaceVanishExplosionSpeciesId = 7,
            RemainsUponHit = true,
            DirX = 1,
            DirY = 0,
            Hardness = 32,
            ElementalAttrs = ELE_FIRE,
            BType = BulletType.Fireball,
            ExplosionSfxName = "Explosion4",
            MhType = MultiHitType.FromEmission,
            RejectsReflectionFromAnotherBullet = true,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX, 
        };

        public static BulletConfig goblinBombAmber = new BulletConfig {
            StartupFrames = 0,
            ActiveFrames = 34,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = 0,
            HitboxSizeY = 0,
            SpeciesId = 17,
            InplaceVanishExplosionSpeciesId = 27,
            DirX = 1,
            DirY = 0,
            BType = BulletType.Fireball,
            MhInheritsFramesInBlState = true,
        };

        public static Skill timedBouncingBomb = new Skill {
            RecoveryFrames = 24,
            RecoveryFramesOnBlock = 24,
            RecoveryFramesOnHit = 24,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk6
        }
        .AddHit(timedBouncingBombStarter)
        .AddHit(touchExplosionBombShock)
        .AddHit(touchExplosionBombAmber);

        public static Skill goblinBomb = new Skill {
            Id = 117,
            RecoveryFrames = 72,
            RecoveryFramesOnBlock = 72,
            RecoveryFramesOnHit = 72,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3
        }
        .AddHit(goblinBombStarter)
        .AddHit(goblinBombShock)
        .AddHit(goblinBombAmber);

        public static BulletConfig JumperImpact1 = new BulletConfig {
            StartupFrames = 6, // Should match that of "Tdestroyed" anim clip
            ActiveFrames = 20, // Should match the rest of "Tdestroyed" anim clip
            Damage = 0,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = (int)(12f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            DirX = 0,
            DirY = +1,
            Hardness = 7,
            BType = BulletType.Melee,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static Skill JumperImpact1Skill = new Skill {
            Id = 104
        }.AddHit(JumperImpact1);

        public static BulletConfig GhostHorseStarter = new BulletConfig {
            StartupFrames = 8,
            StartupInvinsibleFrames = 8,
            ActiveFrames = 800,
            HitStunFrames = 15,
            BlockStunFrames = 60, // Yes, blocking yields more frames to recover for this bullet
            Damage = 20,
            Speed = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            IgnoreSlopeDeceleration = true,
            PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-1 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(42 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 16,
            TakesGravity = true,
            AirRidingGroundWave = true,
            ExplosionSpeciesId = 3,
            ExplosionFrames = 25,
            DirX = +2,
            DirY = 0,
            Hardness = 50,
            BType = BulletType.GroundWave,
            ElementalAttrs = ELE_ROCK,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActual,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };
        public static BulletConfig GhostHorsePusher = new BulletConfig {
            StartupFrames = 1,
            StartupInvinsibleFrames = 1,
            HitStunFrames = MAX_INT, // [WARNING] With 0 hardness, this "HitStunFrame" is only used for creating long immune period
            ActiveFrames = 800,
            Speed = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            IgnoreSlopeDeceleration = true,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxSizeX = (int)(42 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 16,
            ExplosionSpeciesId = 16,
            ExplosionFrames = 25,
            TakesGravity = true,
            AirRidingGroundWave = true,
            RemainsUponHit = true,
            ProvidesXHardPushback = true,
            ProvidesYHardPushbackTop = true,
            ProvidesYHardPushbackBottom = true,
            DirX = +2,
            DirY = 0,
            Hardness = 0,
            ElementalAttrs = ELE_ROCK,
            BType = BulletType.GroundWave,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };

        public static Skill GhostHorseSkill = new Skill {
            RecoveryFrames = 21,
            RecoveryFramesOnBlock = 21,
            RecoveryFramesOnHit = 21,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(GhostHorseStarter)
        .AddHit(GhostHorsePusher)
        ;

        public static BulletConfig MobileThunderBallPrimerBullet = new BulletConfig {
            StartupFrames = 4,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 450,
            HitStunFrames = 24,
            HitInvinsibleFrames = 6,
            BlockStunFrames = 2,
            Damage = 28,
            PushbackVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            Speed = (int)(2.2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            RotatesAlongVelocity = true,
            SpeciesId = 14,
            ExplosionFrames = 25,
            BType = BulletType.MissileLinear,
            MissileSearchIntervalPow2Minus1 = (1u << 3) - 1u,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            AllowsWalking = true,
            ElementalAttrs = ELE_THUNDER,
            AngularFrameVelCos = (float)Math.Cos(15.0/(Math.PI * BATTLE_DYNAMICS_FPS)), // human readable number is in degrees/second
            AngularFrameVelSin = (float)Math.Sin(15.0/(Math.PI * BATTLE_DYNAMICS_FPS)),
            VisionOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            ActiveVfxSpeciesId = VfxCastThunderTwins.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX,
            BuffConfig = ShortParalyzer,
        };

        public static BulletConfig MobileThunderBallPrimerBulletAir = new BulletConfig(MobileThunderBallPrimerBullet).SetAllowsWalking(false).SetSelfLockVel(0, 0, NO_LOCK_VEL);

        public static Skill MobileThunderBallPrimer = new Skill {
            Id = 66,
            RecoveryFrames = 32,
            RecoveryFramesOnBlock = 32,
            RecoveryFramesOnHit = 32,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1,
        }
        .AddHit(MobileThunderBallPrimerBullet);

        public static Skill MobileThunderBallPrimerAir = new Skill {
            Id = 67,
            RecoveryFrames = 32,
            RecoveryFramesOnBlock = 32,
            RecoveryFramesOnHit = 32,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk1, 
        }
        .AddHit(MobileThunderBallPrimerBulletAir);

        public static Skill MobileThunderBallPrimerCrouch = new Skill {
            Id = 69, 
            RecoveryFrames = 32,
            RecoveryFramesOnBlock = 32,
            RecoveryFramesOnHit = 32,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = CrouchAtk1,
        }
        .AddHit(MobileThunderBallPrimerBullet);

        public static Skill MobileThunderBallPrimerWall = new Skill {
            Id = 70,
            RecoveryFrames = 32,
            RecoveryFramesOnBlock = 32,
            RecoveryFramesOnHit = 32,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = OnWallAtk1 
        }
        .AddHit(MobileThunderBallPrimerBulletAir);

        public static int HunterDashSlashTotDurationRdfCnt = 45;
        public static BulletConfig HunterDashSlashBl1 = new BulletConfig {
            StartupFrames = 13,
            StartupInvinsibleFrames = 13,
            ActiveFrames = 18,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlowUp = true,
            BlockStunFrames = 60,
            Damage = 36,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(7.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(28 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 5,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_WIND,
            DirX = 1,
            DirY = 0,
            Hardness = 8,
            OmitSoftPushback = true,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName="Melee_Explosion2",
            DelaySelfVelToActive = true,
            RemainsUponHit = true,
            MhType = MultiHitType.FromEmission, 
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static BulletConfig HunterDashSlashStopper = new BulletConfig {
            StartupFrames = (HunterDashSlashBl1.StartupFrames + HunterDashSlashBl1.ActiveFrames),
            StartupInvinsibleFrames = HunterDashSlashTotDurationRdfCnt - (HunterDashSlashBl1.StartupFrames + HunterDashSlashBl1.ActiveFrames),
            ActiveFrames = HunterDashSlashTotDurationRdfCnt - (HunterDashSlashBl1.StartupFrames + HunterDashSlashBl1.ActiveFrames),
            SelfLockVelX = 0,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            SpeciesId = 5,
            DirX = 1,
            DirY = 0,
            OmitSoftPushback = true,
            BType = BulletType.Melee,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static Skill HunterDashSlashSKill = new Skill {
            Id = 76, 
            RecoveryFrames = HunterDashSlashTotDurationRdfCnt,
            RecoveryFramesOnBlock = HunterDashSlashTotDurationRdfCnt,
            RecoveryFramesOnHit = HunterDashSlashTotDurationRdfCnt,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk7, 
            SelfNonStockBuff = new BuffConfig {
                CharacterHardnessDelta = 3,
            },
        }
        .AddHit(HunterDashSlashBl1)
        .AddHit(HunterDashSlashStopper);

        public static BulletConfig DiverImpactStarterBullet = new BulletConfig {
            StartupFrames = 10,
            StartupInvinsibleFrames = 7,
            ActiveFrames = 7*BATTLE_DYNAMICS_FPS,
            HitStunFrames = 15,
            BlockStunFrames = 60,
            Damage = 10,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = (int)(-5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = 0,
            SelfLockVelY = (int)(-9.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 
            HitboxSizeX = (int)(42 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(22 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_ROCK,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName= "Explosion2",
            MhType = MultiHitType.FromPrevHitAnyway,
            MhNotTriggerOnChHit = true,
            MhNotTriggerOnHarderBulletHit = true,
            GroundImpactMeleeCollision = true,
            RemainsUponHit = true,
            OmitSoftPushback = true,
            FinishingFrames = 10,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static BulletConfig DiverImpactShockBullet = new BulletConfig {
            ActiveFrames = 640,
            HitStunFrames = 35,
            BlockStunFrames = 12,
            Damage = 7,
            PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(6.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 18,
            ExplosionSpeciesId = 9,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_ROCK,
            Speed = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            BType = BulletType.GroundWave,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX,
        };

        public static Skill BladeGirlDiverImpact = new Skill {
            Id = 77,
            RecoveryFrames = DiverImpactStarterBullet.StartupFrames + DiverImpactStarterBullet.ActiveFrames + DiverImpactStarterBullet.FinishingFrames,
            RecoveryFramesOnBlock = DiverImpactStarterBullet.StartupFrames + DiverImpactStarterBullet.ActiveFrames + DiverImpactStarterBullet.FinishingFrames,
            RecoveryFramesOnHit = DiverImpactStarterBullet.StartupFrames + DiverImpactStarterBullet.ActiveFrames + DiverImpactStarterBullet.FinishingFrames,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk7, 
            SelfNonStockBuff = new BuffConfig {
                CharacterHardnessDelta = 3,
            },
        }
        .AddHit(DiverImpactStarterBullet)
        .AddHit(DiverImpactShockBullet);

        public static BulletConfig StandardAirDashHit1 = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 8,
            ActiveFrames = 4,
            OmitSoftPushback = true,
            CancellableStFrame = 7,
            CancellableEdFrame = 10,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            MhType = MultiHitType.FromEmission,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        }; 

        public static BulletConfig StandardAirDashHit2 = new BulletConfig {
            StartupFrames = 10,
            ActiveFrames = 8,
            OmitSoftPushback = true,
            CancellableStFrame = 10,
            CancellableEdFrame = 19,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(4.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            BType = BulletType.Melee,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        }; 

        public static BulletConfig DemonDiverImpactPreJumpBullet = new BulletConfig {
            StartupFrames = 16,
            StartupInvinsibleFrames = 7,
            ActiveFrames = 20,
            HitStunFrames = 15,
            BlockStunFrames = 60,
            PushbackVelX = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 
            HitboxSizeX = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static BulletConfig DemonDiverImpactStarterBullet = new BulletConfig {
            StartupFrames = DemonDiverImpactPreJumpBullet.StartupFrames + DemonDiverImpactPreJumpBullet.ActiveFrames,
            StartupInvinsibleFrames = 7,
            ActiveFrames = 7*BATTLE_DYNAMICS_FPS,
            HitStunFrames = 15,
            BlockStunFrames = 60,
            Damage = 30,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = (int)(-5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = 0,
            SelfLockVelY = (int)(-9.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 
            HitboxSizeX = (int)(38 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 3,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_ROCK,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromPrevHitAnyway,
            MhNotTriggerOnChHit = true,
            MhNotTriggerOnHarderBulletHit = true,
            GroundImpactMeleeCollision = true,
            RemainsUponHit = true,
            OmitSoftPushback = true,
            FinishingFrames = 20,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static BulletConfig DemonDiverImpactShockBullet = new BulletConfig {
            ActiveFrames = 100,
            HitStunFrames = 35,
            BlockStunFrames = 12,
            Damage = 20,
            PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(6.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 18,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_FIRE,
            Speed = (int)(6.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            BType = BulletType.GroundWave,
            SimultaneousMultiHitCnt = 2u,
            CharacterEmitSfxName = "Explosion2",
            ExplosionSfxName= "Explosion2",
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX,
        };

        public static BulletConfig DemonDiverImpactShockBulletRevX = new BulletConfig(DemonDiverImpactShockBullet)
        .SetDir(-2, 0)
        .SetSimultaneousMultiHitCnt(1u);

        public static Skill DemonDiverImpact = new Skill {
            Id = 83,
            RecoveryFrames = (DemonDiverImpactStarterBullet.StartupFrames + DemonDiverImpactStarterBullet.ActiveFrames + DemonDiverImpactStarterBullet.FinishingFrames),
            RecoveryFramesOnBlock = (DemonDiverImpactStarterBullet.StartupFrames + DemonDiverImpactStarterBullet.ActiveFrames + DemonDiverImpactStarterBullet.FinishingFrames),
            RecoveryFramesOnHit = (DemonDiverImpactStarterBullet.StartupFrames + DemonDiverImpactStarterBullet.ActiveFrames + DemonDiverImpactStarterBullet.FinishingFrames),
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3, 
            SelfNonStockBuff = new BuffConfig {
                CharacterHardnessDelta = 3,
            },
        }
        .AddHit(DemonDiverImpactPreJumpBullet)
        .AddHit(DemonDiverImpactStarterBullet)
        .AddHit(DemonDiverImpactShockBullet)
        .AddHit(DemonDiverImpactShockBulletRevX);

        public static uint HunterPistolWallId = 68, HunterPistolId = 71, HunterPistolAirId = 72, HunterDashingId = 78, HunterSlidingId = 132, HunterPistolCrouchId = 133, HunterDragonPunchId = 73, MobileThunderBallPrimerId = 66;
        
        public static BulletConfig BasicBladeHit1 = new BulletConfig {
            StartupFrames = 3,
            StartupInvinsibleFrames = 1,
            ActiveFrames = 18,
            HitStunFrames = 20,
            BlockStunFrames = 8,
            Damage = 10,
            PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CancellableStFrame = 8,
            CancellableEdFrame = 19,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ReflectFireballXIfNotHarder = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, BladeGirlGroundSlash2Id).UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlGroundSlash2Id);

        public static BulletConfig BasicBladeHit2 = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 14,
            HitStunFrames = 25,
            BlockStunFrames = 9,
            Damage = 12,
            PushbackVelX = 0,
            PushbackVelY = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(42*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CancellableStFrame = 12,
            CancellableEdFrame = 20,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, BladeGirlGroundSlash3Id).UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlGroundSlash3Id).UpsertCancelTransit(PATTERN_UP_B, BladeGirlDragonPunchId);
    
        public static BulletConfig BasicBladeHit3 = new BulletConfig {
            StartupFrames = 6,
            StartupInvinsibleFrames = 4,
            ActiveFrames = 17,
            HitStunFrames = 34,
            BlockStunFrames = 5,
            Damage = 18,
            PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(-1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(40*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(6*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = false,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CancellableStFrame = 10,
            CancellableEdFrame = 25,
            CancellableByInventorySlotC = true,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ReflectFireballXIfNotHarder = true,
            RemainsUponHit = true,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig BasicAirBladeHit1 = new BulletConfig {
            StartupFrames = 5,
            ActiveFrames = 14,
            HitStunFrames = 15,
            BlockStunFrames = 9,
            Damage = 13,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            ReflectFireballXIfNotHarder = true,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            CancellableStFrame = 13,
            CancellableEdFrame = 21,
            CharacterEmitSfxName="SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig BasicAirBladeHit2 = new BulletConfig {
            StartupFrames = 9,
            StartupInvinsibleFrames = 4,
            ActiveFrames = 20,
            HitStunFrames = 15,
            BlockStunFrames = 5,
            FinishingFrames = 15,
            Damage = 10,
            PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            ReflectFireballXIfNotHarder = true,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill BladeGirlGroundSlash1 = new Skill {
            // [WARNING] The relationship between "RecoveryFrames", "StartupFrames", "HitStunFrames" and "PushbackVelX" makes sure that a MeleeBullet is counterable!
            Id = BladeGirlGroundSlash1Id,
            RecoveryFrames = 26,
            RecoveryFramesOnBlock = 26,
            RecoveryFramesOnHit = 26,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }.AddHit(BasicBladeHit1);

        public static Skill BladeGirlGroundSlash2 = new Skill {
            Id = BladeGirlGroundSlash2Id,
            RecoveryFrames = 24,
            RecoveryFramesOnBlock = 24,
            RecoveryFramesOnHit = 24,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(BasicBladeHit2);

        public static Skill BladeGirlGroundSlash3 = new Skill {
            Id = BladeGirlGroundSlash3Id,
            RecoveryFrames = 39,
            RecoveryFramesOnBlock = 39,
            RecoveryFramesOnHit = 39,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3
        }
        .AddHit(BasicBladeHit3);

        public static BulletConfig SuperBladeHit1 = new BulletConfig {
            StartupFrames = 5,
            StartupInvinsibleFrames = 25,
            ActiveFrames = 11,
            HitStunFrames = 30,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = false,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName="SlashEmitSpd3",
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            ActiveVfxSpeciesId = VfxSmallSting.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SuperBladeHit2 = new BulletConfig {
            StartupFrames = 16,
            ActiveFrames = 9,
            HitStunFrames = 30,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(2.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            ActiveVfxSpeciesId = VfxSmallSting.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SuperBladeHit3 = new BulletConfig {
            StartupFrames = 25,
            ActiveFrames = 9,
            HitStunFrames = 30,
            BlockStunFrames = 9,
            Damage = 10,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(2.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            ActiveVfxSpeciesId = VfxSmallSting.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SuperBladeHit4 = new BulletConfig {
            StartupFrames = 34,
            ActiveFrames = 9,
            HitStunFrames = 30,
            BlockStunFrames = 9,
            Damage = 6,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            ActiveVfxSpeciesId = VfxSmallSting.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SuperBladeHit5 = new BulletConfig {
            StartupFrames = 43,
            ActiveFrames = 9,
            HitStunFrames = MAX_INT,
            BlowUp = true,
            BlockStunFrames = 9,
            Damage = 6,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = (int)(6.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            RemainsUponHit = true,
            OmitSoftPushback = true,
            ExplosionSfxName="Melee_Explosion2",
            MhType = MultiHitType.FromEmission,
            ActiveVfxSpeciesId = VfxSmallSting.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill BladeGirlGroundSuperSlash = new Skill {
            Id = BladeGirlGroundSuperSlashId,
            RecoveryFrames = 64,
            RecoveryFramesOnBlock = 64,
            RecoveryFramesOnHit = 64,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk4
        }
        .AddHit(SuperBladeHit1)
        .AddHit(SuperBladeHit2)
        .AddHit(SuperBladeHit3)
        .AddHit(SuperBladeHit4)
        .AddHit(SuperBladeHit5);

        public static BulletConfig BladeGirlSlidingSlashBullet = new BulletConfig {
                StartupFrames = 5,
                StartupInvinsibleFrames = 4,
                ActiveFrames = 15,
                HitStunFrames = 20,
                BlockStunFrames = 9,
                Damage = 8,
                PushbackVelX = NO_LOCK_VEL,
                PushbackVelY = NO_LOCK_VEL,
                SelfLockVelX = (int)(3.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                SelfLockVelY = NO_LOCK_VEL,
                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                HitboxOffsetX = (int)(26*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxOffsetY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeY = (int)(20*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                BlowUp = false,
                SpeciesId = 2,
                ExplosionSpeciesId = 2,
                DirX = 1,
                DirY = 0,
                Hardness = 7,
                ExplosionFrames = 25,
                BType = BulletType.Melee,
                CharacterEmitSfxName="SlashEmitSpd3",
                ExplosionSfxName="Melee_Explosion2",
                MhType = MultiHitType.FromEmission,
                CancellableStFrame = 10,
                CancellableEdFrame = 22,
                CancellableByInventorySlotC = true,
                CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
            }.UpsertCancelTransit(PATTERN_B, BladeGirlGroundSlash1Id).UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlCrouchSlashId).UpsertCancelTransit(PATTERN_UP_B, BladeGirlDragonPunchId);

        public static Skill BladeGirlSlidingSlashSkill = new Skill {
            Id = BladeGirlSlidingSlashId,
            RecoveryFrames = 21,
            RecoveryFramesOnBlock = 21,
            RecoveryFramesOnHit = 21,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk8, 
        }
        .AddHit(BladeGirlSlidingSlashBullet);

        public static BulletConfig BladeGirlCrouchSlashHit1 = new BulletConfig {
                StartupFrames = 3,
                StartupInvinsibleFrames = 1,
                ActiveFrames = 15,
                HitStunFrames = 12,
                BlockStunFrames = 8,
                Damage = 8,
                PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                PushbackVelY = NO_LOCK_VEL,
                SelfLockVelX = NO_LOCK_VEL,
                SelfLockVelY = NO_LOCK_VEL,
                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                HitboxOffsetX = (int)(22*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxOffsetY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeY = (int)(20*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                CancellableStFrame = 8,
                CancellableEdFrame = 26,
                SpeciesId = 2,
                ExplosionSpeciesId = 2,
                ExplosionFrames = 25,
                BType = BulletType.Melee,
                DirX = 1,
                DirY = 0,
                Hardness = 5,
                CharacterEmitSfxName = "SlashEmitSpd1",
                ExplosionSfxName="Melee_Explosion2",
                CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, BladeGirlGroundSlash1Id).UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlCrouchSlashId).UpsertCancelTransit(PATTERN_UP_B, BladeGirlDragonPunchId);

        public static Skill BladeGirlCrouchSlash = new Skill {
                Id = BladeGirlCrouchSlashId,
                RecoveryFrames = 26,
                RecoveryFramesOnBlock = 26,
                RecoveryFramesOnHit = 26,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = CrouchAtk1
        }
        .AddHit(BladeGirlCrouchSlashHit1);

        public static BulletConfig DroppingFireballHit1 = new BulletConfig {
            StartupFrames = 4,
            ActiveFrames = 600,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 20,
            PushbackVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            ElementalAttrs = ELE_FIRE,
            SpeciesId = 4,
            Speed = (int)(2.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            TakesGravity = true,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="FlameEmit1",
            ExplosionSfxName="Explosion4",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX,
            RejectsReflectionFromAnotherBullet = true,
            InitSpinCos = (float)Math.Cos(0.5 * Math.PI), 
            InitSpinSin = (float)Math.Sin(-0.5f * Math.PI),
        };

        public static Skill DroppingFireballSkill = new Skill {
            Id = 85,
            RecoveryFrames = 15,
            RecoveryFramesOnBlock = 15,
            RecoveryFramesOnHit = 15,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1, 
            MpDelta = 1200,
        }.AddHit(DroppingFireballHit1);

        public static BulletConfig LightGuardMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 30,
            ActiveFrames = 20,
            HitStunFrames = 15,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = (int)(2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill LightGuardMelee1PrimerSkill = new Skill {
            RecoveryFrames = 55,
            RecoveryFramesOnBlock = 55,
            RecoveryFramesOnHit = 55,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(LightGuardMelee1PrimerBullet);

        public static BulletConfig LightGuardDashBullet = new BulletConfig {
            StartupFrames = 15,
            StartupInvinsibleFrames = 3,
            ActiveFrames = 15,
            SelfLockVelX = (int)(+2.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill LightGuardDashSkill = new Skill {
            RecoveryFrames = 38,
            RecoveryFramesOnBlock = 38,
            RecoveryFramesOnHit = 38,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing,
            SelfNonStockBuff = new BuffConfig {
                CharacterHardnessDelta = 3,
            },
        }
        .AddHit(LightGuardDashBullet);

        public static BulletConfig HeavyGuardMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 40,
            ActiveFrames = 23,
            HitStunFrames = 15,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 9,
            Damage = 20,
            PushbackVelX = (int)(2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig HeavyGuardMelee2PrimerBullet = new BulletConfig {
            StartupFrames = 35,
            ActiveFrames = 20,
            HitStunFrames = 15,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 9,
            Damage = 15,
            PushbackVelX = (int)(2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(30 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 8,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };
        
        public static Skill HeavyGuardMelee1PrimerSkill = new Skill {
            RecoveryFrames = 63,
            RecoveryFramesOnBlock = 63,
            RecoveryFramesOnHit = 63,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(HeavyGuardMelee1PrimerBullet);

        public static Skill HeavyGuardMelee2PrimerSkill = new Skill {
            RecoveryFrames = 65,
            RecoveryFramesOnBlock = 65,
            RecoveryFramesOnHit = 65,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(HeavyGuardMelee2PrimerBullet);

        public static BulletConfig HeavyGuardDashBullet = new BulletConfig {
            StartupFrames = 18,
            StartupInvinsibleFrames = 5,
            ActiveFrames = 32,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 13,
            PushbackVelX = (int)(+2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(20 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(+2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            SpeciesId = 2,
            DirX = 2,
            DirY = 0,
            ExplosionFrames = 25,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill HeavyGuardDashSkill = new Skill {
            RecoveryFrames = 55,
            RecoveryFramesOnBlock = 55,
            RecoveryFramesOnHit = 55,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing,
            SelfNonStockBuff = new BuffConfig {
                AutoDef1 = true,
                CharacterHardnessDelta = 99,
                RepelSoftPushback = true,
            },
        }
        .AddHit(HeavyGuardDashBullet);

        public static BulletConfig RiderGuardMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 43,
            ActiveFrames = 25,
            HitStunFrames = 20,
            BlockStunFrames = 9,
            Damage = 28,
            PushbackVelX = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(3.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(80 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DelaySelfVelToActive = true,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            RemainsUponHit = true,
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };
        
        public static Skill RiderGuardMelee1PrimerSkill = new Skill {
            RecoveryFrames = 72,
            RecoveryFramesOnBlock = 72,
            RecoveryFramesOnHit = 72,
            MpDelta = 400,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1,
            SelfNonStockBuff = new BuffConfig {
                AutoDef1 = true,
                CharacterHardnessDelta = 99,
                RepelSoftPushback = true,
            },
        }
        .AddHit(RiderGuardMelee1PrimerBullet);

        public static BulletConfig RiderGuardDashBullet = new BulletConfig {
            StartupFrames = 18,
            StartupInvinsibleFrames = 6,
            ActiveFrames = 33,
            HitStunFrames = 15,
            BlockStunFrames = 9,
            Damage = 10,
            PushbackVelX = (int)(+3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(+3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(30 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(40 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(+3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            SpeciesId = 3,
            DirX = 2,
            DirY = 0,
            ExplosionFrames = 25,
            RemainsUponHit = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion1",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill RiderGuardDashSkill = new Skill {
            RecoveryFrames = 52,
            RecoveryFramesOnBlock = 52,
            RecoveryFramesOnHit = 52,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing,
            SelfNonStockBuff = new BuffConfig {
                CharacterHardnessDelta = 99,
            },
        }
        .AddHit(RiderGuardDashBullet);

        public static BulletConfig StoneSwordHit1 = new BulletConfig {
            StartupFrames = 40,
            StartupInvinsibleFrames = 20,
            HitStunFrames = 15,
            ActiveFrames = 800,
            Damage = 22,
            Speed = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelX = (int)(5.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            HitboxOffsetX = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 25,
            ExplosionSpeciesId = 15,
            ExplosionFrames = 25,
            DirX = +2,
            DirY = 0,
            Hardness = 7,
            MhNotTriggerOnChHit = true,
            ProvidesYHardPushbackTop = true,
            ProvidesYHardPushbackBottom = true,
            ElementalAttrs = ELE_ROCK,
            BType = BulletType.Fireball,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };

        public static Skill StoneSwordSkill = new Skill {
            RecoveryFrames = 75,
            RecoveryFramesOnBlock = 75,
            RecoveryFramesOnHit = 75,
            MpDelta = 500,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1,
        }
        .AddHit(StoneSwordHit1)
        ;

        public static BulletConfig StoneRollHit1 = new BulletConfig {
            StartupFrames = 40,
            FinishingFrames = 40,
            ActiveFrames = 260,
            HitStunFrames = 25,
            BlockStunFrames = 9,
            Damage = 22,
            PushbackVelX = (int)(5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(4.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(8.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 3,
            DirX = 2,
            DirY = 0,
            Hardness = 60,
            NoExplosionOnHardPushback = true,
            RemainsUponHit = true,
            DefaultHardPushbackBounceQuota = 100,
            HardPushbackBounceNormFactor = 1.0f,
            HardPushbackBounceSheerFactor = 1.0f,
            SpinAnchorX = 18f,
            SpinAnchorY = 18f,
            AngularFrameVelCos = (float)Math.Cos(9f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            AngularFrameVelSin = (float)Math.Sin(9f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            InitSpinCos = 1,
            InitSpinSin = 0,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "Explosion3",
            DelaySelfVelToActive = true,
            RotateOffenderWithSpin = true,
            OmitSoftPushback = true,
            MhType = MultiHitType.FromEmission,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };

        public static BulletConfig StoneRollHit2 = new BulletConfig {
            StartupFrames = StoneRollHit1.StartupFrames+StoneRollHit1.ActiveFrames,
            ActiveFrames = 40,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SpeciesId = 1,
            DirX = 2,
            DirY = 0,
            Hardness = 60,
            NoExplosionOnHardPushback = true,
            AngularFrameVelCos = (float)Math.Cos(6f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            AngularFrameVelSin = (float)Math.Sin(6f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            SpinAnchorX = 12f,
            SpinAnchorY = 12f,
            InitSpinCos = 1,
            InitSpinSin = 0,
            BType = BulletType.Melee,
            RotateOffenderWithSpin = true,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX,
        };

        public static Skill StoneRollSkill = new Skill {
            RecoveryFrames = 350,
            RecoveryFramesOnBlock = 350,
            RecoveryFramesOnHit = 350,
            MpDelta = 600,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
                RepelSoftPushback = true,
            }
        }
        .AddHit(StoneRollHit1)
        .AddHit(StoneRollHit2);

        public static BulletConfig StoneCrusherStarterBullet = new BulletConfig {
            StartupFrames = 10,
            StartupInvinsibleFrames = 7,
            ActiveFrames = 7*BATTLE_DYNAMICS_FPS,
            BlowUp = true,
            HitStunFrames = MAX_INT,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(-8.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 
            HitboxSizeX = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_ROCK,
            DirX = 2,
            DirY = 0,
            Hardness = 100,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName= "Explosion2",
            MhType = MultiHitType.FromPrevHitAnyway,
            MhNotTriggerOnChHit = true,
            MhNotTriggerOnHarderBulletHit = true,
            WallImpactMeleeCollision = true,
            FinishingFrames = 40,
            RemainsUponHit = true,
            OmitSoftPushback = true,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX, 
        };

        public static BulletConfig StoneCrusherCastBullet1 = new BulletConfig { 
            StartupFrames = 60,
            StartupInvinsibleFrames = 20,
            HitStunFrames = 45,
            ActiveFrames = 800,
            Damage = 30,
            Speed = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 28,
            StartupVfxSpeciesId = VfxStoneDropperStart.SpeciesId,
            ExplosionSpeciesId = 25,
            ExplosionFrames = 25,
            UseChOffsetRegardlessOfEmissionMh = true,
            DirX = +2,
            DirY = 0,
            Hardness = 100,
            ElementalAttrs = ELE_ROCK,
            SimultaneousMultiHitCnt = 3,
            BType = BulletType.Fireball,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
        };

        public static BulletConfig StoneCrusherCastBullet2 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int) (3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetSimultaneousMultiHitCnt(2);

        public static BulletConfig StoneCrusherCastBullet3 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(64 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetMhType(MultiHitType.FromEmissionJustActive)
            .SetSimultaneousMultiHitCnt(0);

        public static BulletConfig StoneCrusherCastBullet4 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetStartupFrames(100)
            .SetSimultaneousMultiHitCnt(3);

        public static BulletConfig StoneCrusherCastBullet5 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetStartupFrames(100)
            .SetSimultaneousMultiHitCnt(2);

        public static BulletConfig StoneCrusherCastBullet6 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(80 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetStartupFrames(100)
            .SetMhType(MultiHitType.FromEmissionJustActive)
            .SetSimultaneousMultiHitCnt(0);

        public static BulletConfig StoneCrusherCastBullet7 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetStartupFrames(140)
            .SetSimultaneousMultiHitCnt(3);

        public static BulletConfig StoneCrusherCastBullet8 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetStartupFrames(140)
            .SetSimultaneousMultiHitCnt(2);

        public static BulletConfig StoneCrusherCastBullet9 = new BulletConfig(StoneCrusherCastBullet1)
            .SetSpeed((int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetHitboxOffsets((int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(64 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
            .SetStartupFrames(140)
            .SetSimultaneousMultiHitCnt(0);

        public static Skill StoneCrusherSkill = new Skill {
            RecoveryFrames = 650,
            RecoveryFramesOnBlock = 650,
            RecoveryFramesOnHit = 650,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3, 
        }
        .AddHit(StoneCrusherStarterBullet)
        .AddHit(StoneCrusherCastBullet1)
        .AddHit(StoneCrusherCastBullet2)
        .AddHit(StoneCrusherCastBullet3)
        .AddHit(StoneCrusherCastBullet4)
        .AddHit(StoneCrusherCastBullet5)
        .AddHit(StoneCrusherCastBullet6)
        .AddHit(StoneCrusherCastBullet7)
        .AddHit(StoneCrusherCastBullet8)
        .AddHit(StoneCrusherCastBullet9)
        ;

        public static BulletConfig StoneDropperStarterHit = new BulletConfig {
            StartupFrames = 30,
            StartupInvinsibleFrames = 35,
            ActiveFrames = 30,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            Speed = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = NO_VFX_ID,
            ExplosionSpeciesId = NO_VFX_ID,
            ExplosionVfxSpeciesId = NO_VFX_ID,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            HitboxOffsetX = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(240 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            MhType = MultiHitType.FromVisionSeekOrDefault,
            MhNotTriggerOnHarderBulletHit = true, // [WARNING] Together with "CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX", only targets character 
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig StoneDropperStrikerHit1 = new BulletConfig {
            StartupFrames = StoneDropperStarterHit.StartupFrames+StoneDropperStarterHit.ActiveFrames, // [WARNING] Different from the calculation of "DiverImpactStarterBullet" which is `Melee`, this "StartupFrames" is all accounted in "StartupVfxSpeciesId" because the animation clip of any `Fireball` only begins with active state!
            ActiveFrames = 480,
            HitStunFrames = MAX_INT,
            BlowUp = true,
            HitInvinsibleFrames = 6,
            BlockStunFrames = 2,
            Damage = 32,
            PushbackVelX = 0,
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            Speed = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Could be too fast and pass through the target
            SpeciesId = 28,
            StartupVfxSpeciesId = VfxStoneDropperStart.SpeciesId,
            ExplosionSpeciesId = 25,
            ExplosionFrames = 25,
            IsPixelatedActiveVfx = true,
            BType = BulletType.Fireball,
            DirX = 0,
            DirY = -2,
            Hardness = 10,
            ElementalAttrs = ELE_ROCK,
            SimultaneousMultiHitCnt = 2,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX,
        };

        public static BulletConfig StoneDropperStrikerHit2 = new BulletConfig(StoneDropperStrikerHit1)
                                                            .SetHitboxOffsets((int)(-60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                            .SetSimultaneousMultiHitCnt(1);

        public static BulletConfig StoneDropperStrikerHit3 = new BulletConfig(StoneDropperStrikerHit1)
                                                            .SetHitboxOffsets((int)(+60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                            .SetSimultaneousMultiHitCnt(0);

        public static Skill StoneDropperSkill = new Skill {
            RecoveryFrames = 75,
            RecoveryFramesOnBlock = 75,
            RecoveryFramesOnHit = 75,
            MpDelta = 700,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk4,
            SelfNonStockBuff = new BuffConfig {
                RepelSoftPushback = true,
            }
        }
        .AddHit(StoneDropperStarterHit)
        .AddHit(StoneDropperStrikerHit1)
        .AddHit(StoneDropperStrikerHit2)
        .AddHit(StoneDropperStrikerHit3)
        ;

        public static BulletConfig ThunderBoltStarterHit = new BulletConfig {
            StartupFrames = 30,
            StartupInvinsibleFrames = 40,
            ActiveFrames = 30,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            Speed = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 26,
            ExplosionSpeciesId = NO_VFX_ID,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            HitboxOffsetX = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            MhType = MultiHitType.FromVisionSeekOrDefault,
            MhNotTriggerOnHarderBulletHit = true, // [WARNING] Together with "CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX", only targets character 
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig ThunderBoltStrikerHit = new BulletConfig {
            StartupFrames = 40, // [WARNING] Different from the calculation of "DiverImpactStarterBullet" which is `Melee`, this "StartupFrames" is all accounted in "StartupVfxSpeciesId" because the animation clip of any `Fireball` only begins with active state! 
            StartupInvinsibleFrames = 40,
            ActiveFrames = 30,
            HitStunFrames = 60,
            HitInvinsibleFrames = 6,
            BlockStunFrames = 2,
            Damage = 40,
            PushbackVelX = 0,
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeIncY = 2,
            Speed = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 27,
            ExplosionSpeciesId = 26,
            NoExplosionOnHardPushback = true,
            InplaceVanishExplosionSpeciesId = 20,
            StartupVfxSpeciesId = VfxThunderStrikeStart.SpeciesId,
            IsPixelatedActiveVfx = true,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            DirX = 0,
            DirY = -1,
            Hardness = 10,
            DelaySelfVelToActive = true,
            ElementalAttrs = ELE_THUNDER,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            MhType = MultiHitType.FromPrevHitActual,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX,
            BuffConfig = LongParalyzer,
        };

        public static BulletConfig ThunderBoltHopperHit = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 120,
            HitStunFrames = 45,
            HitInvinsibleFrames = 6,
            BlockStunFrames = 2,
            Damage = 12,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            Speed = (int)(5.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Could be too fast and pass through the target
            RotatesAlongVelocity = true,
            SpeciesId = 26,
            ExplosionFrames = 25,
            BType = BulletType.MissileLinear,
            MissileSearchIntervalPow2Minus1 = (1u << 3) - 1u,
            BeamRendering = true,
            BeamVisualSizeY = (int)(11 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] DON'T hardcode in Unity "AnimationCilp.GetComponent<SpriteRenderer>().size" or it'd cause weird rendering!
            HopperMissile = true,
            RemainsUponHit = true,
            DefaultHardPushbackBounceQuota = 9,
            MhNotTriggerOnHardPushbackHit = true,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ElementalAttrs = ELE_THUNDER,
            VisionOffsetX = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(300 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(300 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            ActiveVfxSpeciesId = VfxHoppingBolt.SpeciesId,
            IsPixelatedActiveVfx = true,
            CollisionTypeMask = COLLISION_FIREBALL_INDEX_PREFIX,
            BuffConfig = LongParalyzer,
        };

        public static Skill ThunderBoltSkill = new Skill {
            RecoveryFrames = 75,
            RecoveryFramesOnBlock = 75,
            RecoveryFramesOnHit = 75,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5, 
        }
        .AddHit(ThunderBoltStarterHit)
        .AddHit(ThunderBoltStrikerHit)
        .AddHit(ThunderBoltHopperHit)
        ;

        public static BulletConfig RepPerforationBl1 = new BulletConfig {
            StartupFrames = 19,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 6,
            HitStunFrames = 12,
            BlockStunFrames = 8,
            Damage = 4,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            RemainsUponHit = true,
            MhType = MultiHitType.FromEmission,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };
        public static BulletConfig RepPerforationBl2 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(25);
        public static BulletConfig RepPerforationBl3 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(37);
        public static BulletConfig RepPerforationBl4 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(43);
        public static BulletConfig RepPerforationBl5 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(49);
        public static BulletConfig RepPerforationBl6 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(55);
        public static BulletConfig RepPerforationBl7 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(61);
        public static BulletConfig RepPerforationBl8 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(67);
        public static BulletConfig RepPerforationBl9 = new BulletConfig(RepPerforationBl1)
                                                            .SetStartupFrames(73)
                                                            .SetMhType(MultiHitType.None);

        public static BulletConfig LightRepPerforationBl1 = new BulletConfig {
            StartupFrames = 12,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 6,
            HitStunFrames = 12,
            BlockStunFrames = 8,
            Damage = 6,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 8,
            RemainsUponHit = true,
            MhType = MultiHitType.FromEmission,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };
        public static BulletConfig LightRepPerforationBl2 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(18);
        public static BulletConfig LightRepPerforationBl3 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(30);
        public static BulletConfig LightRepPerforationBl4 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(36)
                                                            .SetCancellableFrames(36, 42).UpsertCancelTransit(PATTERN_UP_B, 113)
                                                            ;
        public static BulletConfig LightRepPerforationBl5 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(42)
                                                            .SetCancellableFrames(42, 48).UpsertCancelTransit(PATTERN_UP_B, 113)
                                                            ;
        public static BulletConfig LightRepPerforationBl6 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(48)
                                                            .SetCancellableFrames(48, 54).UpsertCancelTransit(PATTERN_UP_B, 113)
                                                            ;
        public static BulletConfig LightRepPerforationBl7 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(54)
                                                            .SetCancellableFrames(54, 60).UpsertCancelTransit(PATTERN_UP_B, 113)
                                                            ;
        public static BulletConfig LightRepPerforationBl8 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(60)
                                                            .SetCancellableFrames(60, 66).UpsertCancelTransit(PATTERN_UP_B, 113)
                                                            ;
        public static BulletConfig LightRepPerforationBl9 = new BulletConfig(LightRepPerforationBl1)
                                                            .SetStartupFrames(66)
                                                            .SetCancellableFrames(66, 78).UpsertCancelTransit(PATTERN_UP_B, 113)
                                                            .SetMhType(MultiHitType.None);

        public static BulletConfig BladeGirlAirSlash1Hit1 = new BulletConfig(BasicAirBladeHit1)
                                                            .UpsertCancelTransit(PATTERN_B, BladeGirlAirSlash2Id)    
                                                            .UpsertCancelTransit(PATTERN_UP_B, BladeGirlAirSlash2Id)
                                                            .UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlDiverImpactId);
        
        public static Skill BladeGirlAirSlash1 = new Skill {
               Id = BladeGirlAirSlash1Id,
               RecoveryFrames = 20,
               RecoveryFramesOnBlock = 20,
               RecoveryFramesOnHit = 20,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = InAirAtk1
        }.AddHit(BladeGirlAirSlash1Hit1);
    
        public static Skill BladeGirlAirSlash2 = new Skill{
               Id = BladeGirlAirSlash2Id, 
               RecoveryFrames = 45,
               RecoveryFramesOnBlock = 45,
               RecoveryFramesOnHit = 45,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = InAirAtk2
        }.AddHit(BasicAirBladeHit2);

        public static BulletConfig WandWitchGirlBasicBulletHit1 = new BulletConfig {
            StartupFrames = 15,
            StartupInvinsibleFrames = 12,
            ActiveFrames = 450,
            HitStunFrames = 24,
            HitInvinsibleFrames = 6,
            BlockStunFrames = 2,
            Damage = 15,
            PushbackVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            Speed = (int)(2.2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            RotatesAlongVelocity = true,
            SpeciesId = 33,
            ExplosionSpeciesId = 13,
            ExplosionFrames = 25,
            BType = BulletType.MissileLinear,
            MissileSearchIntervalPow2Minus1 = (1u << 3) - 1u,
            DirX = +2,
            DirY = +1,
            Hardness = 5,
            ElementalAttrs = ELE_THUNDER,
            AngularFrameVelCos = (float)Math.Cos(15.0/(Math.PI * BATTLE_DYNAMICS_FPS)), // human readable number is in degrees/second
            AngularFrameVelSin = (float)Math.Sin(15.0/(Math.PI * BATTLE_DYNAMICS_FPS)),
            VisionOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionOffsetY = (int)(0 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            IsPixelatedActiveVfx = true,
            SimultaneousMultiHitCnt = 1,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX,
            BuffConfig = ShortFreezer,
        };

        public static BulletConfig WandWitchGirlBasicBulletHit2 = new BulletConfig(WandWitchGirlBasicBulletHit1)
                                                                        .SetDir(+2, -1)
                                                                        .SetSimultaneousMultiHitCnt(0);

        public static BulletConfig WandWitchHeatBeamBulletHit1 = new BulletConfig {
            StartupFrames = 80,
            StartupInvinsibleFrames = 120,
            ActiveFrames = 180,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 10,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(30f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(20 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BeamVisualSizeY = (int)(22 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] DON'T hardcode in Unity "AnimationCilp.GetComponent<SpriteRenderer>().size" or it'd cause weird rendering!
            SpeciesId = 24,
            ExplosionSpeciesId = 15,
            Speed = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeedIfNotHit = (int)(7.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 10,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "Explosion4",
            StartupVfxSpeciesId = VfxHeatBeam.SpeciesId,
            ActiveVfxSpeciesId = VfxHeatBeam.SpeciesId,
            IsPixelatedActiveVfx = true,
            BeamCollision = true,
            MhType = MultiHitType.FromPrevHitActual,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig WandWitchHeatBeamBulletRepeatingHit = new BulletConfig(WandWitchHeatBeamBulletHit1)
                                                              .SetHitboxOffsets(0, 0)
                                                              .SetStartupFrames(8)
                                                              .SetStartupInvinsibleFrames(0);

        public static BulletConfig WandWitchHeatBeamBulletEnderHit = new BulletConfig(WandWitchHeatBeamBulletHit1)
                                                              .SetMhType(MultiHitType.None);


        public static Skill WandWitchHeatBeamSkill = new Skill {
            RecoveryFrames = 210,
            RecoveryFramesOnBlock = 210,
            RecoveryFramesOnHit = 210,
            TriggerType = SkillTriggerType.RisingEdge,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true,
                RepelSoftPushback = true,
                CharacterHardnessDelta = 3,
            },
            BoundChState = Atk3
        }
        .AddHit(WandWitchHeatBeamBulletHit1)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletRepeatingHit)
        .AddHit(WandWitchHeatBeamBulletEnderHit)
        ;

        public static BulletConfig SpearWomanBasicFireballBl = new BulletConfig {
            StartupFrames = 34,
            StartupInvinsibleFrames = 30,
            ActiveFrames = 600,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 45,
            BlockStunFrames = 8,
            Damage = 32,
            PushbackVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 30,
            ExplosionSpeciesId = 19,
            Speed = (int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 7,
            RemainsUponHit = true,
            ElementalAttrs = ELE_THUNDER,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig LightSpearWomanBasicFireballBl = new BulletConfig {
            StartupFrames = 34,
            StartupInvinsibleFrames = 15,
            ActiveFrames = 600,
            HitStunFrames = MAX_INT,
            HitInvinsibleFrames = 60,
            BlockStunFrames = 8,
            BlowUp = true,
            Damage = 25,
            PushbackVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(22 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 32,
            ExplosionSpeciesId = 19,
            Speed = (int)(4.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 6,
            RemainsUponHit = true,
            ExplosionFrames = 25,
            ElementalAttrs = ELE_THUNDER,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SpearWomanMeleePrimerBl = new BulletConfig {
            StartupFrames = 10,
            StartupInvinsibleFrames = 5,
            ActiveFrames = 16,
            HitStunFrames = 35,
            BlockStunFrames = 8,
            Damage = 15,
            PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(18*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CancellableStFrame = 21,
            CancellableEdFrame = 32,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            RemainsUponHit = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, 106);

        public static BulletConfig SpearWomanAirMeleePrimerBl = new BulletConfig {
            StartupFrames = 8,
            StartupInvinsibleFrames = 3,
            ActiveFrames = 20,
            HitStunFrames = 35,
            BlockStunFrames = 8,
            Damage = 15,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ReflectFireballXIfNotHarder = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig SpearWomanDragonPunchPrimerBl = new BulletConfig {
            StartupFrames = 15,
            StartupInvinsibleFrames = 16,
            ActiveFrames = 25,
            HitStunFrames = 33,
            BlockStunFrames = 25,
            Damage = 20,
            CancellableStFrame = 22,
            CancellableEdFrame = 42,
            PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(-1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 9,
            ReflectFireballXIfNotHarder = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, 105).UpsertCancelTransit(PATTERN_DOWN_B, 105).UpsertCancelTransit(PATTERN_UP_B, 105);

        public static BulletConfig WindShaverPrimerBl = new BulletConfig {
            StartupFrames = 13,
            StartupInvinsibleFrames = 16,
            ActiveFrames = 9,
            HitStunFrames = 20,
            BlockStunFrames = 8,
            Damage = 9,
            PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 9,
            ElementalAttrs = ELE_WIND,
            StartupVfxSpeciesId = VfxSmallSlash1.SpeciesId,
            ActiveVfxSpeciesId = VfxSmallSlash1.SpeciesId,
            RemainsUponHit = true,
            IsPixelatedActiveVfx = true,
            ReflectFireballXIfNotHarder = true,
            MhType = MultiHitType.FromEmission,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion2",
            NoExplosionOnHardPushback = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig WindShaverHit2 = new BulletConfig(WindShaverPrimerBl)
        .SetStartupFrames(WindShaverPrimerBl.StartupFrames+WindShaverPrimerBl.ActiveFrames)
        .SetStartupInvinsibleFrames(0)
        .SetStartupVfxSpeciesId(VfxSmallSlash2.SpeciesId)
        .SetActiveVfxSpeciesId(VfxSmallSlash2.SpeciesId);

        public static BulletConfig WindShaverHit3 = new BulletConfig(WindShaverPrimerBl)
        .SetStartupFrames(WindShaverHit2.StartupFrames+WindShaverHit2.ActiveFrames)
        .SetStartupInvinsibleFrames(0)
        .SetStartupVfxSpeciesId(VfxSmallSlash3.SpeciesId)
        .SetActiveVfxSpeciesId(VfxSmallSlash3.SpeciesId);

        public static BulletConfig WindShaverHit4 = new BulletConfig(WindShaverPrimerBl)
        .SetStartupFrames(WindShaverHit3.StartupFrames+WindShaverHit3.ActiveFrames)
        .SetStartupInvinsibleFrames(0)
        .SetStartupVfxSpeciesId(VfxSmallSlash1.SpeciesId)
        .SetActiveVfxSpeciesId(VfxSmallSlash1.SpeciesId);

        public static BulletConfig WindShaverHit5 = new BulletConfig(WindShaverPrimerBl)
        .SetStartupFrames(WindShaverHit4.StartupFrames+WindShaverHit4.ActiveFrames)
        .SetStartupInvinsibleFrames(0)
        .SetStartupVfxSpeciesId(VfxSmallSlash2.SpeciesId)
        .SetActiveVfxSpeciesId(VfxSmallSlash2.SpeciesId);

        public static BulletConfig WindShaverEnd = new BulletConfig(WindShaverPrimerBl)
        .SetStartupFrames(WindShaverHit5.StartupFrames+WindShaverHit5.ActiveFrames)
        .SetMhType(MultiHitType.None)
        .SetDamage(10)
        .SetBlowUp(true)
        .SetPushbacks((int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
        .SetHitStunFrames(MAX_INT)
        .SetHitInvinsibleFrames(20)
        .SetStartupVfxSpeciesId(VfxSmallSlash3.SpeciesId)
        .SetActiveVfxSpeciesId(VfxSmallSlash3.SpeciesId);

        public static BulletConfig LightSpearWomanMeleePrimerBl = new BulletConfig {
            StartupFrames = 9,
            StartupInvinsibleFrames = 5,
            ActiveFrames = 17,
            HitStunFrames = 35,
            BlockStunFrames = 8,
            Damage = 16,
            PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(18*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CancellableStFrame = 16,
            CancellableEdFrame = 27,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 6,
            RemainsUponHit = true,
            ElementalAttrs = ELE_THUNDER,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Piercing",
            ExplosionOnRockSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        }.UpsertCancelTransit(PATTERN_B, 112);

        public static BulletConfig LightSpearWomanAirMeleePrimerBl = new BulletConfig {
            StartupFrames = 6,
            StartupInvinsibleFrames = 5,
            ActiveFrames = 17,
            HitStunFrames = 35,
            BlockStunFrames = 8,
            Damage = 22,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 7,
            ElementalAttrs = ELE_THUNDER,
            ReflectFireballXIfNotHarder = true,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName="Melee_Explosion2",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig DarkTowerPrimerBl1 = new BulletConfig {
            StartupFrames = 40,
            ActiveFrames = 600,
            HitStunFrames = MAX_INT,
            BlowUp = true,
            BlockStunFrames = 9,
            Damage = 32,
            PushbackVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            HitboxOffsetX = (int)(4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(22f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(28*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 31,
            Speed = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 32,
            ExplosionFrames = 30,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="FlameEmit1",
            ExplosionSfxName="Explosion4",
            RotatesAlongVelocity = true,
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill DarkTowerPrimerSkill = new Skill {
            Id = 122,
            RecoveryFrames = 120,
            RecoveryFramesOnBlock = 120,
            RecoveryFramesOnHit = 120,
            MpDelta = 550,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(DarkTowerPrimerBl1);

        public static BulletConfig DarkTowerUpperBl1 = new BulletConfig(DarkTowerPrimerBl1).SetDir(1, 1);
        public static Skill DarkTowerUpperSkill = new Skill {
            Id = 123, 
            RecoveryFrames = 120,
            RecoveryFramesOnBlock = 120,
            RecoveryFramesOnHit = 120,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(DarkTowerUpperBl1);

        public static BulletConfig DarkTowerLowerBl1 = new BulletConfig(DarkTowerPrimerBl1).SetDir(1, -1);
        public static Skill DarkTowerLowerSkill = new Skill {
            Id = 124,
            RecoveryFrames = 120,
            RecoveryFramesOnBlock = 120,
            RecoveryFramesOnHit = 120,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(DarkTowerLowerBl1);

        public static BulletConfig SmallBallEmitterBeamHit1 = new BulletConfig {
            StartupFrames = 30,
            StartupInvinsibleFrames = 12,
            ActiveFrames = MAX_INT,
            HitStunFrames = 10,
            HitInvinsibleFrames = 45,
            BlockStunFrames = 9,
            Damage = 30,
            PushbackVelX = (int)(1.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(-2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BeamRendering = true,
            BeamVisualSizeY = (int)(22*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] DON'T hardcode in Unity "AnimationCilp.GetComponent<SpriteRenderer>().size" or it'd cause weird rendering!
            SpeciesId = 24,
            ExplosionSpeciesId = 15,
            Speed = (int)(0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeedIfNotHit = (int)(2.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 999,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            ExplosionSfxName="Explosion4",
            StartupVfxSpeciesId = VfxSmallBallEmitterBeam.SpeciesId,
            ActiveVfxSpeciesId = VfxSmallBallEmitterBeam.SpeciesId,
            IsPixelatedActiveVfx = true,
            BeamCollision = true,
            RemainsUponHit = true,
            InitSpinCos = (float)Math.Cos(0.5 * Math.PI), 
            InitSpinSin = (float)Math.Sin(0.5f * Math.PI),
            SpinAnchorX = 0f,
            SpinAnchorY = 5.0f,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        public static Skill SmallBallEmitterBeamSkill = new Skill{
            Id = 130
        }.AddHit(SmallBallEmitterBeamHit1);

        public static Skill WitchGirlMeleeSkill1 = new Skill {
            Id = 6,
            RecoveryFrames = 32,
            RecoveryFramesOnBlock = 32,
            RecoveryFramesOnHit = 32,
            MpDelta = 200,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk2
        }
        .AddHit(
            new BulletConfig {
                StartupFrames = 13,
                ActiveFrames = 19,
                HitStunFrames = 27,
                BlockStunFrames = 9,
                Damage = 15,
                PushbackVelX = (int)(-0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                PushbackVelY = NO_LOCK_VEL,
                SelfLockVelX = NO_LOCK_VEL,
                SelfLockVelY = NO_LOCK_VEL,
                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                HitboxOffsetX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeX = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                HitboxSizeY = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                CancellableStFrame = 16,
                CancellableEdFrame = 32,
                SpeciesId = 1,
                ExplosionSpeciesId = 1,
                DirX = 1,
                DirY = 0,
                Hardness = 5,
                ExplosionFrames = 25,
                BType = BulletType.Melee,
                RemainsUponHit = true,
                ReflectFireballXIfNotHarder = true,
                ElementalAttrs = ELE_FIRE,
                CharacterEmitSfxName = "FlameEmit1",
                ExplosionSfxName = "FlameBurning1",
                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
            }.UpsertCancelTransit(PATTERN_B, 7).UpsertCancelTransit(PATTERN_DOWN_B, 9).UpsertCancelTransit(PATTERN_UP_B, 55)
        );

        public static Skill WitchGirlMeleeSkill2 = new Skill {
            Id = 7,
            RecoveryFrames = 29,
            RecoveryFramesOnBlock = 29,
            RecoveryFramesOnHit = 29,
            MpDelta = 200,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk3
        }
            .AddHit(
                new BulletConfig {
                    StartupFrames = 7,
                    ActiveFrames = 22,
                    HitStunFrames = 23,
                    BlockStunFrames = 9,
                    Damage = 14,
                    PushbackVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelY = NO_LOCK_VEL,
                    SelfLockVelYWhenFlying = NO_LOCK_VEL,
                    HitboxOffsetX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SpeciesId = 1,
                    ExplosionSpeciesId = 1,
                    ExplosionFrames = 25,
                    BType = BulletType.Melee,
                    DirX = 1,
                    DirY = 0,
                    Hardness = 5,
                    RemainsUponHit = true,
                    ReflectFireballXIfNotHarder = true,
                    ElementalAttrs = ELE_FIRE,
                    CancellableStFrame = 16,
                    CancellableEdFrame = 30,
                    CancellableByInventorySlotC = true,
                    CharacterEmitSfxName = "FlameEmit1",
                    ExplosionSfxName = "FlameBurning1",
                    CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                }.UpsertCancelTransit(PATTERN_B, 8).UpsertCancelTransit(PATTERN_DOWN_B, 9).UpsertCancelTransit(PATTERN_UP_B, 55)
        );

        public static Skill WitchGirlMeleeSkill3 = new Skill {
            Id = 8,
            RecoveryFrames = 40,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            MpDelta = 300,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk4
        }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 12,
                                StartupInvinsibleFrames = 8,
                                ActiveFrames = 26,
                                HitStunFrames = MAX_INT,
                                HitInvinsibleFrames = 60,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(-4f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 3,
                                ExplosionSpeciesId = 3,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ElementalAttrs = ELE_FIRE,
                                RemainsUponHit = true,
                                CharacterEmitSfxName = "FlameEmit1",
                                ExplosionSfxName = "Explosion4",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                );

        public static Skill WitchGirlFireball = new Skill {
            Id = 9,
            RecoveryFrames = 50,
            RecoveryFramesOnBlock = 50,
            RecoveryFramesOnHit = 50,
            MpDelta = 500,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(WitchGirlFireballBulletHit1);

        public static Skill WitchGirlBackDashSkill = new Skill {
            Id = 10,
            RecoveryFrames = 15,
            RecoveryFramesOnBlock = 15,
            RecoveryFramesOnHit = 15,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = BackDashing
        }
        .AddHit(
            new BulletConfig {
                StartupFrames = 3,
                StartupInvinsibleFrames = 8,
                PushbackVelX = NO_LOCK_VEL,
                PushbackVelY = NO_LOCK_VEL,
                SelfLockVelX = (int)(-4f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                SelfLockVelY = 0,
                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                DelaySelfVelToActive = true,
                BType = BulletType.Melee,
                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
            }
        );

        public static Skill BladeGirlDashing = new Skill {
            Id = BladeGirlDashingId,
            RecoveryFrames = 20,
            RecoveryFramesOnBlock = 20,
            RecoveryFramesOnHit = 20,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing
        }
        .AddHit(
            new BulletConfig(StandardAirDashHit1).SetOmitSoftPushback(true).UpsertCancelTransit(PATTERN_B, BladeGirlAirSlash1.Id).UpsertCancelTransit(PATTERN_UP_B, BladeGirlAirSlash1.Id).UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlDiverImpact.Id)
        )
        .AddHit(
            new BulletConfig(StandardAirDashHit2).SetOmitSoftPushback(true).UpsertCancelTransit(PATTERN_B, BladeGirlAirSlash1.Id).UpsertCancelTransit(PATTERN_UP_B, BladeGirlAirSlash1.Id).UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlDiverImpact.Id)
        );

        public static Skill SwordManDragonPunchPrimerSkill = new Skill {
            Id = 12,
            RecoveryFrames = 40,
            MpDelta = 150,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk4
        }
        .AddHit(SwordManDragonPunchPrimerBullet);

        public static Skill SpearWomanSliding = new Skill {
            Id = 18,
            RecoveryFrames = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
            RecoveryFramesOnBlock = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
            RecoveryFramesOnHit = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Sliding
        }
                            .AddHit(new BulletConfig(BasicSlidingHit1).UpsertCancelTransit(PATTERN_B, 105).UpsertCancelTransit(PATTERN_DOWN_B, 109).UpsertCancelTransit(PATTERN_UP_B, 109))
                            .AddHit(new BulletConfig(BasicSlidingHit2).UpsertCancelTransit(PATTERN_B, 105).UpsertCancelTransit(PATTERN_DOWN_B, 109).UpsertCancelTransit(PATTERN_UP_B, 109));

        public static Skill SpearWomanDashing = new Skill {
            Id = 19,
            RecoveryFrames = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
            RecoveryFramesOnBlock = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
            RecoveryFramesOnHit = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing
        }
            .AddHit(new BulletConfig(BasicDashingHit1).UpsertCancelTransit(PATTERN_B, 105))
            .AddHit(new BulletConfig(BasicDashingHit2).UpsertCancelTransit(PATTERN_B, 105));

        public static Skill HunterBackDashing = new Skill {
            Id = 131,
            RecoveryFrames = 15,
            RecoveryFramesOnBlock = 15,
            RecoveryFramesOnHit = 15,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = BackDashing
        }
        .AddHit(
            new BulletConfig {
                StartupFrames = 3,
                StartupInvinsibleFrames = 8,
                PushbackVelX = NO_LOCK_VEL,
                PushbackVelY = NO_LOCK_VEL,
                SelfLockVelX = (int)(-4f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                SelfLockVelY = 0,
                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                DelaySelfVelToActive = true,
                BType = BulletType.Melee,
                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
            }
        );

        public static BulletConfig BladeGirlSlidingHit1 = new BulletConfig(BasicSlidingHit1)
                                                              .SetOmitSoftPushback(true)
                                                              .SetActiveFrames(10)
                                                              .SetSelfLockVel((int)(4.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, NO_LOCK_VEL)
                                                              .SetCollisionTypeMask(COLLISION_CHARACTER_INDEX_PREFIX)
                                                              .UpsertCancelTransit(PATTERN_B, BladeGirlSlidingSlashSkill.Id)
                                                              .UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlSlidingSlashSkill.Id)
                                                              .UpsertCancelTransit(PATTERN_UP_B, BladeGirlDragonPunch.Id);
                                                            
        
        public static BulletConfig BladeGirlSlidingHit2 = new BulletConfig(BasicSlidingHit2)
                                                            .SetStartupFrames(BladeGirlSlidingHit1.StartupFrames+BladeGirlSlidingHit1.ActiveFrames) 
                                                            .SetCancellableFrames(BladeGirlSlidingHit1.StartupFrames+BladeGirlSlidingHit1.ActiveFrames, 40)
                                                            .SetOmitSoftPushback(true)
                                                            .SetCollisionTypeMask(COLLISION_CHARACTER_INDEX_PREFIX)
                                                            .UpsertCancelTransit(PATTERN_B, BladeGirlSlidingSlashSkill.Id)
                                                            .UpsertCancelTransit(PATTERN_DOWN_B, BladeGirlSlidingSlashSkill.Id)
                                                            .UpsertCancelTransit(PATTERN_UP_B, BladeGirlDragonPunch.Id);

        public static Skill BladeGirlSliding = new Skill{
            Id = 20,
               RecoveryFrames = 30,
               RecoveryFramesOnBlock = 30,
               RecoveryFramesOnHit = 30,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Sliding
        }
        .AddHit(BladeGirlSlidingHit1)
        .AddHit(BladeGirlSlidingHit2);

        public static BulletConfig HunterSlidingHit1 = new BulletConfig(BasicSlidingHit1)
                                                              .SetActiveFrames(10)
                                                              .SetSelfLockVel((int)(4.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), NO_LOCK_VEL, NO_LOCK_VEL)
                                                              .SetCollisionTypeMask(COLLISION_CHARACTER_INDEX_PREFIX)
                                                              .UpsertCancelTransit(PATTERN_B, HunterPistolId)
                                                              .UpsertCancelTransit(PATTERN_DOWN_B, HunterPistolCrouchId)
                                                              .UpsertCancelTransit(PATTERN_UP_B, HunterDragonPunchId)
                                                              .UpsertCancelTransit(PATTERN_RELEASED_B, MobileThunderBallPrimerId);
                                                            
        
        public static BulletConfig HunterSlidingHit2 = new BulletConfig(BasicSlidingHit2)
                                                            .SetStartupFrames(BladeGirlSlidingHit1.StartupFrames+BladeGirlSlidingHit1.ActiveFrames) 
                                                            .SetCancellableFrames(BladeGirlSlidingHit1.StartupFrames+BladeGirlSlidingHit1.ActiveFrames, 40)
                                                            .SetOmitSoftPushback(true)
                                                            .SetCollisionTypeMask(COLLISION_CHARACTER_INDEX_PREFIX)
                                                            .UpsertCancelTransit(PATTERN_B, HunterPistolId)
                                                            .UpsertCancelTransit(PATTERN_DOWN_B, HunterPistolCrouchId)
                                                            .UpsertCancelTransit(PATTERN_UP_B, HunterDragonPunchId)
                                                            .UpsertCancelTransit(PATTERN_RELEASED_B, MobileThunderBallPrimerId);

        public static Skill HunterSliding = new Skill {
            Id = HunterSlidingId,
               RecoveryFrames = 30,
               RecoveryFramesOnBlock = 30,
               RecoveryFramesOnHit = 30,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Sliding
        }
        .AddHit(HunterSlidingHit1)
        .AddHit(HunterSlidingHit2);

        public static Skill HunterPistolWall = new Skill{
             Id = HunterPistolWallId,
               RecoveryFrames = 12,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = OnWallAtk1
        }.AddHit(BasicGunBulletAir);

        public static Skill HunterPistol = new Skill{
            Id = HunterPistolId,
               RecoveryFrames = 14,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = Atk1
        }
        .AddHit(BasicGunBulletGround);
        
        public static Skill HunterPistolAir = new Skill{
            Id = HunterPistolAirId,
               RecoveryFrames = 13,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = InAirAtk1
        }
        .AddHit(BasicGunBulletAir);

        public static Skill HunterPistolCrouch = new Skill{
            Id = HunterPistolCrouchId,
               RecoveryFrames = 14,
               RecoveryFramesOnBlock = 10,
               RecoveryFramesOnHit = 10,
               TriggerType = SkillTriggerType.RisingEdge,
               BoundChState = CrouchAtk1
        }
        .AddHit(BasicPistolBulletCrouch);

        public static Skill HunterDashing = new Skill {
            Id = HunterDashingId,
                RecoveryFrames = 18,
                RecoveryFramesOnBlock = 18,
                RecoveryFramesOnHit = 18,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = Dashing
        }
        .AddHit(
            new BulletConfig(BasicDashingHit1).UpsertCancelTransit(PATTERN_UP_B, HunterAirSlash.Id).UpsertCancelTransit(PATTERN_B, HunterPistolAir.Id).UpsertCancelTransit(PATTERN_DOWN_B, HunterPistolAir.Id).UpsertCancelTransit(PATTERN_RELEASED_B, MobileThunderBallPrimerAir.Id)
        )
        .AddHit(
            new BulletConfig(BasicDashingHit2).UpsertCancelTransit(PATTERN_UP_B, HunterAirSlash.Id).UpsertCancelTransit(PATTERN_B, HunterPistolAir.Id).UpsertCancelTransit(PATTERN_DOWN_B, HunterPistolAir.Id).UpsertCancelTransit(PATTERN_RELEASED_B, MobileThunderBallPrimerAir.Id)
        );

        public static BulletConfig BasicHpHealerStarterHit = new BulletConfig {
            StartupFrames = 10,
            StartupInvinsibleFrames = 8,
            ActiveFrames = 20,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            DirX = 1,
            DirY = 0,
            Damage = -30,
            Speed = (int)(8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetX = (int)(60 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeX = (int)(120 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            VisionSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            CharacterEmitSfxName = "SlashEmitSpd1",
            BType = BulletType.Melee,
            MhType = MultiHitType.FromVisionSeekOrDefault,
            HopperMissile = true,
            StartupVfxSpeciesId = VfxIceCharged.SpeciesId,
            ActiveVfxSpeciesId = VfxIceCharged.SpeciesId,
            IsPixelatedActiveVfx = true,
            MhNotTriggerOnHarderBulletHit = true, 
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig BasicHpHealerHit = new BulletConfig {
            StartupFrames = 0,
            ActiveFrames = 5,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = NO_LOCK_VEL,
            BType = BulletType.Fireball,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX,
            HitboxSizeX = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            Damage = -30,
            HitStunFrames = 60,
            ExplosionSpeciesId = 6,
            ExplosionFrames = 28,
        };

        public static Skill BasicHpHealer = new Skill {
            Id = 137,
            MpDelta = 500,
                RecoveryFrames = 60,
                RecoveryFramesOnBlock = 60,
                RecoveryFramesOnHit = 60,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = Atk1
        }
        .AddHit(BasicHpHealerStarterHit)
        .AddHit(BasicHpHealerHit);

        public static BulletConfig AngelBasicBulletHit1 = new BulletConfig {
            StartupFrames = 15,
            StartupInvinsibleFrames = 12,
            ActiveFrames = 450,
            HitStunFrames = 24,
            HitInvinsibleFrames = 6,
            BlockStunFrames = 2,
            Damage = 26,
            PushbackVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = 0,
            SelfLockVelY = NO_LOCK_VEL,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            Speed = (int)(4.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 14,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            DirX = +1,
            DirY = 0,
            Hardness = 7,
            ElementalAttrs = ELE_THUNDER,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX,
            BuffConfig = ShortParalyzer
        };

        public static uint AngelBasicAtkId = 138;
        public static Skill AngelBasicAtk = new Skill {
            Id = AngelBasicAtkId,
                RecoveryFrames = 40,
                RecoveryFramesOnBlock = 40,
                RecoveryFramesOnHit = 40,
                TriggerType = SkillTriggerType.RisingEdge,
                BoundChState = Atk2
        }.AddHit(AngelBasicBulletHit1);

        public static BulletConfig AngelBackDashingHit1 = new BulletConfig {
            StartupFrames = 19,
            StartupInvinsibleFrames = 12,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(-2.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill AngelBackDashing = new Skill {
            Id = 139,
            RecoveryFrames = 40,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            MpDelta = 60,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = BackDashing
        }.AddHit(AngelBackDashingHit1);

        public static BulletConfig AngelDashingHit1 = new BulletConfig {
            StartupFrames = 19,
            StartupInvinsibleFrames = 12,
            PushbackVelX = NO_LOCK_VEL,
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = (int)(3.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            DelaySelfVelToActive = true,
            BType = BulletType.Melee,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill AngelDashing = new Skill {
            Id = 140,
            RecoveryFrames = 40,
            RecoveryFramesOnBlock = 40,
            RecoveryFramesOnHit = 40,
            MpDelta = 60,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Dashing
        }.AddHit(AngelDashingHit1);

        public static BulletConfig DrakePrimerFireballBl1 = new BulletConfig {
            StartupFrames = 28,
            ActiveFrames = 600,
            HitStunFrames = MAX_INT,
            BlowUp = true,
            BlockStunFrames = 9,
            Damage = 32,
            PushbackVelX = (int)(3.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = 0,
            SelfLockVelY = 0,
            SelfLockVelYWhenFlying = 0,
            HitboxOffsetX = (int)(6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(22 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 31,
            Speed = (int)(3 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 32,
            ExplosionFrames = 30,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "Explosion4",
            CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
        };

        public static Skill DrakePrimerFireball = new Skill {
            Id = 141,
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 60,
            RecoveryFramesOnHit = 60,
            MpDelta = 550,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk1
        }
        .AddHit(DrakePrimerFireballBl1);

        public static Skill DrakePrimerAirFireball = new Skill {
            Id = 142,
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 60,
            RecoveryFramesOnHit = 60,
            MpDelta = 550,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = InAirAtk1,
            SelfNonStockBuff = new BuffConfig {
                OmitGravity = true
            }
        }
        .AddHit(DrakePrimerFireballBl1);

        public static ImmutableDictionary<uint, Skill> skills = ImmutableDictionary.Create<uint, Skill>().AddRange(
                new[]
                {
                new KeyValuePair<uint, Skill>(BladeGirlGroundSlash1.Id, BladeGirlGroundSlash1),
                new KeyValuePair<uint, Skill>(BladeGirlGroundSlash2.Id, BladeGirlGroundSlash2),
                new KeyValuePair<uint, Skill>(BladeGirlGroundSlash3.Id, BladeGirlGroundSlash3),
                new KeyValuePair<uint, Skill>(BladeGirlGroundSuperSlash.Id, BladeGirlGroundSuperSlash),
                new KeyValuePair<uint, Skill>(5, SwordManMelee1PrimerSkill),
                new KeyValuePair<uint, Skill>(6, WitchGirlMeleeSkill1),
                new KeyValuePair<uint, Skill>(7, WitchGirlMeleeSkill2),
                new KeyValuePair<uint, Skill>(8, WitchGirlMeleeSkill3),
                new KeyValuePair<uint, Skill>(9, WitchGirlFireball),
                new KeyValuePair<uint, Skill>(10, WitchGirlBackDashSkill),

                    new KeyValuePair<uint, Skill>(BladeGirlDashing.Id, BladeGirlDashing),

                    new KeyValuePair<uint, Skill>(12, SwordManDragonPunchPrimerSkill),

                    new KeyValuePair<uint, Skill>(13, FireSwordManFireballSkill),

                    new KeyValuePair<uint, Skill>(14, DemonFireSlimeMelee1PrimarySkill),

                    new KeyValuePair<uint, Skill>(15, DemonFireSlimeFireballSkill),

                    new KeyValuePair<uint, Skill>(16, FireSwordManMelee1PrimerSkill),

                    new KeyValuePair<uint, Skill>(17, FireSwordManDragonPunchPrimerSkill),

                    new KeyValuePair<uint, Skill>(18, SpearWomanSliding),

                    new KeyValuePair<uint, Skill>(19, SpearWomanDashing),

                    new KeyValuePair<uint, Skill>(20, BladeGirlSliding),

                    new KeyValuePair<uint, Skill>(21, new Skill{
                            RecoveryFrames = 30,
                            RecoveryFramesOnBlock = 60,
                            RecoveryFramesOnHit = 60,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk3,
                            SelfNonStockBuff = new BuffConfig {
                                                    OmitGravity = true,
                                                }
                            }
                            .AddHit(SlashNovaStarterBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaRepeatingBullet)
                            .AddHit(SlashNovaEnderBullet)
                    ),

                    new KeyValuePair<uint, Skill>(22, GoblinMelee1PrimerSkill),

                    new KeyValuePair<uint, Skill>(23, new Skill {
                            RecoveryFrames = 27,
                            RecoveryFramesOnBlock = 27,
                            RecoveryFramesOnHit = 27,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 4,
                                StartupInvinsibleFrames = 6,
                                ActiveFrames = 20,
                                HitStunFrames = 22,
                                BlockStunFrames = 9,
                                Damage = 19,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 11,
                                CancellableEdFrame = 26,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 7,
                                CharacterEmitSfxName = "SlashEmitSpd1",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }.UpsertCancelTransit(1, 24)
                        )),

                            new KeyValuePair<uint, Skill>(24, new Skill{
                                    RecoveryFrames = 23,
                                    RecoveryFramesOnBlock = 23,
                                    RecoveryFramesOnHit = 23,
                                    TriggerType = SkillTriggerType.RisingEdge,
                                    BoundChState = Atk2
                                    }
                                    .AddHit(
                                        new BulletConfig {
                                        StartupFrames = 6,
                                        StartupInvinsibleFrames = 5,
                                        ActiveFrames = 18,
                                        HitStunFrames = MAX_INT,
                                        HitInvinsibleFrames = 60,
                                        BlockStunFrames = 18,
                                        Damage = 24,
                                        PushbackVelX = 0,
                                        PushbackVelY = (int)(-2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SelfLockVelX = NO_LOCK_VEL,
                                        SelfLockVelY = NO_LOCK_VEL,
                                        SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                        HitboxOffsetX = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeX = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeY = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SpeciesId = 3,
                                        ExplosionFrames = 25,
                                        BType = BulletType.Melee,
                                        BlowUp = true,
                                        DirX = 1,
                                        DirY = 0,
                                        Hardness = 8,
                                        CharacterEmitSfxName = "SlashEmitSpd2",
                                        ExplosionSfxName="Explosion4",
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }
                        )),

                    new KeyValuePair<uint, Skill>(25, new Skill{
                            RecoveryFrames = 40,
                            RecoveryFramesOnBlock = 40,
                            RecoveryFramesOnHit = 40,
                            MpDelta = 400,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = InAirAtk1
                            }
                            .AddHit(WitchGirlFireballBulletAirHit1)
                    ),

                    new KeyValuePair<uint, Skill>(26, new Skill{
                            RecoveryFrames = 24,
                            RecoveryFramesOnBlock = 24,
                            RecoveryFramesOnHit = 24,
                            MpDelta = 100,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Dashing
                    }
                    .AddHit(
                        new BulletConfig {
                        StartupFrames = 3,
                        StartupInvinsibleFrames = 8,
                        PushbackVelX = NO_LOCK_VEL,
                        PushbackVelY = NO_LOCK_VEL,
                        SelfLockVelX = (int)(+4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        SelfLockVelY = 0,
                        SelfLockVelYWhenFlying = NO_LOCK_VEL,
                        DelaySelfVelToActive = true,
                        BType = BulletType.Melee,
                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                        }
                    )),

                    new KeyValuePair<uint, Skill>(27, IcePillarSkill),
                    new KeyValuePair<uint, Skill>(MagSwordGirlAirSlash.Id, MagSwordGirlAirSlash),
                    new KeyValuePair<uint, Skill>(MagicPistolGround.Id, MagicPistolGround),
                    new KeyValuePair<uint, Skill>(MagicPistolAir.Id, MagicPistolAir),
                    new KeyValuePair<uint, Skill>(31, PurpleArrowPrimarySkill),

                    new KeyValuePair<uint, Skill>(32, FallingPurpleArrowSkill),
                    new KeyValuePair<uint, Skill>(MagicPistolCrouch.Id, MagicPistolCrouch),
                    new KeyValuePair<uint, Skill>(MagSwordGirlSliding.Id, MagSwordGirlSliding),

                    new KeyValuePair<uint, Skill>(HeatBeamSkill.Id, HeatBeamSkill),

                    new KeyValuePair<uint, Skill>(36, WaterballAirSkill),

                    new KeyValuePair<uint, Skill>(37, RidleyMeleeAirSkill1),

                    new KeyValuePair<uint, Skill>(38, RidleyMeleeSkill1),

                    new KeyValuePair<uint, Skill>(39, new Skill{
                            RecoveryFrames = 40,
                            RecoveryFramesOnBlock = 40,
                            RecoveryFramesOnHit = 40,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk3
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 7,
                                ActiveFrames = 22,
                                HitStunFrames = 23,
                                BlockStunFrames = 9,
                                Damage = 22,
                                PushbackVelX = (int)(0.1f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(15*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 1,
                                ExplosionSpeciesId = 1,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                RemainsUponHit = true,
                                CancellableStFrame = 16,
                                CancellableEdFrame = 36,
                                CharacterEmitSfxName = "SlashEmitSpd2",
                                ExplosionSfxName = "Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }.UpsertCancelTransit(PATTERN_B, 40)
                )),

                    new KeyValuePair<uint, Skill>(40, new Skill{
                            RecoveryFrames = 50,
                            RecoveryFramesOnBlock = 50,
                            RecoveryFramesOnHit = 50,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk4
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 8,
                                StartupInvinsibleFrames = 4,
                                ActiveFrames = 18,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = 0,
                                PushbackVelY = 0,
                                SelfLockVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = (int)(+4.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(22*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(13*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = false,
                                SpeciesId = 1,
                                ExplosionSpeciesId = 1,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                MhType = MultiHitType.FromEmission,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 8,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName = "Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                )
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 27,
                                ActiveFrames = 18,
                                HitStunFrames = MAX_INT,
                                HitInvinsibleFrames = 60,
                                BlockStunFrames = 9,
                                Damage = 30,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(-4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = 0,
                                SelfLockVelY = (int)(-6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(-13*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(52*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(78*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 3,
                                ExplosionSpeciesId = 3,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 8,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName = "Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                            )
                ),

                    new KeyValuePair<uint, Skill>(41, BoarWarriorMelee1PrimerSkill),

                    new KeyValuePair<uint, Skill>(42, RidleyMeleeAirSkill1),

                    new KeyValuePair<uint, Skill>(43, SwordManMelee1PrimerSkillAir),

                    new KeyValuePair<uint, Skill>(44, FireSwordManMelee1PrimerSkillAir),

                    new KeyValuePair<uint, Skill>(BoarMelee1PrimerSkill.Id, BoarMelee1PrimerSkill),

                    new KeyValuePair<uint, Skill>(BoarImpact.Id, BoarImpact),

                    new KeyValuePair<uint, Skill>(47, WaterballGroundSkill1),
                    new KeyValuePair<uint, Skill>(48, WaterballGroundSkill2),
                    new KeyValuePair<uint, Skill>(49, ThunderBoltSkill),

                    new KeyValuePair<uint, Skill>(HeatBeamAirSkill.Id, HeatBeamAirSkill),

                    new KeyValuePair<uint, Skill>(BasicDashing.Id, BasicDashing),

                    new KeyValuePair<uint, Skill>(BasicSliding.Id, BasicSliding),

                    new KeyValuePair<uint, Skill>(54, WitchGirlMeleeAir1),

                    new KeyValuePair<uint, Skill>(55, new Skill {
                        Id = 55,
                            RecoveryFrames = 68,
                            RecoveryFramesOnBlock = 68,
                            RecoveryFramesOnHit = 68,
                            MpDelta = 850,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk6
                    }.AddHit(FirePillarBullet)),

                    new KeyValuePair<uint, Skill>(56, new Skill {
                        Id = 56,
                            RecoveryFrames = 35,
                            RecoveryFramesOnBlock = 35,
                            RecoveryFramesOnHit = 35,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk4
                    }.AddHit(WaterSpikeStarterBullet)),

                    new KeyValuePair<uint, Skill>(57, new Skill {
                        Id = 57,
                            RecoveryFrames = 15,
                            RecoveryFramesOnBlock = 15,
                            RecoveryFramesOnHit = 15,
                            MpDelta = 60,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = BackDashing
                    }
                    .AddHit(
                        new BulletConfig {
                        StartupFrames = 3,
                        StartupInvinsibleFrames = 8,
                        PushbackVelX = NO_LOCK_VEL,
                        PushbackVelY = NO_LOCK_VEL,
                        SelfLockVelX = (int)(-4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        SelfLockVelY = 0,
                        SelfLockVelYWhenFlying = NO_LOCK_VEL,
                        CancellableStFrame = 3,
                        CancellableEdFrame = 16,
                        CancellableByInventorySlotC = true,
                        DelaySelfVelToActive = true,
                        BType = BulletType.Melee,
                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                        }.UpsertCancelTransit(PATTERN_RELEASED_B, 56).UpsertCancelTransit(PATTERN_B, 47).UpsertCancelTransit(PATTERN_DOWN_B, 47).UpsertCancelTransit(PATTERN_UP_B, 47)
                    )),

                    new KeyValuePair<uint, Skill>(58, timedBouncingBomb),
                    new KeyValuePair<uint, Skill>(59, bouncingTouchExplosionBomb),
                    new KeyValuePair<uint, Skill>(60, new Skill {
                            RecoveryFrames = 20,
                            RecoveryFramesOnBlock = 20,
                            RecoveryFramesOnHit = 20,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = OnWallAtk1
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 5,
                                ActiveFrames = 14,
                                HitStunFrames = 15,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(-24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CharacterEmitSfxName="SlashEmitSpd1",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                        )),

                    new KeyValuePair<uint, Skill>(61, RisingPurpleArrowSkill),
                    new KeyValuePair<uint, Skill>(62, PurpleArrowRainSkill),
                    new KeyValuePair<uint, Skill>(63, RisingWaterballPair),
                    new KeyValuePair<uint, Skill>(64, FallingWaterballPair),
                    new KeyValuePair<uint, Skill>(65, GhostHorseSkill),
                    new KeyValuePair<uint, Skill>(MobileThunderBallPrimer.Id, MobileThunderBallPrimer),
                    new KeyValuePair<uint, Skill>(MobileThunderBallPrimerAir.Id, MobileThunderBallPrimerAir),
                    new KeyValuePair<uint, Skill>(HunterPistolWall.Id, HunterPistolWall),
                    new KeyValuePair<uint, Skill>(MobileThunderBallPrimerCrouch.Id, MobileThunderBallPrimerCrouch),
                    new KeyValuePair<uint, Skill>(MobileThunderBallPrimerWall.Id, MobileThunderBallPrimerWall),

                    new KeyValuePair<uint, Skill>(HunterPistol.Id, HunterPistol),

                    new KeyValuePair<uint, Skill>(HunterPistolAir.Id, HunterPistolAir),

                    new KeyValuePair<uint, Skill>(HunterDragonPunch.Id, HunterDragonPunch),
                    new KeyValuePair<uint, Skill>(HunterAirSlash.Id, HunterAirSlash),
                    new KeyValuePair<uint, Skill>(HunterDashSlashSKill.Id, HunterDashSlashSKill),
                    new KeyValuePair<uint, Skill>(BladeGirlDiverImpact.Id, BladeGirlDiverImpact),
                    new KeyValuePair<uint, Skill>(HunterDashing.Id, HunterDashing),

                    new KeyValuePair<uint, Skill>(79, SpinLightBeamSkill),
                    new KeyValuePair<uint, Skill>(80, SpinLightBeamAirSkill),
                    new KeyValuePair<uint, Skill>(81, FireSwordManFireBreathSkill),
                    new KeyValuePair<uint, Skill>(DemonFireBreathSkill.Id, DemonFireBreathSkill),
                    new KeyValuePair<uint, Skill>(DemonDiverImpact.Id, DemonDiverImpact),
                    new KeyValuePair<uint, Skill>(BladeGirlSlidingSlashSkill.Id, BladeGirlSlidingSlashSkill),
                    new KeyValuePair<uint, Skill>(85, DroppingFireballSkill),
                    new KeyValuePair<uint, Skill>(HunterDragonPunchSecondarySkill.Id, HunterDragonPunchSecondarySkill),
                    new KeyValuePair<uint, Skill>(MagicCannonGround.Id, MagicCannonGround),
                    new KeyValuePair<uint, Skill>(MagicCannonAir.Id, MagicCannonAir),
                    new KeyValuePair<uint, Skill>(MagicCannonCrouch.Id, MagicCannonCrouch),

                    new KeyValuePair<uint, Skill>(90, HeavyGuardMelee1PrimerSkill),
                    new KeyValuePair<uint, Skill>(91, HeavyGuardDashSkill),
                    new KeyValuePair<uint, Skill>(92, LightGuardMelee1PrimerSkill),
                    new KeyValuePair<uint, Skill>(93, LightGuardDashSkill),
                    new KeyValuePair<uint, Skill>(BladeGirlCrouchSlash.Id, BladeGirlCrouchSlash),

                    new KeyValuePair<uint, Skill>(95, RiderGuardMelee1PrimerSkill),
                    new KeyValuePair<uint, Skill>(96, RiderGuardDashSkill),
                    new KeyValuePair<uint, Skill>(97, StoneSwordSkill),
                    new KeyValuePair<uint, Skill>(98, StoneRollSkill),

                    new KeyValuePair<uint, Skill>(99, PurpleArrowPrimarySkillAir),
                    new KeyValuePair<uint, Skill>(100, FallingPurpleArrowSkillAir),
                    new KeyValuePair<uint, Skill>(101, RisingPurpleArrowSkillAir),
                    new KeyValuePair<uint, Skill>(102, StoneCrusherSkill),
                    new KeyValuePair<uint, Skill>(103, StoneDropperSkill),
                    new KeyValuePair<uint, Skill>(104, JumperImpact1Skill),

                    new KeyValuePair<uint, Skill>(105, new Skill {
                        Id = 105,
                            // [WARNING] The relationship between "RecoveryFrames", "StartupFrames", "HitStunFrames" and "PushbackVelX" makes sure that a MeleeBullet is counterable!
                            RecoveryFrames = 31,
                            RecoveryFramesOnBlock = 31,
                            RecoveryFramesOnHit = 31,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(SpearWomanMeleePrimerBl)),

                        new KeyValuePair<uint, Skill>(106, new Skill{
                            Id = 106,
                                RecoveryFrames = 46,
                                RecoveryFramesOnBlock = 46,
                                RecoveryFramesOnHit = 46,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Atk2
                                }
                                .AddHit(
                                    new BulletConfig {
                                    StartupFrames = 24,
                                    StartupInvinsibleFrames = 12,
                                    ActiveFrames = 20,
                                    HitStunFrames = 45,
                                    BlockStunFrames = 9,
                                    Damage = 18,
                                    PushbackVelX = 0,
                                    PushbackVelY = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    SelfLockVelY = NO_LOCK_VEL,
                                    SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                    HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    CancellableStFrame = 33,
                                    CancellableEdFrame = 47,
                                    SpeciesId = 2,
                                    ExplosionSpeciesId = 2,
                                    ExplosionFrames = 25,
                                    BType = BulletType.Melee,
                                    DirX = 1,
                                    DirY = 0,
                                    Hardness = 7,
                                    RemainsUponHit = true,
                                    CharacterEmitSfxName = "SlashEmitSpd2",
                                    ExplosionSfxName="Melee_Explosion2",
                                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                                    }.UpsertCancelTransit(PATTERN_B, 107)
                    )),

                        new KeyValuePair<uint, Skill>(107, new Skill{
                            Id = 107,
                                RecoveryFrames = 78,
                                RecoveryFramesOnBlock = 78,
                                RecoveryFramesOnHit = 78,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Atk3
                                }
                                .AddHit(RepPerforationBl1)
                                .AddHit(RepPerforationBl2)
                                .AddHit(RepPerforationBl3)
                                .AddHit(RepPerforationBl4)
                                .AddHit(RepPerforationBl5)
                                .AddHit(RepPerforationBl6)
                                .AddHit(RepPerforationBl7)
                                .AddHit(RepPerforationBl8)
                                .AddHit(RepPerforationBl9)
                        ),

                    new KeyValuePair<uint, Skill>(108, new Skill {
                        Id = 108,
                            RecoveryFrames = 29,
                            RecoveryFramesOnBlock = 29,
                            RecoveryFramesOnHit = 29,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = InAirAtk1
                            }
                            .AddHit(SpearWomanAirMeleePrimerBl)),

                    new KeyValuePair<uint, Skill>(109, new Skill {
                        Id = 109,
                            RecoveryFrames = 42,
                            RecoveryFramesOnBlock = 42,
                            RecoveryFramesOnHit = 42,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk4
                            }
                            .AddHit(SpearWomanDragonPunchPrimerBl)),

                    new KeyValuePair<uint, Skill>(110, new Skill {
                        Id = 110,
                            RecoveryFrames = 51,
                            RecoveryFramesOnBlock = 51,
                            RecoveryFramesOnHit = 51,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk5
                            }
                            .AddHit(SpearWomanBasicFireballBl)),

                    new KeyValuePair<uint, Skill>(111, new Skill {
                        Id = 111,
                            RecoveryFrames = 26,
                            RecoveryFramesOnBlock = 26,
                            RecoveryFramesOnHit = 26,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(LightSpearWomanMeleePrimerBl)),

                        new KeyValuePair<uint, Skill>(112, new Skill{
                            Id = 112,
                                RecoveryFrames = 44,
                                RecoveryFramesOnBlock = 44,
                                RecoveryFramesOnHit = 44,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Atk2
                                }
                                .AddHit(
                                    new BulletConfig {
                                    StartupFrames = 13,
                                    StartupInvinsibleFrames = 7,
                                    ActiveFrames = 20,
                                    HitStunFrames = 45,
                                    BlockStunFrames = 9,
                                    Damage = 19,
                                    PushbackVelX = (int)(-0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    PushbackVelY = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    SelfLockVelY = NO_LOCK_VEL,
                                    SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                    HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    CancellableStFrame = 25,
                                    CancellableEdFrame = 45,
                                    SpeciesId = 2,
                                    ExplosionSpeciesId = 2,
                                    ExplosionFrames = 25,
                                    BType = BulletType.Melee,
                                    DirX = 1,
                                    DirY = 0,
                                    Hardness = 7,
                                    RemainsUponHit = true,
                                    ElementalAttrs = ELE_THUNDER,
                                    CharacterEmitSfxName = "SlashEmitSpd2",
                                    ExplosionSfxName="Melee_Explosion2",
                                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                                    }.UpsertCancelTransit(PATTERN_B, 129)
                    )),

                    new KeyValuePair<uint, Skill>(113, new Skill{
                          Id = 113,
                            RecoveryFrames = 52,
                            RecoveryFramesOnBlock = 52,
                            RecoveryFramesOnHit = 52,
                            TriggerType = SkillTriggerType.RisingEdge,
                            MpDelta = 300,
                            BoundChState = Atk3
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 8,
                                StartupInvinsibleFrames = 4,
                                ActiveFrames = 13,
                                HitStunFrames = 35,
                                BlockStunFrames = 9,
                                Damage = 18,
                                PushbackVelX = (int)(-0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = 0,
                                SelfLockVelY = 0,
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(13*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(52*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionSpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                MhType = MultiHitType.FromEmission,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 9,
                                ElementalAttrs = ELE_THUNDER,
                                RemainsUponHit = true,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName = "Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                    )
                        .AddHit(
                                new BulletConfig {
                                StartupFrames = 21,
                                StartupInvinsibleFrames = 4,
                                ActiveFrames = 15,
                                HitStunFrames = 75,
                                BlockStunFrames = 9,
                                Damage = 20,
                                BlowUp = true,
                                PushbackVelX = (int)(1f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = 0,
                                SelfLockVelY = 0,
                                SelfLockVelYWhenFlying = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(52*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionSpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                MhType = MultiHitType.FromEmission,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 9,
                                ElementalAttrs = ELE_THUNDER,
                                RemainsUponHit = true,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName = "Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                    )
                        ),

                    new KeyValuePair<uint, Skill>(114, new Skill {
                            RecoveryFrames = 29,
                            RecoveryFramesOnBlock = 29,
                            RecoveryFramesOnHit = 29,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = InAirAtk1
                            }
                            .AddHit(LightSpearWomanAirMeleePrimerBl)),

                    new KeyValuePair<uint, Skill>(115, new Skill {
                            RecoveryFrames = 40,
                            RecoveryFramesOnBlock = 40,
                            RecoveryFramesOnHit = 40,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1,
                            MpDelta = 500,
                            }
                            .AddHit(WandWitchGirlBasicBulletHit1)
                            .AddHit(WandWitchGirlBasicBulletHit2)
                    ),

                    new KeyValuePair<uint, Skill>(116, WandWitchHeatBeamSkill),
                    new KeyValuePair<uint, Skill>(goblinBomb.Id, goblinBomb),
                    new KeyValuePair<uint, Skill>(118, SwordManMelee1GroundHit2),
                    new KeyValuePair<uint, Skill>(119, SwordManMelee1GroundHit3),

                    new KeyValuePair<uint, Skill>(120, FireSwordManMelee1GroundHit2),
                    new KeyValuePair<uint, Skill>(121, FireSwordManMelee1GroundHit3),
                    new KeyValuePair<uint, Skill>(122, DarkTowerPrimerSkill),
                    new KeyValuePair<uint, Skill>(123, DarkTowerUpperSkill),
                    new KeyValuePair<uint, Skill>(124, DarkTowerLowerSkill),

                    new KeyValuePair<uint, Skill>(125, new Skill {
                        Id = 125,
                            RecoveryFrames = 60,
                            RecoveryFramesOnBlock = 60,
                            RecoveryFramesOnHit = 60,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk4
                            }
                            .AddHit(WindShaverPrimerBl)
                            .AddHit(WindShaverHit2)
                            .AddHit(WindShaverHit3)
                            .AddHit(WindShaverHit4)
                            .AddHit(WindShaverHit5)
                            .AddHit(WindShaverEnd)),

                    new KeyValuePair<uint, Skill>(126, new Skill {
                        Id = 126,
                            RecoveryFrames = 51,
                            RecoveryFramesOnBlock = 51,
                            RecoveryFramesOnHit = 51,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk5,
                            MpDelta = 4*BATTLE_DYNAMICS_FPS,
                            }
                            .AddHit(LightSpearWomanBasicFireballBl)),

                    new KeyValuePair<uint, Skill>(127, new Skill {
                        Id = 127,
                            RecoveryFrames = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2), 
                            RecoveryFramesOnBlock = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
                            RecoveryFramesOnHit = (BasicSlidingHit2.StartupFrames + BasicSlidingHit2.ActiveFrames + 2),
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Sliding
                            }
                            .AddHit(new BulletConfig(BasicSlidingHit1).UpsertCancelTransit(PATTERN_B, 111).UpsertCancelTransit(PATTERN_DOWN_B, 126))
                            .AddHit(new BulletConfig(BasicSlidingHit2).UpsertCancelTransit(PATTERN_B, 111).UpsertCancelTransit(PATTERN_DOWN_B, 126))
                    ),

                    new KeyValuePair<uint, Skill>(128, new Skill {
                        Id = 128,
                            RecoveryFrames = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
                            RecoveryFramesOnBlock = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
                            RecoveryFramesOnHit = (BasicDashingHit2.StartupFrames + BasicDashingHit2.ActiveFrames + 2),
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Dashing
                            }
                            .AddHit(new BulletConfig(BasicDashingHit1).UpsertCancelTransit(PATTERN_B, 111))
                            .AddHit(new BulletConfig(BasicDashingHit2).UpsertCancelTransit(PATTERN_B, 111))
                    ),

                    new KeyValuePair<uint, Skill>(129, new Skill{
                            RecoveryFrames = 78,
                            RecoveryFramesOnBlock = 78,
                            RecoveryFramesOnHit = 78,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk6
                            }
                            .AddHit(LightRepPerforationBl1)
                            .AddHit(LightRepPerforationBl2)
                            .AddHit(LightRepPerforationBl3)
                            .AddHit(LightRepPerforationBl4)
                            .AddHit(LightRepPerforationBl5)
                            .AddHit(LightRepPerforationBl6)
                            .AddHit(LightRepPerforationBl7)
                            .AddHit(LightRepPerforationBl8)
                            .AddHit(LightRepPerforationBl9)
                            ),

                    new KeyValuePair<uint,Skill>(SmallBallEmitterBeamSkill.Id, SmallBallEmitterBeamSkill),
                    new KeyValuePair<uint,Skill>(HunterBackDashing.Id, HunterBackDashing),
                    new KeyValuePair<uint,Skill>(HunterSliding.Id, HunterSliding),
                    new KeyValuePair<uint, Skill>(HunterPistolCrouch.Id, HunterPistolCrouch),
                    new KeyValuePair<uint, Skill>(BladeGirlAirSlash1.Id, BladeGirlAirSlash1),
                    new KeyValuePair<uint, Skill>(BladeGirlAirSlash2.Id, BladeGirlAirSlash2),
                    new KeyValuePair<uint, Skill>(BladeGirlDragonPunch.Id, BladeGirlDragonPunch),
                    new KeyValuePair<uint, Skill>(BatMelee1PrimerSkill.Id, BatMelee1PrimerSkill),
                    new KeyValuePair<uint, Skill>(BasicHpHealer.Id, BasicHpHealer),
                    new KeyValuePair<uint, Skill>(AngelBasicAtk.Id, AngelBasicAtk),
                    new KeyValuePair<uint, Skill>(AngelBackDashing.Id, AngelBackDashing),
                    new KeyValuePair<uint, Skill>(AngelDashing.Id, AngelDashing),
                    new KeyValuePair<uint, Skill>(DrakePrimerFireball.Id, DrakePrimerFireball),
                    new KeyValuePair<uint, Skill>(DrakePrimerAirFireball.Id, DrakePrimerAirFireball),
                }
        );
        
        public static (Skill?, BulletConfig?) FindBulletConfig(uint skillId, int skillHit) {
            if (NO_SKILL == skillId) return (null, null);
            if (NO_SKILL_HIT == skillHit) return (null, null);
            if (!skills.ContainsKey(skillId)) return (null, null);
            var skill = skills[skillId];
            if (skillHit > skill.Hits.Count) return (null, null); 
            return (skill, skill.Hits[skillHit-1]);
        }

        public static uint FindSkillId(int patternId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, uint speciesId, bool slotUsed, uint slotLockedSkillId, ILoggerBridge logger) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            if (Parried == currCharacterDownsync.CharacterState) {
                notRecovered = (currCharacterDownsync.FramesInChState >= PARRIED_FRAMES_TO_START_CANCELLABLE);
            }
            switch (speciesId) {
                case SPECIES_BLADEGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_DOWN_B: // Including "PATTERN_DOWN_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    if (OnWallIdle1 != currCharacterDownsync.CharacterState) {
                                        if (PATTERN_DOWN_B == patternId && (InAirIdle1ByJump == currCharacterDownsync.CharacterState || InAirIdle1ByWallJump == currCharacterDownsync.CharacterState || InAirIdle2ByJump == currCharacterDownsync.CharacterState || InAirIdle1NoJump == currCharacterDownsync.CharacterState) && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                            return BladeGirlDiverImpact.Id;
                                        } else {
                                            return BladeGirlAirSlash1.Id; // A fallback to "InAirAtk1"
                                        }
                                    } else {
                                        return 60; // A fallback to "OnWallAtk1"
                                    }
                                } else {
                                    if (isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                        return 94;
                                    } else {
                                        return 1;
                                    }
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B: // Including "PATTERN_DOWN_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return BladeGirlAirSlash1.Id; // A fallback to "InAirAtk1" 
                                } else {
                                    return BladeGirlDragonPunch.Id;
                                }
                            } else {
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                // Air-dash is allowed for this speciesId
                                return BladeGirlDashing.Id;
                            } else if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                return BladeGirlSliding.Id; 
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        case PATTERN_INVENTORY_SLOT_D:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_SWORDMAN_BOSS:
                case SPECIES_SWORDMAN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 43;
                                } else {
                                    return 5;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is prohibited for this character!
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                }
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 12;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                return BasicSliding.Id; 
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_SPEARWOMAN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 108;
                                } else {
                                    return 105;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is prohibited for this character!
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                }
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B:
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                } else {
                                    return 109;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is prohibited for this character!
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                }
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                return 19;
                            } else if (!currCharacterDownsync.InAir) {
                                return 18; 
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_LIGHTSPEARWOMAN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 114;
                                } else {
                                    return 111;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is prohibited for this character!
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                }
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                } else {
                                    return 113;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is prohibited for this character!
                                if (currCharacterDownsync.InAir) {
                                    return NO_SKILL;
                                }
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 126;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                return 128;
                            } else if (!currCharacterDownsync.InAir) {
                                return 127; 
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_WITCHGIRL:
                    switch (patternId) {
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 25; // A fallback to "InAirAtk1"
                                } else {
                                    return 9;
                                }
                            } else {
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    // A fallback to "InAirAtk2"
                                    return 54; 
                                } else {
                                    return 6;
                                }
                            } else {
                                if (currCharacterDownsync.InAir) {
                                    // [WARNING] No air combo for this character!
                                    return NO_SKILL;
                                } else {
                                    // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                    var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                    if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                    if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                    return currBulletConfig.CancelTransit[patternId];
                                }
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    // A fallback to "InAirAtk2"
                                    return 54; 
                                } else {
                                    return 55;
                                }
                            } else {
                                if (currCharacterDownsync.InAir) {
                                    // [WARNING] No air combo for this character!
                                    return NO_SKILL;
                                } else {
                                    // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                    var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                    if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                    if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                    return currBulletConfig.CancelTransit[patternId];
                                }
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_BACK_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_BACK_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                if (PATTERN_FRONT_E == patternId || PATTERN_FRONT_E_HOLD_B == patternId) {
                                    return 26;
                                } else {
                                    return 10;
                                }
                            } else if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState && PATTERN_BACK_E != patternId && PATTERN_BACK_E_HOLD_B != patternId) {
                                return 26;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_WANDWITCHGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                return 115;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_BACK_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_BACK_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                if (PATTERN_FRONT_E == patternId || PATTERN_FRONT_E_HOLD_B == patternId) {
                                    return 26;
                                } else {
                                    return 10;
                                }
                            } else if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState && PATTERN_BACK_E != patternId && PATTERN_BACK_E_HOLD_B != patternId) {
                                return 26;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_FIRESWORDMAN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 44;
                                } else {
                                    return 16;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (Sliding != currCharacterDownsync.CharacterState && isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 17;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 13;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                return BasicDashing.Id;
                            } else if (!currCharacterDownsync.InAir) {
                                return BasicDashing.Id; 
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BRIGHTWITCH:
                    switch (patternId) {
                        case PATTERN_RELEASED_B:
                            if (currCharacterDownsync.InAir) { 
                                return NO_SKILL;
                            }
                            if (!notRecovered) {
                                return 56;
                            } else {
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_B:
                        case PATTERN_UP_B:
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    if (PATTERN_UP_B == patternId) {
                                        return 63;
                                    } else if (PATTERN_DOWN_B == patternId) {
                                        return 64;
                                    } else {
                                        return 36; // fallback
                                    }
                                } else {
                                    if (PATTERN_UP_B == patternId) {
                                        return 63;
                                    } else if (PATTERN_DOWN_B == patternId) {
                                        return 64;
                                    } else {
                                        return 47; // fallback

                                    }
                                }
                            } else {
                                if (currCharacterDownsync.InAir) return NO_SKILL;
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_BACK_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_BACK_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                if (PATTERN_FRONT_E == patternId || PATTERN_FRONT_E_HOLD_B == patternId) {
                                    return 26;
                                } else {
                                    return 57;
                                }
                            } else if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState && PATTERN_BACK_E != patternId && PATTERN_BACK_E_HOLD_B != patternId) {
                                return 26;
                            } else {
                                return NO_SKILL;
                            } 
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        case PATTERN_INVENTORY_SLOT_D:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_MAGSWORDGIRL:
                    switch (patternId) {
                        case PATTERN_RELEASED_B:
                            if (!slotUsed) return NO_SKILL;
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return MagicCannonAir.Id;
                                } else {
                                    if (isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                        return MagicCannonCrouch.Id;
                                    } else {
                                        return MagicCannonGround.Id;
                                    }
                                }
                            } else {
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_B:
                        case PATTERN_UP_B:
                        case PATTERN_DOWN_B:
                            if (!slotUsed) return NO_SKILL;
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return MagicPistolAir.Id;
                                } else {
                                    if (isCrouching(currCharacterDownsync.CharacterState, chConfig) || PATTERN_DOWN_B == patternId) {
                                        return MagicPistolCrouch.Id;
                                    } else {
                                        return MagicPistolGround.Id;
                                    }
                                }
                            } else {
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                return MagSwordGirlSliding.Id;
                            } else if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                // Air-dash is allowed for this speciesId
                                return 28;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        case PATTERN_INVENTORY_SLOT_D:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BOUNTYHUNTER:
                    switch (patternId) {
                        case PATTERN_RELEASED_B:
                            if (!slotUsed) return NO_SKILL;
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    if (OnWallIdle1 == currCharacterDownsync.CharacterState) {
                                        return MobileThunderBallPrimerWall.Id;
                                    } else {
                                        return MobileThunderBallPrimerAir.Id;
                                    }
                                } else {
                                    if (isCrouching(currCharacterDownsync.CharacterState, chConfig)) {
                                        return MobileThunderBallPrimerCrouch.Id;
                                    } else {
                                        return MobileThunderBallPrimer.Id;
                                    }
                                }
                            } else {
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B: // Including "PATTERN_DOWN_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return HunterAirSlash.Id; 
                                } else {
                                    return HunterDragonPunch.Id;
                                }
                            } else {
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_B:
                        case PATTERN_DOWN_B:
                            if (!slotUsed) return NO_SKILL;
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    if (OnWallIdle1 != currCharacterDownsync.CharacterState) {
                                        return HunterPistolAir.Id;
                                    } else {
                                        return HunterPistolWall.Id;
                                    }
                                } else {
                                    if (isCrouching(currCharacterDownsync.CharacterState, chConfig) || PATTERN_DOWN_B == patternId) {
                                        return HunterPistolCrouch.Id;
                                    } else {
                                        return HunterPistol.Id;
                                    }
                                }
                            } else {
                                var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) {
                                // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                // Air-dash is allowed for this speciesId
                                return HunterDashing.Id;
                            } else if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                if (PATTERN_FRONT_E == patternId || PATTERN_FRONT_E_HOLD_B == patternId) {
                                    return HunterSliding.Id; 
                                } else {
                                    return HunterBackDashing.Id;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        case PATTERN_INVENTORY_SLOT_D:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_DEMON_FIRE_SLIME:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                return 14;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 82;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 83;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_GOBLIN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 22;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BOMBERGOBLIN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 22;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_SKELEARCHER:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 99;
                                } else {
                                    return 31;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 100;
                                } else {
                                    return 32;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 101;
                                } else {
                                    return 61;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                return BasicSliding.Id; 
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BAT:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_DOWN_B:
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                return BatMelee1PrimerSkill.Id;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_FIREBAT:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                return BatMelee1PrimerSkill.Id;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                return 85;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_RIDLEYDRAKE:
                    switch (patternId) {
                        case PATTERN_DOWN_B:
                            if (notRecovered) return NO_SKILL;
                            if (currCharacterDownsync.InAir) {
                                return DrakePrimerAirFireball.Id;
                            } else {
                                return DrakePrimerFireball.Id;
                            }
                        case PATTERN_B:
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    // A fallback to "InAirAtk2"
                                    if (currCharacterDownsync.OmitGravity) {
                                        return 42;
                                    } else {    
                                        return 37; 
                                    }
                                } else {
                                    return 38;
                                }
                            } else {
                                if (currCharacterDownsync.InAir) {
                                    // [WARNING] No air combo for this character!
                                    return NO_SKILL;
                                } else {
                                    // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                    var (currSkillConfig, currBulletConfig) = FindBulletConfig(currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                                    if (null == currSkillConfig || null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;
                                    if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                    return currBulletConfig.CancelTransit[patternId];
                                }
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            if (!currCharacterDownsync.InAir) {
                                return BasicDashing.Id;
                            } else if (currCharacterDownsync.InAir && ((0 < currCharacterDownsync.RemainingAirDashQuota && IN_AIR_DASH_GRACE_PERIOD_RDF_CNT < currCharacterDownsync.FramesInChState) || (currCharacterDownsync.OmitGravity))) {
                                return BasicDashing.Id;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BOARWARRIOR:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 41;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BOAR:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                return BoarMelee1PrimerSkill.Id;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            else return BoarImpact.Id;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_HEAVYGUARD_RED:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                return 90;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            else return 91;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_LIGHTGUARD_RED:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                return 92;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            else return 93;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_RIDERGUARD_RED:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                return 95;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_FRONT_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            else return 96;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_STONE_GOLEM:
                    switch (patternId) {
                        case PATTERN_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            return 97;
                        case PATTERN_DOWN_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            return 103;
                        case PATTERN_UP_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            else return 98;
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_DARKBEAMTOWER:
                    switch (patternId) {
                        case PATTERN_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            return 122;
                        case PATTERN_UP_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            else return 123;
                        case PATTERN_DOWN_B:
                            if (notRecovered || currCharacterDownsync.InAir) return NO_SKILL;
                            return 124;
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_ANGEL:
                    switch (patternId) {
                        case PATTERN_B:
                            if (notRecovered) return NO_SKILL;
                            return BasicHpHealer.Id;
                        case PATTERN_FRONT_E:
                        case PATTERN_FRONT_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            return AngelDashing.Id;
                        case PATTERN_E:
                        case PATTERN_DOWN_E:
                        case PATTERN_UP_E:
                        case PATTERN_E_HOLD_B:
                        case PATTERN_DOWN_E_HOLD_B:
                        case PATTERN_UP_E_HOLD_B:
                            if (notRecovered) return NO_SKILL;
                            return AngelBackDashing.Id;
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!slotUsed) return NO_SKILL;
                            return slotLockedSkillId;
                        default:
                            return NO_SKILL;
                    }
                default:
                    return NO_SKILL;
            }
        }
    }
}
