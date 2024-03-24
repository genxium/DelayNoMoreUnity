using System.Linq;
using UnityEngine;

public class AbstractSingleSelectGroup : MonoBehaviour {
    public bool doubleClickToConfirm = false;
    protected int selectedIdx = 0;
    public AbstractSingleSelectCell[] cells = null;

    // Start is called before the first frame update
    void Start() {
        Debug.Log("AbstractSingleSelectGroup: cells count = " + cells.Length);
    }

    // Update is called once per frame
    void Update() {

    }

    public void onCellSelected(int newSelectedIdx) {
        cells[selectedIdx].setSelected(false);
        cells[newSelectedIdx].setSelected(true);
        selectedIdx = newSelectedIdx;
    }

    public void MoveSelection(int delta) {
        int newSelectedIdx = selectedIdx + delta;
        if (0 > newSelectedIdx || newSelectedIdx >= cells.Length) return;
        onCellSelected(newSelectedIdx);
    }

    public void toggleUIInteractability(bool val) {
        foreach (var cell in cells) {
            cell.toggleUIInteractability(val);
        }
    }
}
