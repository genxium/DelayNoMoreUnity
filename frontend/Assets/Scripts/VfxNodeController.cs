using CartoonFX;
using UnityEngine;
using shared;

public class VfxNodeController : MonoBehaviour {
    public int score, speciesId;
    public ParticleSystem attachedPs = null;
    public ParticleSystemRenderer attachedPsr = null;
    public CFXR_Effect cfxrEff = null;

    // Start is called before the first frame update
    void Start() {
        attachedPs = this.gameObject.GetComponent<ParticleSystem>();
        attachedPsr = this.gameObject.GetComponent<ParticleSystemRenderer>();
        cfxrEff = this.gameObject.GetComponent<CFXR_Effect>();
        var vfxConfig = Battle.vfxDict[speciesId];
        if (vfxConfig.MotionType == VfxMotionType.Tracing) {
            attachedPs.Play();
        }
    }

    private void OnDestroy() {
        // If mysterious "vfxAnimHolder being null" issue is encountered, try to uncomment the following log to see if it gets called due to "Destroy == CFXR_Effect.ClearBehaviour" which is the default. 
        // Debug.Log(this.gameObject.name + " is being destroyed");
    }
}
