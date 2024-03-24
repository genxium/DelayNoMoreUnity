using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

public class LoginPageController : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public ModeSelect modeSelect;
    public CaptchaLoginFormController captchaLoginForm;

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
            ModeSelect.OnLoginRequiredDelegate onArenaLoginRequired = (WsSessionManager.OnLoginResult callback) => {
                captchaLoginForm.gameObject.SetActive(true);
            };
            modeSelect.ConfirmSelection(onArenaLoginRequired);
            toggleUIInteractability(true);
        }
    }
}
