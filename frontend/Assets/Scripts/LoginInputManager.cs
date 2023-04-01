using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using UnityEngine.Networking;
using System.Collections;
using Newtonsoft.Json;

public class LoginInputManager : MonoBehaviour {

    // UI bindings are done in the Editor, the field bindings here are used for enabling/disabling in script.
    public TMP_InputField UnameInput;
	public TMP_InputField CaptchaInput;
	public Button GetCaptchaButton;
	public Button LoginActionButton;

    string unameInputValue;
    string captchaInputValue;

    // Start is called before the first frame update
    void Start() {
        
    }

    // Update is called once per frame
    void Update() {

    }

    public void OnUnameInputChanged(string val) {
        Debug.Log(String.Format("Uname is changed to {0}", val));
        unameInputValue = val;
    }

    public void OnCaptchaInputChanged(string val) {
        Debug.Log(String.Format("Captcha is changed to {0}", val));
        captchaInputValue = val;
    }

    public void OnGetCaptchaButtonClicked() {
        string httpHost = Env.Instance.getHttpHost();
        Debug.Log(String.Format("GetCaptchaButton is clicked, httpHost={0}", httpHost));

        StartCoroutine(doRequestGetCapture(httpHost));
    }

    IEnumerator doRequestGetCapture(string httpHost) {
        string uri = httpHost + String.Format("/Auth/SmsCaptcha/Get?uname={0}", unameInputValue);
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
                    Debug.Log("Received: " + webRequest.downloadHandler.text);
                    break;
            }
        }
    }

    public void OnLoginActionButtonClicked() {
        Debug.Log(String.Format("LoginActionButton is clicked"));
    }
}
