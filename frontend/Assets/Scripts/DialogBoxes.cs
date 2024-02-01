using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogBoxes : MonoBehaviour {
    public Image avatarL, avatarR;
    public TMP_Text textL, textR;
    public Button nextBtn; // to toggle interactability

    // Start is called before the first frame update
    void Start() {

    }

    // Update is called once per frame
    void Update() {

    }

    public virtual void OnNextBtnClicked() {
        Debug.Log(String.Format("Next button clicked"));
    }
}
