using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.InputSystem;

public class SettlementPanel : MonoBehaviour {
    public TMP_Text finished, failed;
    public delegate void PostSettlementCallbackT();
    public PostSettlementCallbackT postSettlementCallback;
    private bool currentPanelEnabled = false;
    public Image confirmButton;

    public void toggleUIInteractability(bool val) {
        currentPanelEnabled = val;
        if (val) {
            confirmButton.transform.localScale = Vector3.one;
        } else {
            confirmButton.transform.localScale = Vector3.zero;
        }
    }

    public virtual void PlaySettlementAnim(bool success) {
        if (success) {
            failed.gameObject.SetActive(false);
            finished.gameObject.SetActive(true);
            finished.gameObject.transform.localScale = Vector3.one;
            finished.gameObject.transform.DOScale(1.5f * Vector3.one, 0.7f);
        } else {
            finished.gameObject.SetActive(false);
            failed.gameObject.SetActive(true);
            failed.gameObject.transform.localScale = Vector3.one;
            failed.gameObject.transform.DOScale(1.5f * Vector3.one, 0.7f);
        }
    }

    public void OnBtnConfirm(InputAction.CallbackContext context) {
        Debug.LogFormat("SettlementPanel.OnBtnConfirm with currentPanelEnabled={0}", currentPanelEnabled);
        if (!currentPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            OnConfirm();
        }
    }

    public void OnConfirm() {
        if (null != postSettlementCallback) {
            postSettlementCallback();
        }
    }
}
