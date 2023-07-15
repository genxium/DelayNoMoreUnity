using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        public static TrapConfig TrapBarrier = new TrapConfig {
            SpeciesId = 1
        };

        public static TrapConfig LinearSpike = new TrapConfig {
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 15,
            HitStunFrames = 10,
            HitInvinsibleFrames = 120,
            DestroyUponHit = false 
        };
    }
}
