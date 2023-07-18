using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        public static TrapConfig TrapBarrier = new TrapConfig {
            SpeciesId = 1,
            SpeciesName = "TrapBarrier"
        };

        public static TrapConfig LinearSpike = new TrapConfig {
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 15,
            HitStunFrames = 25,
            HitInvinsibleFrames = 120,
            DestroyUponHit = false,
            SpeciesName = "LinearSpike"
        };

        public static TrapConfig LinearBallSpike = new TrapConfig {
            SpeciesId = 3,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 5,
            HitStunFrames = 30,
            HitInvinsibleFrames = 60,
            DestroyUponHit = false,
            SpeciesName = "LinearBallSpike"
        };

        public static ImmutableDictionary<int, TrapConfig> trapConfigs = ImmutableDictionary.Create<int, TrapConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, TrapConfig>(TrapBarrier.SpeciesId, TrapBarrier),
                    new KeyValuePair<int, TrapConfig>(LinearSpike.SpeciesId, LinearSpike),
                    new KeyValuePair<int, TrapConfig>(LinearBallSpike.SpeciesId, LinearBallSpike),
                }
        );
    }
}
