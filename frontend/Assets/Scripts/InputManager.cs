using UnityEngine;
using System;
using shared;
using static shared.Battle;

public class InputManager : MonoBehaviour {
    private const double magicLeanLowerBound = 0.1;
    private const double magicLeanUpperBound = 0.9;
    private const double joyStickEps = 0.1;

    public static (int, int, int) DiscretizeDirection(double continuousDx, double continuousDy, double eps) {
        int dx = 0, dy = 0, encodedIdx = 0;
        if (Math.Abs(continuousDx) < eps && Math.Abs(continuousDy) < eps) {
            return (dx, dy, encodedIdx);
        }

        double criticalRatio = continuousDy / continuousDx;
        if (Math.Abs(criticalRatio) < magicLeanLowerBound) {
            dy = 0;
            if (0 < continuousDx) {
                dx = +2; // right 
                encodedIdx = 3;
            }
            else {
                dx = -2; // left 
                encodedIdx = 4;
            }
        }
        else if (Math.Abs(criticalRatio) > magicLeanUpperBound) {
            dx = 0;
            if (0 < continuousDy) {
                dy = +2; // up
                encodedIdx = 1;
            }
            else {
                dy = -2; // down
                encodedIdx = 2;
            }
        }
        else {
            if (0 < continuousDx) {
                if (0 < continuousDy) {
                    dx = +1;
                    dy = +1;
                    encodedIdx = 5;
                }
                else {
                    dx = +1;
                    dy = -1;
                    encodedIdx = 7;
                }
            }
            else {
                // 0 >= continuousDx
                if (0 < continuousDy) {
                    dx = -1;
                    dy = +1;
                    encodedIdx = 8;
                }
                else {
                    dx = -1;
                    dy = -1;
                    encodedIdx = 6;
                }
            }
        }

        return (dx, dy, encodedIdx);
    }

    public static ulong GetImmediateEncodedInput() {
        float continuousDx = Input.GetAxis("Horizontal");
        float continuousDy = Input.GetAxis("Vertical");
        var (_, _, discretizedDir) = DiscretizeDirection(continuousDx, continuousDy, joyStickEps);
        return (ulong)discretizedDir;
    }
}
