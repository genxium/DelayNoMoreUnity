using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using shared;

public class LoginInputManager : MonoBehaviour {

    // UI bindings are done in the Editor, the field bindings here are used for enabling/disabling in script.
    public TMP_InputField UnameInput;
	public TMP_InputField CaptchaInput;
	public Button GetCaptchaButton;
	public Button LoginActionButton;

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {

    }

    public void OnGetCaptchaButtonClicked() {
        string httpHost = Env.Instance.getHttpHost();
        Debug.Log(String.Format("GetCaptchaButton is clicked, httpHost={0}", httpHost));

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
        }
    }

    public void OnLoginActionButtonClicked() {
        string httpHost = Env.Instance.getHttpHost();
        Debug.Log(String.Format("LoginActionButton is clicked, httpHost={0}", httpHost));

        StartCoroutine(doLoginAction(httpHost));
    }

    IEnumerator doLoginAction(string httpHost) {
        string uri = httpHost + String.Format("/Auth/SmsCaptcha/Login");
        WWWForm form = new WWWForm();
        form.AddField("uname", UnameInput.text); 
        form.AddField("captcha", CaptchaInput.text); 
        using (UnityWebRequest webRequest = UnityWebRequest.Post(uri, form)) {
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
                    if (ErrCode.Ok == res["retCode"].Value<int>()) {
                        // TODO: Jump to OnlineMap with "authToken" and "playerId"
                        var authToken = res["newAuthToken"].Value<string>();
                        var playerId = res["playerId"].Value<int>();
                        Debug.Log(String.Format("newAuthToken: {0}, playerId: {1}", authToken, playerId));
                    }
                    break;
            }
        }
    }
}
