using UnityEngine;

public class StoryLevelSelectSceneOverallController : MonoBehaviour {
    public StoryRegionPanel regionPanel;
    public StoryLevelPanel levelPanel;

    public void OnCancelBtnClicked() {
        if (regionPanel.isActiveAndEnabled) {
            regionPanel.OnCancelBtnClicked();
        } else if (levelPanel.isActiveAndEnabled) {
            levelPanel.OnCancelBtnClicked();
        }
    }
}
