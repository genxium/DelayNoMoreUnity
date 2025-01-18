using UnityEngine;
using UnityEngine.UI;
using shared;
using DG.Tweening;

public class SelfBattleHeading : AbstractHpBar {
    public Image mpFiller;
    public Image overflowMpFiller;
    public Image mpHolder;

    public Image hpNegInterpolator; 
    public Image hpFiller;
    public Image overflowHpNegInterpolator; 
    public Image overflowHpFiller;
    public Image hpHolder;

    protected new static float DEFAULT_HP100_WIDTH = 120.0f;
    protected new static float DEFAULT_HP100_HEIGHT = 18.0f;
    protected new static float DEFAULT_HOLDER_PADDING = 3.0f;

    protected static int MP_PER_SECTION = 1000;
    protected static float MP_PER_SECTION_F = (float)MP_PER_SECTION;

    protected static float DEFAULT_MP_SECTION_WIDTH = 120.0f;
    protected static float DEFAULT_MP_SECTION_HEIGHT = 9.0f;
    protected static float DEFAULT_MP_HOLDER_PADDING = 3.0f;

    protected static Color[] mpColors = new Color[] {
        new Color(0x90 / 255f, 0xE0 / 255f, 0xEF / 255f),
        new Color(0x00 / 255f, 0xB4 / 255f, 0xD8 / 255f),
        new Color(0x00 / 255f, 0x77 / 255f, 0xB6 / 255f),
        new Color(0x02 / 255f, 0x3E / 255f, 0x8A / 255f),
        new Color(0x03 / 255f, 0x04 / 255f, 0x5E / 255f),
    };

    private int currHpVal;
    private int currMpVal;

    private static float hpSizeXFillPerSecond = 0.4f * DEFAULT_HP100_WIDTH;
    private float hpInterpolaterSpeed = hpSizeXFillPerSecond / (Battle.BATTLE_DYNAMICS_FPS); // per frame

    public Image avatar;
    public BuffActiveCountDown buffActiveCountDown;
    public void ResetSelf() {
        avatar.sprite = null;
    }
    
    private static string MATERIAL_SHINING_OPACITY_REF = "_ShiningOpacity";
    public void BlinkMpNotEnough() {
        var material = mpHolder.material;
        int currVal = material.GetInt("_ShiningOpacity");
        DOTween.Sequence().Append(
            DOTween.To(() => material.GetInt(MATERIAL_SHINING_OPACITY_REF), x => material.SetInt(MATERIAL_SHINING_OPACITY_REF, x), 1, 0))
            .Append(DOTween.To(() => material.GetInt(MATERIAL_SHINING_OPACITY_REF), x => material.SetInt(MATERIAL_SHINING_OPACITY_REF, x), 0, 1.5f));
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
        float newHolderWidth = (MP_PER_SECTION >= mpCap ? DEFAULT_MP_SECTION_WIDTH * (mpCap / MP_PER_SECTION_F) : DEFAULT_MP_SECTION_WIDTH);
        newSizeHolder.Set(newHolderWidth, DEFAULT_MP_SECTION_HEIGHT);
        mpFiller.rectTransform.sizeDelta = newSizeHolder;
        newSizeHolder.Set(newHolderWidth+DEFAULT_MP_HOLDER_PADDING, DEFAULT_MP_SECTION_HEIGHT+2);
        mpHolder.rectTransform.sizeDelta = newSizeHolder;

        float newOverflowFillerWidth = (MP_PER_SECTION >= mpCap ? 0 : DEFAULT_MP_SECTION_WIDTH);
        newSizeHolder.Set(newOverflowFillerWidth, DEFAULT_MP_SECTION_HEIGHT);
        overflowMpFiller.rectTransform.sizeDelta = newSizeHolder;

        int oldMpVal = currMpVal;
        if (MP_PER_SECTION >= mp) {
            int baseMpSectionIdx = 0;
            var baseColor = mpColors[baseMpSectionIdx];

            float mpNewSizeX = (float)mp*DEFAULT_MP_SECTION_WIDTH / MP_PER_SECTION;
            newSizeHolder.Set(mpNewSizeX, DEFAULT_MP_SECTION_HEIGHT);
            mpFiller.rectTransform.sizeDelta = newSizeHolder;
            mpFiller.color = baseColor;

            newSizeHolder.Set(0, DEFAULT_MP_SECTION_HEIGHT);
            overflowMpFiller.rectTransform.sizeDelta = newSizeHolder;
        } else {
            int overwhelmedMpSectionIdx = (mp / MP_PER_SECTION);
            var overwhelmedColor = mpColors[overwhelmedMpSectionIdx];

            int baseMpSectionIdx = overwhelmedMpSectionIdx - 1;
            var baseColor = mpColors[baseMpSectionIdx];

            newSizeHolder.Set(DEFAULT_MP_SECTION_WIDTH, DEFAULT_MP_SECTION_HEIGHT); 
            mpFiller.rectTransform.sizeDelta = newSizeHolder;
            mpFiller.color = baseColor;

            float mpNewSizeX = (mp - (overwhelmedMpSectionIdx * MP_PER_SECTION)) * DEFAULT_MP_SECTION_WIDTH / MP_PER_SECTION;
            newSizeHolder.Set(mpNewSizeX, DEFAULT_MP_SECTION_HEIGHT);
            overflowMpFiller.rectTransform.sizeDelta = newSizeHolder;
            overflowMpFiller.color = overwhelmedColor;
        }

        currMpVal = mp;
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
