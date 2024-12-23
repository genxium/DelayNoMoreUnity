using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class InventorySlot : MonoBehaviour {
    public TMP_Text quota;
    public Image cooldownMask;
    public Image content;
    private Material contentMat;

    public Image gauge;
    public Image positiveGaugeInterpolater;
    private static float gaugePercentFillPerSecond = 0.5f;
    private float gaugeInterpolaterSpeed = gaugePercentFillPerSecond / (shared.Battle.BATTLE_DYNAMICS_FPS); // per frame
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
            if (null != positiveGaugeInterpolater) {
                positiveGaugeInterpolater.gameObject.SetActive(true);
            }
            cooldownMask.gameObject.SetActive(false);
        } else {
            if (null != gauge) {
                gauge.gameObject.SetActive(false);
            }
            if (null != gaugeForeground) {
                gaugeForeground.gameObject.SetActive(false);
            }
            if (null != positiveGaugeInterpolater) {
                positiveGaugeInterpolater.gameObject.SetActive(false);
            }
            cooldownMask.gameObject.SetActive(true);
            contentMat.SetInt("_ShiningOpacity", 0);
        }
    }

    public void updateData(shared.InventorySlot slot) {
        lazyInit();
        if (shared.Battle.TERMINATING_BUFF_SPECIES_ID != slot.BuffSpeciesId) {
            var buffConfig = shared.Battle.buffConfigs[slot.BuffSpeciesId];
            if (shared.Battle.SPECIES_NONE_CH != buffConfig.XformChSpeciesId) {      
                content.color = semiTransparent;
                content.sprite = buffConfigSprites[8];
            }
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
        } else if (21 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[3]; // TODO: Remove this nonsense hardcoded index! 
        } else if (4 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[4]; // TODO: Remove this nonsense hardcoded index! 
        } else if (35 == slot.SkillId || 79 == slot.SkillId || 116 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[5]; // TODO: Remove this nonsense hardcoded index! 
        } else if (76 == slot.SkillId || 49 == slot.SkillId) {  
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[6]; // TODO: Remove this nonsense hardcoded index! 
        } else if (58 == slot.SkillId || 81 == slot.SkillId) {
            content.color = semiTransparent;
            content.sprite = buffConfigSprites[7]; // TODO: Remove this nonsense hardcoded index! 
        }

        switch (slot.StockType) {
            case shared.InventorySlotStockType.GaugedMagazineIv:
                toggleGaugeModeOn(true);
                content.enabled = true;
                if (0 < slot.Quota) {
                    quota.enabled = true;
                    contentMat.SetInt("_GrayOut", 0);
                    if (slot.Quota == slot.DefaultQuota && shared.Battle.TERMINATING_BUFF_SPECIES_ID != slot.FullChargeBuffSpeciesId) {     
                        contentMat.SetInt("_ShiningOpacity", 1);
                        var buffConfig = shared.Battle.buffConfigs[slot.FullChargeBuffSpeciesId];
                        if (shared.Battle.SPECIES_NONE_CH != buffConfig.XformChSpeciesId) {      
                            content.sprite = buffConfigSprites[8];
                        } else {
                            // TODO
                        }
                    } else if (slot.Quota == slot.DefaultQuota && shared.Battle.NO_SKILL != slot.FullChargeSkillId) {
                        contentMat.SetInt("_ShiningOpacity", 1);
                        // TODO
                    } else {    
                        contentMat.SetInt("_ShiningOpacity", 0);
                    }
                } else {
                    quota.enabled = false;
                    contentMat.SetInt("_GrayOut", 1);
                    contentMat.SetInt("_ShiningOpacity", 0);
                }
                quota.text = (0 < slot.Quota ? slot.Quota.ToString() : "");
                var oldFillAmt = gauge.fillAmount;
                var targetInterpolatedFillAmt = (float)slot.GaugeCharged / slot.GaugeRequired;
                if (null != positiveGaugeInterpolater) {
                    var oldInterpolatedFillAmt = positiveGaugeInterpolater.fillAmount;
                    if (targetInterpolatedFillAmt > oldInterpolatedFillAmt) {
                        var newInterpolatedFillAmt = oldInterpolatedFillAmt;
                        if (targetInterpolatedFillAmt < oldInterpolatedFillAmt + gaugeInterpolaterSpeed) {
                            newInterpolatedFillAmt = targetInterpolatedFillAmt;
                        } else {
                            newInterpolatedFillAmt = oldInterpolatedFillAmt + gaugeInterpolaterSpeed;
                        }
                        positiveGaugeInterpolater.fillAmount = newInterpolatedFillAmt;
                    } else {
                        positiveGaugeInterpolater.fillAmount = targetInterpolatedFillAmt;
                    }
                }
                gauge.fillAmount = targetInterpolatedFillAmt;
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
