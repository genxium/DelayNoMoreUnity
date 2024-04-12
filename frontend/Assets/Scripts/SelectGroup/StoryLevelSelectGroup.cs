

using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

public class StoryLevelSelectGroup : AbstractSingleSelectGroup {
    public override void OnMoveByKeyboard(InputAction.CallbackContext context) {
        if (!enabled) return;
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
        if (!enabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising) {
            confirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        if (!enabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising) {
            if (null != postCancelledCallback) {
                postCancelledCallback();
            }
            enabled = false;
        }
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (newSelectedIdx == selectedIdx) {
            confirmSelection();
        } else {
            cells[selectedIdx].setSelected(false);
            cells[newSelectedIdx].setSelected(true);
            selectedIdx = newSelectedIdx;
        }
    }

    private void confirmSelection() {
        if (null != postConfirmedCallback) {
            postConfirmedCallback(selectedIdx);
        }
    }

    public override void toggleUIInteractability(bool val) {
        base.toggleUIInteractability(val);
    }
}
