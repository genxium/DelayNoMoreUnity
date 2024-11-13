using UnityEngine;
using UnityEngine.UI;

public class StoryModeCharacterSelectPanel : MonoBehaviour {
    public CharacterSelectGroup characterSelectGroup;

    public void SetCallbacks(CharacterSelectGroup.PostConfirmedCallbackT postConfirmedCb, CharacterSelectGroup.PostCancelledCallbackT postCancelledCb) {
        characterSelectGroup.postConfirmedCallback = postConfirmedCb;
        characterSelectGroup.postCancelledCallback = postCancelledCb;
    }

    void Start() {
        ResetSelf();
    }

    private void OnEnable() {
        ResetSelf();
    }

    public void toggleUIInteractability(bool val) {
        characterSelectGroup.toggleUIInteractability(enabled);
    }

    public void ResetSelf() {
        toggleUIInteractability(true);
    }
}
