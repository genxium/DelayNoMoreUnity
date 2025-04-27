using UnityEngine.UI;
using shared;
using TMPro;

public class AbstractHpBarInUIHeading : AbstractHpBar {

	public Image hpNegInterpolator;
	public Image hpFiller;
	public Image overflowHpNegInterpolator;
	public Image overflowHpFiller;
	public Image hpHolder;

	protected int currHpVal;

	public TMP_Text title;

    protected float hpSizeXFillPerSecond = 0; 
    protected float hpInterpolaterSpeed = 0; 
    
    public AbstractHpBarInUIHeading() {
        hpSizeXFillPerSecond = 0.4f * DEFAULT_HP100_WIDTH;
        hpInterpolaterSpeed = hpSizeXFillPerSecond / (Battle.BATTLE_DYNAMICS_FPS); // per frame 
    }

    public void ResetSelf() {
        if (null == title) return;
		title.text = "";
	}

	public virtual void SetCharacter(CharacterDownsync chd) {
		if (!isActiveAndEnabled) {
			gameObject.SetActive(true);
		}

		var newChConfig = Battle.characters[chd.SpeciesId];
		updateHpByValsAndCaps(chd.Hp, newChConfig.Hp);
		title.text = newChConfig.SpeciesName;
	}

	public void updateHpByValsAndCaps(int hp, int hpCap) {
		float newHolderWidth = (HP_PER_SECTION >= hpCap ? DEFAULT_HP100_WIDTH * (hpCap / HP_PER_SECTION_F) : DEFAULT_HP100_WIDTH);
		newSizeHolder.Set(newHolderWidth, DEFAULT_HP100_HEIGHT);
		hpFiller.rectTransform.sizeDelta = newSizeHolder;
		newSizeHolder.Set(newHolderWidth + DEFAULT_HOLDER_PADDING, DEFAULT_HP100_HEIGHT);
		hpHolder.rectTransform.sizeDelta = newSizeHolder;

		float newOverflowFillerWidth = (HP_PER_SECTION >= hpCap ? 0 : DEFAULT_HP100_WIDTH);
		newSizeHolder.Set(newOverflowFillerWidth, DEFAULT_HP100_HEIGHT);
		overflowHpFiller.rectTransform.sizeDelta = newSizeHolder;

		int oldHpVal = currHpVal;

		if (HP_PER_SECTION >= hp) {
			int baseHpSectionIdx = 0;
			var baseColor = hpColors[baseHpSectionIdx];

			float hpNewSizeX = (float)hp * DEFAULT_HP100_WIDTH / HP_PER_SECTION;
			interpolateHp(hpNegInterpolator, hpNewSizeX, oldHpVal);

			newSizeHolder.Set(hpNewSizeX, DEFAULT_HP100_HEIGHT);
			hpFiller.rectTransform.sizeDelta = newSizeHolder;
			hpFiller.color = baseColor;

			newSizeHolder.Set(0, DEFAULT_HP100_HEIGHT);
			overflowHpFiller.rectTransform.sizeDelta = newSizeHolder;
			overflowHpNegInterpolator.rectTransform.sizeDelta = newSizeHolder;
		} else {
			int overwhelmedHpSectionIdx = (hp / HP_PER_SECTION);
			var overwhelmedColor = hpColors[overwhelmedHpSectionIdx];

			int baseHpSectionIdx = overwhelmedHpSectionIdx - 1;
			var baseColor = hpColors[baseHpSectionIdx];

			newSizeHolder.Set(DEFAULT_HP100_WIDTH, DEFAULT_HP100_HEIGHT);
			hpFiller.rectTransform.sizeDelta = newSizeHolder;
			hpNegInterpolator.rectTransform.sizeDelta = newSizeHolder;
			hpFiller.color = baseColor;

			float hpNewSizeX = (hp - (overwhelmedHpSectionIdx * HP_PER_SECTION)) * DEFAULT_HP100_WIDTH / HP_PER_SECTION;
			interpolateHp(overflowHpNegInterpolator, hpNewSizeX, oldHpVal);

			newSizeHolder.Set(hpNewSizeX, DEFAULT_HP100_HEIGHT);
			overflowHpFiller.rectTransform.sizeDelta = newSizeHolder;
			overflowHpFiller.color = overwhelmedColor;
		}

		currHpVal = hp;
	}

	protected void interpolateHp(Image interpolator, float hpNewSizeX, int oldHpVal) {
		var oldInterpolatedSizeX = interpolator.rectTransform.sizeDelta.x;
		if (hpNewSizeX < oldInterpolatedSizeX) {
			var newInterpolatedSizeX = oldInterpolatedSizeX;
			if (hpNewSizeX > oldInterpolatedSizeX - hpInterpolaterSpeed) {
				newSizeHolder.Set(hpNewSizeX, DEFAULT_HP100_HEIGHT);
			} else {
				newSizeHolder.Set(oldInterpolatedSizeX - hpInterpolaterSpeed, DEFAULT_HP100_HEIGHT);
			}
			interpolator.rectTransform.sizeDelta = newSizeHolder;
		} else if (hpNewSizeX > oldInterpolatedSizeX && oldHpVal > currHpVal) {
			newSizeHolder.Set(DEFAULT_HP100_WIDTH, DEFAULT_HP100_HEIGHT);
			interpolator.rectTransform.sizeDelta = newSizeHolder;
		} else {
			newSizeHolder.Set(hpNewSizeX, DEFAULT_HP100_HEIGHT);
			interpolator.rectTransform.sizeDelta = newSizeHolder;
		}
	}
}
