using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

public class LoginPageController : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public ModeSelect modeSelect;
    public CaptchaLoginFormController captchaLoginForm;
    public AllSettings allSettingsPanel;

    private void Start() {
        ModeSelect.OnLoginRequiredDelegate onArenaLoginRequired = (WsSessionManager.OnLoginResult newOnLoginResultCallback) => {
            captchaLoginForm.SetOnLoginResultCallback(newOnLoginResultCallback);
            captchaLoginForm.gameObject.SetActive(true);
        };
        modeSelect.SetOnLoginRequired(onArenaLoginRequired);

        ModeSelect.ParentUIInteractabilityDelegate parentUIInteractabilityToggle = (bool val) => {
            toggleUIInteractability(val);
        };
        modeSelect.SetParentUIInteractabilityToggle(parentUIInteractabilityToggle);

        modeSelect.SetAllSettingsPanel(allSettingsPanel);
        allSettingsPanel.SetSameSceneLoginStatusBar(loginStatusBarController);
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
    }

    private void Awake() {
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
    }

    void toggleUIInteractability(bool enabled) {
        modeSelect.toggleUIInteractability(enabled);
    }

    public void OnMoveByKeyboard(InputAction.CallbackContext context) {
        var kctrl = (KeyControl)context.control;
        if (null == kctrl || !kctrl.wasReleasedThisFrame) return;
        switch (kctrl.keyCode) {
            case Key.W:
            case Key.UpArrow:
                modeSelect.MoveSelection(-1);
                break;
            case Key.S:
            case Key.DownArrow:
                modeSelect.MoveSelection(+1);
                break;
        }
    }

    public void OnBtnConfirm(InputAction.CallbackContext context) {
        bool rising = context.ReadValueAsButton();
        if (rising) {
            modeSelect.ConfirmSelection();
        }
    }
}
