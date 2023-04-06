using UnityEngine;
using System;
using shared;
using static shared.Battle;
using static shared.CharacterState;
using static WsSessionManager;
using System.Threading;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class OnlineMapController : AbstractMapController {
    CancellationTokenSource wsCancellationTokenSource; 
    CancellationToken wsCancellationToken; 

    void onWsSessionOpen(int resultCode) {
        Debug.Log("Ws session is opened");
    }

    void onWsSessionClosed(int resultCode) {
        Debug.Log("Ws session is closed");
        WsSessionManager.Instance.ClearCredentials();
        SceneManager.LoadScene("LoginScene", LoadSceneMode.Single);
    }

    void Start() {
        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token; 
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

    void startWsThread() {
        // [WARNING] Must be a new thread here to avoid blocking UIThread
        new Thread(async () => {
            // Declared "async" for the convenience to "await" existing async methods
            string wsEndpoint = Env.Instance.getWsEndpoint();
            await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource, onWsSessionOpen, onWsSessionClosed);
            Debug.Log(String.Format("WebSocket thread is ended"));
        }).Start();
    }

    // Update is called once per frame
    void Update() {
        try {
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
}
