using UnityEngine;
using UnityEngine.InputSystem;

public class ArenaModeSettings : MonoBehaviour {

    public SettingsSelectGroup arenaModeSettingsSelectGroup;
    public delegate void SimpleDelegate();

    SimpleDelegate onExitCallback = null, onCancelCallback = null;
    protected bool currentSelectPanelEnabled = false;

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        arenaModeSettingsSelectGroup.toggleUIInteractability(val);
    }

    public void SetCallbacks(SimpleDelegate theExitCallback, SimpleDelegate theCancelCallback) {
        onExitCallback = theExitCallback;
        onCancelCallback = theCancelCallback;

        arenaModeSettingsSelectGroup.postConfirmedCallback = (int val) => {
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
        if (null != arenaModeSettingsSelectGroup.uiSoundSource) {
            arenaModeSettingsSelectGroup.uiSoundSource.PlayNegative();
        }
        gameObject.SetActive(false);
        onExitCallback();
    }

    public void OnCancel() {
        if (null != arenaModeSettingsSelectGroup.uiSoundSource) {
            arenaModeSettingsSelectGroup.uiSoundSource.PlayCancel();
        }
        gameObject.SetActive(false);
        onCancelCallback();
    }
}
