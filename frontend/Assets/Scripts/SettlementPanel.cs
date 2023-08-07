using TMPro;
using UnityEngine;
using DG.Tweening;

public class SettlementPanel : MonoBehaviour {
    public TMP_Text finished;
    void Start() {

    }

    public void playFinishedAnim() {
        finished.gameObject.SetActive(true);
        finished.gameObject.transform.localScale = Vector3.one;
        finished.gameObject.transform.DOScale(1.5f * Vector3.one, 0.7f);
    }

    // Update is called once per frame
    void Update() {

    }
}
