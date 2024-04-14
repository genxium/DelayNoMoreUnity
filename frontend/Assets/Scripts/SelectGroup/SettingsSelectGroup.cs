using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine;

public class SettingsSelectGroup : AbstractSingleSelectGroup {
    public delegate void SimpleDelegate();

    public void ConfirmSelection() {
        switch (selectedIdx) {
            case 0:
                if (null != postConfirmedCallback) {
                    postConfirmedCallback(0);
                }
            break;
            default:
            break;
        }
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            Debug.Log("StoryModeSettingsSelectGroup OnBtnConfirm");
            toggleUIInteractability(false);
            ConfirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        throw new System.NotImplementedException();
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

    public override void OnMoveByKeyboard(InputAction.CallbackContext context) {
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
