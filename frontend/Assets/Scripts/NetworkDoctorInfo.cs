using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class NetworkDoctorInfo: MonoBehaviour {
 
    public TMP_Text sendingFpsTitle;
    public TMP_Text sendingFpsValue;

    public TMP_Text srvDownsyncFpsTitle;
    public TMP_Text srvDownsyncFpsValue;

    public TMP_Text peerUpsyncFpsTitle;
    public TMP_Text peerUpsyncFpsValue;

    public TMP_Text ifdLagTitle;
    public TMP_Text ifdLagValue;

    public TMP_Text stepsLockedTitle;
    public TMP_Text stepsLockedValue;

    public TMP_Text rollbackFramesTitle;
    public TMP_Text rollbackFramesValue;

    public TMP_Text udpPunchedCntTitle;
    public TMP_Text udpPunchedCntValue;

    private float indicatorBaseAlpha = 0.1f;
    public Image chasedToPlayerRdfIdIndicator;
    public Image forceResyncImmediatePumpIndicator;
    public Image forceResyncFutureAppliedIndicator;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void SetValues(float sendingFps, float srvDownsyncFps, float peerUpsyncFps, int ifdLag, int stepsLocked, int rollbackFrames, long udpPunchedCnt) {
        sendingFpsValue.text = sendingFps.ToString("0.0");
        srvDownsyncFpsValue.text = srvDownsyncFps.ToString("0.0");
        peerUpsyncFpsValue.text = peerUpsyncFps.ToString("0.0");
        ifdLagValue.text = ifdLag.ToString();
        stepsLockedValue.text = stepsLocked.ToString();
        rollbackFramesValue.text = rollbackFrames.ToString();
        udpPunchedCntValue.text = udpPunchedCnt.ToString();

        float a1 = indicatorBaseAlpha + (NetworkDoctor.Instance.chasedToPlayerRdfIdIndicatorCountdown/NetworkDoctor.Instance.DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_1)*(1.0f - indicatorBaseAlpha);
        chasedToPlayerRdfIdIndicator.color = new Color(chasedToPlayerRdfIdIndicator.color.r, chasedToPlayerRdfIdIndicator.color.g, chasedToPlayerRdfIdIndicator.color.b, a1);

        float a2 = indicatorBaseAlpha + (NetworkDoctor.Instance.forceResyncImmediatePumpIndicatorCountdown / NetworkDoctor.Instance.DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_2) * (1.0f - indicatorBaseAlpha);
        forceResyncImmediatePumpIndicator.color = new Color(forceResyncImmediatePumpIndicator.color.r, forceResyncImmediatePumpIndicator.color.g, forceResyncImmediatePumpIndicator.color.b, a2);

        float a3 = indicatorBaseAlpha + (NetworkDoctor.Instance.forceResyncFutureAppliedIndicatorCountdown / NetworkDoctor.Instance.DEFAULT_INDICATOR_COUNTDOWN_RDF_CNT_3) * (1.0f - indicatorBaseAlpha);
        forceResyncFutureAppliedIndicator.color = new Color(forceResyncFutureAppliedIndicator.color.r, forceResyncFutureAppliedIndicator.color.g, forceResyncFutureAppliedIndicator.color.b, a3);
    }
}
