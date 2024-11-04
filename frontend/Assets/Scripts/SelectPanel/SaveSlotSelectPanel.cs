using static SaveSlotSelectGroup;
using UnityEngine;
using UnityEngine.InputSystem;
using shared;
using UnityEngine.SceneManagement;

public class SaveSlotSelectPanel : MonoBehaviour {
    public UISoundSource uiSoundSource;
    protected bool currentSelectPanelEnabled = false;
    public SaveSlotSelectGroup saveSlotSelectGroup;
    public GameObject cancelBtn;

    // Start is called before the first frame update
    void Start() {
        ResetSelf();
    }

    private void OnEnable() {
        ResetSelf();
    }

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        saveSlotSelectGroup.toggleUIInteractability(val);
        if (val) {
            cancelBtn.transform.localScale = Vector3.one;
        } else {
            cancelBtn.transform.localScale = Vector3.zero;
        }
    }

    public delegate void ParentUIInteractabilityDelegate(bool val);
    protected ParentUIInteractabilityDelegate parentUIInteractabilityToggle = null;
    public void SetParentUIInteractabilityToggle(ParentUIInteractabilityDelegate newParentUIInteractabilityToggle) {
        parentUIInteractabilityToggle = newParentUIInteractabilityToggle;
    }

    public void SetCallbacks(DeleteClickedCallbackT theDeleteClickedCallback, AbstractSingleSelectGroup.PostCancelledCallbackT thePostCancelledCallback) {
        saveSlotSelectGroup.postCancelledCallback = thePostCancelledCallback;
        saveSlotSelectGroup.deleteClickedCallback = theDeleteClickedCallback;
        saveSlotSelectGroup.postConfirmedCallback = (int selectedIdx) => {
            toggleUIInteractability(false);
            if (null != uiSoundSource) {
                uiSoundSource.PlayPositive();
            }
            parentUIInteractabilityToggle(false);
            PlayerStoryProgressManager.Instance.LoadFromSlot(selectedIdx+1);
            SceneManager.LoadScene("StoryLevelSelectScene", LoadSceneMode.Single);
        };
        toggleUIInteractability(true);
    }


    public void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising) {
            toggleUIInteractability(false);
            OnCancel();
        }
    }

    public void OnCancel() {
        if (null != uiSoundSource) {
            uiSoundSource.PlayCancel();
        }
        if (null != saveSlotSelectGroup.postCancelledCallback) {
            saveSlotSelectGroup.postCancelledCallback();
        }
    }

    public void ResetSelf() {
        PlayerStoryProgress[] headings = PlayerStoryProgressManager.Instance.LoadHeadingsFromAllSaveSlots();
        for (int slotId = 1; slotId <= headings.Length; slotId++) {
            var cell = saveSlotSelectGroup.cells[slotId - 1] as SaveSlot;
            cell.UpdateByStoryProgress(headings[slotId-1]);
        }
        toggleUIInteractability(true);
    }
}
