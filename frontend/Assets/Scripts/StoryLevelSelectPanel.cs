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

    void onEnable() {
        storyProgress = Battle.loadStoryProgress(Application.persistentDataPath, "story");
        reset();
    }

    public void reset() {
        Debug.Log("StoryLevelSelectPanel reset");
        levels.postCancelledCallback = OnBackButtonClicked;
        AbstractSingleSelectGroup.PostConfirmedCallbackT levelPostConfirmedCallback = (int selectedIdx) => {
            Debug.Log("StoryLevelSelectPanel levelPostConfirmedCallback");
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
            case 2:
                levels.toggleUIInteractability(false);
                characterSelectGroup.toggleUIInteractability(false);
                backButton.gameObject.SetActive(false);
                break;
        }
    }

    public void OnBackButtonClicked() {
        Debug.Log("StoryLevelSelectPanel OnBackButtonClicked at selectionPhase=" + selectionPhase);
        if (0 < selectionPhase) {
            reset();
        } else {
            SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
        }
    }

    public void allConfirmed(int selectedSpeciesId) {
        Debug.Log("StoryLevelSelectPanel allConfirmed at selectedSpeciesId=" + selectedSpeciesId);
        try {
            characterSelectGroup.toggleUIInteractability(false);
            backButton.gameObject.SetActive(false);
            string selectedLevelName = null;
            switch (selectedLevelIdx) {
                case 0:
                    selectedLevelName = "SmallForest";
                    break;
                case 1:
                    selectedLevelName = "ArrowPalace";
                    break;
            }
            selectionPhase = 2;
            toggleUIInteractability(false);
            characterSelectGroup.postCancelledCallback = null;
            characterSelectGroup.postConfirmedCallback = null;
            levels.postCancelledCallback = null;
            levels.postConfirmedCallback = null;
            map.onCharacterAndLevelSelectGoAction(selectedSpeciesId, selectedLevelName);
        } catch (Exception ex) {
            Debug.LogError(ex);
            reset();
        }
    }
}
