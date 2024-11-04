using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using DG.Tweening;

public class SaveSlotSelectGroup : AbstractSingleSelectGroup {

    public delegate void DeleteClickedCallbackT(int val);
    public DeleteClickedCallbackT deleteClickedCallback;

    public GameObject btnDel;

    public void OnBtnDelete(InputAction.CallbackContext context) {
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            btnDel.transform.DOScale(0.5f * Vector3.one, 0.1f);
            if (null != deleteClickedCallback) {
                if (null != uiSoundSource) {
                    uiSoundSource.PlayNegative();
                }
                int slotId = selectedIdx + 1;
                deleteClickedCallback(slotId);
            }
        } else {
            btnDel.transform.DOScale(1.0f * Vector3.one, 0.3f);
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        throw new System.NotImplementedException();
    }

    public override void OnBtnConfirm(InputAction.CallbackContext context) {
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            if (null != postConfirmedCallback) {
                postConfirmedCallback(selectedIdx);
            }         
        }
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (!currentSelectGroupEnabled) return;
        if (newSelectedIdx == selectedIdx) {
            if (null != postConfirmedCallback) {
                postConfirmedCallback(selectedIdx);
            }
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
        if (val) {
            btnDel.gameObject.transform.localScale = Vector3.one;
        } else {
            btnDel.gameObject.transform.localScale = Vector3.zero;
        }
    }
}
