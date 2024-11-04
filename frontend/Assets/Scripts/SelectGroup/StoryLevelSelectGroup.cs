using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

public class StoryLevelSelectGroup : AbstractSprOnlySingleSelectGroup {
    public delegate void StoryLevelPostConfirmedCallbackT(int idx);
    public StoryLevelPostConfirmedCallbackT levelPostConfirmedCallback;

    public delegate void StoryLevelPostCursorMovedCallbackT(int idx);
    public StoryLevelPostCursorMovedCallbackT levelPostCursorMovedCallback;

    public override void OnMoveByKeyboard(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        var kctrl = (KeyControl)context.control;
        if (null == kctrl || !kctrl.wasReleasedThisFrame) return;
        int newSelectedIdx = selectedIdx;
        switch (kctrl.keyCode) {
            case Key.A:
            case Key.LeftArrow:
                newSelectedIdx = selectedIdx - 1;
                if (0 > newSelectedIdx || newSelectedIdx >= cells.Length) return;
                MoveSelection(-1);
                break;
            case Key.D:
            case Key.RightArrow:
                newSelectedIdx = selectedIdx + 1;
                if (0 > newSelectedIdx || newSelectedIdx >= cells.Length) return;
                MoveSelection(+1);
                break;
        }
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            Debug.Log("StoryLevelSelectGroup OnBtnConfirm");
            confirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            Debug.Log("StoryLevelSelectGroup OnBtnCancel");
            if (null != postCancelledCallback) {
                postCancelledCallback();
            }
        }
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (!currentSelectGroupEnabled) return;
        if (newSelectedIdx == selectedIdx) {
            confirmSelection();
        } else {
            if (null != uiSoundSource) {
                uiSoundSource.PlayCursor();
            }
            cells[selectedIdx].setSelected(false);
            cells[newSelectedIdx].setSelected(true);
            selectedIdx = newSelectedIdx;
            if (null != levelPostCursorMovedCallback) {
                levelPostCursorMovedCallback(selectedIdx);
            }
        }
    }

    public void drySetSelectedIdx(int newSelectedIdx) {
        selectedIdx = newSelectedIdx;
    }

    private void confirmSelection() {
        var targetCell = cells[selectedIdx] as StoryLevelCell;
        if (targetCell.isLocked) {
            Debug.LogFormat("LevelId={0} is locked, rejecting confirmation", targetCell.levelId);
            return;
        }
        toggleUIInteractability(false);
        if (null != levelPostConfirmedCallback) {
            if (null != uiSoundSource) {
                uiSoundSource.PlayPositive();
            }
            levelPostConfirmedCallback(selectedIdx);
        }
    }

    public override void toggleUIInteractability(bool val) {
        base.toggleUIInteractability(val);
        currentSelectGroupEnabled = val;
    }
}
