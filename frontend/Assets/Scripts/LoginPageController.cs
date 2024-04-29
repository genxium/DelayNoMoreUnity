using TMPro;
using UnityEngine;

public class LoginPageController : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public ModeSelectGroup modeSelectGroup;
    public CaptchaLoginFormController captchaLoginForm;
    public AllSettings allSettingsPanel;
    public TMP_Text appVersion;

    private void Start() {
        CaptchaLoginFormController.SimpleDelegate captchaLoginFormPostCancelledCb = () => {
            reset();
        };
        ModeSelectGroup.OnLoginRequiredDelegate onArenaLoginRequired = (WsSessionManager.OnLoginResult newOnLoginResultCallback) => {
            captchaLoginForm.SetOnLoginResultCallback(newOnLoginResultCallback, captchaLoginFormPostCancelledCb);
            captchaLoginForm.gameObject.SetActive(true);
        };
        modeSelectGroup.SetOnLoginRequired(onArenaLoginRequired);

        ModeSelectGroup.ParentUIInteractabilityDelegate parentUIInteractabilityToggle = (bool val) => {
            toggleUIInteractability(val);
        };
        modeSelectGroup.SetParentUIInteractabilityToggle(parentUIInteractabilityToggle);

        modeSelectGroup.SetAllSettingsPanel(allSettingsPanel);
        allSettingsPanel.SetSameSceneLoginStatusBar(loginStatusBarController);
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        appVersion.text = Application.version;

        AllSettings.SimpleDelegate allSettingsPostCancelledCb = () => {
            reset();
        };
        allSettingsPanel.SetCallbacks(allSettingsPostCancelledCb);
    }

    private void reset() {
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        toggleUIInteractability(true);
    }

    private void Awake() {
        reset();
    }

    void toggleUIInteractability(bool enabled) {
        modeSelectGroup.toggleUIInteractability(enabled);
        allSettingsPanel.toggleUIInteractability(enabled);
    }
}
