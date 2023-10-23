using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour {
    public TMP_Text quota;
    public Image cooldownMask;
    public Image content;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        
    }

    public void updateData(shared.InventorySlot slot) {
        switch (slot.StockType) {
            case shared.InventorySlotStockType.TimedIv:
                quota.enabled = false;
                if (content.enabled) content.enabled = true;
                cooldownMask.fillAmount = (float)slot.FramesToRecover / slot.DefaultFramesToRecover;
            break;
            case shared.InventorySlotStockType.TimedMagazineIv:
                if (quota.enabled) quota.enabled = true;
                if (content.enabled) content.enabled = true;
                quota.text = slot.Quota.ToString();
                cooldownMask.fillAmount = (float)slot.FramesToRecover / slot.DefaultFramesToRecover;
                break;
            default:
                quota.enabled = false;
                content.enabled = false;
                cooldownMask.fillAmount = 0;
            break;
        }
    }
}
