using shared;

public class TeamTowerHpBar : AbstractHpBarInUIHeading {
  
    public TeamTowerHpBar() {
        DEFAULT_HP100_WIDTH = 200.0f;
        DEFAULT_HP100_HEIGHT = 15.0f;
        DEFAULT_HOLDER_PADDING = 2.0f;
        hpSizeXFillPerSecond = 0.4f * DEFAULT_HP100_WIDTH;
        hpInterpolaterSpeed = hpSizeXFillPerSecond / (Battle.BATTLE_DYNAMICS_FPS); // per frame 
    }

	public override void SetCharacter(CharacterDownsync chd) {
		if (!isActiveAndEnabled) {
			gameObject.SetActive(true);
		}

		var newChConfig = Battle.characters[chd.SpeciesId];
		updateHpByValsAndCaps(chd.Hp, newChConfig.Hp);
	}
}
