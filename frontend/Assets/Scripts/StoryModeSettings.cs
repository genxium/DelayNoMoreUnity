using UnityEngine;

public class StoryModeSettings : MonoBehaviour {

    public delegate void SimpleDelegate();

    SimpleDelegate onExitCallback = null, onCloseCallback = null;

    public void SetCallbacks(SimpleDelegate theExitCallback, SimpleDelegate theCloseCallback) {
        onExitCallback = theExitCallback;
        onCloseCallback = theCloseCallback;
    }

    public void OnExit() {
        gameObject.SetActive(false);
        onExitCallback();
    }

    public void OnClose() {
        gameObject.SetActive(false);
        onCloseCallback();
    }

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }
}
