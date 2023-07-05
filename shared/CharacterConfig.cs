namespace shared {
    public struct CharacterConfig {
        // [WARNING] All fields of this class should be deemed as "initial values", and could be changed during a battle by whatever powerup/debuff :)
        public int SpeciesId;
        public string SpeciesName;

        public int Hp;

        public int InAirIdleFrameIdxTurningPoint;
        public int InAirIdleFrameIdxTurnedCycle;

        public int LayDownFrames;
        public int LayDownFramesToRecover;

        public int GetUpInvinsibleFrames;
        public int GetUpFramesToRecover;

        public int Speed;
        public int DownSlopePrimerVelY; // this value should be big enough such that smooth transition is enabled for the steepest slope, but small enough such that no character would penetrate the thinest barrier
        public int MpRegenRate; // an integer for mp regeneration rate PER FRAME

        public int JumpingInitVelY;
        public int InertiaFramesToRecover;
        public bool DashingEnabled;
        public bool OnWallEnabled;
        public int WallJumpingFramesToRecover;
        public int WallJumpingInitVelX;
        public int WallJumpingInitVelY;
        public int WallSlidingVelY;

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

        public int CloseEnoughVirtualGridDistance;

        // Collision masks
        public bool OmitGravity;
        public bool OmitPushback;
        public ulong CollisionTypeMask;

        public bool HasTurnAroundAnim;
    }
}
