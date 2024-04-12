using UnityEngine;
using UnityEngine.UI;

public class AllSettings : MonoBehaviour {
    public LoginStatusBarController loginStatusBarController;
    public Button logoutButton;
    private LoginStatusBarController sameSceneLoginStatusBar;
    public void OnClose() {
        gameObject.SetActive(false);
    }

    public void SetSameSceneLoginStatusBar(LoginStatusBarController thatBar) {
        sameSceneLoginStatusBar = thatBar;
    }

    public void onLogoutClicked() {
        // TODO: Actually send a "/Auth/Logout" request to clear on backend as well.
        WsSessionManager.Instance.ClearCredentials();
        logoutButton.gameObject.SetActive(false);
        loginStatusBarController.SetLoggedInData(null);
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(null);
        }
    }

    private void Start() {
        logoutButton.gameObject.SetActive(WsSessionManager.Instance.IsPossiblyLoggedIn());
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(WsSessionManager.Instance.GetUname());
        }
    }

    private void Awake() {
        logoutButton.gameObject.SetActive(WsSessionManager.Instance.IsPossiblyLoggedIn());
        loginStatusBarController.SetLoggedInData(WsSessionManager.Instance.GetUname());
        if (null != sameSceneLoginStatusBar) {
            sameSceneLoginStatusBar.SetLoggedInData(WsSessionManager.Instance.GetUname());
        }
    }

    private void Update() {

    }
}
