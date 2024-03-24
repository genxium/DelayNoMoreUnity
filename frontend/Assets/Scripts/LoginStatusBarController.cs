using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginStatusBarController : MonoBehaviour {
    public Sprite loggedInSpr, notLoggedInSpr;
    public TMP_Text uname;
    public Image loggedInIcon;
    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public void SetLoggedInData(string aUname) {
        loggedInIcon.sprite = loggedInSpr;
        uname.text = aUname;
    }

    public void ClearLoggedInData() {
        loggedInIcon.sprite = notLoggedInSpr;
        uname.text = "Offline";
    }
}
