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
    public Camera cutsceneCamera;
    public CutsceneManager storyControlCutsceneManager;
    public CutsceneManager nonctrlCutsceneManager;

    private LevelStory currentStory = null;
    private StoryPoint justTriggeredStoryPoint = null;

    private bool isInStoryControl = false;
    private bool isInNonctrlStory = false;
    private bool isInStorySettings = false;

    public NoBranchStoryNarrativeDialogBox noBranchStoryNarrativeDialogBoxes;
    public NonctrlStoryNarrativeDialogBox nonctrlStoryNarrativeDialogBoxes;
    public StoryModeSettings storyModeSettings;
    private int initSeqNo = 0;
    private RoomDownsyncFrame cachedStartRdf = null;
    protected override void sendInputFrameUpsyncBatch(int noDelayInputFrameId) {
        throw new NotImplementedException();
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        return false;
    }

    protected void gobackToSelection() {
        PlayerStoryProgressManager.Instance.ResetCachedForOfflineMap();
        if (PlayerStoryProgressManager.Instance.HasAnyUsedSlot()) {
            SceneManager.LoadScene("StoryLevelSelectScene", LoadSceneMode.Single);
        } else {
            SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
        }
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();

        initSeqNo = 0;
        cachedStartRdf = null;
        currentStory = null;
        justTriggeredStoryPoint = null;
        isInStoryControl = false;
        isInNonctrlStory = false;
        isInStorySettings = false;
        remainingTriggerForceCtrlRdfCount = 0;
        latestTriggerForceCtrlCmd = 0;
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
        isOnlineMode = false;
        StoryModeSettings.SimpleDelegate onExitCallback = () => {
            onBattleStopped(); // [WARNING] Deliberately NOT calling "pauseAllAnimatingCharacters(false)" such that "iptmgr.gameObject" remains inactive, unblocking the keyboard control to "characterSelectPanel"! 
            gobackToSelection();
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
                gameplayCamera.transform.position = new Vector3(-effectivelyInfinitelyFar, -effectivelyInfinitelyFar, 1024);
                cutsceneCamera.transform.position = new Vector3(-effectivelyInfinitelyFar, -effectivelyInfinitelyFar, 1024);
                initSeqNo++;
            } else if (2 == initSeqNo) {
                selfPlayerInfo = new CharacterDownsync();
                roomCapacity = 1;
                resetCurrentMatch(PlayerStoryProgressManager.Instance.GetCachedLevelName());
                calcCameraCaps();
                storyControlCutsceneManager.effectivelyInfinitelyFar = effectivelyInfinitelyFar;
                nonctrlCutsceneManager.effectivelyInfinitelyFar = effectivelyInfinitelyFar;
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
                refreshColliders(startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId, ref triggerEditorIdToConfigFromTiled);
                cachedStartRdf = startRdf;

                foreach (var trigger in startRdf.TriggersArr) {
                    if (TERMINATING_TRIGGER_ID == trigger.TriggerLocalId) break;
                    var configFromTiled = triggerEditorIdToConfigFromTiled[trigger.EditorId];
                    if (configFromTiled.IsBossSavepoint) {
                        bossSavepointMask |= (1ul << (trigger.TriggerLocalId - 1));
                    }
                    if (0 < configFromTiled.ForceCtrlRdfCount) {
                        triggerForceCtrlMask |= (1ul << (trigger.TriggerLocalId - 1));
                    }
                    if (null != configFromTiled.BossSpeciesSet) {
                        bossSpeciesSet.UnionWith(configFromTiled.BossSpeciesSet.Keys);
                    }
                }

                currentStory = StoryUtil.getStory(levelId);

                initSeqNo++;
            } else if (4 == initSeqNo) {
                if (FinishedLvOption.StoryAndBoss == PlayerStoryProgressManager.Instance.GetCachedFinishedLvOption() && StoryUtil.STORY_NONE != currentStory && currentStory.Points.ContainsKey(StoryConstants.STORY_POINT_LV_INTRO)) {
                    var stPoint = currentStory.Points[StoryConstants.STORY_POINT_LV_INTRO]; 
                    var cutsceneName = stPoint.CutsceneName; 
                    if (!storyControlCutsceneManager.playOrContinue(cutsceneName)) {
                        storyControlCutsceneManager.clear();
                    } else {
                        return;
                    }
                }

                initSeqNo++;
            } else if (5 == initSeqNo) {
                cameraTrack(cachedStartRdf, null, false); // Move camera first, such that NPCs can be rendered in cam view
                applyRoomDownsyncFrameDynamics(cachedStartRdf, null);
                var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
                Debug.LogFormat("Battle ready to start, teleport camera to selfPlayer dst={0}, thread id={1}", playerGameObj.transform.position, Thread.CurrentThread.ManagedThreadId);
                readyGoPanel.playReadyAnim(null, () => {
                    Debug.LogFormat("played ready animation, thread id={0}", Thread.CurrentThread.ManagedThreadId);
                    initSeqNo++;
                    // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START"
                    cachedStartRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
                    onRoomDownsyncFrame(cachedStartRdf, null);
                    pauseAllAnimatingCharacters(false);
                });
                initSeqNo++;
            } else if (7 == initSeqNo) {
                enableBattleInput(true);
                readyGoPanel.playGoAnim();
                bgmSource.Play();
                initSeqNo++;
            }

            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }

            if (isInStoryControl) {
                var cutsceneName = justTriggeredStoryPoint.CutsceneName;
                var (ok, rdf) = renderBuffer.GetByFrameId(playerRdfId);
                // TODO: What if dynamics should be updated during story narrative? A simple proposal is to cover all objects with a preset RenderFrame, yet there's a lot to hardcode by this approach. 
                if (!ok || null == rdf) {
                    nonctrlCutsceneManager.clear();
                    storyControlCutsceneManager.clear();
                    noBranchStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                    isInStoryControl = false;
                    pauseAllAnimatingCharacters(false);
                    iptmgr.resumeScales();
                    iptmgr.enable(true);
                } else if (String.IsNullOrEmpty(cutsceneName)) {
                    if (!noBranchStoryNarrativeDialogBoxes.isActiveAndEnabled) {
                        noBranchStoryNarrativeDialogBoxes.gameObject.SetActive(true);
                        noBranchStoryNarrativeDialogBoxes.init();
                    }
                    if (!noBranchStoryNarrativeDialogBoxes.renderStoryPoint(rdf, justTriggeredStoryPoint)) {
                        noBranchStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                        isInStoryControl = false;
                        pauseAllAnimatingCharacters(false);
                        iptmgr.resumeScales();
                        iptmgr.enable(true);
                    } else {
                        // No dynamics or nonctrl-narrative during "InStoryControl" -- however whether or not nonctrl-cutscenes are allowed is pending discussion
                        nonctrlCutsceneManager.clear();
                        nonctrlStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                        isInNonctrlStory = false;
                        return;
                    }
                } else {
                    pauseAllAnimatingCharacters(true);
                    iptmgr.enable(false);
                    if (!storyControlCutsceneManager.playOrContinue(cutsceneName)) {
                        storyControlCutsceneManager.clear();
                        isInStoryControl = false;
                        pauseAllAnimatingCharacters(false);
                        iptmgr.resumeScales();
                        iptmgr.enable(true);
                    } else {
                        // No dynamics or nonctrl-narrative during "InStoryControl" -- however whether or not nonctrl-cutscenes are allowed is pending discussion
                        nonctrlCutsceneManager.clear();
                        nonctrlStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                        isInNonctrlStory = false;
                        return;
                    }
                }
            } else if (isInNonctrlStory) {
                var cutsceneName = justTriggeredStoryPoint.CutsceneName;
                var (ok, rdf) = renderBuffer.GetByFrameId(playerRdfId);
                if (!ok || null == rdf) {
                    nonctrlStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                    isInNonctrlStory = false;
                } else if (String.IsNullOrEmpty(cutsceneName)) {
                    nonctrlStoryNarrativeDialogBoxes.gameObject.SetActive(true);
                    if (!nonctrlStoryNarrativeDialogBoxes.renderStoryPoint(rdf, justTriggeredStoryPoint)) {
                        nonctrlStoryNarrativeDialogBoxes.gameObject.SetActive(false);
                        isInNonctrlStory = false;
                    }
                } else {
                    if (!nonctrlCutsceneManager.playOrContinue(cutsceneName)) {
                        nonctrlCutsceneManager.clear();
                        isInNonctrlStory = false;
                    }
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
            if (StoryConstants.STORY_POINT_NONE != justTriggeredStoryPointId && StoryUtil.STORY_NONE != currentStory) {

                var oldTriggeredStoryPoint = justTriggeredStoryPoint;
                justTriggeredStoryPoint = StoryUtil.getStoryPoint(currentStory, justTriggeredStoryPointId);

                if (!justTriggeredStoryPoint.Nonctrl) {
                    iptmgr.enable(false);
                    // Handover control to DialogBox GUI
                    isInStoryControl = true; // Set it back to "false" in the DialogBox control!
                    pauseAllAnimatingCharacters(true);
                    Debug.LogFormat("Story control handover triggered at playerRdfId={0}", playerRdfId);
                    return;
                } else {
                    if (isInNonctrlStory) {
                        Debug.LogWarningFormat("NonctrlStory  triggered at playerRdfId={0} but we're in another nonctrl story-point, please check configuration!", playerRdfId);
                        justTriggeredStoryPoint = oldTriggeredStoryPoint;
                    } else {
                        isInNonctrlStory = true;
                        if (null != justTriggeredStoryPoint.CutsceneName) {
                            Debug.LogFormat("NonctrlCutscene triggered at playerRdfId={0}: {1}", playerRdfId, justTriggeredStoryPoint.CutsceneName);
                        } else {
                            nonctrlStoryNarrativeDialogBoxes.gameObject.SetActive(true);
                            nonctrlStoryNarrativeDialogBoxes.init();
                            Debug.LogFormat("NonctrlStory triggered at playerRdfId={0}", playerRdfId);
                        }
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
            ulong triggeredForceCtrlMask = (fulfilledTriggerSetMask & triggerForceCtrlMask);
            if (0 < triggeredForceCtrlMask) {
                Debug.LogFormat("Triggered force ctrl mask={1} at post-doUpdate-playerRdfId={0}", playerRdfId, triggeredForceCtrlMask);
                int forceCtrlTriggerLocalId = 0;
                while (0 < triggeredForceCtrlMask) {
                    triggeredForceCtrlMask >>= 1;
                    ++forceCtrlTriggerLocalId;
                }
                if (0 < forceCtrlTriggerLocalId) {
                    var trigger = currRdf.TriggersArr[forceCtrlTriggerLocalId-1];
                    var triggerConfigFromTiled = triggerEditorIdToConfigFromTiled[trigger.EditorId];
                    remainingTriggerForceCtrlRdfCount = triggerConfigFromTiled.ForceCtrlRdfCount;
                    latestTriggerForceCtrlCmd = triggerConfigFromTiled.ForceCtrlCmd;
                    
                    Debug.LogFormat("Picked remainingTriggerForceCtrlRdfCount={1}, latestTriggerForceCtrlCmd={2} at post-doUpdate-playerRdfId={0}", playerRdfId, remainingTriggerForceCtrlRdfCount, latestTriggerForceCtrlCmd);
                }
            }

            ulong triggeredBossSavepointMask = (fulfilledTriggerSetMask & bossSavepointMask);
            if (0 < triggeredBossSavepointMask) {
                Debug.LogFormat("Triggered boss savepoint mask={1} at post-doUpdate-playerRdfId={0}", playerRdfId, triggeredBossSavepointMask);
                int bossSavepointTriggerLocalId = 0;
                while (0 < triggeredBossSavepointMask) {
                    triggeredBossSavepointMask >>= 1;
                    ++bossSavepointTriggerLocalId;
                }
                if (0 < bossSavepointTriggerLocalId) {
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
                settlementRdfId = playerRdfId;
                Debug.LogWarning("Calling onBattleStopped with localTimerEnded @playerRdfId=" + playerRdfId);
                onBattleStopped();
                StartCoroutine(delayToShowSettlementPanel());
            } else {
                readyGoPanel.setCountdown(playerRdfId, battleDurationFrames);
                bool battleResultIsSet = isBattleResultSet(confirmedBattleResult);
                if (battleResultIsSet) {
                    if (StoryConstants.LEVEL_NAMES.ContainsKey(levelId)) {
                        // TODO: Use real "score" and "finishTime"
                        PlayerStoryProgressManager.Instance.FinishLevel(levelId, 100, 100, PlayerStoryProgressManager.Instance.GetCachedChSpeciesId(), true);
                    }
                    Debug.LogWarning("Calling onBattleStopped with confirmedBattleResult=" + confirmedBattleResult.ToString() + " @playerRdfId=" + playerRdfId);
                    settlementRdfId = playerRdfId;
                    onBattleStopped();
                    StartCoroutine(delayToShowSettlementPanel());
                }
            }
        } catch (Exception ex) {
            var msg = String.Format("Error during OfflineMap.Update {0}", ex);
            popupErrStackPanel(msg);
            Debug.LogWarning(msg);
            onBattleStopped();
            gobackToSelection();
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
        if (ROOM_STATE_IN_BATTLE == battleState) {
            Debug.LogWarning("Why calling delayToShowSettlementPanel during active battle? playerRdfId = " + playerRdfId);
            yield return new WaitForSeconds(0);
        } else {
            battleState = ROOM_STATE_IN_SETTLEMENT;
            storySettlementPanel.postSettlementCallback = () => {
                gobackToSelection();
            };
            storySettlementPanel.gameObject.SetActive(true);
            storySettlementPanel.toggleUIInteractability(true);
            var (ok, rdf) = renderBuffer.GetByFrameId(settlementRdfId-1);
            if (ok && null != rdf) {
                storySettlementPanel.SetCharacter(rdf.PlayersArr[selfPlayerInfo.JoinIndex - 1]);
            }
            storySettlementPanel.SetTimeUsed(settlementRdfId);
            // TODO: In versus mode, should differentiate between "winnerJoinIndex == selfPlayerIndex" and otherwise
            if (isBattleResultSet(confirmedBattleResult)) {
                storySettlementPanel.PlaySettlementAnim(true);
            } else {
                storySettlementPanel.PlaySettlementAnim(false);
            }
        }
    }
}
