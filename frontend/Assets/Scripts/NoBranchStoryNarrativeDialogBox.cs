using shared;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NoBranchStoryNarrativeDialogBox : MonoBehaviour {
    public GameObject dialogUp, dialogDown;
    public Image avatarDown, avatarUp;
    public TMP_Text textDown, textUp;
    protected int stepCnt = 0;
    protected int renderingStepCnt = 0;
    protected bool currentSelectPanelEnabled = false;
    public Image nextBtn, cancelBtn; // to toggle interactability

    public void init() {
        stepCnt = 0;
        renderingStepCnt = -1;
    }

    public virtual void OnNextBtnClicked() {
        stepCnt++;
    }

    public void toggleUIInteractability(bool val) {
        currentSelectPanelEnabled = val;
        if (null != nextBtn) {
            if (val) {
                nextBtn.gameObject.transform.localScale = Vector3.one;
            } else {
                nextBtn.gameObject.transform.localScale = Vector3.zero;
            }
        }
        if (null != cancelBtn) {
            if (val) {
                cancelBtn.gameObject.transform.localScale = Vector3.one;
            } else {
                cancelBtn.gameObject.transform.localScale = Vector3.zero;
            }
        }
    }

    public void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentSelectPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            Skip();
        }
    }

    public void OnBtnConfirm(InputAction.CallbackContext context) {
        if (!currentSelectPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            IncrementStep();
        }
    }

    public void IncrementStep() {
        stepCnt++;
        Debug.Log(String.Format("IncrementStep executed, now stepCnt = " + stepCnt));
    }

    public void Skip() {
        stepCnt = Battle.MAX_INT;
        Debug.Log(String.Format("Skip executed, now stepCnt = " + stepCnt));
    }

    public bool renderStoryPoint(RoomDownsyncFrame rdf, StoryPoint storyPoint) {
        if (stepCnt >= storyPoint.Steps.Count) {
            dialogUp.SetActive(false);
            dialogDown.SetActive(false);
            toggleUIInteractability(false);
            return false;
        } else {
            if (renderingStepCnt < stepCnt) {
                dialogUp.SetActive(false);
                dialogDown.SetActive(false);
                toggleUIInteractability(false);
                StoryPointStep storyPointStep = storyPoint.Steps[stepCnt];
                StartCoroutine(renderStoryPointStep(rdf, storyPointStep));
            }
            
            return true;
        }
    }

    protected IEnumerator renderStoryPointStep(RoomDownsyncFrame rdf, StoryPointStep step) {
        // Hide "up" dialog box by default
        yield return new WaitForSeconds(0.1f);

        foreach (var line in step.Lines) {
            if (line.DownOrNot) {
                textDown.text = line.Content;
                dialogDown.SetActive(true);
            } else {
                textUp.text = line.Content;
                dialogUp.SetActive(true);
            }

            uint speciesIdInAvatar = Battle.SPECIES_NONE_CH;
            if (Battle.SPECIES_NONE_CH != line.NarratorSpeciesId) {
                speciesIdInAvatar = line.NarratorSpeciesId;
            } else {
                speciesIdInAvatar = rdf.PlayersArr[line.NarratorJoinIndex - 1].SpeciesId;
            }
            var chConfig = Battle.characters[speciesIdInAvatar];
            if (line.DownOrNot) {
                AvatarUtil.SetAvatar1(avatarDown, chConfig);
            } else {
                AvatarUtil.SetAvatar1(avatarUp, chConfig);
            }
        }
        renderingStepCnt = stepCnt;
        toggleUIInteractability(true);
    }
}
