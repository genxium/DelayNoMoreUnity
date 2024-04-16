using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.Networking;
using UnityEngine.InputSystem;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using shared;

public class CaptchaLoginFormController : MonoBehaviour {
    private bool currentPanelEnabled = true;

    // UI bindings are done in the Editor, the field bindings here are used for enabling/disabling in script.
    public TMP_InputField UnameInput;
	public TMP_InputField CaptchaInput;
    
    // TODO: Find a way to implement free-text input and focus-change all by keyboard operations 
	public Button GetCaptchaButton; 
	public Button LoginActionButton;
    private WsSessionManager.OnLoginResult onLoginResultCallback = null;

    public Image CancelButton;

    public void SetOnLoginResultCallback(WsSessionManager.OnLoginResult newOnLoginResultCallback) {
        onLoginResultCallback = newOnLoginResultCallback;
    }

    public void ClearOnLoginResultCallback() {
        onLoginResultCallback = null;
    }

    public void OnBtnCancel(InputAction.CallbackContext context) {
        if (!currentPanelEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising && InputActionPhase.Performed == context.phase) {
            toggleUIInteractability(false);
            OnCancel();
        }
    }

    public void OnCancel() {
        ClearOnLoginResultCallback();
        gameObject.SetActive(false);
        toggleUIInteractability(true);
    }

    public void toggleUIInteractability(bool enabled) {
        currentPanelEnabled = enabled;

        UnameInput.interactable = enabled;
        CaptchaInput.interactable = enabled;
        GetCaptchaButton.interactable = enabled;
        LoginActionButton.interactable = enabled;
        if (enabled) {
            CancelButton.gameObject.transform.localScale = Vector3.one;
        } else {    
            CancelButton.gameObject.transform.localScale = Vector3.zero;
        }
    }

    public void OnGetCaptchaButtonClicked() {
        string httpHost = Env.Instance.getHttpHost();
        Debug.Log(String.Format("GetCaptchaButton is clicked, httpHost={0}", httpHost));
        toggleUIInteractability(false);
        StartCoroutine(doRequestGetCapture(httpHost));
    }

    IEnumerator doRequestGetCapture(string httpHost) {
        string uri = httpHost + String.Format("/Auth/SmsCaptcha/Get?uname={0}", UnameInput.text);
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri)) {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result) {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError("Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError("HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    var res = JsonConvert.DeserializeObject<JObject>(webRequest.downloadHandler.text);
                    Debug.Log(String.Format("Received: {0}", res));
                    if (ErrCode.IsTestAcc == res["retCode"].Value<int>()) {
                        CaptchaInput.text = res["captcha"].Value<string>();
                    }
                    break;
            }
            toggleUIInteractability(true);
        }
    }

    public void OnLoginActionButtonClicked() {
        string httpHost = Env.Instance.getHttpHost();
        Debug.Log(String.Format("LoginActionButton is clicked, httpHost={0}", httpHost));
        toggleUIInteractability(false);
        StartCoroutine(doSmsCaptchaLoginAction(httpHost));
    }

    IEnumerator doSmsCaptchaLoginAction(string httpHost) {
        string uri = httpHost + String.Format("/Auth/SmsCaptcha/Login");
        WWWForm form = new WWWForm();
        string uname = UnameInput.text; // must remain const after http resp
        form.AddField("uname", uname); 
        form.AddField("captcha", CaptchaInput.text); 
        using (UnityWebRequest webRequest = UnityWebRequest.Post(uri, form)) {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            switch (webRequest.result) {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError("Error: " + webRequest.error);
                    toggleUIInteractability(true);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError("HTTP Error: " + webRequest.error);
                    toggleUIInteractability(true);
                    break;
                case UnityWebRequest.Result.Success:
                    var res = JsonConvert.DeserializeObject<JObject>(webRequest.downloadHandler.text);
                    int retCode = res["retCode"].Value<int>();
                    Debug.Log(String.Format("Received: {0}", res));
                    if (ErrCode.Ok == retCode) {
                        var authToken = res["newAuthToken"].Value<string>();
                        var playerId = res["playerId"].Value<int>();
                        Debug.Log(String.Format("newAuthToken: {0}, playerId: {1}", authToken, playerId));
                        WsSessionManager.Instance.SetCredentials(uname, authToken, playerId);
                        if (null != onLoginResultCallback) {
                            onLoginResultCallback(ErrCode.Ok, uname, playerId, authToken);
                        }
                    } else {
                        toggleUIInteractability(true);
                    }
                    break;
            }
        }
    }
}
