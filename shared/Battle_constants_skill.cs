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

        public const int PATTERN_INVENTORY_SLOT_C = 1024;
        public const int PATTERN_INVENTORY_SLOT_D = 1025;

        public static BulletConfig SwordManMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 16,
            HitStunFrames = 4,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 9,
            Damage = 11,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
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
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Melee_Explosion2",
            ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig SwordManDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 13,
            ActiveFrames = 20,
            HitStunFrames = MAX_INT,
            BlockStunFrames = 9,
            Damage = 11,
            PushbackVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            DirX = 1,
            DirY = 0,
            ExplosionFrames = 25,
            Hardness = 5,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig FireSwordManMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 16,
            HitStunFrames = 4,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 9,
            Damage = 13,
            PushbackVelX = (int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
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
            CharacterEmitSfxName = "SlashEmitSpd2",
            ExplosionSfxName = "Explosion4",
            ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig FireSwordManDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 13,
            ActiveFrames = 20,
            HitStunFrames = MAX_INT,
            BlockStunFrames = 9,
            Damage = 13,
            PushbackVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(3.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Explosion4",
            ActiveVfxSpeciesId = VfxFireSlashActive.SpeciesId,
            ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill FireSwordManFireballSkill = new Skill {
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            MpDelta = 600,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk4
        }
        .AddHit(
                new BulletConfig {
                    StartupFrames = 21,
                    ActiveFrames = 360,
                    HitStunFrames = 3,
                    HitInvinsibleFrames = 8,
                    BlockStunFrames = 3,
                    Damage = 11,
                    PushbackVelX = (int)(.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = NO_LOCK_VEL,
                    SelfLockVelY = NO_LOCK_VEL,
                    HitboxOffsetX = (int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(9 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    CancellableStFrame = 0,
                    CancellableEdFrame = 0,
                    BlowUp = false,
                    SpeciesId = 4,
                    Speed = (int)(4.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    DirX = 1,
                    DirY = 0,
                    ExplosionFrames = 20,
                    Hardness = 4,
                    BType = BulletType.Fireball,
                    CharacterEmitSfxName = "FlameEmit1",
                    ExplosionSfxName = "Explosion4",
                    ActiveVfxSpeciesId = VfxFirePointLightActive.SpeciesId,
                    ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                }
        );

        public static BulletConfig BullWarriorMelee1PrimaryBullet = new BulletConfig {
            StartupFrames = 18,
            ActiveFrames = 9,
            HitStunFrames = 9,
            BlockStunFrames = 3,
            Damage = 10,
            PushbackVelX = 0, // Freeze the target for visual emphasis
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(64 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(80 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(90 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = false,
            SpeciesId = 1,
            MhType = MultiHitType.FromEmission,
            ExplosionFrames = 20,
            BType = BulletType.Melee,
            DirX = 1,
            Hardness = 6,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static BulletConfig BullWarriorFireballPivotBullet = new BulletConfig {
            StartupFrames = 33,
            ActiveFrames = 360,
            HitStunFrames = 30,
            BlockStunFrames = 9,
            Damage = 12,
            PushbackVelX = (int)(0.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 4,
            Speed = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 30,
            BType = BulletType.Fireball,
            FireballEmitSfxName = "FlameEmit1",
            ExplosionSfxName = "Explosion4",
            ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
            SimultaneousMultiHitCnt = 2
        };

        public static Skill BullWarriorFireballSkill = new Skill {
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 50,
            RecoveryFramesOnHit = 50,
            MpDelta = 300,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5
        }
        .AddHit(
                BullWarriorFireballPivotBullet
               )
            .AddHit(
                    new BulletConfig(BullWarriorFireballPivotBullet)
                    .SetDir(1, -1)
                    .SetRotateAlongVelocity(true)
                    .SetHitboxOffsets((int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                    .SetPushbacks((int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                    .SetSimultaneousMultiHitCnt(1u)
                   )
            .AddHit(
                    new BulletConfig(BullWarriorFireballPivotBullet)
                    .SetDir(1, +1)
                    .SetRotateAlongVelocity(true)
                    .SetHitboxOffsets((int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(28 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                    .SetPushbacks((int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(+0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                    .SetSimultaneousMultiHitCnt(0u)
                   );

        private static BulletConfig PistolBulletAir = new BulletConfig {
            StartupFrames = 2,
            ActiveFrames = 180,
            HitStunFrames = 1,
            BlockStunFrames = 2,
            Damage = 18,
            PushbackVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(15 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 8,
            Speed = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 10,
            BType = BulletType.Fireball,
            ExplosionSpeciesId = 4,
            CharacterEmitSfxName = "Fireball8",
            ExplosionSfxName = "Explosion8",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig MagicPistolBulletAir = new BulletConfig {
            StartupFrames = 2,
            ActiveFrames = 180,
            HitStunFrames = 2,
            BlockStunFrames = 2,
            Damage = 15,
            PushbackVelX = (int)(0.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(15 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(2 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionSpeciesId = 1,
            Speed = (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 4,
            ExplosionFrames = 30,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig SlashNovaRepeatingBullet = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 600,
            HitStunFrames = 14,
            BlockStunFrames = 9,
            Damage = 3,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 9,
            ExplosionSpeciesId = 9,
            ExplosionFrames = 25,
            Speed = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeedIfNotHit = (int)(3 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActual,
            MhVanishOnMeleeHit = true,
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig SlashNovaStarterBullet = new BulletConfig(SlashNovaRepeatingBullet).SetStartupFrames(10).SetSpeed(SlashNovaRepeatingBullet.SpeedIfNotHit);

        private static BulletConfig SlashNovaEnderBullet = new BulletConfig(SlashNovaRepeatingBullet).SetStartupFrames(9).SetMhType(MultiHitType.None).SetSpeedIfNotHit(0).SetSpeed(SlashNovaRepeatingBullet.SpeedIfNotHit).SetPushbacks(
            (int)(0.3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // The last hit has some pushback 
            NO_LOCK_VEL
        );

        private static BulletConfig IcePillarStarterBullet = new BulletConfig {
            StartupFrames = 35,
            ActiveFrames = 500,
            HitStunFrames = 20,
            BlockStunFrames = 60,
            Damage = 5,
            PushbackVelX = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(-8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // [WARNING] Such that it can start on slope!
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(52 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 3,
            ExplosionSpeciesId = 13,
            ExplosionFrames = 25,
            Speed = (int)(3 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            DownSlopePrimerVelY = (int)(-1.6f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // A bullet is generally faster than a character, make sure that the downslope speed is large enough!
            BType = BulletType.GroundWave,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Explosion2",
            MhType = MultiHitType.FromPrevHitActual,
            MhVanishOnMeleeHit = false, // Makes it more powerful on ground than the SlashNova
            RemainsUponHit = true,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX, 
            BuffConfig = ShortFreezer,
        };

        private static BulletConfig IcePillarRepeatingBullet = new BulletConfig {
            StartupFrames = 0,
            ActiveFrames = 60,
            HitStunFrames = 60,
            BlockStunFrames = 60,
            Damage = 30,
            // No pushbacks
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxSizeX = (int)(48 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(52 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 13,
            ExplosionFrames = 25,
            Speed = 0,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
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
            BoundChState = Atk5
        }
        .AddHit(IcePillarStarterBullet)
        .AddHit(IcePillarRepeatingBullet);

        private static BulletConfig PistolBulletGround = new BulletConfig(PistolBulletAir).SetAllowsWalking(true).SetAllowsCrouching(true);
        private static BulletConfig MagicPistolBulletGround = new BulletConfig(MagicPistolBulletAir).SetAllowsWalking(true).SetAllowsCrouching(true);

        private static BulletConfig PurpleArrowBullet = new BulletConfig {
            StartupFrames = 12,
            ActiveFrames = 180,
            HitStunFrames = 10,
            BlockStunFrames = 10,
            Damage = 15,
            PushbackVelX = (int)(0.05f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(15 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 11,
            Speed = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 2,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 20,
            ExplosionSpeciesId = 11,
            BType = BulletType.Fireball,
            CharacterEmitSfxName = "Fireball8",
            ExplosionSfxName = "Explosion2",
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig PurpleArrowRainBullet1 = new BulletConfig(PurpleArrowBullet)
                                                                .SetSpeed((int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetHitboxOffsets((int)(22 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(6 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSimultaneousMultiHitCnt(2u)
                                                                .SetDir(+2, +1)
                                                                .SetTakesGravity(true)
                                                                .SetRotateAlongVelocity(true);
        private static BulletConfig PurpleArrowRainBullet2 = new BulletConfig(PurpleArrowRainBullet1)
                                                                .SetSpeed((int)(13 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSimultaneousMultiHitCnt(1u)
                                                                .SetDir(+1, +1)
                                                                .SetHitboxOffsets((int)(20 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));
        private static BulletConfig PurpleArrowRainBullet3 = new BulletConfig(PurpleArrowRainBullet1)
                                                                .SetSpeed((int)(15 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                                                .SetSimultaneousMultiHitCnt(0u)
                                                                .SetDir(+1, +2)
                                                                .SetHitboxOffsets((int)(18 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(10 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO));

        public static BulletConfig BladeGirlDragonPunchPrimerBullet = new BulletConfig {
            StartupFrames = 4,
            StartupInvinsibleFrames = 2,
            ActiveFrames = 25,
            HitStunFrames = MAX_INT,
            BlockStunFrames = 9,
            Damage = 18,
            PushbackVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SelfLockVelY = (int)(6.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetX = (int)(14 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(24 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(64 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(56 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            BlowUp = true,
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            CharacterEmitSfxName = "SlashEmitSpd3",
            ExplosionSfxName = "Melee_Explosion2",
            ActiveVfxSpeciesId = VfxFireSlashActive.SpeciesId,
            DelaySelfVelToActive = true,
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static Skill BladeGirlDragonPunchSkill = new Skill {
            RecoveryFrames = 30,
            RecoveryFramesOnBlock = 30,
            RecoveryFramesOnHit = 30,
            MpDelta = 0,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5
        }.
            AddHit(BladeGirlDragonPunchPrimerBullet);

        public static BulletConfig WitchGirlFireballBulletHit1 = new BulletConfig {
            StartupFrames = 15,
            ActiveFrames = 600,
            HitStunFrames = 12,
            BlockStunFrames = 9,
            Damage = 25,
            PushbackVelX = (int)(3.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = NO_LOCK_VEL,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(9f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 2,
            Speed = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            ExplosionFrames = 25,
            BType = BulletType.Fireball,
            CharacterEmitSfxName="SlashEmitSpd1",
            ExplosionSfxName="Explosion2",
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        public static BulletConfig GoblinMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 63,
            ActiveFrames = 10,
            HitStunFrames = 6,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 2,
            Damage = 12,
            PushbackVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            PushbackVelY = 0,
            SelfLockVelX = NO_LOCK_VEL,
            SelfLockVelY = NO_LOCK_VEL,
            HitboxOffsetX = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxOffsetY = (int)(8 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeX = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            HitboxSizeY = (int)(36 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            SpeciesId = 1,
            ExplosionFrames = 25,
            BType = BulletType.Melee,
            DirX = 1,
            DirY = 0,
            Hardness = 5,
            CharacterEmitSfxName = "SlashEmitSpd1",
            ExplosionSfxName = "Melee_Explosion1",
            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
        };

        public static ImmutableDictionary<int, Skill> skills = ImmutableDictionary.Create<int, Skill>().AddRange(
                new[]
                {
                new KeyValuePair<int, Skill>(1, new Skill {
                        // [WARNING] The relationship between "RecoveryFrames", "StartupFrames", "HitStunFrames" and "PushbackVelX" makes sure that a MeleeBullet is counterable!
                        RecoveryFrames = 26,
                        RecoveryFramesOnBlock = 26,
                        RecoveryFramesOnHit = 26,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                        }
                        .AddHit(
                            new BulletConfig {
                            StartupFrames = 3,
                            StartupInvinsibleFrames = 1,
                            ActiveFrames = 15,
                            HitStunFrames = 22,
                            BlockStunFrames = 8,
                            Damage = 7,
                            PushbackVelX = 0,
                            PushbackVelY = NO_LOCK_VEL,
                            SelfLockVelX = NO_LOCK_VEL,
                            SelfLockVelY = NO_LOCK_VEL,
                            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
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
                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }.UpsertCancelTransit(PATTERN_B, 2).UpsertCancelTransit(PATTERN_DOWN_B, 2)
                )),

                    new KeyValuePair<int, Skill>(2, new Skill{
                            RecoveryFrames = 24,
                            RecoveryFramesOnBlock = 24,
                            RecoveryFramesOnHit = 24,
                            MpDelta = 0,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk2
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 3,
                                StartupInvinsibleFrames = 2,
                                ActiveFrames = 13,
                                HitStunFrames = 22,
                                BlockStunFrames = 9,
                                Damage = 8,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 8,
                                CancellableEdFrame = 24,
                                SpeciesId = 2,
                                ExplosionSpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 6,
                                CharacterEmitSfxName = "SlashEmitSpd2",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }.UpsertCancelTransit(PATTERN_B, 3).UpsertCancelTransit(PATTERN_DOWN_B, 3).UpsertCancelTransit(PATTERN_UP_B, 259)
                )),

                    new KeyValuePair<int, Skill>(3, new Skill{
                            RecoveryFrames = 39,
                            RecoveryFramesOnBlock = 39,
                            RecoveryFramesOnHit = 39,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk3
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 5,
                                StartupInvinsibleFrames = 4,
                                ActiveFrames = 8,
                                HitStunFrames = 38,
                                BlockStunFrames = 5,
                                Damage = 10,
                                PushbackVelX = (int)(-0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(-1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionSpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 7,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName="Melee_Explosion2",
                                ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                                MhType = MultiHitType.FromEmission,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                )
                    .AddHit(
                            new BulletConfig {
                            StartupFrames = 14,
                            ActiveFrames = 6,
                            HitStunFrames = 10,
                            BlockStunFrames = 10,
                            Damage = 10,
                            PushbackVelX = (int)(2.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            PushbackVelY = (int)(-1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SelfLockVelX = 0,
                            SelfLockVelY = NO_LOCK_VEL,
                            HitboxOffsetX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SpeciesId = 2,
                            ExplosionSpeciesId = 2,
                            ExplosionFrames = 25,
                            BType = BulletType.Melee,
                            DirX = 1,
                            DirY = 0,
                            Hardness = 7,
                            ExplosionSfxName="Melee_Explosion2",
                            MhType = MultiHitType.FromEmission,
                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                )
                    .AddHit(
                            new BulletConfig {
                            StartupFrames = 21,
                            ActiveFrames = 2,
                            HitStunFrames = 10,
                            BlockStunFrames = 10,
                            Damage = 8,
                            PushbackVelX = (int)(-0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            PushbackVelY = (int)(-1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SelfLockVelX = NO_LOCK_VEL,
                            SelfLockVelY = NO_LOCK_VEL,
                            HitboxOffsetX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SpeciesId = 2,
                            ExplosionSpeciesId = 2,
                            ExplosionFrames = 25,
                            BType = BulletType.Melee,
                            DirX = 1,
                            DirY = 0,
                            Hardness = 7,
                            ExplosionSfxName="Melee_Explosion2",
                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                )
                    ),

                    new KeyValuePair<int, Skill>(4, new Skill{
                            RecoveryFrames = 79,
                            RecoveryFramesOnBlock = 79,
                            RecoveryFramesOnHit = 79,
                            MpDelta = 0,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk4
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 9,
                                StartupInvinsibleFrames = 7,
                                ActiveFrames = 10,
                                HitStunFrames = 10,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(3.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionSpeciesId = 2,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionFrames = 30,
                                BType = BulletType.Melee,
                                CharacterEmitSfxName="SlashEmitSpd3",
                                ActiveVfxSpeciesId = VfxXform.SpeciesId,
                                ExplosionSfxName="Melee_Explosion2",
                                MhType = MultiHitType.FromEmission,
                                CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                                }
                )
                .AddHit(
                    new BulletConfig {
                    StartupFrames = 20,
                    ActiveFrames = 9,
                    HitStunFrames = 11,
                    BlockStunFrames = 9,
                    Damage = 12,
                    PushbackVelX = NO_LOCK_VEL,
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = (int)(2.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelY = NO_LOCK_VEL,
                    HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    BlowUp = false,
                    SpeciesId = 2,
                    ExplosionSpeciesId = 2,
                    DirX = 1,
                    DirY = 0,
                    Hardness = 6,
                    ExplosionFrames = 30,
                    BType = BulletType.Melee,
                    CharacterEmitSfxName="SlashEmitSpd3",
                    ExplosionSfxName="Melee_Explosion2",
                    MhType = MultiHitType.FromEmission,
                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                    }
                )
                .AddHit(
                    new BulletConfig {
                    StartupFrames = 30,
                    ActiveFrames = 9,
                    HitStunFrames = 12,
                    BlockStunFrames = 9,
                    Damage = 8,
                    PushbackVelX = NO_LOCK_VEL,
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = (int)(2.0f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelY = NO_LOCK_VEL,
                    HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    BlowUp = false,
                    SpeciesId = 2,
                    ExplosionSpeciesId = 2,
                    DirX = 1,
                    DirY = 0,
                    Hardness = 6,
                    ExplosionFrames = 30,
                    BType = BulletType.Melee,
                    CharacterEmitSfxName="SlashEmitSpd3",
                    ExplosionSfxName="Melee_Explosion2",
                    MhType = MultiHitType.FromEmission,
                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                    }
                )
                .AddHit(
                    new BulletConfig {
                    StartupFrames = 40,
                    ActiveFrames = 9,
                    HitStunFrames = 12,
                    BlockStunFrames = 9,
                    Damage = 8,
                    PushbackVelX = NO_LOCK_VEL,
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = (int)(2.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelY = NO_LOCK_VEL,
                    HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    BlowUp = false,
                    SpeciesId = 2,
                    ExplosionSpeciesId = 2,
                    DirX = 1,
                    DirY = 0,
                    Hardness = 6,
                    ExplosionFrames = 30,
                    BType = BulletType.Melee,
                    OmitSoftPushback = true,
                    CharacterEmitSfxName="SlashEmitSpd3",
                    ExplosionSfxName="Melee_Explosion2",
                    MhType = MultiHitType.FromEmission,
                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                    }
                )
                .AddHit(
                    new BulletConfig {
                    StartupFrames = 50,
                    ActiveFrames = 9,
                    HitStunFrames = 12,
                    BlockStunFrames = 9,
                    Damage = 8,
                    PushbackVelX = NO_LOCK_VEL,
                    PushbackVelY = NO_LOCK_VEL,
                    SelfLockVelX = (int)(2.7f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    SelfLockVelY = NO_LOCK_VEL,
                    HitboxOffsetX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                    BlowUp = true,
                    SpeciesId = 2,
                    ExplosionSpeciesId = 2,
                    DirX = 1,
                    DirY = 0,
                    Hardness = 6,
                    ExplosionFrames = 30,
                    BType = BulletType.Melee,
                    OmitSoftPushback = true,
                    CharacterEmitSfxName="SlashEmitSpd3",
                    ExplosionSfxName="Melee_Explosion2",
                    MhType = MultiHitType.FromEmission,
                    CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                    }
                )),

                    new KeyValuePair<int, Skill>(5, new Skill{
                            RecoveryFrames = 50,
                            RecoveryFramesOnBlock = 30,
                            RecoveryFramesOnHit = 30,
                            MpDelta = 0,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(
                                SwordManMelee1PrimerBullet
                                )),

                    new KeyValuePair<int, Skill>(6, new Skill{
                            RecoveryFrames = 32,
                            RecoveryFramesOnBlock = 32,
                            RecoveryFramesOnHit = 32,
                            MpDelta = 100,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk2
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 7,
                                ActiveFrames = 24,
                                HitStunFrames = 27,
                                BlockStunFrames = 9,
                                Damage = 15,
                                PushbackVelX = (int)(-0.1f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(15*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 16,
                                CancellableEdFrame = 32,
                                SpeciesId = 1,
                                ExplosionSpeciesId = 1,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                CharacterEmitSfxName = "FlameEmit1",
                                ExplosionSfxName = "Explosion4",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }.UpsertCancelTransit(PATTERN_B, 7)
                )),

                    new KeyValuePair<int, Skill>(7, new Skill{
                            RecoveryFrames = 29,
                            RecoveryFramesOnBlock = 29,
                            RecoveryFramesOnHit = 29,
                            MpDelta = 100,
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
                                PushbackVelX = (int)(0.1f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(15*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 3,
                                ExplosionSpeciesId = 3,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CancellableStFrame = 16,
                                CancellableEdFrame = 30,
                                CharacterEmitSfxName = "FlameEmit1",
                                ExplosionSfxName = "Explosion4",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }.UpsertCancelTransit(PATTERN_B, 8)
                )),

                    new KeyValuePair<int, Skill>(8, new Skill{
                            RecoveryFrames = 40,
                            RecoveryFramesOnBlock = 40,
                            RecoveryFramesOnHit = 40,
                            MpDelta = 0,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk4
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 12,
                                StartupInvinsibleFrames = 8,
                                ActiveFrames = 26,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(-4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(18*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(13*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 1,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CharacterEmitSfxName = "FlameEmit1",
                                ExplosionSfxName = "Explosion4",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                )),

                    new KeyValuePair<int, Skill>(9, new Skill{
                            RecoveryFrames = 51,
                            RecoveryFramesOnBlock = 51,
                            RecoveryFramesOnHit = 51,
                            MpDelta = 550,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(WitchGirlFireballBulletHit1)
                    ),

                    new KeyValuePair<int, Skill>(10, new Skill{
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
                        PushbackVelX = NO_LOCK_VEL,
                        PushbackVelY = NO_LOCK_VEL,
                        SelfLockVelX = (int)(-4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        SelfLockVelY = 0,
                        DelaySelfVelToActive = true,
                        BType = BulletType.Melee,
                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                        }
                    )),

                            new KeyValuePair<int, Skill>(11, new Skill{
                                    RecoveryFrames = 18,
                                    RecoveryFramesOnBlock = 18,
                                    RecoveryFramesOnHit = 18,
                                    TriggerType = SkillTriggerType.RisingEdge,
                                    BoundChState = Dashing
                                    }
                                    .AddHit(
                                        new BulletConfig {
                                        StartupFrames = 3,
                                        StartupInvinsibleFrames = 2,
                                        ActiveFrames = 4,
                                        PushbackVelX = NO_LOCK_VEL,
                                        PushbackVelY = NO_LOCK_VEL,
                                        SelfLockVelX = (int)(4.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SelfLockVelY = 0,
                                        DelaySelfVelToActive = true,
                                        OmitSoftPushback = true,
                                        BType = BulletType.Melee,
                                        ActiveVfxSpeciesId = VfxDashingActive.SpeciesId,
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }
                                    )
                                    .AddHit(
                                        new BulletConfig {
                                        StartupFrames = 8,
                                        ActiveFrames = 9,
                                        PushbackVelX = NO_LOCK_VEL,
                                        PushbackVelY = NO_LOCK_VEL,
                                        SelfLockVelX = (int)(5.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SelfLockVelY = 0,
                                        BType = BulletType.Melee,
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }
                                    )
                            ),

                                                    new KeyValuePair<int, Skill>(12, new Skill{
                                                            RecoveryFrames = 55,
                                                            RecoveryFramesOnBlock = 28,
                                                            RecoveryFramesOnHit = 28,
                                                            MpDelta = 0,
                                                            TriggerType = SkillTriggerType.RisingEdge,
                                                            BoundChState = Atk2
                                                            }
                                                            .AddHit(
                                                                SwordManDragonPunchPrimerBullet
                                                                )),

                                                    new KeyValuePair<int, Skill>(13, FireSwordManFireballSkill),

                                                    new KeyValuePair<int, Skill>(14, new Skill{
                                                            RecoveryFrames = 110,
                                                            RecoveryFramesOnBlock = 110,
                                                            RecoveryFramesOnHit = 110,
                                                            MpDelta = 0,
                                                            TriggerType = SkillTriggerType.RisingEdge,
                                                            BoundChState = Atk1
                                                            }
                                                            .AddHit(
                                                                BullWarriorMelee1PrimaryBullet
                                                                )
                                                            .AddHit(
                                                                new BulletConfig {
                                                                StartupFrames = 17,
                                                                ActiveFrames = 19,
                                                                HitStunFrames = 10,
                                                                HitInvinsibleFrames = 16,
                                                                BlockStunFrames = 9,
                                                                Damage = 25,
                                                                PushbackVelX = (int)(2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                                PushbackVelY = (int)(-8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                                SelfLockVelX = NO_LOCK_VEL,
                                                                SelfLockVelY = NO_LOCK_VEL,
                                                                HitboxOffsetX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                                HitboxSizeX = (int)(100*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                                HitboxSizeY = (int)(100*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                                BlowUp = false,
                                                                SpeciesId = 3,
                                                                ExplosionFrames = 25,
                                                                BType = BulletType.Melee,
                                                                DirX = 1,
                                                                Hardness = 6,
                                                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                                                }
                )
                    ),

                    new KeyValuePair<int, Skill>(15, BullWarriorFireballSkill),

                    new KeyValuePair<int, Skill>(16, new Skill{
                            RecoveryFrames = 50,
                            RecoveryFramesOnBlock = 30,
                            RecoveryFramesOnHit = 30,
                            MpDelta = 0,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(
                                FireSwordManMelee1PrimerBullet
                                )),

                    new KeyValuePair<int, Skill>(17, new Skill{
                            RecoveryFrames = 55,
                            RecoveryFramesOnBlock = 28,
                            RecoveryFramesOnHit = 28,
                            MpDelta = 0,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk2
                            }
                            .AddHit(
                                FireSwordManDragonPunchPrimerBullet
                                )),

                    new KeyValuePair<int, Skill>(18, new Skill{
                            RecoveryFrames = 12,
                            RecoveryFramesOnBlock = 10,
                            RecoveryFramesOnHit = 10,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk1
                            }
                            .AddHit(
                                PistolBulletGround
                                )
                            ),

                    new KeyValuePair<int, Skill>(19, new Skill{
                            RecoveryFrames = 12,
                            RecoveryFramesOnBlock = 10,
                            RecoveryFramesOnHit = 10,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = InAirAtk1
                            }
                            .AddHit(
                                PistolBulletAir
                                )
                            ),

                        new KeyValuePair<int, Skill>(20, new Skill{
                                RecoveryFrames = 22,
                                RecoveryFramesOnBlock = 22,
                                RecoveryFramesOnHit = 22,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Sliding
                                }
                                .AddHit(
                                    new BulletConfig {
                                    StartupFrames = 3,
                                    StartupInvinsibleFrames = 2,
                                    ActiveFrames = 4,
                                    PushbackVelX = NO_LOCK_VEL,
                                    PushbackVelY = NO_LOCK_VEL,
                                    SelfLockVelX = (int)(4.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    SelfLockVelY = NO_LOCK_VEL,
                                    DelaySelfVelToActive = true,
                                    OmitSoftPushback = true,
                                    BType = BulletType.Melee,
                                    ActiveVfxSpeciesId = VfxDashingActive.SpeciesId,
                                    CollisionTypeMask = COLLISION_CHARACTER_INDEX_PREFIX,
                                    }
                                )
                                .AddHit(
                                    new BulletConfig {
                                    StartupFrames = 8,
                                    ActiveFrames = 13,
                                    PushbackVelX = NO_LOCK_VEL,
                                    PushbackVelY = NO_LOCK_VEL,
                                    SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                    SelfLockVelY = NO_LOCK_VEL,
                                    BType = BulletType.Melee,
                                    CollisionTypeMask = COLLISION_CHARACTER_INDEX_PREFIX,
                                    }
                                )
                        ),

                    new KeyValuePair<int, Skill>(21, new Skill{
                            RecoveryFrames = 30,
                            RecoveryFramesOnBlock = 60,
                            RecoveryFramesOnHit = 60,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Atk2
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

                            new KeyValuePair<int, Skill>(22, new Skill{
                                    RecoveryFrames = 90,
                                    RecoveryFramesOnBlock = 80,
                                    RecoveryFramesOnHit = 70,
                                    MpDelta = 0,
                                    TriggerType = SkillTriggerType.RisingEdge,
                                    BoundChState = Atk1
                                    }
                                    .AddHit(GoblinMelee1PrimerBullet)
                                    ),

                            new KeyValuePair<int, Skill>(23, new Skill {
                                    RecoveryFrames = 27,
                                    RecoveryFramesOnBlock = 27,
                                    RecoveryFramesOnHit = 27,
                                    MpDelta = 0,
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
                                        HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeY = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        CancellableStFrame = 11,
                                        CancellableEdFrame = 26,
                                        SpeciesId = 2,
                                        ExplosionFrames = 25,
                                        BType = BulletType.Melee,
                                        ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                                        DirX = 1,
                                        DirY = 0,
                                        Hardness = 7,
                                        CharacterEmitSfxName = "SlashEmitSpd1",
                                        ExplosionSfxName="Melee_Explosion2",
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }.UpsertCancelTransit(1, 24)
                        )),

                                    new KeyValuePair<int, Skill>(24, new Skill{
                                            RecoveryFrames = 23,
                                            RecoveryFramesOnBlock = 23,
                                            RecoveryFramesOnHit = 23,
                                            MpDelta = 0,
                                            TriggerType = SkillTriggerType.RisingEdge,
                                            BoundChState = Atk2
                                            }
                                            .AddHit(
                                                new BulletConfig {
                                                StartupFrames = 6,
                                                StartupInvinsibleFrames = 5,
                                                ActiveFrames = 18,
                                                HitStunFrames = 18,
                                                BlockStunFrames = 18,
                                                Damage = 24,
                                                PushbackVelX = 0,
                                                PushbackVelY = (int)(-2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                SelfLockVelX = NO_LOCK_VEL,
                                                SelfLockVelY = NO_LOCK_VEL,
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

                    new KeyValuePair<int, Skill>(25, new Skill{
                            RecoveryFrames = 32,
                            RecoveryFramesOnBlock = 32,
                            RecoveryFramesOnHit = 32,
                            MpDelta = 350,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = InAirAtk1
                            }
                            .AddHit(WitchGirlFireballBulletHit1)
                    ),

                    new KeyValuePair<int, Skill>(26, new Skill{
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
                        PushbackVelX = NO_LOCK_VEL,
                        PushbackVelY = NO_LOCK_VEL,
                        SelfLockVelX = (int)(+4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        SelfLockVelY = 0,
                        DelaySelfVelToActive = true,
                        BType = BulletType.Melee,
                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                        }
                    )),

                    new KeyValuePair<int, Skill>(27, IcePillarSkill),

                    new KeyValuePair<int, Skill>(28, new Skill{
                            RecoveryFrames = 12,
                            RecoveryFramesOnBlock = 12,
                            RecoveryFramesOnHit = 12,
                            MpDelta = 8*BATTLE_DYNAMICS_FPS,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = Dashing
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 4,
                                ActiveFrames = 8,
                                HitStunFrames = 12,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                BType = BulletType.Melee,
                                FireballEmitSfxName="SlashEmitSpd2",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX,
                                ActiveVfxSpeciesId = VfxDashingActive.SpeciesId
                                }
                            )
                        ),

                        new KeyValuePair<int, Skill>(29, new Skill{
                                RecoveryFrames = 12,
                                RecoveryFramesOnBlock = 10,
                                RecoveryFramesOnHit = 10,
                                MpDelta = 0,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Atk1
                                }
                                .AddHit(
                                    MagicPistolBulletGround
                                    )
                                ),

                        new KeyValuePair<int, Skill>(30, new Skill{
                                RecoveryFrames = 12,
                                RecoveryFramesOnBlock = 10,
                                RecoveryFramesOnHit = 10,
                                MpDelta = 0,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = InAirAtk1
                                }
                                .AddHit(
                                    MagicPistolBulletAir
                                    )
                                ),

                        new KeyValuePair<int, Skill>(31, new Skill{
                                RecoveryFrames = 100,
                                RecoveryFramesOnBlock = 10,
                                RecoveryFramesOnHit = 10,
                                MpDelta = 120,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Atk1
                                }
                                .AddHit(
                                    PurpleArrowBullet
                                    )
                                ),

                        new KeyValuePair<int, Skill>(32, new Skill{
                                RecoveryFrames = 100,
                                RecoveryFramesOnBlock = 10,
                                RecoveryFramesOnHit = 10,
                                MpDelta = 180,
                                TriggerType = SkillTriggerType.RisingEdge,
                                BoundChState = Atk2
                                }
                                .AddHit(
                                    PurpleArrowRainBullet1
                                    )
                                .AddHit(
                                    PurpleArrowRainBullet2
                                    )
                                .AddHit(
                                    PurpleArrowRainBullet3
                                    )
                                ),

                            new KeyValuePair<int, Skill>(33, new Skill{
                                    RecoveryFrames = 18,
                                    RecoveryFramesOnBlock = 18,
                                    RecoveryFramesOnHit = 18,
                                    TriggerType = SkillTriggerType.RisingEdge,
                                    BoundChState = Sliding
                                    }
                                    .AddHit(
                                        new BulletConfig {
                                        StartupFrames = 5,
                                        PushbackVelX = NO_LOCK_VEL,
                                        PushbackVelY = NO_LOCK_VEL,
                                        SelfLockVelX = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SelfLockVelY = 0,
                                        DelaySelfVelToActive = true,
                                        SpeciesId = 1,
                                        HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxOffsetY = (int)(-12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        BType = BulletType.Melee,
                                        Hardness = 5,
                                        ActiveVfxSpeciesId = VfxDashingActive.SpeciesId,
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }
                                        )),

                            new KeyValuePair<int, Skill>(34, new Skill {
                                    RecoveryFrames = 28,
                                    RecoveryFramesOnBlock = 28,
                                    RecoveryFramesOnHit = 28,
                                    TriggerType = SkillTriggerType.RisingEdge,
                                    BoundChState = InAirAtk2
                                    }
                                    .AddHit(
                                        new BulletConfig {
                                        StartupFrames = 6,
                                        ActiveFrames = 16,
                                        HitStunFrames = 24,
                                        BlockStunFrames = 9,
                                        Damage = 13,
                                        PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        PushbackVelY = NO_LOCK_VEL,
                                        SelfLockVelX = NO_LOCK_VEL,
                                        SelfLockVelY = NO_LOCK_VEL,
                                        HitboxOffsetX = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SpeciesId = 2,
                                        ExplosionFrames = 25,
                                        BType = BulletType.Melee,
                                        DirX = 1,
                                        DirY = 0,
                                        Hardness = 5,
                                        ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                        CharacterEmitSfxName = "FlameEmit1",
                                        ExplosionSfxName = "Explosion4",
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }
                        )),

                            new KeyValuePair<int, Skill>(255, new Skill {
                                    RecoveryFrames = 20,
                                    RecoveryFramesOnBlock = 20,
                                    RecoveryFramesOnHit = 20,
                                    TriggerType = SkillTriggerType.RisingEdge,
                                    BoundChState = InAirAtk1
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
                                        HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                        SpeciesId = 2,
                                        ExplosionFrames = 25,
                                        BType = BulletType.Melee,
                                        DirX = 1,
                                        DirY = 0,
                                        Hardness = 5,
                                        CancellableStFrame = 10,
                                        CancellableEdFrame = 21,
                                        CharacterEmitSfxName="SlashEmitSpd1",
                                        ExplosionSfxName="Melee_Explosion2",
                                        CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                        }.UpsertCancelTransit(PATTERN_B, 258).UpsertCancelTransit(PATTERN_DOWN_B, 258).UpsertCancelTransit(PATTERN_UP_B, 258)
                        )),

                                    new KeyValuePair<int, Skill>(256, new Skill{
                                            RecoveryFrames = 60,
                                            RecoveryFramesOnBlock = 60,
                                            RecoveryFramesOnHit = 60,
                                            MpDelta = 0,
                                            TriggerType = SkillTriggerType.RisingEdge,
                                            BoundChState = InAirAtk1
                                            }
                                            .AddHit(
                                                new BulletConfig {
                                                StartupFrames = 2,
                                                ActiveFrames = 2,
                                                HitStunFrames = 12,
                                                BlockStunFrames = 9,
                                                Damage = 13,
                                                PushbackVelX = NO_LOCK_VEL,
                                                PushbackVelY = NO_LOCK_VEL,
                                                SelfLockVelX = NO_LOCK_VEL,
                                                SelfLockVelY = NO_LOCK_VEL,
                                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                SpeciesId = 1,
                                                ExplosionFrames = 20,
                                                DirX = 1,
                                                DirY = 0,
                                                Hardness = 5,
                                                BType = BulletType.Melee,
                                                FireballEmitSfxName="SlashEmitSpd1",
                                                ExplosionSfxName="Melee_Explosion1",
                                                MhType = MultiHitType.FromEmission,
                                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                                }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 4,
                                            ActiveFrames = 3,
                                            HitStunFrames = 12,
                                            BlockStunFrames = 9,
                                            Damage = 11,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(2*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 1,
                                            ExplosionFrames = 20,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 5,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd2",
                                            ExplosionSfxName="Melee_Explosion1",
                                            MhType = MultiHitType.FromEmission,
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 7,
                                            ActiveFrames = 3,
                                            HitStunFrames = 13,
                                            BlockStunFrames = 9,
                                            Damage = 7,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 1,
                                            ExplosionFrames = 20,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 5,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd1",
                                            ExplosionSfxName="Melee_Explosion1",
                                            MhType = MultiHitType.FromEmission,
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 10,
                                            ActiveFrames = 3,
                                            HitStunFrames = 13,
                                            BlockStunFrames = 9,
                                            Damage = 7,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 1,
                                            ExplosionFrames = 20,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 5,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd2",
                                            ExplosionSfxName="Melee_Explosion1",
                                            MhType = MultiHitType.FromEmission,
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 13,
                                            ActiveFrames = 3,
                                            HitStunFrames = 13,
                                            BlockStunFrames = 9,
                                            Damage = 7,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 1,
                                            ExplosionFrames = 20,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 5,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd1",
                                            ExplosionSfxName="Melee_Explosion1",
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    ),

                                    new KeyValuePair<int, Skill>(257, new Skill{
                                            RecoveryFrames = 35,
                                            RecoveryFramesOnBlock = 35,
                                            RecoveryFramesOnHit = 35,
                                            MpDelta = 0,
                                            TriggerType = SkillTriggerType.RisingEdge,
                                            BoundChState = InAirAtk1
                                            }
                                            .AddHit(
                                                new BulletConfig {
                                                StartupFrames = 2,
                                                ActiveFrames = 2,
                                                HitStunFrames = 12,
                                                BlockStunFrames = 9,
                                                Damage = 13,
                                                PushbackVelX = NO_LOCK_VEL,
                                                PushbackVelY = NO_LOCK_VEL,
                                                SelfLockVelX = NO_LOCK_VEL,
                                                SelfLockVelY = NO_LOCK_VEL,
                                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                                SpeciesId = 2,
                                                ExplosionFrames = 25,
                                                DirX = 1,
                                                DirY = 0,
                                                Hardness = 7,
                                                BType = BulletType.Melee,
                                                FireballEmitSfxName="SlashEmitSpd2",
                                                ExplosionSfxName="Melee_Explosion2",
                                                MhType = MultiHitType.FromEmission,
                                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                                }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 4,
                                            ActiveFrames = 3,
                                            HitStunFrames = 12,
                                            BlockStunFrames = 9,
                                            Damage = 11,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(2*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 2,
                                            ExplosionFrames = 25,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 7,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd2",
                                            ExplosionSfxName="Melee_Explosion2",
                                            MhType = MultiHitType.FromEmission,
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 7,
                                            ActiveFrames = 3,
                                            HitStunFrames = 13,
                                            BlockStunFrames = 9,
                                            Damage = 7,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 2,
                                            ExplosionFrames = 25,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 7,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd2",
                                            ExplosionSfxName="Melee_Explosion2",
                                            MhType = MultiHitType.FromEmission,
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 10,
                                            ActiveFrames = 3,
                                            HitStunFrames = 13,
                                            BlockStunFrames = 9,
                                            Damage = 7,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 2,
                                            ExplosionFrames = 25,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 7,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd2",
                                            ExplosionSfxName="Melee_Explosion2",
                                            MhType = MultiHitType.FromEmission,
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    .AddHit(
                                            new BulletConfig {
                                            StartupFrames = 13,
                                            ActiveFrames = 3,
                                            HitStunFrames = 13,
                                            BlockStunFrames = 9,
                                            Damage = 7,
                                            PushbackVelX = NO_LOCK_VEL,
                                            PushbackVelY = NO_LOCK_VEL,
                                            SelfLockVelX = NO_LOCK_VEL,
                                            SelfLockVelY = NO_LOCK_VEL,
                                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxOffsetY = (int)(3*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                            SpeciesId = 2,
                                            ExplosionFrames = 25,
                                            DirX = 1,
                                            DirY = 0,
                                            Hardness = 7,
                                            BType = BulletType.Melee,
                                            FireballEmitSfxName="SlashEmitSpd2",
                                            ExplosionSfxName="Melee_Explosion2",
                                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                            }
                                )
                                    ), 

                    new KeyValuePair<int, Skill>(258, new Skill{
                            RecoveryFrames = 20,
                            RecoveryFramesOnBlock = 20,
                            RecoveryFramesOnHit = 20,
                            TriggerType = SkillTriggerType.RisingEdge,
                            BoundChState = InAirAtk2
                            }
                            .AddHit(
                                new BulletConfig {
                                StartupFrames = 4,
                                StartupInvinsibleFrames = 3,
                                ActiveFrames = 15,
                                HitStunFrames = 18,
                                BlockStunFrames = 5,
                                Damage = 12,
                                PushbackVelX = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(0.8f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 6,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                                }
                )
                    ),

                new KeyValuePair<int, Skill>(259, BladeGirlDragonPunchSkill),
                }
        );

        public static int FindSkillId(int patternId, CharacterDownsync currCharacterDownsync, int speciesId, bool slotUsed) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            switch (speciesId) {
                case SPECIES_BLADEGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_DOWN_B: // Including "PATTERN_DOWN_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 255; // A fallback to "InAirAtk1"
                                } else {
                                    return 1;
                                }
                            } else {
                                // [WARNING] Combo in crouching is prohibited for this character.
                                if (isCrouching(currCharacterDownsync.CharacterState)) {
                                    return NO_SKILL;
                                }
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B: // Including "PATTERN_DOWN_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 255; // A fallback to "InAirAtk1" 
                                } else {
                                    return 259;
                                }
                            } else {
                                // [WARNING] Combo in air is possible for this character!
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_DOWN_A:
                            if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota) {
                                // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                // Air-dash is allowed for this speciesId
                                return 11;
                            } else if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                return 20; 
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!notRecovered && slotUsed && !currCharacterDownsync.InAir) {
                                return 4;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_SWORDMAN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 5;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 12;
                            } else {
                                return NO_SKILL;
                            }
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
                                return NO_SKILL;
                            }
                        case PATTERN_B:
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 34; // A fallback to "InAirAtk2"
                                } else {
                                    return 6;
                                }
                            } else {
                                if (currCharacterDownsync.InAir) {
                                    // [WARNING] No air combo for this character!
                                    return NO_SKILL;
                                } else {
                                    // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                    if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                    var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                    var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                    if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                    if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                    return currBulletConfig.CancelTransit[patternId];
                                }
                            }
                        case PATTERN_DOWN_A:
                            if (!currCharacterDownsync.InAir) {
                                return 10;
                            } else if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota) {
                                return 26;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!notRecovered && slotUsed && !currCharacterDownsync.InAir) {
                                return 27;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_FIRESWORDMAN:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 16;
                            } else {
                                return NO_SKILL;
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
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_MAGSWORDGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_UP_B:
                        case PATTERN_DOWN_B:
                            if (!notRecovered && slotUsed) {
                                if (currCharacterDownsync.InAir) {
                                    return 30;
                                } else {
                                    return 29;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_INVENTORY_SLOT_C:
                            if (!notRecovered && slotUsed) {
                                return 21;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_A:
                            if (!currCharacterDownsync.InAir) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                return 33;
                            } else if (currCharacterDownsync.InAir && 0 < currCharacterDownsync.RemainingAirDashQuota) {
                                // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                // Air-dash is allowed for this speciesId
                                return 28;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_BULLWARRIOR:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 14;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 15;
                            } else {
                                return NO_SKILL;
                            }
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
                case SPECIES_SKELEARCHER:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_DOWN_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 31;
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 32;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                default:
                    return NO_SKILL;
            }

        }
    }
}
