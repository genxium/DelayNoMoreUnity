using CartoonFX;
using UnityEngine;

public class VfxNodeController : MonoBehaviour {
    public int score;
    public ParticleSystem attachedPs = null;
    public CFXR_Effect cfxrEff = null;

    // Start is called before the first frame update
    void Start() {
        attachedPs = this.gameObject.GetComponent<ParticleSystem>();
        cfxrEff = this.gameObject.GetComponent<CFXR_Effect>();
    }

    // Update is called once per frame
    void Update() {

    }

    private void OnDestroy() {
        Debug.Log(this.gameObject.name + " is being destroyed");
    }
}
