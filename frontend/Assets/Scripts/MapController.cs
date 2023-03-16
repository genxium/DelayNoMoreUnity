using UnityEngine;
using System;
using shared;
using static shared.Battle;

public class MapController : MonoBehaviour {
    public const int BATTLE_STATE_NONE = -1;
    public const int BATTLE_STATE_WAITING = 0;
    public const int BATTLE_STATE_IN_BATTLE = 1;
    public const int BATTLE_STATE_IN_SETTLEMENT = 2;
    public const int BATTLE_STATE_IN_DISMISSAL = 3;

    public const int MAGIC_ROOM_DOWNSYNC_FRAME_ID_BATTLE_READY_TO_START = -1;
    public const int MAGIC_ROOM_DOWNSYNC_FRAME_ID_BATTLE_START = 0;

    int roomCapacity = 1;
    int renderFrameId; // After battle started
    int renderFrameIdLagTolerance;
    int chaserRenderFrameId; // at any moment, "chaserRenderFrameId <= renderFrameId", but "chaserRenderFrameId" would fluctuate according to "onInputFrameDownsyncBatch"
    int maxChasingRenderFramesPerUpdate;
    int renderBufferSize;
    public GameObject characterPrefab;

    int[] lastIndividuallyConfirmedInputFrameId;
    ulong[] lastIndividuallyConfirmedInputList;
    PlayerDownsync selfPlayerInfo = null;
    FrameRingBuffer<RoomDownsyncFrame> renderBuffer = null;
    FrameRingBuffer<InputFrameDownsync> inputBuffer = null;

    ulong[] prefabbedInputListHolder;
    GameObject[] playerGameObjs;

    int battleState;
    int spaceOffsetX;
    int spaceOffsetY;

    shared.Collision collisionHolder;
    SatResult overlapResult;
    Vector[] effPushbacks;
    Vector[][] hardPushbackNormsArr;
    bool[] jumpedOrNotList;
    shared.Collider[] dynamicRectangleColliders;
    InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    CollisionSpace collisionSys;

    // Start is called before the first frame update
    void Start() {
        _resetCurrentMatch();
        var playerStartingCollisionSpacePositions = new Vector[roomCapacity];
        double defaultColliderRadius = 12.0;

        var grid = this.GetComponentInChildren<Grid>();
        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "Barrier":
                    foreach (Transform barrierChild in child) {
                        var barrierTileObj = barrierChild.gameObject.GetComponent<SuperTiled2Unity.SuperObject>();
                        var (tiledRectCx, tiledRectCy) = (barrierTileObj.m_X + barrierTileObj.m_Width * 0.5f, barrierTileObj.m_Y + barrierTileObj.m_Height * 0.5f);
                        var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        // [WARNING] The "Unity World (0, 0)" is aligned with the top-left corner of the rendered "TiledMap (via SuperMap)", to make it easy for me on debugging in collision space, I'm still using a "Collision Space (0, 0)" aligned with the center of the rendered "TiledMap (via SuperMap)" as the CocosCreator version.
                        var barrierCollider = GenerateRectCollider(rectCx, rectCy, barrierTileObj.m_Width, barrierTileObj.m_Height, 0, 0, 0, 0, 0, 0, null);
                        Debug.Log(String.Format("new barrierCollider=[X:{0}, Y:{1}, Width: {2}, Height: {3}]", barrierCollider.X, barrierCollider.Y, barrierCollider.W, barrierCollider.H));
                        collisionSys.AddSingle(barrierCollider);
                    }
                    break;
                case "PlayerStartingPos":
                    int i = 0;
                    foreach (Transform playerPosChild in child) {
                        var playerPosTileObj = playerPosChild.gameObject.GetComponent<SuperTiled2Unity.SuperObject>();
                        var (playerCx, playerCy) = TiledLayerPositionToCollisionSpacePosition(playerPosTileObj.m_X, playerPosTileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        playerStartingCollisionSpacePositions[i] = new Vector(playerCx, playerCy);
                        Debug.Log(String.Format("new playerStartingCollisionSpacePositions[i:{0}]=[X:{1}, Y:{2}]", i, playerCx, playerCy));
                        i++;
                        if (i >= roomCapacity) break;
                    }
                    break;
                default:
                    break;
            }
        }

        var camOldPos = Camera.main.transform.position;

        var startRdf = NewPreallocatedRoomDownsyncFrame(roomCapacity, 64, 64);
        startRdf.Id = MAGIC_ROOM_DOWNSYNC_FRAME_ID_BATTLE_START;
        startRdf.ShouldForceResync = false;
        var (selfPlayerWx, selfPlayerWy) = CollisionSpacePositionToWorldPosition(playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].X, playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].Y, spaceOffsetX, spaceOffsetY);
        spawnPlayerNode(0, selfPlayerWx, selfPlayerWy);
        Camera.main.transform.position = new Vector3(selfPlayerWx, selfPlayerWy, camOldPos.z);
        var selfPlayerInRdf = startRdf.PlayersArr[selfPlayerInfo.JoinIndex - 1];
        var (selfPlayerVposX, selfPlayerVposY) = WorldToVirtualGridPos(playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].X, playerStartingCollisionSpacePositions[selfPlayerInfo.JoinIndex - 1].Y); // World and CollisionSpace coordinates have the same scale, just translated
        selfPlayerInRdf.Id = 10;
        selfPlayerInRdf.JoinIndex = selfPlayerInfo.JoinIndex;
        selfPlayerInRdf.VirtualGridX = selfPlayerVposX;
        selfPlayerInRdf.VirtualGridY = selfPlayerVposY;
        selfPlayerInRdf.RevivalVirtualGridX = selfPlayerVposX;
        selfPlayerInRdf.RevivalVirtualGridY = selfPlayerVposY;
        selfPlayerInRdf.Speed = 10;
        selfPlayerInRdf.ColliderRadius = (int)defaultColliderRadius;
        selfPlayerInRdf.CharacterState = Battle.ATK_CHARACTER_STATE_INAIR_IDLE1_NO_JUMP;
        selfPlayerInRdf.FramesToRecover = 0;
        selfPlayerInRdf.DirX = 2;
        selfPlayerInRdf.DirY = 0;
        selfPlayerInRdf.VelX = 0;
        selfPlayerInRdf.VelY = 0;
        selfPlayerInRdf.InAir = true;
        selfPlayerInRdf.OnWall = false;
        selfPlayerInRdf.Hp = 100;
        selfPlayerInRdf.MaxHp = 100;

        onRoomDownsyncFrame(startRdf, null);
    }

    // Update is called once per frame
    void Update() {
        if (BATTLE_STATE_IN_BATTLE != battleState) {
            return;
        }
        int noDelayInputFrameId = ConvertToNoDelayInputFrameId(renderFrameId);
        ulong prevSelfInput = 0, currSelfInput = 0;
        if (ShouldGenerateInputFrameUpsync(renderFrameId)) {
            (prevSelfInput, currSelfInput) = getOrPrefabInputFrameUpsync(noDelayInputFrameId, true, prefabbedInputListHolder);
        }
        int delayedInputFrameId = ConvertToDelayedInputFrameId(renderFrameId);
        var (delayedInputFrameExists, _) = inputBuffer.GetByFrameId(delayedInputFrameId);
        if (!delayedInputFrameExists) {
            // Possible edge case after resync, kindly note that it's OK to prefab a "future inputFrame" here, because "sendInputFrameUpsyncBatch" would be capped by "noDelayInputFrameId from self.renderFrameId". 
            getOrPrefabInputFrameUpsync(delayedInputFrameId, false, prefabbedInputListHolder);
        }

        int prevChaserRenderFrameId = chaserRenderFrameId;
        int nextChaserRenderFrameId = (prevChaserRenderFrameId + maxChasingRenderFramesPerUpdate);
        if (nextChaserRenderFrameId > renderFrameId) {
            nextChaserRenderFrameId = renderFrameId;
        }
        if (prevChaserRenderFrameId < nextChaserRenderFrameId) {
            // Do not execute "rollbackAndChase" when "prevChaserRenderFrameId == nextChaserRenderFrameId", otherwise if "nextChaserRenderFrameId == self.renderFrameId" we'd be wasting computing power once. 
            rollbackAndChase(prevChaserRenderFrameId, nextChaserRenderFrameId, collisionSys, true);
        }

        // Inside the following "rollbackAndChase" actually ROLLS FORWARD w.r.t. the corresponding delayedInputFrame, REGARDLESS OF whether or not "chaserRenderFrameId == renderFrameId" now. 
        var (prevRdf, rdf) = rollbackAndChase(renderFrameId, renderFrameId + 1, collisionSys, false);
        // Having "prevRdf.Id == renderFrameId" & "rdf.Id == renderFrameId+1" 

        applyRoomDownsyncFrameDynamics(rdf, prevRdf);
        showDebugBoundaries(rdf);
        ++renderFrameId;
    }

    void spawnPlayerNode(int joinIndex, float wx, float wy) {
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(wx, wy, 0), Quaternion.identity);
        playerGameObjs[joinIndex] = newPlayerNode;
    }

    (ulong, ulong) getOrPrefabInputFrameUpsync(int inputFrameId, bool canConfirmSelf, ulong[] prefabbedInputList) {
        if (null == selfPlayerInfo) {
            String msg = String.Format("noDelayInputFrameId={0:D} couldn't be generated due to selfPlayerInfo being null", inputFrameId);
            throw new ArgumentException(msg);
        }

        ulong previousSelfInput = 0,
          currSelfInput = 0;
        int joinIndex = selfPlayerInfo.JoinIndex;
        ulong selfJoinIndexMask = ((ulong)1 << (joinIndex - 1));
        var (_, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
        var (_, previousInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId - 1);
        previousSelfInput = (null == previousInputFrameDownsync ? 0 : previousInputFrameDownsync.InputList[joinIndex - 1]);
        if (
          null != existingInputFrame
          &&
          (true != canConfirmSelf)
        ) {
            return (previousSelfInput, existingInputFrame.InputList[joinIndex - 1]);
        }

        Array.Fill<ulong>(prefabbedInputList, 0);
        for (int k = 0; k < roomCapacity; ++k) {
            if (null != existingInputFrame) {
                // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here, we just have to assign "prefabbedInputList[(joinIndex-1)]" specifically and copy all others
                prefabbedInputList[k] = existingInputFrame.InputList[k];
            }
            else if (lastIndividuallyConfirmedInputFrameId[k] <= inputFrameId) {
                prefabbedInputList[k] = lastIndividuallyConfirmedInputList[k];
                // Don't predict "btnA & btnB"!
                prefabbedInputList[k] = (prefabbedInputList[k] & 15);
            }
            else if (null != previousInputFrameDownsync) {
                // When "self.lastIndividuallyConfirmedInputFrameId[k] > inputFrameId", don't use it to predict a historical input!
                prefabbedInputList[k] = previousInputFrameDownsync.InputList[k];
                // Don't predict "btnA & btnB"!
                prefabbedInputList[k] = (prefabbedInputList[k] & 15);
            }
        }

        // [WARNING] Do not blindly use "selfJoinIndexMask" here, as the "actuallyUsedInput for self" couldn't be confirmed while prefabbing, otherwise we'd have confirmed a wrong self input by "_markConfirmationIfApplicable()"!
        ulong initConfirmedList = 0;
        if (null != existingInputFrame) {
            // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here
            initConfirmedList = (existingInputFrame.ConfirmedList | selfJoinIndexMask);
        }
        currSelfInput = InputManager.GetImmediateEncodedInput(); // When "null == existingInputFrame", it'd be safe to say that "GetImmediateEncodedInput()" is for the requested "inputFrameId"
        prefabbedInputList[(joinIndex - 1)] = currSelfInput;
        while (inputBuffer.EdFrameId <= inputFrameId) {
            // Fill the gap
            int gapInputFrameId = inputBuffer.EdFrameId;
            inputBuffer.DryPut();
            var (ok, ifdHolder) = inputBuffer.GetByFrameId(gapInputFrameId);
            if (!ok || null == ifdHolder) {
                throw new ArgumentNullException(String.Format("inputBuffer was not fully pre-allocated for gapInputFrameId={0}! Now inputBuffer StFrameId={1}, EdFrameId={2}, Cnt/N={3}/{4}", gapInputFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId, inputBuffer.Cnt, inputBuffer.N));
            }

            ifdHolder.InputFrameId = gapInputFrameId;
            for (int k = 0; k < roomCapacity; ++k) {
                ifdHolder.InputList[k] = prefabbedInputList[k];
            }
            ifdHolder.ConfirmedList = initConfirmedList;
        }

        return (previousSelfInput, currSelfInput);
    }

    public (RoomDownsyncFrame, RoomDownsyncFrame) rollbackAndChase(int renderFrameIdSt, int renderFrameIdEd, CollisionSpace collisionSys, bool isChasing) {
        RoomDownsyncFrame prevLatestRdf = null, latestRdf = null;
        for (int i = renderFrameIdSt; i < renderFrameIdEd; i++) {
            var (ok1, currRdf) = renderBuffer.GetByFrameId(i);
            if (false == ok1 || null == currRdf) {
                throw new ArgumentNullException(String.Format("Couldn't find renderFrame for i={0} to rollback, renderFrameId={1}, might've been interruptted by onRoomDownsyncFrame", i, renderFrameId));
            }
            int j = ConvertToDelayedInputFrameId(i);
            var (ok2, delayedInputFrame) = inputBuffer.GetByFrameId(j);
            if (false == ok2 || null == delayedInputFrame) {
                throw new ArgumentNullException(String.Format("Couldn't find delayedInputFrame for j={0} to rollback, renderFrameId={1}", j, renderFrameId));
            }

            bool allowUpdateInputFrameInPlaceUponDynamics = (!isChasing);
            if (allowUpdateInputFrameInPlaceUponDynamics) {
                bool hasInputBeenMutated = UpdateInputFrameInPlaceUponDynamics(j, roomCapacity, delayedInputFrame.ConfirmedList, delayedInputFrame.InputList, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, selfPlayerInfo.JoinIndex);
                if (hasInputBeenMutated) {
                    int ii = ConvertToFirstUsedRenderFrameId(j);
                    if (ii < i) {
                        /*
						   [WARNING] 
						   If we don't rollback at this spot, when the mutated "delayedInputFrame.inputList" a.k.a. "inputFrame#j" matches the later downsynced version, rollback WOULDN'T be triggered for the incorrectly rendered "renderFrame#(ii+1)", and it would STAY IN HISTORY FOREVER -- as the history becomes incorrect, EVERY LATEST renderFrame since "inputFrame#j" was mutated would be ALWAYS incorrectly rendering too!
						 */
                        _handleIncorrectlyRenderedPrediction(j, false);
                    }
                }
            }
            Step(inputBuffer, i, roomCapacity, collisionSys, spaceOffsetX, spaceOffsetY, renderBuffer, ref overlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, jumpedOrNotList, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder);
            var (ok3, nextRdf) = renderBuffer.GetByFrameId(i + 1);
            if (false == ok3 || null == nextRdf) {
                throw new ArgumentNullException(String.Format("Couldn't find nextRdf for i+1={0} to rollback, renderFrameId={1}", i + 1, renderFrameId));
            }

            if (true == isChasing) {
                // [WARNING] Move the cursor "chaserRenderFrameId" when "true == isChasing", keep in mind that "chaserRenderFrameId" is not monotonic!
                chaserRenderFrameId = nextRdf.Id;
            }
            else if (nextRdf.Id == chaserRenderFrameId + 1) {
                chaserRenderFrameId = nextRdf.Id; // To avoid redundant calculation 
            }
            prevLatestRdf = currRdf;
            latestRdf = nextRdf;
        }

        return (prevLatestRdf, latestRdf);
    }

    private void _handleIncorrectlyRenderedPrediction(int firstPredictedYetIncorrectInputFrameId, bool fromUDP) {
        if (TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId) return;
        int renderFrameId1 = ConvertToFirstUsedRenderFrameId(firstPredictedYetIncorrectInputFrameId);
        if (renderFrameId1 >= chaserRenderFrameId) return;

        /*
		   A typical case is as follows.
		   --------------------------------------------------------
		   <renderFrameId1>                           :              36


		   <this.chaserRenderFrameId>                 :              62

		   [this.renderFrameId]                       :              64
		   --------------------------------------------------------
		 */
        // The actual rollback-and-chase would later be executed in "Update()". 
        chaserRenderFrameId = renderFrameId1;
    }

    public void applyRoomDownsyncFrameDynamics(RoomDownsyncFrame prevRdf, RoomDownsyncFrame rdf) {
        for (int k = 0; k < roomCapacity; k++) {
            var currPlayerDownsync = rdf.PlayersArr[k];
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToWorldPos(currPlayerDownsync.VirtualGridX, currPlayerDownsync.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var playerGameObj = playerGameObjs[k];
            playerGameObj.transform.position = new Vector3(wx, wy, playerGameObj.transform.position.z);
        }
    }

    public void _resetCurrentMatch() {
        battleState = BATTLE_STATE_NONE;
        renderFrameId = -1;
        renderFrameIdLagTolerance = 4;
        chaserRenderFrameId = -1;
        maxChasingRenderFramesPerUpdate = 5;
        renderBufferSize = 256;
        playerGameObjs = new GameObject[roomCapacity];
        lastIndividuallyConfirmedInputFrameId = new int[roomCapacity];
        Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        lastIndividuallyConfirmedInputList = new ulong[roomCapacity];
        Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        renderBuffer = new FrameRingBuffer<RoomDownsyncFrame>(renderBufferSize);
        for (int i = 0; i < renderBufferSize; i++) {
            renderBuffer.Put(NewPreallocatedRoomDownsyncFrame(roomCapacity, 64, 64));
        }
        renderBuffer.Clear(); // Then use it by "DryPut"
        int inputBufferSize = (renderBufferSize >> 1) + 1;
        inputBuffer = new FrameRingBuffer<InputFrameDownsync>(inputBufferSize);
        for (int i = 0; i < inputBufferSize; i++) {
            inputBuffer.Put(NewPreallocatedInputFrameDownsync(roomCapacity));
        }
        inputBuffer.Clear(); // Then use it by "DryPut"
        selfPlayerInfo = new PlayerDownsync();
        selfPlayerInfo.JoinIndex = 1;
        var superMap = this.GetComponent<SuperTiled2Unity.SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        collisionSys = new CollisionSpace(spaceOffsetX * 2, spaceOffsetY * 2, 64, 64);

        collisionHolder = new shared.Collision();
        // [WARNING] For "effPushbacks", "hardPushbackNormsArr" and "jumpedOrNotList", use array literal instead of "new Array" for compliance when passing into "gopkgs.ApplyInputFrameDownsyncDynamicsOnSingleRenderFrameJs"!
        effPushbacks = new Vector[roomCapacity];
        Array.Fill<Vector>(effPushbacks, new Vector(0, 0));
        hardPushbackNormsArr = new Vector[roomCapacity][];
        for (int i = 0; i < roomCapacity; i++) {
            hardPushbackNormsArr[i] = new Vector[5];
            Array.Fill<Vector>(hardPushbackNormsArr[i], new Vector(0, 0));
        }
        jumpedOrNotList = new bool[roomCapacity];
        Array.Fill(jumpedOrNotList, false);
        dynamicRectangleColliders = new shared.Collider[64];
        Array.Fill(dynamicRectangleColliders, GenerateRectCollider(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null));
        prefabbedInputListHolder = new ulong[roomCapacity];

        decodedInputHolder = new InputFrameDecoded();
        prevDecodedInputHolder = new InputFrameDecoded();
    }

    public void onInputFrameDownsyncBatch(Google.Protobuf.Collections.RepeatedField<InputFrameDownsync> batch) {
        // TODO
    }

    public void onRoomDownsyncFrame(RoomDownsyncFrame pbRdf, Google.Protobuf.Collections.RepeatedField<InputFrameDownsync> accompaniedInputFrameDownsyncBatch) {
        // This function is also applicable to "re-joining".
        onInputFrameDownsyncBatch(accompaniedInputFrameDownsyncBatch); // Important to do this step before setting IN_BATTLE
        if (null == renderBuffer) {
            return;
        }
        if (BATTLE_STATE_IN_SETTLEMENT == battleState) {
            return;
        }
        int rdfId = pbRdf.Id;
        bool shouldForceDumping1 = (MAGIC_ROOM_DOWNSYNC_FRAME_ID_BATTLE_START == rdfId);
        bool shouldForceDumping2 = (rdfId >= renderFrameId + renderFrameIdLagTolerance);
        bool shouldForceResync = pbRdf.ShouldForceResync;
        ulong selfJoinIndexMask = ((ulong)1 << (selfPlayerInfo.JoinIndex - 1));
        bool notSelfUnconfirmed = (0 == (pbRdf.BackendUnconfirmedMask & selfJoinIndexMask));
        if (notSelfUnconfirmed) {
            shouldForceDumping2 = false;
            shouldForceResync = false;
            // othersForcedDownsyncRenderFrameDict.set(rdfId, pbRdf);
        }
        /*
		   If "BackendUnconfirmedMask" is non-all-1 and contains the current player, show a label/button to hint manual reconnection. Note that the continuity of "recentInputCache" is not a good indicator, because due to network delay upon a [type#1 forceConfirmation] a player might just lag in upsync networking and have all consecutive inputFrameIds locally. 
		 */

        var (dumpRenderCacheRet, oldStRenderFrameId, oldEdRenderFrameId) = (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) ? renderBuffer.SetByFrameId(pbRdf, rdfId) : (RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET, TERMINATING_RENDER_FRAME_ID, TERMINATING_RENDER_FRAME_ID);

        if (RingBuffer<RoomDownsyncFrame>.RING_BUFF_FAILED_TO_SET == dumpRenderCacheRet) {
            throw new ArgumentException(String.Format("Failed to dump render cache#1 (maybe recentRenderCache too small)! rdfId={0}", rdfId));
        }

        if (!shouldForceResync && (MAGIC_ROOM_DOWNSYNC_FRAME_ID_BATTLE_START < rdfId && RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet)) {
            /*
			   Don't change 
			   - chaserRenderFrameId, it's updated only in "rollbackAndChase & onInputFrameDownsyncBatch" (except for when RING_BUFF_NON_CONSECUTIVE_SET)
			 */
            return;
        }

        if (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) {
            // In fact, not having "window.RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet" should already imply that "renderFrameId <= rdfId", but here we double check and log the anomaly  
            if (MAGIC_ROOM_DOWNSYNC_FRAME_ID_BATTLE_START == rdfId) {
                Debug.Log(String.Format("On battle started! renderFrameId={0}", rdfId));
            }
            else {
                Debug.Log(String.Format("On battle resynced! renderFrameId={0}", rdfId));
            }

            renderFrameId = rdfId;
            // In this case it must be true that "rdfId > chaserRenderFrameId".
            chaserRenderFrameId = rdfId;

            battleState = BATTLE_STATE_IN_BATTLE;
        }

        // [WARNING] Leave all graphical updates in "update(dt)" by "applyRoomDownsyncFrameDynamics"
        return;
    }

    void showDebugBoundaries(RoomDownsyncFrame rdf) {
        CreateLineMaterial();
        lineMaterial.SetPass(0);

        GL.PushMatrix();
        // Set transformation matrix for drawing to
        // match our transform
        GL.MultMatrix(transform.localToWorldMatrix);
        
        var grid = this.GetComponentInChildren<Grid>();
        foreach (Transform child in grid.transform) {
            if ("Barrier" == child.gameObject.name) {
                foreach (Transform barrierChild in child) {
                    var barrierTileObj = barrierChild.gameObject.GetComponent<SuperTiled2Unity.SuperObject>();
                    var (tiledRectCx, tiledRectCy) = (barrierTileObj.m_X + barrierTileObj.m_Width * 0.5f, barrierTileObj.m_Y + barrierTileObj.m_Height * 0.5f);
                    var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                    var barrierCollider = GenerateRectCollider(rectCx, rectCy, barrierTileObj.m_Width, barrierTileObj.m_Height, 0, 0, 0, 0, 0, 0, null);

                    GL.Begin(GL.LINES);
                    for (int i = 0; i < 4; i++) {
                        switch (i) {
                            case 0:
                                GL.Vertex3((float)(barrierCollider.X + spaceOffsetX), (float)(barrierCollider.Y - spaceOffsetY), 0);
                                GL.Vertex3((float)(barrierCollider.X + barrierCollider.W + spaceOffsetX), (float)(barrierCollider.Y - spaceOffsetY), 0);
                                break;
                            case 1:
                                GL.Vertex3((float)(barrierCollider.X + barrierCollider.W + spaceOffsetX), (float)(barrierCollider.Y - spaceOffsetY), 0);
                                GL.Vertex3((float)(barrierCollider.X + barrierCollider.W + spaceOffsetX), (float)(barrierCollider.Y + barrierCollider.H - spaceOffsetY), 0);
                                break;
                            case 2:
                                GL.Vertex3((float)(barrierCollider.X + barrierCollider.W + spaceOffsetX), (float)(barrierCollider.Y + barrierCollider.H - spaceOffsetY), 0);
                                GL.Vertex3((float)(barrierCollider.X + spaceOffsetX), (float)(barrierCollider.Y + barrierCollider.H - spaceOffsetY), 0);
                                break;
                            case 3:
                                GL.Vertex3((float)(barrierCollider.X + spaceOffsetX), (float)(barrierCollider.Y + barrierCollider.H - spaceOffsetY), 0);
                                GL.Vertex3((float)(barrierCollider.X + spaceOffsetX), (float)(barrierCollider.Y - spaceOffsetY), 0);
                                break;
                        }
                    }
                    GL.End();
                }
            }
        }
        GL.PopMatrix();
    }

    static Material lineMaterial;
    static void CreateLineMaterial() {
        if (!lineMaterial) {
            // Unity has a built-in shader that is useful for drawing
            // simple colored things.
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            lineMaterial = new Material(shader);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            // Turn on alpha blending
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // Turn backface culling off
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // Turn off depth writes
            lineMaterial.SetInt("_ZWrite", 0);
        }
    }
}
