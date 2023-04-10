using UnityEngine;
using shared;
using static shared.Battle;
using System;
using static shared.CharacterState;
using Pbc = Google.Protobuf.Collections;

public abstract class AbstractMapController : MonoBehaviour {
    protected int roomCapacity = 1;
    protected int renderFrameId; // After battle started
    protected int renderFrameIdLagTolerance;
    protected int lastAllConfirmedInputFrameId;
    protected int lastUpsyncInputFrameId;

    protected int chaserRenderFrameId; // at any moment, "chaserRenderFrameId <= renderFrameId", but "chaserRenderFrameId" would fluctuate according to "onInputFrameDownsyncBatch"
    protected int maxChasingRenderFramesPerUpdate;
    protected int renderBufferSize;
    public GameObject characterPrefab;

    protected int[] lastIndividuallyConfirmedInputFrameId;
    protected ulong[] lastIndividuallyConfirmedInputList;
    protected PlayerDownsync selfPlayerInfo = null;
    protected FrameRingBuffer<RoomDownsyncFrame> renderBuffer = null;
    protected FrameRingBuffer<InputFrameDownsync> inputBuffer = null;

    protected ulong[] prefabbedInputListHolder;
    protected GameObject[] playerGameObjs;

    protected long battleState;
    protected int spaceOffsetX;
    protected int spaceOffsetY;

    protected shared.Collision collisionHolder;
    protected SatResult overlapResult;
    protected Vector[] effPushbacks;
    protected Vector[][] hardPushbackNormsArr;
    protected bool[] jumpedOrNotList;
    protected shared.Collider[] dynamicRectangleColliders;
    protected shared.Collider[] staticRectangleColliders;
    protected InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    protected CollisionSpace collisionSys;

    protected bool debugDrawingEnabled = true; 

    protected void spawnPlayerNode(int joinIndex, float wx, float wy) {
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(wx, wy, 0), Quaternion.identity);
        playerGameObjs[joinIndex] = newPlayerNode;
    }

    protected (ulong, ulong) getOrPrefabInputFrameUpsync(int inputFrameId, bool canConfirmSelf, ulong[] prefabbedInputList) {
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
            } else if (lastIndividuallyConfirmedInputFrameId[k] <= inputFrameId) {
                prefabbedInputList[k] = lastIndividuallyConfirmedInputList[k];
                // Don't predict "btnA & btnB"!
                prefabbedInputList[k] = (prefabbedInputList[k] & 15);
            } else if (null != previousInputFrameDownsync) {
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
        BattleInputManager iptmgr = this.gameObject.GetComponent<BattleInputManager>();
        currSelfInput = iptmgr.GetImmediateEncodedInput(); // When "null == existingInputFrame", it'd be safe to say that "GetImmediateEncodedInput()" is for the requested "inputFrameId"
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

    protected (RoomDownsyncFrame, RoomDownsyncFrame) rollbackAndChase(int renderFrameIdSt, int renderFrameIdEd, CollisionSpace collisionSys, bool isChasing) {
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

                           The update to chaserRenderFrameId here would NOT impact the current cycle of rollbackAndChase !
                         */
                        _handleIncorrectlyRenderedPrediction(j, false);
                    }
                }
            }
            Step(inputBuffer, i, roomCapacity, collisionSys, renderBuffer, ref overlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, jumpedOrNotList, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder);
            var (ok3, nextRdf) = renderBuffer.GetByFrameId(i + 1);
            if (false == ok3 || null == nextRdf) {
                throw new ArgumentNullException(String.Format("Couldn't find nextRdf for i+1={0} to rollback, renderFrameId={1}", i + 1, renderFrameId));
            }

            if (true == isChasing) {
                // [WARNING] Move the cursor "chaserRenderFrameId" when "true == isChasing", keep in mind that "chaserRenderFrameId" is not monotonic!
                chaserRenderFrameId = nextRdf.Id;
            } else if (nextRdf.Id == chaserRenderFrameId + 1) {
                chaserRenderFrameId = nextRdf.Id; // To avoid redundant calculation 
            }
            prevLatestRdf = currRdf;
            latestRdf = nextRdf;
        }

        return (prevLatestRdf, latestRdf);
    }

    private bool _allConfirmed(ulong confirmedList) {
        return (confirmedList + 1) == (ulong)(1 << roomCapacity);
    }

    private int _markConfirmationIfApplicable() {
        int newAllConfirmedCnt = 0;
        int candidateInputFrameId = (lastAllConfirmedInputFrameId + 1);
        if (candidateInputFrameId < inputBuffer.StFrameId) {
            candidateInputFrameId = inputBuffer.StFrameId;
        }
        while (inputBuffer.StFrameId <= candidateInputFrameId && candidateInputFrameId < inputBuffer.EdFrameId) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(candidateInputFrameId);
            if (false == res1 || null == inputFrameDownsync) break;
            if (false == _allConfirmed(inputFrameDownsync.ConfirmedList)) break;
            ++candidateInputFrameId;
            ++newAllConfirmedCnt;
        }
        if (0 < newAllConfirmedCnt) {
            lastAllConfirmedInputFrameId = candidateInputFrameId - 1;
        }
        return newAllConfirmedCnt;
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

    public void applyRoomDownsyncFrameDynamics(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf) {
        for (int k = 0; k < roomCapacity; k++) {
            var currPlayerDownsync = rdf.PlayersArr[k];
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currPlayerDownsync.VirtualGridX, currPlayerDownsync.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var playerGameObj = playerGameObjs[k];
            playerGameObj.transform.position = new Vector3(wx, wy, playerGameObj.transform.position.z);

            var chConfig = characters[0]; // todo: remove hardcoded speciesId
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.updateCharacterAnim(currPlayerDownsync, null, false, chConfig);

            if (k == selfPlayerInfo.JoinIndex - 1) {
                var camOldPos = Camera.main.transform.position;
                Camera.main.transform.position = new Vector3(wx, wy, camOldPos.z);
            }
        }
    }

    public virtual void _resetCurrentMatch() {
        battleState = ROOM_STATE_IMPOSSIBLE;
        renderFrameId = Battle.TERMINATING_RENDER_FRAME_ID;
        renderFrameIdLagTolerance = 4;
        chaserRenderFrameId = Battle.TERMINATING_RENDER_FRAME_ID;
        lastAllConfirmedInputFrameId = Battle.TERMINATING_INPUT_FRAME_ID;
        lastUpsyncInputFrameId = Battle.TERMINATING_INPUT_FRAME_ID;
        maxChasingRenderFramesPerUpdate = 5;
        renderBufferSize = 256;
        playerGameObjs = new GameObject[roomCapacity];
        lastIndividuallyConfirmedInputFrameId = new int[roomCapacity];
        Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        lastIndividuallyConfirmedInputList = new ulong[roomCapacity];
        Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        renderBuffer = new FrameRingBuffer<RoomDownsyncFrame>(renderBufferSize);
        for (int i = 0; i < renderBufferSize; i++) {
            renderBuffer.Put(NewPreallocatedRoomDownsyncFrame(roomCapacity, 128));
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
        effPushbacks = new Vector[roomCapacity];
        for (int i = 0; i < roomCapacity; i++) {
            effPushbacks[i] = new Vector(0, 0);
        }
        hardPushbackNormsArr = new Vector[roomCapacity][];
        for (int i = 0; i < roomCapacity; i++) {
            int cap = 5;
            hardPushbackNormsArr[i] = new Vector[cap];
            for (int j = 0; j < cap; j++) {
                hardPushbackNormsArr[i][j] = new Vector(0, 0);
            }
        }
        jumpedOrNotList = new bool[roomCapacity];
        Array.Fill(jumpedOrNotList, false);

        int dynamicRectangleCollidersCap = 64;
        dynamicRectangleColliders = new shared.Collider[dynamicRectangleCollidersCap];
        for (int i = 0; i < dynamicRectangleCollidersCap; i++) {
            dynamicRectangleColliders[i] = NewRectCollider(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
        }
        staticRectangleColliders = new shared.Collider[128];

        prefabbedInputListHolder = new ulong[roomCapacity];

        decodedInputHolder = new InputFrameDecoded();
        prevDecodedInputHolder = new InputFrameDecoded();
    }

    public void onInputFrameDownsyncBatch(Pbc.RepeatedField<InputFrameDownsync> batch) {
        // This method is guaranteed to run in UIThread only.
        if (null == batch) {
            return;
        }
        if (null == inputBuffer) {
            return;
        }
        if (ROOM_STATE_IN_SETTLEMENT == battleState) {
            return;
        }

        int firstPredictedYetIncorrectInputFrameId = TERMINATING_INPUT_FRAME_ID;
        foreach (var inputFrameDownsync in batch) {
            int inputFrameDownsyncId = inputFrameDownsync.InputFrameId;
            if (inputFrameDownsyncId <= lastAllConfirmedInputFrameId) {
                continue;
            }
            // [WARNING] Now that "inputFrameDownsyncId > self.lastAllConfirmedInputFrameId", we should make an update immediately because unlike its backend counterpart "Room.LastAllConfirmedInputFrameId", the frontend "mapIns.lastAllConfirmedInputFrameId" might inevitably get gaps among discrete values due to "either type#1 or type#2 forceConfirmation" -- and only "onInputFrameDownsyncBatch" can catch this! 
            lastAllConfirmedInputFrameId = inputFrameDownsyncId;
            var (res1, localInputFrame) = inputBuffer.GetByFrameId(inputFrameDownsyncId);
            if (null != localInputFrame
              &&
              TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId
              &&
              !shared.Battle.EqualInputLists(localInputFrame.InputList, inputFrameDownsync.InputList)
            ) {
                firstPredictedYetIncorrectInputFrameId = inputFrameDownsyncId;
            }
            // [WARNING] Take all "inputFrameDownsync" from backend as all-confirmed, it'll be later checked by "rollbackAndChase". 
            inputFrameDownsync.ConfirmedList = (ulong)(1 << roomCapacity) - 1;

            for (int j = 0; j < roomCapacity; j++) {
                if (inputFrameDownsync.InputFrameId > lastIndividuallyConfirmedInputFrameId[j]) {
                    lastIndividuallyConfirmedInputFrameId[j] = inputFrameDownsync.InputFrameId;
                    lastIndividuallyConfirmedInputList[j] = inputFrameDownsync.InputList[j];
                }
            }
            //console.log(`Confirmed inputFrameId=${inputFrameDownsync.inputFrameId}`);
            var (res2, oldStFrameId, oldEdFrameId) = inputBuffer.SetByFrameId(inputFrameDownsync, inputFrameDownsync.InputFrameId);
            if (RingBuffer<InputFrameDownsync>.RING_BUFF_FAILED_TO_SET == res2) {
                throw new ArgumentException(String.Format("Failed to dump input cache(maybe recentInputCache too small)! inputFrameDownsync.inputFrameId={0}, lastAllConfirmedInputFrameId={1}", inputFrameDownsyncId, lastAllConfirmedInputFrameId));
            }
        }
        _markConfirmationIfApplicable();
        _handleIncorrectlyRenderedPrediction(firstPredictedYetIncorrectInputFrameId, false);
    }

    public void onRoomDownsyncFrame(RoomDownsyncFrame pbRdf, Pbc::RepeatedField<InputFrameDownsync> accompaniedInputFrameDownsyncBatch) {
        // This function is also applicable to "re-joining".
        onInputFrameDownsyncBatch(accompaniedInputFrameDownsyncBatch); // Important to do this step before setting IN_BATTLE
        if (null == renderBuffer) {
            return;
        }
        if (ROOM_STATE_IN_SETTLEMENT == battleState) {
            return;
        }
        int rdfId = pbRdf.Id;
        bool shouldForceDumping1 = (Battle.DOWNSYNC_MSG_ACT_BATTLE_START == rdfId);
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

        if (!shouldForceResync && (Battle.DOWNSYNC_MSG_ACT_BATTLE_START < rdfId && RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet)) {
            /*
			   Don't change 
			   - chaserRenderFrameId, it's updated only in "rollbackAndChase & onInputFrameDownsyncBatch" (except for when RING_BUFF_NON_CONSECUTIVE_SET)
			 */
            return;
        }

        if (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) {
            // In fact, not having "window.RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet" should already imply that "renderFrameId <= rdfId", but here we double check and log the anomaly  
            if (Battle.DOWNSYNC_MSG_ACT_BATTLE_START == rdfId) {
                Debug.Log(String.Format("On battle started! renderFrameId={0}", rdfId));
            } else {
                Debug.Log(String.Format("On battle resynced! renderFrameId={0}", rdfId));
            }

            renderFrameId = rdfId;
            // In this case it must be true that "rdfId > chaserRenderFrameId".
            chaserRenderFrameId = rdfId;

            battleState = ROOM_STATE_IN_BATTLE;
        }

        // [WARNING] Leave all graphical updates in "Update()" by "applyRoomDownsyncFrameDynamics"
        return;
    }

    // Update is called once per frame
    protected void doUpdate() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
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

        if (shouldSendInputFrameUpsyncBatch(prevSelfInput, currSelfInput, lastUpsyncInputFrameId, noDelayInputFrameId)) {
          // TODO: Is the following statement run asynchronously in an implicit manner? Should I explicitly run it asynchronously?
          sendInputFrameUpsyncBatch(noDelayInputFrameId);
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
        ++renderFrameId;
    }

    protected void onBattleStopped() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        battleState = ROOM_STATE_IN_SETTLEMENT;
    }

    protected abstract bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int lastUpsyncInputFrameId, int currInputFrameId);

    protected abstract void sendInputFrameUpsyncBatch(int latestLocalInputFrameId);

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

	protected void enableBattleInput(bool yesOrNo) {
        BattleInputManager iptmgr = this.gameObject.GetComponent<BattleInputManager>();
		iptmgr.enable(yesOrNo);
	}
}
