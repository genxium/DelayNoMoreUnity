using UnityEngine;
using TMPro;
using DG.Tweening;

public class ReadyGo : MonoBehaviour {
    public TMP_Text ready;
    public TMP_Text go;
    public TMP_Text countdownSeconds;
    public TMP_Text countdownTicks;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void playReadyAnim() {
        ready.gameObject.SetActive(true);
        ready.gameObject.transform.DOScale(2.0f * Vector3.one, 0.7f);
    }

    public void playGoAnim() {
        ready.gameObject.SetActive(false);
        go.gameObject.SetActive(true);
        var sequence = DOTween.Sequence();
        sequence.Append(go.gameObject.transform.DOScale(1.5f * Vector3.one, 0.5f));
        sequence.Append(go.gameObject.transform.DOScale(0.01f * Vector3.one, 0.1f));
        sequence.onComplete = () => {
            if (null == go) return;
            go.gameObject.SetActive(false); 
        };
    }

    public void resetCountdown() {
        countdownSeconds.text = "--";
        countdownTicks.text = "--";
    }

    public void setCountdown(int renderFrameId, int battleDurationFrames) {
        int remainingTicks = battleDurationFrames-renderFrameId;
        if (0 >= remainingTicks) remainingTicks = 0;

        int remainingSecs = remainingTicks / 60;
        int remainingTicksMod = remainingTicks - remainingSecs*60;

        string remainingSecsStr = string.Format("{0:d2}", remainingSecs);
        string remainingTicksStr = string.Format("{0:d2}", remainingTicksMod);

        countdownSeconds.text = remainingSecsStr;
        countdownTicks.text = remainingTicksStr;
    }
}
