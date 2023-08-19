namespace shared {
    public partial class Battle {
        public static VfxConfig VfxDashingActive = new VfxConfig {
            SpeciesId = 1, 
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDuringType.OneOff
        };

        public static VfxConfig VfxFireExplodingBig = new VfxConfig {
            SpeciesId = 2,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDuringType.OneOff
        };

        public static VfxConfig VfxIceExplodingBig = new VfxConfig {
            SpeciesId = 3,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDuringType.OneOff
        };

        public static VfxConfig VfxFireSlashActive = new VfxConfig {
            SpeciesId = 4,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDuringType.OneOff
        };

        public static VfxConfig VfxSlashActive = new VfxConfig {
            SpeciesId = 5,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDuringType.OneOff
        };

        public static VfxConfig VfxSpikeSlashExplodingActive = new VfxConfig {
            SpeciesId = 6,
            MotionType = VfxMotionType.Dropped,
            DurationType = VfxDuringType.OneOff
        };

        public static VfxConfig VfxFirePointLightActive  = new VfxConfig {
            SpeciesId = 7,
            MotionType = VfxMotionType.Tracing,
            DurationType = VfxDuringType.Repeating
        };
    }
}
