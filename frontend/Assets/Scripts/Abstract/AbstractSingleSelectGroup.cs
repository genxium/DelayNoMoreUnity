using UnityEngine;
using UnityEngine.InputSystem;

public abstract class AbstractSingleSelectGroup : MonoBehaviour {
    public UISoundSource uiSoundSource;
    public delegate void PostCancelledCallbackT();
    public PostCancelledCallbackT postCancelledCallback;

    public delegate void PostConfirmedCallbackT(int val);
    public PostConfirmedCallbackT postConfirmedCallback;

    protected int selectedIdx = 0;
    protected bool currentSelectGroupEnabled = false;

    public AbstractSingleSelectCell[] cells = null;

    public abstract void OnMoveByKeyboard(InputAction.CallbackContext context);
    public abstract void OnBtnConfirm(InputAction.CallbackContext context);

    public abstract void OnBtnCancel(InputAction.CallbackContext context);

    public virtual void onCellSelected(int newSelectedIdx) {
        cells[selectedIdx].setSelected(false);
        cells[newSelectedIdx].setSelected(true);
        selectedIdx = newSelectedIdx;
    }

    public void MoveSelection(int delta) {
        int newSelectedIdx = selectedIdx + delta;
        if (0 > newSelectedIdx || newSelectedIdx >= cells.Length) return;
        onCellSelected(newSelectedIdx);
    }

    public virtual void toggleUIInteractability(bool val) {
        foreach (var cell in cells) {
            cell.toggleUIInteractability(val);
        }
        currentSelectGroupEnabled = val;
    }

    public virtual void ResetCells(AbstractSingleSelectCell[] newCells, bool deleteComponent) {
        if (null != cells) {
            foreach (var cell in cells) {
                if (deleteComponent) {
                    Destroy(cell);
                } else {
                    Destroy(cell.gameObject);
                }
            }
        }

        cells = newCells;
    }
}
