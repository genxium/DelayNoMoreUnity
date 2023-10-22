using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        // debuffConfigs
        public static DebuffConfig ShortFrozen = new DebuffConfig {
            SpeciesId = 1,
            StockType = BuffStockType.Timed,
            Stock = 180,
            Type = DebuffType.FrozenPositionLocked
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
            Stock = 600
        }.AddAssociatedDebuff(ShortFrozen);
    }
}
