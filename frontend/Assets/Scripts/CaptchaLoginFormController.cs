using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using shared;

public class CaptchaLoginFormController : MonoBehaviour {

    // UI bindings are done in the Editor, the field bindings here are used for enabling/disabling in script.
    public TMP_InputField UnameInput;
	public TMP_InputField CaptchaInput;
	public Button GetCaptchaButton;
	public Button LoginActionButton;
    public Button CloseButton;
    private WsSessionManager.OnLoginResult onLoginResultCallback = null;

    public void SetOnLoginResultCallback(WsSessionManager.OnLoginResult newOnLoginResultCallback) {
        onLoginResultCallback = newOnLoginResultCallback;
    }
    public void ClearOnLoginResultCallback() {
        onLoginResultCallback = null;
    }

    public void OnClose() {
        ClearOnLoginResultCallback();
        gameObject.SetActive(false);
    }

    public void toggleUIInteractability(bool enabled) {
        UnameInput.interactable = enabled;
        CaptchaInput.interactable = enabled;
        GetCaptchaButton.interactable = enabled;
        LoginActionButton.interactable = enabled;
        CloseButton.interactable = enabled;
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
                        WsSessionManager.Instance.SetCredentials(authToken, playerId);
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
