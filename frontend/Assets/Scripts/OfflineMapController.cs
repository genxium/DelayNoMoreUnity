using UnityEngine;
using System;
using System.Collections;
using shared;
using static shared.Battle;

public class OfflineMapController : AbstractMapController {

    protected override void sendInputFrameUpsyncBatch(int noDelayInputFrameId) {
        throw new NotImplementedException();
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        return false;
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();
        characterSelectPanel.gameObject.SetActive(true);
    }

    public override void onCharacterSelectGoAction(int speciesId) {
        Debug.Log(String.Format("Executing extra goAction with selectedSpeciesId={0}", speciesId));
        selfPlayerInfo = new CharacterDownsync();

        roomCapacity = 1;
        preallocateHolders();
        resetCurrentMatch("Dungeon");
        selfPlayerInfo.JoinIndex = 1;

        battleDurationFrames = 60 * 60;

        // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START"
        int[] speciesIdList = new int[roomCapacity];
        speciesIdList[selfPlayerInfo.JoinIndex - 1] = speciesId;
        var startRdf = mockStartRdf(speciesIdList);
        applyRoomDownsyncFrameDynamics(startRdf, null);

        var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
        Debug.Log(String.Format("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
        Camera.main.transform.position = new Vector3(playerGameObj.transform.position.x, playerGameObj.transform.position.y, Camera.main.transform.position.z);
        characterSelectPanel.gameObject.SetActive(false);
        readyGoPanel.playReadyAnim();

        StartCoroutine(delayToStartBattle(startRdf));
    }

    private IEnumerator delayToStartBattle(RoomDownsyncFrame startRdf) {
        yield return new WaitForSeconds(1);
        readyGoPanel.playGoAnim();
        // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START"
        startRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
        onRoomDownsyncFrame(startRdf, null);
    }

    // Start is called before the first frame update
    void Start() {
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.targetFrameRate = 60;
    }

    // Update is called once per frame
    void Update() {
        try {
            doUpdate();
            if (renderFrameId >= battleDurationFrames) {
                onBattleStopped();
            } else {
                readyGoPanel.setCountdown(renderFrameId, battleDurationFrames);
            }
            //throw new NotImplementedException("Intended");
        } catch (Exception ex) {
            var msg = String.Format("Error during OfflineMap.Update {0}", ex);
            popupErrStackPanel(msg);
            onBattleStopped();
        }
    }

    public void OnBackButtonClicked() {
        Debug.Log("OnBackButtonClicked");
        characterSelectPanel.gameObject.SetActive(true);
    }

    void OnRenderObject() {
        if (debugDrawingEnabled) {
            return;
        }
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        // The magic name "OnRenderObject" is the only callback I found working to draw the debug boundaries.
        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        try {
            // Set transformation matrix for drawing to
            // match our transform
            GL.MultMatrix(transform.localToWorldMatrix);

            // The anchoring quad for testing
            /*
               GL.Begin(GL.QUADS);
               GL.Vertex3(1024, -512, 0);
               GL.Vertex3(1040, -512, 0);
               GL.Vertex3(1040, -496, 0);
               GL.Vertex3(1024, -496, 0);
               GL.End();
             */
            // Draw static colliders
            foreach (var collider in staticRectangleColliders) {
                if (null == collider) {
                    break;
                }
                if (null == collider.Shape) {
                    throw new ArgumentNullException("barrierCollider.Shape is null when drawing staticRectangleColliders");
                }
                if (null == collider.Shape.Points) {
                    throw new ArgumentNullException("barrierCollider.Shape.Points is null when drawing staticRectangleColliders");
                }
                GL.Begin(GL.LINES);
                for (int i = 0; i < 4; i++) {
                    int j = i + 1;
                    if (j >= 4) j -= 4;
                    var (_, pi) = collider.Shape.Points.GetByOffset(i);
                    var (_, pj) = collider.Shape.Points.GetByOffset(j);
                    var (ix, iy) = CollisionSpacePositionToWorldPosition(collider.X + pi.X, collider.Y + pi.Y, spaceOffsetX, spaceOffsetY);
                    var (jx, jy) = CollisionSpacePositionToWorldPosition(collider.X + pj.X, collider.Y + pj.Y, spaceOffsetX, spaceOffsetY);
                    GL.Vertex3(ix, iy, 0);
                    GL.Vertex3(jx, jy, 0);
                }
                GL.End();
            }

            // Draw dynamic colliders
            var (_, rdf) = renderBuffer.GetByFrameId(renderFrameId);
            if (null != rdf) {
                for (int k = 0; k < roomCapacity; k++) {
                    var currCharacterDownsync = rdf.PlayersArr[k];
                    var chConfig = characters[currCharacterDownsync.SpeciesId];
                    float boxCx, boxCy, boxCw, boxCh;
                    calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);

                    var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
                    // World space width and height are just the same as that of collision space.

                    GL.Begin(GL.LINES);

                    GL.Vertex3((wx - 0.5f * boxCw), (wy - 0.5f * boxCh), 0);
                    GL.Vertex3((wx + 0.5f * boxCw), (wy - 0.5f * boxCh), 0);

                    GL.Vertex3((wx + 0.5f * boxCw), (wy - 0.5f * boxCh), 0);
                    GL.Vertex3((wx + 0.5f * boxCw), (wy + 0.5f * boxCh), 0);

                    GL.Vertex3((wx + 0.5f * boxCw), (wy + 0.5f * boxCh), 0);
                    GL.Vertex3((wx - 0.5f * boxCw), (wy + 0.5f * boxCh), 0);

                    GL.Vertex3((wx - 0.5f * boxCw), (wy + 0.5f * boxCh), 0);
                    GL.Vertex3((wx - 0.5f * boxCw), (wy - 0.5f * boxCh), 0);

                    GL.End();
                }

                for (int k = 0; k < rdf.NpcsArr.Count; k++) {
                    var currCharacterDownsync = rdf.NpcsArr[k];
                    if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
                    var chConfig = characters[currCharacterDownsync.SpeciesId];
                    float boxCx, boxCy, boxCw, boxCh;
                    calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);

                    var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
                    GL.Begin(GL.LINES);
                    GL.Color(Color.grey);

                    GL.Vertex3((wx - 0.5f * boxCw), (wy - 0.5f * boxCh), 0);
                    GL.Vertex3((wx + 0.5f * boxCw), (wy - 0.5f * boxCh), 0);

                    GL.Vertex3((wx + 0.5f * boxCw), (wy - 0.5f * boxCh), 0);
                    GL.Vertex3((wx + 0.5f * boxCw), (wy + 0.5f * boxCh), 0);

                    GL.Vertex3((wx + 0.5f * boxCw), (wy + 0.5f * boxCh), 0);
                    GL.Vertex3((wx - 0.5f * boxCw), (wy + 0.5f * boxCh), 0);

                    GL.Vertex3((wx - 0.5f * boxCw), (wy + 0.5f * boxCh), 0);
                    GL.Vertex3((wx - 0.5f * boxCw), (wy - 0.5f * boxCh), 0);

                    GL.End();

                    float visionCx, visionCy, visionCw, visionCh;
                    calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);
                    (wx, wy) = CollisionSpacePositionToWorldPosition(visionCx, visionCy, spaceOffsetX, spaceOffsetY);

                    GL.Begin(GL.LINES);
                    GL.Color(Color.yellow);
                    
                    GL.Vertex3((wx - 0.5f * visionCw), (wy - 0.5f * visionCh), 0);
                    GL.Vertex3((wx + 0.5f * visionCw), (wy - 0.5f * visionCh), 0);

                    GL.Vertex3((wx + 0.5f * visionCw), (wy - 0.5f * visionCh), 0);
                    GL.Vertex3((wx + 0.5f * visionCw), (wy + 0.5f * visionCh), 0);

                    GL.Vertex3((wx + 0.5f * visionCw), (wy + 0.5f * visionCh), 0);
                    GL.Vertex3((wx - 0.5f * visionCw), (wy + 0.5f * visionCh), 0);

                    GL.Vertex3((wx - 0.5f * visionCw), (wy + 0.5f * visionCh), 0);
                    GL.Vertex3((wx - 0.5f * visionCw), (wy - 0.5f * visionCh), 0);

                    GL.End();
                }
            }
        } finally {
            GL.PopMatrix();
        }
    }
}
