using UnityEngine;
using TMPro;

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
    }
}
