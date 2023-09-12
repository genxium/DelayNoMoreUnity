using System.Collections.Generic;
using System.Collections.Immutable;

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

        public static TrapConfig VerticalTrapBarrier = new TrapConfig {
            SpeciesId = 4,
            SpeciesName = "VerticalTrapBarrier"
        };

        public static TrapConfig SawSmall = new TrapConfig {
            SpeciesId = 5,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 8,
            HitStunFrames = 30,
            HitInvinsibleFrames = 60,
            DestroyUponHit = false,
            SpeciesName = "SawSmall"
        };

        public static TrapConfig SawBig = new TrapConfig {
            SpeciesId = 6,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 16,
            HitStunFrames = 30,
            HitInvinsibleFrames = 60,
            DestroyUponHit = false,
            SpeciesName = "SawBig"
        };

        public static ImmutableDictionary<int, TrapConfig> trapConfigs = ImmutableDictionary.Create<int, TrapConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, TrapConfig>(TrapBarrier.SpeciesId, TrapBarrier),
                    new KeyValuePair<int, TrapConfig>(LinearSpike.SpeciesId, LinearSpike),
                    new KeyValuePair<int, TrapConfig>(LinearBallSpike.SpeciesId, LinearBallSpike),
                    new KeyValuePair<int, TrapConfig>(VerticalTrapBarrier.SpeciesId, VerticalTrapBarrier),
                    new KeyValuePair<int, TrapConfig>(SawSmall.SpeciesId, SawSmall),
                    new KeyValuePair<int, TrapConfig>(SawBig.SpeciesId, SawBig),
                }
        );
    }
}
