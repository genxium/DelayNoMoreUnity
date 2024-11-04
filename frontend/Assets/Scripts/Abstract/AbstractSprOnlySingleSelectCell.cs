using UnityEngine;

public class AbstractSprOnlySingleSelectCell : MonoBehaviour {
    // Start is called before the first frame update
    public SpriteRenderer background;
    public Sprite selectedSpr, unselectedSpr;
    public bool hideUponNullBkg = true;

    public virtual void setSelected(bool val) {
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
                    // Don't set "background.sprite = null" in this case, or it might accidentally reset the sprite draw mode
                } else {
                    background.sprite = null;
                }
            }
        }
    }
}
