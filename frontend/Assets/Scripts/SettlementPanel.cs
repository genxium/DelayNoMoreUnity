using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SettlementPanel : MonoBehaviour {
    public TMP_Text finished;
    public delegate void PostSettlementCallbackT();
    public PostSettlementCallbackT postSettlementCallback;

    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void PlayFinishedAnim() {
        finished.gameObject.SetActive(true);
        finished.gameObject.transform.localScale = Vector3.one;
        finished.gameObject.transform.DOScale(1.5f * Vector3.one, 0.7f);
    }

    public void OnOkClicked() {
        postSettlementCallback();
    }
}
