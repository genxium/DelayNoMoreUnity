using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Toast : MonoBehaviour {
    public Image advice;
    public TMP_Text adviceText;
    private bool intendToHideAdvice = true;
    private static float secondsInCycle = 0f, upwardCycleSeconds = 0.5f, keepCycleSeconds = 2.5f, downwardCycleSeconds = 2.5f;
    private static float keepCycleSecondsPrefix = upwardCycleSeconds+keepCycleSeconds, downwardCycleSecondsPrefix = upwardCycleSeconds+keepCycleSeconds+downwardCycleSeconds;
    private static float upwardCycleSecondsInv = 1f / upwardCycleSeconds, downwardCycleSecondsInv = 1f / downwardCycleSeconds;
    public void hideAdvice() {
        intendToHideAdvice = true;
        advice.color = adviceZeroColor;
        this.gameObject.SetActive(false);
    }

    private Color adviceOrigColor = Color.white;
    private Color adviceTextOrigColor = Color.white;

    private Color adviceZeroColor = Color.clear;
    private Color adviceTextZeroColor = Color.clear;

    public void Start() {
        if (null != advice) {
            adviceOrigColor = advice.color;
            adviceZeroColor = new Color(advice.color.r, advice.color.g, advice.color.b, 0f);
            advice.color = adviceZeroColor;
        }
        if (null != adviceText) {
            adviceTextOrigColor = adviceText.color;
            adviceTextZeroColor = new Color(adviceText.color.r, adviceText.color.g, adviceText.color.b, 0f);
            adviceText.color = adviceTextZeroColor;
        }
    }

    public void Update() {
        if (intendToHideAdvice) {
            return;
        }
        float dt = Time.deltaTime;
        secondsInCycle += dt;
        if (secondsInCycle <= upwardCycleSeconds) {
            float ratio = secondsInCycle * upwardCycleSecondsInv;
            advice.color = Color.Lerp(adviceZeroColor, adviceOrigColor, ratio);
            adviceText.color = Color.Lerp(adviceTextZeroColor, adviceTextOrigColor, ratio);
        } else if (keepCycleSecondsPrefix < secondsInCycle && secondsInCycle < downwardCycleSecondsPrefix) {
            float ratio = (secondsInCycle - keepCycleSecondsPrefix) * downwardCycleSecondsInv;
            advice.color = Color.Lerp(adviceOrigColor, adviceZeroColor, ratio);
            adviceText.color = Color.Lerp(adviceTextOrigColor, adviceTextZeroColor, ratio);
        } else if (secondsInCycle > downwardCycleSecondsPrefix) {
            advice.color = adviceZeroColor;
            adviceText.color = adviceTextZeroColor;
            hideAdvice();
        }
    }

    public void showAdvice(string text) {
        if (null == text) return;
        if (null == advice) return;
        if (null == adviceText) return;
        if (false == intendToHideAdvice) {
            return;
        }
        Debug.Log("showAdvice: " + text);
        intendToHideAdvice = false;
        adviceText.text = text;
        secondsInCycle = 0f;
        this.gameObject.SetActive(true);
    }
}
