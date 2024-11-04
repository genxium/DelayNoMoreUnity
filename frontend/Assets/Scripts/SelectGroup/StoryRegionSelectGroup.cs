using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

public class StoryRegionSelectGroup : AbstractSprOnlySingleSelectGroup {
    public delegate void StoryRegionPostConfirmedCallbackT(int idx);
    public StoryRegionPostConfirmedCallbackT regionPostConfirmedCallback;

    public delegate void StoryRegionPostCursorMovedCallbackT(int idx);
    public StoryRegionPostCursorMovedCallbackT regionPostCursorMovedCallback;

    public override void OnMoveByKeyboard(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        var kctrl = (KeyControl)context.control;
        if (null == kctrl || !kctrl.wasReleasedThisFrame) return;
        int newSelectedIdx = selectedIdx;
        var targetCell = cells[newSelectedIdx] as StoryRegionCell;
        switch (kctrl.keyCode) {
            case Key.A:
            case Key.LeftArrow:
                newSelectedIdx = selectedIdx - 1;
                if (0 > newSelectedIdx || newSelectedIdx >= cells.Length) return;
                targetCell = cells[newSelectedIdx] as StoryRegionCell;
                if (targetCell.isShadowed) return;
                MoveSelection(-1);
                break;
            case Key.D:
            case Key.RightArrow:
                newSelectedIdx = selectedIdx + 1;
                if (0 > newSelectedIdx || newSelectedIdx >= cells.Length) return;
                targetCell = cells[newSelectedIdx] as StoryRegionCell;
                if (targetCell.isShadowed) return;
                MoveSelection(+1);
                break;
        }
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            Debug.Log("StoryRegionSelectGroup OnBtnConfirm");
            confirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            Debug.Log("StoryRegionSelectGroup OnBtnCancel");
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
            if (null != regionPostCursorMovedCallback) {
                regionPostCursorMovedCallback(selectedIdx);
            }
        }
    }

    private void confirmSelection() {
        var targetCell = cells[selectedIdx] as StoryRegionCell;
        if (targetCell.isLocked) {
            Debug.LogFormat("regionId={0} is locked, rejecting confirmation", targetCell.regionId);
            return;
        }
        if (null != uiSoundSource) {
            uiSoundSource.PlayPositive();
        }
        toggleUIInteractability(false);
        if (null != regionPostConfirmedCallback) {
            regionPostConfirmedCallback(selectedIdx);
        }
    }

    public override void toggleUIInteractability(bool val) {
        base.toggleUIInteractability(val);
        currentSelectGroupEnabled = val;
    }
}
