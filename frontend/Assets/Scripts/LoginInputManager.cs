using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.

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
        Debug.Log(String.Format("GetCaptchaButton is clicked"));
    }

    public void OnLoginActionButtonClicked() {
        Debug.Log(String.Format("LoginActionButton is clicked"));
    }
}
