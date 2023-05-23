using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ErrStackLogPanel : MonoBehaviour {
    public TMP_Text content;

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void OnBackToLoginButtonClicked() {
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }
}
