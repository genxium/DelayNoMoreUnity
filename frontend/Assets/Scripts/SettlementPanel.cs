using TMPro;
using UnityEngine;
using DG.Tweening;

public class SettlementPanel : MonoBehaviour {
    public TMP_Text finished, failed;
    public delegate void PostSettlementCallbackT();
    public PostSettlementCallbackT postSettlementCallback;

    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void PlaySettlementAnim(bool success) {
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

    public void OnOkClicked() {
        postSettlementCallback();
    }
}
