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
            Destroyable = false,
            SpeciesName = "LinearSpike",
            Hardness = 6, 
        };

        public static TrapConfig LinearBallSpike = new TrapConfig {
            SpeciesId = 3,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 5,
            HitStunFrames = 30,
            HitInvinsibleFrames = 60,
            Destroyable = false,
            SpeciesName = "LinearBallSpike",
            Hardness = 6, 
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
            Destroyable = false,
            SpeciesName = "SawSmall",
            Hardness = 8, 
        };

        public static TrapConfig SawBig = new TrapConfig {
            SpeciesId = 6,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 16,
            HitStunFrames = 30,
            HitInvinsibleFrames = 60,
            Destroyable = false,
            SpeciesName = "SawBig",
            Hardness = 9, 
        };

        public static TrapConfig EscapeDoor = new TrapConfig {
            SpeciesId = 7,
            Destroyable = false,
            SpeciesName = "EscapeDoor",
        };

        public static TrapConfig GreenGate = new TrapConfig {
            SpeciesId = 8,
            Destroyable = true,
            SpeciesName = "GreenGate",
            DestroyUponTriggered = true,
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
                    new KeyValuePair<int, TrapConfig>(GreenGate.SpeciesId, GreenGate),
                }
        );
    }
}
