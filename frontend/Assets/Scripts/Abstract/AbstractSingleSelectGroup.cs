using UnityEngine;

public class AbstractSingleSelectGroup : MonoBehaviour {
    public bool doubleClickToConfirm = false;
    private int selectedIdx = 0;
    public AbstractSingleSelectCell[] cells;
    public void onCellSelected(int newSelectedIdx) {
        cells[selectedIdx].setSelected(false);
        cells[newSelectedIdx].setSelected(true);
        selectedIdx = newSelectedIdx;
    }
}
