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

    private const int DEFAULT_FRAMES_TO_SKIP_LANDING_SINCE_START = (BATTLE_DYNAMICS_FPS);
    private const int DEFAULT_STYCMD_CAPPED_FRAMES = (DEFAULT_FRAMES_TO_SKIP_LANDING_SINCE_START << 2);
    private LevelStory currentStory = null;
    private bool shouldPlayStoryLvIntro = false;
    private StoryPoint justTriggeredStoryPoint = null;
    private bool isInStoryControl = false;
    private bool isInNonctrlStory = false;
    private bool isInStorySettings = false;
    private int execStoryCmdfromMockRdfId = 0, execStoryCmdCappedMockRdfId = DEFAULT_STYCMD_CAPPED_FRAMES;

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
        execStoryCmdfromMockRdfId = 0;
        execStoryCmdCappedMockRdfId = DEFAULT_STYCMD_CAPPED_FRAMES;
        shouldPlayStoryLvIntro = false;
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
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
        isOnlineMode = false;
        renderBufferSize = 1024;
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

        // Overrides capacities
        preallocNpcCapacity = (DEFAULT_PREALLOC_NPC_CAPACITY << 1);
        preallocBulletCapacity = (DEFAULT_PREALLOC_BULLET_CAPACITY << 1);
        preallocTrapCapacity = (DEFAULT_PREALLOC_TRAP_CAPACITY << 1);
        preallocTriggerCapacity = (DEFAULT_PREALLOC_TRIGGER_CAPACITY << 1);
        preallocPickableCapacity = (DEFAULT_PREALLOC_PICKABLE_CAPACITY << 1);
    }

    // Update is called once per frame
    void Update() {
        try {
            if (1 == initSeqNo) {
                gameplayCamera.transform.position = new Vector3(-effectivelyInfinitelyFar, -effectivelyInfinitelyFar, 1024);
                cutsceneCamera.transform.position = new Vector3(-effectivelyInfinitelyFar, -effectivelyInfinitelyFar, 1024);
                initSeqNo++;
            } else if (2 == initSeqNo) {
                selfPlayerInfo = new PlayerMetaInfo();
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
                selfJoinIndex = selfPlayerInfo.JoinIndex; 
                selfJoinIndexArrIdx = selfJoinIndex - 1; 
                selfJoinIndexMask = (1UL << selfJoinIndexArrIdx); 
                allConfirmedMask = (1UL << roomCapacity) - 1;
                selfPlayerInfo.SpeciesId = PlayerStoryProgressManager.Instance.GetCachedChSpeciesId();
                shouldPlayStoryLvIntro = false;
                initSeqNo++; // To avoid accessing "gameObject.transform" in the same renderFrame right after "resetCurrentMatch" and the "preallocations"
            } else if (3 == initSeqNo) {
                Debug.Log("About to mock start rdf");
                // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START"
                uint[] speciesIdList = new uint[roomCapacity];
                speciesIdList[selfJoinIndexArrIdx] = selfPlayerInfo.SpeciesId;
                var (startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, battleDurationSecondsVal) = mockStartRdf(speciesIdList, PlayerStoryProgressManager.Instance.GetCachedFinishedLvOption());
                
                Debug.LogFormat("mockStartRdf with {0} bytes", startRdf.ToByteArray().Length);
                attachParallaxEffect();
                battleDurationFrames = battleDurationSecondsVal * BATTLE_DYNAMICS_FPS;
                refreshColliders(startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, collisionSpaceHalfWidth, collisionSpaceHalfHeight, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId, ref triggerEditorIdToConfigFromTiled);

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

                cachedStartRdf = startRdf;
                cachedStartRdf.Id = 0;
                renderBuffer.SetByFrameId(cachedStartRdf, 0);
                (execStoryCmdfromMockRdfId, execStoryCmdCappedMockRdfId) = elapseStoryCmd(0, DEFAULT_FRAMES_TO_SKIP_LANDING_SINCE_START);
                cachedStartRdf = renderBuffer.GetLast();
                Destroy(playerGameObjs[0]); // Immediately after this, "patchStartRdf" will re-spawn it at correct location
                patchStartRdf(cachedStartRdf, new uint[] { selfPlayerInfo.SpeciesId });

                currentStory = StoryUtil.getStory(levelId);
                shouldPlayStoryLvIntro = (FinishedLvOption.StoryAndBoss == PlayerStoryProgressManager.Instance.GetCachedFinishedLvOption() && StoryUtil.STORY_NONE != currentStory && currentStory.Points.ContainsKey(StoryConstants.STORY_POINT_LV_INTRO));

                initSeqNo++;
            } else if (4 == initSeqNo) {
                if (shouldPlayStoryLvIntro) {
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
                if (TERMINATING_TRIGGER_ID != storyReadyGoTriggerLocalId) {
                    if (execStoryCmdfromMockRdfId >= execStoryCmdCappedMockRdfId && DEFAULT_FRAMES_TO_SKIP_LANDING_SINCE_START == execStoryCmdCappedMockRdfId) {
                        execStoryCmdCappedMockRdfId = DEFAULT_STYCMD_CAPPED_FRAMES;
                        if (shouldPlayStoryLvIntro) {
                            cutsceneCamera.transform.position = new Vector3(-effectivelyInfinitelyFar, -effectivelyInfinitelyFar, 1024);
                            cameraTrack(cachedStartRdf, null, false, true);
                        }
                    }
                    (execStoryCmdfromMockRdfId, execStoryCmdCappedMockRdfId) = elapseStoryCmd(execStoryCmdfromMockRdfId, execStoryCmdCappedMockRdfId, shouldPlayStoryLvIntro, true);
                    if (execStoryCmdfromMockRdfId >= execStoryCmdCappedMockRdfId && DEFAULT_FRAMES_TO_SKIP_LANDING_SINCE_START < execStoryCmdCappedMockRdfId) {
                        execStoryCmdfromMockRdfId = 0;
                        execStoryCmdCappedMockRdfId = DEFAULT_STYCMD_CAPPED_FRAMES;
                        storyReadyGoTriggerLocalId = TERMINATING_TRIGGER_ID; // Let readygo play from next render frame
                        cachedStartRdf = renderBuffer.GetLast();
                        if (!shouldPlayStoryLvIntro) {
                            Destroy(playerGameObjs[0]); // Immediately after this, "patchStartRdf" will re-spawn it at correct location
                            patchStartRdf(cachedStartRdf, new uint[] { selfPlayerInfo.SpeciesId });
                        }
                    }
                    return;
                } else {
                    cameraTrack(cachedStartRdf, null, false, true); // Move camera first, such that NPCs can be rendered in cam view
                    applyRoomDownsyncFrameDynamics(cachedStartRdf, null);
                    var playerGameObj = playerGameObjs[selfJoinIndexArrIdx];
                    Debug.LogFormat("Battle ready to start, teleport camera to selfPlayer dst={0}, thread id={1}", playerGameObj.transform.position, Thread.CurrentThread.ManagedThreadId);
                    readyGoPanel.playReadyAnim(null, () => {
                        Debug.LogFormat("played ready animation, thread id={0}", Thread.CurrentThread.ManagedThreadId);
                        initSeqNo++;
                        // Mimics "shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START"
                        cachedStartRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
                        renderBuffer.Clear();
                        inputBuffer.Clear();
                        onRoomDownsyncFrame(cachedStartRdf, null);
                        pauseAllAnimatingCharacters(false);
                    });
                    initSeqNo++;
                }
            } else if (7 == initSeqNo) {
                enableBattleInput(true);
                readyGoPanel.playGoAnim();
                if (null != toast) {
                    toast.showAdvice("Defeat all enemies");
                }
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
                        if (!String.IsNullOrEmpty(justTriggeredStoryPoint.CutsceneName)) {
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

    private (int, int) elapseStoryCmd(int fromMockRdfId, int cappedMockRdfId, bool applyRendering=false, bool breakAtReadyGoTriggered=false) {
        int mockRdfId = fromMockRdfId;
        while (mockRdfId < cappedMockRdfId) {
            int delayedInputFrameId = ConvertToDelayedInputFrameId(mockRdfId);
            getOrPrefabInputFrameUpsync(delayedInputFrameId, false, prefabbedInputListHolder);
            var (ok1, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            // [WARNING] ALWAYS respect "remainingTriggerForceCtrlRdfCount" and "latestTriggerForceCtrlCmd" in "elapseStoryCmd"!
            if (0 < remainingTriggerForceCtrlRdfCount) {
                if (ok1 && null != delayedInputFrameDownsync) {
                    delayedInputFrameDownsync.InputList[0] = latestTriggerForceCtrlCmd;
                    delayedInputFrameDownsync.ConfirmedList = ((1u << roomCapacity) - 1);
                }
                --remainingTriggerForceCtrlRdfCount;
            }
            
            bool mockHasIncorrectlyPredictedRenderFrame = false;
            bool mockSelfNotEnoughMp = false;
            var (_, currRdf) = renderBuffer.GetByFrameId(mockRdfId);

            Step(inputBuffer, mockRdfId, roomCapacity, collisionSys, renderBuffer, ref overlapResult, ref primaryOverlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, residueCollided, triggerEditorIdToLocalId, triggerEditorIdToConfigFromTiled, trapLocalIdToColliderAttrs, completelyStaticTrapColliders, unconfirmedBattleResult, ref confirmedBattleResult, pushbackFrameLogBuffer, frameLogEnabled, playerRdfId, shouldDetectRealtimeRenderHistoryCorrection, out mockHasIncorrectlyPredictedRenderFrame, historyRdfHolder, missionTriggerLocalId, selfJoinIndex, joinIndexRemap, ref justTriggeredStoryPointId, ref justTriggeredBgmId, justDeadNpcIndices, out fulfilledTriggerSetMask, ref mockSelfNotEnoughMp, _loggerBridge);

            ulong forceCtrlMask = (fulfilledTriggerSetMask & triggerForceCtrlMask);
            if (0 < forceCtrlMask) {
                Debug.LogFormat("Triggered force ctrl mask={1} at start-skipping-phase, mockRdfId={0}", mockRdfId, forceCtrlMask);
                int forceCtrlTriggerLocalId = 0;
                while (0 < forceCtrlMask) {
                    forceCtrlMask >>= 1;
                    ++forceCtrlTriggerLocalId;
                }
                if (0 < forceCtrlTriggerLocalId) {
                    var trigger = currRdf.TriggersArr[forceCtrlTriggerLocalId - 1];
                    var triggerConfigFromTiled = triggerEditorIdToConfigFromTiled[trigger.EditorId];
                    remainingTriggerForceCtrlRdfCount = triggerConfigFromTiled.ForceCtrlRdfCount;
                    latestTriggerForceCtrlCmd = triggerConfigFromTiled.ForceCtrlCmd;

                    Debug.Log($"Picked remainingTriggerForceCtrlRdfCount={remainingTriggerForceCtrlRdfCount}, latestTriggerForceCtrlCmd={latestTriggerForceCtrlCmd} at start-skipping-phase, mockRdfId={mockRdfId}");
                }
            }

            var chConfig = characters[selfPlayerInfo.SpeciesId];
            int fadeOutRdfCnt = (chConfig.InertiaFramesToRecover + 4);
            bool inFadeOut = ((mockRdfId + fadeOutRdfCnt) > cappedMockRdfId);
            if (!inFadeOut && breakAtReadyGoTriggered) {
                ulong storyReadyGoTriggerMask = (1u << (storyReadyGoTriggerLocalId - 1));
                bool storyReadyGoTriggered = (0 < (fulfilledTriggerSetMask & storyReadyGoTriggerMask));
                if (storyReadyGoTriggered) {
                    Debug.Log($"Stopping 'elapseStoryCmd' in {fadeOutRdfCnt} more rdfs by storyReadyGoTriggered@remainingTriggerForceCtrlRdfCount={remainingTriggerForceCtrlRdfCount}, latestTriggerForceCtrlCmd={latestTriggerForceCtrlCmd} at start-skipping-phase, mockRdfId={mockRdfId}");
                    cappedMockRdfId = mockRdfId + fadeOutRdfCnt;
                }
            }

            var (_, nextRdf) = renderBuffer.GetByFrameId(mockRdfId + 1);
            mockRdfId++;
            if (applyRendering) {
                if (null != nextRdf) {
                    applyRoomDownsyncFrameDynamics(nextRdf, currRdf);
                    //Debug.Log($"Rendering mockRdfId={mockRdfId}, player={stringifyPlayer(nextRdf.PlayersArr[0])} by cmd={stringifyIfd(delayedInputFrameDownsync, true)}");
                    cameraTrack(nextRdf, currRdf, false);
                }
                break;
            }
        }
        return (mockRdfId, cappedMockRdfId);
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
                storySettlementPanel.SetCharacter(rdf.PlayersArr[selfJoinIndexArrIdx]);
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
