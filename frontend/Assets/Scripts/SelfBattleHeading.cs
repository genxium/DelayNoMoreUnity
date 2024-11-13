using UnityEngine.UI;
using shared;

public class SelfBattleHeading : AbstractHpBar {
    private uint speciesId = Battle.SPECIES_NONE_CH;
    public Image mpFiller;
    public Image mpHolder;

    public Image hpFiller;
    public Image overflowHpFiller;
    public Image hpHolder;

    public Image avatar;
    public BuffActiveCountDown buffActiveCountDown;
    public void ResetSelf() {
        speciesId = Battle.SPECIES_NONE_CH;
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

        if (HP_PER_SECTION >= hp) {
            int baseHpSectionIdx = 0;
            var baseColor = hpColors[baseHpSectionIdx];

            float hpNewScaleX = (float)hp / HP_PER_SECTION;
            newScaleHolder.Set(hpNewScaleX, hpFiller.transform.localScale.y, hpFiller.transform.localScale.z);

            hpFiller.transform.localScale = newScaleHolder;
            hpFiller.color = baseColor;

            newSizeHolder.Set(0, DEFAULT_HP100_HEIGHT);
            overflowHpFiller.rectTransform.sizeDelta = newSizeHolder;
        } else {
            int overwhelmedHpSectionIdx = (hp / HP_PER_SECTION);
            var overwhelmedColor = hpColors[overwhelmedHpSectionIdx];

            int baseHpSectionIdx = overwhelmedHpSectionIdx - 1;
            var baseColor = hpColors[baseHpSectionIdx];

            newScaleHolder.Set(1.0f, hpFiller.transform.localScale.y, hpFiller.transform.localScale.z);
            hpFiller.transform.localScale = newScaleHolder;
            hpFiller.color = baseColor;

            int overwhelmedHp = hp - (overwhelmedHpSectionIdx * HP_PER_SECTION);
            float overwhelmedHpNewScaleX = (float)overwhelmedHp / HP_PER_SECTION_F;
            newScaleHolder.Set(overwhelmedHpNewScaleX, overflowHpFiller.transform.localScale.y, overflowHpFiller.transform.localScale.z);
            overflowHpFiller.transform.localScale = newScaleHolder;
            overflowHpFiller.color = overwhelmedColor;
        }
    }

    private void updateMpByValsAndCaps(int mp, int mpCap) {
        if (null == mpFiller || null == mpHolder) return;
        
        float mpNewScaleX = (float)mp / mpCap;
        newScaleHolder.Set(mpNewScaleX, mpFiller.transform.localScale.y, mpFiller.transform.localScale.z);
        mpFiller.transform.localScale = newScaleHolder;
    }
}
