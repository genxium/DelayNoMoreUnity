using UnityEngine;
using System;
using shared;
using static shared.Battle;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using System.Collections.Concurrent;

public class OnlineMapController : AbstractMapController {
    Task wsTask, udpTask;
    CancellationTokenSource wsCancellationTokenSource;
    CancellationToken wsCancellationToken;
    WsResp wsRespHolder;
    public bool useFreezingLockStep = true; // [WARNING] If set to "false", expect more teleports due to "chaseRolledbackRdfs" but less frozen graphics when your device has above average network among all peers in the same battle -- yet "useFreezingLockStep" could NOT completely rule out teleports as long as potential floating point mismatch between devices exists (especially between backend .NET 7.0 and frontend .NET 2.1).
    public NetworkDoctorInfo networkInfoPanel;
    int clientAuthKey;
    bool shouldLockStep = false;
    bool localTimerEnded = false;
    bool timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync = false;
    int timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync = DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD;

    public PlayerWaitingPanel playerWaitingPanel;
    public OnlineArenaCharacterSelectPanel characterSelectPanel;

    public ArenaModeSettings arenaModeSettings;

    void pollAndHandleWsRecvBuffer() {
        while (WsSessionManager.Instance.recvBuffer.TryDequeue(out wsRespHolder)) {
            // Debug.Log(String.Format("@playerRdfId={0}, handling wsResp in main thread: {0}", playerRdfId, wsRespHolder));
            if (ErrCode.Ok != wsRespHolder.Ret) {
                var msg = String.Format("@playerRdfId={0}, received ws error {1}", playerRdfId, wsRespHolder);
                popupErrStackPanel(msg);
                onWsSessionClosed();
                break;
            }
            switch (wsRespHolder.Act) {
                case DOWNSYNC_MSG_WS_OPEN:
                    onWsSessionOpen();
                    break;
                case DOWNSYNC_MSG_WS_CLOSED:
                    // [WARNING] "DOWNSYNC_MSG_WS_CLOSED" is only a signal generated locally on the frontend.
                    onWsSessionClosed();
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO:
                    Debug.Log(String.Format("Handling DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO in main thread"));
                    battleDurationFrames = (int)wsRespHolder.BciFrame.BattleDurationFrames;
                    inputFrameUpsyncDelayTolerance = wsRespHolder.BciFrame.InputFrameUpsyncDelayTolerance;
                    selfPlayerInfo.Id = WsSessionManager.Instance.GetPlayerId();
                    roomCapacity = wsRespHolder.BciFrame.BoundRoomCapacity;
                    frameLogEnabled = wsRespHolder.BciFrame.FrameLogEnabled;
                    clientAuthKey = wsRespHolder.BciFrame.BattleUdpTunnel.AuthKey;
                    selfPlayerInfo.JoinIndex = wsRespHolder.PeerJoinIndex;
                    
                    playerWaitingPanel.InitPlayerSlots(roomCapacity);
                    resetCurrentMatch("ForestVersus");
                    calcCameraCaps();
                    preallocateBattleDynamicsHolder();
                    
                    var tempSpeciesIdList = new int[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        tempSpeciesIdList[i] = SPECIES_NONE_CH;
                    }
                    tempSpeciesIdList[selfPlayerInfo.JoinIndex - 1] = WsSessionManager.Instance.GetSpeciesId();
                    var (thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, battleDurationSeconds) = mockStartRdf(tempSpeciesIdList);

                    battleDurationFrames = battleDurationSeconds * BATTLE_DYNAMICS_FPS;
                    renderBuffer.Put(thatStartRdf);
                    
                    refreshColliders(thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out int staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerTrackingIdToTrapLocalId);

                    var initialPeerUdpAddrList = wsRespHolder.PeerUdpAddrList;
                    var serverHolePuncher = new WsReq {
                        PlayerId = selfPlayerInfo.Id,
                        Act = UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL,
                        JoinIndex = selfPlayerInfo.JoinIndex,
                        AuthKey = clientAuthKey
                    };
                    var peerHolePuncher = new WsReq {
                        PlayerId = selfPlayerInfo.Id,
                        Act = UPSYNC_MSG_ACT_HOLEPUNCH_PEER_UDP_ADDR,
                        JoinIndex = selfPlayerInfo.JoinIndex,
                        AuthKey = clientAuthKey
                    };
                    UdpSessionManager.Instance.ResetUdpClient(roomCapacity, selfPlayerInfo.JoinIndex, initialPeerUdpAddrList, serverHolePuncher, peerHolePuncher, wsCancellationToken);
                    udpTask = Task.Run(async () => {
                        await UdpSessionManager.Instance.OpenUdpSession(roomCapacity, selfPlayerInfo.JoinIndex, wsCancellationToken);
                    });

                    // The following "Act=UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK" sets player battle state to PLAYER_BATTLE_STATE_ACTIVE on the backend Room. 
                    var reqData = new WsReq {
                        PlayerId = selfPlayerInfo.Id,
                        Act = UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK,
                        JoinIndex = selfPlayerInfo.JoinIndex,
                        SelfParsedRdf = thatStartRdf,
                        SerializedTrapLocalIdToColliderAttrs = serializedTrapLocalIdToColliderAttrs,
                        SerializedTriggerTrackingIdToTrapLocalId = serializedTriggerTrackingIdToTrapLocalId,
                        SpaceOffsetX = spaceOffsetX,
                        SpaceOffsetY = spaceOffsetY,
                        BattleDurationSeconds = battleDurationSeconds,
                    };

                    reqData.SerializedBarrierPolygons.AddRange(serializedBarrierPolygons);
                    reqData.SerializedStaticPatrolCues.AddRange(serializedStaticPatrolCues);
                    reqData.SerializedCompletelyStaticTraps.AddRange(serializedCompletelyStaticTraps);
                    reqData.SerializedStaticTriggers.AddRange(serializedStaticTriggers);

                    WsSessionManager.Instance.senderBuffer.Add(reqData);
                    Debug.Log("Sent UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK.");

                    preallocateFrontendOnlyHolders();
                    preallocateSfxNodes();
                    preallocatePixelVfxNodes();
                    preallocateNpcNodes();

                    break;
                case DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED:
                    playerWaitingPanel.OnParticipantChange(wsRespHolder.Rdf);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_START:
                    Debug.Log("Handling DOWNSYNC_MSG_ACT_BATTLE_START in main thread.");
                    var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    readyGoPanel.playGoAnim();
                    bgmSource.Play();
                    onRoomDownsyncFrame(startRdf, null);
                    enableBattleInput(true);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_STOPPED:
                    enableBattleInput(false);
                    StartCoroutine(delayToShowSettlementPanel());
                    // Reference https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
                    if (frameLogEnabled) {
                        wrapUpFrameLogs(renderBuffer, inputBuffer, rdfIdToActuallyUsedInput, true, pushbackFrameLogBuffer, Application.persistentDataPath, String.Format("p{0}.log", selfPlayerInfo.JoinIndex));
                    }
                    break;
                case DOWNSYNC_MSG_ACT_INPUT_BATCH:
                    // Debug.Log("Handling DOWNSYNC_MSG_ACT_INPUT_BATCH in main thread.");
                    onInputFrameDownsyncBatch(wsRespHolder.InputFrameDownsyncBatch);
                    break;
                case DOWNSYNC_MSG_ACT_PEER_UDP_ADDR:
                    var newPeerUdpAddrList = wsRespHolder.PeerUdpAddrList;
                    Debug.Log(String.Format("Handling DOWNSYNC_MSG_ACT_PEER_UDP_ADDR in main thread, newPeerUdpAddrList: {0}", newPeerUdpAddrList));
                    UdpSessionManager.Instance.UpdatePeerAddr(roomCapacity, selfPlayerInfo.JoinIndex, newPeerUdpAddrList);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START:
                    /*
                     [WARNING] Deliberately trying to START "PunchAllPeers" for every participant at roughly the same time. 
                    
                    In practice, I found a weird case where P2 starts holepunching P1 much earlier than the opposite direction (e.g. when P2 joins the room later, but gets the peer udp addr of P1 earlier upon DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO), the punching for both directions would fail if the firewall(of network provider) of P1 rejected & blacklisted the early holepunching packet from P2 for a short period (e.g. 1 minute).
                     */
                    UdpSessionManager.Instance.PunchAllPeers(wsCancellationToken);

                    var speciesIdList = new int[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        speciesIdList[i] = wsRespHolder.Rdf.PlayersArr[i].SpeciesId;
                    }
                    var (ok2, toPatchStartRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    patchStartRdf(toPatchStartRdf, speciesIdList);
                    applyRoomDownsyncFrameDynamics(toPatchStartRdf, null);
                    cameraTrack(toPatchStartRdf, null, false);
                    var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
                    networkInfoPanel.gameObject.SetActive(true);
                    playerWaitingPanel.gameObject.SetActive(false);
                    Debug.Log(String.Format("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
                    readyGoPanel.playReadyAnim(() => {}, null);
                    break;
                case DOWNSYNC_MSG_ACT_FORCED_RESYNC:
                  if (null == wsRespHolder.InputFrameDownsyncBatch || 0 >= wsRespHolder.InputFrameDownsyncBatch.Count) {
                    Debug.LogWarning(String.Format("Got empty inputFrameDownsyncBatch upon resync@localRenderFrameId={0}, @lastAllConfirmedInputFrameId={1}, @chaserRenderFrameId={2}, @inputBuffer:{3}", playerRdfId, lastAllConfirmedInputFrameId, chaserRenderFrameId, inputBuffer.toSimpleStat()));
                    return;
                  }
                  if (null == selfPlayerInfo) {
                    Debug.LogWarning(String.Format("Got empty selfPlayerInfo upon resync@localRenderFrameId={0}, @lastAllConfirmedInputFrameId={1}, @chaserRenderFrameId={2}, @inputBuffer:{3}", playerRdfId, lastAllConfirmedInputFrameId, chaserRenderFrameId, inputBuffer.toSimpleStat()));
                    return;
                  }
                  Debug.Log(String.Format("Received a force-resync frame rdfId={0}, backendUnconfirmedMask={1}, selfJoinIndex={2} @localRenderFrameId={3}, @lastAllConfirmedInputFrameId={4}, @chaserRenderFrameId={5}, @renderBuffer:{6}, @inputBuffer:{7}", wsRespHolder.Rdf.Id, wsRespHolder.Rdf.BackendUnconfirmedMask, selfPlayerInfo.JoinIndex, playerRdfId, lastAllConfirmedInputFrameId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat()));
                  readyGoPanel.hideReady();
                  readyGoPanel.hideGo();
                  onRoomDownsyncFrame(wsRespHolder.Rdf, wsRespHolder.InputFrameDownsyncBatch);
                  break;
                default:
                    break;
            }
        }
    }

    void pollAndHandleUdpRecvBuffer() {
        WsReq wsReqHolder;
        while (UdpSessionManager.Instance.recvBuffer.TryDequeue(out wsReqHolder)) {
            // Debug.Log(String.Format("Handling udpSession wsReq in main thread: {0}", wsReqHolder));
            onPeerInputFrameUpsync(wsReqHolder.JoinIndex, wsReqHolder.InputFrameUpsyncBatch);
        }
    }

    public void onWaitingInterrupted() {
        Debug.Log("OnlineMapController.onWaitingInterrupted");
        cleanupNetworkSessions();
    }

    public override void onCharacterSelectGoAction(int speciesId) {
        Debug.Log(String.Format("Executing OnlineMapController.onCharacterSelectGoAction with selectedSpeciesId={0}", speciesId));
        if (ROOM_STATE_IMPOSSIBLE != battleState && ROOM_STATE_STOPPED != battleState) {
            Debug.LogWarningFormat("OnlineMapController.onCharacterSelectGoAction having invalid battleState={0}, calling `cleanupNetworkSessions > base.onBattleStopped`", battleState);
            cleanupNetworkSessions();
            base.onBattleStopped();
        }
        // [WARNING] Deliberately NOT declaring this method as "async" to make tests related to `<proj-root>/GOROUTINE_TO_ASYNC_TASK.md` more meaningful.
        battleState = ROOM_STATE_IDLE;

        WsSessionManager.Instance.SetSpeciesId(speciesId);

        // [WARNING] Must avoid blocking MainThread. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.
        Debug.LogWarning(String.Format("About to start ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));

        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;

        bool guiCanProceedOnFailure = false;
        var guiCanProceedSignalSource = new CancellationTokenSource(); // by design a new token-source for each "onCharacterSelectGoAction"
        var guiCanProceedSignal = guiCanProceedSignalSource.Token;
        var pseudoQ = new BlockingCollection<int>();
        Task guiWaitToProceedTask = Task.Run(async () => {
            await Task.Delay(int.MaxValue, guiCanProceedSignal);
        });

        try {
            wsTask = Task.Run(async () => {
                Debug.LogWarning(String.Format("About to start ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId)); // [WARNING] By design switched to another thread other than the MainThread!
                await wsSessionTaskAsync(guiCanProceedSignalSource);
                Debug.LogWarning(String.Format("Ends ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId));

                // [WARNING] At the end of "wsSessionTaskAsync", we'll have a "DOWNSYNC_MSG_WS_CLOSED" message, thus triggering "onWsSessionClosed -> cleanupNetworkSessions" to clean up other network resources!
            }, wsCancellationToken)
            .ContinueWith(failedTask => {
                Debug.LogWarning(String.Format("Failed to start ws session#1: thread id={0}.", Thread.CurrentThread.ManagedThreadId)); // [WARNING] NOT YET in MainThread
                guiCanProceedOnFailure = true;
                if (!wsCancellationToken.IsCancellationRequested) {
                    wsCancellationTokenSource.Cancel();
                }
                if (!guiCanProceedSignal.IsCancellationRequested) {
                    guiCanProceedSignalSource.Cancel();
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

            // [WARNING] returned to MainThread
            if (!guiCanProceedSignal.IsCancellationRequested && (TaskStatus.Canceled != guiWaitToProceedTask.Status && TaskStatus.Faulted != guiWaitToProceedTask.Status)) {
                try {
                    guiWaitToProceedTask.Wait(guiCanProceedSignal);
                } catch (Exception guiWaitCancelledEx) {
                    Debug.LogWarning(String.Format("guiWaitToProceedTask was cancelled before proactive awaiting#1: thread id={0} a.k.a. the MainThread, ex={1}.", Thread.CurrentThread.ManagedThreadId, guiWaitCancelledEx));
                }
            }
            
            if (!guiCanProceedOnFailure) {
                Debug.Log(String.Format("Started ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));
                characterSelectPanel.gameObject.SetActive(false);
            } else {
                var msg = String.Format("Failed to start ws session#2: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId);
                popupErrStackPanel(msg);
                WsSessionManager.Instance.ClearCredentials();
                onWsSessionClosed();
            }
        } finally {
            try {
                if (!guiCanProceedSignal.IsCancellationRequested) {
                    guiCanProceedSignalSource.Cancel();
                }
                guiWaitToProceedTask.Wait();
            } catch (Exception guiWaitEx) {
                Debug.LogWarning(String.Format("guiWaitToProceedTask was cancelled before proactive awaiting#2: thread id={0} a.k.a. the MainThread, ex={1}.", Thread.CurrentThread.ManagedThreadId, guiWaitEx));
            } finally {
                try {
                    guiWaitToProceedTask.Dispose();
                } catch (Exception guiWaitTaskDisposedEx) {
                    Debug.LogWarning(String.Format("guiWaitToProceedTask couldn't be disposed: thread id={0} a.k.a. the MainThread: {1}", Thread.CurrentThread.ManagedThreadId, guiWaitTaskDisposedEx));
                }
            }

            guiCanProceedSignalSource.Dispose();
        }

        //wsTask = Task.Run(wsSessionActionAsync(guiCanProceedSignalSource)); // This couldn't make `wsTask.Wait()` synchronous in "cleanupNetworkSessions" -- the behaviour of "async void (a.k.a. C# Action)" is different from "async Task (a.k.a. C# Task)" in a few subtleties, e.g. "an async method that returns void can't be awaited", see https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types#void-return-type for more information.

        //wsSessionActionAsync(guiCanProceedSignalSource); // [c] no immediate thread switch till AFTER THE FIRST AWAIT
        //_ = wsSessionTaskAsync(guiCanProceedSignalSource); // [d] no immediate thread switch till AFTER THE FIRST AWAIT
    }

    void Start() {
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        selfPlayerInfo = new CharacterDownsync();
        inputFrameUpsyncDelayTolerance = TERMINATING_INPUT_FRAME_ID;
        Application.targetFrameRate = Battle.BATTLE_DYNAMICS_FPS;
        isOnlineMode = true;
        // [WARNING] Keep this property true if you want to use backendDynamics! Read the comments around "shapeOverlappedOtherChCnt" in "shared/Battle_dynamics.cs" for details.
        useOthersForcedDownsyncRenderFrameDict = true;
        enableBattleInput(false);

        ArenaModeSettings.SimpleDelegate onExitCallback = () => {
            enableBattleInput(false);
            if (frameLogEnabled) {
                wrapUpFrameLogs(renderBuffer, inputBuffer, rdfIdToActuallyUsedInput, true, pushbackFrameLogBuffer, Application.persistentDataPath, String.Format("p{0}.log", selfPlayerInfo.JoinIndex));
            }
            onWsSessionClosed(); // [WARNING] Deliberately NOT calling "pauseAllAnimatingCharacters(false)" such that "iptmgr.gameObject" remains inactive, unblocking the keyboard control to "characterSelectPanel"! 
        };
        ArenaModeSettings.SimpleDelegate onCancelCallback = () => {
            
        };
        arenaModeSettings.SetCallbacks(onExitCallback, onCancelCallback);
    }

    public void onWsSessionOpen() {
        Debug.Log("Handling WsSession open in main thread.");
        playerWaitingPanel.gameObject.SetActive(true);
    }

    public void onWsSessionClosed() {
        Debug.Log("Handling WsSession closed in main thread.");
        // [WARNING] No need to show SettlementPanel in this case, but instead we should show something meaningful to the player if it'd be better for bug reporting.
        onBattleStopped();
        playerWaitingPanel.gameObject.SetActive(false);
        cleanupNetworkSessions(); // Make sure that all resources are properly deallocated
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();
        characterSelectPanel.gameObject.SetActive(true);
        characterSelectPanel.reset();
    }

    private async Task wsSessionTaskAsync(CancellationTokenSource guiCanProceedSignalSource) {
        Debug.LogWarning(String.Format("In ws session TASK but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        string wsEndpoint = Env.Instance.getWsEndpoint();
        await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource, guiCanProceedSignalSource);
        Debug.LogWarning(String.Format("In ws session TASK and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
    }

    private async void wsSessionActionAsync(CancellationTokenSource guiCanProceedSignalSource) {
        Debug.LogWarning(String.Format("In ws session ACTION but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        string wsEndpoint = Env.Instance.getWsEndpoint();
        await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource, guiCanProceedSignalSource);
        Debug.LogWarning(String.Format("In ws session ACTION and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
    }

    protected override int chaseRolledbackRdfs() {
        int nextChaserRenderFrameId = base.chaseRolledbackRdfs();
        if (nextChaserRenderFrameId == playerRdfId) { 
            if (playerRdfId >= battleDurationFrames) {
                var (rdfAllConfirmed, _) = isRdfAllConfirmed(playerRdfId, inputBuffer, roomCapacity);
                if (rdfAllConfirmed) {
                    timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync = true;
                }
            }
            NetworkDoctor.Instance.LogChasedToPlayerRdfId();
        }
        return nextChaserRenderFrameId;
    }

    // Update is called once per frame
    void Update() {
        try {
            if (ROOM_STATE_STOPPED == battleState) {
                // For proactive exit
                return;
            }

            if (ROOM_STATE_IN_SETTLEMENT == battleState) {
                // For settlement 
                return;
            }
            pollAndHandleWsRecvBuffer();
            pollAndHandleUdpRecvBuffer();
            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }

            int localRequiredIfdId = ConvertToDelayedInputFrameId(playerRdfId);
            NetworkDoctor.Instance.LogLocalRequiredIfdId(localRequiredIfdId);
            if (0 <= lastAllConfirmedInputFrameId && localRequiredIfdId - lastAllConfirmedInputFrameId > (inputFrameUpsyncDelayTolerance << 3 + inputFrameUpsyncDelayTolerance << 2)) {
                var msg = String.Format("@playerRdfId={0}, localRequiredIfdId={1}, lastAllConfirmedInputFrameId={2}, inputFrameUpsyncDelayTolerance={3}: unstable ws session detected, please try another battle :)", playerRdfId, localRequiredIfdId, lastAllConfirmedInputFrameId, inputFrameUpsyncDelayTolerance);
                popupErrStackPanel(msg);
                onWsSessionClosed();
                return;
            }

            // [WARNING] Whenever a "[type#1 forceConfirmation]" is about to occur, we want "lockstep" to prevent it as soon as possible, because "lockstep" provides better graphical consistency. 
            if (useFreezingLockStep) {
                var (tooFastOrNot, ifdLag, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, lockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfPlayerInfo.JoinIndex, lastIndividuallyConfirmedInputFrameId, ((inputFrameUpsyncDelayTolerance >> 1) << INPUT_SCALE_FRAMES), inputFrameUpsyncDelayTolerance - 1);
                shouldLockStep = tooFastOrNot;

                /*
                if (tooFastOrNot) {
                    // Will resort to lockstep instead.
                    localExtraInputDelayFrames = 0;
                } else {
                    // [WARNING] NOT guaranteed to have a better result than always keeping localExtraInputDelayFrames zero
                    localExtraInputDelayFrames = (1 < ifdLag ? 1 : 0);
                }
                */

                networkInfoPanel.SetValues(sendingFps, srvDownsyncFps, peerUpsyncFps, ifdLag, lockedStepsCnt, rollbackFrames, udpPunchedCnt);
            }

            // [WARNING] Chasing should be executed regardless of whether or not "shouldLockStep" -- in fact it's even better to chase during "shouldLockStep"!
            chaseRolledbackRdfs();
            /*
            int nextChaserRenderFrameId = chaseRolledbackRdfs();
            if (nextChaserRenderFrameId == playerRdfId) {
                var (ok, latestPlayerRdf) = renderBuffer.GetByFrameId(playerRdfId);
                if (ok && null != latestPlayerRdf) {
                    applyRoomDownsyncFrameDynamics(latestPlayerRdf, null);
                }
            }
            */
            NetworkDoctor.Instance.LogRollbackFrames(playerRdfId > chaserRenderFrameId ? (playerRdfId - chaserRenderFrameId) : 0);

            if (shouldLockStep) {
                NetworkDoctor.Instance.LogLockedStepCnt();
                shouldLockStep = false;
                return; // An early return here only stops "inputFrameIdFront" from incrementing, "int[] lastIndividuallyConfirmedInputFrameId" would keep increasing by the "pollXxx" calls above. 
            }

            if (localTimerEnded) {
                if (!timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync && 0 < timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync) {
                    // TODO: Popup some GUI hint to tell the player that we're awaiting downsync only, as the local "playerRdfId" is monotonically increasing, there's no way to rewind and change any input from here!
                    timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync -= 16; // hardcoded for now
                } else {
                    StartCoroutine(delayToShowSettlementPanel());
                }
                return;
            }
            doUpdate();

            if (!useFreezingLockStep) {
                var (tooFastOrNot, ifdLag, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, lockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfPlayerInfo.JoinIndex, lastIndividuallyConfirmedInputFrameId, ((inputFrameUpsyncDelayTolerance >> 1) << INPUT_SCALE_FRAMES), inputFrameUpsyncDelayTolerance - 1);
                shouldLockStep = tooFastOrNot;

                /*
                if (tooFastOrNot) {
                    // Will resort to lockstep instead.
                    localExtraInputDelayFrames = 0;
                } else {
                    // [WARNING] NOT guaranteed to have a better result than always keeping localExtraInputDelayFrames zero
                    localExtraInputDelayFrames = (1 < ifdLag ? 1 : 0);
                }
                */

                networkInfoPanel.SetValues(sendingFps, srvDownsyncFps, peerUpsyncFps, ifdLag, lockedStepsCnt, rollbackFrames, udpPunchedCnt);
            }

            if (playerRdfId >= battleDurationFrames) {
                localTimerEnded = true;
            } else {
                readyGoPanel.setCountdown(playerRdfId, battleDurationFrames);    
            }
            //throw new NotImplementedException("Intended");
        } catch (Exception ex) {
            var msg = String.Format("Error during OnlineMap.Update {0}", ex);
            popupErrStackPanel(msg);
            // [WARNING] No need to show SettlementPanel in this case, but instead we should show something meaningful to the player if it'd be better for bug reporting.
            onBattleStopped();
        }
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        /*
        For a 2-player-battle, this "shouldUpsyncForEarlyAllConfirmedOnBackend" can be omitted, however for more players in a same battle, to avoid a "long time non-moving player" jamming the downsync of other moving players, we should use this flag.

        When backend implements the "force confirmation" feature, we can have "false == shouldUpsyncForEarlyAllConfirmedOnBackend" all the time as well!
        */

        var shouldUpsyncForEarlyAllConfirmedOnBackend = (currInputFrameId - lastUpsyncInputFrameId >= inputFrameUpsyncDelayTolerance);
        return shouldUpsyncForEarlyAllConfirmedOnBackend || (prevSelfInput != currSelfInput);
    }

    protected override void sendInputFrameUpsyncBatch(int latestLocalInputFrameId) {
        // [WARNING] Why not just send the latest input? Because different player would have a different "latestLocalInputFrameId" of changing its last input, and that could make the server not recognizing any "all-confirmed inputFrame"!
        var inputFrameUpsyncBatch = new RepeatedField<InputFrameUpsync>();
        var batchInputFrameIdSt = lastUpsyncInputFrameId + 1;
        if (batchInputFrameIdSt < inputBuffer.StFrameId) {
            // Upon resync, "this.lastUpsyncInputFrameId" might not have been updated properly.
            batchInputFrameIdSt = inputBuffer.StFrameId;
        }

        var batchInputFrameIdEdClosed = latestLocalInputFrameId;
        if (batchInputFrameIdEdClosed >= inputBuffer.EdFrameId) {
            batchInputFrameIdEdClosed = inputBuffer.EdFrameId-1;
        }

        NetworkDoctor.Instance.LogSending(batchInputFrameIdSt, latestLocalInputFrameId);

        for (var i = batchInputFrameIdSt; i <= batchInputFrameIdEdClosed; i++) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(i);
            if (false == res1 || null == inputFrameDownsync) {
                Debug.LogError(String.Format("sendInputFrameUpsyncBatch: recentInputCache is NOT having i={0}, at playerRdfId={1}, latestLocalInputFrameId={2}, inputBuffer:{3} ", i, playerRdfId, latestLocalInputFrameId, inputBuffer.toSimpleStat()));
            } else {
                var inputFrameUpsync = new InputFrameUpsync {
                    InputFrameId = i,
                    Encoded = inputFrameDownsync.InputList[selfPlayerInfo.JoinIndex - 1]
                };
                inputFrameUpsyncBatch.Add(inputFrameUpsync);
            }
        }

        var reqData = new WsReq {
            PlayerId = selfPlayerInfo.Id,
            Act = Battle.UPSYNC_MSG_ACT_PLAYER_CMD,
            JoinIndex = selfPlayerInfo.JoinIndex,
            AckingInputFrameId = lastAllConfirmedInputFrameId,
            AuthKey = clientAuthKey
        };
        reqData.InputFrameUpsyncBatch.AddRange(inputFrameUpsyncBatch);

        WsSessionManager.Instance.senderBuffer.Add(reqData);
        UdpSessionManager.Instance.senderBuffer.Add(reqData);
        lastUpsyncInputFrameId = latestLocalInputFrameId;
    }

    protected void onPeerInputFrameUpsync(int peerJoinIndex, RepeatedField<InputFrameUpsync> batch) {
        if (null == batch) {
            return;
        }
        if (null == inputBuffer) {
            return;
        }
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }

        int effCnt = 0, batchCnt = batch.Count;
        int firstPredictedYetIncorrectInputFrameId = TERMINATING_INPUT_FRAME_ID;
        for (int k = 0; k < batchCnt; k++) {
            var inputFrameUpsync = batch[k];
            int inputFrameId = inputFrameUpsync.InputFrameId;
            ulong peerEncodedInput = inputFrameUpsync.Encoded;

            if (inputFrameId <= lastAllConfirmedInputFrameId) {
                // [WARNING] Don't reject it by "inputFrameId <= lastIndividuallyConfirmedInputFrameId[peerJoinIndex-1]", the arrival of UDP packets might not reserve their sending order!
                // Debug.Log(String.Format("Udp upsync inputFrameId={0} from peerJoinIndex={1} is ignored because it's already confirmed#1! lastAllConfirmedInputFrameId={2}", inputFrameId, peerJoinIndex, lastAllConfirmedInputFrameId));
                continue;
            }
            ulong peerJoinIndexMask = ((ulong)1 << (peerJoinIndex - 1));
            getOrPrefabInputFrameUpsync(inputFrameId, false, prefabbedInputListHolder); // Make sure that inputFrame exists locally
            var (res1, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
            if (!res1 || null == existingInputFrame) {
                throw new ArgumentNullException(String.Format("inputBuffer doesn't contain inputFrameId={0} after prefabbing! Now inputBuffer StFrameId={1}, EdFrameId={2}, Cnt/N={3}/{4}", inputFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId, inputBuffer.Cnt, inputBuffer.N));
            }
            ulong existingConfirmedList = existingInputFrame.ConfirmedList;
            if (0 < (existingConfirmedList & peerJoinIndexMask)) {
                // Debug.Log(String.Format("Udp upsync inputFrameId={0} from peerJoinIndex={1} is ignored because it's already confirmed#2! lastAllConfirmedInputFrameId={2}, existingInputFrame={3}", inputFrameId, peerJoinIndex, lastAllConfirmedInputFrameId, existingInputFrame));
                continue;
            }
            if (inputFrameId > lastIndividuallyConfirmedInputFrameId[peerJoinIndex - 1]) {
                lastIndividuallyConfirmedInputFrameId[peerJoinIndex - 1] = inputFrameId;
                lastIndividuallyConfirmedInputList[peerJoinIndex - 1] = peerEncodedInput;
            }
            effCnt += 1;

            bool isPeerEncodedInputUpdated = (existingInputFrame.InputList[peerJoinIndex - 1] != peerEncodedInput);
            existingInputFrame.InputList[peerJoinIndex - 1] = peerEncodedInput;

            int playerRdfId2 = ConvertToLastUsedRenderFrameId(inputFrameId);
            if (
              (TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId || inputFrameId < firstPredictedYetIncorrectInputFrameId) // [WARNING] Unlike "onInputFrameDownsyncBatch(...)" for TCP, here via UDP we might be traversing out-of-order "InputFrameUpsync"s
              && 
              playerRdfId2 >= chaserRenderFrameIdLowerBound // [WARNING] Such that "inputFrameId" has a meaningful impact.
              &&
              isPeerEncodedInputUpdated
            ) {
                firstPredictedYetIncorrectInputFrameId = inputFrameId;
            }
        }
        if (null != batch && 0 < batchCnt) {
            NetworkDoctor.Instance.LogPeerInputFrameUpsync(batch[0].InputFrameId, batch[batchCnt - 1].InputFrameId);
        }
        /*
        [WARNING] 

        Deliberately NOT setting "existingInputFrame.ConfirmedList = (existingConfirmedList | peerJoinIndexMask)", thus NOT helping the move of "lastAllConfirmedInputFrameId" in "_markConfirmationIfApplicable()". 

        The edge case of concern here is "type#1 forceConfirmation". Assume that there is a battle among [P_u, P_v, P_x, P_y] where [P_x] is being an "ActiveSlowerTicker", then for [P_u, P_v, P_y] there might've been some "inputFrameUpsync"s received from [P_x] by UDP peer-to-peer transmission EARLIER THAN BUT CONFLICTING WITH the "accompaniedInputFrameDownsyncBatch of type#1 forceConfirmation" -- in such case the latter should be respected -- by "conflicting", the backend actually ignores those "inputFrameUpsync"s from [P_x] by "forceConfirmation".
    
        However, we should still call "_handleIncorrectlyRenderedPrediction(...)" here to break rollbacks into smaller chunks, because even if not used for "inputFrameDownsync.ConfirmedList", a "UDP inputFrameUpsync" is still more accurate than the locally predicted inputs.
        */
        _handleIncorrectlyRenderedPrediction(firstPredictedYetIncorrectInputFrameId, true);
    }

    protected override void resetCurrentMatch(string theme) {
        base.resetCurrentMatch(theme);

        // Reset lockstep
        shouldLockStep = false;
        localTimerEnded = false;
        timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync = false;
        timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync = DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD;
        NetworkDoctor.Instance.Reset();
    }

    protected void cleanupNetworkSessions() {
        // [WARNING] This method is reentrant-safe!
        if (null != wsCancellationTokenSource) {
            try {
                if (!wsCancellationTokenSource.IsCancellationRequested) {
                    Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, cancelling ws session"));
                    wsCancellationTokenSource.Cancel();
                } else {
                    Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsCancellationTokenSource is already cancelled!"));
                }
                wsCancellationTokenSource.Dispose();
            } catch (ObjectDisposedException ex) {
                Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsCancellationTokenSource is already disposed: {0}", ex));
            }
        }

        if (null != wsTask && (TaskStatus.Canceled != wsTask.Status && TaskStatus.Faulted != wsTask.Status)) {
            try {
                wsTask.Wait();
            } catch (Exception ex) {
                Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask is already cancelled: {0}", ex));
            } finally {
                try {
                    wsTask.Dispose(); // frontend of this project targets ".NET Standard 2.1", thus calling "Task.Dispose()" explicitly, reference, reference https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.dispose?view=net-7.0
                } catch (ObjectDisposedException ex) {
                    Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask is already disposed: {0}", ex));
                } finally {
                    Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask disposed"));
                }
            }
        }

        if (null != udpTask) {
            UdpSessionManager.Instance.CloseUdpSession(); // Would effectively end "ReceiveAsync" if it's blocking "Receive" loop in udpTask.
            if (TaskStatus.Canceled != udpTask.Status && TaskStatus.Faulted != udpTask.Status) {
                try {
                    udpTask.Wait();
                } catch (Exception ex) {
                    Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask is already cancelled: {0}", ex));
                } finally {
                    try {
                        udpTask.Dispose(); // frontend of this project targets ".NET Standard 2.1", thus calling "Task.Dispose()" explicitly, reference, reference https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.dispose?view=net-7.0
                    } catch (ObjectDisposedException ex) {
                        Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask is already disposed: {0}", ex));
                    } finally {
                        Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask disposed"));
                    }
                }
            }
        }
    }

    protected void OnDestroy() {
        Debug.LogWarning(String.Format("OnlineMapController.OnDestroy#1, about to clean up network sessions"));
        cleanupNetworkSessions();  
        Debug.LogWarning(String.Format("OnlineMapController.OnDestroy#2, cleaned network sessions"));
    }

    void OnApplicationQuit() {
        Debug.LogWarning(String.Format("OnlineMapController.OnApplicationQuit"));
    }

    public override void OnSettingsClicked() {
        if (ROOM_STATE_IN_BATTLE != battleState) return;
        arenaModeSettings.gameObject.SetActive(true);
        arenaModeSettings.toggleUIInteractability(true); }

    public override void onCharacterAndLevelSelectGoAction(int speciesId, string levelName) {
        throw new NotImplementedException();
    }
}
