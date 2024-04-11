using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbstractSingleSelectCell : MonoBehaviour {
    // Start is called before the first frame update
    public TMP_Text title;
    public Image background;
    public Sprite selectedSpr, unselectedSpr;
    public bool hideUponNullBkg = true;
    
    public void toggleUIInteractability(bool val) {
        background.gameObject.SetActive(val);
        if (null != title) {
            title.gameObject.SetActive(val);
        }
    }

    public void setSelected(bool val) {
        if (val) {
            background.sprite = selectedSpr;
            if (hideUponNullBkg) {
                background.transform.localScale = Vector3.one;
            }
        } else {
            if (null != unselectedSpr) {
                background.sprite = unselectedSpr;
                if (hideUponNullBkg) {
                    background.transform.localScale = Vector3.one;
                }
            } else {
                if (hideUponNullBkg) {
                    background.transform.localScale = Vector3.zero;
                }
                background.sprite = null;
            }
        }
    }
}
