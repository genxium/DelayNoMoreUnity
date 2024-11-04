using shared;
using UnityEngine;
using UnityEngine.UI;

public class BuffActiveCountDown : MonoBehaviour {
    public Slider countDownMask;

    public void updateData(Buff buff) {
        if (Battle.TERMINATING_BUFF_SPECIES_ID != buff.SpeciesId) {
            var buffConfig = Battle.buffConfigs[buff.SpeciesId];
            if (BuffStockType.Timed == buffConfig.StockType) {
                var remainingRdfCount = buff.Stock;
                var totalRdfCount = buffConfig.Stock;
                countDownMask.SetValueWithoutNotify((float)remainingRdfCount / totalRdfCount);
                countDownMask.gameObject.SetActive(true);
            } else {
                countDownMask.SetValueWithoutNotify(0);
                countDownMask.gameObject.SetActive(false);
            }
        } else {
            countDownMask.SetValueWithoutNotify(0);
            countDownMask.gameObject.SetActive(false);
        }
    }
}
