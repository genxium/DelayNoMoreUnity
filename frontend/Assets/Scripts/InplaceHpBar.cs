using UnityEngine;

public class InplaceHpBar : MonoBehaviour {
    public int score;
    public GameObject hpFiller;
    public GameObject mpFiller;
    private Vector3 newScaleHolder = new Vector3();

    // Start is called before the first frame update
    void Start() {
        updateHp(1f, 1f);
    }

    // Update is called once per frame
    void Update() {

    }

    public void updateHp(float hpNewScaleX, float mpNewScaleX) {
        newScaleHolder.Set(hpNewScaleX, hpFiller.transform.localScale.y, hpFiller.transform.localScale.z);
        hpFiller.transform.localScale = newScaleHolder;

        if (null != mpFiller) {
            newScaleHolder.Set(mpNewScaleX, mpFiller.transform.localScale.y, mpFiller.transform.localScale.z);
            mpFiller.transform.localScale = newScaleHolder;
        }
    }
}
