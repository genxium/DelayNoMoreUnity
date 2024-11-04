using UnityEngine;
using System;
using System.Collections;
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
    protected int autoRejoinQuota = 1;
    protected bool tcpJamEnabled = false;

    // [WARNING] As "udpTask" will persist in the same battle regardless of "wsSession reconnection", it should use a separate cancellation token.
    CancellationTokenSource udpCancellationTokenSource;
    CancellationToken udpCancellationToken;
    /*
    An example for slowDownIfdLagThreshold = 1, freezeIfdLagThresHold = 3, acIfdLagThresHold = 14.

    playerRdf:             | 4,5,6,7 | 8,9,10,11 | 12,13,14,15 | 16,17,18,19 | 20   ...  | 100          
    requiredIfd:           | 1       | 2         | 3           | 4           | 5    ...  | 25
    minIfdFront:           | 1       | 1         | 1           | 1           | 1    ...  | 24
    lastAcIfdId:           | 1       | 1         | 1           | 1           | 1    ...  | 10
    slowDown:              | no      | no        | yes         | yes         | yes  ...  | no
    freeze:                | no      | no        | no          | no          | yes  ...  | yes
    */
    private int slowDownIfdLagThreshold = 2;
    private int freezeIfdLagThresHold = 3;
    private int acIfdLagThresHold = 25; // "ac == all-confirmed"
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

    public RejoinPrompt rejoinPrompt;

    void pollAndHandleWsRecvBuffer() {
        while (WsSessionManager.Instance.recvBuffer.TryDequeue(out wsRespHolder)) {
            // Debug.Log(String.Format("@playerRdfId={0}, handling wsResp in main thread: {0}", playerRdfId, wsRespHolder));
            if (ErrCode.Ok != wsRespHolder.Ret) {
                if (ErrCode.PlayerNotAddableToRoom == wsRespHolder.Ret) {
                    var msg = String.Format("@playerRdfId={0}, received ws error PlayerNotAddableToRoom for roomId={1}", playerRdfId, roomId);
                    popupErrStackPanel(msg);
                } else {
                    var msg = String.Format("@playerRdfId={0}, received ws error {1}", playerRdfId, wsRespHolder);
                    popupErrStackPanel(msg);
                }
                cleanupNetworkSessions(false);
                base.onBattleStopped();
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
                    roomId = wsRespHolder.BciFrame.BoundRoomId;
                    frameLogEnabled = wsRespHolder.BciFrame.FrameLogEnabled;
                    clientAuthKey = wsRespHolder.BciFrame.BattleUdpTunnel.AuthKey;
                    selfPlayerInfo.JoinIndex = wsRespHolder.PeerJoinIndex;

                    playerWaitingPanel.InitPlayerSlots(roomCapacity);
                    resetCurrentMatch(wsRespHolder.BciFrame.StageName);
                    calcCameraCaps();
                    preallocateBattleDynamicsHolder();

                    var tempSpeciesIdList = new uint[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        tempSpeciesIdList[i] = SPECIES_NONE_CH;
                    }

                    preallocateFrontendOnlyHolders();
                    tempSpeciesIdList[selfPlayerInfo.JoinIndex - 1] = WsSessionManager.Instance.GetSpeciesId();
                    var (thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, battleDurationSeconds) = mockStartRdf(tempSpeciesIdList);

                    battleDurationFrames = battleDurationSeconds * BATTLE_DYNAMICS_FPS;
                    renderBuffer.Put(thatStartRdf);

                    refreshColliders(thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out int staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId);

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
                    UdpSessionManager.Instance.ResetUdpClient(roomCapacity, selfPlayerInfo.JoinIndex, initialPeerUdpAddrList, serverHolePuncher, peerHolePuncher, udpCancellationToken);
                    udpTask = Task.Run(async () => {
                        await UdpSessionManager.Instance.OpenUdpSession(roomCapacity, selfPlayerInfo.JoinIndex, udpCancellationToken);
                    });

                    // The following "Act=UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK" sets player battle state to PLAYER_BATTLE_STATE_ACTIVE on the backend Room. 
                    var reqData = new WsReq {
                        PlayerId = selfPlayerInfo.Id,
                        Act = UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK,
                        JoinIndex = selfPlayerInfo.JoinIndex,
                        SelfParsedRdf = thatStartRdf,
                        SerializedTrapLocalIdToColliderAttrs = serializedTrapLocalIdToColliderAttrs,
                        SerializedTriggerEditorIdToLocalId = serializedTriggerEditorIdToLocalId,
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

                    preallocateSfxNodes();
                    preallocatePixelVfxNodes();
                    preallocateNpcNodes();

                    break;
                case DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED:
                    playerWaitingPanel.OnParticipantChange(wsRespHolder.Rdf);
                    break;
                case DOWNSYNC_MSG_ACT_PLAYER_READDED_AND_ACKED:
                    Debug.Log("Handling DOWNSYNC_MSG_ACT_PLAYER_READDED_AND_ACKED in main thread for peerJoinIndex=" + wsRespHolder.PeerJoinIndex + ".");
                    disconnectedPeerJoinIndices.Remove(wsRespHolder.PeerJoinIndex);
                    break;
                case DOWNSYNC_MSG_ACT_PLAYER_DISCONNECTED:
                    Debug.Log("Handling DOWNSYNC_MSG_ACT_PLAYER_DISCONNECTED in main thread for peerJoinIndex=" + wsRespHolder.PeerJoinIndex + ".");
                    disconnectedPeerJoinIndices.Add(wsRespHolder.PeerJoinIndex);
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
                    var batch = wsRespHolder.InputFrameDownsyncBatch;
                    /*
                    if (true == WsSessionManager.Instance.GetForReentry()) {
                        Debug.LogFormat("[AFTER REENTRY] onInputFrameDownsyncBatch called for batchInputFrameIdRange [{0}, {1}]", batch[0].InputFrameId, batch[batch.Count-1].InputFrameId);
                    }
                    */
                    onInputFrameDownsyncBatch(batch);
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

                    var speciesIdList = new uint[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        speciesIdList[i] = wsRespHolder.Rdf.PlayersArr[i].SpeciesId;
                    }
                    var (ok2, toPatchStartRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    patchStartRdf(toPatchStartRdf, speciesIdList);
                    cameraTrack(toPatchStartRdf, null, false);
                    applyRoomDownsyncFrameDynamics(toPatchStartRdf, null);
                    var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
                    networkInfoPanel.gameObject.SetActive(true);
                    playerWaitingPanel.gameObject.SetActive(false);
                    Debug.Log(String.Format("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
                    readyGoPanel.playReadyAnim(() => { }, null);
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
                    readyGoPanel.hideReady();
                    readyGoPanel.hideGo();
                    onRoomDownsyncFrame(wsRespHolder.Rdf, wsRespHolder.InputFrameDownsyncBatch);
                    /*
                    if (true == WsSessionManager.Instance.GetForReentry()) {
                        Debug.LogFormat("[AFTER REENTRY] Received a force-resync frame rdfId={0}, backendUnconfirmedMask={1}, selfJoinIndex={2} @localRenderFrameId={3}, @lastAllConfirmedInputFrameId={4}, @chaserRenderFrameId={5}, @renderBuffer:{6}, @inputBuffer:{7}, @battleState={8}", wsRespHolder.Rdf.Id, wsRespHolder.Rdf.BackendUnconfirmedMask, selfPlayerInfo.JoinIndex, playerRdfId, lastAllConfirmedInputFrameId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat(), battleState);
                    }
                    */
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
        cleanupNetworkSessions(false);
    }

    public void rejoinByWs() {
        uint selfSpeciesId = WsSessionManager.Instance.GetSpeciesId();
        if (ROOM_ID_NONE == roomId) {
            Debug.Log(String.Format("Early returning OnlineMapController.rejoinByWs with selectedSpeciesId={0}, roomId=ROOM_ID_NONE", selfSpeciesId));
            return;
        }
        Debug.Log(String.Format("Executing OnlineMapController.rejoinByWs with selectedSpeciesId={0}, roomId={1}", selfSpeciesId, roomId));
        if (ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN != battleState && ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN != battleState) {
            Debug.Log(String.Format("Early returning OnlineMapController.rejoinByWs with selectedSpeciesId={0}, roomId={1}, battleState={2}", selfSpeciesId, roomId, battleState));
            return;
        }

        tcpJamEnabled = false; // [WARNING] Upsyncing will be enabled after receiving the FORCED_RESYNC rdf which sets "battleState=ROOM_STATE_IN_BATTLE".
        battleState = ROOM_STATE_FRONTEND_REJOINING; 
        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;

        bool guiCanProceedOnFailure = false;
        var guiCanProceedSignalSource = new CancellationTokenSource(); // by design a new token-source for each "rejoinByWs"
        var guiCanProceedSignal = guiCanProceedSignalSource.Token;
        var pseudoQ = new BlockingCollection<int>();
        Task guiWaitToProceedTask = Task.Run(async () => {
            await Task.Delay(int.MaxValue, guiCanProceedSignal);
        });

        try {
            WsSessionManager.Instance.SetForReentry(true);
            WsSessionManager.Instance.SetRoomId(roomId);
            wsTask = Task.Run(async () => {
                Debug.LogWarning(String.Format("About to rejoin ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId)); // [WARNING] By design switched to another thread other than the MainThread!
                await wsSessionTaskAsync(guiCanProceedSignalSource);
                Debug.LogWarning(String.Format("Ends rejoined ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId));

                // [WARNING] At the end of "wsSessionTaskAsync", we'll have a "DOWNSYNC_MSG_WS_CLOSED" message to trigger "cleanupNetworkSessions" to clean up ws session resources!
            }, wsCancellationToken)
            .ContinueWith(failedTask => {
                Debug.LogWarning(String.Format("Failed to rejoin ws session#1: thread id={0}.", Thread.CurrentThread.ManagedThreadId)); // [WARNING] NOT YET in MainThread
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
                    //Debug.LogWarning(String.Format("guiWaitToProceedTask was cancelled before proactive awaiting#1: thread id={0} a.k.a. the MainThread, ex={1}.", Thread.CurrentThread.ManagedThreadId, guiWaitCancelledEx));
                }
            }

            if (!guiCanProceedOnFailure) {
                Debug.Log(String.Format("Rejoined ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));
            } else {
                var msg = String.Format("Failed to rejoin ws session#2: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId);
                battleState = ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN; 
                rejoinPrompt.toggleUIInteractability(true);
                rejoinPrompt.gameObject.SetActive(true);
            }
        } finally {
            try {
                if (!guiCanProceedSignal.IsCancellationRequested) {
                    guiCanProceedSignalSource.Cancel();
                }
                guiWaitToProceedTask.Wait();
            } catch (Exception guiWaitEx) {
                //Debug.LogWarning(String.Format("guiWaitToProceedTask was cancelled before proactive awaiting#2: thread id={0} a.k.a. the MainThread, ex={1}.", Thread.CurrentThread.ManagedThreadId, guiWaitEx));
            } finally {
                try {
                    guiWaitToProceedTask.Dispose();
                } catch (Exception guiWaitTaskDisposedEx) {
                    Debug.LogWarning(String.Format("guiWaitToProceedTask couldn't be disposed: thread id={0} a.k.a. the MainThread: {1}", Thread.CurrentThread.ManagedThreadId, guiWaitTaskDisposedEx));
                }
            }

            guiCanProceedSignalSource.Dispose();
        }
    }

    public override void onCharacterSelectGoAction(uint speciesId) {
        Debug.Log(String.Format("Executing OnlineMapController.onCharacterSelectGoAction with selectedSpeciesId={0}", speciesId));
        if (ROOM_STATE_IMPOSSIBLE != battleState && ROOM_STATE_STOPPED != battleState) {
            Debug.LogWarningFormat("OnlineMapController.onCharacterSelectGoAction having invalid battleState={0}, calling `base.onBattleStopped`", battleState);
            base.onBattleStopped();
        }
        // [WARNING] Deliberately NOT declaring this method as "async" to make tests related to `<proj-root>/GOROUTINE_TO_ASYNC_TASK.md` more meaningful.
        battleState = ROOM_STATE_IDLE;

        WsSessionManager.Instance.SetSpeciesId(speciesId);

        // [WARNING] Must avoid blocking MainThread. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.
        Debug.LogWarning(String.Format("About to start ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));

        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;
        tcpJamEnabled = false;

        udpCancellationTokenSource = new CancellationTokenSource();
        udpCancellationToken = udpCancellationTokenSource.Token;

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

                // [WARNING] At the end of "wsSessionTaskAsync", we'll have a "DOWNSYNC_MSG_WS_CLOSED" message to trigger "cleanupNetworkSessions" to clean up ws session resources!
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
                onBattleStopped();
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
        mainCamera = Camera.main;
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
            onBattleStopped(); // [WARNING] Deliberately NOT calling "pauseAllAnimatingCharacters(false)" such that "iptmgr.gameObject" remains inactive, unblocking the keyboard control to "characterSelectPanel"! 
        };
        ArenaModeSettings.SimpleDelegate onCancelCallback = () => {

        };
        arenaModeSettings.SetCallbacks(onExitCallback, onCancelCallback);

        rejoinPrompt.SetCallbacks(rejoinByWs, onBattleStopped);
    }

    public void onWsSessionOpen() {
        Debug.Log("Handling WsSession open in main thread.");
        if (false == WsSessionManager.Instance.GetForReentry()) {
            playerWaitingPanel.gameObject.SetActive(true);
        } else {
            rejoinPrompt.gameObject.SetActive(false);
            autoRejoinQuota++; // Such that next time "onWsSessionClosed" can use "ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN" again for once.
        }
    }

    public void onWsSessionClosed() {
        Debug.Log("Handling onWsSessionClosed in main thread, battleState=" + battleState + ", roomId=" + roomId + ".");
        // [WARNING] No need to show SettlementPanel in this case, but instead we should show something meaningful to the player if it'd be better for bug reporting.
        playerWaitingPanel.gameObject.SetActive(false);
        cleanupNetworkSessions(true); // Make sure that all resources are properly deallocated
        if (ROOM_ID_NONE != roomId && ROOM_STATE_IN_BATTLE == battleState && 0 < autoRejoinQuota) {
            Debug.LogFormat("As roomId={0} is not ROOM_ID_NONE and autoRejoinQuota={1} and battleState was ROOM_STATE_IN_BATTLE, transited battleState to ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN.", roomId, autoRejoinQuota);
            battleState = ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN;
            autoRejoinQuota--;
        } else {
            Debug.LogFormat("Resetting roomId and replaying from character select.");
            roomId = ROOM_ID_NONE;
            characterSelectPanel.gameObject.SetActive(true);
            characterSelectPanel.reset();
        }
        Debug.Log("Handled onWsSessionClosed in main thread.");
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();
        roomId = ROOM_ID_NONE;
        autoRejoinQuota = 1;
        WsSessionManager.Instance.SetForReentry(false);
        WsSessionManager.Instance.SetRoomId(ROOM_ID_NONE);
        cleanupNetworkSessions(false); // Make sure that all resources are properly deallocated
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
            NetworkDoctor.Instance.LogChasedToPlayerRdfId();
        }
        return nextChaserRenderFrameId;
    }

    // Update is called once per frame
    void Update() {
        try {
            if (ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN == battleState) {
                rejoinByWs();
                return;
            }
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
            /*
            if (0 <= lastAllConfirmedInputFrameId && localRequiredIfdId - lastAllConfirmedInputFrameId > ((inputFrameUpsyncDelayTolerance << 3) + (inputFrameUpsyncDelayTolerance << 2))) {
                Debug.LogFormat("@playerRdfId={0}, localRequiredIfdId={1}, lastAllConfirmedInputFrameId={2}, inputFrameUpsyncDelayTolerance={3}: unstable ws session detected, please try another battle :)", playerRdfId, localRequiredIfdId, lastAllConfirmedInputFrameId, inputFrameUpsyncDelayTolerance);
            }
            */

            // [WARNING] Whenever a "[type#1 forceConfirmation]" is about to occur, we want "lockstep" to prevent it as soon as possible, because "lockstep" provides better graphical consistency. 
            if (useFreezingLockStep && !shouldLockStep) {
                var (tooFastOrNot, ifdLag, sendingFps, peerUpsyncFps, rollbackFrames, lockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfPlayerInfo.JoinIndex, lastIndividuallyConfirmedInputFrameId, ((inputFrameUpsyncDelayTolerance >> 1) << INPUT_SCALE_FRAMES), freezeIfdLagThresHold, disconnectedPeerJoinIndices);

                shouldLockStep = (
                    tooFastOrNot
                    ||
                    (0 < ifdLag && (localRequiredIfdId > (lastAllConfirmedInputFrameId + acIfdLagThresHold)))
                );
                
                networkInfoPanel.SetValues(sendingFps, (localRequiredIfdId > lastAllConfirmedInputFrameId ? localRequiredIfdId - lastAllConfirmedInputFrameId : 0), peerUpsyncFps, ifdLag, lockedStepsCnt, rollbackFrames, udpPunchedCnt);
            }

            // [WARNING] Chasing should be executed regardless of whether or not "shouldLockStep" -- in fact it's even better to chase during "shouldLockStep"!
            chaseRolledbackRdfs();
            if (localTimerEnded) {
                var (rdfAllConfirmed, _) = isRdfAllConfirmed(playerRdfId, inputBuffer, roomCapacity);
                if (rdfAllConfirmed) {
                    timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync = true;
                }
                if (!timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync && 0 < timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync) {
                    // TODO: Popup some GUI hint to tell the player that we're awaiting downsync only, as the local "playerRdfId" is monotonically increasing, there's no way to rewind and change any input from here!
                    timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync -= 16; // hardcoded for now
                } else {
                    StartCoroutine(delayToShowSettlementPanel());
                }
                return;
            }

            NetworkDoctor.Instance.LogRollbackFrames(playerRdfId > chaserRenderFrameId ? (playerRdfId - chaserRenderFrameId) : 0);

            if (shouldLockStep) {
                NetworkDoctor.Instance.LogLockedStepCnt();
                shouldLockStep = false;
                if (useFreezingLockStep && 0 < disconnectedPeerJoinIndices.Count) {
                    Debug.LogWarningFormat("Freezing at playerRdfId={0}, disconnectedPeerJoinIndices.Count={1}", playerRdfId, disconnectedPeerJoinIndices.Count);
                }
                return; // An early return here only stops "inputFrameIdFront" from incrementing, "int[] lastIndividuallyConfirmedInputFrameId" would keep increasing by the "pollXxx" calls above. 
            }

            doUpdate();
            {
                var (tooFastOrNot, ifdLag, sendingFps, peerUpsyncFps, rollbackFrames, lockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfPlayerInfo.JoinIndex, lastIndividuallyConfirmedInputFrameId, ((inputFrameUpsyncDelayTolerance >> 1) << INPUT_SCALE_FRAMES), slowDownIfdLagThreshold, disconnectedPeerJoinIndices);
                shouldLockStep = tooFastOrNot;

                networkInfoPanel.SetValues(sendingFps, (localRequiredIfdId > lastAllConfirmedInputFrameId ? localRequiredIfdId - lastAllConfirmedInputFrameId : 0), peerUpsyncFps, ifdLag, lockedStepsCnt, rollbackFrames, udpPunchedCnt);
            }
            if (playerRdfId > battleDurationFrames) {
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
            batchInputFrameIdEdClosed = inputBuffer.EdFrameId - 1;
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

        if (!tcpJamEnabled) {
            WsSessionManager.Instance.senderBuffer.Add(reqData);
        }
        UdpSessionManager.Instance.senderBuffer.Add(reqData);
        lastUpsyncInputFrameId = batchInputFrameIdEdClosed;
        /*
        if (true == WsSessionManager.Instance.GetForReentry()) {
            Debug.LogFormat("[AFTER REENTRY] sendInputFrameUpsyncBatch ends at playerRdfId={0}, lastUpsyncInputFrameId={1}, latestLocalInputFrameId={2}, batchInputFrameIdSt={3}, batchInputFrameIdEdClosed={4}; inputBuffer={5}", playerRdfId, lastUpsyncInputFrameId, latestLocalInputFrameId, batchInputFrameIdSt, batchInputFrameIdEdClosed, inputBuffer.toSimpleStat());
        }
        */
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

    protected void cleanupNetworkSessions(bool wsOnly) {
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

        if (!wsOnly) {
            if (null != udpCancellationTokenSource) {
                try {
                    if (!udpCancellationTokenSource.IsCancellationRequested) {
                        Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, cancelling udp session"));
                        udpCancellationTokenSource.Cancel();
                    } else {
                        Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpCancellationTokenSource is already cancelled!"));
                    }
                    udpCancellationTokenSource.Dispose();
                } catch (ObjectDisposedException ex) {
                    Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpCancellationTokenSource is already disposed: {0}", ex));
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
    }

    protected void OnDestroy() {
        Debug.LogWarning(String.Format("OnlineMapController.OnDestroy#1, about to clean up network sessions"));
        cleanupNetworkSessions(false);
        Debug.LogWarning(String.Format("OnlineMapController.OnDestroy#2, cleaned network sessions"));
    }

    void OnApplicationQuit() {
        Debug.LogWarning(String.Format("OnlineMapController.OnApplicationQuit"));
    }

    public override void OnSettingsClicked() {
        if (ROOM_STATE_IN_BATTLE != battleState) return;
        arenaModeSettings.gameObject.SetActive(true);
        arenaModeSettings.toggleUIInteractability(true);
    }

    protected override IEnumerator delayToShowSettlementPanel() {
        var arenaSettlementPanel = settlementPanel as ArenaSettlementPanel;
        if (ROOM_STATE_IN_BATTLE != battleState) {
            Debug.LogWarning("Why calling delayToShowSettlementPanel during active battle? playerRdfId = " + playerRdfId);
            yield return new WaitForSeconds(0);
        } else {
            battleState = ROOM_STATE_IN_SETTLEMENT;
            arenaSettlementPanel.postSettlementCallback = () => {
                onBattleStopped();
            };
            arenaSettlementPanel.gameObject.SetActive(true);
            arenaSettlementPanel.toggleUIInteractability(true);
            var (ok, rdf) = renderBuffer.GetByFrameId(playerRdfId - 1);
            if (ok && null != rdf) {
                arenaSettlementPanel.SetCharacters(rdf.PlayersArr);
            }
        }
    }

    public void toggleTcpJamEnabled() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            Debug.LogWarning("battleState is not active, there's no need to toggle tcpJam.");
            return;
        }
        tcpJamEnabled = !tcpJamEnabled;
        Debug.LogWarning("tcpJamEnabled = " + (tcpJamEnabled ? "enabled" : "disabled") + " at playerRdfId = " + playerRdfId);
    }
}
