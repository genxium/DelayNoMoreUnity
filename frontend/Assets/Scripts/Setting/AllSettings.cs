using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class AllSettings : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    private LoginStatusBarController sameSceneLoginStatusBar;
    protected bool currentSelectPanelEnabled = false;
    public SettingsSelectGroup allSettingsSelectGroup;
    public delegate void SimpleDelegate();
    public Image cancelBtn;

    public void SetCallbacks() {
        allSettingsSelectGroup.postConfirmedCallback = (int val) => {
            if (0 == val) {
                onLogoutClicked();
            }
        };

        toggleUIInteractability(true);
    }

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        allSettingsSelectGroup.toggleUIInteractability(val);
        if (val) {
            cancelBtn.gameObject.transform.localScale = Vector3.one;
        } else {
            cancelBtn.gameObject.transform.localScale = Vector3.zero;
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
    }

    public void SetSameSceneLoginStatusBar(LoginStatusBarController thatBar) {
        sameSceneLoginStatusBar = thatBar;
    }

    public void onLogoutClicked() {
        if (!currentSelectPanelEnabled) return;
        // TODO: Actually send a "/Auth/Logout" request to clear on backend as well.
        WsSessionManager.Instance.ClearCredentials();
        loginStatusBarController.SetLoggedInData(null);
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(null);
        }
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

    private void Update() {

    }
}
