using shared;

public class BossBattleHeading : AbstractHpBarInUIHeading {
  
    public BossBattleHeading() {
        DEFAULT_HP100_WIDTH = 480.0f;
        DEFAULT_HP100_HEIGHT = 12.0f;
        hpSizeXFillPerSecond = 0.4f * DEFAULT_HP100_WIDTH;
        hpInterpolaterSpeed = hpSizeXFillPerSecond / (Battle.BATTLE_DYNAMICS_FPS); // per frame 
    }
}
