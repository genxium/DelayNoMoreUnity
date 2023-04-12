using UnityEngine;
using System;
using shared;
using static shared.Battle;
using static shared.CharacterState;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using Google.Protobuf.Collections;

public class OnlineMapController : AbstractMapController {
    CancellationTokenSource wsCancellationTokenSource;
    CancellationToken wsCancellationToken;
    int inputFrameUpsyncDelayTolerance;
    WsResp wsRespHolder;

    private RoomDownsyncFrame mockStartRdf() {
        var playerStartingCollisionSpacePositions = new Vector[roomCapacity];
        var (defaultColliderRadius, _) = PolygonColliderCtrToVirtualGridPos(12, 0);

        var grid = this.GetComponentInChildren<Grid>();
        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "Barrier":
                    int i = 0;
                    foreach (Transform barrierChild in child) {
                        var barrierTileObj = barrierChild.gameObject.GetComponent<SuperTiled2Unity.SuperObject>();
                        var (tiledRectCx, tiledRectCy) = (barrierTileObj.m_X + barrierTileObj.m_Width * 0.5f, barrierTileObj.m_Y + barrierTileObj.m_Height * 0.5f);
                        var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        /*
                         [WARNING] 
                        
                        The "Unity World (0, 0)" is aligned with the top-left corner of the rendered "TiledMap (via SuperMap)".

                        It's noticeable that all the "Collider"s in "CollisionSpace" must be of positive coordinates to work due to the implementation details of "resolv". Thus I'm using a "Collision Space (0, 0)" aligned with the bottom-left of the rendered "TiledMap (via SuperMap)". 
                        */
                        var barrierCollider = NewRectCollider(rectCx, rectCy, barrierTileObj.m_Width, barrierTileObj.m_Height, 0, 0, 0, 0, 0, 0, null);
                        // Debug.Log(String.Format("new barrierCollider=[X: {0}, Y: {1}, Width: {2}, Height: {3}]", barrierCollider.X, barrierCollider.Y, barrierCollider.W, barrierCollider.H));
                        collisionSys.AddSingle(barrierCollider);
                        staticRectangleColliders[i++] = barrierCollider;
                    }
                    break;
                case "PlayerStartingPos":
                    int j = 0;
                    foreach (Transform playerPosChild in child) {
                        var playerPosTileObj = playerPosChild.gameObject.GetComponent<SuperTiled2Unity.SuperObject>();
                        var (playerCx, playerCy) = TiledLayerPositionToCollisionSpacePosition(playerPosTileObj.m_X, playerPosTileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        playerStartingCollisionSpacePositions[j] = new Vector(playerCx, playerCy);
                        /// Debug.Log(String.Format("new playerStartingCollisionSpacePositions[i:{0}]=[X:{1}, Y:{2}]", j, playerCx, playerCy));
                        j++;
                        if (j >= roomCapacity) break;
                    }
                    break;
                default:
                    break;
            }
        }

        var startRdf = NewPreallocatedRoomDownsyncFrame(roomCapacity, 128);
        startRdf.Id = Battle.DOWNSYNC_MSG_ACT_BATTLE_START;
        startRdf.ShouldForceResync = false;
		for (int i = 0; i < roomCapacity; i++) {
			var collisionSpacePosition = playerStartingCollisionSpacePositions[i];
			var (playerWx, playerWy) = CollisionSpacePositionToWorldPosition(collisionSpacePosition.X, collisionSpacePosition.Y, spaceOffsetX, spaceOffsetY);
			spawnPlayerNode(i+1, playerWx, playerWy);

			var characterSpeciesId = 0;
			var playerCharacter = Battle.characters[characterSpeciesId];

			var playerInRdf = startRdf.PlayersArr[i];
			var (playerVposX, playerVposY) = PolygonColliderCtrToVirtualGridPos(collisionSpacePosition.X, collisionSpacePosition.Y); // World and CollisionSpace coordinates have the same scale, just translated
			playerInRdf.JoinIndex = i+1;
			playerInRdf.VirtualGridX = playerVposX;
			playerInRdf.VirtualGridY = playerVposY;
			playerInRdf.RevivalVirtualGridX = playerVposX;
			playerInRdf.RevivalVirtualGridY = playerVposY;
			playerInRdf.Speed = playerCharacter.Speed;
			playerInRdf.ColliderRadius = (int)defaultColliderRadius;
			playerInRdf.CharacterState = InAirIdle1NoJump;
			playerInRdf.FramesToRecover = 0;
			playerInRdf.DirX = (1 == playerInRdf.JoinIndex ? 2 : -2);
			playerInRdf.DirY = 0;
			playerInRdf.VelX = 0;
			playerInRdf.VelY = 0;
			playerInRdf.InAir = true;
			playerInRdf.OnWall = false;
			playerInRdf.Hp = 100;
			playerInRdf.MaxHp = 100;
			playerInRdf.SpeciesId = characterSpeciesId;
		}

        return startRdf;
    }

    void pollAndHandleWsRecvBuffer() {
        while (WsSessionManager.Instance.recvBuffer.TryDequeue(out wsRespHolder)) {
            switch (wsRespHolder.Act) {
                case shared.Battle.DOWNSYNC_MSG_WS_CLOSED:
                    Debug.Log("Handling WsSession closed in main thread.");
                    WsSessionManager.Instance.ClearCredentials();
                    SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
                    break;
                case shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO:
                    Debug.Log("Handling UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK in main thread.");
                    inputFrameUpsyncDelayTolerance = wsRespHolder.BciFrame.InputFrameUpsyncDelayTolerance;
                    selfPlayerInfo.Id = WsSessionManager.Instance.GetPlayerId();
					roomCapacity = wsRespHolder.BciFrame.BoundRoomCapacity;
                    selfPlayerInfo.JoinIndex = wsRespHolder.PeerJoinIndex;
					_resetCurrentMatch();
                    var reqData = new WsReq {
                        PlayerId = selfPlayerInfo.Id,
                        Act = shared.Battle.UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK,
                        JoinIndex = selfPlayerInfo.JoinIndex
                    };
                    WsSessionManager.Instance.senderBuffer.Enqueue(reqData);
                    Debug.Log("Sent UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK.");
                    break;
                case shared.Battle.DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED:
					// TODO
					break;
                case shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START:
                    Debug.Log("Handling DOWNSYNC_MSG_ACT_BATTLE_START in main thread.");
                    var startRdf = mockStartRdf();
                    onRoomDownsyncFrame(startRdf, null);
                    enableBattleInput(true);
                    break;
                case shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_STOPPED:
                    enableBattleInput(false);
                    break;
				case shared.Battle.DOWNSYNC_MSG_ACT_INPUT_BATCH: 
                    // Debug.Log("Handling DOWNSYNC_MSG_ACT_INPUT_BATCH in main thread.");
					onInputFrameDownsyncBatch(wsRespHolder.InputFrameDownsyncBatch);
					break;	
                default:
                    break;
            }
        }
    }

    void Start() {
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        selfPlayerInfo = new PlayerDownsync();
        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;
        inputFrameUpsyncDelayTolerance = TERMINATING_INPUT_FRAME_ID;
        Application.targetFrameRate = 60;

        enableBattleInput(false);

        // [WARNING] Must avoid blocking MainThread. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.
        Debug.LogWarning(String.Format("About to start ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));
        Task.Run(wsSessionActionAsync);

        // _ = wsSessionTaskAsync(); // no immediate thread switch till AFTER THE FIRST AWAIT
        // wsSessionActionAsync(); // no immediate thread switch till AFTER THE FIRST AWAIT
    }

    private async Task wsSessionTaskAsync() {
        /**
         [WARNING] This method only exists for an experiment mentioned in "GOROUTINE_TO_ASYNC_TASK.md", i.e. to show that neither [c] nor [d] switches to another thread immediately for execution, DON'T use it in practice.
         */
        Debug.LogWarning(String.Format("In ws session TASK but before the async action: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        wsSessionActionAsync();
        Debug.LogWarning(String.Format("In ws session TASK and after the async action: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        await Task.Delay(1000);
        Debug.LogWarning(String.Format("In ws session TASK and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
    }

    private async void wsSessionActionAsync() {
        Debug.LogWarning(String.Format("In ws session action but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        string wsEndpoint = Env.Instance.getWsEndpoint();
        await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource);
        Debug.LogWarning(String.Format("In ws session action and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));

        var closeMsg = new WsResp {
            Ret = ErrCode.Ok,
            Act = shared.Battle.DOWNSYNC_MSG_WS_CLOSED
        };
        WsSessionManager.Instance.recvBuffer.Enqueue(closeMsg);
        Debug.LogWarning(String.Format("Enqueued DOWNSYNC_MSG_WS_CLOSED for main thread."));

        Debug.LogWarning(String.Format("ws session action is ended"));
    }

    // Update is called once per frame
    void Update() {
        try {
            pollAndHandleWsRecvBuffer();
            doUpdate();
        } catch (Exception ex) {
            Debug.LogError(String.Format("Error during OnlineMap.Update: {0}", ex));
            onBattleStopped();
        }
    }

    private void OnDestroy() {
        if (null != wsCancellationTokenSource) {
            if (null != wsCancellationToken && !wsCancellationToken.IsCancellationRequested) {
                wsCancellationTokenSource.Cancel(); // To stop the "WebSocketThread"
            }
            wsCancellationTokenSource.Dispose();
        }
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int lastUpsyncInputFrameId, int currInputFrameId) {
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
        for (var i = batchInputFrameIdSt; i <= latestLocalInputFrameId; i++) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(i);
            if (false == res1 || null == inputFrameDownsync) {
                Debug.LogError(String.Format("sendInputFrameUpsyncBatch: recentInputCache is NOT having i={0}, latestLocalInputFrameId={1}", i, latestLocalInputFrameId));
            } else {
                var inputFrameUpsync = new InputFrameUpsync {
                    InputFrameId = i,
                    Encoded = inputFrameDownsync.InputList[selfPlayerInfo.JoinIndex - 1]
                };
                inputFrameUpsyncBatch.Add(inputFrameUpsync);
            }
        }

        // console.info(`inputFrameUpsyncBatch: ${JSON.stringify(inputFrameUpsyncBatch)}`);
        var reqData = new WsReq {
            PlayerId = selfPlayerInfo.Id,
            Act = shared.Battle.UPSYNC_MSG_ACT_PLAYER_CMD,
            JoinIndex = selfPlayerInfo.JoinIndex,
            AckingInputFrameId = lastAllConfirmedInputFrameId,
        };
        reqData.InputFrameUpsyncBatch.AddRange(inputFrameUpsyncBatch);

        WsSessionManager.Instance.senderBuffer.Enqueue(reqData);
        lastUpsyncInputFrameId = latestLocalInputFrameId;
    }

    public override void _resetCurrentMatch() {
        base._resetCurrentMatch();
        if (null != wsCancellationTokenSource) {
            wsCancellationTokenSource.Dispose();
            wsCancellationTokenSource = new CancellationTokenSource();
            wsCancellationToken = wsCancellationTokenSource.Token;
        }
    }
}
