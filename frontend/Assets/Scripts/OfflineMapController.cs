using UnityEngine;
using System;
using shared;
using static shared.Battle;
using static shared.CharacterState;

public class OfflineMapController : AbstractMapController {
    protected override void sendInputFrameUpsyncBatch(int noDelayInputFrameId) {
        throw new NotImplementedException();
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int lastUpsyncInputFrameId, int currInputFrameId) {
        return false;
    }

    // Start is called before the first frame update
    void Start() {
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;
        selfPlayerInfo = new PlayerDownsync();
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
    }

    // Update is called once per frame
    void Update() {
        try {
            doUpdate();
        } catch (Exception ex) {
            Debug.LogError(String.Format("Error during OfflineMap.Update {0}", ex));
            onBattleStopped();
        }
    }

    void OnRenderObject() {
        if (debugDrawingEnabled) {
            return;
        }
        // The magic name "OnRenderObject" is the only callback I found working to draw the debug boundaries.
        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        // Set transformation matrix for drawing to
        // match our transform
        GL.MultMatrix(transform.localToWorldMatrix);

        // The anchoring quad for testing
        /*
        GL.Begin(GL.QUADS);
        GL.Vertex3(1024, -512, 0);
        GL.Vertex3(1040, -512, 0);
        GL.Vertex3(1040, -496, 0);
        GL.Vertex3(1024, -496, 0);
        GL.End();
        */
        // Draw static colliders
        foreach (var barrierCollider in staticRectangleColliders) {
            if (null == barrierCollider) {
                break;
            }
            if (null == barrierCollider.Shape) {
                throw new ArgumentNullException("barrierCollider.Shape is null when drawing staticRectangleColliders");
            }
            if (null == barrierCollider.Shape.Points) {
                throw new ArgumentNullException("barrierCollider.Shape.Points is null when drawing staticRectangleColliders");
            }
            GL.Begin(GL.LINES);
            for (int i = 0; i < 4; i++) {
                int j = i + 1;
                if (j >= 4) j -= 4;
                var (_, pi) = barrierCollider.Shape.Points.GetByOffset(i);
                var (_, pj) = barrierCollider.Shape.Points.GetByOffset(j);
                var (ix, iy) = CollisionSpacePositionToWorldPosition(barrierCollider.X + pi.X, barrierCollider.Y + pi.Y, spaceOffsetX, spaceOffsetY);
                var (jx, jy) = CollisionSpacePositionToWorldPosition(barrierCollider.X + pj.X, barrierCollider.Y + pj.Y, spaceOffsetX, spaceOffsetY);
                GL.Vertex3(ix, iy, 0);
                GL.Vertex3(jx, jy, 0);
            }
            GL.End();
        }

        // Draw dynamic colliders
        var (_, rdf) = renderBuffer.GetByFrameId(renderFrameId);
        if (null != rdf) {
            for (int k = 0; k < roomCapacity; k++) {
                var currPlayerDownsync = rdf.PlayersArr[k];
                int colliderWidth = currPlayerDownsync.ColliderRadius * 2, colliderHeight = currPlayerDownsync.ColliderRadius * 4;

                switch (currPlayerDownsync.CharacterState) {
                    case LayDown1:
                        colliderWidth = currPlayerDownsync.ColliderRadius * 4;
                        colliderHeight = currPlayerDownsync.ColliderRadius * 2;
                        break;
                    case BlownUp1:
                    case InAirIdle1NoJump:
                    case InAirIdle1ByJump:
                    case OnWall:
                        colliderWidth = currPlayerDownsync.ColliderRadius * 2;
                        colliderHeight = currPlayerDownsync.ColliderRadius * 2;
                        break;
                }
                var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currPlayerDownsync.VirtualGridX, currPlayerDownsync.VirtualGridY);
                var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
                var (colliderWorldWidth, colliderWorldHeight) = VirtualGridToPolygonColliderCtr(colliderWidth, colliderHeight);

                GL.Begin(GL.LINES);

                GL.Vertex3((wx - 0.5f * colliderWorldWidth), (wy - 0.5f * colliderWorldHeight), 0);
                GL.Vertex3((wx + 0.5f * colliderWorldWidth), (wy - 0.5f * colliderWorldHeight), 0);

                GL.Vertex3((wx + 0.5f * colliderWorldWidth), (wy - 0.5f * colliderWorldHeight), 0);
                GL.Vertex3((wx + 0.5f * colliderWorldWidth), (wy + 0.5f * colliderWorldHeight), 0);

                GL.Vertex3((wx + 0.5f * colliderWorldWidth), (wy + 0.5f * colliderWorldHeight), 0);
                GL.Vertex3((wx - 0.5f * colliderWorldWidth), (wy + 0.5f * colliderWorldHeight), 0);

                GL.Vertex3((wx - 0.5f * colliderWorldWidth), (wy + 0.5f * colliderWorldHeight), 0);
                GL.Vertex3((wx - 0.5f * colliderWorldWidth), (wy - 0.5f * colliderWorldHeight), 0);

                GL.End();
            }
        }
        GL.PopMatrix();
    }
}
