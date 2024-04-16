using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public class OnlineArenaCharacterSelectPanel : MonoBehaviour {
    public CharacterSelectGroup characterSelectGroup;
    public Image backButton;
    public AbstractMapController map;

    // Start is called before the first frame update
    void Start() {
        reset();
    }

    public void toggleUIInteractability(bool enabled) {
        characterSelectGroup.toggleUIInteractability(enabled);
        backButton.gameObject.SetActive(enabled);
    }

    public void reset() {
        characterSelectGroup.postCancelledCallback = OnBackButtonClicked;
        characterSelectGroup.postConfirmedCallback = allConfirmed;
        toggleUIInteractability(true);
    }

    public void allConfirmed(int selectedSpeciesId) {
        try {
            toggleUIInteractability(false);
            map.onCharacterSelectGoAction(selectedSpeciesId);
        } catch (Exception ex) {
            Debug.LogError(ex);
            reset();
        }
    }

    public void OnBackButtonClicked() {
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }
}
