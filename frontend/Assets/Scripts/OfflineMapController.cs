using UnityEngine;
using System;
using System.Collections;
using shared;
using static shared.Battle;

public class OfflineMapController : AbstractMapController {

    private bool isInStoryControl = false;
    public DialogBoxes dialogBoxes;
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
        throw new NotImplementedException();
    }

    public override void onCharacterAndLevelSelectGoAction(int speciesId, string levelName) {
        Debug.Log(String.Format("Executing extra goAction with selectedSpeciesId={0}, selectedLevelName={1}", speciesId, levelName));
        selfPlayerInfo = new CharacterDownsync();

        roomCapacity = 1;
        preallocateHolders();
        resetCurrentMatch(levelName);
        preallocateVfxNodes();
        preallocateSfxNodes();
        preallocateNpcNodes();
        selfPlayerInfo.JoinIndex = 1;

        battleDurationFrames = 60 * 60;

        // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START"
        int[] speciesIdList = new int[roomCapacity];
        speciesIdList[selfPlayerInfo.JoinIndex - 1] = speciesId;
        var (startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId) = mockStartRdf(speciesIdList);
        refreshColliders(startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, ref collisionHolder, ref  completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerTrackingIdToTrapLocalId);
        applyRoomDownsyncFrameDynamics(startRdf, null);
        cameraTrack(startRdf, null);
        var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
        Debug.Log(String.Format("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
        characterSelectPanel.gameObject.SetActive(false);
        readyGoPanel.playReadyAnim();

        StartCoroutine(delayToStartBattle(startRdf));
    }

    private IEnumerator delayToStartBattle(RoomDownsyncFrame startRdf) {
        yield return new WaitForSeconds(1);
        readyGoPanel.playGoAnim();
        bgmSource.Play();
        // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START"
        startRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
        onRoomDownsyncFrame(startRdf, null);
    }


    // Start is called before the first frame update
    void Start() {
        debugDrawingAllocation = true;
        debugDrawingEnabled = false;
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.targetFrameRate = 60;
        isOnlineMode = false;
    }

    // Update is called once per frame
    void Update() {
        try {
            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }
            if (isInStoryControl) {
                // No stepping during "InStoryControl".
                // TODO: What if dynamics should be updated during story narrative? A simple proposal is to cover all objects with a preset RenderFrame, yet there's a lot to hardcode by this approach. 
                return;
            }
            doUpdate();
            if (0 < justFulfilledEvtSubCnt) {
                for (int i = 0; i < justFulfilledEvtSubCnt; i++) {
                    int justFulfilledEvtSub = justFulfilledEvtSubArr[i]; 
                    if (MAGIC_EVTSUB_ID_STORYPOINT == justFulfilledEvtSub) {
                        // Handover control to DialogBox GUI
                        isInStoryControl = true; // Set it back to "false" in the DialogBox control!
                        pauseAllAnimatingCharacters(true);
                        var msg = String.Format("Story control handover triggered at playerRdfId={0}", playerRdfId);
                        Debug.Log(msg);
                        dialogBoxes.gameObject.SetActive(true);
                        break;
                    }
                }  
            }
            urpDrawDebug();
            if (playerRdfId >= battleDurationFrames) {
                StartCoroutine(delayToShowSettlementPanel());
            } else {
                readyGoPanel.setCountdown(playerRdfId, battleDurationFrames);
            }
            //throw new NotImplementedException("Intended");
        } catch (Exception ex) {
            var msg = String.Format("Error during OfflineMap.Update {0}", ex);
            Debug.Log(msg);
            popupErrStackPanel(msg);
            onBattleStopped();
        }
    }

    public void OnBackButtonClicked() {
        Debug.Log("OnBackButtonClicked");
        onBattleStopped();
    }
}
