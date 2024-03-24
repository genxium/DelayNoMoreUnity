using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using static WsSessionManager;

public class LoginPageController : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public ModeSelect modeSelect;
    public CaptchaLoginFormController captchaLoginForm;

    private void Start() {
        ModeSelect.OnLoginRequiredDelegate onArenaLoginRequired = (OnLoginResult newOnLoginResultCallback) => {
            captchaLoginForm.SetOnLoginResultCallback(newOnLoginResultCallback);
            captchaLoginForm.gameObject.SetActive(true);
        };
        modeSelect.SetOnLoginRequired(onArenaLoginRequired);
    }

    void toggleUIInteractability(bool enabled) {
        modeSelect.toggleUIInteractability(enabled);
    }

    public void OnMoveByKeyboard(InputAction.CallbackContext context) {
        switch (((KeyControl)context.control).keyCode) {
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
            toggleUIInteractability(false);
            modeSelect.ConfirmSelection();
            toggleUIInteractability(true);
        }
    }
}
