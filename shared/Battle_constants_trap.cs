using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        /*
        What makes a trap a "trap not bullet or npc"?

        1. It can subscribe to a trigger multiple times, e.g. a door that can open and close by triggers.
        2. It can provide "slipJump" (either static or not).
        3. It must have no vision, or use a Trigger to mimic one if needed. A movable vision implementation is reserved only for bullet and npc.
        4. It's indestructible -- which is imposed by the use of a static "trapLocalIdToColliderAttrs" in "Step(...)", a design legacy not easy to change for now, i.e. "_leftDestroyedTraps" needs a remap in "trapLocalIdToColliderAttrs" to work.   
        */
        /*
        [WORKAROUND@2025-01-06]
        
        There's a proposal to enable "_leftDestroyedTraps" by replacing "trapLocalIdToColliderAttrs" with "trapSpeciesIdToColliderAttrs" -- and yes it'll work for that purpose: "destructible trap". However this workaround would also disable the capability to configure different collider boxes for different trap instances of a same "trapSpeciesId", e.g. static spike-walls and conveyors of different lengths.

        By the time of writing, mocking a "destructible trap" by a "bullet" or an "npc" works well for me.
        */
        public static int MAGIC_FRAMES_FOR_TRAP_TO_WAIVE_PATROL_CUE = 45;
        public static TrapConfig TrapBarrier = new TrapConfig {
            SpeciesId = 1,
            SpeciesName = "TrapBarrier",
            NoXFlipRendering = true,
        };

        public static TrapConfig LinearSpike = new TrapConfig {
            SpeciesId = 2,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 15,
            HitStunFrames = 25,
            HitInvinsibleFrames = 45,
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
            HitInvinsibleFrames = 45,
            SpeciesName = "LinearBallSpike",
            Hardness = 6, 
        };

        public static TrapConfig VerticalTrapBarrier = new TrapConfig {
            SpeciesId = 4,
            SpeciesName = "VerticalTrapBarrier",
            NoXFlipRendering = true,
        };

        public static TrapConfig SawSmall = new TrapConfig {
            SpeciesId = 5,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 8,
            HitStunFrames = 30,
            HitInvinsibleFrames = 45,
            SpeciesName = "SawSmall",
            Hardness = 8, 
            PatrolCueRequiresFullContain = true,
            NoXFlipRendering = true,
        };

        public static TrapConfig SawBig = new TrapConfig {
            SpeciesId = 6,
            ExplosionSpeciesId = 2,
            ExplosionFrames = 15,
            BlowUp = false,
            Damage = 16,
            HitStunFrames = 30,
            HitInvinsibleFrames = 45,
            SpeciesName = "SawBig",
            Hardness = 9, 
            PatrolCueRequiresFullContain = true,
            NoXFlipRendering = true,
        };

        public static TrapConfig EscapeDoor = new TrapConfig {
            SpeciesId = 7,
            SpeciesName = "EscapeDoor",
        };

        public static TrapConfig GreenGate = new TrapConfig {
            SpeciesId = 8,
            Deactivatable = true,
            SpeciesName = "GreenGate",
            DeactivateUponTriggered = true,
        };

        public static TrapConfig RedGate = new TrapConfig {
            SpeciesId = 9,
            Deactivatable = true,
            SpeciesName = "RedGate",
            DeactivateUponTriggered = true,
        };

        public static TrapConfig Jumper1 = new TrapConfig {
            SpeciesId = 10,
            SpeciesName = "Jumper1",
            Atk1UponTriggered = true,
            Atk1SkillId = JumperImpact1Skill.Id,
        };

        public static TrapConfig Fort = new TrapConfig {
            SpeciesId = 11,
            SpeciesName = "Fort",
            Atk1UponTriggered = true,
            Atk1SkillId = FortLv1Fireball.Id,
        };

        public static TrapConfig LongConveyorToL = new TrapConfig {
            SpeciesId = 12,
            SpeciesName = "LongConveyorToL",
            ConstFrictionVelXTop = (int)(-2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            ConstFrictionVelXBottom = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
        };

        public static TrapConfig LongConveyorToR = new TrapConfig {
            SpeciesId = 13,
            SpeciesName = "LongConveyorToR",
            ConstFrictionVelXTop = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            ConstFrictionVelXBottom = (int)(-2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
        };

        public static TrapConfig ShortConveyorToL = new TrapConfig {
            SpeciesId = 14,
            SpeciesName = "ShortConveyorToL",
            ConstFrictionVelXTop = (int)(-2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            ConstFrictionVelXBottom = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
        };

        public static TrapConfig ShortConveyorToR = new TrapConfig {
            SpeciesId = 15,
            SpeciesName = "ShortConveyorToR",
            ConstFrictionVelXTop = (int)(2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
            ConstFrictionVelXBottom = (int)(-2.0f * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO),
        };

        public static TrapConfig RotaryBarrier = new TrapConfig {
            SpeciesId = 16,
            SpeciesName = "RotaryBarrier",
            SpinAnchorX = 12.0f,
            SpinAnchorY = 8.0f,
            IsRotary = true,
            AngularFrameVelCos = (float)Math.Cos(+1f / (Math.PI * BATTLE_DYNAMICS_FPS)), 
            AngularFrameVelSin = (float)Math.Sin(+1f / (Math.PI * BATTLE_DYNAMICS_FPS)), 
            PatrolCueRequiresFullContain = true,
        };

        public static TrapConfig VerticalRotaryBarrier = new TrapConfig {
            SpeciesId = 17,
            SpeciesName = "VerticalRotaryBarrier",
            SpinAnchorX = 8.0f,
            SpinAnchorY = 6.0f,
            IsRotary = true,
            AngularFrameVelCos = (float)Math.Cos(+1f / (Math.PI * BATTLE_DYNAMICS_FPS)), 
            AngularFrameVelSin = (float)Math.Sin(+1f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            IntrinsicSpinCos = 0,
            IntrinsicSpinSin = 1,
            PatrolCueRequiresFullContain = true,
        };

        public static TrapConfig VerticalRotaryBarrierLong = new TrapConfig {
            SpeciesId = 18,
            SpeciesName = "VerticalRotaryBarrierLong",
            SpinAnchorX = 8.0f,
            SpinAnchorY = 6.0f,
            IsRotary = true,
            AngularFrameVelCos = (float)Math.Cos(+6f / (Math.PI * BATTLE_DYNAMICS_FPS)), 
            AngularFrameVelSin = (float)Math.Sin(+6f / (Math.PI * BATTLE_DYNAMICS_FPS)),
            IntrinsicSpinCos = 0,
            IntrinsicSpinSin = 1,
            PatrolCueRequiresFullContain = true,
        };

        public static TrapConfig SmallBallEmitter = new TrapConfig {
            SpeciesId = 19,
            SpeciesName = "SmallBallEmitter",
            Atk1UponTriggered = true,
            SpinAnchorX = 0.0f,
            SpinAnchorY = 0.0f,
            IsRotary = true,
            Atk1SkillId = 130,
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
                    new KeyValuePair<int, TrapConfig>(RedGate.SpeciesId, RedGate),
                    new KeyValuePair<int, TrapConfig>(Jumper1.SpeciesId, Jumper1),
                    new KeyValuePair<int, TrapConfig>(Fort.SpeciesId, Fort),
                    new KeyValuePair<int, TrapConfig>(LongConveyorToL.SpeciesId, LongConveyorToL),
                    new KeyValuePair<int, TrapConfig>(LongConveyorToR.SpeciesId, LongConveyorToR),
                    new KeyValuePair<int, TrapConfig>(ShortConveyorToL.SpeciesId, ShortConveyorToL),
                    new KeyValuePair<int, TrapConfig>(ShortConveyorToR.SpeciesId, ShortConveyorToR),
                    new KeyValuePair<int, TrapConfig>(RotaryBarrier.SpeciesId, RotaryBarrier),
                    new KeyValuePair<int, TrapConfig>(VerticalRotaryBarrier.SpeciesId, VerticalRotaryBarrier),
                    new KeyValuePair<int, TrapConfig>(VerticalRotaryBarrierLong.SpeciesId, VerticalRotaryBarrierLong),
                    new KeyValuePair<int, TrapConfig>(SmallBallEmitter.SpeciesId, SmallBallEmitter),
                }
        );
    }
}
