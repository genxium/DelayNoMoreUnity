using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour {
    public TMP_Text quota;
    public Image cooldownMask;
    public Image content;

    [SerializeField] 
    public Sprite[] buffConfigSprites; 

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        
    }

    public void updateData(shared.InventorySlot slot) {
        if (shared.Battle.TERMINATING_BUFF_SPECIES_ID != slot.BuffSpeciesId) {
            int spriteIdx = slot.BuffSpeciesId - 1;  
            Sprite spr = buffConfigSprites[spriteIdx]; 
            content.color = Color.white;
            content.sprite = spr;
        } else if (shared.Battle.NO_SKILL != slot.SkillId) {
            Sprite spr = buffConfigSprites[2]; // TODO: Remove this nonsense hardcoded index! 
            content.color = Color.white;
            content.sprite = spr;
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
                quota.text = 0 < slot.Quota ? slot.Quota.ToString() : "";
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
