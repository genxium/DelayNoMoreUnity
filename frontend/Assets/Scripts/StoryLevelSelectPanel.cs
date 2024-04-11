using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using System;
using shared;

public class StoryLevelSelectPanel : MonoBehaviour {
    public Image GoActionButton; // to toggle interactability
    public CharacterSelectGroup characterSelectGroup;
    public ToggleGroup levels;
    protected PlayerStoryProgress storyProgress = null;

    void Start() {
        // Reference https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
        storyProgress = Battle.loadStoryProgress(Application.persistentDataPath, "story");
    }

    public void toggleUIInteractability(bool enabled) {
        GoActionButton.gameObject.SetActive(enabled);
    }

    public void OnGoActionClicked(AbstractMapController map) {
        toggleUIInteractability(false);
        Debug.Log(String.Format("GoAction button clicked with map={0}", map));
        int selectedSpeciesId = Battle.SPECIES_NONE_CH;
        switch (characterSelectGroup.getSelectedIdx()) {
            case 0:
                selectedSpeciesId = 0;
                break;
            case 1:
                selectedSpeciesId = 2;
                break;
            case 2:
                selectedSpeciesId = 6;
                break;
        }
        string selectedLevelName = null;
        
        foreach (var toggle in levels.gameObject.GetComponentsInChildren<Toggle>()) {
            if (null != toggle && toggle.isOn) {
                Debug.Log(String.Format("Level {0} chosen", toggle.name));
                selectedLevelName = toggle.name;
                break;
            }
        }

        if (null != map) {
            Debug.Log(String.Format("Extra goAction to be executed with selectedSpeciesId={0}, selectedLevelName={1}", selectedSpeciesId, selectedLevelName));
            map.onCharacterAndLevelSelectGoAction(selectedSpeciesId, selectedLevelName);
        } else {
            Debug.LogWarning(String.Format("There's no extra goAction to be executed with selectedSpeciesId={0}, selectedLevelName={1}", selectedSpeciesId, selectedLevelName));
        }
        toggleUIInteractability(true);
    }
}
