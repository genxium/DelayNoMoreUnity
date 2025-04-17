using UnityEngine;
using TMPro;
using DG.Tweening;
using shared;

public class ReadyGo : MonoBehaviour {
    public TMP_Text ready;
    public TMP_Text go;

    private Sequence readyBoundSequence;
    private Sequence goBoundSequence;

    public TMP_Text countdownSeconds;
    public TMP_Text countdownTicks;

    private int phase = 0;

    public void hideReady() {
        if (Vector3.zero == ready.gameObject.transform.localScale) return;
        if (null != readyBoundSequence && readyBoundSequence.IsPlaying()) {
             readyBoundSequence.Kill();
             readyBoundSequence = null;
        }
        ready.gameObject.transform.localScale = Vector3.zero;
    }

    public void hideGo() {
        if (Vector3.zero == go.gameObject.transform.localScale) return;
        if (null != goBoundSequence && goBoundSequence.IsPlaying()) {
            goBoundSequence.Kill();
            goBoundSequence = null;
        }
        go.gameObject.transform.localScale = Vector3.zero;
    }

    public void playReadyAnim(TweenCallback onStart, TweenCallback onComplete) {
        if (0 < phase) return;
        if (null != readyBoundSequence && readyBoundSequence.IsPlaying()) {
            readyBoundSequence.Kill();
        }
        readyBoundSequence = DOTween.Sequence();
        readyBoundSequence.Append(ready.gameObject.transform.DOScale(2.5f * Vector3.one, 0.7f));
        readyBoundSequence.AppendInterval(0.4f);
        if (null != onStart) {
            readyBoundSequence.OnStart(onStart);
        }
        if (null != onComplete) {
            readyBoundSequence.OnComplete(onComplete);
        }
        readyBoundSequence.Play();
        phase = 1;
    }

    public void playGoAnim() {
        hideReady();
        if (1 < phase) return;
        if (goBoundSequence != null && goBoundSequence.IsPlaying()) {
            goBoundSequence.Kill();
            goBoundSequence = null;
        }
        goBoundSequence = DOTween.Sequence();
        goBoundSequence.Append(go.gameObject.transform.DOScale(2.0f * Vector3.one, 0.5f));
        goBoundSequence.Append(go.gameObject.transform.DOScale(0.01f * Vector3.one, 0.1f));
        goBoundSequence.onComplete = () => {
            if (null == go) return;
            hideGo();
        };
        goBoundSequence.Play();
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
