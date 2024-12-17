using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        // vfxConfigs
        public const int EXPLOSION_SPECIES_FOLLOW = 0;
        public const int EXPLOSION_SPECIES_NONE = -1;

        public static VfxConfig VfxHealing = new VfxConfig {
            SpeciesId = 1,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false, 
            UsePixelatedVer = true,
            Name = "Healing1" 
        };

        public static VfxConfig VfxMpHealing = new VfxConfig {
            SpeciesId = 2,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "MpHealing1"
        };

        public static VfxConfig VfxSmokeNDust1 = new VfxConfig {
            SpeciesId = 3,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "SmokeNDust1"
        };

        public static VfxConfig VfxSmallSting = new VfxConfig {
            SpeciesId = 4,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "SmallSting"
        };

        public static VfxConfig VfxMovingTornado = new VfxConfig {
            SpeciesId = 5,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "MovingTornado"
        };

        public static VfxConfig VfxWitchDef1Active = new VfxConfig {
            SpeciesId = 6,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "WitchDef1Active"
        };

        public static VfxConfig VfxWitchDef1Atked = new VfxConfig {
            SpeciesId = 7,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "WitchDef1Atked"
        };

        public static VfxConfig VfxWitchDef1Broken = new VfxConfig {
            SpeciesId = 8,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "WitchDef1Broken"
        };

        public static VfxConfig VfxHolyExplosion = new VfxConfig {
            SpeciesId = 9,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "HolyExplosion"
        };

        public static VfxConfig VfxLightCast1 = new VfxConfig {
            SpeciesId = 10,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "LightCast1"
        };

        public static VfxConfig VfxCastThunderTwins = new VfxConfig {
            SpeciesId = 11,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true,
            UsePixelatedVer = true,
            Name = "CastThunderTwins"
        };

        public static VfxConfig VfxMagicBarrier = new VfxConfig {
            SpeciesId = 12,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "MagicBarrier"
        };

        public static VfxConfig VfxWaterBallCast1 = new VfxConfig {
            SpeciesId = 13,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true,
            UsePixelatedVer = true,
            Name = "WaterBallCast1"
        };

        public static VfxConfig VfxHeatBeam = new VfxConfig {
            SpeciesId = 14,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "HeatBeam"
        };

        public static VfxConfig VfxLightBeam = new VfxConfig {
            SpeciesId = 15,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "LightBeam"
        };

        public static VfxConfig VfxSharedChargingPreparation = new VfxConfig {
            SpeciesId = 16,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "SharedChargingPreparation"
        };

        public static VfxConfig VfxThunderCharged = new VfxConfig {
            SpeciesId = 17,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "ThunderCharged"
        };

        public static VfxConfig VfxWaterCharged = new VfxConfig {
            SpeciesId = 18,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "WaterCharged"
        };

        public static VfxConfig VfxLightCharged = new VfxConfig {
            SpeciesId = 19,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "LightCharged"
        };

        public static VfxConfig VfxIceCharged = new VfxConfig {
            SpeciesId = 20,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true,
            Name = "IceCharged"
        };

        public static VfxConfig VfxPistolSpark = new VfxConfig {
            SpeciesId = 21,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true, 
            UsePixelatedVer = true,
            Name = "PistolSpark" 
        };

        public static VfxConfig VfxHoppingBolt = new VfxConfig {
            SpeciesId = 22,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = false,
            OnBullet = true,
            UsePixelatedVer = true,
            Name = "HoppingBolt"
        };

        public static VfxConfig VfxThunderStrikeStart = new VfxConfig {
            SpeciesId = 23,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true,
            UsePixelatedVer = true,
            Name = "ThunderStrikeStart"
        };

        public static VfxConfig VfxStoneDropperStart = new VfxConfig {
            SpeciesId = 24,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true,
            UsePixelatedVer = true,
            Name = "StoneDropperStart"
        };

        public static ImmutableDictionary<int, VfxConfig> pixelatedVfxDict = ImmutableDictionary.Create<int, VfxConfig>().AddRange(
             new[]
             {
                    new KeyValuePair<int, VfxConfig>(VfxHealing.SpeciesId, VfxHealing),
                    new KeyValuePair<int, VfxConfig>(VfxMpHealing.SpeciesId, VfxMpHealing),
                    new KeyValuePair<int, VfxConfig>(VfxSmokeNDust1.SpeciesId, VfxSmokeNDust1),
                    new KeyValuePair<int, VfxConfig>(VfxSmallSting.SpeciesId, VfxSmallSting),
                    new KeyValuePair<int, VfxConfig>(VfxMovingTornado.SpeciesId, VfxMovingTornado),
                    new KeyValuePair<int, VfxConfig>(VfxWitchDef1Active.SpeciesId, VfxWitchDef1Active),
                    new KeyValuePair<int, VfxConfig>(VfxWitchDef1Atked.SpeciesId, VfxWitchDef1Atked),
                    new KeyValuePair<int, VfxConfig>(VfxWitchDef1Broken.SpeciesId, VfxWitchDef1Broken),
                    new KeyValuePair<int, VfxConfig>(VfxHolyExplosion.SpeciesId, VfxHolyExplosion),
                    new KeyValuePair<int, VfxConfig>(VfxLightCast1.SpeciesId, VfxLightCast1),
                    new KeyValuePair<int, VfxConfig>(VfxCastThunderTwins.SpeciesId, VfxCastThunderTwins),
                    new KeyValuePair<int, VfxConfig>(VfxMagicBarrier.SpeciesId, VfxMagicBarrier),
                    new KeyValuePair<int, VfxConfig>(VfxWaterBallCast1.SpeciesId, VfxWaterBallCast1),
                    new KeyValuePair<int, VfxConfig>(VfxHeatBeam.SpeciesId, VfxHeatBeam),
                    new KeyValuePair<int, VfxConfig>(VfxLightBeam.SpeciesId, VfxLightBeam),
                    new KeyValuePair<int, VfxConfig>(VfxSharedChargingPreparation.SpeciesId, VfxSharedChargingPreparation),
                    new KeyValuePair<int, VfxConfig>(VfxThunderCharged.SpeciesId, VfxThunderCharged),
                    new KeyValuePair<int, VfxConfig>(VfxWaterCharged.SpeciesId, VfxWaterCharged),
                    new KeyValuePair<int, VfxConfig>(VfxLightCharged.SpeciesId, VfxLightCharged),
                    new KeyValuePair<int, VfxConfig>(VfxIceCharged.SpeciesId, VfxIceCharged),
                    new KeyValuePair<int, VfxConfig>(VfxPistolSpark.SpeciesId, VfxPistolSpark),
                    new KeyValuePair<int, VfxConfig>(VfxHoppingBolt.SpeciesId, VfxHoppingBolt),
                    new KeyValuePair<int, VfxConfig>(VfxThunderStrikeStart.SpeciesId, VfxThunderStrikeStart),
                    new KeyValuePair<int, VfxConfig>(VfxStoneDropperStart.SpeciesId, VfxStoneDropperStart),
             }
        );

        // debuffConfigs
        public const int DEBUFF_ARR_IDX_FROZEN = 0; // Used to access "characterDownsync.DebuffList" to quickly detect conflicting debuffs 

        public static DebuffConfig ShortFrozen = new DebuffConfig {
            SpeciesId = 1,
            StockType = BuffStockType.Timed,
            Stock = 120,
            Type = DebuffType.FrozenPositionLocked,
            ArrIdx = DEBUFF_ARR_IDX_FROZEN
        };

        public static ImmutableDictionary<uint, DebuffConfig> debuffConfigs = ImmutableDictionary.Create<uint, DebuffConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, DebuffConfig>(ShortFrozen.SpeciesId, ShortFrozen)
                }
        );

        // buffConfigs
        public static BuffConfig ShortFreezer = new BuffConfig {
            SpeciesId = 1,
            StockType = BuffStockType.Timed,
            Stock = 480,
            XformChSpeciesId = SPECIES_NONE_CH,
            CharacterVfxSpeciesId = NO_VFX_ID, // TODO
        }.AddAssociatedDebuff(ShortFrozen);

        public static BuffConfig XformToLightSpearWoman = new BuffConfig {
            SpeciesId = 2,
            StockType = BuffStockType.Timed,
            Stock = 30 * BATTLE_DYNAMICS_FPS,
            XformChSpeciesId = SPECIES_LIGHTSPEARWOMAN,  
            CharacterVfxSpeciesId = NO_VFX_ID // TODO
        };

        public static BuffConfig XformToWandWitchGirl = new BuffConfig {
            SpeciesId = 3,
            StockType = BuffStockType.Timed,
            Stock = 30 * BATTLE_DYNAMICS_FPS,
            XformChSpeciesId = SPECIES_WANDWITCHGIRL,  
            CharacterVfxSpeciesId = NO_VFX_ID // TODO
        };

        public static BuffConfig XformToFireSwordMan = new BuffConfig {
            SpeciesId = 4,
            StockType = BuffStockType.Timed,
            Stock = 30 * BATTLE_DYNAMICS_FPS,
            XformChSpeciesId = SPECIES_FIRESWORDMAN,
            CharacterVfxSpeciesId = NO_VFX_ID // TODO
        };

        public static ImmutableDictionary<uint, BuffConfig> buffConfigs = ImmutableDictionary.Create<uint, BuffConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, BuffConfig>(ShortFreezer.SpeciesId, ShortFreezer),
                    new KeyValuePair<uint, BuffConfig>(XformToLightSpearWoman.SpeciesId, XformToLightSpearWoman),
                    new KeyValuePair<uint, BuffConfig>(XformToWandWitchGirl.SpeciesId, XformToWandWitchGirl),
                    new KeyValuePair<uint, BuffConfig>(XformToFireSwordMan.SpeciesId, XformToFireSwordMan),
                }
        );

        public static ConsumableConfig HpRefillSmall = new ConsumableConfig {
            SpeciesId = 1,
            RefillDelta = 50,
            VfxIdOnPicker = VfxHealing.SpeciesId,
            IsPixelatedVfxOnPicker = true
        };

        public static ConsumableConfig HpRefillMiddle = new ConsumableConfig {
            SpeciesId = 2,
            RefillDelta = 80,
            VfxIdOnPicker = VfxHealing.SpeciesId,
            IsPixelatedVfxOnPicker = true
        };

        public static ConsumableConfig MpRefillSmall = new ConsumableConfig {
            SpeciesId = 3,
            RefillDelta = 800,
            VfxIdOnPicker = VfxMpHealing.SpeciesId,
            IsPixelatedVfxOnPicker = true
        };

        public static ConsumableConfig MpRefillMiddle = new ConsumableConfig {
            SpeciesId = 4,
            RefillDelta = 1600,
            VfxIdOnPicker = VfxMpHealing.SpeciesId,
            IsPixelatedVfxOnPicker = true
        };

        public static ImmutableDictionary<uint, ConsumableConfig> consumableConfigs = ImmutableDictionary.Create<uint, ConsumableConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, ConsumableConfig>(HpRefillSmall.SpeciesId, HpRefillSmall),
                    new KeyValuePair<uint, ConsumableConfig>(HpRefillMiddle.SpeciesId, HpRefillMiddle),
                    new KeyValuePair<uint, ConsumableConfig>(MpRefillSmall.SpeciesId, MpRefillSmall),
                    new KeyValuePair<uint, ConsumableConfig>(MpRefillMiddle.SpeciesId, MpRefillMiddle),
                }
        );

        public static PickableSkillConfig PickableSkill1 = new PickableSkillConfig {
            SkillId = 1,
            VfxIdOnPicker = VfxMpHealing.SpeciesId,
            IsPixelatedVfxOnPicker = true
        };

        public static ImmutableDictionary<uint, PickableSkillConfig> pickableSkillConfigs = ImmutableDictionary.Create<uint, PickableSkillConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, PickableSkillConfig>(PickableSkill1.SkillId, PickableSkill1)
                }
        );

        public static ImmutableDictionary<uint, ImmutableDictionary<uint, uint>> pickableConsumableIdMapper = ImmutableDictionary.Create<uint, ImmutableDictionary<uint, uint>>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, ImmutableDictionary<uint, uint>>(HpRefillSmall.SpeciesId, 
                        ImmutableDictionary.Create<uint, uint>()
                        .AddRange(
                            new[]
                            {
                            new KeyValuePair<uint, uint>(SPECIES_BLADEGIRL, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WITCHGIRL, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_MAGSWORDGIRL, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BRIGHTWITCH, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BOUNTYHUNTER, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SPEARWOMAN, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_LIGHTSPEARWOMAN, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WANDWITCHGIRL, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SWORDMAN, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_FIRESWORDMAN, HpRefillSmall.SpeciesId),
                            }
                        )
                    ),
                    new KeyValuePair<uint, ImmutableDictionary<uint, uint>>(MpRefillSmall.SpeciesId, 
                        ImmutableDictionary.Create<uint, uint>()
                        .AddRange(
                            new[]
                            {
                            new KeyValuePair<uint, uint>(SPECIES_BLADEGIRL, HpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WITCHGIRL, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_MAGSWORDGIRL, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BRIGHTWITCH, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BOUNTYHUNTER, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SPEARWOMAN, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_LIGHTSPEARWOMAN, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WANDWITCHGIRL, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SWORDMAN, MpRefillSmall.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_FIRESWORDMAN, MpRefillSmall.SpeciesId),
                            }
                        )
                    ),
                    new KeyValuePair<uint, ImmutableDictionary<uint, uint>>(HpRefillMiddle.SpeciesId, 
                        ImmutableDictionary.Create<uint, uint>()
                        .AddRange(
                            new[]
                            {
                            new KeyValuePair<uint, uint>(SPECIES_BLADEGIRL, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WITCHGIRL, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_MAGSWORDGIRL, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BRIGHTWITCH, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BOUNTYHUNTER, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SPEARWOMAN, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_LIGHTSPEARWOMAN, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WANDWITCHGIRL, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SWORDMAN, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_FIRESWORDMAN, HpRefillMiddle.SpeciesId),
                            }
                        )
                    ),
                    new KeyValuePair<uint, ImmutableDictionary<uint, uint>>(MpRefillMiddle.SpeciesId, 
                        ImmutableDictionary.Create<uint, uint>()
                        .AddRange(
                            new[]
                            {
                            new KeyValuePair<uint, uint>(SPECIES_BLADEGIRL, HpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WITCHGIRL, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_MAGSWORDGIRL, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BRIGHTWITCH, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_BOUNTYHUNTER, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SPEARWOMAN, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_LIGHTSPEARWOMAN, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_WANDWITCHGIRL, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_SWORDMAN, MpRefillMiddle.SpeciesId),
                            new KeyValuePair<uint, uint>(SPECIES_FIRESWORDMAN, MpRefillMiddle.SpeciesId),
                            }
                        )
                    ),
                }
        ); 

        public static ImmutableDictionary<uint, ImmutableDictionary<uint, uint>> pickableSkillIdMapper = ImmutableDictionary.Create<uint, ImmutableDictionary<uint, uint>>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, ImmutableDictionary<uint, uint>>(1, 
                        ImmutableDictionary.Create<uint, uint>()
                        .AddRange(
                            new[]
                            {
                            new KeyValuePair<uint, uint>(SPECIES_BLADEGIRL, 59),
                            new KeyValuePair<uint, uint>(SPECIES_MAGSWORDGIRL, 65),
                            new KeyValuePair<uint, uint>(SPECIES_BRIGHTWITCH, 49),
                            new KeyValuePair<uint, uint>(SPECIES_BOUNTYHUNTER, 58),
                            }
                        )
                    ),
                }
        );

        public static ImmutableDictionary<uint, ImmutableDictionary<uint, uint>> pickableSkillIdAirMapper = ImmutableDictionary.Create<uint, ImmutableDictionary<uint, uint>>().AddRange(
                new[]
                {
                    new KeyValuePair<uint, ImmutableDictionary<uint, uint>>(1,
                        ImmutableDictionary.Create<uint, uint>()
                        .AddRange(
                            new[]
                            {
                            new KeyValuePair<uint, uint>(SPECIES_BLADEGIRL, 59),
                            new KeyValuePair<uint, uint>(SPECIES_MAGSWORDGIRL, 65),
                            new KeyValuePair<uint, uint>(SPECIES_BRIGHTWITCH, NO_SKILL),
                            new KeyValuePair<uint, uint>(SPECIES_BOUNTYHUNTER, 58),
                            }
                        )
                    ),
                }
        );
    }
}
