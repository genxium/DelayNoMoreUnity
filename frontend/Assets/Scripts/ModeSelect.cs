using shared;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelect : AbstractSingleSelectGroup {

    // Start is called before the first frame update
    void Start() {
        Debug.Log("ModeSelect: cells count = " + cells.Length);
    }

    // Update is called once per frame
    void Update() {

    }

    public delegate void OnLoginRequiredDelegate(WsSessionManager.OnLoginResult callback);

    public void ConfirmSelection(OnLoginRequiredDelegate onLoginRequired) {
        switch (selectedIdx) {
            case 0:
                enterStoryMode();
            break;
            case 1:
                tryEnteringOnlineArena(onLoginRequired);
            break;
        }
    }

    private void enterStoryMode() {
        SceneManager.LoadScene("OfflineMapScene", LoadSceneMode.Single);
    }

    private WsSessionManager.OnLoginResult onLoggedInPerAdhocRequirement = (int retCode, string? uname, int? playerId, string? authToken) => {
        if (ErrCode.Ok != retCode) {
            // TODO: Popup dismissable error message prompt!
            return;
        }
        SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
    };

    private void tryEnteringOnlineArena(OnLoginRequiredDelegate onLoginRequired) {
        if (WsSessionManager.Instance.IsPossiblyLoggedIn()) {
            SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
        } else {
            onLoginRequired(onLoggedInPerAdhocRequirement);
        }
    }
}
