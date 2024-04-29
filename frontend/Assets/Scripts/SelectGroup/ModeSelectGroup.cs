using shared;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

public class ModeSelectGroup : AbstractSingleSelectGroup {
    private AllSettings allSettingsPanel;

    public delegate void ParentUIInteractabilityDelegate(bool val);
    public delegate void OnLoginRequiredDelegate(WsSessionManager.OnLoginResult callback);
    private OnLoginRequiredDelegate onLoginRequired = null;
    private ParentUIInteractabilityDelegate parentUIInteractabilityToggle = null;
    
    public void SetAllSettingsPanel(AllSettings theAllSettingsPanel) {
        allSettingsPanel = theAllSettingsPanel;
    }

    public void SetOnLoginRequired(OnLoginRequiredDelegate newOnLoginRequired) {
        onLoginRequired = newOnLoginRequired;
    }

    public void SetParentUIInteractabilityToggle(ParentUIInteractabilityDelegate newParentUIInteractabilityToggle) {
        parentUIInteractabilityToggle = newParentUIInteractabilityToggle;
    }

    public void ConfirmSelection() {
        if (!currentSelectGroupEnabled) return;
        parentUIInteractabilityToggle(false);
        switch (selectedIdx) {
            case 0:
                enterStoryMode();
            break;
            case 1:
                tryEnteringOnlineArena();
            break;
            case 2:
                enterAllSettings();
            break;
            default:
            break;
        }
    }

    private void enterStoryMode() {
        SceneManager.LoadScene("OfflineMapScene", LoadSceneMode.Single);
    }

    private void enterAllSettings() {
        allSettingsPanel.gameObject.SetActive(true);
        allSettingsPanel.toggleUIInteractability(true);
    }

    private WsSessionManager.OnLoginResult onLoggedInPerAdhocRequirement = (int retCode, string? uname, int? playerId, string? authToken) => {
        if (ErrCode.Ok != retCode) {
            // TODO: Popup dismissable error message prompt!
            return;
        }
        SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
    };

    public void tryEnteringOnlineArena() {
        if (WsSessionManager.Instance.IsPossiblyLoggedIn()) {
            SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
        } else {
            onLoginRequired(onLoggedInPerAdhocRequirement);
        }
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        bool rising = context.ReadValueAsButton();
        if (rising) {
            ConfirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        throw new System.NotImplementedException();
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (!currentSelectGroupEnabled) return;
        if (newSelectedIdx == selectedIdx) {
            ConfirmSelection();
        } else {
            cells[selectedIdx].setSelected(false);
            cells[newSelectedIdx].setSelected(true);
            selectedIdx = newSelectedIdx;
        }
    }

    public override void OnMoveByKeyboard(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        var kctrl = (KeyControl)context.control;
        if (null == kctrl || !kctrl.wasReleasedThisFrame) return;
        switch (kctrl.keyCode) {
            case Key.W:
            case Key.UpArrow:
                MoveSelection(-1);
                break;
            case Key.S:
            case Key.DownArrow:
                MoveSelection(+1);
                break;
        }
    }

    public override void toggleUIInteractability(bool val) {
        base.toggleUIInteractability(val);
    }
}
