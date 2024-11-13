using UnityEngine;
using System;
using System.Collections;
using shared;
using static shared.Battle;
using System.Threading;
using UnityEngine.SceneManagement;
using Story;
using Google.Protobuf;

public class OfflineMapController : AbstractMapController {
    private LevelStory currentStory = null;
    private StoryPoint justTriggeredStoryPoint = null;

    private bool isInStoryControl = false;
    private bool isInStoryAutoplay = false;
    private bool isInStorySettings = false;
    public NoBranchStoryNarrativeDialogBox noBranchStoryNarrativeDialogBoxes;
    public AutoplayStoryNarrativeDialogBox autoplayStoryNarrativeDialogBoxes;
    public StoryModeSettings storyModeSettings;
    private int initSeqNo = 0;
    private RoomDownsyncFrame cachedStartRdf = null;
    protected override void sendInputFrameUpsyncBatch(int noDelayInputFrameId) {
        throw new NotImplementedException();
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        return false;
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();

        initSeqNo = 0;
        cachedStartRdf = null;
        currentStory = null;
        justTriggeredStoryPoint = null;
        isInStoryControl = false;
        isInStoryAutoplay = false;
        isInStorySettings = false;

        PlayerStoryProgressManager.Instance.ResetCachedForOfflineMap();
        if (PlayerStoryProgressManager.Instance.HasAnyUsedSlot()) {
            SceneManager.LoadScene("StoryLevelSelectScene", LoadSceneMode.Single);
        } else {
            SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
        }
    }

    // Start is called before the first frame update
    void Start() {
        debugDrawingAllocation = true;
        debugDrawingEnabled = false;
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.targetFrameRate = 60;
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        mainCamera = Camera.main;
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

        initSeqNo = 1; // To avoid loading "underlyingMap" in this click callback
        enableBattleInput(false);
    }

    // Update is called once per frame
    void Update() {
        try {
            if (1 == initSeqNo) {
                initSeqNo++;
            } else if (2 == initSeqNo) {
                selfPlayerInfo = new CharacterDownsync();
                roomCapacity = 1;
                resetCurrentMatch(PlayerStoryProgressManager.Instance.GetCachedLevelName());
                calcCameraCaps();
                preallocateBattleDynamicsHolder();
                preallocateFrontendOnlyHolders();
                preallocateSfxNodes();
                preallocatePixelVfxNodes();
                preallocateNpcNodes();
                selfPlayerInfo.JoinIndex = 1;
                initSeqNo++; // To avoid accessing "gameObject.transform" in the same renderFrame right after "resetCurrentMatch" and the "preallocations"
            } else if (3 == initSeqNo) {
                Debug.Log("About to mock start rdf");
                // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START"
                uint[] speciesIdList = new uint[roomCapacity];
                speciesIdList[selfPlayerInfo.JoinIndex - 1] = PlayerStoryProgressManager.Instance.GetCachedChSpeciesId();
                var (startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, battleDurationSecondsVal) = mockStartRdf(speciesIdList, PlayerStoryProgressManager.Instance.GetCachedFinishedLvOption());
                Debug.LogFormat("mockStartRdf with {0} bytes", startRdf.ToByteArray().Length);
                attachParallaxEffect();
                battleDurationFrames = battleDurationSecondsVal * BATTLE_DYNAMICS_FPS;
                refreshColliders(startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId);
                cachedStartRdf = startRdf;

                foreach (var trigger in startRdf.TriggersArr) {
                    if (TERMINATING_TRIGGER_ID == trigger.TriggerLocalId) break;
                    if (trigger.ConfigFromTiled.IsBossSavepoint) {
                        bossSavepointMask |= (1ul << trigger.TriggerLocalId);
                    }
                    if (null != trigger.ConfigFromTiled.BossSpeciesSet) {
                        bossSpeciesSet.UnionWith(trigger.ConfigFromTiled.BossSpeciesSet.Keys);
                    }
                }

                cameraTrack(startRdf, null, false); // Move camera first, such that NPCs can be rendered in cam view
                applyRoomDownsyncFrameDynamics(startRdf, null);
                var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
                Debug.LogFormat("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position);
                initSeqNo++;
            } else if (4 == initSeqNo) {
                Debug.LogFormat("about to play ready animation, thread id={0}", Thread.CurrentThread.ManagedThreadId);
                readyGoPanel.playReadyAnim(null, () => {
                    Debug.LogFormat("played ready animation, thread id={0}", Thread.CurrentThread.ManagedThreadId);
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
                if (!ok || null == rdf || !noBranchStoryNarrativeDialogBoxes.renderStoryPoint(rdf, justTriggeredStoryPoint)) {
                    noBranchStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                    isInStoryControl = false;
                    pauseAllAnimatingCharacters(false);
                    iptmgr.resumeScales();
                    iptmgr.enable(true);
                } else {
                    // No dynamics during "InStoryControl".
                    autoplayStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                    isInStoryAutoplay = false;
                    return;
                }
            } else if (isInStoryAutoplay) {
                var (ok, rdf) = renderBuffer.GetByFrameId(playerRdfId);
                if (!ok || null == rdf || !autoplayStoryNarrativeDialogBoxes.renderStoryPoint(rdf, justTriggeredStoryPoint)) {
                    autoplayStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                    isInStoryAutoplay = false;
                }
            }

            if (BGM_NO_CHANGE != justTriggeredBgmId) {
                bgmSource.PlaySpecifiedBgm(justTriggeredBgmId);
                justTriggeredBgmId = BGM_NO_CHANGE;
            }

            if (isInStorySettings) {
                // No dynamics during "InStorySettings".
                return;
            }
            justTriggeredStoryPointId = StoryConstants.STORY_POINT_NONE;
            doUpdate();
            if (StoryConstants.STORY_POINT_NONE != justTriggeredStoryPointId) {
                if (null == currentStory) {
                    currentStory = StoryUtil.getStory(levelId);
                }
                var oldTriggeredStoryPoint = justTriggeredStoryPoint;
                justTriggeredStoryPoint = StoryUtil.getStoryPoint(currentStory, justTriggeredStoryPointId);

                if (!justTriggeredStoryPoint.Autoplay) {
                    iptmgr.enable(false);
                    // Handover control to DialogBox GUI
                    isInStoryControl = true; // Set it back to "false" in the DialogBox control!
                    pauseAllAnimatingCharacters(true);
                    Debug.LogFormat("Story control handover triggered at playerRdfId={0}", playerRdfId);
                    noBranchStoryNarrativeDialogBoxes.gameObject.SetActive(true);
                    noBranchStoryNarrativeDialogBoxes.init();
                    return;
                } else {
                    if (isInStoryAutoplay) {
                        Debug.LogWarningFormat("Story autoplay triggered at playerRdfId={0} but we're in another autoplay story-point, please check configuration!", playerRdfId);
                        justTriggeredStoryPoint = oldTriggeredStoryPoint;
                    } else {
                        isInStoryAutoplay = true;
                        autoplayStoryNarrativeDialogBoxes.gameObject.SetActive(true);
                        autoplayStoryNarrativeDialogBoxes.init();
                        Debug.LogFormat("Story autoplay triggered at playerRdfId={0}", playerRdfId);
                    }
                }
            }
            
            var (res1, currRdf) = renderBuffer.GetByFrameId(playerRdfId-1); // [WARNING] After "doUpdate()", "playerRdfId" is incremented  
            var (res2, nextRdf) = renderBuffer.GetByFrameId(playerRdfId);   
            if (!res1 || null == currRdf) {
                throw new Exception("Couldn't find a valid currRdf");
            }
            if (!res2 || null == nextRdf) {
                throw new Exception("Couldn't find a valid nextRdf");
            }
            ulong triggeredBossSavepointMask = (fulfilledTriggerSetMask & bossSavepointMask);
            if (0 < triggeredBossSavepointMask) {
                Debug.LogFormat("Triggered boss savepoint at post-doUpdate-playerRdfId={0}", playerRdfId);
                int bossSavepointTriggerLocalId = -1;
                while (0 < triggeredBossSavepointMask) {
                    triggeredBossSavepointMask >>= 1;
                    ++bossSavepointTriggerLocalId;
                }
                if (0 <= bossSavepointTriggerLocalId) {
                    if (null == latestBossSavepoint) {
                        latestBossSavepoint = new RoomDownsyncFrame(historyRdfHolder);
                    }
                    AssignToBossSavepoint(currRdf, nextRdf, latestBossSavepoint, roomCapacity, _loggerBridge);
                    Debug.LogFormat("Done boss savepoint at post-doUpdate-playerRdfId={0}", playerRdfId);
                }
            } else {
                if (1 == roomCapacity) {
                    var nextPlayerChd = nextRdf.PlayersArr[0];
                    if (nextPlayerChd.NewBirth && null != latestBossSavepoint) {
                        AssignFromBossSavepoint(latestBossSavepoint, nextRdf, roomCapacity, joinIndexRemap, justDeadNpcIndices, bossSpeciesSet, _loggerBridge);
                    }
                }
            }

            if (BGM_NO_CHANGE != justTriggeredBgmId) {
                // By default put BGM change after story narrative is over
                bgmSource.PlaySpecifiedBgm(justTriggeredBgmId);
                justTriggeredBgmId = BGM_NO_CHANGE;
            }
            urpDrawDebug();
            if (playerRdfId >= battleDurationFrames) {
                StartCoroutine(delayToShowSettlementPanel());
            } else {
                readyGoPanel.setCountdown(playerRdfId, battleDurationFrames);
            }

            bool battleResultIsSet = isBattleResultSet(confirmedBattleResult);
            if (battleResultIsSet) {
                if (StoryConstants.LEVEL_NAMES.ContainsKey(levelId)) {
                    // TODO: Use real "score" and "finishTime"
                    PlayerStoryProgressManager.Instance.FinishLevel(levelId, 100, 100, PlayerStoryProgressManager.Instance.GetCachedChSpeciesId(), true);
                }
            }
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

    protected override IEnumerator delayToShowSettlementPanel() {
        var storySettlementPanel = settlementPanel as StorySettlementPanel;
        if (ROOM_STATE_IN_BATTLE != battleState) {
            Debug.LogWarning("Why calling delayToShowSettlementPanel during active battle? playerRdfId = " + playerRdfId);
            yield return new WaitForSeconds(0);
        } else {
            battleState = ROOM_STATE_IN_SETTLEMENT;
            storySettlementPanel.postSettlementCallback = () => {
                onBattleStopped();
            };
            storySettlementPanel.gameObject.SetActive(true);
            storySettlementPanel.toggleUIInteractability(true);
            var (ok, rdf) = renderBuffer.GetByFrameId(playerRdfId-1);
            if (ok && null != rdf) {
                storySettlementPanel.SetCharacter(rdf.PlayersArr[selfPlayerInfo.JoinIndex - 1]);
            }
            storySettlementPanel.SetTimeUsed(playerRdfId);
            // TODO: In versus mode, should differentiate between "winnerJoinIndex == selfPlayerIndex" and otherwise
            if (isBattleResultSet(confirmedBattleResult)) {
                storySettlementPanel.PlaySettlementAnim(true);
            } else {
                storySettlementPanel.PlaySettlementAnim(false);
            }
        }
    }
}
