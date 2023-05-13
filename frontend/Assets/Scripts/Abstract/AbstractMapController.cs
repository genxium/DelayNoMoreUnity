using UnityEngine;
using UnityEngine.UI; // Required when Using UI elements.
using shared;
using static shared.Battle;
using System;
using System.Collections.Generic;
using Pbc = Google.Protobuf.Collections;
using SuperTiled2Unity;

public abstract class AbstractMapController : MonoBehaviour {
    protected int roomCapacity;
    protected int preallocAiPlayerCapacity = DEFAULT_PREALLOC_AI_PLAYER_CAPACITY;
    protected int preallocBulletCapacity = DEFAULT_PREALLOC_BULLET_CAPACITY;
    protected int renderFrameId; // After battle started
    protected int renderFrameIdLagTolerance;
    protected int lastAllConfirmedInputFrameId;
    protected int lastUpsyncInputFrameId;

    protected int chaserRenderFrameId; // at any moment, "chaserRenderFrameId <= renderFrameId", but "chaserRenderFrameId" would fluctuate according to "onInputFrameDownsyncBatch"
    protected int maxChasingRenderFramesPerUpdate;
    protected int renderBufferSize;
    public GameObject characterPrefabForPlayer;
    public GameObject characterPrefabForAi;
    public GameObject fireballPrefab;
    public GameObject errStackLogPanelPrefab;
    protected GameObject errStackLogPanelObj;
    protected GameObject underlyingMap;
    public Canvas canvas;
	public Button backButton;

    protected int[] lastIndividuallyConfirmedInputFrameId;
    protected ulong[] lastIndividuallyConfirmedInputList;
    protected CharacterDownsync selfPlayerInfo = null;
    protected FrameRingBuffer<RoomDownsyncFrame> renderBuffer = null;
    protected FrameRingBuffer<InputFrameDownsync> inputBuffer = null;

    protected ulong[] prefabbedInputListHolder;
    protected GameObject[] playerGameObjs;
    protected List<GameObject> npcGameObjs; // TODO: Use a "Heap with Key access" like https://github.com/genxium/DelayNoMore/blob/main/frontend/assets/scripts/PriorityQueue.js to manage npc rendering, e.g. referencing the treatment of bullets in https://github.com/genxium/DelayNoMore/blob/main/frontend/assets/scripts/Map.js

    protected long battleState;
    protected int spaceOffsetX;
    protected int spaceOffsetY;
    protected float effectivelyInfiniteLyFar;

    protected shared.Collision collisionHolder;
    protected SatResult overlapResult;
    protected Vector[] effPushbacks;
    protected Vector[][] hardPushbackNormsArr;
    protected shared.Collider[] dynamicRectangleColliders;
    protected shared.Collider[] staticRectangleColliders;
    protected InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    protected CollisionSpace collisionSys;
    protected KvPriorityQueue<string, FireballAnimController>.ValScore cachedFireballScore = (x) => x.score;

    protected KvPriorityQueue<string, FireballAnimController> cachedFireballs;

    protected bool frameLogEnabled = false;
    protected Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput;

    protected bool debugDrawingEnabled = false;

    protected ILoggerBridge _loggerBridge = new LoggerBridgeImpl();
    protected void spawnPlayerNode(int joinIndex, float wx, float wy) {
        GameObject newPlayerNode = Instantiate(characterPrefabForPlayer, new Vector3(wx, wy, 0), Quaternion.identity, underlyingMap.transform);
        playerGameObjs[joinIndex - 1] = newPlayerNode;
    }

    protected void spawnNpcNode(float wx, float wy) {
        GameObject newAiPlayerNode = Instantiate(characterPrefabForAi, new Vector3(wx, wy, 0), Quaternion.identity, underlyingMap.transform);
        npcGameObjs.Add(newAiPlayerNode);
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
            Step(inputBuffer, i, roomCapacity, collisionSys, renderBuffer, ref overlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, _loggerBridge);

            if (frameLogEnabled) {
                rdfIdToActuallyUsedInput[i] = delayedInputFrame.Clone();
            }

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

    protected void _handleIncorrectlyRenderedPrediction(int firstPredictedYetIncorrectInputFrameId, bool fromUDP) {
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

        // Printing of this message might induce a performance impact.
        Debug.Log(String.Format("Mismatched input detected, resetting chaserRenderFrameId: {0}->{1}; firstPredictedYetIncorrectInputFrameId: {2}, lastAllConfirmedInputFrameId={3}, fromUDP={4}", chaserRenderFrameId, renderFrameId1, firstPredictedYetIncorrectInputFrameId, lastAllConfirmedInputFrameId, fromUDP));
        // The actual rollback-and-chase would later be executed in "Update()". 
        chaserRenderFrameId = renderFrameId1;

        int rollbackFrames = (renderFrameId - chaserRenderFrameId);
        if (0 > rollbackFrames) {
            rollbackFrames = 0;
        }
        NetworkDoctor.Instance.LogRollbackFrames(rollbackFrames);
    }

    public void applyRoomDownsyncFrameDynamics(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf) {
        for (int k = 0; k < roomCapacity; k++) {
            var currCharacterDownsync = rdf.PlayersArr[k];
            //Debug.Log(String.Format("At rdf.Id={0}, currCharacterDownsync[k:{1}] at [vGridX: {2}, vGridY: {3}, velX: {4}, velY: {5}, chState: {6}, framesInChState: {7}, dirx: {8}]", rdf.Id, k, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, currCharacterDownsync.VelX, currCharacterDownsync.VelY, currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, currCharacterDownsync.DirX));
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var playerGameObj = playerGameObjs[k];
            playerGameObj.transform.position = new Vector3(wx, wy, playerGameObj.transform.position.z);

            var chConfig = characters[currCharacterDownsync.SpeciesId];
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.updateCharacterAnim(currCharacterDownsync, null, false, chConfig);
        }

        for (int k = 0; k < rdf.NpcsArr.Count; k++) {
            var currNpcDownsync = rdf.NpcsArr[k];
            if (TERMINATING_PLAYER_ID == currNpcDownsync.Id) break;
            // Debug.Log(String.Format("At rdf.Id={0}, currNpcDownsync[k:{1}] at [vx: {2}, vy: {3}, chState: {4}, framesInChState: {5}]", rdf.Id, k, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY, currNpcDownsync.CharacterState, currNpcDownsync.FramesInChState));
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var playerGameObj = npcGameObjs[k];
            playerGameObj.transform.position = new Vector3(wx, wy, playerGameObj.transform.position.z);

            var chConfig = characters[currNpcDownsync.SpeciesId];
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.updateCharacterAnim(currNpcDownsync, null, false, chConfig);
        }

        // Put all to infinitely far first
        for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
            var (res, fireballHolder) = cachedFireballs.vals.GetByFrameId(i);
            if (!res || null == fireballHolder) throw new ArgumentNullException(String.Format("There's no fireballHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));

            fireballHolder.gameObject.transform.position = new Vector3(effectivelyInfiniteLyFar, effectivelyInfiniteLyFar, fireballHolder.gameObject.transform.position.z);
        }

        for (int k = 0; k < rdf.Bullets.Count; k++) {
            var bullet = rdf.Bullets[k];
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BattleAttr.BulletLocalId) break;
            bool isExploding = IsBulletExploding(bullet);
            string lookupKey = null;
            var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);
            bool spontaneousLooping = false;
            switch (bullet.Config.BType) {
                case BulletType.Melee:
                    if (isExploding) {
                        lookupKey = String.Format("Melee_Explosion{0}", bullet.Config.SpeciesId);
                    }
                    break;
                case BulletType.Fireball:
                    if (IsBulletActive(bullet, rdf.Id) || isExploding) {
                        lookupKey = isExploding ? String.Format("Explosion{0}", bullet.Config.SpeciesId) : String.Format("Fireball{0}", bullet.Config.SpeciesId);
                        spontaneousLooping = !isExploding;
                    }
                    break;
                default:
                    break;
            }
            if (null == lookupKey) continue;
            var explosionAnimHolder = cachedFireballs.PopAny(lookupKey);
            if (null == explosionAnimHolder) {
                explosionAnimHolder = cachedFireballs.Pop();
                //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a new fireball node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a cached node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
            }

            if (null == explosionAnimHolder) {
                throw new ArgumentNullException(String.Format("No available fireball node for lookupKey={0}", lookupKey));
            }
            explosionAnimHolder.updateAnim(lookupKey, bullet.FramesInBlState, bullet.DirX, spontaneousLooping, rdf);
            explosionAnimHolder.score = rdf.Id;
            explosionAnimHolder.gameObject.transform.position = new Vector3(wx, wy, explosionAnimHolder.gameObject.transform.position.z);

            cachedFireballs.Put(lookupKey, explosionAnimHolder);
        }
    }

    protected void preallocateHolders() {
        if (0 >= roomCapacity) {
            throw new ArgumentException(String.Format("roomCapacity={0} is non-positive, please initialize it first!", roomCapacity));
        }

        if (0 >= preallocAiPlayerCapacity) {
            throw new ArgumentException(String.Format("preallocAiPlayerCapacity={0} is non-positive, please initialize it first!", preallocAiPlayerCapacity));
        }
        Debug.Log(String.Format("preallocateHolders with roomCapacity={0}, preallocAiPlayerCapacity={1}, preallocBulletCapacity={2}", roomCapacity, preallocAiPlayerCapacity, preallocBulletCapacity));
        renderBufferSize = 1024;

        renderBuffer = new FrameRingBuffer<RoomDownsyncFrame>(renderBufferSize);
        for (int i = 0; i < renderBufferSize; i++) {
            renderBuffer.Put(NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocAiPlayerCapacity, preallocBulletCapacity));
        }
        renderBuffer.Clear(); // Then use it by "DryPut"

        int inputBufferSize = (renderBufferSize >> 1) + 1;
        inputBuffer = new FrameRingBuffer<InputFrameDownsync>(inputBufferSize);
        for (int i = 0; i < inputBufferSize; i++) {
            inputBuffer.Put(NewPreallocatedInputFrameDownsync(roomCapacity));
        }
        inputBuffer.Clear(); // Then use it by "DryPut"

        lastIndividuallyConfirmedInputFrameId = new int[roomCapacity];
        Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);

        lastIndividuallyConfirmedInputList = new ulong[roomCapacity];
        Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);

        prefabbedInputListHolder = new ulong[roomCapacity];
        Array.Fill<ulong>(prefabbedInputListHolder, 0);

        effPushbacks = new Vector[roomCapacity + preallocAiPlayerCapacity];
        for (int i = 0; i < effPushbacks.Length; i++) {
            effPushbacks[i] = new Vector(0, 0);
        }
        hardPushbackNormsArr = new Vector[roomCapacity + preallocAiPlayerCapacity][];
        for (int i = 0; i < hardPushbackNormsArr.Length; i++) {
            int cap = 5;
            hardPushbackNormsArr[i] = new Vector[cap];
            for (int j = 0; j < cap; j++) {
                hardPushbackNormsArr[i][j] = new Vector(0, 0);
            }
        }

        int dynamicRectangleCollidersCap = 32;
        dynamicRectangleColliders = new shared.Collider[dynamicRectangleCollidersCap];
        for (int i = 0; i < dynamicRectangleCollidersCap; i++) {
            dynamicRectangleColliders[i] = NewRectCollider(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
        }
        staticRectangleColliders = new shared.Collider[128];

        decodedInputHolder = new InputFrameDecoded();
        prevDecodedInputHolder = new InputFrameDecoded();

        int fireballHoldersCap = 256;
        cachedFireballs = new KvPriorityQueue<string, FireballAnimController>(fireballHoldersCap, cachedFireballScore);

        effectivelyInfiniteLyFar = 4f * Math.Max(spaceOffsetX, spaceOffsetY);
        for (int i = 0; i < fireballHoldersCap; i++) {
            // Fireballs & explosions should be drawn above any character
            GameObject newFireballNode = Instantiate(fireballPrefab, new Vector3(effectivelyInfiniteLyFar, effectivelyInfiniteLyFar, -5), Quaternion.identity);
            FireballAnimController holder = newFireballNode.GetComponent<FireballAnimController>();
            holder.score = -1;
            string initLookupKey = String.Format("{0}", -(i + 1)); // there's definitely no such "bulletLocalId"
            cachedFireballs.Put(initLookupKey, holder);
        }
    }

    protected virtual void resetCurrentMatch() {
        Debug.Log(String.Format("resetCurrentMatch with roomCapacity={0}", roomCapacity));
        battleState = ROOM_STATE_IMPOSSIBLE;
        renderFrameId = 0;
        renderFrameIdLagTolerance = 4;
        chaserRenderFrameId = -1;
        lastAllConfirmedInputFrameId = -1;
        lastUpsyncInputFrameId = -1;
        maxChasingRenderFramesPerUpdate = 5;
        rdfIdToActuallyUsedInput = new Dictionary<int, InputFrameDownsync>();

        playerGameObjs = new GameObject[roomCapacity];
        npcGameObjs = new List<GameObject>();

        var superMap = underlyingMap.GetComponent<SuperTiled2Unity.SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        collisionSys = new CollisionSpace(spaceOffsetX * 2, spaceOffsetY * 2, 64, 64);
        collisionHolder = new shared.Collision();

        // Reset the preallocated
        Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        renderBuffer.Clear();
        inputBuffer.Clear();
        Array.Fill<ulong>(prefabbedInputListHolder, 0);

        // Clearing cached fireball rendering nodes [BEGINS]
        // TODO
        // Clearing cached fireball rendering nodes [ENDS]
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
        // Debug.Log(String.Format("onInputFrameDownsyncBatch called for batchInputFrameIdRange [{0}, {1}]", batch[0].InputFrameId, batch[batch.Count-1].InputFrameId));

        NetworkDoctor.Instance.LogInputFrameDownsync(batch[0].InputFrameId, batch[batch.Count - 1].InputFrameId);
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

            NetworkDoctor.Instance.LogRollbackFrames(0);

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

        if (shouldSendInputFrameUpsyncBatch(prevSelfInput, currSelfInput, noDelayInputFrameId)) {
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
        if (Battle.DOWNSYNC_MSG_ACT_BATTLE_START == renderFrameId) {
            var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
        	Debug.Log(String.Format("Battle started, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
            Camera.main.transform.position = new Vector3(playerGameObj.transform.position.x, playerGameObj.transform.position.y, Camera.main.transform.position.z);
        } else {
            cameraTrack(rdf);
        }

        ++renderFrameId;
    }

    protected void onBattleStopped() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        battleState = ROOM_STATE_IN_SETTLEMENT;
    }

    protected abstract bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId);

    protected abstract void sendInputFrameUpsyncBatch(int latestLocalInputFrameId);

    protected static Material lineMaterial;
    protected static void CreateLineMaterial() {
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

    protected RoomDownsyncFrame mockStartRdf() {
        var playerStartingCposList = new Vector[roomCapacity];
        var (defaultColliderRadius, _) = PolygonColliderCtrToVirtualGridPos(12, 0);

        var grid = underlyingMap.GetComponentInChildren<Grid>();

        var npcsStartingCposList = new List<(Vector, int, int)>();
        float defaultPatrolCueRadius = 6;
        int staticColliderIdx = 0;
        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "Barrier":
                    foreach (Transform barrierChild in child) {
                        var barrierTileObj = barrierChild.gameObject.GetComponent<SuperObject>();
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
                        staticRectangleColliders[staticColliderIdx++] = barrierCollider;
                    }
                    break;
                case "PlayerStartingPos":
                    int j = 0;
                    foreach (Transform playerPos in child) {
                        var posTileObj = playerPos.gameObject.GetComponent<SuperObject>();
                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(posTileObj.m_X, posTileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        playerStartingCposList[j] = new Vector(cx, cy);
                        //Debug.Log(String.Format("new playerStartingCposList[i:{0}]=[X:{1}, Y:{2}]", j, cx, cy));
                        j++;
                        if (j >= roomCapacity) break;
                    }
                    break;
                case "NpcStartingPos":
                    foreach (Transform npcPos in child) {
                        var tileObj = npcPos.gameObject.GetComponent<SuperObject>();
                        var tileProps = npcPos.gameObject.gameObject.GetComponent<SuperCustomProperties>();
                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        CustomProperty dirX, speciesId;
                        tileProps.TryGetCustomProperty("dirX", out dirX);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        npcsStartingCposList.Add((new Vector(cx, cy), dirX.IsEmpty ? 2 : dirX.GetValueAsInt(), speciesId.IsEmpty ? 0 : speciesId.GetValueAsInt()));
                    }
                    break;
                case "PatrolCue":
                    foreach (Transform patrolCueChild in child) {
                        var tileObj = patrolCueChild.gameObject.GetComponent<SuperObject>();
                        var tileProps = patrolCueChild.gameObject.GetComponent<SuperCustomProperties>();

                        var (patrolCueCx, patrolCueCy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);

                        CustomProperty flAct, frAct;
                        tileProps.TryGetCustomProperty("flAct", out flAct);
                        tileProps.TryGetCustomProperty("frAct", out frAct);

                        var newPatrolCue = new PatrolCue {
                            FlAct = flAct.IsEmpty ? 0 : (ulong)flAct.GetValueAsInt(),
                            FrAct = frAct.IsEmpty ? 0 : (ulong)frAct.GetValueAsInt(),
                        };

                        var patrolCueCollider = NewRectCollider(patrolCueCx, patrolCueCy, 2 * defaultPatrolCueRadius, 2 * defaultPatrolCueRadius, 0, 0, 0, 0, 0, 0, newPatrolCue);
                        collisionSys.AddSingle(patrolCueCollider);
                        staticRectangleColliders[staticColliderIdx++] = patrolCueCollider;
                        //Debug.Log(String.Format("newPatrolCue={0} at [X:{1}, Y:{2}]", newPatrolCue, patrolCueCx, patrolCueCy));
                    }
                    break;
                default:
                    break;
            }
        }

        var startRdf = NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocAiPlayerCapacity, preallocBulletCapacity);
        startRdf.Id = Battle.DOWNSYNC_MSG_ACT_BATTLE_START;
        startRdf.ShouldForceResync = false;
        for (int i = 0; i < roomCapacity; i++) {
            int joinIndex = i + 1;
            var cpos = playerStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            spawnPlayerNode(joinIndex, wx, wy);

            var characterSpeciesId = 0;
            var chConfig = Battle.characters[characterSpeciesId];

            var playerInRdf = startRdf.PlayersArr[i];
            var (playerVposX, playerVposY) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y); // World and CollisionSpace coordinates have the same scale, just translated
            playerInRdf.JoinIndex = joinIndex;
            playerInRdf.VirtualGridX = playerVposX;
            playerInRdf.VirtualGridY = playerVposY;
            playerInRdf.RevivalVirtualGridX = playerVposX;
            playerInRdf.RevivalVirtualGridY = playerVposY;
            playerInRdf.Speed = chConfig.Speed;
            playerInRdf.ColliderRadius = defaultColliderRadius;
            playerInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
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

        for (int i = 0; i < npcsStartingCposList.Count; i++) {
            int joinIndex = roomCapacity + i + 1;
            var (cpos, dirX, characterSpeciesId) = npcsStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            spawnNpcNode(wx, wy);

            var chConfig = Battle.characters[characterSpeciesId];

            var npcInRdf = new CharacterDownsync();
            var (vx, vy) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y);
            npcInRdf.Id = 0; // Just for not being excluded 
            npcInRdf.JoinIndex = joinIndex;
            npcInRdf.VirtualGridX = vx;
            npcInRdf.VirtualGridY = vy;
            npcInRdf.RevivalVirtualGridX = vx;
            npcInRdf.RevivalVirtualGridY = vy;
            npcInRdf.Speed = chConfig.Speed;
            npcInRdf.ColliderRadius = defaultColliderRadius;
            npcInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
            npcInRdf.FramesToRecover = 0;
            npcInRdf.DirX = dirX;
            npcInRdf.DirY = 0;
            npcInRdf.VelX = 0;
            npcInRdf.VelY = 0;
            npcInRdf.InAir = true;
            npcInRdf.OnWall = false;
            npcInRdf.Hp = 100;
            npcInRdf.MaxHp = 100;
            npcInRdf.SpeciesId = characterSpeciesId;

            startRdf.NpcsArr[i] = npcInRdf;
        }

        return startRdf;
    }

    protected void popupErrStackPanel(string msg) {
        if (null == errStackLogPanelObj) {
            errStackLogPanelObj = Instantiate(errStackLogPanelPrefab, new Vector3(canvas.transform.position.x, canvas.transform.position.y, +5), Quaternion.identity, canvas.transform);
        }
        var errStackLogPanel = errStackLogPanelObj.GetComponent<ErrStackLogPanel>();
        errStackLogPanel.content.text = msg;
    }

    protected void cameraTrack(RoomDownsyncFrame rdf) {
        if (null == selfPlayerInfo) return;
        var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
        var playerCharacterDownsync = rdf.PlayersArr[selfPlayerInfo.JoinIndex - 1];
        var (velCX, velCY) = VirtualGridToPolygonColliderCtr(playerCharacterDownsync.Speed, playerCharacterDownsync.Speed);
		var cameraSpeedInWorld = new Vector2(velCX, velCY).magnitude * 50;
        var camOldPos = Camera.main.transform.position;
        var dst = playerGameObj.transform.position;
        var dstDiff2 = new Vector2(dst.x - camOldPos.x, dst.y - camOldPos.y);

        //Debug.Log(String.Format("cameraTrack, camOldPos={0}, dst={1}, deltaTime={2}", camOldPos, dst, Time.deltaTime));
        var stepLength = Time.deltaTime * cameraSpeedInWorld;
        if (stepLength > dstDiff2.magnitude) {
            Camera.main.transform.position = new Vector3(dst.x, dst.y, camOldPos.z);
        } else {
            var newMapPosDiff2 = dstDiff2.normalized * stepLength;
            Camera.main.transform.position = new Vector3(camOldPos.x + newMapPosDiff2.x, camOldPos.y + newMapPosDiff2.y, camOldPos.z);
        }
    }
}
