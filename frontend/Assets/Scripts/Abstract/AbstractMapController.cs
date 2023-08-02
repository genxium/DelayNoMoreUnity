using UnityEngine;
using shared;
using static shared.Battle;
using System;
using System.Collections.Generic;
using Pbc = Google.Protobuf.Collections;
using SuperTiled2Unity;

public abstract class AbstractMapController : MonoBehaviour {
    protected int roomCapacity;
    protected int battleDurationFrames;

    protected int preallocNpcCapacity = DEFAULT_PREALLOC_NPC_CAPACITY;
    protected int preallocBulletCapacity = DEFAULT_PREALLOC_BULLET_CAPACITY;
    protected int preallocTrapCapacity = DEFAULT_PREALLOC_TRAP_CAPACITY;

    protected int renderFrameId; // After battle started
    protected int renderFrameIdLagTolerance;
    protected int lastAllConfirmedInputFrameId;
    protected int lastUpsyncInputFrameId;

    protected int chaserRenderFrameId; // at any moment, "chaserRenderFrameId <= renderFrameId", but "chaserRenderFrameId" would fluctuate according to "onInputFrameDownsyncBatch"
    protected int maxChasingRenderFramesPerUpdate;
    protected int renderBufferSize;
    public InplaceHpBar inplaceHpBarPrefab;
    public GameObject fireballPrefab;
    public GameObject errStackLogPanelPrefab;
    public GameObject teamRibbonPrefab;
    protected GameObject errStackLogPanelObj;
    protected GameObject underlyingMap;
    public Canvas canvas;

    protected int[] lastIndividuallyConfirmedInputFrameId;
    protected ulong[] lastIndividuallyConfirmedInputList;
    protected CharacterDownsync selfPlayerInfo = null;
    protected FrameRingBuffer<RoomDownsyncFrame> renderBuffer = null;
    protected FrameRingBuffer<InputFrameDownsync> inputBuffer = null;
    protected FrameRingBuffer<shared.Collider> residueCollided = null;

    protected ulong[] prefabbedInputListHolder;
    protected GameObject[] playerGameObjs;
    protected List<GameObject> npcGameObjs; // TODO: Use a "Heap with Key access" like https://github.com/genxium/DelayNoMore/blob/main/frontend/assets/scripts/PriorityQueue.js to manage npc rendering, e.g. referencing the treatment of bullets in https://github.com/genxium/DelayNoMore/blob/main/frontend/assets/scripts/Map.js
    protected List<GameObject> dynamicTrapGameObjs;

    protected long battleState;
    protected int spaceOffsetX;
    protected int spaceOffsetY;
    protected float effectivelyInfinitelyFar;

    protected shared.Collision collisionHolder;
    protected SatResult overlapResult, primaryOverlapResult;
    protected Vector[] effPushbacks;
    protected Vector[][] hardPushbackNormsArr;
    protected Vector[] softPushbacks;
    protected shared.Collider[] dynamicRectangleColliders;
    protected shared.Collider[] staticColliders;
    protected InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    protected CollisionSpace collisionSys;
    protected KvPriorityQueue<string, FireballAnimController>.ValScore cachedFireballScore = (x) => x.score;
    protected KvPriorityQueue<string, DebugLine>.ValScore cachedLineScore = (x) => x.score;

    public GameObject linePrefab;
    protected KvPriorityQueue<string, FireballAnimController> cachedFireballs;
    protected Vector3[] debugDrawPositionsHolder = new Vector3[4]; // Currently only rectangles are drawn
    protected KvPriorityQueue<string, DebugLine> cachedLineRenderers;

    protected bool frameLogEnabled = false;
    protected Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput;
    protected Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs;
    protected List<shared.Collider> completelyStaticTrapColliders;

    public CharacterSelectPanel characterSelectPanel;

    public abstract void onCharacterSelectGoAction(int speciesId);

    protected bool debugDrawingEnabled = false;

    protected ILoggerBridge _loggerBridge = new LoggerBridgeImpl();

    public SelfBattleHeading selfBattleHeading;

    public GameObject playerLightsPrefab;
    protected PlayerLights selfPlayerLights;

    protected Vector2 teamRibbonOffset = new Vector2(-8f, +18f);

    protected Vector2 inplaceHpBarOffset = new Vector2(-16f, +24f);
    protected float characterZ = 0;
    protected float fireballZ = -5;
    protected float lineRendererZ = +5;
    protected float inplaceHpBarZ = +10;

    protected GameObject loadCharacterPrefab(CharacterConfig chConfig) {
        string path = String.Format("Prefabs/{0}", chConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadTrapPrefab(TrapConfig trapConfig) {
        string path = String.Format("Prefabs/{0}", trapConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    public ReadyGo readyGoPanel;
    protected Vector3 newPosHolder = new Vector3();

    protected void spawnPlayerNode(int joinIndex, int speciesId, float wx, float wy, int bulletTeamId) {
        var characterPrefab = loadCharacterPrefab(characters[speciesId]);
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(wx, wy, characterZ), Quaternion.identity, underlyingMap.transform);
        playerGameObjs[joinIndex - 1] = newPlayerNode;

        var animController = newPlayerNode.GetComponent<CharacterAnimController>();

        TeamRibbon associatedTeamRibbon = Instantiate(teamRibbonPrefab, new Vector3(wx + teamRibbonOffset.x, wy + teamRibbonOffset.y, inplaceHpBarZ), Quaternion.identity, underlyingMap.transform).GetComponent<TeamRibbon>();
        animController.teamRibbon = associatedTeamRibbon;
        associatedTeamRibbon.setBulletTeamId(bulletTeamId);

        if (joinIndex != selfPlayerInfo.JoinIndex) {
            InplaceHpBar associatedHpBar = Instantiate(inplaceHpBarPrefab, new Vector3(wx + inplaceHpBarOffset.x, wy + inplaceHpBarOffset.y, inplaceHpBarZ), Quaternion.identity, underlyingMap.transform).GetComponent<InplaceHpBar>();

            animController.hpBar = associatedHpBar;
        } else {
            //selfPlayerLights = Instantiate(playerLightsPrefab, new Vector3(wx, wy, 0), Quaternion.identity, underlyingMap.transform).GetComponent<PlayerLights>();
        }
    }

    protected void spawnNpcNode(int speciesId, float wx, float wy, int bulletTeamId) {
        var characterPrefab = loadCharacterPrefab(characters[speciesId]);
        GameObject newNpcNode = Instantiate(characterPrefab, new Vector3(wx, wy, characterZ), Quaternion.identity, underlyingMap.transform);
        npcGameObjs.Add(newNpcNode);
        InplaceHpBar associatedHpBar = Instantiate(inplaceHpBarPrefab, new Vector3(wx + inplaceHpBarOffset.x, wy + inplaceHpBarOffset.y, inplaceHpBarZ), Quaternion.identity, underlyingMap.transform).GetComponent<InplaceHpBar>();

        TeamRibbon associatedTeamRibbon = Instantiate(teamRibbonPrefab, new Vector3(wx + teamRibbonOffset.x, wy + teamRibbonOffset.y, inplaceHpBarZ), Quaternion.identity, underlyingMap.transform).GetComponent<TeamRibbon>();

        var animController = newNpcNode.GetComponent<CharacterAnimController>();

        animController.hpBar = associatedHpBar;
        animController.teamRibbon = associatedTeamRibbon;
        associatedTeamRibbon.setBulletTeamId(bulletTeamId);
    }

    protected void spawnDynamicTrapNode(int speciesId, float wx, float wy) {
        var trapPrefab = loadTrapPrefab(trapConfigs[speciesId]);
        GameObject newTrapNode = Instantiate(trapPrefab, new Vector3(wx, wy, characterZ), Quaternion.identity, underlyingMap.transform);
        dynamicTrapGameObjs.Add(newTrapNode);
    }

    protected (ulong, ulong) getOrPrefabInputFrameUpsync(int inputFrameId, bool canConfirmSelf, ulong[] prefabbedInputList) {
        if (null == selfPlayerInfo) {
            string msg = String.Format("noDelayInputFrameId={0:D} couldn't be generated due to selfPlayerInfo being null", inputFrameId);
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
        currSelfInput = iptmgr.GetEncodedInput(); // When "null == existingInputFrame", it'd be safe to say that "GetImmediateEncodedInput()" is for the requested "inputFrameId"
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

            Step(inputBuffer, i, roomCapacity, collisionSys, renderBuffer, ref overlapResult, ref primaryOverlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, softPushbacks, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, residueCollided, trapLocalIdToColliderAttrs, completelyStaticTrapColliders, _loggerBridge);

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
        // Debug.Log(String.Format("Mismatched input detected, resetting chaserRenderFrameId: {0}->{1}; firstPredictedYetIncorrectInputFrameId: {2}, lastAllConfirmedInputFrameId={3}, fromUDP={4}", chaserRenderFrameId, renderFrameId1, firstPredictedYetIncorrectInputFrameId, lastAllConfirmedInputFrameId, fromUDP));
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
            var prevCharacterDownsync = (null == prevRdf ? null : prevRdf.PlayersArr[k]);
            //Debug.Log(String.Format("At rdf.Id={0}, currCharacterDownsync[k:{1}] at [vGridX: {2}, vGridY: {3}, velX: {4}, velY: {5}, chState: {6}, framesInChState: {7}, dirx: {8}]", rdf.Id, k, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, currCharacterDownsync.VelX, currCharacterDownsync.VelY, currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, currCharacterDownsync.DirX));
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var playerGameObj = playerGameObjs[k];
            newPosHolder.Set(wx, wy, playerGameObj.transform.position.z);
            playerGameObj.transform.position = newPosHolder;

            var chConfig = characters[currCharacterDownsync.SpeciesId];
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.updateCharacterAnim(currCharacterDownsync, prevCharacterDownsync, false, chConfig);
            newPosHolder.Set(wx + teamRibbonOffset.x, wy + .5f * chConfig.DefaultSizeY * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO, inplaceHpBarZ);
            chAnimCtrl.teamRibbon.transform.localPosition = newPosHolder;
            if (currCharacterDownsync.JoinIndex == selfPlayerInfo.JoinIndex) {
                selfBattleHeading.SetCharacter(currCharacterDownsync);
                //newPosHolder.Set(wx, wy, playerGameObj.transform.position.z);
                //selfPlayerLights.gameObject.transform.position = newPosHolder;
                //selfPlayerLights.setDirX(currCharacterDownsync.DirX);
            } else {
                newPosHolder.Set(wx + inplaceHpBarOffset.x, wy + .5f * chConfig.DefaultSizeY * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO, inplaceHpBarZ);
                chAnimCtrl.hpBar.updateHp((float)currCharacterDownsync.Hp / currCharacterDownsync.MaxHp, (float)currCharacterDownsync.Mp / currCharacterDownsync.MaxMp);
                chAnimCtrl.hpBar.transform.localPosition = newPosHolder;
            }
        }

        for (int k = 0; k < rdf.NpcsArr.Count; k++) {
            var currNpcDownsync = rdf.NpcsArr[k];
            var prevNpcDownsync = (null == prevRdf ? null : prevRdf.NpcsArr[k]);

            if (TERMINATING_PLAYER_ID == currNpcDownsync.Id) break;
            // Debug.Log(String.Format("At rdf.Id={0}, currNpcDownsync[k:{1}] at [vx: {2}, vy: {3}, chState: {4}, framesInChState: {5}]", rdf.Id, k, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY, currNpcDownsync.CharacterState, currNpcDownsync.FramesInChState));
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var npcGameObj = npcGameObjs[k];
            newPosHolder.Set(wx, wy, npcGameObj.transform.position.z);
            npcGameObj.transform.position = newPosHolder;

            var chConfig = characters[currNpcDownsync.SpeciesId];
            var chAnimCtrl = npcGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.updateCharacterAnim(currNpcDownsync, prevNpcDownsync, false, chConfig);
            newPosHolder.Set(wx + inplaceHpBarOffset.x, wy + .5f * chConfig.DefaultSizeY * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO, inplaceHpBarZ);
            chAnimCtrl.hpBar.transform.localPosition = newPosHolder;
            chAnimCtrl.hpBar.updateHp((float)currNpcDownsync.Hp / currNpcDownsync.MaxHp, (float)currNpcDownsync.Mp / currNpcDownsync.MaxMp);

            newPosHolder.Set(wx + teamRibbonOffset.x, wy + .5f * chConfig.DefaultSizeY * VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO, inplaceHpBarZ);
            chAnimCtrl.teamRibbon.transform.localPosition = newPosHolder;
        }

        int kDynamicTrap = 0;
        for (int k = 0; k < rdf.TrapsArr.Count; k++) {
            var currTrap = rdf.TrapsArr[k];
            if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) break;
            if (currTrap.IsCompletelyStatic) continue;
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currTrap.VirtualGridX, currTrap.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var dynamicTrapObj = dynamicTrapGameObjs[kDynamicTrap];
            newPosHolder.Set(wx, wy, dynamicTrapObj.transform.position.z);
            dynamicTrapObj.transform.position = newPosHolder;
            var chAnimCtrl = dynamicTrapObj.GetComponent<TrapAnimationController>();
            chAnimCtrl.updateAnim("Tidle", 0, 0, true); // TODO: remove the hardcoded value
            kDynamicTrap++;
        }

        // Put all to infinitely far first
        for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
            var (res, fireballHolder) = cachedFireballs.vals.GetByFrameId(i);
            if (!res || null == fireballHolder) throw new ArgumentNullException(String.Format("There's no fireballHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));

            fireballHolder.gameObject.transform.position = new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballHolder.gameObject.transform.position.z);
        }

        for (int k = 0; k < rdf.Bullets.Count; k++) {
            var bullet = rdf.Bullets[k];
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BattleAttr.BulletLocalId) break;
            bool isExploding = IsBulletExploding(bullet);
            bool isInMultiHitSubsequence = (0 < bullet.BattleAttr.ActiveSkillHit);
            string lookupKey = bullet.BattleAttr.BulletLocalId.ToString(), animName = null;
            var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);
            bool spontaneousLooping = false;
            switch (bullet.Config.BType) {
                case BulletType.Melee:
                    if (isExploding) {
                        animName = String.Format("Melee_Explosion{0}", bullet.Config.SpeciesId);
                    }
                    break;
                case BulletType.Fireball:
                    if (IsBulletActive(bullet, rdf.Id) || isInMultiHitSubsequence || isExploding) {
                        animName = isExploding ? String.Format("Explosion{0}", bullet.Config.SpeciesId) : String.Format("Fireball{0}", bullet.Config.SpeciesId);
                        spontaneousLooping = !isExploding;
                    }
                    break;
                default:
                    break;
            }
            if (null == animName) continue;
            var explosionAnimHolder = cachedFireballs.PopAny(lookupKey);
            if (null == explosionAnimHolder) {
                explosionAnimHolder = cachedFireballs.Pop();
                //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a new fireball node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a cached node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
            }

            if (null == explosionAnimHolder) {
                throw new ArgumentNullException(String.Format("No available fireball node for lookupKey={0}, animName={1}", lookupKey, animName));
            }
            explosionAnimHolder.updateAnim(animName, bullet.FramesInBlState, bullet.DirX, spontaneousLooping, bullet.Config, rdf);
            explosionAnimHolder.score = rdf.Id;
            newPosHolder.Set(wx, wy, explosionAnimHolder.gameObject.transform.position.z);
            explosionAnimHolder.gameObject.transform.position = newPosHolder;

            cachedFireballs.Put(lookupKey, explosionAnimHolder);
        }
    }

    protected void preallocateHolders() {
        if (0 >= roomCapacity) {
            throw new ArgumentException(String.Format("roomCapacity={0} is non-positive, please initialize it first!", roomCapacity));
        }

        if (0 >= preallocNpcCapacity) {
            throw new ArgumentException(String.Format("preallocAiPlayerCapacity={0} is non-positive, please initialize it first!", preallocNpcCapacity));
        }

        Debug.Log(String.Format("preallocateHolders with roomCapacity={0}, preallocAiPlayerCapacity={1}, preallocBulletCapacity={2}", roomCapacity, preallocNpcCapacity, preallocBulletCapacity));
        int residueCollidedCap = 128;
        residueCollided = new FrameRingBuffer<shared.Collider>(residueCollidedCap);

        renderBufferSize = 1024;
        renderBuffer = new FrameRingBuffer<RoomDownsyncFrame>(renderBufferSize);
        for (int i = 0; i < renderBufferSize; i++) {
            renderBuffer.Put(NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity));
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

        effPushbacks = new Vector[roomCapacity + preallocNpcCapacity + preallocTrapCapacity];
        for (int i = 0; i < effPushbacks.Length; i++) {
            effPushbacks[i] = new Vector(0, 0);
        }
        hardPushbackNormsArr = new Vector[roomCapacity + preallocNpcCapacity + preallocTrapCapacity][];
        for (int i = 0; i < hardPushbackNormsArr.Length; i++) {
            int cap = 5;
            hardPushbackNormsArr[i] = new Vector[cap];
            for (int j = 0; j < cap; j++) {
                hardPushbackNormsArr[i][j] = new Vector(0, 0);
            }
        }
        int softPushbacksCap = 16;
        softPushbacks = new Vector[softPushbacksCap];
        for (int i = 0; i < softPushbacks.Length; i++) {
            softPushbacks[i] = new Vector(0, 0);
        }

        int dynamicRectangleCollidersCap = 64;
        dynamicRectangleColliders = new shared.Collider[dynamicRectangleCollidersCap];
        for (int i = 0; i < dynamicRectangleCollidersCap; i++) {
            dynamicRectangleColliders[i] = NewRectCollider(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null);
        }
        staticColliders = new shared.Collider[128];

        decodedInputHolder = new InputFrameDecoded();
        prevDecodedInputHolder = new InputFrameDecoded();

        int fireballHoldersCap = 64;
        if (null != cachedFireballs) {
            for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
                var (res, fireball) = cachedFireballs.vals.GetByFrameId(i);
                if (!res || null == fireball) throw new ArgumentNullException(String.Format("There's no cachedFireball for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));
                Destroy(fireball.gameObject);
            }
        }
        cachedFireballs = new KvPriorityQueue<string, FireballAnimController>(fireballHoldersCap, cachedFireballScore);

        effectivelyInfinitelyFar = 4f * Math.Max(spaceOffsetX, spaceOffsetY);
        for (int i = 0; i < fireballHoldersCap; i++) {
            // Fireballs & explosions should be drawn above any character
            GameObject newFireballNode = Instantiate(fireballPrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ), Quaternion.identity);
            FireballAnimController holder = newFireballNode.GetComponent<FireballAnimController>();
            holder.score = -1;
            string initLookupKey = (-(i + 1)).ToString(); // there's definitely no such "bulletLocalId"
            cachedFireballs.Put(initLookupKey, holder);
        }

        int lineHoldersCap = 192;
        if (null != cachedLineRenderers) {
            for (int i = cachedLineRenderers.vals.StFrameId; i < cachedLineRenderers.vals.EdFrameId; i++) {
                var (res, line) = cachedLineRenderers.vals.GetByFrameId(i);
                if (!res || null == line) throw new ArgumentNullException(String.Format("There's no line for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedLineRenderers.vals.StFrameId, cachedLineRenderers.vals.EdFrameId));
                Destroy(line.gameObject);
            }
        }

        cachedLineRenderers = new KvPriorityQueue<string, DebugLine>(lineHoldersCap, cachedLineScore);
        for (int i = 0; i < lineHoldersCap; i++) {
            GameObject newLineObj = Instantiate(linePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, lineRendererZ), Quaternion.identity);
            DebugLine newLine = newLineObj.GetComponent<DebugLine>();
            newLine.score = -1;
            newLine.SetWidth(2.0f);
            var initLookupKey = i.ToString();
            cachedLineRenderers.Put(initLookupKey, newLine);
        }
    }

    protected virtual void resetCurrentMatch(string theme) {
        Debug.Log(String.Format("resetCurrentMatch with roomCapacity={0}", roomCapacity));
        battleState = ROOM_STATE_IMPOSSIBLE;
        renderFrameId = 0;
        renderFrameIdLagTolerance = 4;
        chaserRenderFrameId = -1;
        lastAllConfirmedInputFrameId = -1;
        lastUpsyncInputFrameId = -1;
        maxChasingRenderFramesPerUpdate = 5;
        rdfIdToActuallyUsedInput = new Dictionary<int, InputFrameDownsync>();
        trapLocalIdToColliderAttrs = new Dictionary<int, List<TrapColliderAttr>>();
        completelyStaticTrapColliders = new List<shared.Collider>();

        if (null != underlyingMap) {
            Destroy(underlyingMap);
        }
        playerGameObjs = new GameObject[roomCapacity];
        npcGameObjs = new List<GameObject>();
        dynamicTrapGameObjs = new List<GameObject>();

        string path = String.Format("Tiled/{0}/map", theme);
        var underlyingMapPrefab = Resources.Load(path) as GameObject;
        underlyingMap = GameObject.Instantiate(underlyingMapPrefab);

        var superMap = underlyingMap.GetComponent<SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        int cellWidth = 64;
        int cellHeight = 256; // To avoid dynamic trap as a standing point to slip when moving down along with the character
        collisionSys = new CollisionSpace(spaceOffsetX * 2, spaceOffsetY * 2, cellWidth, cellHeight);
        collisionHolder = new shared.Collision();

        // Reset the preallocated
        Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        renderBuffer.Clear();
        inputBuffer.Clear();
        residueCollided.Clear();
        trapLocalIdToColliderAttrs.Clear();
        Array.Fill<ulong>(prefabbedInputListHolder, 0);

        readyGoPanel.resetCountdown();
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
        cameraTrack(rdf);
        ++renderFrameId;
    }

    protected virtual void onBattleStopped() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        battleState = ROOM_STATE_IN_SETTLEMENT;

        BattleInputManager iptmgr = this.gameObject.GetComponent<BattleInputManager>();
        iptmgr.reset();
    }

    protected abstract bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId);

    protected abstract void sendInputFrameUpsyncBatch(int latestLocalInputFrameId);

    protected void enableBattleInput(bool yesOrNo) {
        BattleInputManager iptmgr = this.gameObject.GetComponent<BattleInputManager>();
        iptmgr.enable(yesOrNo);
    }

    protected RoomDownsyncFrame mockStartRdf(int[] speciesIdList) {
        var grid = underlyingMap.GetComponentInChildren<Grid>();
        var playerStartingCposList = new List<(Vector, int, int)>();
        var npcsStartingCposList = new List<(Vector, int, int, int, int, bool)>();
        var trapList = new List<Trap>();
        float defaultPatrolCueRadius = 10;
        int staticColliderIdx = 0;
        int trapLocalId = 0;
        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "Barrier":
                    foreach (Transform barrierChild in child) {
                        var barrierTileObj = barrierChild.gameObject.GetComponent<SuperObject>();
                        var inMapCollider = barrierChild.gameObject.GetComponent<EdgeCollider2D>();

                        if (null == inMapCollider || 0 >= inMapCollider.pointCount) {
                            var (tiledRectCx, tiledRectCy) = (barrierTileObj.m_X + barrierTileObj.m_Width * 0.5f, barrierTileObj.m_Y + barrierTileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            /*
                             [WARNING] 

                            The "Unity World (0, 0)" is aligned with the top-left corner of the rendered "TiledMap (via SuperMap)".

                            It's noticeable that all the "Collider"s in "CollisionSpace" must be of positive coordinates to work due to the implementation details of "resolv". Thus I'm using a "Collision Space (0, 0)" aligned with the bottom-left of the rendered "TiledMap (via SuperMap)". 
                            */
                            var barrierCollider = NewRectCollider(rectCx, rectCy, barrierTileObj.m_Width, barrierTileObj.m_Height, 0, 0, 0, 0, 0, 0, null);
                            //Debug.Log(String.Format("new barrierCollider=[X: {0}, Y: {1}, Width: {2}, Height: {3}]", barrierCollider.X, barrierCollider.Y, barrierCollider.W, barrierCollider.H));
                            collisionSys.AddSingle(barrierCollider);
                            staticColliders[staticColliderIdx++] = barrierCollider;
                        } else {
                            var points = inMapCollider.points;
                            List<float> points2 = new List<float>();
                            foreach (var point in points) {
                                points2.Add(point.x);
                                points2.Add(point.y);
                            }
                            var (anchorCx, anchorCy) = TiledLayerPositionToCollisionSpacePosition(barrierTileObj.m_X, barrierTileObj.m_Y, spaceOffsetX, spaceOffsetY);
                            var srcPolygon = new ConvexPolygon(anchorCx, anchorCy, points2.ToArray());
                            var barrierCollider = NewConvexPolygonCollider(srcPolygon, 0, 0, null);

                            collisionSys.AddSingle(barrierCollider);
                            staticColliders[staticColliderIdx++] = barrierCollider;
                        }

                        // TODO: By now I have to enable the import of all colliders to see the "inMapCollider: EdgeCollider2D" component, then remove unused components here :(
                        Destroy(barrierChild.gameObject.GetComponent<EdgeCollider2D>());
                        Destroy(barrierChild.gameObject.GetComponent<BoxCollider2D>());
                        Destroy(barrierChild.gameObject.GetComponent<SuperColliderComponent>());
                    }
                    break;
                case "PlayerStartingPos":
                    int j = 0;
                    foreach (Transform playerPos in child) {
                        var posTileObj = playerPos.gameObject.GetComponent<SuperObject>();
                        var tileProps = playerPos.gameObject.gameObject.GetComponent<SuperCustomProperties>();
                        CustomProperty teamId, dirX;
                        tileProps.TryGetCustomProperty("teamId", out teamId);
                        tileProps.TryGetCustomProperty("dirX", out dirX);

                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(posTileObj.m_X, posTileObj.m_Y, spaceOffsetX, spaceOffsetY);

                        playerStartingCposList.Add((
                            new Vector(cx, cy),
                            null == teamId || teamId.IsEmpty ? DEFAULT_BULLET_TEAM_ID : teamId.GetValueAsInt(),
                            null == dirX || dirX.IsEmpty ? +2 : dirX.GetValueAsInt()
                        ));
                        //Debug.Log(String.Format("new playerStartingCposList[i:{0}]=[X:{1}, Y:{2}]", j, cx, cy));
                        j++;
                    }
                    break;
                case "NpcStartingPos":
                    foreach (Transform npcPos in child) {
                        var tileObj = npcPos.gameObject.GetComponent<SuperObject>();
                        var tileProps = npcPos.gameObject.gameObject.GetComponent<SuperCustomProperties>();
                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        CustomProperty dirX, dirY, speciesId, teamId, isStatic;
                        tileProps.TryGetCustomProperty("dirX", out dirX);
                        tileProps.TryGetCustomProperty("dirY", out dirY);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("teamId", out teamId);
                        tileProps.TryGetCustomProperty("static", out isStatic);
                        npcsStartingCposList.Add((
                                                    new Vector(cx, cy),
                                                    null == dirX || dirX.IsEmpty ? 0 : dirX.GetValueAsInt(),
                                                    null == dirY || dirY.IsEmpty ? 0 : dirY.GetValueAsInt(),
                                                    null == speciesId || speciesId.IsEmpty ? 0 : speciesId.GetValueAsInt(),
                                                    null == teamId || teamId.IsEmpty ? DEFAULT_BULLET_TEAM_ID : teamId.GetValueAsInt(),
                                                    null == isStatic || isStatic.IsEmpty ? false : (1 == isStatic.GetValueAsInt())
                        ));
                    }
                    break;
                case "PatrolCue":
                    foreach (Transform patrolCueChild in child) {
                        var tileObj = patrolCueChild.gameObject.GetComponent<SuperObject>();
                        var tileProps = patrolCueChild.gameObject.GetComponent<SuperCustomProperties>();

                        var (patrolCueCx, patrolCueCy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);

                        CustomProperty id, flAct, frAct, flCaptureFrames, frCaptureFrames, fdAct, fuAct, fdCaptureFrames, fuCaptureFrames, collisionTypeMask;
                        tileProps.TryGetCustomProperty("id", out id);
                        tileProps.TryGetCustomProperty("flAct", out flAct);
                        tileProps.TryGetCustomProperty("frAct", out frAct);
                        tileProps.TryGetCustomProperty("flCaptureFrames", out flCaptureFrames);
                        tileProps.TryGetCustomProperty("frCaptureFrames", out frCaptureFrames);
                        tileProps.TryGetCustomProperty("fdAct", out fdAct);
                        tileProps.TryGetCustomProperty("fuAct", out fuAct);
                        tileProps.TryGetCustomProperty("fdCaptureFrames", out fdCaptureFrames);
                        tileProps.TryGetCustomProperty("fuCaptureFrames", out fuCaptureFrames);
                        tileProps.TryGetCustomProperty("collisionTypeMask", out collisionTypeMask);

                        ulong collisionTypeMaskVal = (null != collisionTypeMask && !collisionTypeMask.IsEmpty) ? (ulong)collisionTypeMask.GetValueAsInt() : COLLISION_NPC_PATROL_CUE_INDEX_PREFIX;

                        var newPatrolCue = new PatrolCue {
                            Id = (null == id || id.IsEmpty) ? NO_PATROL_CUE_ID : id.GetValueAsInt(),
                            FlAct = (null == flAct || flAct.IsEmpty) ? 0 : (ulong)flAct.GetValueAsInt(),
                            FrAct = (null == frAct || frAct.IsEmpty) ? 0 : (ulong)frAct.GetValueAsInt(),
                            FlCaptureFrames = (null == flCaptureFrames || flCaptureFrames.IsEmpty) ? 0 : (ulong)flCaptureFrames.GetValueAsInt(),
                            FrCaptureFrames = (null == frCaptureFrames || frCaptureFrames.IsEmpty) ? 0 : (ulong)frCaptureFrames.GetValueAsInt(),

                            FdAct = (null == fdAct || fdAct.IsEmpty) ? 0 : (ulong)fdAct.GetValueAsInt(),
                            FuAct = (null == fuAct || fuAct.IsEmpty) ? 0 : (ulong)fuAct.GetValueAsInt(),
                            FdCaptureFrames = (null == fdCaptureFrames || fdCaptureFrames.IsEmpty) ? 0 : (ulong)fdCaptureFrames.GetValueAsInt(),
                            FuCaptureFrames = (null == fuCaptureFrames || fuCaptureFrames.IsEmpty) ? 0 : (ulong)fuCaptureFrames.GetValueAsInt(),
                            CollisionTypeMask = collisionTypeMaskVal
                        };

                        var patrolCueCollider = NewRectCollider(patrolCueCx, patrolCueCy, 2 * defaultPatrolCueRadius, 2 * defaultPatrolCueRadius, 0, 0, 0, 0, 0, 0, newPatrolCue);
                        collisionSys.AddSingle(patrolCueCollider);
                        staticColliders[staticColliderIdx++] = patrolCueCollider;
                        //Debug.Log(String.Format("newPatrolCue={0} at [X:{1}, Y:{2}]", newPatrolCue, patrolCueCx, patrolCueCy));
                    }
                    break;
                case "TrapStartingPos":
                    foreach (Transform trapChild in child) {
                        var tileObj = trapChild.gameObject.GetComponent<SuperObject>();
                        var tileProps = trapChild.gameObject.GetComponent<SuperCustomProperties>();

                        CustomProperty speciesId, providesHardPushback, providesDamage, isCompletelyStatic, collisionTypeMask, dirX, dirY, speed;
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("providesHardPushback", out providesHardPushback);
                        tileProps.TryGetCustomProperty("providesDamage", out providesDamage);
                        tileProps.TryGetCustomProperty("static", out isCompletelyStatic);
                        tileProps.TryGetCustomProperty("dirX", out dirX);
                        tileProps.TryGetCustomProperty("dirY", out dirY);
                        tileProps.TryGetCustomProperty("speed", out speed);

                        int speciesIdVal = speciesId.GetValueAsInt(); // Not checking null or empty for this property because it shouldn't be, and in case it comes empty anyway, this automatically throws an error 
                        bool providesHardPushbackVal = (null != providesHardPushback && !providesHardPushback.IsEmpty && 1 == providesHardPushback.GetValueAsInt()) ? true : false;
                        bool providesDamageVal = (null != providesDamage && !providesDamage.IsEmpty && 1 == providesDamage.GetValueAsInt()) ? true : false;
                        bool isCompletelyStaticVal = (null != isCompletelyStatic && !isCompletelyStatic.IsEmpty && 1 == isCompletelyStatic.GetValueAsInt()) ? true : false;

                        int dirXVal = (null == dirX || dirX.IsEmpty ? 0 : dirX.GetValueAsInt());
                        int dirYVal = (null == dirY || dirY.IsEmpty ? 0 : dirY.GetValueAsInt());
                        int speedVal = (null == speed || speed.IsEmpty ? 0 : speed.GetValueAsInt());

                        var trapDirMagSq = dirXVal * dirXVal + dirYVal * dirYVal;
                        var invTrapDirMag = InvSqrt32(trapDirMagSq);
                        var trapSpeedXfac = invTrapDirMag * dirXVal;
                        var trapSpeedYfac = invTrapDirMag * dirYVal;

                        int trapVelX = (int)(trapSpeedXfac * speedVal); 
                        int trapVelY = (int)(trapSpeedYfac * speedVal);

                        TrapConfig trapConfig = trapConfigs[speciesIdVal];
                        TrapConfigFromTiled trapConfigFromTiled = new TrapConfigFromTiled {
                            SpeciesId = speciesIdVal,
                            Quota = MAGIC_QUOTA_INFINITE,
                            Speed = speedVal,
                            DirX = dirXVal,
                            DirY = dirYVal
                        };

                        tileProps.TryGetCustomProperty("collisionTypeMask", out collisionTypeMask);
                        ulong collisionTypeMaskVal = (null != collisionTypeMask && !collisionTypeMask.IsEmpty) ? (ulong)collisionTypeMask.GetValueAsInt() : 0;

                        List<TrapColliderAttr> colliderAttrs = new List<TrapColliderAttr>();
                        if (isCompletelyStaticVal) {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            var (rectVw, rectVh) = PolygonColliderCtrToVirtualGridPos(tileObj.m_Width, tileObj.m_Height);
                            var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);

                            Trap trap = new Trap {
                                TrapLocalId = trapLocalId,
                                Config = trapConfig,
                                ConfigFromTiled = trapConfigFromTiled,
                                VirtualGridX = rectCenterVx,
                                VirtualGridY = rectCenterVy,
                                DirX = dirXVal,
                                DirY = dirYVal,
                                VelX = trapVelX,
                                VelY = trapVelY,
                                IsCompletelyStatic = true
                            };

                            TrapColliderAttr colliderAttr = new TrapColliderAttr {
                                ProvidesDamage = providesDamageVal,
                                ProvidesHardPushback = providesHardPushbackVal,
                                HitboxOffsetX = 0,
                                HitboxOffsetY = 0,
                                HitboxSizeX = rectVw,
                                HitboxSizeY = rectVh,
                                CollisionTypeMask = collisionTypeMaskVal,
                                TrapLocalId = trapLocalId
                            };

                            colliderAttrs.Add(colliderAttr);
                            trapLocalIdToColliderAttrs[trapLocalId] = colliderAttrs;

                            var trapCollider = NewRectCollider(rectCx, rectCy, tileObj.m_Width, tileObj.m_Height, 0, 0, 0, 0, 0, 0, colliderAttr);

                            collisionSys.AddSingle(trapCollider);
                            completelyStaticTrapColliders.Add(trapCollider);
                            trapList.Add(trap);
                            staticColliders[staticColliderIdx++] = trapCollider;

                            Debug.Log(String.Format("new completely static trap created {0}", trap));
                        } else {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                            Trap trap = new Trap {
                                TrapLocalId = trapLocalId,
                                Config = trapConfig,
                                ConfigFromTiled = trapConfigFromTiled,
                                VirtualGridX = rectCenterVx,
                                VirtualGridY = rectCenterVy,
                                DirX = dirXVal,
                                DirY = dirYVal,
                                VelX = trapVelX,
                                VelY = trapVelY,
                                IsCompletelyStatic = false
                            };
                            var collisionObjs = tileObj.m_SuperTile.m_CollisionObjects;
                            foreach (var collisionObj in collisionObjs) {
                                bool childProvidesHardPushbackVal = false, childProvidesDamageVal = false;
                                foreach (var collisionObjProp in collisionObj.m_CustomProperties) {
                                    if ("providesHardPushback".Equals(collisionObjProp.m_Name)) {
                                        childProvidesHardPushbackVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                    }
                                    if ("providesDamage".Equals(collisionObjProp.m_Name)) {
                                        childProvidesDamageVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                    }
                                    if ("collisionTypeMask".Equals(collisionObjProp.m_Name) && !collisionObjProp.IsEmpty) {
                                        collisionTypeMaskVal =  (ulong)collisionObjProp.GetValueAsInt();
                                    }
                                }

                                // [WARNING] The offset (0, 0) of the tileObj within TSX is the top-left corner, but SuperTiled2Unity converted that to bottom-left corner and reverted y-axis by itself... 
                                var (hitboxOffsetCx, hitboxOffsetCy) = (-tileObj.m_Width * 0.5f + collisionObj.m_Position.x + collisionObj.m_Size.x * 0.5f,  collisionObj.m_Position.y - collisionObj.m_Size.y * 0.5f - tileObj.m_Height * 0.5f);
                                var (hitboxOffsetVx, hitboxOffsetVy) = PolygonColliderCtrToVirtualGridPos(hitboxOffsetCx, hitboxOffsetCy);
                                var (hitboxSizeVx, hitboxSizeVy) = PolygonColliderCtrToVirtualGridPos(collisionObj.m_Size.x, collisionObj.m_Size.y);
                                TrapColliderAttr colliderAttr = new TrapColliderAttr {
                                    ProvidesDamage = childProvidesDamageVal,
                                    ProvidesHardPushback = childProvidesHardPushbackVal,
                                    HitboxOffsetX = hitboxOffsetVx,
                                    HitboxOffsetY = hitboxOffsetVy,
                                    HitboxSizeX = hitboxSizeVx,
                                    HitboxSizeY = hitboxSizeVy,
                                    CollisionTypeMask = collisionTypeMaskVal,
                                    TrapLocalId = trapLocalId
                                };
                                colliderAttrs.Add(colliderAttr);
                            }
                            trapLocalIdToColliderAttrs[trapLocalId] = colliderAttrs;
                            trapList.Add(trap);
                            Destroy(child.gameObject); // [WARNING] It'll be animated by "TrapPrefab" in "applyRoomDownsyncFrame" instead!
                        }
                        trapLocalId++;
                    }
                    break;
                default:
                    break;
            }
        }

        // Sorting to make sure that if "roomCapacity" is smaller than the position counts in Tiled, we take only the smaller teamIds
        playerStartingCposList.Sort(delegate ((Vector, int, int) lhs, (Vector, int, int) rhs) {
            return Math.Sign(lhs.Item2 - rhs.Item2);
        });

        var startRdf = NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity);
        startRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START;
        startRdf.ShouldForceResync = false;
        for (int i = 0; i < roomCapacity; i++) {
            int joinIndex = i + 1;
            var (cpos, teamId, dirX) = playerStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            teamId = (DEFAULT_BULLET_TEAM_ID == teamId ? joinIndex : teamId);
            var playerInRdf = startRdf.PlayersArr[i];
            playerInRdf.SpeciesId = speciesIdList[i];
            playerInRdf.JoinIndex = joinIndex;
            playerInRdf.BulletTeamId = teamId;
            playerInRdf.ChCollisionTeamId = teamId; // If we want to stand on certain teammates' shoulder, then this value should be tuned accordingly. 
            var chConfig = Battle.characters[playerInRdf.SpeciesId];
            spawnPlayerNode(joinIndex, playerInRdf.SpeciesId, wx, wy, playerInRdf.BulletTeamId);
            var (playerVposX, playerVposY) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y); // World and CollisionSpace coordinates have the same scale, just translated
            playerInRdf.VirtualGridX = playerVposX;
            playerInRdf.VirtualGridY = playerVposY;
            playerInRdf.RevivalVirtualGridX = playerVposX;
            playerInRdf.RevivalVirtualGridY = playerVposY;
            playerInRdf.RevivalDirX = dirX;
            playerInRdf.RevivalDirY = 0;
            playerInRdf.Speed = chConfig.Speed;
            playerInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
            playerInRdf.FramesToRecover = 0;
            playerInRdf.DirX = dirX;
            playerInRdf.DirY = 0;
            playerInRdf.VelX = 0;
            playerInRdf.VelY = 0;
            playerInRdf.InAir = true;
            playerInRdf.OnWall = false;
            playerInRdf.Hp = chConfig.Hp;
            playerInRdf.MaxHp = chConfig.Hp;
            playerInRdf.Mp = 1000;
            playerInRdf.MaxMp = 1000;
            playerInRdf.CollisionTypeMask = COLLISION_CHARACTER_INDEX_PREFIX;
        }

        for (int i = 0; i < npcsStartingCposList.Count; i++) {
            int joinIndex = roomCapacity + i + 1;
            var (cpos, dirX, dirY, characterSpeciesId, teamId, isStatic) = npcsStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            spawnNpcNode(characterSpeciesId, wx, wy, teamId);

            var chConfig = Battle.characters[characterSpeciesId];

            var npcInRdf = new CharacterDownsync();
            var (vx, vy) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y);
            npcInRdf.Id = 0; // Just for not being excluded 
            npcInRdf.JoinIndex = joinIndex;
            npcInRdf.VirtualGridX = vx;
            npcInRdf.VirtualGridY = vy;
            npcInRdf.RevivalVirtualGridX = vx;
            npcInRdf.RevivalVirtualGridY = vy;
            npcInRdf.RevivalDirX = dirX;
            npcInRdf.RevivalDirY = dirY;
            npcInRdf.Speed = chConfig.Speed;
            npcInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
            npcInRdf.FramesToRecover = 0;
            npcInRdf.DirX = dirX;
            npcInRdf.DirY = dirY;
            npcInRdf.VelX = 0;
            npcInRdf.VelY = 0;
            npcInRdf.InAir = true;
            npcInRdf.OnWall = false;
            npcInRdf.Hp = 100;
            npcInRdf.MaxHp = 100;
            npcInRdf.Mp = 1000;
            npcInRdf.MaxMp = 1000;
            npcInRdf.SpeciesId = characterSpeciesId;
            npcInRdf.BulletTeamId = teamId;
            npcInRdf.ChCollisionTeamId = teamId;
            npcInRdf.CollisionTypeMask = COLLISION_CHARACTER_INDEX_PREFIX;
            npcInRdf.WaivingSpontaneousPatrol = isStatic;
            npcInRdf.OmitGravity = chConfig.OmitGravity;
            npcInRdf.OmitPushback = chConfig.OmitPushback;
            startRdf.NpcsArr[i] = npcInRdf;
        }

        for (int i = 0; i < trapList.Count; i++) {
            var trap = trapList[i];
            startRdf.TrapsArr[i] = trap;
            if (trap.IsCompletelyStatic) continue;
            spawnDynamicTrapNode(trap.Config.SpeciesId, trap.VirtualGridX, trap.VirtualGridY);
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


    protected Vector2 camSpeedHolder = new Vector2();
    protected Vector2 camDiffDstHolder = new Vector2();
    protected void cameraTrack(RoomDownsyncFrame rdf) {
        if (null == selfPlayerInfo) return;
        var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
        var playerCharacterDownsync = rdf.PlayersArr[selfPlayerInfo.JoinIndex - 1];
        var (velCX, velCY) = VirtualGridToPolygonColliderCtr(playerCharacterDownsync.Speed, playerCharacterDownsync.Speed);
        camSpeedHolder.Set(velCX, velCY);
        var cameraSpeedInWorld = camSpeedHolder.magnitude * 100;
        var camOldPos = Camera.main.transform.position;
        var dst = playerGameObj.transform.position;
        camDiffDstHolder.Set(dst.x - camOldPos.x, dst.y - camOldPos.y);

        //Debug.Log(String.Format("cameraTrack, camOldPos={0}, dst={1}, deltaTime={2}", camOldPos, dst, Time.deltaTime));
        var stepLength = Time.deltaTime * cameraSpeedInWorld;
        if (stepLength > camDiffDstHolder.magnitude) {
            newPosHolder.Set(dst.x, dst.y, camOldPos.z);
            Camera.main.transform.position = newPosHolder;
        } else {
            var newMapPosDiff2 = camDiffDstHolder.normalized * stepLength;
            newPosHolder.Set(camOldPos.x + newMapPosDiff2.x, camOldPos.y + newMapPosDiff2.y, camOldPos.z);
            Camera.main.transform.position = newPosHolder;
        }
    }

    protected void resetLine(DebugLine line) {
        newPosHolder.x = 0;
        newPosHolder.y = 0;
        line.transform.position = newPosHolder;
        line.GetPositions(debugDrawPositionsHolder);
        (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = (0, 0);
        (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = (0, 0);
        (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = (0, 0);
        (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = (0, 0);
        line.SetPositions(debugDrawPositionsHolder);
    }

    protected void urpDrawDebug() {
        if (!debugDrawingEnabled) {
            return;
        }
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        for (int i = cachedLineRenderers.vals.StFrameId; i < cachedLineRenderers.vals.EdFrameId; i++) {
            var (res, line) = cachedLineRenderers.vals.GetByFrameId(i);
            if (!res || null == line) throw new ArgumentNullException(String.Format("There's no line for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedLineRenderers.vals.StFrameId, cachedLineRenderers.vals.EdFrameId));

            resetLine(line);
        }
        var (_, rdf) = renderBuffer.GetByFrameId(renderFrameId);
        if (null == rdf) return;

        // Draw static colliders
        int lineIndex = 0;
        foreach (var collider in staticColliders) {
            if (null == collider) {
                break;
            }
            if (null == collider.Shape) {
                throw new ArgumentNullException("barrierCollider.Shape is null when drawing staticRectangleColliders");
            }
            if (null == collider.Shape.Points) {
                throw new ArgumentNullException("barrierCollider.Shape.Points is null when drawing staticRectangleColliders");
            }
            string key = "Static-" + lineIndex.ToString();
            lineIndex++;
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.white);
            if (null != collider.Data) {
#nullable enable
                TrapColliderAttr? colliderAttr = collider.Data as TrapColliderAttr;
                if (null != colliderAttr) {
                    if (colliderAttr.ProvidesHardPushback) {
                        line.SetColor(Color.green);
                    } else if (colliderAttr.ProvidesDamage) {
                        line.SetColor(Color.red);
                    }
                }
#nullable disable
            }
            int m = collider.Shape.Points.Cnt;
            line.GetPositions(debugDrawPositionsHolder);
            for (int i = 0; i < m; i++) {
                var (_, pi) = collider.Shape.Points.GetByOffset(i);
                (debugDrawPositionsHolder[i].x, debugDrawPositionsHolder[i].y) = CollisionSpacePositionToWorldPosition(collider.X + pi.X, collider.Y + pi.Y, spaceOffsetX, spaceOffsetY);
            }
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);
        }

        // Draw dynamic colliders
        for (int k = 0; k < roomCapacity; k++) {
            var currCharacterDownsync = rdf.PlayersArr[k];
            string key = "Player-" + currCharacterDownsync.JoinIndex.ToString();
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.white);
            line.GetPositions(debugDrawPositionsHolder);
            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);

            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
            // World space width and height are just the same as that of collision space.

            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);
        }

        for (int k = 0; k < rdf.NpcsArr.Count; k++) {
            var currCharacterDownsync = rdf.NpcsArr[k];
            if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
            string key = "Npc-" + currCharacterDownsync.JoinIndex.ToString();
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.gray);
            line.GetPositions(debugDrawPositionsHolder);
            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);

            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);

            string keyVision = "NpcVision-" + currCharacterDownsync.JoinIndex.ToString();
            var lineVision = cachedLineRenderers.PopAny(keyVision);
            if (null == lineVision) {
                lineVision = cachedLineRenderers.Pop();
            }
            if (null == lineVision) {
                throw new ArgumentNullException("Cached line is null for keyVision:" + keyVision);
            }
            lineVision.SetColor(Color.yellow);
            lineVision.GetPositions(debugDrawPositionsHolder);
            float visionCx, visionCy, visionCw, visionCh;
            calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);
            (wx, wy) = CollisionSpacePositionToWorldPosition(visionCx, visionCy, spaceOffsetX, spaceOffsetY);

            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * visionCw), (wy - 0.5f * visionCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * visionCw), (wy - 0.5f * visionCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * visionCw), (wy + 0.5f * visionCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * visionCw), (wy + 0.5f * visionCh));
            lineVision.SetPositions(debugDrawPositionsHolder);
            lineVision.score = rdf.Id;
            cachedLineRenderers.Put(keyVision, lineVision);
        }

        for (int k = 0; k < rdf.Bullets.Count; k++) {
            var bullet = rdf.Bullets[k];
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BattleAttr.BulletLocalId) break;

            string key = "Bullet-" + bullet.BattleAttr.BulletLocalId.ToString();
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            if (!IsBulletActive(bullet, rdf.Id)) {
                cachedLineRenderers.Put(key, line);
                continue;
            }
            line.SetColor(Color.red);
            line.GetPositions(debugDrawPositionsHolder);
            var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);

            var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bullet.Config.HitboxSizeX, bullet.Config.HitboxSizeY);
            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

            // Debug.Log("Active Bullet " + bullet.BattleAttr.BulletLocalId.ToString() + ": wx=" + wx.ToString() + ", wy=" + wy.ToString() + ", boxCw=" + boxCw.ToString() + ", boxCh=" + boxCh.ToString());
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);
        }

        for (int i = 0; i < rdf.TrapsArr.Count; i++) {
            var currTrap = rdf.TrapsArr[i];
            if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) continue;
            if (currTrap.IsCompletelyStatic) continue;
           
            List<TrapColliderAttr> colliderAttrs = trapLocalIdToColliderAttrs[currTrap.TrapLocalId];
            foreach (var colliderAttr in colliderAttrs) {
                float boxCx, boxCy, boxCw, boxCh;
                calcTrapBoxInCollisionSpace(colliderAttr, currTrap.VirtualGridX, currTrap.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
                
                string key = "DynamicTrap-" + currTrap.TrapLocalId.ToString() + "-" + colliderAttr.ProvidesDamage; // TODO: Use a collider ID for the last part
                var line = cachedLineRenderers.PopAny(key);
                if (null == line) {
                    line = cachedLineRenderers.Pop();
                }
                if (null == line) {
                    throw new ArgumentNullException("Cached line is null for key:" + key);
                }
                if (colliderAttr.ProvidesHardPushback) {
                    line.SetColor(Color.green);
                } else if (colliderAttr.ProvidesDamage) {
                    line.SetColor(Color.red);
                }
                
                line.GetPositions(debugDrawPositionsHolder);
                var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);

                (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
                (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

                line.SetPositions(debugDrawPositionsHolder);
                line.score = rdf.Id;
                cachedLineRenderers.Put(key, line);
            }
        }
    }
}
