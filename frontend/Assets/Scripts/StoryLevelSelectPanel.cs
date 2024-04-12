using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.SceneManagement;
using shared;
using System;
using UnityEngine.InputSystem;
public class StoryLevelSelectPanel : MonoBehaviour {
    private int selectionPhase = 0;
    private int selectedLevelIdx = -1;
    public Image backButton;
    public CharacterSelectGroup characterSelectGroup;
    public StoryLevelSelectGroup levels;
    protected PlayerStoryProgress storyProgress = null;

    public AbstractMapController map;
    private PlayerInput sharedPlayerInputInstance;

    void Start() {
        // Reference https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
        storyProgress = Battle.loadStoryProgress(Application.persistentDataPath, "story");
        reset();
    }
    
    public void reset() {
        levels.gameObject.SetActive(true);
        levels.toggleUIInteractability(true);
        levels.postCancelledCallback = OnBackButtonClicked;
        AbstractSingleSelectGroup.PostConfirmedCallbackT levelPostConfirmedCallback = (int selectedIdx) => {
            selectedLevelIdx = selectedIdx;
            selectionPhase = 1;
            characterSelectGroup.gameObject.SetActive(true);
            toggleUIInteractability(true);
        };
        levels.postConfirmedCallback = levelPostConfirmedCallback;
        characterSelectGroup.gameObject.SetActive(false);
        characterSelectGroup.postCancelledCallback = OnBackButtonClicked;
        characterSelectGroup.postConfirmedCallback = allConfirmed;
        selectionPhase = 0;
        selectedLevelIdx = -1;
        sharedPlayerInputInstance = levels.GetComponent<PlayerInput>();
        toggleUIInteractability(true);
    }

    public void toggleUIInteractability(bool enabled) {
        switch (selectionPhase) {
            case 0:
                levels.toggleUIInteractability(enabled);
                characterSelectGroup.toggleUIInteractability(!enabled);
                backButton.gameObject.SetActive(enabled);
                break;
            case 1:
                levels.toggleUIInteractability(!enabled);
                characterSelectGroup.toggleUIInteractability(enabled);
                backButton.gameObject.SetActive(enabled);
                break;
        }
    }

    public void OnBackButtonClicked() {
        if (0 < selectionPhase) {
            reset();
        } else {
            SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
        }
    }

    public void allConfirmed(int selectedCharacterIdx) {
        try {
            characterSelectGroup.toggleUIInteractability(false);
            backButton.gameObject.SetActive(false);
            int selectedSpeciesId = Battle.SPECIES_NONE_CH;
            switch (selectedCharacterIdx) {
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
            switch (selectedLevelIdx) {
                case 0:
                    selectedLevelName = "SmallForest";
                    break;
                case 1:
                    selectedLevelName = "ArrowPalace";
                    break;
            }

            map.onCharacterAndLevelSelectGoAction(selectedSpeciesId, selectedLevelName);
        } catch (Exception ex) {
            Debug.LogError(ex);
            reset();
        }
    }
}
