using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using shared;

public class FinishedLvOptionSelectGroup : AbstractSingleSelectGroup {
    public override void OnBtnCancel(InputAction.CallbackContext context) {
        throw new System.NotImplementedException();
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        throw new System.NotImplementedException();
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

    public void highlightSelected() {
        onCellSelected(selectedIdx);
    }

    public FinishedLvOption getOption() {
        switch (selectedIdx) {
            case 0:
                return FinishedLvOption.BossOnly;
            default:
                return FinishedLvOption.StoryAndBoss;
        }
    }
}
