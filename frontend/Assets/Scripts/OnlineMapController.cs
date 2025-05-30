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

    CancellationTokenSource udpCancellationTokenSource;
    CancellationToken udpCancellationToken;
    // TODO: Make "slowDownIfdLagThreshold" and "freezeIfdLagThresHold" configurable from UI.
    /*
    An example for slowDownIfdLagThreshold = 1, freezeIfdLagThresHold = 3, acIfdLagThresHold = 14.

    playerRdf:             | 4,5,6,7 | 8,9,10,11 | 12,13,14,15 | 16,17,18,19 | 20   ...  | 100          
    requiredIfd:           | 1       | 2         | 3           | 4           | 5    ...  | 25
    minIfdFront:           | 1       | 1         | 1           | 1           | 1    ...  | 24
    lastAcIfdId:           | 1       | 1         | 1           | 1           | 1    ...  | 10
    slowDown:              | no      | no        | yes         | yes         | yes  ...  | no
    freeze:                | no      | no        | no          | no          | yes  ...  | yes
    */
    private const int slowDownIfdLagThreshold = ((int)(BATTLE_DYNAMICS_FPS*0.20f) >> INPUT_SCALE_FRAMES);
    /* 
    [WARNING] Lower the value of "freezeIfdLagThresHold" if you want to see more frequent freezing and verify that the graphics are continuous across the freezing point.
    */
    private const int freezeIfdLagThresHold = ((int)(BATTLE_DYNAMICS_FPS*1.5f) >> INPUT_SCALE_FRAMES);

    /*
    [WARNING] The following fields "frozenRdfCount", "frozenGracingRdfCnt", "frozenRdfCountLimit", "frozenGracePeriodRdfCount" are NOT APPLICABLE to "acIfdLag-induced-freezing", see comments around "acIfdLagThresHold" for how it's used to try avoiding "insituForceConfirmation" as much as possible.
    */
    private int frozenRdfCount = 0;
    private int frozenGracingRdfCnt = 0;
    private const int frozenRdfCountLimit = (BATTLE_DYNAMICS_FPS >> 1);
    private const int frozenGracePeriodRdfCount = (BATTLE_DYNAMICS_FPS);

    private const int acIfdLagThresHold = (DEFAULT_BACKEND_INPUT_BUFFER_SIZE/5); // "ac == all-confirmed"; when INPUT_SCALE_FRAMES == 2, 1 InputFrameDownsync maps to 4*16ms = 64ms 
    //private const int acIfdLagThresHold = (DEFAULT_BACKEND_INPUT_BUFFER_SIZE << 1); 

    /*****************************
     * The values "slowDownIfdLagThreshold" and "freezeIfdLagThresHold" are mostly used for tracking "UDP received packet front".
     * 
     * The value "acIfdLagThresHold" is used for tracking "TCP received packet front". Its value should be a little smaller than "backend inputBuffer.N", see codes for "BattleServer/Room.insituForceConfirmationEnabled" for more information, roughly speacking
        - "acIfdLagThresHold" too small will trigger frequent frontend freezing
        - "acIfdLagThresHold" too big will trigger frequent backend insituForceConfirmation
            - e.g. with "acIfdLagThresHold = (DEFAULT_BACKEND_INPUT_BUFFER_SIZE << 1)" you can see lots of "insituForceConfirmation" and "lastIfdIdOfBatch tooAdvanced" logs on backend; while with "acIfdLagThresHold = (DEFAULT_BACKEND_INPUT_BUFFER_SIZE - 4)" such logs are only seen in extreme cases (like PC#1 v.s. PC#2 via internet while PC#1's traffic is controlled by Clumsy v0.3 for various conditions)
        - the trade-off result here is to have a large "DEFAULT_BACKEND_INPUT_BUFFER_SIZE" to hold 30 seconds of inputs, then "acIfdLagThresHold" small enough relative to "DEFAULT_BACKEND_INPUT_BUFFER_SIZE" to avoid frequent insituForceConfirmation, but not too small to trigger frequent frontend freezing
     */

    public bool useFreezingLockStep = true; // [WARNING] If set to "false", expect more teleports due to "chaseRolledbackRdfs" but less frozen graphics when your device has above average network among all peers in the same battle -- yet "useFreezingLockStep" could NOT completely rule out teleports as long as potential floating point mismatch between devices exists (especially between backend .NET 7.0 and frontend .NET 2.1).
    public NetworkDoctorInfo networkInfoPanel;
    int clientAuthKey;
    bool acLagShouldLockStep = false;
    bool ifdFrontShouldLockStep = false;
    bool localTimerEnded = false;
    bool timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync = false;
    int timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync = DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD;

    public PlayerWaitingPanel playerWaitingPanel;
    public OnlineArenaCharacterSelectPanel characterSelectPanel;

    public ArenaModeSettings arenaModeSettings;

    public RejoinPrompt rejoinPrompt;
    bool startUdpTaskIfNotYet() {
        if (null != udpTask) return false;
        udpCancellationTokenSource = new CancellationTokenSource();
        udpCancellationToken = udpCancellationTokenSource.Token;
        udpTask = Task.Run(async () => {
            await UdpSessionManager.Instance.OpenUdpSession(roomCapacity, selfJoinIndex, udpCancellationToken);
        });
        return true;
    }

    void pollAndHandleWsRecvBuffer() {
        while (WsSessionManager.Instance.recvBuffer.TryDequeue(out wsRespHolder)) {
            // Debug.Log(String.Format("@playerRdfId={0}, handling wsResp in main thread: {0}", playerRdfId, wsRespHolder));
            if (ErrCode.Ok != wsRespHolder.Ret) {
                if (ErrCode.BattleStopped == wsRespHolder.Ret) {
                    Debug.LogWarning("Calling onBattleStopped with remote errCode=BattleStopped @playerRdfId=" + playerRdfId + " with battleState=" + battleState);
                    settlementRdfId = playerRdfId;
                    onBattleStopped();
                    StartCoroutine(delayToShowSettlementPanel());
                } else if (ErrCode.PlayerNotReAddableToRoom == wsRespHolder.Ret) {
                    var msg = $"@playerRdfId={playerRdfId}, received ws error PlayerNotReAddableToRoom for roomId={roomId} when roomBattleState={battleState}";
                    Debug.LogWarning(msg);
                    switch (battleState) {
                        case ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN:
                        case ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN:
                        case ROOM_STATE_FRONTEND_REJOINING:
                            autoRejoinQuota = 0; // To require manual rejoin.
                            cleanupNetworkSessions();
                            break;
                        default:
                            popupErrStackPanel(msg);
                            break;
                    }
                } else {
                    var msg = String.Format("@playerRdfId={0}, received ws error and prompting for manual rejoin: {1}", playerRdfId, wsRespHolder);
                    Debug.LogWarning(msg);
                    autoRejoinQuota = 0; // To require manual rejoin.
                    cleanupNetworkSessions();
                }
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
                    selfPlayerInfo.PlayerId = WsSessionManager.Instance.GetPlayerId();
                    roomCapacity = wsRespHolder.BciFrame.BoundRoomCapacity;
                    roomId = wsRespHolder.BciFrame.BoundRoomId;
                    frameLogEnabled = wsRespHolder.BciFrame.FrameLogEnabled;
                    clientAuthKey = wsRespHolder.BciFrame.BattleUdpTunnel.AuthKey;
                    selfPlayerInfo.JoinIndex = wsRespHolder.PeerJoinIndex;
                    selfJoinIndex = selfPlayerInfo.JoinIndex; 
                    selfJoinIndexArrIdx = selfJoinIndex - 1; 
                    selfJoinIndexMask = (1UL << selfJoinIndexArrIdx); 
                    allConfirmedMask = (1UL << roomCapacity) - 1;
                    playerWaitingPanel.InitPlayerSlots(roomCapacity);
                    resetCurrentMatch(wsRespHolder.BciFrame.StageName);
                    calcCameraCaps();
                    preallocateBattleDynamicsHolder();

                    var tempSpeciesIdList = new uint[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        tempSpeciesIdList[i] = SPECIES_NONE_CH;
                    }

                    preallocateFrontendOnlyHolders();
                    tempSpeciesIdList[selfJoinIndexArrIdx] = WsSessionManager.Instance.GetSpeciesId();
                    var (thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, battleDurationSeconds) = mockStartRdf(tempSpeciesIdList);
                    foreach (var ch in thatStartRdf.PlayersArr) {
                        if (ch.JoinIndex == selfJoinIndex) {
                            selfPlayerInfo.BulletTeamId = ch.BulletTeamId;
                            break;
                        }
                    } 

                    if (null != teamTowerHeading && 0 < mainTowersDict.Count) {
                        teamTowerHeading.Init(selfPlayerInfo.BulletTeamId, mainTowersDict);
                    }

                    battleDurationFrames = battleDurationSeconds * BATTLE_DYNAMICS_FPS;
                    renderBuffer.Put(thatStartRdf);

                    refreshColliders(thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, collisionSpaceHalfWidth, collisionSpaceHalfHeight, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out int staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId, ref triggerEditorIdToConfigFromTiled);

                    var initialPeerUdpAddrList = wsRespHolder.PeerUdpAddrList;
                    var serverHolePuncher = new WsReq {
                        PlayerId = selfPlayerInfo.PlayerId,
                        Act = UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL,
                        JoinIndex = selfJoinIndex,
                        AuthKey = clientAuthKey
                    };
                    var peerHolePuncher = new WsReq {
                        PlayerId = selfPlayerInfo.PlayerId,
                        Act = UPSYNC_MSG_ACT_HOLEPUNCH_PEER_UDP_ADDR,
                        JoinIndex = selfJoinIndex,
                        AuthKey = clientAuthKey
                    };
                    
                    UdpSessionManager.Instance.ResetUdpClient(roomCapacity, selfJoinIndex, initialPeerUdpAddrList, serverHolePuncher, peerHolePuncher, udpCancellationToken);
                    startUdpTaskIfNotYet();

                    // The following "Act=UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK" sets player battle state to PLAYER_BATTLE_STATE_ACTIVE on the backend Room. 
                    var reqData = new WsReq {
                        PlayerId = selfPlayerInfo.PlayerId,
                        Act = UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK,
                        JoinIndex = selfJoinIndex,
                        SelfParsedRdf = thatStartRdf,
                        SerializedTrapLocalIdToColliderAttrs = serializedTrapLocalIdToColliderAttrs,
                        SerializedTriggerEditorIdToLocalId = serializedTriggerEditorIdToLocalId,
                        CollisionSpaceHalfWidth = collisionSpaceHalfWidth,
                        CollisionSpaceHalfHeight = collisionSpaceHalfHeight,
                        BattleDurationSeconds = battleDurationSeconds,
                    };

                    reqData.SerializedBarrierPolygons.AddRange(serializedBarrierPolygons);
                    reqData.SerializedStaticPatrolCues.AddRange(serializedStaticPatrolCues);
                    reqData.SerializedCompletelyStaticTraps.AddRange(serializedCompletelyStaticTraps);
                    reqData.SerializedStaticTriggers.AddRange(serializedStaticTriggers);

                    WsSessionManager.Instance.senderBuffer.Add(reqData);
                    battleState = ROOM_STATE_WAITING; 
                    Debug.Log("Sent UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK, now battleState=" + battleState);

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
                    Debug.Log("Handling DOWNSYNC_MSG_ACT_BATTLE_START in main thread with battleState=" + battleState);
                    var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    if (ROOM_STATE_IN_BATTLE > battleState) {
                        // Making anim respect battleState to avoid wrong order of DOTWeen async execution  
                        readyGoPanel.playGoAnim(); 
                    } else {
                        readyGoPanel.hideReady();
                        readyGoPanel.hideGo();
                    }
                    bgmSource.Play();
                    onRoomDownsyncFrame(startRdf, null);
                    enableBattleInput(true);
                    frozenRdfCount = 0;
                    frozenGracingRdfCnt = 0;
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_STOPPED:
                    Debug.LogWarning("Calling onBattleStopped with remote act=DOWNSYNC_MSG_ACT_BATTLE_STOPPED @playerRdfId=" + playerRdfId + " with battleState=" + battleState);
                    settlementRdfId = playerRdfId;
                    onBattleStopped();
                    StartCoroutine(delayToShowSettlementPanel());
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
                    UdpSessionManager.Instance.UpdatePeerAddr(roomCapacity, selfJoinIndex, newPeerUdpAddrList);
                    /*
                     [WARNING] Deliberately trying to START "PunchAllPeers" for every participant at roughly the same time. 
                    
                    In practice, I found a weird case where P2 starts holepunching P1 much earlier than the opposite direction (e.g. when P2 joins the room later, but gets the peer udp addr of P1 earlier upon DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO), the punching for both directions would fail if the firewall(of network provider) of P1 rejected & blacklisted the early holepunching packet from P2 for a short period (e.g. 1 minute).
                     */
                    UdpSessionManager.Instance.PunchAllPeers(wsCancellationToken);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START:
                    UdpSessionManager.Instance.PunchAllPeers(wsCancellationToken);
                    var speciesIdList = new uint[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        speciesIdList[i] = wsRespHolder.Rdf.PlayersArr[i].SpeciesId;
                    }
                    var (ok2, toPatchStartRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    patchStartRdf(toPatchStartRdf, speciesIdList);
                    cameraTrack(toPatchStartRdf, null, false);
                    applyRoomDownsyncFrameDynamics(toPatchStartRdf, null);
                    var playerGameObj = playerGameObjs[selfJoinIndexArrIdx];
                    if (null != networkInfoPanel) { 
                        networkInfoPanel.gameObject.SetActive(true);
                    } 
                    playerWaitingPanel.gameObject.SetActive(false);
                    battleState = ROOM_STATE_PREPARE; 
                    Debug.LogFormat("Battle ready to start with battleState={0}, teleport camera to selfPlayer dst={1}", battleState, playerGameObj.transform.position);
                    readyGoPanel.playReadyAnim(() => { }, null);
                    break;
                case DOWNSYNC_MSG_ACT_FORCED_RESYNC:
                    // By now backend has transited the current player to "PLAYER_BATTLE_STATE_ACTIVE" regardless of initial joining or re-joining.
                    switch (battleState) {
                        case ROOM_STATE_IMPOSSIBLE:
                        case ROOM_STATE_IN_SETTLEMENT:
                        case ROOM_STATE_STOPPED:
                            Debug.LogWarning("In roomBattleState={battleState}, shouldn't handle force-resync#1@playerRdfId={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer:{inputBuffer.toSimpleStat()}");
                            return;
                        case ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN:
                        case ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN:
                            Debug.LogWarning("In roomBattleState={battleState}, shouldn't handle force-resync#2@playerRdfId={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer:{inputBuffer.toSimpleStat()}");
                            return;
                        default:
                            var pbRdf = wsRespHolder.Rdf;
                            var pbRdfId = pbRdf.Id;
                            if (null == wsRespHolder.InputFrameDownsyncBatch || 0 >= wsRespHolder.InputFrameDownsyncBatch.Count) {
                                Debug.LogWarning($"Got empty inputFrameDownsyncBatch upon resync pbRdfId={pbRdfId} @playerRdfId={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}");
                                return;
                            }
                            if (null == selfPlayerInfo) {
                                Debug.LogWarning(String.Format("Got empty selfPlayerInfo upon resync@playerRdfId(local)={0}, @lastAllConfirmedInputFrameId={1}, @chaserRenderFrameId={2}, @inputBuffer:{3}", playerRdfId, lastAllConfirmedInputFrameId, chaserRenderFrameId, inputBuffer.toSimpleStat()));
                                return;
                            }
                            //logForceResyncForChargeDebug(wsRespHolder.Rdf, wsRespHolder.InputFrameDownsyncBatch);
                            readyGoPanel.hideReady();
                            readyGoPanel.hideGo();
                            frozenRdfCount = 0;
                            frozenGracingRdfCnt = 0;
                            bool selfUnconfirmed = (0 < (pbRdf.BackendUnconfirmedMask & selfJoinIndexMask));
                            if (ROOM_STATE_FRONTEND_REJOINING == battleState) {
                                Debug.LogWarning($"Got force-resync during battleState={battleState} @pbRdfId={pbRdfId}, @chaserRenderFrameIdLowerBound={chaserRenderFrameIdLowerBound}, @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @lastUpsyncInputFrameId={lastUpsyncInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}");
                                rejoinPrompt.gameObject.SetActive(false);
                                skipInterpolation = true;
                            }
                            onRoomDownsyncFrame(wsRespHolder.Rdf, wsRespHolder.InputFrameDownsyncBatch);
                            if (pbRdfId < chaserRenderFrameIdLowerBound) {
                                Debug.LogWarning($"Got obsolete force-resync pbRdfId={pbRdfId} < chaserRenderFrameIdLowerBound={chaserRenderFrameIdLowerBound}, @battleState={battleState}, @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @lastUpsyncInputFrameId={lastUpsyncInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}: NOT SURE WHY THIS HAPPENS but calling cleanupNetworkSessions for manual rejoin");
                                autoRejoinQuota = 0;
                                cleanupNetworkSessions();
                            } else {
                                if (pbRdfId < playerRdfId && useFreezingLockStep && ROOM_STATE_FRONTEND_REJOINING == battleState && selfUnconfirmed) {
                                    int localRequiredIfdId = ConvertToDelayedInputFrameId(playerRdfId);
                                    var (tooFastOrNot, ifdLag, sendingFps, peerUpsyncFps, rollbackFrames, acLagLockedStepsCnt, ifdFrontLockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfJoinIndex, lastIndividuallyConfirmedInputFrameId, ifdLagTolerance: freezeIfdLagThresHold, disconnectedPeerJoinIndices);

                                    // [WARNING] DON'T check "acLagShouldLockStep" here because it's mostly likely to be true in a battle where both peers disconnected for a while before one reconnects
                                    ifdFrontShouldLockStep = (tooFastOrNot);
                                    
                                    if (ifdFrontShouldLockStep) {
                                        Debug.LogWarning($"Force-resync pbRdfId={pbRdfId} cannot unfreeze the current player @battleState={battleState}, @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @lastUpsyncInputFrameId={lastUpsyncInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}, disconnectedPeerJoinIndices.Count={(null != disconnectedPeerJoinIndices ? disconnectedPeerJoinIndices.Count : 0)}: calling cleanupNetworkSessions for manual rejoin");
                                        autoRejoinQuota = 0;
                                        cleanupNetworkSessions();
                                        ifdFrontShouldLockStep = false;
                                    } else {
                                        bool startedUdpTask = startUdpTaskIfNotYet();
                                        battleState = ROOM_STATE_IN_BATTLE;
                                        if (startedUdpTask) {
                                            Debug.Log($"Started new udpTask upon force-resync pbRdfId={pbRdfId} @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @lastUpsyncInputFrameId={lastUpsyncInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}: will re-punch server#1");
                                        }
                                        //Debug.LogWarning($"Per force-resync @pbRdfId={pbRdfId} is valid @battleState={battleState}, @chaserRenderFrameIdLowerBound={chaserRenderFrameIdLowerBound}, @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}: transited to ROOM_STATE_IN_BATTLE#1");
                                    }
                                } else {
                                    bool startedUdpTask = startUdpTaskIfNotYet();
                                    battleState = ROOM_STATE_IN_BATTLE;
                                    if (startedUdpTask) {
                                        Debug.Log($"Started new udpTask upon force-resync pbRdfId={pbRdfId} @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @lastUpsyncInputFrameId={lastUpsyncInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}: will re-punch server#2");
                                    }
                                    //Debug.LogWarning($"Per force-resync @pbRdfId={pbRdfId} is valid @battleState={battleState}, @chaserRenderFrameIdLowerBound={chaserRenderFrameIdLowerBound}, @playerRdfId(local)={playerRdfId}, @lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, @lastUpsyncInputFrameId={lastUpsyncInputFrameId}, @chaserRenderFrameId={chaserRenderFrameId}, @inputBuffer={inputBuffer.toSimpleStat()}: transited to ROOM_STATE_IN_BATTLE#2");
                                }
                            }
                            break;
                    }
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
        Debug.Log("OnlineMapController.onWaitingInterrupted, calling cleanupNetworkSessions");
        cleanupNetworkSessions();
    }

    public void rejoinByWs() {
        uint selfSpeciesId = WsSessionManager.Instance.GetSpeciesId();
        if (ROOM_ID_NONE == roomId) {
            Debug.Log("Early returning OnlineMapController.rejoinByWs with selectedSpeciesId={selfSpeciesId}, roomId=ROOM_ID_NONE");
            return;
        }
        Debug.Log($"Executing OnlineMapController.rejoinByWs with selectedSpeciesId={selfSpeciesId}, roomId={roomId}");
        if (ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN != battleState && ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN != battleState) {
            Debug.Log($"Early returning OnlineMapController.rejoinByWs with selectedSpeciesId={selfSpeciesId}, roomId={roomId}, battleState={battleState}");
            return;
        }

        rejoinPrompt.toggleUIInteractability(false);
        rejoinPrompt.gameObject.SetActive(true);

        tcpJamEnabled = false; // [WARNING] Upsyncing will be enabled after receiving the FORCED_RESYNC rdf which sets "battleState=ROOM_STATE_IN_BATTLE".
        battleState = ROOM_STATE_FRONTEND_REJOINING; 
        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;

        WsSessionManager.Instance.SetForReentry(true);
        WsSessionManager.Instance.SetRoomId(roomId);
        wsTask = Task.Run(async () => {
            Debug.LogWarning($"About to rejoin ws session within Task.Run(async lambda): thread id={Thread.CurrentThread.ManagedThreadId}."); // [WARNING] By design switched to another thread other than the MainThread!
            using (CancellationTokenSource dummyGuiCanceller = new CancellationTokenSource()) {
                await wsSessionTaskAsync(dummyGuiCanceller);
            }
            Debug.LogWarning($"Ends rejoined ws session within Task.Run(async lambda): thread id={Thread.CurrentThread.ManagedThreadId}.");

            // [WARNING] At the end of "wsSessionTaskAsync", we'll have a "DOWNSYNC_MSG_WS_CLOSED" message to trigger "cleanupNetworkSessions" to clean up ws session resources!
        }, wsCancellationToken)
        .ContinueWith(failedTask => {
            Debug.LogWarning($"Failed to start ws session#1: thread id={Thread.CurrentThread.ManagedThreadId}: {failedTask.Exception}"); // NOT YET in MainThread
            if (!wsCancellationTokenSource.IsCancellationRequested) {
                wsCancellationTokenSource.Cancel();
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
    

    public void onCharacterSelectGoAction(uint speciesId) {
        bool inArenaPracticeMode = WsSessionManager.Instance.getInArenaPracticeMode();
        Debug.Log($"Executing OnlineMapController.onCharacterSelectGoAction with selectedSpeciesId={speciesId}, battleState={battleState}, inArenaPracticeMode={inArenaPracticeMode}");
        if (inArenaPracticeMode) {
            selfPlayerInfo = new PlayerMetaInfo();
            roomCapacity = 2;
            selfPlayerInfo.BulletTeamId = 1;
            selfPlayerInfo.JoinIndex = 1;
            selfPlayerInfo.SpeciesId = speciesId;
            selfJoinIndex = selfPlayerInfo.JoinIndex; 
            selfJoinIndexArrIdx = selfJoinIndex - 1; 
            selfJoinIndexMask = (1UL << selfJoinIndexArrIdx); 
            allConfirmedMask = (1UL << roomCapacity) - 1;
            uint[] speciesIdList = new uint[roomCapacity];
            speciesIdList[selfJoinIndexArrIdx] = speciesId;
            speciesIdList[1] = SPECIES_BLADEGIRL;
            //resetCurrentMatch("FlatVersus");
            resetCurrentMatch("FlatVersusTraining");
            calcCameraCaps();
            preallocateBattleDynamicsHolder();
            preallocateFrontendOnlyHolders();
            preallocateSfxNodes();
            preallocatePixelVfxNodes();
            preallocateNpcNodes();
         
            var (thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, battleDurationSeconds) = mockStartRdf(speciesIdList);
            if (null != teamTowerHeading && 0 < mainTowersDict.Count) {
                teamTowerHeading.Init(selfPlayerInfo.BulletTeamId, mainTowersDict);
            }
            renderBuffer.Put(thatStartRdf);
            battleDurationFrames = battleDurationSeconds * BATTLE_DYNAMICS_FPS;
            refreshColliders(thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, (int)collisionSpaceHalfWidth, (int)collisionSpaceHalfHeight, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out int staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId, ref triggerEditorIdToConfigFromTiled);
            if (null != networkInfoPanel) {
                networkInfoPanel.gameObject.SetActive(false);
            } 
            playerWaitingPanel.gameObject.SetActive(false);
            characterSelectPanel.gameObject.SetActive(false);
            battleState = ROOM_STATE_PREPARE;
            var peerChd = thatStartRdf.PlayersArr[1];
            peerChd.BulletTeamId = 2;
            peerChd.GoalAsNpc = NpcGoal.Npatrol;
            peerChd.DirX = -2;
            patchStartRdf(thatStartRdf, speciesIdList);
            cameraTrack(thatStartRdf, null, false);
            applyRoomDownsyncFrameDynamics(thatStartRdf, null);
            readyGoPanel.playReadyAnim(() => { }, () => {
                var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                if (ROOM_STATE_IN_BATTLE > battleState) {
                    // Making anim respect battleState to avoid wrong order of DOTWeen async execution  
                    readyGoPanel.playGoAnim();
                } else {
                    readyGoPanel.hideReady();
                    readyGoPanel.hideGo();
                }
                bgmSource.Play();
                onRoomDownsyncFrame(startRdf, null);
                enableBattleInput(true);
            });
            return;
        }

        // [WARNING] Deliberately NOT declaring this method as "async" to make tests related to `<proj-root>/GOROUTINE_TO_ASYNC_TASK.md` more meaningful.
        battleState = ROOM_STATE_IDLE;

        WsSessionManager.Instance.SetSpeciesId(speciesId);

        // [WARNING] Must avoid blocking MainThread. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.
        Debug.LogWarning(String.Format("About to start ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));

        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;
        tcpJamEnabled = false;

        bool guiCanProceedOnFailure = false;

        using (var guiCanProceedSignalSource = new CancellationTokenSource()) {
            var guiCanProceedSignal = guiCanProceedSignalSource.Token;
            Task guiWaitToProceedTask = Task.Run(async () => {
                await Task.Delay(int.MaxValue, guiCanProceedSignal);
            });
            try {
                wsTask = Task.Run(async () => {
                    Debug.LogWarning(String.Format("About to start ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId)); // [WARNING] By design switched to another thread other than the MainThread!
                    await wsSessionTaskAsync(guiCanProceedSignalSource);
                    Debug.LogWarning(String.Format("Ends ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId));

                    // [WARNING] At the end of "wsSessionTaskAsync", we'll have a "DOWNSYNC_MSG_WS_CLOSED" message to trigger "onWsSessionClosed" to clean up ws session resources!
                }, wsCancellationToken)
                .ContinueWith(failedTask => {
                    Debug.LogWarning($"Failed to start ws session#1: thread id={Thread.CurrentThread.ManagedThreadId}: {failedTask.Exception}"); // [WARNING] NOT YET in MainThread
                    guiCanProceedOnFailure = true;
                    if (!wsCancellationToken.IsCancellationRequested) {
                        wsCancellationTokenSource.Cancel();
                    }
                    if (!guiCanProceedSignalSource.IsCancellationRequested) {
                        guiCanProceedSignalSource.Cancel();
                    }
                }, TaskContinuationOptions.OnlyOnFaulted);

                // [WARNING] returned to MainThread
                if (!guiCanProceedSignal.IsCancellationRequested && (TaskStatus.Canceled != guiWaitToProceedTask.Status && TaskStatus.Faulted != guiWaitToProceedTask.Status)) {
                    try {
                        guiWaitToProceedTask.Wait(guiCanProceedSignal);
                    } catch (Exception guiWaitCancelledEx) {
                        // Debug.LogWarning($"guiWaitToProceedTask was cancelled before proactive awaiting#1: thread id={Thread.CurrentThread.ManagedThreadId} a.k.a. the MainThread, ex={guiWaitCancelledEx}.");
                    }
                }

                if (false == guiCanProceedOnFailure) {
                    Debug.Log(String.Format("Started ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));
                    characterSelectPanel.gameObject.SetActive(false);
                } else {
                    var msg = String.Format("Failed to start ws session#2: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId);
                    Debug.LogWarning(msg);
                    popupErrStackPanel(msg);
                    WsSessionManager.Instance.ClearCredentials();
                    onBattleStopped();
                    showCharacterSelection();
                }
            } finally {
                try {
                    guiWaitToProceedTask.Dispose();
                } catch (Exception _) {

                }
            }
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
        Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Exception, StackTraceLogType.None);
        renderBufferSize = 50*BATTLE_DYNAMICS_FPS;
        selfPlayerInfo = new PlayerMetaInfo();
        inputFrameUpsyncDelayTolerance = TERMINATING_INPUT_FRAME_ID;
        Application.targetFrameRate = Battle.BATTLE_DYNAMICS_FPS;
        isOnlineMode = true;
        
        // [WARNING] Keep this property true if you want to use backendDynamics! Read the comments around "shapeOverlappedOtherChCnt" in "shared/Battle_dynamics.cs" for details.
        useOthersForcedDownsyncRenderFrameDict = true;
        enableBattleInput(false);

        ArenaModeSettings.SimpleDelegate onExitCallback = () => {
            Debug.LogWarning("Calling onBattleStopped with settings>exit @playerRdfId=" + playerRdfId);
            onBattleStopped(); // [WARNING] Deliberately NOT calling "pauseAllAnimatingCharacters(false)" such that "iptmgr.gameObject" remains inactive, unblocking the keyboard control to "characterSelectPanel"! 
            showCharacterSelection();
        };
        ArenaModeSettings.SimpleDelegate onCancelCallback = () => {

        };
        arenaModeSettings.SetCallbacks(onExitCallback, onCancelCallback);

        rejoinPrompt.SetCallbacks(rejoinByWs, aExitCallback: () => {
            Debug.LogWarning("Calling onBattleStopped with rejoinPrompt>exit @playerRdfId=" + playerRdfId);
            onBattleStopped(); // [WARNING] Deliberately NOT calling "pauseAllAnimatingCharacters(false)" such that "iptmgr.gameObject" remains inactive, unblocking the keyboard control to "characterSelectPanel"! 
            showCharacterSelection();
        });
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
        if (ROOM_STATE_FRONTEND_REJOINING == battleState) {
            Debug.LogWarning($"Handling onWsSessionClosed in main thread, battleState=ROOM_STATE_FRONTEND_REJOINING, roomId={roomId}, autoRejoinQuota={autoRejoinQuota}.");
        } else {
            Debug.Log($"Handling onWsSessionClosed in main thread, battleState={battleState}, roomId={roomId}, autoRejoinQuota={autoRejoinQuota}.");
        }
        // [WARNING] No need to show SettlementPanel in this case, but instead we should show something meaningful to the player if it'd be better for bug reporting.
        playerWaitingPanel.gameObject.SetActive(false);
        if (null != disconnectedPeerJoinIndices) {
            disconnectedPeerJoinIndices.Clear();
        } 
        cleanupNetworkSessions(); // Make sure that all resources are properly deallocated
        if (ROOM_ID_NONE != roomId && (ROOM_STATE_IN_BATTLE == battleState || ROOM_STATE_FRONTEND_REJOINING == battleState)) {
            if (0 < autoRejoinQuota) {
                Debug.Log($"As roomId={roomId} is not ROOM_ID_NONE and autoRejoinQuota={autoRejoinQuota} and battleState was ROOM_STATE_IN_BATTLE, transited battleState to ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN.");
                battleState = ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN;
                autoRejoinQuota--;
            } else {
                Debug.Log($"As roomId={roomId} is not ROOM_ID_NONE and autoRejoinQuota={autoRejoinQuota} and battleState was ROOM_STATE_IN_BATTLE, transited battleState to ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN.");
                battleState = ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN;
                rejoinPrompt.toggleUIInteractability(true);
                rejoinPrompt.gameObject.SetActive(true);
            }
        } else if (ROOM_STATE_FRONTEND_AWAITING_AUTO_REJOIN == battleState || ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN == battleState || ROOM_STATE_FRONTEND_REJOINING == battleState) {
            Debug.Log($"As roomId={roomId} is not ROOM_ID_NONE and autoRejoinQuota={autoRejoinQuota} and battleState was ROOM_STATE_IN_BATTLE, transited battleState to ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN.");
            battleState = ROOM_STATE_FRONTEND_AWAITING_MANUAL_REJOIN;
            rejoinPrompt.toggleUIInteractability(true);
            rejoinPrompt.gameObject.SetActive(true);
        } else {
            Debug.Log($"Stopping battle due to onWsSessionClosed and replaying from character select at battleState={battleState}.");
            onBattleStopped();
            showCharacterSelection();
        }
        Debug.Log("Handled onWsSessionClosed in main thread.");
    }

    protected override void onBattleStopped() {
        base.onBattleStopped();
        enableBattleInput(false);
        roomId = ROOM_ID_NONE;
        autoRejoinQuota = 1;
        WsSessionManager.Instance.SetForReentry(false);
        WsSessionManager.Instance.SetRoomId(ROOM_ID_NONE);
        rejoinPrompt.gameObject.SetActive(false); // After setting ROOM_ID_NONE, the rejoinPrompt becomes useless
        cleanupNetworkSessions(); // Make sure that all resources are properly deallocated

        // Reference https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
        if (frameLogEnabled) {
            wrapUpFrameLogs(renderBuffer, inputBuffer, rdfIdToActuallyUsedInput, false, pushbackFrameLogBuffer, Application.persistentDataPath, String.Format("p{0}.log", selfJoinIndex));
        }
    }

    private async Task wsSessionTaskAsync(CancellationTokenSource guiCanProceedSignalSource) {
        Debug.LogWarning(String.Format("In ws session TASK but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        await WsSessionManager.Instance.ConnectWsAsync(wsCancellationToken, wsCancellationTokenSource, guiCanProceedSignalSource);
        Debug.LogWarning(String.Format("In ws session TASK and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
    }

    private async void wsSessionActionAsync(CancellationTokenSource guiCanProceedSignalSource) {
        Debug.LogWarning(String.Format("In ws session ACTION but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        await WsSessionManager.Instance.ConnectWsAsync(wsCancellationToken, wsCancellationTokenSource, guiCanProceedSignalSource);
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
            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }

            // [WARNING] Don't poll UDP when not in battle, e.g. rejoining, to avoid "fake ifdLag=0 if accidentally polled in a large `inputFrameUpsync.InputFrameId` while `lastAllConfirmedInputFrameId` is still very small". 
            pollAndHandleUdpRecvBuffer();

            int localRequiredIfdId = ConvertToDelayedInputFrameId(playerRdfId);
            NetworkDoctor.Instance.LogLocalRequiredIfdId(localRequiredIfdId);
            /*
            if (0 <= lastAllConfirmedInputFrameId && localRequiredIfdId - lastAllConfirmedInputFrameId > ((inputFrameUpsyncDelayTolerance << 3) + (inputFrameUpsyncDelayTolerance << 2))) {
                Debug.LogFormat("@playerRdfId={0}, localRequiredIfdId={1}, lastAllConfirmedInputFrameId={2}, inputFrameUpsyncDelayTolerance={3}: unstable ws session detected, please try another battle :)", playerRdfId, localRequiredIfdId, lastAllConfirmedInputFrameId, inputFrameUpsyncDelayTolerance);
            }
            */

            if (!WsSessionManager.Instance.getInArenaPracticeMode() && useFreezingLockStep && !acLagShouldLockStep && !ifdFrontShouldLockStep) {
                var (tooFastOrNot, ifdLag, sendingFps, peerUpsyncFps, rollbackFrames, acLagLockedStepsCnt, ifdFrontLockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfJoinIndex, lastIndividuallyConfirmedInputFrameId, ifdLagTolerance: freezeIfdLagThresHold, disconnectedPeerJoinIndices);
                int acIfdLag = (localRequiredIfdId - lastAllConfirmedInputFrameId);
                if (0 > acIfdLag) acIfdLag = 0;
                if (acIfdLag < ifdLag) {
                    Debug.LogWarning($"@playerRdfId={playerRdfId}, acIfdLag={acIfdLag} < ifdLag={ifdLag}, how is this possible#1? localRequiredIfdId={localRequiredIfdId}, lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, lastIndividuallyConfirmedInputFrameId[]={String.Join(',', lastIndividuallyConfirmedInputFrameId)}");
                }
                ifdFrontShouldLockStep = (frozenRdfCount < frozenRdfCountLimit && tooFastOrNot);
                acLagShouldLockStep = (0 < ifdLag) && (acIfdLag > acIfdLagThresHold); //[WARNING] If "0 == ifdLag", the other peers are possibly all disconnected, no need to freeze

                if (null != networkInfoPanel) {
                    networkInfoPanel.SetValues(sendingFps, acIfdLag, peerUpsyncFps, ifdLag, acLagLockedStepsCnt, ifdFrontLockedStepsCnt, rollbackFrames, udpPunchedCnt);
                }
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
                    Debug.LogWarning($"Calling onBattleStopped with localTimerEnded @playerRdfId={playerRdfId}");
                    settlementRdfId = playerRdfId;
                    onBattleStopped();
                    StartCoroutine(delayToShowSettlementPanel());
                }
                return;
            }

            NetworkDoctor.Instance.LogRollbackFrames(playerRdfId > chaserRenderFrameId ? (playerRdfId - chaserRenderFrameId) : 0);
        
            if (acLagShouldLockStep || ifdFrontShouldLockStep) {
                if (acLagShouldLockStep) {
                    NetworkDoctor.Instance.LogAcLagLockedStepCnt();
                    //Debug.LogWarning($"Frozen by acLagShouldLockStep @playerRdfId={playerRdfId}");
                    frozenRdfCount = 0;
                    frozenGracingRdfCnt = 0;
                } else {
                    NetworkDoctor.Instance.LogIfdFrontLockedStepCnt();
                    //Debug.LogWarning($"Frozen by ifdFrontShouldLockStep @playerRdfId={playerRdfId}, frozenRdfCount={frozenRdfCount}/{frozenRdfCountLimit}");
                    if (frozenRdfCount < frozenRdfCountLimit && frozenRdfCount+1 >= frozenRdfCountLimit) {
                        frozenGracingRdfCnt = 0;
                    }
                    ++frozenRdfCount;
                }
                ifdFrontShouldLockStep = false;
                acLagShouldLockStep = false;
                
                return; // An early return here only stops "inputFrameIdFront" from incrementing, "int[] lastIndividuallyConfirmedInputFrameId" would keep increasing by the "pollXxx" calls above. 
            }

            if (frozenGracingRdfCnt >= frozenGracePeriodRdfCount) {
                frozenRdfCount = 0;
                frozenGracingRdfCnt = 0;
            } else if (frozenRdfCount >= frozenRdfCountLimit) {
                ++frozenGracingRdfCnt;
            }

            doUpdate();

            if (frozenRdfCount < frozenRdfCountLimit && !WsSessionManager.Instance.getInArenaPracticeMode()) {
                var (tooFastOrNot, ifdLag, sendingFps, peerUpsyncFps, rollbackFrames, acLagLockedStepsCnt, ifdFrontLockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfJoinIndex, lastIndividuallyConfirmedInputFrameId, ifdLagTolerance: slowDownIfdLagThreshold, disconnectedPeerJoinIndices);

                ifdFrontShouldLockStep = tooFastOrNot;
                int acIfdLag = (localRequiredIfdId - lastAllConfirmedInputFrameId);
                if (0 > acIfdLag) acIfdLag = 0;
                if (acIfdLag < ifdLag) {
                    Debug.LogWarning($"@playerRdfId={playerRdfId}, acIfdLag={acIfdLag} < ifdLag={ifdLag}, how is this possible#2? localRequiredIfdId={localRequiredIfdId}, lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, lastIndividuallyConfirmedInputFrameId[]={String.Join(',', lastIndividuallyConfirmedInputFrameId)}");
                }
                if (null != networkInfoPanel) {
                    networkInfoPanel.SetValues(sendingFps, acIfdLag, peerUpsyncFps, ifdLag, acLagLockedStepsCnt, ifdFrontLockedStepsCnt, rollbackFrames, udpPunchedCnt);
                }
            }

            if (playerRdfId > battleDurationFrames) {
                localTimerEnded = true;
            } else {
                readyGoPanel.setCountdown(playerRdfId, battleDurationFrames);
            }

            bool battleResultIsSet = isBattleResultSet(confirmedBattleResult);
            if (battleResultIsSet) {
                Debug.LogWarning("Calling onBattleStopped with confirmedBattleResult=" + confirmedBattleResult.ToString() + " @playerRdfId=" + playerRdfId);
                settlementRdfId = playerRdfId;
                onBattleStopped();
                StartCoroutine(delayToShowSettlementPanel());
            }
            //throw new NotImplementedException("Intended");
        } catch (Exception ex) {
            Debug.LogError($"Error during OnlineMap.Update, calling cleanupNetworkSessions for manual rejoin: {ex}");
            autoRejoinQuota = 0; // To require manual rejoin.
            cleanupNetworkSessions();
        }
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, ulong currSelfInputConfirmList, int currInputFrameId) {
        /*
        For a 2-player-battle, this "shouldUpsyncForEarlyAllConfirmedOnBackend" can be omitted, however for more players in a same battle, to avoid a "long time non-moving player" jamming the downsync of other moving players, we should use this flag.

        When backend implements the "force confirmation" feature, we can have "false == shouldUpsyncForEarlyAllConfirmedOnBackend" all the time as well!
        */
        if (0 == (selfJoinIndexMask & currSelfInputConfirmList)) return false;
        var shouldUpsyncForEarlyAllConfirmedOnBackend = (currInputFrameId - lastUpsyncInputFrameId >= inputFrameUpsyncDelayTolerance);
        return shouldUpsyncForEarlyAllConfirmedOnBackend || (prevSelfInput != currSelfInput);
    }

    protected override void sendInputFrameUpsyncBatch(int latestLocalInputFrameId, bool battleResultIsSet) {
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

        for (var i = batchInputFrameIdSt; i <= batchInputFrameIdEdClosed; i++) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(i);
            if (false == res1 || null == inputFrameDownsync) {
                Debug.LogError($"sendInputFrameUpsyncBatch: recentInputCache is NOT having i={i}, at playerRdfId={playerRdfId}, latestLocalInputFrameId={latestLocalInputFrameId}, inputBuffer:{inputBuffer.toSimpleStat()}");
            } else {
                bool selfConfirmed = (0 < (selfJoinIndexMask & (inputFrameDownsync.ConfirmedList | inputFrameDownsync.UdpConfirmedList)));
                if (battleResultIsSet || selfConfirmed) {
                    var inputFrameUpsync = new InputFrameUpsync {
                        InputFrameId = i,
                        Encoded = inputFrameDownsync.InputList[selfJoinIndexArrIdx]
                    };
                    inputFrameUpsyncBatch.Add(inputFrameUpsync);
                }
            }
        }

        if (0 >= inputFrameUpsyncBatch.Count) {
            return;
        }

        NetworkDoctor.Instance.LogSending(batchInputFrameIdSt, latestLocalInputFrameId);

        var reqData = new WsReq {
            PlayerId = selfPlayerInfo.PlayerId,
            Act = Battle.UPSYNC_MSG_ACT_PLAYER_CMD,
            JoinIndex = selfJoinIndex,
            AckingInputFrameId = lastAllConfirmedInputFrameId,
            AuthKey = clientAuthKey
        };
        reqData.InputFrameUpsyncBatch.AddRange(inputFrameUpsyncBatch);

        if (!tcpJamEnabled) {
            WsSessionManager.Instance.senderBuffer.Add(reqData);
        }
        UdpSessionManager.Instance.senderBuffer.Add(reqData);
        lastUpsyncInputFrameId = batchInputFrameIdEdClosed;
        //Debug.Log($"sendInputFrameUpsyncBatch ends at playerRdfId={playerRdfId}, lastUpsyncInputFrameId={lastUpsyncInputFrameId}, latestLocalInputFrameId={latestLocalInputFrameId}, batchInputFrameIdSt={batchInputFrameIdSt}, batchInputFrameIdEdClosed={batchInputFrameIdEdClosed}; inputBuffer={inputBuffer.toSimpleStat()}");
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
            int peerJ = (peerJoinIndex - 1);
            ulong peerJoinIndexMask = (1u << peerJ);
            getOrPrefabInputFrameUpsync(inputFrameId, false, prefabbedInputListHolder); // Make sure that inputFrame exists locally
            var (res1, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
            if (!res1 || null == existingInputFrame) {
                throw new ArgumentNullException(String.Format("inputBuffer doesn't contain inputFrameId={0} after prefabbing! Now inputBuffer StFrameId={1}, EdFrameId={2}, Cnt/N={3}/{4}", inputFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId, inputBuffer.Cnt, inputBuffer.N));
            }
            ulong existingConfirmedList = existingInputFrame.ConfirmedList;
            ulong existingUdpConfirmedList = existingInputFrame.UdpConfirmedList;
            if (0 < (existingConfirmedList & peerJoinIndexMask) || 0 < (existingUdpConfirmedList & peerJoinIndexMask)) {
                // Debug.Log(String.Format("Udp upsync inputFrameId={0} from peerJoinIndex={1} is ignored because it's already confirmed#2! lastAllConfirmedInputFrameId={2}, existingInputFrame={3}", inputFrameId, peerJoinIndex, lastAllConfirmedInputFrameId, existingInputFrame));
                continue;
            }
            if (inputFrameId > lastIndividuallyConfirmedInputFrameId[peerJ]) {
                lastIndividuallyConfirmedInputFrameId[peerJ] = inputFrameId;
                lastIndividuallyConfirmedInputList[peerJ] = peerEncodedInput;
            }
            effCnt += 1;

            bool isPeerEncodedInputUpdated = (existingInputFrame.InputList[peerJ] != peerEncodedInput);
            existingInputFrame.InputList[peerJ] = peerEncodedInput;
            existingInputFrame.UdpConfirmedList |= peerJoinIndexMask;

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
    
        However, we should still call "_handleIncorrectlyRenderedPrediction(...)" here to break rollbacks into smaller chunks, because even if not used for "inputFrameDownsync.ConfirmedList", a "UDP inputFrameUpsync" is still more accurate than the locally predicted inputs.
        */
        _handleIncorrectlyRenderedPrediction(firstPredictedYetIncorrectInputFrameId, true);
    }

    protected override void resetCurrentMatch(string theme) {
        base.resetCurrentMatch(theme);
        
        // Reset lockstep
        acLagShouldLockStep = false;
        ifdFrontShouldLockStep = false;
        frozenRdfCount = 0;
        frozenGracingRdfCnt = 0;
        localTimerEnded = false;
        timerEndedRdfDerivedFromAllConfirmedInputFrameDownsync = false;
        timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync = DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD;
        NetworkDoctor.Instance.Reset();
    }

    protected void cleanupNetworkSessions() {
        NetworkDoctor.Instance.Reset();
        // [WARNING] This method is reentrant-safe!
        if (null != wsCancellationTokenSource) {
            try {
                if (!wsCancellationTokenSource.IsCancellationRequested) {
                    //Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, cancelling ws session"));
                    wsCancellationTokenSource.Cancel();
                } else {
                    //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsCancellationTokenSource is already cancelled!"));
                }
                wsCancellationTokenSource.Dispose();
            } catch (ObjectDisposedException ex) {
                //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsCancellationTokenSource is already disposed: {0}", ex));
            } finally {
                Debug.Log("OnlineMapController.cleanupNetworkSessions, wsTask disposed");
            }
            wsCancellationTokenSource = null;
        }

        if (null != wsTask) {
            if (TaskStatus.Canceled != wsTask.Status && TaskStatus.Faulted != wsTask.Status) {
                try {
                    //Debug.Log($"OnlineMapController.cleanupNetworkSessions, about to wait for wsTask with status={wsTask.Status}");
                    var waitingSuccess = wsTask.Wait(3000);
                    //Debug.Log($"OnlineMapController.cleanupNetworkSessions, wsTask returns with waitingSuccess={waitingSuccess}, status={wsTask.Status}");
                } catch (Exception ex) {
                    //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask is already cancelled: {0}", ex));
                }
            }
            try {
                wsTask.Dispose(); // frontend of this project targets ".NET Standard 2.1", thus calling "Task.Dispose()" explicitly, reference, reference https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.dispose?view=net-7.0
            } catch (ObjectDisposedException ex) {
                //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask is already disposed: {0}", ex));
            } finally {
                Debug.Log("OnlineMapController.cleanupNetworkSessions, wsTask disposed");
            }
            wsTask = null;
        }

        if (null != udpCancellationTokenSource) {
            try {
                if (!udpCancellationTokenSource.IsCancellationRequested) {
                    //Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, cancelling udp session"));
                    udpCancellationTokenSource.Cancel();
                } else {
                    //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpCancellationTokenSource is already cancelled!"));
                }
                udpCancellationTokenSource.Dispose();
            } catch (ObjectDisposedException ex) {
                //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpCancellationTokenSource is already disposed: {0}", ex));
            } finally {
                Debug.Log("OnlineMapController.cleanupNetworkSessions, udpCancellationTokenSource disposed");
            }
            udpCancellationTokenSource = null;
        }

        if (null != udpTask) {
            UdpSessionManager.Instance.CloseUdpSession(); // Would effectively end "ReceiveAsync" if it's blocking "Receive" loop in udpTask.
            if (TaskStatus.Canceled != udpTask.Status && TaskStatus.Faulted != udpTask.Status) {
                try {
                    //Debug.Log($"OnlineMapController.cleanupNetworkSessions, about to wait for udpTask with status={udpTask.Status}");
                    var waitingSuccess = udpTask.Wait(3000);
                    //Debug.Log($"OnlineMapController.cleanupNetworkSessions, udpTask returns with waitingSuccess={waitingSuccess}, status={udpTask.Status}");
                } catch (Exception ex) {
                    //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask is already cancelled: {0}", ex));
                } finally {
                    try {
                        udpTask.Dispose(); // frontend of this project targets ".NET Standard 2.1", thus calling "Task.Dispose()" explicitly, reference, reference https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.dispose?view=net-7.0
                    } catch (ObjectDisposedException ex) {
                        //Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask is already disposed: {0}", ex));
                    } finally {
                        Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask disposed"));
                    }
                }
            }
            udpTask = null;
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
        arenaModeSettings.toggleUIInteractability(true);
    }

    protected void showCharacterSelection() {
        if (characterSelectPanel.gameObject.activeSelf) return;
        characterSelectPanel.gameObject.SetActive(true);
        characterSelectPanel.ResetSelf();
    }

    protected override IEnumerator delayToShowSettlementPanel() {
        var arenaSettlementPanel = settlementPanel as ArenaSettlementPanel;
        if (ROOM_STATE_IN_BATTLE == battleState) {
            Debug.LogWarning("Why calling delayToShowSettlementPanel during active battle? playerRdfId = " + playerRdfId + ", settlementRdfId" + settlementRdfId);
            yield return new WaitForSeconds(0);
        } else {
            battleState = ROOM_STATE_IN_SETTLEMENT;
            arenaSettlementPanel.postSettlementCallback = () => {
                showCharacterSelection();
            };
            arenaSettlementPanel.gameObject.SetActive(true);
            arenaSettlementPanel.toggleUIInteractability(true);
            var (ok, rdf) = renderBuffer.GetByFrameId(settlementRdfId - 1);
            if (ok && null != rdf) {
                arenaSettlementPanel.SetCharacters(rdf.PlayersArr);
            } else {
                Debug.LogWarning("No character info to show for settlementRdfId=" + settlementRdfId);
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
