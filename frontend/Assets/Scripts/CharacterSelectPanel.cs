using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using TMPro;
public class CharacterSelectPanel : MonoBehaviour {

    int selectedSequenceId = 0;
    int selectedSpeciesId = 2;
 
    public delegate void OnGoActionCallback(int speciesId);

    public Button GoActionButton;
    public OnGoActionCallback goAction;
    public GameObject characters;

    // Start is called before the first frame update
    void Start() {
        int i = 0;
        foreach (Transform child in characters.transform) {
            var btn = child.gameObject.GetComponentInChildren<Button>();
            switch (i) {    
            case 0:
                btn.onClick.AddListener(delegate { onAvatarClicked(i, 2); });
                break;
            case 1:
                btn.onClick.AddListener(delegate { onAvatarClicked(i, 0); });
               break;
            }
            i++;
        }
    }

    // Update is called once per frame
    void Update() {

    }

    void toggleUIInteractability(bool enabled) {
        GoActionButton.interactable = enabled;
    }

    public void OnGoActionClicked() {
        toggleUIInteractability(false);
        if (null == goAction) {
            goAction(selectedSpeciesId);
        }
    }

    private void onAvatarClicked(int sequenceId, int speciesId) {
        selectedSequenceId = sequenceId;
        selectedSpeciesId = speciesId;

        int i = 0;
        foreach (Transform child in characters.transform) {
            var chosen = child.gameObject.GetComponentInChildren<TMP_Text>(); 
            if (i == selectedSequenceId) {
                chosen.text = "Y";
            } else {
                chosen.text = "";
            } 
            i++;
        }
    }
}
