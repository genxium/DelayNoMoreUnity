using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbstractSingleSelectCell : MonoBehaviour {
    // Start is called before the first frame update
    public TMP_Text title;
    public Image background;
    public Sprite selectedSpr, unselectedSpr;
    
    public void toggleUIInteractability(bool val) {
        background.gameObject.SetActive(val);
        title.gameObject.SetActive(val);
    }

    public void setSelected(bool val) {
        if (val) {
            background.sprite = selectedSpr;
        } else {
            if (null != unselectedSpr) {
                background.sprite = unselectedSpr;
            } else {
                background.sprite = null;
            }
        }
    }
}
