using shared;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoplayStoryNarrativeDialogBox : MonoBehaviour {
    public GameObject dialogUp, dialogDown;
    public Image avatarDown, avatarUp;
    public TMP_Text textDown, textUp;
    protected int lastStepChangedAtRdfId = Battle.TERMINATING_RENDER_FRAME_ID;
    protected int stepCnt = 0;
    protected bool currentSelectPanelEnabled = false;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void init() {
        stepCnt = 0;
        lastStepChangedAtRdfId = Battle.TERMINATING_RENDER_FRAME_ID;
    }

    public void IncrementStep(int rdfId) {
        stepCnt++;
        lastStepChangedAtRdfId++;
        Debug.LogFormat("Step executed, now stepCnt={0} @ rdfId={1}", stepCnt, rdfId);
    }

    public void Skip() {
        stepCnt = Battle.MAX_INT;
        Debug.Log(String.Format("Skip executed, now stepCnt = " + stepCnt));
    }

    public bool renderStoryPoint(RoomDownsyncFrame rdf, StoryPoint storyPoint) {
        if (Battle.TERMINATING_RENDER_FRAME_ID == lastStepChangedAtRdfId) {
            lastStepChangedAtRdfId = rdf.Id;
        } else {
            var renderingStep = storyPoint.Steps[stepCnt];
            int elapsedRdfCount = rdf.Id - lastStepChangedAtRdfId;
            if (elapsedRdfCount > renderingStep.AutoplayAliveRdfCount) {
                IncrementStep(rdf.Id);
            } else {
                return true;
            }
        }

        dialogUp.SetActive(false);
        dialogDown.SetActive(false);
        if (stepCnt >= storyPoint.Steps.Count) {
            lastStepChangedAtRdfId = Battle.TERMINATING_RENDER_FRAME_ID;
            return false;
        } else {
            StoryPointStep storyPointStep = storyPoint.Steps[stepCnt];
            StartCoroutine(renderStoryPointStep(rdf, storyPointStep));
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
            string speciesName = chConfig.SpeciesName;
            string spriteSheetPath = String.Format("Characters/{0}/{0}", speciesName, speciesName);
            var sprites = Resources.LoadAll<Sprite>(spriteSheetPath);
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
}
