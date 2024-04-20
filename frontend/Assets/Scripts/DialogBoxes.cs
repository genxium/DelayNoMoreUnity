using DG.Tweening;
using shared;
using System;
using System.Collections;
using System.Collections.Immutable;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DialogBoxes : MonoBehaviour {
    public GameObject dialogUp, dialogDown;
    public Image avatarDown, avatarUp;
    public TMP_Text textDown, textUp;
    protected int stepCnt = 0;
    protected int renderingStepCnt = 0;
    protected bool currentSelectPanelEnabled = false;
    public Image nextBtn, cancelBtn; // to toggle interactability

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

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
        throw new NotImplementedException();
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

    public bool renderStoryPoint(RoomDownsyncFrame rdf, int levelId, int storyPointId) {
        ImmutableDictionary<int, ImmutableArray<ImmutableArray<StoryPointDialogLine>>> levelStory = Story.StoryConstants.STORIES_OF_LEVELS[levelId];
        ImmutableArray<ImmutableArray<StoryPointDialogLine>> storyPoint = levelStory[storyPointId];
        if (stepCnt >= storyPoint.Length) {
            dialogUp.SetActive(false);
            dialogDown.SetActive(false);
            toggleUIInteractability(false);
            return false;
        } else {
            if (renderingStepCnt < stepCnt) {
                dialogUp.SetActive(false);
                dialogDown.SetActive(false);
                toggleUIInteractability(false);
                ImmutableArray<StoryPointDialogLine> storyPointStep = storyPoint[stepCnt];
                StartCoroutine(renderStoryPointStep(rdf, storyPointStep));
            }
            
            return true;
        }
    }

    protected IEnumerator renderStoryPointStep(RoomDownsyncFrame rdf, ImmutableArray<StoryPointDialogLine> step) {
        // Hide "up" dialog box by default
        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < step.Length; i++) {
            var line = step[i];
            if (line.DownOrNot) {
                textDown.text = line.Content;
                dialogDown.SetActive(true);
            } else {
                textUp.text = line.Content;
                dialogUp.SetActive(true);
            }

            int speciesIdInAvatar = Battle.SPECIES_NONE_CH;
            if (Battle.SPECIES_NONE_CH != line.NarratorSpeciesId) {
                speciesIdInAvatar = line.NarratorSpeciesId;
            } else {
                speciesIdInAvatar = rdf.PlayersArr[line.NarratorJoinIndex - 1].SpeciesId;
            }

            var chConfig = Battle.characters[speciesIdInAvatar];
            string speciesName = chConfig.SpeciesName;
            string spriteSheetPath = String.Format("Characters/{0}/{0}", speciesName, speciesName);
            var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
            if (null == sprites || chConfig.UseIsolatedAvatar) {
                var sprite = Resources.Load<Sprite>(String.Format("Characters/{0}/Avatar_1", speciesName));
                if (null != sprite) {
                    if (line.DownOrNot) {
                        avatarDown.sprite = sprite;
                    } else {
                        avatarUp.sprite = sprite;
                    }
                }
            } else {
                foreach (Sprite sprite in sprites) {
                    if ("Avatar_1".Equals(sprite.name)) {
                        if (line.DownOrNot) {
                            avatarDown.sprite = sprite;
                        } else {
                            avatarUp.sprite = sprite;
                        }
                        break;
                    }
                }
            }
        }
        renderingStepCnt = stepCnt;
        toggleUIInteractability(true);
    }
}
