using UnityEngine;
using TMPro;
using DG.Tweening;
using shared;

public class ReadyGo : MonoBehaviour {
    public TMP_Text ready;
    public TMP_Text go;
    public TMP_Text countdownSeconds;
    public TMP_Text countdownTicks;

    private int phase = 0;

    // Start is called before the first frame update
    void Start() {
    }

    // Update is called once per frame
    void Update() {

    }

    public void hideReady() {
        ready.gameObject.transform.localScale = Vector3.zero;
    }

    public void hideGo() {
        go.gameObject.transform.localScale = Vector3.zero;
    }

    public void playReadyAnim(TweenCallback onStart, TweenCallback onComplete) {
        if (0 < phase) return;
        var sequence = DOTween.Sequence();
        sequence.Append(ready.gameObject.transform.DOScale(2.5f * Vector3.one, 0.7f));
        sequence.AppendInterval(0.4f);
        if (null != onStart) {
            sequence.OnStart(onStart);
        }
        if (null != onComplete) {
            sequence.OnComplete(onComplete);
        }
        sequence.Play();
        phase = 1;
    }

    public void playGoAnim() {
        hideReady();
        if (1 < phase) return;
        var sequence = DOTween.Sequence();
        sequence.Append(go.gameObject.transform.DOScale(2.0f * Vector3.one, 0.5f));
        sequence.Append(go.gameObject.transform.DOScale(0.01f * Vector3.one, 0.1f));
        sequence.onComplete = () => {
            if (null == go) return;
            hideGo();
        };
        sequence.Play();
        phase = 2;
    }

    public void resetCountdown() {
        countdownSeconds.text = "--";
        countdownTicks.text = "--";
        phase = 0;
    }

    public void setCountdown(int renderFrameId, int battleDurationFrames) {
        int remainingTicks = battleDurationFrames-renderFrameId;
        if (0 >= remainingTicks) remainingTicks = 0;

        int remainingSecs = remainingTicks / Battle.BATTLE_DYNAMICS_FPS;
        int remainingTicksMod = remainingTicks - remainingSecs*Battle.BATTLE_DYNAMICS_FPS;

        string remainingSecsStr = string.Format("{0:d2}", remainingSecs);
        string remainingTicksStr = string.Format("{0:d2}", remainingTicksMod);

        countdownSeconds.text = remainingSecsStr;
        countdownTicks.text = remainingTicksStr;
    }
}
