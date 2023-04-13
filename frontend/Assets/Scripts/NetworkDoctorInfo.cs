using UnityEngine;
using TMPro;

public class NetworkDoctorInfo: MonoBehaviour {
 
    public TMP_Text sendingFpsTitle;
    public TMP_Text sendingFpsValue;

    public TMP_Text srvDownsyncFpsTitle;
    public TMP_Text srvDownsyncFpsValue;

    public TMP_Text stepsLockedTitle;
    public TMP_Text stepsLockedValue;

    public TMP_Text rollbackFramesTitle;
    public TMP_Text rollbackFramesValue;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void SetValues(int sendingFps, int srvDownsyncFps, int stepsLocked, int rollbackFrames) {
        sendingFpsValue.text = sendingFps.ToString();
        srvDownsyncFpsValue.text = srvDownsyncFps.ToString();
        stepsLockedValue.text = stepsLocked.ToString();
        rollbackFramesValue.text = rollbackFrames.ToString();
    }
}
