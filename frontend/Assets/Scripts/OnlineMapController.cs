using UnityEngine;
using System;
using shared;
using static shared.Battle;
using static shared.CharacterState;
using System.Threading;
using UnityEngine.SceneManagement;
using Google.Protobuf.Collections;

public class OnlineMapController : AbstractMapController {
    CancellationTokenSource wsCancellationTokenSource;
    CancellationToken wsCancellationToken;
    int inputFrameUpsyncDelayTolerance;
    WsResp wsRespHolder; 

    void pollAndHandleWsRecvBuffer() {
        while (WsSessionManager.Instance.recvBuffer.TryDequeue(out wsRespHolder)) {
            Debug.Log(String.Format("Handling WsSession downsync in main thread: {0}", wsRespHolder));
            switch (wsRespHolder.Act) {
            case shared.Battle.DOWNSYNC_MSG_WS_CLOSED:
                Debug.LogWarning("Handling WsSession closed in main thread.");
                WsSessionManager.Instance.ClearCredentials();
                SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
            break; 
            case shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO:
                inputFrameUpsyncDelayTolerance = wsRespHolder.BciFrame.InputFrameUpsyncDelayTolerance;
            break;
            default:
            break;
            }
        }
    }

    void Start() {
        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;
        inputFrameUpsyncDelayTolerance = TERMINATING_INPUT_FRAME_ID;
        Application.targetFrameRate = 60;
        _resetCurrentMatch();
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
                        Debug.Log(String.Format("new barrierCollider=[X: {0}, Y: {1}, Width: {2}, Height: {3}]", barrierCollider.X, barrierCollider.Y, barrierCollider.W, barrierCollider.H));
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
                        Debug.Log(String.Format("new playerStartingCollisionSpacePositions[i:{0}]=[X:{1}, Y:{2}]", j, playerCx, playerCy));
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
        var (selfPlayerWx, selfPlayerWy) = CollisionSpacePositionToWorldPosition(playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].X, playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].Y, spaceOffsetX, spaceOffsetY);
        spawnPlayerNode(0, selfPlayerWx, selfPlayerWy);

        var selfPlayerCharacterSpeciesId = 0;
        var selfPlayerCharacter = Battle.characters[selfPlayerCharacterSpeciesId];

        var selfPlayerInRdf = startRdf.PlayersArr[selfPlayerInfo.JoinIndex - 1];
        var (selfPlayerVposX, selfPlayerVposY) = PolygonColliderCtrToVirtualGridPos(playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].X, playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].Y); // World and CollisionSpace coordinates have the same scale, just translated
        selfPlayerInRdf.Id = 10;
        selfPlayerInRdf.JoinIndex = selfPlayerInfo.JoinIndex;
        selfPlayerInRdf.VirtualGridX = selfPlayerVposX;
        selfPlayerInRdf.VirtualGridY = selfPlayerVposY;
        selfPlayerInRdf.RevivalVirtualGridX = selfPlayerVposX;
        selfPlayerInRdf.RevivalVirtualGridY = selfPlayerVposY;
        selfPlayerInRdf.Speed = selfPlayerCharacter.Speed;
        selfPlayerInRdf.ColliderRadius = (int)defaultColliderRadius;
        selfPlayerInRdf.CharacterState = InAirIdle1NoJump;
        selfPlayerInRdf.FramesToRecover = 0;
        selfPlayerInRdf.DirX = 2;
        selfPlayerInRdf.DirY = 0;
        selfPlayerInRdf.VelX = 0;
        selfPlayerInRdf.VelY = 0;
        selfPlayerInRdf.InAir = true;
        selfPlayerInRdf.OnWall = false;
        selfPlayerInRdf.Hp = 100;
        selfPlayerInRdf.MaxHp = 100;
        selfPlayerInRdf.SpeciesId = 0;

        onRoomDownsyncFrame(startRdf, null);

        startWsThread();
    }

    async void startWsThread() {
        // [WARNING] Must avoid blocking MainThread

        // Declared "async" for the convenience to "await" existing async methods
        string wsEndpoint = Env.Instance.getWsEndpoint();
        await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource);
        Debug.Log(String.Format("WebSocket async task is ended"));
    }

    // Update is called once per frame
    void Update() {
        try {
            pollAndHandleWsRecvBuffer();
            doUpdate();
        } catch (Exception ex) {
            Debug.LogError(String.Format("Error during OfflineMap.doUpdate {0}", ex.Message));
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
