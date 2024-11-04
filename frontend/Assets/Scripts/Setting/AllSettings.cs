using UnityEngine;
using UnityEngine.InputSystem;

public class AllSettings : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    private LoginStatusBarController sameSceneLoginStatusBar;
    protected bool currentSelectPanelEnabled = false;
    public AllSettingsSelectGroup allSettingsSelectGroup;
    public delegate void SimpleDelegate();
    public GameObject cancelBtn;

    SimpleDelegate postCancelledCb = null;

    public void SetCallbacks(SimpleDelegate thePostCancelledCb) {
        allSettingsSelectGroup.postConfirmedCallback = (int val) => {
            if (0 == val) {
                onLogoutClicked();
            }
        };

        postCancelledCb = thePostCancelledCb;
        toggleUIInteractability(true);
    }

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        allSettingsSelectGroup.toggleUIInteractability(val);
        if (val) {
            cancelBtn.transform.localScale = Vector3.one;
            bool logoutBtnShouldBeInteractable = (null != WsSessionManager.Instance.GetUname());
            allSettingsSelectGroup.toggleLogoutBtnInteractability(logoutBtnShouldBeInteractable);
        } else {
            cancelBtn.transform.localScale = Vector3.zero;
        }
    }

    public void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            OnCancel();
        }
    }

    public void OnCancel() {
        gameObject.SetActive(false);
        if (null != postCancelledCb) {
            postCancelledCb();
        }
    }

    public void SetSameSceneLoginStatusBar(LoginStatusBarController thatBar) {
        sameSceneLoginStatusBar = thatBar;
    }

    public void onLogoutClicked() {
        if (!currentSelectPanelEnabled) return;
        toggleUIInteractability(false);
        // TODO: Actually send a "/Auth/Logout" request to clear on backend as well.
        WsSessionManager.Instance.ClearCredentials();
        if (null != allSettingsSelectGroup.uiSoundSource) {
            allSettingsSelectGroup.uiSoundSource.PlayNegative();
        }
        loginStatusBarController.SetLoggedInData(null);
        allSettingsSelectGroup.toggleLogoutBtnInteractability(false);
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(null);
        }
        toggleUIInteractability(true);
    }

    private void Start() {
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(WsSessionManager.Instance.GetUname());
        }
    }

    private void Awake() {
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(WsSessionManager.Instance.GetUname());
        }
    }
}
