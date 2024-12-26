using UnityEngine.UI;
using shared;

public class SelfBattleHeading : AbstractHpBar {
    public Image mpFiller;
    public Image mpHolder;

    public Image hpNegInterpolator; 
    public Image hpFiller;
    public Image overflowHpNegInterpolator; 
    public Image overflowHpFiller;
    public Image hpHolder;

    protected new static float DEFAULT_HP100_WIDTH = 128.0f;
    protected new static float DEFAULT_HP100_HEIGHT = 18.0f;

    private int currHpVal;

    private static float hpSizeXFillPerSecond = 0.4f * DEFAULT_HP100_WIDTH;
    private float hpInterpolaterSpeed = hpSizeXFillPerSecond / (Battle.BATTLE_DYNAMICS_FPS); // per frame

    public Image avatar;
    public BuffActiveCountDown buffActiveCountDown;
    public void ResetSelf() {
        avatar.sprite = null;
    }

    public void SetCharacter(CharacterDownsync chd) {
        if (!isActiveAndEnabled) {
            gameObject.SetActive(true);
        }

        var newChConfig = Battle.characters[chd.SpeciesId];
        AvatarUtil.SetAvatar1(avatar, newChConfig);
        
        updateHpByValsAndCaps(chd.Hp, newChConfig.Hp);
        if (0 >= newChConfig.Mp) {
            mpHolder.gameObject.SetActive(false);
            mpFiller.gameObject.SetActive(false);
            updateMpByValsAndCaps(0, 1);
        } else {
            updateMpByValsAndCaps(chd.Mp, newChConfig.Mp);
            mpHolder.gameObject.SetActive(true);
            mpFiller.gameObject.SetActive(true);
        }

        if (null != chd.BuffList && 0 < chd.BuffList.Count) {
            // TODO: Handle multiple slots.
            buffActiveCountDown.updateData(chd.BuffList[0]);
        }
    }

    private void updateHpByValsAndCaps(int hp, int hpCap) {
        float newHolderWidth = (HP_PER_SECTION >= hpCap ? DEFAULT_HP100_WIDTH * (hpCap / HP_PER_SECTION_F) : DEFAULT_HP100_WIDTH);
        newSizeHolder.Set(newHolderWidth, DEFAULT_HP100_HEIGHT);
        hpFiller.rectTransform.sizeDelta = newSizeHolder;
        newSizeHolder.Set(newHolderWidth+DEFAULT_HOLDER_PADDING, DEFAULT_HP100_HEIGHT);
        hpHolder.rectTransform.sizeDelta = newSizeHolder;

        float newOverflowFillerWidth = (HP_PER_SECTION >= hpCap ? 0 : DEFAULT_HP100_WIDTH);
        newSizeHolder.Set(newOverflowFillerWidth, DEFAULT_HP100_HEIGHT);
        overflowHpFiller.rectTransform.sizeDelta = newSizeHolder;

        int oldHpVal = currHpVal;

        if (HP_PER_SECTION >= hp) {
            int baseHpSectionIdx = 0;
            var baseColor = hpColors[baseHpSectionIdx];

            float hpNewSizeX = (float)hp*DEFAULT_HP100_WIDTH / HP_PER_SECTION;
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

    private void updateMpByValsAndCaps(int mp, int mpCap) {
        if (null == mpFiller || null == mpHolder) return;
        
        float mpNewScaleX = (float)mp / mpCap;
        newScaleHolder.Set(mpNewScaleX, mpFiller.transform.localScale.y, mpFiller.transform.localScale.z);
        mpFiller.transform.localScale = newScaleHolder;
    }

    private void interpolateHp(Image interpolator, float hpNewSizeX, int oldHpVal) {
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
