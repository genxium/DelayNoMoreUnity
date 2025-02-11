using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public struct CharacterConfig {
        // [WARNING] All fields of this class should be deemed as "initial values", and could be changed during a battle by whatever powerup/debuff :)
        public uint SpeciesId;
        public string SpeciesName;

        public int Hp;
        public int Mp;

        public int InAirIdleFrameIdxTurningPoint;
        public int InAirIdleFrameIdxTurnedCycle;

        public int LayDownFrames;
        public int LayDownFramesToRecover;

        public int GetUpInvinsibleFrames;
        public int GetUpFramesToRecover;

        public int Speed;
        public int DownSlopePrimerVelY; // this value should be big enough such that smooth transition is enabled for the steepest slope, but small enough such that no character would penetrate the thinnest barrier
        public int MpRegenPerInterval;
        public int MpRegenInterval; // an integer of RoomDownsyncFrame count

        public int JumpingInitVelY;
        public int InertiaFramesToRecover;
        public bool DashingEnabled;
        public bool SlidingEnabled;
        public bool OnWallEnabled;
        public bool CrouchingEnabled; // Considering that a character might be forced to crouch, "CrouchAtked1" is a MUST if "true == CrouchingEnabled"
        public bool CrouchingAtkEnabled; 
        public int WallJumpingFramesToRecover;
        public int WallJumpingInitVelX;
        public int WallJumpingInitVelY;
        public int WallSlidingVelY;
        public int MinFallingVelY;
        public int MaxAscendingVelY;

        public bool UseInventoryBtnB;

        public bool GroundDodgeEnabledByIvSlotCInBlockStun;
        public int GroundDodgeEnabledByRdfCntFromBeginning;
        public int GroundDodgedFramesToRecover;
        public int GroundDodgedFramesInvinsible;
        public int GroundDodgedSpeed; // TODO: For better flexibility, should allow "configurable list of speed keyframes", like that of "Hurtboxes"  

        /**
         * Collision boxes
         * 
         * [TODO: Dynamic Hurtboxes]
         * If a character is to possess multiple hurtboxes that undergo affine transforms (translate, rotate, scale) during certain (CharacterState, FramesInChState), we should assign a set of "initial hurtboxes @ CharacterState" as well as a set of "transformation matrices per frame @ CharacterState" in "CharacterConfig"; in "CharacterDownsync" we should also memorize "the set of currently accumulated transformation matrices for hurtboxes @ CharacterState" for fast calculation.  
         */
        public int VisionOffsetX;
        public int VisionOffsetY;
        public int VisionSizeX;
        public int VisionSizeY;

        public int DefaultSizeX;
        public int DefaultSizeY;

        public int ShrinkedSizeX;
        public int ShrinkedSizeY;

        public int LayDownSizeX;
        public int LayDownSizeY;

        public int DyingSizeX;
        public int DyingSizeY;

        public int DimmedSizeX;
        public int DimmedSizeY;

        public Dictionary<CharacterState, List<(ConvexPolygon, BoxInterpolationType)>> Hurtboxes;

        // Collision masks
        public bool AntiGravityWhenIdle;
        public int AntiGravityFramesLingering;

        public bool OmitGravity;
        public bool OmitSoftPushback;
        public bool RepelSoftPushback;
        public ulong CollisionTypeMask;

        public bool HasTurnAroundAnim;
        public bool HasDimmedAnim;
        public bool HasAwakingAnim;
        public bool HasWalkStoppingAnim; 
        public bool HasInAirWalkStoppingAnim; 
        public bool LayDownToRecoverFromDimmed;

        public int Hardness;

        public int ProactiveJumpStartupFrames;
        
        public uint DefaultAirJumpQuota;
        public uint DefaultAirDashQuota;
        public uint DefaultDef1Quota;

        public bool IsolatedAirJumpAndDashQuota; // default is false, in most cases AirJump and AirDash quotas are deduced together (but default initial quota can be different) 

        public int AirJumpVfxSpeciesId;

        public List<InventorySlot> InitInventorySlots;

        public float SlipJumpThresHoldBelowTopFace; 
        public int SlipJumpThresHoldBelowTopFaceV;
        public int SlipJumpCharacterDropVirtual; 
        
        public uint TransformIntoSpeciesIdUponDeath;
        public bool JumpHoldingToFly;
        public bool HasDef1;
        public bool HasDef1Atked1Anim;
        public int DefaultDef1BrokenFramesToRecover;
        public int Def1ActiveVfxSpeciesId;
        public int Def1AtkedVfxSpeciesId;
        public int Def1BrokenVfxSpeciesId;
        public int Def1StartupFrames;
        public float Def1DamageYield;
        public bool Def1DefiesEleWeaknessPenetration;
        public bool Def1DefiesDebuff;

        public bool WalkingAutoDef1;

        public IfaceCat Ifc;
        public ulong EleWeakness;
        public ulong EleResistance;
        public bool HasBtnBCharging;
        public int BtnBChargedVfxSpeciesId;

        public bool IsKeyCh;

        public bool AllowsSameTeamSoftPushback; // For bricks
        public int GaugeIncWhenKilled;
        
        public bool JumpingInsteadOfWalking;
        public uint VisionSearchIntervalPow2Minus1U;
        public int VisionSearchIntervalPow2Minus1;

        public bool NpcNoDefaultAirWalking;
        public bool NpcPrioritizeBulletHandling;

        public int TransformIntoFramesToRecover;
        public int TransformIntoFramesInvinsible;

        public int AwakingFramesToRecover;
        public int AwakingFramesInvinsible;

        public bool UseIdle1AsFlyingIdle;

        public ImmutableDictionary<CharacterState, int> LoopingChStates; 
    }
}
