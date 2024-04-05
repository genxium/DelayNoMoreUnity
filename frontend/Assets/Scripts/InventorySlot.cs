using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour {
    public TMP_Text quota;
    public Image cooldownMask;
    public Image content;

    [SerializeField] 
    public Sprite[] buffConfigSprites; 

    [SerializeField] 
    public Sprite inventoryBtnBSprite;
    public Sprite regularBtnBSprite;

    public void resumeRegularBtnB() {
        quota.enabled = false;
        cooldownMask.fillAmount = 0;
        quota.text = "";

        content.sprite = regularBtnBSprite;
    }

    public void updateData(shared.InventorySlot slot) {
        if (shared.Battle.TERMINATING_BUFF_SPECIES_ID != slot.BuffSpeciesId) {
            int spriteIdx = slot.BuffSpeciesId - 1;  
            content.color = Color.white;
            content.sprite = buffConfigSprites[spriteIdx];
        } else if (shared.Battle.INVENTORY_BTN_B_SKILL == slot.SkillId) {
            Sprite spr = inventoryBtnBSprite; 
            content.color = Color.white;
            content.sprite = spr;
        } else if (27 == slot.SkillId) {
            content.color = Color.white;
            content.sprite = buffConfigSprites[2]; // TODO: Remove this nonsense hardcoded index! 
        } else if (21 == slot.SkillId) {
            content.color = Color.white;
            content.sprite = buffConfigSprites[3]; // TODO: Remove this nonsense hardcoded index! 
        }

        switch (slot.StockType) {
            case shared.InventorySlotStockType.TimedIv:
                quota.enabled = false;
                if (!content.enabled) content.enabled = true;
                cooldownMask.fillAmount = (float)slot.FramesToRecover / slot.DefaultFramesToRecover;
            break;
            case shared.InventorySlotStockType.TimedMagazineIv:
                if (0 < slot.Quota) {
                    quota.enabled = true;
                } else {
                    quota.enabled = false;
                }
                if (!content.enabled) content.enabled = true;
                quota.text = (0 < slot.Quota ? slot.Quota.ToString() : "");
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
