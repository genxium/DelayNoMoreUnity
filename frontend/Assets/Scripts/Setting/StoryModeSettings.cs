using UnityEngine;
using UnityEngine.InputSystem;

public class StoryModeSettings : MonoBehaviour {

    public delegate void SimpleDelegate();

    SimpleDelegate onExitCallback = null, onCancelCallback = null;

    public void SetCallbacks(SimpleDelegate theExitCallback, SimpleDelegate theCancelCallback) {
        onExitCallback = theExitCallback;
        onCancelCallback = theCancelCallback;
    }

    public void OnBtnCancel(InputAction.CallbackContext context) {
        if (!enabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising) {
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
