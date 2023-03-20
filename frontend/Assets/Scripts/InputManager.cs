using UnityEngine;
using UnityEngine.InputSystem;
using System;
using shared;
using static shared.Battle;

public class InputManager : MonoBehaviour {
    private const float magicLeanLowerBound = 0.1f;
    private const float magicLeanUpperBound = 0.9f;
    private const float joyStickEps = 0.1f;
    private float joystickX, joystickY;
    private int btnALevel;

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

    public void onBtnA() {
    }

    public void ReadMoveInput(InputAction.CallbackContext context) {
        joystickX = context.ReadValue<Vector2>().normalized.x;
        joystickY = context.ReadValue<Vector2>().normalized.y;
    }

    public ulong GetImmediateEncodedInput() {
        float continuousDx = joystickX;
        float continuousDy = joystickY;
        var (_, _, discretizedDir) = DiscretizeDirection(continuousDx, continuousDy, joyStickEps);
        return (ulong)discretizedDir;
    }
}
