using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using UnityEngine.InputSystem.OnScreen;
using DG.Tweening;
using UnityEngine.InputSystem.Controls;

public class BattleInputManager : MonoBehaviour {
    private const float magicLeanLowerBound = 0.1f;
    private const float magicLeanUpperBound = 0.9f;
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

    private Vector2 joystickInitPos;
    private float joystickKeyboardMoveRadius;
    private float joystickMoveEps;
    public GameObject joystick;
    public GameObject btnA;
    public GameObject btnB;
    public GameObject btnC;
    public GameObject btnD;
    private bool customEnabled = true;

    public bool enablePlatformSpecificHiding = false;
    public Image joystickImg;
    public Sprite joystickIdle, joystickLeft, joystickRight, joystickUp, joystickDown, joystickDownLeft, joystickDownRight, joystickUpLeft, joystickUpRight; 

    void Start() {
        joystickInitPos = joystick.transform.position;
        joystickKeyboardMoveRadius = 0.5f*joystick.GetComponent<OnScreenStick>().movementRange;
        joystickMoveEps = 0.1f;
        reset();
    }

    // [WARNING] "continuousDx", "continuousDy" and "eps" are already scaled into [0, 1]
    public static (int, int, int) DiscretizeDirection(float continuousDx, float continuousDy, float eps) {
        int dx = 0, dy = 0, encodedIdx = 0;
        float absContinuousDx = Math.Abs(continuousDx);
        float absContinuousDy = Math.Abs(continuousDy);

        if (absContinuousDx < eps && absContinuousDy < eps) {
            return (dx, dy, encodedIdx);
        }
        float criticalRatio = continuousDy / continuousDx;
        float absCriticalRatio = Math.Abs(criticalRatio);
        float downEps = 5*eps; // dragging down is often more tentative for a player, thus give it a larger threshold!

        if (absCriticalRatio < magicLeanLowerBound && eps < absContinuousDx) {
            dy = 0;
            if (0 < continuousDx) {
                dx = +2; // right 
                encodedIdx = 3;
            } else {
                dx = -2; // left 
                encodedIdx = 4;
            }
        } else if (absCriticalRatio > magicLeanUpperBound && eps < absContinuousDy) {
            dx = 0;
            if (0 < continuousDy) {
                dy = +2; // up
                encodedIdx = 1;
            } else if (downEps < absContinuousDy) {
                dy = -2; // down
                encodedIdx = 2;
            } else {
                // else stays at "encodedIdx == 0" 
            }
        } else if (eps < absContinuousDx && eps < absContinuousDy) {
            if (0 < continuousDx) {
                dx = +1;
                if (0 < continuousDy) {
                    dy = +1;
                    encodedIdx = 5;
                } else {
                    if (downEps < absContinuousDy) {
                        dy = -1;
                        encodedIdx = 7;
                    } else {
                        dx = +2; // right 
                        encodedIdx = 3;
                    }
                } 
            } else {
                // 0 > continuousDx
                dx = -1;
                if (0 < continuousDy) {
                    dy = +1;
                    encodedIdx = 8;
                } else {
                    if (downEps < absContinuousDy) {
                        dy = -1;
                        encodedIdx = 6;
                    } else {
                        dx = -2; // left 
                        encodedIdx = 4;
                    }
                }
            }
        } else {
            // just use encodedIdx = 0
        }

        return (dx, dy, encodedIdx);
    }

    public void OnBtnAInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        // Debug.Log(String.Format("btnALevel is changed to {0}", btnALevel));
        _triggerEdgeBtnA(rising);
    }

    public void OnBtnBInput(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        // Debug.Log(String.Format("btnBLevel is changed to {0}", btnBLevel));
        _triggerEdgeBtnB(rising);
    }

    public void OnBtnCInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        // Debug.Log(String.Format("btnBLevel is changed to {0}", btnBLevel));
        _triggerEdgeBtnC(rising);
    }

    public void OnBtnDInput(InputAction.CallbackContext context) {
        if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        // Debug.Log(String.Format("btnALevel is changed to {0}", btnALevel));
        _triggerEdgeBtnD(rising);
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
        int btnALevel = (cachedBtnALevel << 4);
        int btnBLevel = (cachedBtnBLevel << 5);
        int btnCLevel = (cachedBtnCLevel << 6);
        int btnDLevel = (cachedBtnDLevel << 7);

        cachedBtnALevel = realtimeBtnALevel;
        cachedBtnBLevel = realtimeBtnBLevel;
        cachedBtnCLevel = realtimeBtnCLevel;
        cachedBtnDLevel = realtimeBtnDLevel;

        btnAEdgeTriggerLock = false;
        btnBEdgeTriggerLock = false;
        btnCEdgeTriggerLock = false;
        btnDEdgeTriggerLock = false;

        float continuousDx = joystickX;
        float continuousDy = joystickY;
        var (_, _, discretizedDir) = DiscretizeDirection(continuousDx, continuousDy, joystickMoveEps);
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
        ulong ret = (ulong)(discretizedDir + btnALevel + btnBLevel + btnCLevel + btnDLevel);
        return ret;
    }

	public void enable(bool yesOrNo) {
        customEnabled = yesOrNo;
        reset(); // reset upon any change of this field!
	}
    
    public void reset() {
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
        if (enablePlatformSpecificHiding && !Application.isMobilePlatform) {
            joystick.gameObject.SetActive(false);
            btnA.gameObject.SetActive(false); // if "chConfig.UseInventoryBtnB", it'll be later enabled in MapController
            btnB.gameObject.SetActive(false);
        }
    }

    private void _triggerEdgeBtnA(bool rising) {
        realtimeBtnALevel = (rising ? 1 : 0);
        if (!btnAEdgeTriggerLock && (1 - realtimeBtnALevel) == cachedBtnALevel) {
            cachedBtnALevel = realtimeBtnALevel;
            btnAEdgeTriggerLock = true;
        }

        if (enablePlatformSpecificHiding && !Application.isMobilePlatform) return; // Save some resources on animating
        if (rising) {
            btnA.transform.DOScale(0.3f * Vector3.one, 0.5f);
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
            btnB.transform.DOScale(0.3f * Vector3.one, 0.5f);
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
            btnC.transform.DOScale(0.3f * Vector3.one, 0.5f);
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

    public void resumeScales() {
        btnA.transform.localScale = Vector3.one;
        btnB.transform.localScale = Vector3.one;
        btnC.transform.localScale = Vector3.one;
        btnD.transform.localScale = Vector3.one;
    }
}
