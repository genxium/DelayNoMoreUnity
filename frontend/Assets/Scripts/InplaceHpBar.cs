using UnityEngine;

public class InplaceHpBar : MonoBehaviour {
    public GameObject filler;
    private float origFillerScaleX = 0.8f;
    private Vector3 newScaleHolder = new Vector3();

    // Start is called before the first frame update
    void Start() {
        updateHp(1f);
    }

    // Update is called once per frame
    void Update() {

    }

    public void updateHp(float newScaleX) {
        newScaleHolder.Set(newScaleX * origFillerScaleX, filler.transform.localScale.y, filler.transform.localScale.z);
        filler.transform.localScale = newScaleHolder;
    }
}
