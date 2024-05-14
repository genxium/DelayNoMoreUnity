using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.SceneManagement;
using shared;
using System;

public class StoryLevelSelectPanel : MonoBehaviour {
    private int selectionPhase = 0;
    private int selectedLevelIdx = -1;
    private string selectedLevelName = null;
    public Image backButton;
    public CharacterSelectGroup characterSelectGroup;
    public StoryLevelSelectGroup levels;
    protected PlayerStoryProgress storyProgress = null;

    public AbstractMapController map;

    void Start() {
        // Reference https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
        storyProgress = Battle.loadStoryProgress(Application.persistentDataPath, "story");
        reset();
    }

    void OnEnable() {
        storyProgress = Battle.loadStoryProgress(Application.persistentDataPath, "story");
        reset();
    }

    public void reset() {
        Debug.Log("StoryLevelSelectPanel reset");
        levels.postCancelledCallback = OnBackButtonClicked;
        StoryLevelSelectGroup.StoryLevelPostConfirmedCallbackT levelPostConfirmedCallback = (int selectedIdx, string selectedName) => {
            Debug.Log("StoryLevelSelectPanel levelPostConfirmedCallback");
            selectedLevelIdx = selectedIdx;
            selectedLevelName = selectedName; 
            selectionPhase = 1;
            characterSelectGroup.gameObject.SetActive(true);
            toggleUIInteractability(true);
        };
        levels.levelPostConfirmedCallback = levelPostConfirmedCallback;
        characterSelectGroup.gameObject.SetActive(false);
        characterSelectGroup.postCancelledCallback = OnBackButtonClicked;
        characterSelectGroup.postConfirmedCallback = allConfirmed;
        selectionPhase = 0;
        selectedLevelIdx = -1;
        selectedLevelName = null;
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
            toggleUIInteractability(false);
            SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
        }
    }

    public void allConfirmed(int selectedSpeciesId) {
        Debug.Log("StoryLevelSelectPanel allConfirmed at selectedSpeciesId=" + selectedSpeciesId);
        try {
            characterSelectGroup.toggleUIInteractability(false);
            backButton.gameObject.SetActive(false);
            selectionPhase = 2;
            toggleUIInteractability(false);
            characterSelectGroup.postCancelledCallback = null;
            characterSelectGroup.postConfirmedCallback = null;
            levels.postCancelledCallback = null;
            levels.levelPostConfirmedCallback = null;
            map.onCharacterAndLevelSelectGoAction(selectedSpeciesId, selectedLevelName);
        } catch (Exception ex) {
            Debug.LogError(ex);
            reset();
        }
    }
}
