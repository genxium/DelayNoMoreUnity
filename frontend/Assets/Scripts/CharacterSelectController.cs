using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.Networking;

public class CharacterSelectController : MonoBehaviour {
 
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
