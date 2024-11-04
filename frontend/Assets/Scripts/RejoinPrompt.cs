using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class RejoinPrompt : MonoBehaviour {
    private bool currentPanelEnabled = false;
    public Image retryButton, exitButton;
    public TMP_Text hint;

    public delegate void RejoinPromptRetryCallback();
    public delegate void RejoinPromptExitCallback();

    RejoinPromptRetryCallback underlyingRetryCallback;
    RejoinPromptExitCallback underlyingExitCallback;
    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void SetCallbacks(RejoinPromptRetryCallback aRetryCallback, RejoinPromptExitCallback aExitCallback) {
        underlyingRetryCallback = aRetryCallback;
        underlyingExitCallback = aExitCallback;
    }

    public void toggleUIInteractability(bool val) {
        currentPanelEnabled = val;
        if (val) {
            retryButton.transform.localScale = Vector3.one;
            exitButton.transform.localScale = Vector3.one;
            hint.text = "Auto rejoin failed, please manually proceed";
        } else {
            retryButton.transform.localScale = Vector3.zero;
            exitButton.transform.localScale = Vector3.zero;
            hint.text = "Rejoining, please wait...";
        }
    }

    public void OnBtnRetry(InputAction.CallbackContext context) {
        Debug.LogFormat("RejoinPrompt.OnBtnRetry", currentPanelEnabled);
        if (!currentPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            OnRetry();
        }
    }

    public void OnBtnExit(InputAction.CallbackContext context) {
        Debug.LogFormat("RejoinPrompt.OnBtnExit", currentPanelEnabled);
        if (!currentPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            OnExit();
        }
    }

    public void OnRetry() {
        if (null != underlyingRetryCallback) {
            underlyingRetryCallback();
        }
    }

    public void OnExit() {
        if (null != underlyingExitCallback) {
            underlyingExitCallback();
        }
    }
}
