using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.Networking;

public class CharacterSelectController : MonoBehaviour {
 
    public delegate void OnCharacterCallback(int speciesId);
    public delegate void OnGoActionCallback();

    public Button GoActionButton;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    void toggleUIInteractability(bool enabled) {
        GoActionButton.interactable = enabled;
    }

    public void OnGoActionClicked() {
        toggleUIInteractability(false);
    }
}
