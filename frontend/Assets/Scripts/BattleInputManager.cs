using UnityEngine;
using UnityEngine.InputSystem;
using System;
using UnityEngine.InputSystem.OnScreen;
using DG.Tweening;

public class BattleInputManager : MonoBehaviour {
    private const float magicLeanLowerBound = 0.1f;
    private const float magicLeanUpperBound = 0.9f;
    private const float joyStickEps = 0.1f;
    private float joystickX, joystickY;

    private int realtimeBtnALevel = 0;
    private int cachedBtnALevel = 0;
    private bool btnAEdgeTriggerLock = false;
    private int realtimeBtnBLevel = 0;
    private int cachedBtnBLevel = 0;
    private bool btnBEdgeTriggerLock = false;

    private Vector2 joystickInitPos;
    private float joystickKeyboardMoveRadius;
    public GameObject joystick;
    public GameObject btnA;
    public GameObject btnB;
	private bool customEnabled = true;

    void Start() {
        joystickInitPos = joystick.transform.position;
        joystickKeyboardMoveRadius = 0.5f*joystick.GetComponent<OnScreenStick>().movementRange;
    }

    public static (int, int, int) DiscretizeDirection(float continuousDx, float continuousDy, float eps) {
        int dx = 0, dy = 0, encodedIdx = 0;
        if (Math.Abs(continuousDx) < eps && Math.Abs(continuousDy) < eps) {
            return (dx, dy, encodedIdx);
        }

        float criticalRatio = continuousDy / continuousDx;
        if (Math.Abs(criticalRatio) < magicLeanLowerBound) {
            dy = 0;
            if (0 < continuousDx) {
                dx = +2; // right 
                encodedIdx = 3;
            } else {
                dx = -2; // left 
                encodedIdx = 4;
            }
        } else if (Math.Abs(criticalRatio) > magicLeanUpperBound) {
            dx = 0;
            if (0 < continuousDy) {
                dy = +2; // up
                encodedIdx = 1;
            } else {
                dy = -2; // down
                encodedIdx = 2;
            }
        } else {
            if (0 < continuousDx) {
                if (0 < continuousDy) {
                    dx = +1;
                    dy = +1;
                    encodedIdx = 5;
                } else {
                    dx = +1;
                    dy = -1;
                    encodedIdx = 7;
                }
            } else {
                // 0 >= continuousDx
                if (0 < continuousDy) {
                    dx = -1;
                    dy = +1;
                    encodedIdx = 8;
                } else {
                    dx = -1;
                    dy = -1;
                    encodedIdx = 6;
                }
            }
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
        var (_, _, discretizedDir) = DiscretizeDirection(continuousDx, continuousDy, joyStickEps);
        ulong ret = (ulong)(discretizedDir + btnALevel + btnBLevel);
        return ret;
    }

	public void enable(bool yesOrNo) {
        customEnabled = yesOrNo;
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
            btnA.transform.DOScale(0.5f * Vector3.one, 0.8f);
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
            btnB.transform.DOScale(0.5f * Vector3.one, 0.8f);
        }
    }
}
