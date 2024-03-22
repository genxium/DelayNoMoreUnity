using shared;
using UnityEngine;
using UnityEngine.UI;

public class BuffActiveCountDown : MonoBehaviour {
    public Image countDownMask;
    public Image content;
    public Image innerBkg;
    public Image outerBkg;

    [SerializeField]
    public Sprite[] buffConfigSprites;

    public void updateData(Buff buff) {
        if (Battle.TERMINATING_BUFF_SPECIES_ID != buff.SpeciesId) {
            innerBkg.enabled = true;
            outerBkg.enabled = true;
            int spriteIdx = buff.SpeciesId - 1;
            Sprite spr = buffConfigSprites[spriteIdx];
            content.color = Color.white;
            content.sprite = spr;
            content.enabled = true;
            var buffConfig = Battle.buffConfigs[buff.SpeciesId];

            if (BuffStockType.Timed == buffConfig.StockType) {
                int remainingRdfCount = buff.Stock;
                int totalRdfCount = buffConfig.Stock;
                countDownMask.enabled = true;
                countDownMask.fillAmount = (float)remainingRdfCount / totalRdfCount;
            } else {
                countDownMask.enabled = false;
            }
        } else {
            countDownMask.enabled = false;
            content.enabled = false;
            innerBkg.enabled = false;
            outerBkg.enabled = false;
        }
    }
}
