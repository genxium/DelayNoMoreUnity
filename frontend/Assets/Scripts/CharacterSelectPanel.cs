using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.SceneManagement;
using System;

public class CharacterSelectPanel : MonoBehaviour {
 
    public Button GoActionButton; // to toggle interactability
    public ToggleGroup characters;

    // Start is called before the first frame update
    void Start() {
    }

    // Update is called once per frame
    void Update() {

    }

    void toggleUIInteractability(bool enabled) {
        GoActionButton.interactable = enabled;
    }

    public void OnGoActionClicked(AbstractMapController map) {
        toggleUIInteractability(false);
        Debug.Log(String.Format("GoAction button clicked with map={0}", map));
        var toggles = characters.gameObject.GetComponentsInChildren<Toggle>();
        int selectedSpeciesId = 0;
        foreach (var toggle in toggles) {
            if (null != toggle && toggle.isOn) {
                Debug.Log(String.Format("{0} chosen", toggle.name));
                switch (toggle.name) {      
                case "KnifeGirl":
                    selectedSpeciesId = 0;
                    break;
                case "MonkGirl":
                    selectedSpeciesId = 2;
                    break;
                }
                break;
            }
        }
        if (null != map) {
            Debug.Log(String.Format("Extra goAction to be executed with selectedSpeciesId={0}", selectedSpeciesId));
            map.onCharacterSelectGoAction(selectedSpeciesId);
        } else {
            Debug.LogWarning(String.Format("There's no extra goAction to be executed with selectedSpeciesId={0}", selectedSpeciesId));
        }
        toggleUIInteractability(true);
    }

    public void OnBackButtonClicked() {
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }
}
