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

    // Update is called once per frame
    void Update() {

    }

    private void OnDestroy() {
        // Debug.Log(this.gameObject.name + " is being destroyed");
    }
}
