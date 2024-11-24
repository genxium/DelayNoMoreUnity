using System.Collections.Generic;

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
        public int MpRegenRate; // an integer for mp regeneration rate PER FRAME

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

        // Collision boxes
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

        // Collision masks
        public bool AntiGravityWhenIdle;
        public int AntiGravityFramesLingering;

        public bool OmitGravity;
        public bool OmitSoftPushback;
        public bool RepelSoftPushback;
        public ulong CollisionTypeMask;

        public bool HasTurnAroundAnim;
        public bool HasDimmedAnim;
        public bool LayDownToRecoverFromDimmed;

        public int Hardness;

        public int ProactiveJumpStartupFrames;
        
        public uint DefaultAirJumpQuota;
        public uint DefaultAirDashQuota;
        public uint DefaultDef1Quota;

        public bool IsolatedAirJumpAndDashQuota; // default is false, in most cases AirJump and AirDash quotas are deduced together (but default initial quota can be different) 

        public bool UseIsolatedAvatar;

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
    }
}
