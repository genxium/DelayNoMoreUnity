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
            HitboxSizeY = (int)(32 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
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
            MpDelta = 270,
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
                    CharacterEmitSfxName="FlameEmit1",
                    ExplosionSfxName="Explosion4",
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
            FireballEmitSfxName="FlameEmit1",
            ExplosionSfxName="Explosion4",
            ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
            CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
            SimultaneousMultiHitCnt = 2
        };

        public static Skill BullWarriorFireballSkill = new Skill {
            RecoveryFrames = 60,
            RecoveryFramesOnBlock = 50,
            RecoveryFramesOnHit = 50,
            MpDelta = 270,
            TriggerType = SkillTriggerType.RisingEdge,
            BoundChState = Atk5
        }
                        .AddHit(
                            BullWarriorFireballPivotBullet                            
                        )
                        .AddHit(
                            new BulletConfig(BullWarriorFireballPivotBullet)
                                .SetDir(1, -1)
                                .SetHitboxOffsets((int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-4 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                .SetPushbacks((int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(-0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                .SetSimultaneousMultiHitCnt(1u)
                        )
                        .AddHit(
                            new BulletConfig(BullWarriorFireballPivotBullet)
                                .SetDir(1, +1)
                                .SetHitboxOffsets((int)(16 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(28 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                .SetPushbacks((int)(0.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), (int)(+0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO))
                                .SetSimultaneousMultiHitCnt(0u)
                        );

        private static BulletConfig PistolBulletAir = new BulletConfig {
                        StartupFrames = 2,
                        ActiveFrames = 180,
                        HitStunFrames = 6,
                        BlockStunFrames = 4,
                        Damage = 6,
                        PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        PushbackVelY = NO_LOCK_VEL,
                        SelfLockVelX = NO_LOCK_VEL,
                        SelfLockVelY = NO_LOCK_VEL,
                        HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        HitboxSizeX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        HitboxSizeY = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        SpeciesId = 8,
                        ExplosionSpeciesId = EXPLOSION_SPECIES_NONE,
                        Speed = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                        DirX = 2,
                        DirY = 0,
                        Hardness = 4,
                        ExplosionFrames = 30,
                        BType = BulletType.Fireball,
                        ExplosionVfxSpeciesId = VfxPistolBulletExploding.SpeciesId,
                        CharacterEmitSfxName = "Fireball8",
                        ExplosionSfxName = "Explosion8",
                        CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig GunGirlSlashNovaRepeatingBullet = new BulletConfig {
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
            ExplosionSpeciesId = 5,
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
            CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
        };

        private static BulletConfig GunGirlSlashNovaStarterBullet = new BulletConfig(GunGirlSlashNovaRepeatingBullet).SetStartupFrames(10).SetSpeed(GunGirlSlashNovaRepeatingBullet.SpeedIfNotHit);

        private static BulletConfig PistolBulletGround = new BulletConfig(PistolBulletAir).SetAllowsWalking(true).SetAllowsCrouching(true);

        public static BulletConfig GoblinMelee1PrimerBullet = new BulletConfig {
            StartupFrames = 63,
            ActiveFrames = 10,
            HitStunFrames = 4,
            HitInvinsibleFrames = 8,
            BlockStunFrames = 2,
            Damage = 12,
            PushbackVelX = (int)(0.3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
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
                        RecoveryFrames = 27,
                        RecoveryFramesOnBlock = 27,
                        RecoveryFramesOnHit = 27,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                    }
                    .AddHit(
                        new BulletConfig {
                            StartupFrames = 3,
                            StartupInvinsibleFrames = 3,
                            ActiveFrames = 22,
                            HitStunFrames = 22,
                            BlockStunFrames = 9,
                            Damage = 13,
                            PushbackVelX = 0,
                            PushbackVelY = NO_LOCK_VEL,
                            SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SelfLockVelY = NO_LOCK_VEL,
                            HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            CancellableStFrame = 11,
                            CancellableEdFrame = 30,
                            SpeciesId = 2,
                            ExplosionFrames = 25,
                            BType = BulletType.Melee,
                            ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                            DirX = 1,
                            DirY = 0,
                            Hardness = 5,
                            CharacterEmitSfxName = "SlashEmitSpd1",
                            ExplosionSfxName="Melee_Explosion2",
                            CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                        }.UpsertCancelTransit(1, 2)
                    )),

                    new KeyValuePair<int, Skill>(2, new Skill{
                        RecoveryFrames = 25,
                        RecoveryFramesOnBlock = 25,
                        RecoveryFramesOnHit = 25,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk2
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 6,
                                StartupInvinsibleFrames = 3,
                                ActiveFrames = 30,
                                HitStunFrames = 20,
                                BlockStunFrames = 9,
                                Damage = 14,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 14,
                                CancellableEdFrame = 36,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CharacterEmitSfxName = "SlashEmitSpd2",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }.UpsertCancelTransit(1, 3)
                        )),

                    new KeyValuePair<int, Skill>(3, new Skill{
                        RecoveryFrames = 40,
                        RecoveryFramesOnBlock = 40,
                        RecoveryFramesOnHit = 40,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk3
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 8,
                                StartupInvinsibleFrames = 6,
                                ActiveFrames = 5,
                                HitStunFrames = 9,
                                BlockStunFrames = 5,
                                Damage = 3,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CharacterEmitSfxName = "SlashEmitSpd3",
                                ExplosionSfxName="Melee_Explosion2",
                                ActiveVfxSpeciesId = VfxSlashActive.SpeciesId,
                                ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                                MhType = MultiHitType.FromEmission,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 13,
                                ActiveFrames = 10,
                                HitStunFrames = 12,
                                BlockStunFrames = 9,
                                Damage = 7,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(0.2f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = false,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionSfxName="Melee_Explosion2",
                                ActiveVfxSpeciesId = VfxSlashActive.SpeciesId,
                                MhType = MultiHitType.FromEmission,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 23,
                                ActiveFrames = 7,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 10,
                                Damage = 7,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionSfxName="Melee_Explosion2",
                                ActiveVfxSpeciesId = VfxSlashActive.SpeciesId,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                        ),

                    new KeyValuePair<int, Skill>(4, new Skill{
                        RecoveryFrames = 60,
                        RecoveryFramesOnBlock = 60,
                        RecoveryFramesOnHit = 60,
                        MpDelta = 240,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 16,
                                ActiveFrames = 600, // enough for it to fly out of the battle area
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(7f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = true,
                                SpeciesId = 3,
                                Speed = (int)(2*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = 0,
                                Hardness = 6,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                CharacterEmitSfxName="FlameEmit1",
                                ActiveSfxName="FlameBurning1",
                                ExplosionSfxName="Explosion3",
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
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
                        RecoveryFrames = 27,
                        RecoveryFramesOnBlock = 27,
                        RecoveryFramesOnHit = 27,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 6,
                                ActiveFrames = 22,
                                HitStunFrames = 22,
                                BlockStunFrames = 9,
                                Damage = 15,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 14,
                                CancellableEdFrame = 30,
                                SpeciesId = 1,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                CharacterEmitSfxName="SlashEmitSpd1",
                                ExplosionSfxName="Melee_Explosion1",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }.UpsertCancelTransit(1, 7)
                        )),

                    new KeyValuePair<int, Skill>(7, new Skill{
                        RecoveryFrames = 30,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk2
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 6,
                                ActiveFrames = 20,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 19,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(0*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 3,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CharacterEmitSfxName="SlashEmitSpd3",
                                ExplosionSfxName="Melee_Explosion3",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )),

                    new KeyValuePair<int, Skill>(8, new Skill{
                        RecoveryFrames = 40,
                        RecoveryFramesOnBlock = 40,
                        RecoveryFramesOnHit = 40,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk3
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                StartupInvinsibleFrames = 10,
                                ActiveFrames = 30,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(1.5f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = (int)(7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 1,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CharacterEmitSfxName="SlashEmitSpd3",
                                ExplosionSfxName="Melee_Explosion3",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )),

                    new KeyValuePair<int, Skill>(9, new Skill{
                        RecoveryFrames = 60,
                        RecoveryFramesOnBlock = 60,
                        RecoveryFramesOnHit = 60,
                        MpDelta = 250,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 7,
                                ActiveFrames = 600,
                                HitStunFrames = 10,
                                BlockStunFrames = 9,
                                Damage = 4,
                                PushbackVelX = (int)(0.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                BType = BulletType.Fireball,
                                CharacterEmitSfxName="SlashEmitSpd1",
                                ExplosionSfxName="Explosion2",
                                MhType = MultiHitType.FromPrevHitActual,
                                CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                ActiveFrames = 600,
                                HitStunFrames = 10,
                                BlockStunFrames = 9,
                                Damage = 4,
                                PushbackVelX = (int)(0.3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                BType = BulletType.Fireball,
                                FireballEmitSfxName="Explosion1",
                                ExplosionSfxName="Explosion2",
                                MhType = MultiHitType.FromPrevHitActual,
                                CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                ActiveFrames = 600,
                                HitStunFrames = 10,
                                BlockStunFrames = 9,
                                Damage = 4,
                                PushbackVelX = (int)(0.1f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(-4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionFrames = 25,
                                FireballEmitSfxName="Explosion1",
                                ExplosionSfxName="Explosion2",
                                BType = BulletType.Fireball,
                                CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
                            }
                        )
                    ),

                    new KeyValuePair<int, Skill>(10, new Skill{
                        RecoveryFrames = 10,
                        RecoveryFramesOnBlock = 10,
                        RecoveryFramesOnHit = 10,
                        MpDelta = 60,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Dashing
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 3,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                DelaySelfVelToActive = true,
                                BType = BulletType.Melee,
                                ActiveVfxSpeciesId = VfxDashingActive.SpeciesId,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )),

                    new KeyValuePair<int, Skill>(11, new Skill{
                        RecoveryFrames = 10,
                        RecoveryFramesOnBlock = 10,
                        RecoveryFramesOnHit = 10,
                        MpDelta = 60,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Dashing
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 3,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
                                DelaySelfVelToActive = true,
                                BType = BulletType.Melee,
                                ActiveVfxSpeciesId = VfxDashingActive.SpeciesId,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )),

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
                        MpDelta = 35,
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
                        MpDelta = 25,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = InAirAtk1
                    }
                        .AddHit(
                            PistolBulletAir
                        )
                    ),

                    new KeyValuePair<int, Skill>(20, new Skill{
                        RecoveryFrames = 25,
                        RecoveryFramesOnBlock = 25,
                        RecoveryFramesOnHit = 25,
                        MpDelta = 60,
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

                    new KeyValuePair<int, Skill>(21, new Skill{
                        RecoveryFrames = 60,
                        RecoveryFramesOnBlock = 60,
                        RecoveryFramesOnHit = 60,
                        MpDelta = 50,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk3
                    }
                        .AddHit(GunGirlSlashNovaStarterBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(GunGirlSlashNovaRepeatingBullet)
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 9,
                                ActiveFrames = 600,
                                HitStunFrames = 12,
                                BlockStunFrames = 9,
                                Damage = 10,
                                PushbackVelX = (int)(0.3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), // The last hit has some pushback
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 9,
                                ExplosionSpeciesId = 2,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                BType = BulletType.Fireball,
                                FireballEmitSfxName="SlashEmitSpd2",
                                ExplosionSfxName="Explosion2",
                                CollisionTypeMask = COLLISION_B_FIREBALL_INDEX_PREFIX
                            }
                        )
                    ),

                    new KeyValuePair<int, Skill>(22, new Skill{
                            RecoveryFrames = 90,
                            RecoveryFramesOnBlock = 80,
                            RecoveryFramesOnHit = 70,
                            MpDelta = 200,
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
                        RecoveryFrames = 23,
                        RecoveryFramesOnBlock = 23,
                        RecoveryFramesOnHit = 23,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                StartupInvinsibleFrames = 5,
                                ActiveFrames = 11,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 18,
                                Damage = 33,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(14f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(56*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                BlowUp = true,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 8,
                                CharacterEmitSfxName = "SlashEmitSpd2",
                                ExplosionSfxName="Melee_Explosion2",
                                MhType = MultiHitType.FromEmission,
                                OmitSoftPushback = true,
                                DelaySelfVelToActive = true,
                                ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                        .AddHit(
                            // Just used to stop the character
                            new BulletConfig {
                                StartupFrames = 15,
                                StartupInvinsibleFrames = 0,
                                HitStunFrames = MAX_INT,
                                Damage = 33,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(3f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = 0,
                                SelfLockVelY = NO_LOCK_VEL,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 8,
                                OmitSoftPushback = true,
                                ExplosionVfxSpeciesId = VfxSlashExploding.SpeciesId,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                   ),

                    new KeyValuePair<int, Skill>(26, new Skill{
                        RecoveryFrames = 55,
                        RecoveryFramesOnBlock = 55,
                        RecoveryFramesOnHit = 55,
                        MpDelta = 120,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Dashing
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 4,
                                ActiveFrames = 4,
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
                                Hardness = 7,
                                BType = BulletType.Melee,
                                FireballEmitSfxName="SlashEmitSpd2",
                                ExplosionSfxName="Melee_Explosion2",
                                MhType = MultiHitType.FromEmission,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX,
                                ActiveVfxSpeciesId = VfxDashingActive.SpeciesId
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 9,
                                ActiveFrames = 5,
                                HitStunFrames = 12,
                                BlockStunFrames = 9,
                                Damage = 11,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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
                                StartupFrames = 17,
                                ActiveFrames = 4,
                                HitStunFrames = 13,
                                BlockStunFrames = 9,
                                Damage = 7,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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
                                StartupFrames = 21,
                                ActiveFrames = 5,
                                HitStunFrames = 13,
                                BlockStunFrames = 9,
                                Damage = 7,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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
                                StartupFrames = 29,
                                ActiveFrames = 4,
                                HitStunFrames = 13,
                                BlockStunFrames = 9,
                                Damage = 7,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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
                                StartupFrames = 34,
                                ActiveFrames = 2,
                                HitStunFrames = 13,
                                BlockStunFrames = 9,
                                Damage = 7,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(6f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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
                                StartupFrames = 46,
                                ActiveFrames = 5,
                                HitStunFrames = 13,
                                BlockStunFrames = 9,
                                Damage = 21,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = (int)(2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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

                    new KeyValuePair<int, Skill>(27, new Skill{
                        RecoveryFrames = 23,
                        RecoveryFramesOnBlock = 23,
                        RecoveryFramesOnHit = 23,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk4
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 8,
                                StartupInvinsibleFrames = 3,
                                ActiveFrames = 1200, // At most flies for 20 seconds 
                                HitStunFrames = 0,
                                BlockStunFrames = 0,
                                Damage = 0,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 10,
                                ExplosionFrames = 0,
                                BType = BulletType.Fireball,
                                DirX = 1,
                                DirY = 1,
                                Hardness = 4,
                                Speed = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CharacterEmitSfxName = "SlashEmitSpd2",
                                MhType = MultiHitType.FromPrevHitActual,
                                AllowsWalking = true,  
                                TakesGravity = true,
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX // Will trigger the explosive wave when hitting anything
                            }
                        )
                        .AddHit(
                            // Just used to stop the character
                            new BulletConfig {
                                StartupFrames = 0,
                                StartupInvinsibleFrames = 0,
                                HitInvinsibleFrames = 20,
                                BlowUp = true,
                                ActiveFrames = 20, 
                                HitStunFrames = MAX_INT,
                                Damage = 45,
                                PushbackVelX = NO_LOCK_VEL,
                                PushbackVelY = (int)(7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                SpeciesId = 10,
                                ExplosionFrames = 40,
                                HitboxSizeX = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeIncX = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO), 
                                HitboxSizeIncY = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BType = BulletType.Fireball,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 8,
                                RemainUponHit = true,
                                ExplosionSfxName="Explosion3",
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
                            }
                        )
                   ),

                    new KeyValuePair<int, Skill>(255, new Skill {
                        RecoveryFrames = 30,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = InAirAtk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 2,
                                ActiveFrames = 20,
                                HitStunFrames = 18,
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
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CharacterEmitSfxName="SlashEmitSpd1",
                                ExplosionSfxName="Melee_Explosion2",
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
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
                    )
                    }
            );

        public static int FindSkillId(int patternId, CharacterDownsync currCharacterDownsync, int speciesId) {
            bool notRecovered = (0 < currCharacterDownsync.FramesToRecover);
            switch (speciesId) {
                case SPECIES_KNIFEGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_UP_B: // Including "PATTERN_UP_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 255;
                                } else {
                                    return 1;
                                }
                            } else {
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 255; // A fallback to "InAirAtk1"
                                } else {
                                    return 4;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_A:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            // Air-dash is allowed for this speciesId
                            return 11;
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
                case SPECIES_MONKGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 256;
                                } else {
                                    return 6;
                                }
                            } else {
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                if (NO_SKILL_HIT == currCharacterDownsync.ActiveSkillHit || currCharacterDownsync.ActiveSkillHit >= currSkillConfig.Hits.Count) return NO_SKILL;
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 256; // A fallback to "InAirAtk1" 
                                } else {
                                    return 8;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 256; // A fallback to "InAirAtk1" 
                                } else {
                                    return 9;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_A:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            // Air-dash is prohibited for this speciesId
                            if (!currCharacterDownsync.InAir) {
                                return 10;
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
                case SPECIES_GUNGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 19;
                                } else {
                                    return 18;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_UP_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 19;
                                } else {
                                    return 27;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_A:
                            if (!notRecovered) {
                                // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                                if (!currCharacterDownsync.InAir) {
                                    return 20;
                                } else {
                                    // Air-sliding is non-sense
                                    return NO_SKILL;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case SPECIES_SUPERKNIFEGIRL:
                    switch (patternId) {
                        case PATTERN_B:
                        case PATTERN_UP_B: // Including "PATTERN_UP_B" here as a "no-skill fallback" if attack button is pressed
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 257;
                                } else {
                                    return 23;
                                }
                            } else {
                                // Now that "0 < FramesToRecover", we're only able to fire any skill if it's a cancellation
                                if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return NO_SKILL;
                                var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case PATTERN_DOWN_B:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 257; // A fallback to "InAirAtk1" 
                                } else {
                                    return 25;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case PATTERN_DOWN_A:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            // Air-dash is prohibited for this speciesId
                            if (!currCharacterDownsync.InAir) {
                                return 26;
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
                default:
                    return NO_SKILL;
            }

        }
    }
}
