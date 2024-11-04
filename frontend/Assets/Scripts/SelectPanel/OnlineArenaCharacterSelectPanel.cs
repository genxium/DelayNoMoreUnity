using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public class OnlineArenaCharacterSelectPanel : MonoBehaviour {
    public CharacterSelectGroup characterSelectGroup;
    public Image cancelBtn;
    public AbstractMapController map;

    // Start is called before the first frame update
    void Start() {
        reset();
    }

    private void OnEnable() {
        reset();
    }

    public void toggleUIInteractability(bool enabled) {
        characterSelectGroup.toggleUIInteractability(enabled);
        cancelBtn.gameObject.SetActive(enabled);
    }

    public void reset() {
        characterSelectGroup.postCancelledCallback = OnCancelBtnClicked;
        characterSelectGroup.postConfirmedCallback = (v) => allConfirmed((uint)v);
        toggleUIInteractability(true);
    }

    public void allConfirmed(uint selectedSpeciesId) {
        try {
            toggleUIInteractability(false);
            map.onCharacterSelectGoAction(selectedSpeciesId);
        } catch (Exception ex) {
            Debug.LogError(ex);
            reset();
        }
    }

    public void OnCancelBtnClicked() {
        toggleUIInteractability(false);
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }
}
