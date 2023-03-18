using System;
using System.Collections.Generic;
using System.Text;

namespace shared {
    public struct CharacterConfig {
        public int SpeciesId;
        public string SpeciesName;

        public int InAirIdleFrameIdxTurningPoint;
        public int InAirIdleFrameIdxTurnedCycle;

        public int LayDownFrames;
        public int LayDownFramesToRecover;

        public int GetUpInvinsibleFrames;
        public int GetUpFramesToRecover;

        public int Speed;
        public int JumpingInitVelY;
        public int InertiaFramesToRecover;
        public bool DashingEnabled;
        public bool OnWallEnabled;
        public int WallJumpingFramesToRecover;
        public int WallJumpingInitVelX;
        public int WallJumpingInitVelY;
        public int WallSlidingVelY;


        public CharacterConfig(int speciesId, string speciesName, int inAirIdleFrameIdxTurningPoint, int inAirIdleFrameIdxTurnedCycle, int layDownFrames, int layDownFramesToRecover, int getUpInvinsibleFrames, int getUpFramesToRecover, int speed, int jumpingInitVelY, int inertiaFramesToRecover, bool dashingEnabled, bool onWallEnabled, int wallJumpingFramesToRecover, int wallJumpingInitVelX, int wallJumpingInitVelY, int wallSlidingVelY) {
            SpeciesId = speciesId;
            SpeciesName = speciesName;
            InAirIdleFrameIdxTurningPoint = inAirIdleFrameIdxTurningPoint;
            InAirIdleFrameIdxTurnedCycle = inAirIdleFrameIdxTurnedCycle;
            LayDownFrames = layDownFrames;
            LayDownFramesToRecover = layDownFramesToRecover;
            GetUpInvinsibleFrames = getUpInvinsibleFrames;
            GetUpFramesToRecover = getUpFramesToRecover;
            Speed = speed;
            JumpingInitVelY = jumpingInitVelY;
            InertiaFramesToRecover = inertiaFramesToRecover;
            DashingEnabled = dashingEnabled;
            OnWallEnabled = onWallEnabled;
            WallJumpingFramesToRecover = wallJumpingFramesToRecover;
            WallJumpingInitVelX = wallJumpingInitVelX;
            WallJumpingInitVelY = wallJumpingInitVelY;
            WallSlidingVelY = wallSlidingVelY;
        }
    }
}
