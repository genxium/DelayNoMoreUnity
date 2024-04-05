using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        // vfxConfigs
        public const int EXPLOSION_SPECIES_FOLLOW = 0;
        public const int EXPLOSION_SPECIES_NONE = -1;

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

        public static VfxConfig VfxPistolBulletExploding = new VfxConfig {
            SpeciesId = 8,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true
        };

        public static VfxConfig VfxSlashExploding = new VfxConfig {
            SpeciesId = 9,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = false,
            OnBullet = true
        };

        public static VfxConfig VfxIceLingering = new VfxConfig {
            SpeciesId = 10,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.Repeating,
            OnCharacter = true,
            OnBullet = false
        };

        public static VfxConfig VfxXform = new VfxConfig {
            SpeciesId = 11,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false
        };

        public static VfxConfig VfxHealing = new VfxConfig {
            SpeciesId = 1,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false, 
            UsePixelatedVer = true
        };

        public static VfxConfig VfxMpHealing = new VfxConfig {
            SpeciesId = 2,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDurationType.OneOff,
            OnCharacter = true,
            OnBullet = false,
            UsePixelatedVer = true
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
                    new KeyValuePair<int, VfxConfig>(VfxPistolBulletExploding.SpeciesId, VfxPistolBulletExploding),
                    new KeyValuePair<int, VfxConfig>(VfxSlashExploding.SpeciesId, VfxSlashExploding),
                    new KeyValuePair<int, VfxConfig>(VfxIceLingering.SpeciesId, VfxIceLingering),
                    new KeyValuePair<int, VfxConfig>(VfxXform.SpeciesId, VfxXform),
             }
        );

        public static ImmutableDictionary<int, VfxConfig> pixelatedVfxDict = ImmutableDictionary.Create<int, VfxConfig>().AddRange(
             new[]
             {
                    new KeyValuePair<int, VfxConfig>(VfxHealing.SpeciesId, VfxHealing),
                    new KeyValuePair<int, VfxConfig>(VfxMpHealing.SpeciesId, VfxMpHealing),
             }
        );

        // debuffConfigs
        public const int DEBUFF_ARR_IDX_FROZEN = 0; // Used to access "characterDownsync.DebuffList" to quickly detect conflicting debuffs 

        public static DebuffConfig ShortFrozen = new DebuffConfig {
            SpeciesId = 1,
            StockType = BuffStockType.Timed,
            Stock = 180,
            Type = DebuffType.FrozenPositionLocked,
            ArrIdx = DEBUFF_ARR_IDX_FROZEN
        };

        public static ImmutableDictionary<int, DebuffConfig> debuffConfigs = ImmutableDictionary.Create<int, DebuffConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, DebuffConfig>(ShortFrozen.SpeciesId, ShortFrozen)
                }
        );

        // buffConfigs
        public static BuffConfig ShortFreezer = new BuffConfig {
            SpeciesId = 1,
            StockType = BuffStockType.Timed,
            Stock = 480,
            XformChSpeciesId = SPECIES_NONE_CH,
            CharacterVfxSpeciesId = VfxIceLingering.SpeciesId
        }.AddAssociatedDebuff(ShortFrozen);

        public static BuffConfig XformToSuperKnifeGirl = new BuffConfig {
            SpeciesId = 2,
            StockType = BuffStockType.Timed,
            Stock = 900,
            XformChSpeciesId = SPECIES_SUPERKNIFEGIRL,  
            CharacterVfxSpeciesId = VfxXform.SpeciesId // TODO: Use another spell launch vfx
        };

        public static ImmutableDictionary<int, BuffConfig> buffConfigs = ImmutableDictionary.Create<int, BuffConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, BuffConfig>(ShortFreezer.SpeciesId, ShortFreezer),
                    new KeyValuePair<int, BuffConfig>(XformToSuperKnifeGirl.SpeciesId, XformToSuperKnifeGirl),
                }
        );

        public static ConsumableConfig MpRefillSmall = new ConsumableConfig {
            SpeciesId = 1,
            RefillDelta = 800,
        };

        public static ConsumableConfig MpRefillMiddle = new ConsumableConfig {
            SpeciesId = 2,
            RefillDelta = 1600,
        };

        public static ImmutableDictionary<int, ConsumableConfig> consumableConfigs = ImmutableDictionary.Create<int, ConsumableConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, ConsumableConfig>(MpRefillSmall.SpeciesId, MpRefillSmall),
                    new KeyValuePair<int, ConsumableConfig>(MpRefillMiddle.SpeciesId, MpRefillMiddle),
                }
        );
    }
}
