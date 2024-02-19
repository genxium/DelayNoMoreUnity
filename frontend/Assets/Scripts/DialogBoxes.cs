using shared;
using System;
using System.Collections.Immutable;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogBoxes : MonoBehaviour {
    public GameObject dialogUp;
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

    public bool renderStoryPoint(int levelId, int storyPointId) {
        ImmutableDictionary<int, ImmutableArray<ImmutableArray<StoryPointDialogLine>>> levelStory = Story.StoryConstants.STORIES_OF_LEVELS[levelId];
        ImmutableArray<ImmutableArray<StoryPointDialogLine>> storyPoint = levelStory[storyPointId];
        if (stepCnt >= storyPoint.Length) {
            return false;
        } else {
            ImmutableArray<StoryPointDialogLine> storyPointStep = storyPoint[stepCnt];
            return renderStoryPointStep(storyPointStep);
        }
    }

    public bool renderStoryPointStep(ImmutableArray<StoryPointDialogLine> step) {
        // Hide "up" dialog box by default
        dialogUp.SetActive(false);

        for (int i = 0; i < step.Length; i++) {
            var line = step[i];
            if (line.DownOrNot) {
                textUp.text = line.Content;
            } else {
                textDown.text = line.Content;
                dialogUp.SetActive(true);
            }
        }

        return true;
    }
}