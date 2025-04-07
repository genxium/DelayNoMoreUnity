using TMPro;
using UnityEngine;

public class LoginPageController : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public ModeSelectGroup modeSelectGroup;
    public CaptchaLoginFormController captchaLoginForm;
    public AllSettings allSettingsPanel;
    public SaveSlotSelectPanel saveSlotSelectPanel;
    public TMP_Text appVersion;
    public GameObject cancelBtn;

    private void Start() {
        CaptchaLoginFormController.SimpleDelegate captchaLoginFormPostCancelledCb = () => {
            reset();
        };
        ModeSelectGroup.OnLoginRequiredDelegate onArenaLoginRequired = (WsSessionManager.OnLoginResult newOnLoginResultCallback) => {
            captchaLoginForm.SetOnLoginResultCallback(newOnLoginResultCallback, captchaLoginFormPostCancelledCb);
            captchaLoginForm.gameObject.SetActive(true);
        };
        modeSelectGroup.SetOnLoginRequired(onArenaLoginRequired);
        modeSelectGroup.SetParentUIInteractabilityToggle((bool val) => {
            toggleUIInteractability(val);
        });

        modeSelectGroup.SetSaveSlotSelectPanel(saveSlotSelectPanel);
        modeSelectGroup.SetAllSettingsPanel(allSettingsPanel);
        allSettingsPanel.SetSameSceneLoginStatusBar(loginStatusBarController);
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        appVersion.text = Application.version;

        AllSettings.SimpleDelegate allSettingsPostCancelledCb = () => {
            /*
             [WARNING]

             The UISoundSouce instance of "AllSettings" panel might've be deallocated by now, so use that of modeSelectGroup to play cancel sound.
             */
            if (null != modeSelectGroup.uiSoundSource) {
                modeSelectGroup.uiSoundSource.PlayCancel();
            }
            reset();
        };
        allSettingsPanel.SetCallbacks(allSettingsPostCancelledCb);

        SaveSlotSelectGroup.DeleteClickedCallbackT saveSlotDeleteClickedCallback = (int slotId) => {
            PlayerStoryProgressManager.Instance.DeleteSlot(slotId);
            saveSlotSelectPanel.ResetSelf();
        };
        SaveSlotSelectGroup.PostCancelledCallbackT saveSlotPostCancelledCb = () => {
            reset();
        };
        saveSlotSelectPanel.SetCallbacks(saveSlotDeleteClickedCallback, saveSlotPostCancelledCb);
        saveSlotSelectPanel.SetParentUIInteractabilityToggle((bool val) => {
            toggleUIInteractability(val);
        });
    }

    private void reset() {
        WsSessionManager.Instance.setInArenaPracticeMode(false); 
        modeSelectGroup.resetShouldShowMarks();
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        toggleUIInteractability(true);
    }

    private void Awake() {
        reset();
    }

    public void Update() {
        if (!modeSelectGroup.showSaveSlotSelectPanel && saveSlotSelectPanel.isActiveAndEnabled) {
            saveSlotSelectPanel.gameObject.SetActive(false);
        }

        if (!modeSelectGroup.showAllSettingsPanel && allSettingsPanel.isActiveAndEnabled) {
            allSettingsPanel.gameObject.SetActive(false);
        }

        if (!modeSelectGroup.showCaptchaLoginForm && captchaLoginForm.isActiveAndEnabled) {
            captchaLoginForm.gameObject.SetActive(false);
        }

        if (!(modeSelectGroup.showSaveSlotSelectPanel || modeSelectGroup.showAllSettingsPanel || modeSelectGroup.showCaptchaLoginForm)) {
            if (!modeSelectGroup.isActiveAndEnabled) {
                modeSelectGroup.gameObject.SetActive(true);
            }
            cancelBtn.transform.localScale = Vector3.zero;
        }
    }

    void toggleUIInteractability(bool enabled) {
        modeSelectGroup.toggleUIInteractability(enabled);
        allSettingsPanel.toggleUIInteractability(enabled);
        saveSlotSelectPanel.toggleUIInteractability(enabled);
    }
}
