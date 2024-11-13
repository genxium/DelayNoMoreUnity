using UnityEngine;

public class InplaceHpBar : AbstractHpBar {
    public int score;
    public SpriteRenderer hpFiller;
    public SpriteRenderer overflowHpFiller;
    public SpriteRenderer hpHolder;

    public void updateHpByValsAndCaps(int hp, int hpCap) {
        float newHolderWidth = (HP_PER_SECTION >= hpCap ? DEFAULT_HP100_WIDTH * (hpCap / HP_PER_SECTION_F) : DEFAULT_HP100_WIDTH);
        newSizeHolder.Set(newHolderWidth, DEFAULT_HP100_HEIGHT);
        hpFiller.size = newSizeHolder;
        newSizeHolder.Set(newHolderWidth+DEFAULT_HOLDER_PADDING, DEFAULT_HP100_HEIGHT);
        hpHolder.size = newSizeHolder;

        float newOverflowFillerWidth = (HP_PER_SECTION >= hpCap ? 0 : DEFAULT_HP100_WIDTH);
        newSizeHolder.Set(newOverflowFillerWidth, DEFAULT_HP100_HEIGHT);
        overflowHpFiller.size = newSizeHolder;

        if (HP_PER_SECTION >= hp) {
            int baseHpSectionIdx = 0;
            var baseColor = hpColors[baseHpSectionIdx];

            float hpNewScaleX = (float)hp / HP_PER_SECTION;
            newScaleHolder.Set(hpNewScaleX, hpFiller.transform.localScale.y, hpFiller.transform.localScale.z);

            hpFiller.transform.localScale = newScaleHolder;
            hpFiller.color = baseColor;

            newSizeHolder.Set(0, DEFAULT_HP100_HEIGHT);
            overflowHpFiller.size = newSizeHolder;
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

}
