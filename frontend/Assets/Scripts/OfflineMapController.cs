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
        resetCurrentMatch("TwoStepStage");
        preallocateVfxNodes();
        preallocateSfxNodes();
        preallocateNpcNodes();
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
        bgmSource.Play();
        // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START"
        startRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
        onRoomDownsyncFrame(startRdf, null);
    }


    // Start is called before the first frame update
    void Start() {
        if (null == postSettlementCallback) {
            postSettlementCallback = () => {
                onBattleStopped();
            };
        }
        debugDrawingAllocation = true;
        debugDrawingEnabled = false;
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.targetFrameRate = 60;
    }

    // Update is called once per frame
    void Update() {
        try {
            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }
            doUpdate();
            urpDrawDebug();
            if (playerRdfId >= battleDurationFrames) {
                onBattleStopped();
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
