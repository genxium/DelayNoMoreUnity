using TMPro;
using UnityEngine;

public class LoginPageController : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public ModeSelectGroup modeSelectGroup;
    public CaptchaLoginFormController captchaLoginForm;
    public AllSettings allSettingsPanel;
    public TMP_Text appVersion;

    private void Start() {
        ModeSelectGroup.OnLoginRequiredDelegate onArenaLoginRequired = (WsSessionManager.OnLoginResult newOnLoginResultCallback) => {
            captchaLoginForm.SetOnLoginResultCallback(newOnLoginResultCallback);
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

        allSettingsPanel.SetCallbacks();
    }

    private void Awake() {
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
    }

    void toggleUIInteractability(bool enabled) {
        modeSelectGroup.toggleUIInteractability(enabled);
        allSettingsPanel.toggleUIInteractability(enabled);
    }
}
