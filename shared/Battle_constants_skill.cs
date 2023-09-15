using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {

        public static VfxConfig VfxDashingActive = new VfxConfig {
            SpeciesId = 1,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false
        };

        public static VfxConfig VfxFireExplodingBig = new VfxConfig {
            SpeciesId = 2,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true
        };

        public static VfxConfig VfxIceExplodingBig = new VfxConfig {
            SpeciesId = 3,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true
        };

        public static VfxConfig VfxFireSlashActive = new VfxConfig {
            SpeciesId = 4,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false
        };

        public static VfxConfig VfxSlashActive = new VfxConfig {
            SpeciesId = 5,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false
        };

        public static VfxConfig VfxSpikeSlashExplodingActive = new VfxConfig {
            SpeciesId = 6,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true
        };

        public static VfxConfig VfxFirePointLightActive = new VfxConfig {
            SpeciesId = 7,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = false,
            OnBullet = true
        };

        public static ImmutableDictionary<int, VfxConfig> vfxDict = ImmutableDictionary.Create<int, VfxConfig>().AddRange(
             new[]
             {
                    new KeyValuePair<int, VfxConfig>(VfxDashingActive.SpeciesId, VfxDashingActive),
                    new KeyValuePair<int, VfxConfig>(VfxFireExplodingBig.SpeciesId, VfxFireExplodingBig),
                    new KeyValuePair<int, VfxConfig>(VfxIceExplodingBig.SpeciesId, VfxIceExplodingBig),
                    new KeyValuePair<int, VfxConfig>(VfxFireSlashActive.SpeciesId, VfxFireSlashActive),
                    new KeyValuePair<int, VfxConfig>(VfxSlashActive.SpeciesId, VfxSlashActive),
                    new KeyValuePair<int, VfxConfig>(VfxSpikeSlashExplodingActive.SpeciesId, VfxSpikeSlashExplodingActive),
                    new KeyValuePair<int, VfxConfig>(VfxFirePointLightActive.SpeciesId, VfxFirePointLightActive),
             }
            );

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
                            ActiveFrames = 22,
                            HitStunFrames = 22,
                            BlockStunFrames = 9,
                            Damage = 13,
                            PushbackVelX = 0,
                            PushbackVelY = 0,
                            SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            SelfLockVelY = NO_LOCK_VEL,
                            HitboxOffsetX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxOffsetY = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeX = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                            CancellableStFrame = 11,
                            CancellableEdFrame = 30,
                            SpeciesId = 2,
                            ExplosionFrames = 25,
                            BType = BulletType.Melee,
                            DirX = 1,
                            DirY = 0,
                            Hardness = 5,
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
                                ActiveFrames = 20,
                                HitStunFrames = 20,
                                BlockStunFrames = 9,
                                Damage = 14,
                                PushbackVelX = 0,
                                PushbackVelY = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(0.1f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(7*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(5*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 14,
                                CancellableEdFrame = 36,
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
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
                                ActiveVfxSpeciesId = VfxSlashActive.SpeciesId,
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
                                Hardness = 5,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
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
                            new BulletConfig {
                                StartupFrames = 9,
                                ActiveFrames = 16,
                                HitStunFrames = 10,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(36*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 2,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
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
                                SpeciesId = 1,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
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
                            new BulletConfig {
                                StartupFrames = 7,
                                ActiveFrames = 20,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 11,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = (int)(7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 2,
                                DirX = 1,
                                DirY = 0,
                                ExplosionFrames = 25,
                                Hardness = 5,
                                BType = BulletType.Melee,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )),

                    new KeyValuePair<int, Skill>(13, new Skill{
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
                                ActiveFrames = 600,
                                HitStunFrames = 6,
                                BlockStunFrames = 9,
                                Damage = 11,
                                PushbackVelX = (int)(.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(18*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 4,
                                Speed = (int)(4.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = 0,
                                ExplosionFrames = 20,
                                Hardness = 5,
                                BType = BulletType.Fireball,
                                ActiveVfxSpeciesId = VfxFirePointLightActive.SpeciesId,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_M_FIREBALL_INDEX_PREFIX
                            }
                        )),

                    new KeyValuePair<int, Skill>(14, new Skill{
                        RecoveryFrames = 50,
                        RecoveryFramesOnBlock = 36,
                        RecoveryFramesOnHit = 36,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 18,
                                ActiveFrames = 9,
                                HitStunFrames = 9,
                                BlockStunFrames = 3,
                                Damage = 10,
                                PushbackVelX = 0, // Freeze the target for visual emphasis
                                PushbackVelY = 0,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(64*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(80*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(90*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = false,
                                SpeciesId = 1,
                                MhType = MultiHitType.FromEmission,
                                ExplosionFrames = 20,
                                BType = BulletType.Melee,
                                DirX = 1,
                                Hardness = 5,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 17,
                                ActiveFrames = 19,
                                HitStunFrames = 40,
                                BlockStunFrames = 9,
                                Damage = 25,
                                PushbackVelX = (int)(4f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
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
                                Hardness = 5,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                    ),

                    new KeyValuePair<int, Skill>(15, new Skill{
                        RecoveryFrames = 60,
                        RecoveryFramesOnBlock = 50,
                        RecoveryFramesOnHit = 50,
                        MpDelta = 270,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk5
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 33,
                                ActiveFrames = 600,
                                HitStunFrames = 30,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = (int)(0.8f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(12*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 4,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 2,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
                                SimultaneousMultiHitCnt = 2
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 33,
                                ActiveFrames = 600,
                                HitStunFrames = 30,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(-0.2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(-4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 4,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = -1,
                                Hardness = 5,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX,
                                SimultaneousMultiHitCnt = 1
                            }
                        )
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 33,
                                ActiveFrames = 600,
                                HitStunFrames = 30,
                                BlockStunFrames = 9,
                                Damage = 12,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(0.2f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(28*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(48*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                CancellableStFrame = 0,
                                CancellableEdFrame = 0,
                                BlowUp = false,
                                SpeciesId = 4,
                                Speed = (int)(4*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 1,
                                DirY = +1,
                                ExplosionFrames = 30,
                                Hardness = 5,
                                BType = BulletType.Fireball,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
                            }
                        )
                    ),

                    new KeyValuePair<int, Skill>(16, new Skill{
                        RecoveryFrames = 50,
                        RecoveryFramesOnBlock = 30,
                        RecoveryFramesOnHit = 30,
                        MpDelta = 0,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                     }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 9,
                                ActiveFrames = 16,
                                HitStunFrames = 10,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = 0,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
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
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
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
                            new BulletConfig {
                                StartupFrames = 7,
                                ActiveFrames = 20,
                                HitStunFrames = MAX_INT,
                                BlockStunFrames = 9,
                                Damage = 13,
                                PushbackVelX = (int)(1.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = (int)(3f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelX = (int)(1.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = (int)(7f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetX = (int)(14*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(24*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(32*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                BlowUp = true,
                                SpeciesId = 2,
                                DirX = 1,
                                DirY = 0,
                                Hardness = 5,
                                ExplosionFrames = 25,
                                BType = BulletType.Melee,
                                ActiveVfxSpeciesId = VfxFireSlashActive.SpeciesId,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )),

                    new KeyValuePair<int, Skill>(18, new Skill{
                        RecoveryFrames = 14,
                        RecoveryFramesOnBlock = 10,
                        RecoveryFramesOnHit = 10,
                        MpDelta = 50,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = Atk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 2,
                                ActiveFrames = 600,
                                HitStunFrames = 6,
                                BlockStunFrames = 4,
                                Damage = 4,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(28*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 4,
                                Speed = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 2,
                                DirY = 0,
                                Hardness = 4,
                                AllowsWalking = true,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
                            }
                        )
                    ),

                    new KeyValuePair<int, Skill>(19, new Skill{
                        RecoveryFrames = 14,
                        RecoveryFramesOnBlock = 10,
                        RecoveryFramesOnHit = 10,
                        MpDelta = 50,
                        TriggerType = SkillTriggerType.RisingEdge,
                        BoundChState = InAirAtk1
                    }
                        .AddHit(
                            new BulletConfig {
                                StartupFrames = 2,
                                ActiveFrames = 600,
                                HitStunFrames = 6,
                                BlockStunFrames = 4,
                                Damage = 4,
                                PushbackVelX = (int)(0.5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                PushbackVelY = NO_LOCK_VEL,
                                SelfLockVelX = NO_LOCK_VEL,
                                SelfLockVelY = NO_LOCK_VEL,
                                HitboxOffsetX = (int)(28*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxOffsetY = (int)(8*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeX = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                HitboxSizeY = (int)(16*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SpeciesId = 4,
                                Speed = (int)(10*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                DirX = 2,
                                DirY = 0,
                                Hardness = 4,
                                ExplosionFrames = 30,
                                BType = BulletType.Fireball,
                                ExplosionVfxSpeciesId = VfxFireExplodingBig.SpeciesId,
                                CollisionTypeMask = COLLISION_B_M_FIREBALL_INDEX_PREFIX
                            }
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
                                SelfLockVelX = (int)(5f*COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
                                SelfLockVelY = 0,
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
                                CollisionTypeMask = COLLISION_MELEE_BULLET_INDEX_PREFIX
                            }
                        )
                    )
                }
        );

        public static int FindSkillId(int patternId, CharacterDownsync currCharacterDownsync, int speciesId) {
            bool notRecovered = inNonInertiaFramesToRecover(currCharacterDownsync);
            switch (speciesId) {
                case 0:
                    switch (patternId) {
                        case 1:
                        case 2: // Including "case 2" here as a "no-skill fallback" if attack button is pressed
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
                        case 3:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 255; // A fallback to "InAirAtk1"
                                } else {
                                    return 4;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case 5:
                            // Dashing is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            // Air-dash is allowed for this speciesId
                            return 11;
                        default:
                            return NO_SKILL;
                    }
                case 1:
                    switch (patternId) {
                        case 1:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 5;
                            } else {
                                return NO_SKILL;
                            }
                        case 2:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 12;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case 2:
                    switch (patternId) {
                        case 1:
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
                                var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
                                if (null == currBulletConfig || !currBulletConfig.CancelTransit.ContainsKey(patternId)) return NO_SKILL;

                                if (!(currBulletConfig.CancellableStFrame <= currCharacterDownsync.FramesInChState && currCharacterDownsync.FramesInChState < currBulletConfig.CancellableEdFrame)) return NO_SKILL;
                                return currBulletConfig.CancelTransit[patternId];
                            }
                        case 2:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 256; // A fallback to "InAirAtk1" 
                                } else {
                                    return 8;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case 3:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 256; // A fallback to "InAirAtk1" 
                                } else {
                                    return 9;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case 5:
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
                case 3:
                    switch (patternId) {
                        case 1:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 16;
                            } else {
                                return NO_SKILL;
                            }
                        case 2:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 17;
                            } else {
                                return NO_SKILL;
                            }
                        case 3:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 13;
                            } else {
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case 4:
                    switch (patternId) {
                        case 1:
                        case 2:
                        case 3:
                            if (!notRecovered) {
                                if (currCharacterDownsync.InAir) {
                                    return 19;
                                } else {
                                    return 18;
                                }
                            } else {
                                return NO_SKILL;
                            }
                        case 5:
                            // Sliding is already constrained by "FramesToRecover & CapturedByInertia" in "deriveOpPattern"
                            if (!currCharacterDownsync.InAir) {
                                return 20;
                            } else {
                                // Air-sliding is non-sense
                                return NO_SKILL;
                            }
                        default:
                            return NO_SKILL;
                    }
                case 4096:
                    switch (patternId) {
                        case 1:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 14;
                            } else {
                                return NO_SKILL;
                            }
                        case 3:
                            if (!notRecovered && !currCharacterDownsync.InAir) {
                                return 15;
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
