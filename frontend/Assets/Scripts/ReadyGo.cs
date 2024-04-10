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

    void hideReady() {
        ready.gameObject.transform.localScale = Vector3.zero;
    }
    void hideGo() {
        go.gameObject.transform.localScale = Vector3.zero;
    }

    public void playReadyAnim(TweenCallback onStart, TweenCallback onComplete) {
        var sequence = DOTween.Sequence();
        sequence.Append(ready.gameObject.transform.DOScale(2.5f * Vector3.one, 0.7f));
        sequence.AppendInterval(1.0f);
        if (null != onStart) {
            sequence.OnStart(onStart);
        }
        if (null != onComplete) {
            sequence.OnComplete(onComplete);
        }
        sequence.Play();
    }

    public void playGoAnim() {
        hideReady();
        var sequence = DOTween.Sequence();
        sequence.Append(go.gameObject.transform.DOScale(2.0f * Vector3.one, 0.5f));
        sequence.Append(go.gameObject.transform.DOScale(0.01f * Vector3.one, 0.1f));
        sequence.onComplete = () => {
            if (null == go) return;
            hideGo();
        };
        sequence.Play();
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
