In the nonpublic version, an extra _render-only(i.e. no change to colliders) position interpolation_ trick is used for smoother graphical experience during bad network.

```c#
public void applyRoomDownsyncFrameDynamics(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf) {
    // ...
    for (int k = 0; k < roomCapacity; k++) {
        //...
        setCharacterGameObjectPosByInterpolation(prevCharacterDownsync, currCharacterDownsync, chConfig, playerGameObj, wx, wy);

        playerGameObj.transform.position = newPosHolder; // [WARNING] Even if not selfPlayer, we have to set position of the other players regardless of new positions being visible within camera or not, otherwise outdated other players' node might be rendered within camera! 
        //...
    }
    // ...
}

protected void setCharacterGameObjectPosByInterpolation(CharacterDownsync? prevCharacterDownsync, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, GameObject chGameObj, float newWx, float newWy) {
    if (null != prevCharacterDownsync && CharacterState.Dying == prevCharacterDownsync.CharacterState) {
        // Revived from Dying state.
        newPosHolder.Set(newWx, newWy, chGameObj.transform.position.z);
        return;
    }

    float dWx = (newWx-chGameObj.transform.position.x);
    float dWy = (newWy-chGameObj.transform.position.y);
    float dis2 = dWx*dWx + dWy*dWy;
    var (velXWorld, velYWorld) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VelX, currCharacterDownsync.VelY); // Just roughly, using "currCharacterDownsync" wouldn't cause NullPointerException thus more effective, and "CharacterDownsync.VelX & VelY" is already normalized to "per frame distance"
    var speedReachable2 = (velXWorld*velXWorld + velYWorld*velYWorld);
    var (chConfigSpeedReachable, _) = VirtualGridToPolygonColliderCtr(chConfig.Speed, 0);
    float defaultSpeedReachable2 = (chConfigSpeedReachable * chConfigSpeedReachable);
    float tolerance2 = 0.01f*Math.Max(speedReachable2, defaultSpeedReachable2);

    if (dis2 <= tolerance2) {
        newPosHolder.Set(newWx, newWy, chGameObj.transform.position.z);
    } else {
        // dis2 > tolerance2 >= 0
        float invMag = InvSqrt32(dis2);
        float ratio = 0;
        if (0 < speedReachable2) {
            float speedReachable = speedReachable2 * InvSqrt32(speedReachable2);
            ratio = speedReachable*invMag; 
        } else {
            ratio = chConfigSpeedReachable*invMag; 
        }
        if (ratio > 1.0f) ratio = 1.0f;
        float interpolatedWx = chGameObj.transform.position.x + ratio * dWx;
        float interpolatedWy = chGameObj.transform.position.y + ratio * dWy;
        newPosHolder.Set(interpolatedWx, interpolatedWy, chGameObj.transform.position.z);
    }
}
```
