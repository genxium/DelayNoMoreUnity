using UnityEngine;
using TMPro;
using UnityEngine.UI;
using shared;

public class StoryLvInfoPanel : MonoBehaviour {
    public FinishedLvOptionSelectGroup finishedLvOptionSelectGroup;
    public TMP_Text title;
    public Image lvPreview;
    protected bool lvFinished;
    protected bool currentSelectPanelEnabled = false;

    void Start() {
    }

    private void OnEnable() {
    }

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        finishedLvOptionSelectGroup.toggleUIInteractability(val);
        if (val && lvFinished) {
            finishedLvOptionSelectGroup.gameObject.transform.localScale = Vector3.one;
        } else {
            finishedLvOptionSelectGroup.gameObject.transform.localScale = Vector3.zero;
        }
    }

    public void refreshLvInfo(string lvName, bool isLocked, PlayerLevelProgress lvProgress) {
        if (!isLocked) {
            title.text = lvName;
        } else {
            title.text = "? ? ?";
        }
        if (0 < lvProgress.HighestScore) {
            lvFinished = true;
            finishedLvOptionSelectGroup.highlightSelected();
        } else {
            lvFinished = false;
        }
    }

    public FinishedLvOption getFinishedLvOption() {
        if (!lvFinished) return FinishedLvOption.StoryAndBoss;
        return finishedLvOptionSelectGroup.getOption(); 
    }
}
