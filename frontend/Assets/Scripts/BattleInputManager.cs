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
    private int btnALevel;

    private Vector2 joystickInitPos;
    private float joystickKeyboardMoveRadius;
    public GameObject joystick;
    public GameObject btnA;
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

    public void OnBtnAInput(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        btnALevel = context.ReadValueAsButton() ? 1 : 0;
        Debug.Log(String.Format("btnALevel is changed to {0}", btnALevel));

        if (1 == btnALevel) {
            btnA.transform.DOScale(0.3f * Vector3.one, 0.5f);
        } else {
            btnA.transform.DOScale(1.5f * Vector3.one, 0.8f);
        }
    }

    public void OnMove(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        joystickX = context.ReadValue<Vector2>().normalized.x;
        joystickY = context.ReadValue<Vector2>().normalized.y;
        Debug.Log(String.Format("(joystickX,joystickY) is changed to ({0},{1}) by touch", joystickX, joystickY));
    }

    public void OnMoveByKeyboard(InputAction.CallbackContext context) {
		if (!customEnabled) return;
        joystickX = context.ReadValue<Vector2>().normalized.x;
        joystickY = context.ReadValue<Vector2>().normalized.y;
        Debug.Log(String.Format("(joystickX,joystickY) is changed to ({0},{1}) by keyboard", joystickX, joystickY));

        joystick.transform.position = new Vector3(
            joystickInitPos.x + joystickKeyboardMoveRadius * joystickX,
            joystickInitPos.y + joystickKeyboardMoveRadius * joystickY,
            joystick.transform.position.z
        );
    }

    public ulong GetImmediateEncodedInput() {
        float continuousDx = joystickX;
        float continuousDy = joystickY;
        var (_, _, discretizedDir) = DiscretizeDirection(continuousDx, continuousDy, joyStickEps);
        ulong ret = (ulong)(discretizedDir + (btnALevel << 4));
        return ret;
    }

	public void enable(bool yesOrNo) {
        customEnabled = yesOrNo;
	}
}
