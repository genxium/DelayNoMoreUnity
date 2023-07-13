using System.Collections.Generic;
using System.Collections.Immutable;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        public static TrapConfig TrapBarrier = new TrapConfig {
            Id = 1,
            Quota = MAGIC_QUOTA_INFINITE,
            ProvidesHardPushback = true,
            CollisionTypeMask = COLLISION_TRAP_INDEX_PREFIX
        };

        public static TrapConfig LinearSpike = new TrapConfig {
            Id = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 15,
            HitStunFrames = 10,
            HitInvinsibleFrames = 120,
            DestroyUponHit = false, 
            Quota = MAGIC_QUOTA_INFINITE,
            ProvidesHardPushback = false,
            CollisionTypeMask = COLLISION_TRAP_INDEX_PREFIX
        };
    }
}
