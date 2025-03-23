using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.Controls;
using shared;
using DG.Tweening;

public class BattleInputManager : MonoBehaviour {
    private float joystickX, joystickY;

    private int realtimeBtnALevel = 0;
    private int cachedBtnALevel = 0;
    private bool btnAEdgeTriggerLock = false;

    private int realtimeBtnBLevel = 0;
    private int cachedBtnBLevel = 0;
    private bool btnBEdgeTriggerLock = false;

    private int realtimeBtnCLevel = 0;
    private int cachedBtnCLevel = 0;
    private bool btnCEdgeTriggerLock = false;

    private int realtimeBtnDLevel = 0;
    private int cachedBtnDLevel = 0;
    private bool btnDEdgeTriggerLock = false;

    private int realtimeBtnELevel = 0;
    private int cachedBtnELevel = 0;
    private bool btnEEdgeTriggerLock = false;

    private Vector2 joystickInitPos;
    private float joystickKeyboardMoveRadius;
    private float joystickMoveEps;
    public GameObject joystick;
    public GameObject btnA;
    public GameObject btnB;
    public GameObject btnC;
    public GameObject btnD;
    public GameObject btnE;
    private bool customEnabled = true;

    public bool enablePlatformSpecificHiding = false;
    public Image joystickImg;
    public Sprite joystickIdle, joystickLeft, joystickRight, joystickUp, joystickDown, joystickDownLeft, joystickDownRight, joystickUpLeft, joystickUpRight;

    public AbstractMapController map;

    void Start() {
        joystickInitPos = joystick.transform.position;
        joystickKeyboardMoveRadius = 0.5f*joystick.GetComponent<OnScreenStick>().movementRange;
        joystickMoveEps = 0.1f;
        ResetSelf();
    }


    public void OnBtnAInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        _triggerEdgeBtnA(rising);
    }

    public void OnBtnBInput(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        _triggerEdgeBtnB(rising);
    }

    public void OnBtnCInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        _triggerEdgeBtnC(rising);
    }

    public void OnBtnDInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        _triggerEdgeBtnD(rising);
    }

    public void OnBtnEInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        _triggerEdgeBtnE(rising);
        //Debug.LogFormat("btnELevel is changed to {0}", realtimeBtnELevel);
    }

    public void onBtnCancelInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        if (rising) {
            attemptToCancelBattle();
        }
    }

    public void attemptToCancelBattle() {
        if (null != map) {
            map.OnSettingsClicked();
        }
    }

    public void OnMove(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        joystickX = context.ReadValue<Vector2>().normalized.x;
        joystickY = context.ReadValue<Vector2>().normalized.y;
        //Debug.Log(String.Format("(joystickX,joystickY) is changed to ({0},{1}) by touch", joystickX, joystickY));
    }

    public void OnMoveByKeyboard(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        switch (((KeyControl)context.control).keyCode) {
            case Key.W:
            case Key.S:
                joystickY = Math.Sign(context.ReadValue<Vector2>().normalized.y);
                break;
            case Key.D:
            case Key.A:
                joystickX = 2*Math.Sign(context.ReadValue<Vector2>().normalized.x);
                break;
        }
        //Debug.Log(String.Format("(joystickX,joystickY) is changed to ({0},{1}) by keyboard", joystickX, joystickY));

        joystick.transform.position = new Vector3(
            joystickInitPos.x + joystickKeyboardMoveRadius * joystickX,
            joystickInitPos.y + joystickKeyboardMoveRadius * joystickY,
            joystick.transform.position.z
        );
    }

    public ulong GetEncodedInput() {
        int btnALevel = cachedBtnALevel;
        int btnBLevel = cachedBtnBLevel;
        int btnCLevel = cachedBtnCLevel;
        int btnDLevel = cachedBtnDLevel;
        int btnELevel = cachedBtnELevel;

        cachedBtnALevel = realtimeBtnALevel;
        cachedBtnBLevel = realtimeBtnBLevel;
        cachedBtnCLevel = realtimeBtnCLevel;
        cachedBtnDLevel = realtimeBtnDLevel;
        cachedBtnELevel = realtimeBtnELevel;

        btnAEdgeTriggerLock = false;
        btnBEdgeTriggerLock = false;
        btnCEdgeTriggerLock = false;
        btnDEdgeTriggerLock = false;
        btnEEdgeTriggerLock = false;

        float continuousDx = joystickX;
        float continuousDy = joystickY;
        var (dx, dy, discretizedDir) = Battle.DiscretizeDirection(continuousDx, continuousDy, joystickMoveEps);
        // "GetEncodedInput" gets called by "AbstractMapController.doUpdate()", thus a proper spot to update UI
        // TODO: Add sprites on skewed directions.
        switch (discretizedDir) {
        case 1:
            joystickImg.sprite = joystickUp;
            break;
        case 2:
            joystickImg.sprite = joystickDown;
            break;
        case 3:
            joystickImg.sprite = joystickRight;
            break;
        case 4:
            joystickImg.sprite = joystickLeft;
            break;
        case 5:
            joystickImg.sprite = joystickUpRight;
            break;
        case 6:
            joystickImg.sprite = joystickDownLeft;
            break;
        case 7:
            joystickImg.sprite = joystickDownRight;
            break;
        case 8:
            joystickImg.sprite = joystickUpLeft;
            break;
        default:
            joystickImg.sprite = joystickIdle;
            break;
        }
        ulong ret = Battle.EncodeInput(dx, dy, btnALevel, btnBLevel, btnCLevel, btnDLevel, btnELevel);
        return ret;
    }

	public void enable(bool yesOrNo) {
        customEnabled = yesOrNo;
        ResetSelf(); // reset upon any change of this field!
	}
    
    public void ResetSelf() {
        joystickX = 0;
        joystickY = 0;
        joystickImg.sprite = joystickIdle;

        realtimeBtnALevel = 0;
        cachedBtnALevel = 0;
        btnAEdgeTriggerLock = false;

        realtimeBtnBLevel = 0;
        cachedBtnBLevel = 0;
        btnBEdgeTriggerLock = false;

        realtimeBtnCLevel = 0;
        cachedBtnCLevel = 0;
        btnCEdgeTriggerLock = false;

        realtimeBtnDLevel = 0;
        cachedBtnDLevel = 0;
        btnDEdgeTriggerLock = false;

        realtimeBtnELevel = 0;
        cachedBtnELevel = 0;
        btnEEdgeTriggerLock = false;

        if (enablePlatformSpecificHiding && !Application.isMobilePlatform) {
            joystick.gameObject.SetActive(false);
            btnA.gameObject.SetActive(false); // if "chConfig.UseInventoryBtnB", it'll be later enabled in MapController
            btnB.gameObject.SetActive(false);
            btnE.gameObject.SetActive(false);
        }
        btnC.gameObject.SetActive(false);
        btnD.gameObject.SetActive(false);
    }

    private void _triggerEdgeBtnA(bool rising) {
        realtimeBtnALevel = (rising ? 1 : 0);
        if (!btnAEdgeTriggerLock && (1 - realtimeBtnALevel) == cachedBtnALevel) {
            cachedBtnALevel = realtimeBtnALevel;
            btnAEdgeTriggerLock = true;
        }

        if (enablePlatformSpecificHiding && !Application.isMobilePlatform) return; // Save some resources on animating
        if (rising) {
            btnA.transform.DOScale(0.3f * Vector3.one, 0.2f);
        } else {
            btnA.transform.DOScale(1.0f * Vector3.one, 0.8f);
        }
    }

    private void _triggerEdgeBtnB(bool rising) {
        realtimeBtnBLevel = (rising ? 1 : 0);
        if (!btnBEdgeTriggerLock && (1 - realtimeBtnBLevel) == cachedBtnBLevel) {
            cachedBtnBLevel = realtimeBtnBLevel;
            btnBEdgeTriggerLock = true;
        }

        if (enablePlatformSpecificHiding && !Application.isMobilePlatform) return; // Save some resources on animating
        if (rising) {
            btnB.transform.DOScale(0.3f * Vector3.one, 0.2f);
        } else {
            btnB.transform.DOScale(1.0f * Vector3.one, 0.8f);
        }
    }

    private void _triggerEdgeBtnC(bool rising) {
        realtimeBtnCLevel = (rising ? 1 : 0);
        if (!btnCEdgeTriggerLock && (1 - realtimeBtnCLevel) == cachedBtnCLevel) {
            cachedBtnCLevel = realtimeBtnCLevel;
            btnCEdgeTriggerLock = true;
        }

        if (rising) {
            btnC.transform.DOScale(0.3f * Vector3.one, 0.2f);
        } else {
            btnC.transform.DOScale(1.0f * Vector3.one, 0.8f);
        }
    }

    private void _triggerEdgeBtnD(bool rising) {
        realtimeBtnDLevel = (rising ? 1 : 0);
        if (!btnDEdgeTriggerLock && (1 - realtimeBtnDLevel) == cachedBtnDLevel) {
            cachedBtnDLevel = realtimeBtnDLevel;
            btnDEdgeTriggerLock = true;
        }

        if (rising) {
            btnD.transform.DOScale(0.3f * Vector3.one, 0.5f);
        } else {
            btnD.transform.DOScale(1.0f * Vector3.one, 0.8f);
        }
    }

    private void _triggerEdgeBtnE(bool rising) {
        realtimeBtnELevel = (rising ? 1 : 0);
        if (!btnEEdgeTriggerLock && (1 - realtimeBtnELevel) == cachedBtnELevel) {
            cachedBtnELevel = realtimeBtnELevel;
            btnEEdgeTriggerLock = true;
        }

        if (rising) {
            btnE.transform.DOScale(0.3f * Vector3.one, 0.5f);
        } else {
            btnE.transform.DOScale(1.0f * Vector3.one, 0.8f);
        }
    }

    public void resumeScales() {
        btnA.transform.localScale = Vector3.one;
        btnB.transform.localScale = Vector3.one;
        btnC.transform.localScale = Vector3.one;
        btnD.transform.localScale = Vector3.one;
        btnE.transform.localScale = Vector3.one;
    }
}
