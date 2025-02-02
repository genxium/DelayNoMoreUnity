using UnityEngine;
using UnityEngine.Playables;

/*
 Reference
 
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ExposedReference_1.html
 */
public class CutsceneCamTracingBehaviour : PlayableBehaviour {

    private Camera cutsceneCam;
    private GameObject tracingTarget;
    private Vector3 posHolder = Vector3.zero;
    private float camPosZ = -10f;

    public void setReferenceFromScript(Camera theCutsceneCam, GameObject theTracingTarget) {
        if (null != theCutsceneCam) {
            cutsceneCam = theCutsceneCam;
        }
        if (null != theTracingTarget) {
            tracingTarget = theTracingTarget;
        }
    }

    public override void PrepareFrame(Playable playable, FrameData frameData) {
        // If the Scene GameObject exists, move it continuously until the Playable pauses
        if (null == cutsceneCam || null == tracingTarget) return;
        posHolder.Set(tracingTarget.transform.position.x, tracingTarget.transform.position.y, camPosZ);
        cutsceneCam.transform.position = posHolder;
    }
}