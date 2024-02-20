using shared;
using System;
using System.Collections.Immutable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogBoxes : MonoBehaviour {
    public GameObject dialogUp, dialogDown;
    public Image avatarDown, avatarUp;
    public TMP_Text textDown, textUp;
    public Button nextBtn; // to toggle interactability
    public int stepCnt = 0; 

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public virtual void OnNextBtnClicked() {
        Debug.Log(String.Format("Next button clicked, now stepCnt = " + stepCnt));
        stepCnt++;
    }

    public bool renderStoryPoint(RoomDownsyncFrame rdf, int levelId, int storyPointId) {
        ImmutableDictionary<int, ImmutableArray<ImmutableArray<StoryPointDialogLine>>> levelStory = Story.StoryConstants.STORIES_OF_LEVELS[levelId];
        ImmutableArray<ImmutableArray<StoryPointDialogLine>> storyPoint = levelStory[storyPointId];
        if (stepCnt >= storyPoint.Length) {
            return false;
        } else {
            ImmutableArray<StoryPointDialogLine> storyPointStep = storyPoint[stepCnt];
            return renderStoryPointStep(rdf, storyPointStep);
        }
    }

    public bool renderStoryPointStep(RoomDownsyncFrame rdf, ImmutableArray<StoryPointDialogLine> step) {
        // Hide "up" dialog box by default
        dialogUp.SetActive(false);
        dialogDown.SetActive(false);

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

            string speciesName = Battle.characters[speciesIdInAvatar].SpeciesName;
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

        return true;
    }
}