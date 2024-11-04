using UnityEngine.UI;
using shared;
using System;
using Story;
using TMPro;

public class SaveSlot : AbstractSingleSelectCell {
    public Image icon;
    public TMP_Text timestamp;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void UpdateByStoryProgress(PlayerStoryProgress progress) {
        if (null == progress) {
            title.text = "No data";
            timestamp.text = string.Empty;
        } else {
            title.text = PlayerStoryModeSelectView.Region == progress.View ? StoryConstants.REGION_NAMES[progress.CursorRegionId] : StoryConstants.REGION_NAMES[progress.CursorRegionId]  + "/" + StoryConstants.LEVEL_NAMES[progress.CursorLevelId];
            timestamp.text = DateTimeOffset.FromUnixTimeMilliseconds((long)progress.SavedAtGmtMillis).LocalDateTime.ToString();
        }
    }
}
