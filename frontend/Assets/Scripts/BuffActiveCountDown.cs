using shared;
using UnityEngine;
using UnityEngine.UI;

public class BuffActiveCountDown : MonoBehaviour {
    public Image countDownMask;
    public float fullWidth = 128;
    public float fullHeight = 3f;
    protected Vector2 newSizeHolder = new Vector2(0, 0);

    public void updateData(Buff buff) {
        if (Battle.TERMINATING_BUFF_SPECIES_ID != buff.SpeciesId) {
            var buffConfig = Battle.buffConfigs[buff.SpeciesId];
            if (BuffStockType.Timed == buffConfig.StockType) {
                var remainingRdfCount = buff.Stock;
                var totalRdfCount = buffConfig.Stock;
                float ratio = (float)remainingRdfCount / totalRdfCount;
                newSizeHolder.Set(ratio*fullWidth, fullHeight);
                countDownMask.rectTransform.sizeDelta = newSizeHolder;
                countDownMask.gameObject.SetActive(true);
            } else {
                newSizeHolder.Set(0f, 0f);
                countDownMask.rectTransform.sizeDelta = newSizeHolder;
                countDownMask.gameObject.SetActive(false);
            }
        } else {
            newSizeHolder.Set(0f, 0f);
            countDownMask.rectTransform.sizeDelta = newSizeHolder;
            countDownMask.gameObject.SetActive(false);
        }
    }
}
