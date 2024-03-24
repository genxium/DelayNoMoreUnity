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
    private OnLoginRequiredDelegate onLoginRequired = null;

    public void SetOnLoginRequired(OnLoginRequiredDelegate newOnLoginRequired) {
        onLoginRequired = newOnLoginRequired;
    }

    public void ConfirmSelection() {
        switch (selectedIdx) {
            case 0:
                enterStoryMode();
            break;
            case 1:
                TryEnteringOnlineArena();
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

    public void TryEnteringOnlineArena() {
        if (WsSessionManager.Instance.IsPossiblyLoggedIn()) {
            SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
        } else {
            onLoginRequired(onLoggedInPerAdhocRequirement);
        }
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (newSelectedIdx == selectedIdx) {
            ConfirmSelection();
        } else {
            cells[selectedIdx].setSelected(false);
            cells[newSelectedIdx].setSelected(true);
            selectedIdx = newSelectedIdx;
        }
    }

}
