using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;
using shared;

public class CharacterSelectGroup : AbstractSingleSelectGroup {

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
            Debug.Log("CharacterSelectGroup OnBtnConfirm");
            toggleUIInteractability(false);
            confirmSelection();
        }
    }

    public override void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectGroupEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            Debug.Log("CharacterSelectGroup OnBtnConfirm");
            if (null != postCancelledCallback) {
                if (null != uiSoundSource) {
                    uiSoundSource.PlayCancel();
                }
                postCancelledCallback();
            }
            toggleUIInteractability(false);
        }
    }

    public override void onCellSelected(int newSelectedIdx) {
        if (newSelectedIdx == selectedIdx) {
            confirmSelection();
        } else {
            if (null != uiSoundSource) {
                uiSoundSource.PlayCursor();
            }
            cells[selectedIdx].setSelected(false);
            cells[newSelectedIdx].setSelected(true);
            selectedIdx = newSelectedIdx;
        }
    }

    private void confirmSelection() {
        uint selectedSpeciesId = Battle.SPECIES_NONE_CH;
        switch (selectedIdx) {
            case 0:
                selectedSpeciesId = Battle.SPECIES_BLADEGIRL;
                //selectedSpeciesId = Battle.SPECIES_HEAVYGUARD_RED;
                //selectedSpeciesId = Battle.SPECIES_SKELEARCHER;
                //selectedSpeciesId = Battle.SPECIES_RIDLEYDRAKE;
                break;
            case 1:
                selectedSpeciesId = Battle.SPECIES_WITCHGIRL;
                //selectedSpeciesId = Battle.SPECIES_DEMON_FIRE_SLIME;
                break;
            case 2:
                selectedSpeciesId = Battle.SPECIES_MAGSWORDGIRL;
                //selectedSpeciesId = Battle.SPECIES_STONE_GOLEM;
                break;
            case 3:
                selectedSpeciesId = Battle.SPECIES_BRIGHTWITCH;
                break;
            case 4:
                selectedSpeciesId = Battle.SPECIES_BOUNTYHUNTER;
                break;
            case 5:
                selectedSpeciesId = Battle.SPECIES_SPEARWOMAN;
                //selectedSpeciesId = Battle.SPECIES_SWORDMAN;
                break;
        }
        if (null != postConfirmedCallback) {
            if (null != uiSoundSource) {
                uiSoundSource.PlayPositive();
            }
            postConfirmedCallback((int)selectedSpeciesId);
        }
    }

    public override void toggleUIInteractability(bool val) {
        base.toggleUIInteractability(val);
    }
}
