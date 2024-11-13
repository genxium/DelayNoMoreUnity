using shared;
using UnityEngine;
using UnityEngine.UI;

public class BuffActiveCountDown : MonoBehaviour {
    public Image countDownMask;
    private Vector3 newScaleHolder = new Vector3();

    public void updateData(Buff buff) {
        if (Battle.TERMINATING_BUFF_SPECIES_ID != buff.SpeciesId) {
            var buffConfig = Battle.buffConfigs[buff.SpeciesId];
            if (BuffStockType.Timed == buffConfig.StockType) {
                var remainingRdfCount = buff.Stock;
                var totalRdfCount = buffConfig.Stock;
                newScaleHolder.Set((float)remainingRdfCount / totalRdfCount, countDownMask.transform.localScale.y, countDownMask.transform.localScale.z);
                countDownMask.transform.localScale = newScaleHolder;
                countDownMask.gameObject.SetActive(true);
            } else {
                newScaleHolder.Set(0f, countDownMask.transform.localScale.y, countDownMask.transform.localScale.z);
                countDownMask.transform.localScale = newScaleHolder;
                countDownMask.gameObject.SetActive(false);
            }
        } else {
            newScaleHolder.Set(0f, countDownMask.transform.localScale.y, countDownMask.transform.localScale.z);
            countDownMask.transform.localScale = newScaleHolder;
            countDownMask.gameObject.SetActive(false);
        }
    }
}
