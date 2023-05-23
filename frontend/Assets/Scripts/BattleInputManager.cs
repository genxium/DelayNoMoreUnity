using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.InputSystem.OnScreen;
using DG.Tweening;

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

    private Vector2 joystickInitPos;
    private float joystickKeyboardMoveRadius;
    private float joystickMoveEps;
    public GameObject joystick;
    public GameObject btnA;
    public GameObject btnB;
	private bool customEnabled = true;

    void Start() {
        joystickInitPos = joystick.transform.position;
        joystickKeyboardMoveRadius = 0.5f*joystick.GetComponent<OnScreenStick>().movementRange;
        joystickMoveEps = 0.1f;
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
        float downEps = 2*eps; // dragging down is often more tentative for a player, thus give it a larger threshold!

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

    public void OnBtnBInput(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        // Debug.Log(String.Format("btnBLevel is changed to {0}", btnBLevel));
        _triggerEdgeBtnB(rising);
    }

    public void OnBtnAInput(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        bool rising = context.ReadValueAsButton();
        // Debug.Log(String.Format("btnALevel is changed to {0}", btnALevel));
        _triggerEdgeBtnA(rising);
    }

    public void OnMove(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        joystickX = context.ReadValue<Vector2>().normalized.x;
        joystickY = context.ReadValue<Vector2>().normalized.y;
        //Debug.Log(String.Format("(joystickX,joystickY) is changed to ({0},{1}) by touch", joystickX, joystickY));
    }

    public void OnMoveByKeyboard(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        joystickX = context.ReadValue<Vector2>().normalized.x;
        joystickY = context.ReadValue<Vector2>().normalized.y;
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

        cachedBtnALevel = realtimeBtnALevel;
        cachedBtnBLevel = realtimeBtnBLevel;

        btnAEdgeTriggerLock = false;
        btnBEdgeTriggerLock = false;

        float continuousDx = joystickX;
        float continuousDy = joystickY;
        var (_, _, discretizedDir) = DiscretizeDirection(continuousDx, continuousDy, joystickMoveEps);
        ulong ret = (ulong)(discretizedDir + btnALevel + btnBLevel);
        return ret;
    }

	public void enable(bool yesOrNo) {
        customEnabled = yesOrNo;
        reset(); // reset upon any change of this field!
	}
    
    public void reset() {
        joystickX = 0;
        joystickY = 0;
        realtimeBtnALevel = 0;
        cachedBtnALevel = 0;
        btnAEdgeTriggerLock = false;
        realtimeBtnBLevel = 0;
        cachedBtnBLevel = 0;
        btnBEdgeTriggerLock = false;
    }

    private void _triggerEdgeBtnA(bool rising) {
        realtimeBtnALevel = (rising ? 1 : 0);
        if (!btnAEdgeTriggerLock && (1 - realtimeBtnALevel) == cachedBtnALevel) {
            cachedBtnALevel = realtimeBtnALevel;
            btnAEdgeTriggerLock = true;
        }

        if (rising) {
            btnA.transform.DOScale(0.3f * Vector3.one, 0.5f);
        } else {
            btnA.transform.DOScale(1.5f * Vector3.one, 0.8f);
        }
    }

    private void _triggerEdgeBtnB(bool rising) {
        realtimeBtnBLevel = (rising ? 1 : 0);
        if (!btnBEdgeTriggerLock && (1 - realtimeBtnBLevel) == cachedBtnBLevel) {
            cachedBtnBLevel = realtimeBtnBLevel;
            btnBEdgeTriggerLock = true;
        }

        if (rising) {
            btnB.transform.DOScale(0.3f * Vector3.one, 0.5f);
        } else {
            btnB.transform.DOScale(1.5f * Vector3.one, 0.8f);
        }
    }
}
