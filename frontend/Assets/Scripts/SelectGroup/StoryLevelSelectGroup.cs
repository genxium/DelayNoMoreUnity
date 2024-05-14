using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using UnityEngine;

public class StoryLevelSelectGroup : AbstractSingleSelectGroup {
    public delegate void StoryLevelPostConfirmedCallbackT(int idx, string name);
    public StoryLevelPostConfirmedCallbackT levelPostConfirmedCallback;

    protected string selectedName = null;

    public override void OnMoveByKeyboard(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        var kctrl = (KeyControl)context.control;
        if (null == kctrl || !kctrl.wasReleasedThisFrame) return;
        switch (kctrl.keyCode) {
            case Key.A:
            case Key.LeftArrow:
                MoveSelection(-1);
                break;
            case Key.D:
            case Key.RightArrow:
                MoveSelection(+1);
                break;
        }
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            Debug.Log("StoryLevelSelectGroup OnBtnConfirm");
            toggleUIInteractability(false);
            confirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            Debug.Log("StoryLevelSelectGroup OnBtnConfirm");
            if (null != postCancelledCallback) {
                postCancelledCallback();
            }
        }
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (newSelectedIdx == selectedIdx) {
            confirmSelection();
        } else {
            cells[selectedIdx].setSelected(false);
            cells[newSelectedIdx].setSelected(true);
            selectedIdx = newSelectedIdx;
            selectedName = cells[newSelectedIdx].name;
        }
    }

    private void confirmSelection() {
        if (null != levelPostConfirmedCallback) {
            levelPostConfirmedCallback(selectedIdx, selectedName);
        }
    }

    public override void toggleUIInteractability(bool val) {
        base.toggleUIInteractability(val);
    }
}
