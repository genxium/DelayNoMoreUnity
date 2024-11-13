using shared;
using Story;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

public class ModeSelectGroup : AbstractSingleSelectGroup {
    private SaveSlotSelectPanel saveSlotSelectPanel;
    private AllSettings allSettingsPanel;

    public bool showCaptchaLoginForm = false;
    public bool showAllSettingsPanel = false;
    public bool showSaveSlotSelectPanel = false;

    public void resetShouldShowMarks() {
        showCaptchaLoginForm = false;
        showAllSettingsPanel = false;
        showSaveSlotSelectPanel = false;
    }

    public void SetSaveSlotSelectPanel(SaveSlotSelectPanel theSaveSlotSelectPanel) {
        saveSlotSelectPanel = theSaveSlotSelectPanel;
    }

    public void SetAllSettingsPanel(AllSettings theAllSettingsPanel) {
        allSettingsPanel = theAllSettingsPanel;
    }

    public delegate void OnLoginRequiredDelegate(WsSessionManager.OnLoginResult callback);
    private OnLoginRequiredDelegate onLoginRequired = null;
    public void SetOnLoginRequired(OnLoginRequiredDelegate newOnLoginRequired) {
        onLoginRequired = newOnLoginRequired;
    }

    public delegate void ParentUIInteractabilityDelegate(bool val);
    protected ParentUIInteractabilityDelegate parentUIInteractabilityToggle = null;
    public void SetParentUIInteractabilityToggle(ParentUIInteractabilityDelegate newParentUIInteractabilityToggle) {
        parentUIInteractabilityToggle = newParentUIInteractabilityToggle;
    }

    public void ConfirmSelection() {
        if (!currentSelectGroupEnabled) return;
        parentUIInteractabilityToggle(false);
        if (null != uiSoundSource) {
            uiSoundSource.PlayPositive();
        }
        switch (selectedIdx) {
            case 0:
                enterStoryMode();
                gameObject.SetActive(false);
                break;
            case 1:
                tryEnteringOnlineArena();
                gameObject.SetActive(false);
                break;
            case 2:
                enterAllSettings();
                gameObject.SetActive(false);
                break;
            default:
            break;
        }
    }

    private void enterStoryMode() {
        if (!PlayerStoryProgressManager.Instance.HasAnyUsedSlot()) {
            // A shortcut to start!
            PlayerStoryProgressManager.Instance.LoadFromSlot(1);
            PlayerStoryProgressManager.Instance.SetCachedForOfflineMap(Battle.SPECIES_BLADEGIRL, StoryConstants.LEVEL_NAMES[StoryConstants.LEVEL_DELICATE_FOREST], FinishedLvOption.StoryAndBoss);
            SceneManager.LoadScene("OfflineMapScene", LoadSceneMode.Single);
        } else {
            showSaveSlotSelectPanel = true;
            saveSlotSelectPanel.gameObject.SetActive(true);
            saveSlotSelectPanel.toggleUIInteractability(true);
        }
    }

    private void enterAllSettings() {
        showAllSettingsPanel = true;
        allSettingsPanel.gameObject.SetActive(true);
        allSettingsPanel.toggleUIInteractability(true);
    }

#nullable enable
    private WsSessionManager.OnLoginResult onLoggedInPerAdhocRequirement = (int retCode, string? uname, int? playerId, string? authToken) => {
        if (ErrCode.Ok != retCode) {
            // TODO: Popup dismissible error message prompt!
            return;
        }
        SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
    };
#nullable disable

    public void tryEnteringOnlineArena() {
        if (WsSessionManager.Instance.IsPossiblyLoggedIn()) {
            SceneManager.LoadScene("OnlineMapScene", LoadSceneMode.Single);
        } else {
            showCaptchaLoginForm = true;
            onLoginRequired(onLoggedInPerAdhocRequirement);
        }
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
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
            if (null != uiSoundSource) {
                uiSoundSource.PlayCursor();
            }
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
