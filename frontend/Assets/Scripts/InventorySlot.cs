using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour {
    public TMP_Text quota;
    public Image cooldownMask;
    public Image content;
    public Material contentMat;

    public Image gauge;
    public Image gaugeForeground;

    [SerializeField] 
    public Sprite[] buffConfigSprites; 

    [SerializeField]
    public Sprite inventoryBtnBSpriteBh;
    public Sprite inventoryBtnBSpriteMsg;
    public Sprite regularBtnBSprite;

    private Color semiTransparent = new Color(1f, 1f, 1f, 1f);
    private void lazyInit() {
        if (null != contentMat) return;
        contentMat = content.material;
    }

    // Start is called before the first frame update
    void Start() {
        lazyInit();
    }

    public void resumeRegularBtnB() {
        quota.enabled = false;
        cooldownMask.fillAmount = 0;
        quota.text = "";

        content.sprite = regularBtnBSprite;
    }

    public void toggleGaugeModeOn(bool val) {
        if (null == gauge || null == gaugeForeground) {
            val = false;
        }
        if (val) {
            gauge.gameObject.SetActive(true);
            gaugeForeground.gameObject.SetActive(true);
            cooldownMask.gameObject.SetActive(false);
        } else {
            if (null != gauge) {
                gauge.gameObject.SetActive(false);
            }
            if (null != gaugeForeground) {
                gaugeForeground.gameObject.SetActive(false);
            }
            cooldownMask.gameObject.SetActive(true);
        }
    }

    public void updateData(shared.InventorySlot slot) {
        lazyInit();
        if (shared.Battle.TERMINATING_BUFF_SPECIES_ID != slot.BuffSpeciesId) {
            uint spriteIdx = slot.BuffSpeciesId - 1;
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[spriteIdx];
        } else if (shared.Battle.INVENTORY_BTN_B_SKILL_BH == slot.SkillId) {
            Sprite spr = inventoryBtnBSpriteBh;
            content.color = semiTransparent;
            content.sprite = spr;
        } else if (shared.Battle.INVENTORY_BTN_B_SKILL_MSG == slot.SkillId) {
            Sprite spr = inventoryBtnBSpriteMsg;
            content.color = semiTransparent;
            content.sprite = spr;
        } else if (65 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[0]; // TODO: Remove this nonsense hardcoded index! 
        } else if (59 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[1]; // TODO: Remove this nonsense hardcoded index! 
        } else if (27 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[2]; // TODO: Remove this nonsense hardcoded index! 
        } else if (76 == slot.SkillId) { 
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[6]; // TODO: Remove this nonsense hardcoded index! 
        } else if (21 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[3]; // TODO: Remove this nonsense hardcoded index! 
        } else if (4 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[4]; // TODO: Remove this nonsense hardcoded index! 
        } else if (79 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[5]; // TODO: Remove this nonsense hardcoded index! 
        } else if (49 == slot.SkillId) {  
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[6]; // TODO: Remove this nonsense hardcoded index! 
        }

        switch (slot.StockType) {
            case shared.InventorySlotStockType.GaugedMagazineIv:
                toggleGaugeModeOn(true);
                content.enabled = true;
                if (0 < slot.Quota) {
                    quota.enabled = true;
                    contentMat.SetInt("_GrayOut", 0);
                } else {
                    quota.enabled = false;
                    contentMat.SetInt("_GrayOut", 1);
                }
                quota.text = (0 < slot.Quota ? slot.Quota.ToString() : "");
                gauge.fillAmount = (float)slot.GaugeCharged / slot.GaugeRequired;
                break;
            case shared.InventorySlotStockType.QuotaIv:
                toggleGaugeModeOn(false);
                if (0 < slot.Quota) {
                    quota.enabled = true;
                    quota.text = (0 < slot.Quota ? slot.Quota.ToString() : "");
                    if (!content.enabled) content.enabled = true;
                } else {
                    quota.enabled = false;
                    content.enabled = false;
                }
                cooldownMask.fillAmount = 0;
                break;
            case shared.InventorySlotStockType.TimedIv:
                toggleGaugeModeOn(false);
                quota.enabled = false;
                if (!content.enabled) content.enabled = true;
                cooldownMask.fillAmount = (float)slot.FramesToRecover / slot.DefaultFramesToRecover;
            break;
            case shared.InventorySlotStockType.TimedMagazineIv:
                toggleGaugeModeOn(false);
                if (!content.enabled) content.enabled = true;
                if (0 < slot.Quota) {
                    quota.enabled = true;
                    contentMat.SetInt("_GrayOut", 0);
                } else {
                    quota.enabled = false;
                    contentMat.SetInt("_GrayOut", 1);
                }
                quota.text = (0 < slot.Quota ? slot.Quota.ToString() : "");
                cooldownMask.fillAmount = (float)slot.FramesToRecover / slot.DefaultFramesToRecover;
            break;
            default:
                toggleGaugeModeOn(false);
                quota.enabled = false;
                content.enabled = false;
                cooldownMask.fillAmount = 0;
            break;
        }
    }
}
