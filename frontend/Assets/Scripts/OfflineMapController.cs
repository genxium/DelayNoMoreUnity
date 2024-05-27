using UnityEngine;
using System;
using shared;
using static shared.Battle;
using System.Threading;

public class OfflineMapController : AbstractMapController {
    
    private bool isInStoryControl = false;
    private bool isInStorySettings = false;
    public DialogBoxes dialogBoxes;
    public StoryModeSettings storyModeSettings;
    private int initSeqNo = 0;
    private int cachedSelfSpeciesId = SPECIES_NONE_CH;
    private string cachedLevelName = null;
    private RoomDownsyncFrame cachedStartRdf = null;

    public StoryLevelSelectPanel characterSelectPanel;

    protected override void sendInputFrameUpsyncBatch(int noDelayInputFrameId) {
        throw new NotImplementedException();
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        return false;
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();
        characterSelectPanel.gameObject.SetActive(true);
        characterSelectPanel.reset();

        initSeqNo = 0;
        cachedSelfSpeciesId = SPECIES_NONE_CH;
        cachedLevelName = null;
        cachedStartRdf = null;
    }

    public override void onCharacterSelectGoAction(int speciesId) {
        throw new NotImplementedException();
    }

    public override void onCharacterAndLevelSelectGoAction(int speciesId, string levelName) {
        Debug.Log(String.Format("Executing extra goAction with selectedSpeciesId={0}, selectedLevelName={1}", speciesId, levelName));
        cachedSelfSpeciesId = speciesId;
        cachedLevelName = levelName;
        initSeqNo = 1; // To avoid loading "underlyingMap" in this click callback
        enableBattleInput(false);
    }


    // Start is called before the first frame update
    void Start() {
        debugDrawingAllocation = true;
        debugDrawingEnabled = false;
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.targetFrameRate = 60;
        isOnlineMode = false;

        StoryModeSettings.SimpleDelegate onExitCallback = () => {
            onBattleStopped(); // [WARNING] Deliberately NOT calling "pauseAllAnimatingCharacters(false)" such that "iptmgr.gameObject" remains inactive, unblocking the keyboard control to "characterSelectPanel"! 
            isInStorySettings = false;
        };
        StoryModeSettings.SimpleDelegate onCancelCallback = () => {
            isInStorySettings = false;
            pauseAllAnimatingCharacters(false);
        };
        storyModeSettings.SetCallbacks(onExitCallback, onCancelCallback);
    }

    // Update is called once per frame
    void Update() {
        try {
            if (1 == initSeqNo) {
                selfPlayerInfo = new CharacterDownsync();

                roomCapacity = 1;
                resetCurrentMatch(cachedLevelName);
                calcCameraCaps();
                preallocateBattleDynamicsHolder();
                preallocateFrontendOnlyHolders();
                preallocateVfxNodes();
                preallocateSfxNodes();
                preallocatePixelVfxNodes();
                preallocateNpcNodes();
                selfPlayerInfo.JoinIndex = 1;

                initSeqNo++; // To avoid accessing "gameObject.transform" in the same renderFrame right after "resetCurrentMatch" and the "preallocations"
            } else if (2 == initSeqNo) {
                Debug.Log("About to mock start rdf");
                // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START"
                int[] speciesIdList = new int[roomCapacity];
                speciesIdList[selfPlayerInfo.JoinIndex - 1] = cachedSelfSpeciesId;
                var (startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, battleDurationSecondsVal) = mockStartRdf(speciesIdList);
                battleDurationFrames = battleDurationSecondsVal * BATTLE_DYNAMICS_FPS;
                refreshColliders(startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerTrackingIdToTrapLocalId);
                cachedStartRdf = startRdf;

                applyRoomDownsyncFrameDynamics(startRdf, null);
                cameraTrack(startRdf, null, false);
                var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
                Debug.Log(String.Format("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
                initSeqNo++;
            } else if (3 == initSeqNo) {
                Debug.Log(String.Format("characterSelectPanel about to hide, thread id={0}", Thread.CurrentThread.ManagedThreadId));
                characterSelectPanel.gameObject.SetActive(false);
                Debug.Log(String.Format("characterSelectPanel hidden, thread id={0}", Thread.CurrentThread.ManagedThreadId));
                initSeqNo++;
            } else if (4 == initSeqNo) {
                Debug.Log(String.Format("about to ready animation, thread id={0}", Thread.CurrentThread.ManagedThreadId));
                readyGoPanel.playReadyAnim(null, () => {
                    Debug.Log(String.Format("played ready animation, thread id={0}", Thread.CurrentThread.ManagedThreadId));
                    initSeqNo++;
                    // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START"
                    cachedStartRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
                    onRoomDownsyncFrame(cachedStartRdf, null);
                    pauseAllAnimatingCharacters(false);
                });
                initSeqNo++;
            } else if (6 == initSeqNo) {
                enableBattleInput(true);
                readyGoPanel.playGoAnim();
                bgmSource.Play();
                initSeqNo++;
            }

            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }

            if (isInStoryControl) {
                // TODO: What if dynamics should be updated during story narrative? A simple proposal is to cover all objects with a preset RenderFrame, yet there's a lot to hardcode by this approach. 
                var (ok, rdf) = renderBuffer.GetByFrameId(playerRdfId);
                if (!ok || null == rdf || !dialogBoxes.renderStoryPoint(rdf, levelId, justTriggeredStoryId)) {
                    dialogBoxes.gameObject.SetActive(false);
                    isInStoryControl = false;
                    pauseAllAnimatingCharacters(false);
                    iptmgr.resumeScales();
                    iptmgr.enable(true);
                } else {
                    // No dynamics during "InStoryControl".
                    return;
                }
            }
            if (isInStorySettings) {
                // No dynamics during "InStorySettings".
                return;
            }
            doUpdate();
            if (0 < justFulfilledEvtSubCnt) {
                for (int i = 0; i < justFulfilledEvtSubCnt; i++) {
                    int justFulfilledEvtSub = justFulfilledEvtSubArr[i]; 
                    if (MAGIC_EVTSUB_ID_STORYPOINT == justFulfilledEvtSub) {
                        iptmgr.enable(false);
                        // Handover control to DialogBox GUI
                        isInStoryControl = true; // Set it back to "false" in the DialogBox control!
                        pauseAllAnimatingCharacters(true);
                        var msg = String.Format("Story control handover triggered at playerRdfId={0}", playerRdfId);
                        Debug.Log(msg);
                        dialogBoxes.gameObject.SetActive(true);
                        dialogBoxes.init();
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

    public override void OnSettingsClicked() {
        if (isInStoryControl || isInStorySettings) return;
        if (ROOM_STATE_IN_BATTLE != battleState) return;
        pauseAllAnimatingCharacters(true);
        storyModeSettings.gameObject.SetActive(true);
        isInStorySettings = true;
        storyModeSettings.toggleUIInteractability(true);
    }
}
