using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public class OnlineArenaCharacterSelectPanel : MonoBehaviour {
    public CharacterSelectGroup characterSelectGroup;
    public Image cancelBtn;
    public OnlineMapController map;

    // Start is called before the first frame update
    void Start() {
        ResetSelf();
    }

    private void OnEnable() {
        ResetSelf();
    }

    public void toggleUIInteractability(bool enabled) {
        characterSelectGroup.toggleUIInteractability(enabled);
        cancelBtn.gameObject.SetActive(enabled);
    }

    public void ResetSelf() {
        characterSelectGroup.postCancelledCallback = OnCancelBtnClicked;
        characterSelectGroup.postConfirmedCallback = (v) => allConfirmed((uint)v);
        toggleUIInteractability(true);
    }

    public void allConfirmed(uint selectedIndex) {
        try {
            toggleUIInteractability(false);
            map.onCharacterSelectGoAction(selectedIndex);
        } catch (Exception ex) {
            Debug.LogError(ex);
            ResetSelf();
        }
    }

    public void OnCancelBtnClicked() {
        toggleUIInteractability(false);
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }
}
