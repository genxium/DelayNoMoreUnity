using UnityEngine;
using UnityEngine.InputSystem;

public class StoryModeSettings : MonoBehaviour {

    public SettingsSelectGroup storyModeSettingsSelectGroup;
    public delegate void SimpleDelegate();

    SimpleDelegate onExitCallback = null, onCancelCallback = null;
    protected bool currentSelectPanelEnabled = false;

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        storyModeSettingsSelectGroup.toggleUIInteractability(val);
    }

    public void SetCallbacks(SimpleDelegate theExitCallback, SimpleDelegate theCancelCallback) {
        onExitCallback = theExitCallback;
        onCancelCallback = theCancelCallback;

        storyModeSettingsSelectGroup.postConfirmedCallback = (int val) => {
            if (0 == val) {
                OnExit();
            }
        };

        toggleUIInteractability(true);
    }

    public void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            OnCancel();
        }
    }

    public void OnExit() {
        gameObject.SetActive(false);
        onExitCallback();
    }

    public void OnCancel() {
        gameObject.SetActive(false);
        onCancelCallback();
    }
}
