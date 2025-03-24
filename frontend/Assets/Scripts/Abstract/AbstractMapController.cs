using DG.Tweening;
using Google.Protobuf.Collections;
using shared;
using SuperTiled2Unity;
using SuperTiled2Unity.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using static shared.Battle;
using static Story.StoryConstants;

public abstract class AbstractMapController : MonoBehaviour {

    protected const int KV_PREFIX_PLAYER = (1 << 7); // 128
    protected const int KV_PREFIX_PK = (KV_PREFIX_PLAYER << 1);
    protected const int KV_PREFIX_NPC = (KV_PREFIX_PK << 1);
    protected const int KV_PREFIX_TRAP = (KV_PREFIX_NPC << 1);
    protected const int KV_PREFIX_BULLET = (KV_PREFIX_TRAP << 1);
    protected const int KV_PREFIX_CHARACTER_SECONDARY = (KV_PREFIX_BULLET << 1);

    protected const int KV_PREFIX_STATIC_COLLLIDER = KV_PREFIX_BULLET; // [WARNING] There's no static bullet, so reuse this prefix value!
    protected const int KV_PREFIX_DYNAMIC_COLLLIDER_VISION = (KV_PREFIX_STATIC_COLLLIDER << 1);
    protected const int KV_PREFIX_DYNAMIC_COLLLIDER = (KV_PREFIX_DYNAMIC_COLLLIDER_VISION << 1);

    protected const int KV_PREFIX_SFX_FT = KV_PREFIX_STATIC_COLLLIDER; // Footstep, jumping, landing, pickable
    protected const int KV_PREFIX_SFX_CH_EMIT = (KV_PREFIX_SFX_FT << 1);
    protected const int KV_PREFIX_SFX_BULLET_ACTIVE = (KV_PREFIX_SFX_CH_EMIT << 1);
    protected const int KV_PREFIX_SFX_BULLET_EXPLODE = (KV_PREFIX_SFX_BULLET_ACTIVE << 1);

    protected const int KV_PREFIX_VFX = (KV_PREFIX_BULLET << 1);
    protected const int KV_PREFIX_VFX_DEF = (KV_PREFIX_VFX << 1);
    protected const int KV_PREFIX_VFX_CHARGE = (KV_PREFIX_VFX_DEF << 1);
    protected const int KV_PREFIX_VFX_CH_EMIT = (KV_PREFIX_VFX_CHARGE << 1);
    protected const int KV_PREFIX_VFX_CH_ELE_DEBUFF = (KV_PREFIX_VFX_CH_EMIT << 1);

    protected int levelId = LEVEL_NONE;
    protected int justTriggeredStoryPointId = STORY_POINT_NONE;
    protected int justTriggeredBgmId = BGM_NO_CHANGE;

    protected int roomCapacity;
    protected int maxTouchingCellsCnt;
    protected int battleDurationFrames;

    protected int preallocNpcCapacity = DEFAULT_PREALLOC_NPC_CAPACITY;
    protected int preallocBulletCapacity = DEFAULT_PREALLOC_BULLET_CAPACITY;
    protected int preallocTrapCapacity = DEFAULT_PREALLOC_TRAP_CAPACITY;
    protected int preallocTriggerCapacity = DEFAULT_PREALLOC_TRIGGER_CAPACITY;
    protected int preallocPickableCapacity = DEFAULT_PREALLOC_PICKABLE_CAPACITY;

    protected int playerRdfId; // After battle started, always increments monotonically (even upon reconnection)
    protected int settlementRdfId;
    protected int lastAllConfirmedInputFrameId;
    protected int lastUpsyncInputFrameId;
    protected int inputFrameUpsyncDelayTolerance;

    protected int chaserRenderFrameId; // at any moment, "chaserRenderFrameId <= playerRdfId", but "chaserRenderFrameId" would fluctuate according to "onInputFrameDownsyncBatch"
    protected int chaserRenderFrameIdLowerBound; // Upon force-resync, each peer receives a "ground truth RoomDownsyncFrame" from the backend, which serves as a "lower bound for the chaserRenderFrameId fluctuation"
    protected int smallChasingRenderFramesPerUpdate;
    protected int bigChasingRenderFramesPerUpdate;
    protected int renderBufferSize;
    public GameObject inplaceHpBarPrefab;
    public GameObject fireballPrefab;
    public GameObject pickablePrefab;
    public GameObject errStackLogPanelPrefab;
    public GameObject teamRibbonPrefab;
    public GameObject keyChPointerPrefab;
    public GameObject sfxSourcePrefab;
    public GameObject pixelVfxNodePrefab;
    public GameObject pixelPlasmaVfxNodePrefab;
    protected GameObject errStackLogPanelObj;
    protected GameObject underlyingMap;
    public Canvas canvas;
    public Toast toast;

    protected int[] lastIndividuallyConfirmedInputFrameId;
    protected ulong[] lastIndividuallyConfirmedInputList;
    protected PlayerMetaInfo selfPlayerInfo = null;
    protected FrameRingBuffer<RoomDownsyncFrame> renderBuffer = null;
    protected FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer = null;
    protected FrameRingBuffer<InputFrameDownsync> inputBuffer = null;
    protected FrameRingBuffer<shared.Collider> residueCollided = null;

    protected ulong[] prefabbedInputListHolder;
    protected GameObject[] playerGameObjs;
    protected CharacterAnimController[] currRdfNpcAnimHolders; // ordered by "CharacterDownsync.JoinIndex"
    protected int currRdfNpcAnimHoldersCnt;

    protected int roomId = ROOM_ID_NONE;
    protected List<GameObject> dynamicTrapGameObjs;
    protected Dictionary<int, GameObject> triggerGameObjs; // They actually don't move
    protected Dictionary<int, int> joinIndexRemap;
    protected HashSet<int> disconnectedPeerJoinIndices;
    protected HashSet<int> justDeadNpcIndices;
    protected ulong fulfilledTriggerSetMask = 0;

    protected RoomDownsyncFrame latestBossSavepoint = null;
    protected HashSet<uint> bossSpeciesSet = new HashSet<uint>();
    protected ulong bossSavepointMask = 0ul;
    protected ulong triggerForceCtrlMask = 0ul;
    protected int remainingTriggerForceCtrlRdfCount = 0;
    protected ulong latestTriggerForceCtrlCmd = 0u;

    protected long battleState;
    protected int spaceOffsetX;
    protected int spaceOffsetY;
    protected float cameraCapMinX, cameraCapMaxX, cameraCapMinY, cameraCapMaxY;
    protected float effectivelyInfinitelyFar;

    protected RoomDownsyncFrame historyRdfHolder;
    protected shared.Collision collisionHolder;
    protected SatResult overlapResult, primaryOverlapResult;
    protected Dictionary<int, BattleResult> unconfirmedBattleResult;
    protected bool useOthersForcedDownsyncRenderFrameDict = false;
    protected Dictionary<int, RoomDownsyncFrame> othersForcedDownsyncRenderFrameDict;
    protected BattleResult confirmedBattleResult;
    protected Vector[] effPushbacks, softPushbacks;
    protected Vector[][] hardPushbackNormsArr;
    protected bool softPushbackEnabled;
    protected shared.Collider[] dynamicRectangleColliders;
    protected shared.Collider[] staticColliders;
    protected int staticCollidersCnt;

    protected InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    protected CollisionSpace collisionSys;

    protected Dictionary<int, int> joinIndexToColorSwapRuleLock;
    protected Dictionary<uint, int> playerSpeciesIdOccurrenceCnt;
    public GameObject linePrefab;
    protected KvPriorityQueue<int, FireballAnimController> cachedFireballs;
    protected KvPriorityQueue<int, PickableAnimController> cachedPickables;
    protected Vector3[] debugDrawPositionsHolder = new Vector3[4]; // Currently only rectangles are drawn
    protected KvPriorityQueue<int, DebugLine> cachedLineRenderers;
    protected Dictionary<uint, GameObject> npcSpeciesPrefabDict;
    protected Dictionary<uint, KvPriorityQueue<int, CharacterAnimController>> cachedNpcs;
    protected HashSet<(int, uint)> usedNpcNodes;
    protected KvPriorityQueue<int, TeamRibbon> cachedTeamRibbons;
    protected KvPriorityQueue<int, InplaceHpBar> cachedHpBars;
    protected KvPriorityQueue<int, KeyChPointer> cachedKeyChPointers;

    protected bool shouldDetectRealtimeRenderHistoryCorrection = false; // Not recommended to enable in production, it might have some memory performance impact.
    protected bool frameLogEnabled = false;
    protected Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput;
    protected Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs;
    protected Dictionary<int, int> triggerEditorIdToLocalId;
    protected Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToConfigFromTiled;

    protected List<shared.Collider> completelyStaticTrapColliders;

    protected KvPriorityQueue<int, PixelVfxNodeController> cachedPixelVfxNodes;
    protected KvPriorityQueue<int, PixelVfxNodeController> cachedPixelPlasmaVfxNodes;
    protected KvPriorityQueue<int, SFXSource> cachedSfxNodes;
    public BGMSource bgmSource;

    protected List<SuperTileLayer> windyLayers;

    public abstract void OnSettingsClicked();

    protected bool debugDrawingAllocation = false;
    protected bool debugDrawingEnabled = false;

    protected ILoggerBridge _loggerBridge = new LoggerBridgeImpl();

    public SelfBattleHeading selfBattleHeading;
    public BossBattleHeading bossBattleHeading;

    public GameObject playerLightsPrefab;
    protected PlayerLights selfPlayerLights;

    protected Vector2 teamRibbonOffset = new Vector2(-10f, +6f);
    protected Vector2 inplaceHpBarOffset = new Vector2(-8f, +16f);
    protected float defaultGameplayCamZ = -10;
    protected float lineRendererZ = +5;
    protected float triggerZ = 0;
    protected float characterZ = 0;
    protected float flyingCharacterZ = -1;
    protected float inplaceHpBarZ = +10;
    protected float fireballZ = -5;
    protected float footstepAttenuationZ = 200.0f;

    private string MATERIAL_REF_THICKNESS = "_Thickness";

    protected KvPriorityQueue<int, TeamRibbon>.ValScore cachedTeamRibbonScore = (x) => x.score;
    protected KvPriorityQueue<int, KeyChPointer>.ValScore cachedKeyChPointerScore = (x) => x.score;
    protected KvPriorityQueue<int, InplaceHpBar>.ValScore cachedHpBarScore = (x) => x.score;
    protected KvPriorityQueue<int, CharacterAnimController>.ValScore cachedNpcScore = (x) => x.score;
    protected KvPriorityQueue<int, FireballAnimController>.ValScore cachedFireballScore = (x) => x.score;
    protected KvPriorityQueue<int, PickableAnimController>.ValScore cachedPickableScore = (x) => x.score;
    protected KvPriorityQueue<int, DebugLine>.ValScore cachedLineScore = (x) => x.score;
    protected KvPriorityQueue<int, SFXSource>.ValScore sfxNodeScore = (x) => x.score;
    protected KvPriorityQueue<int, PixelVfxNodeController>.ValScore pixelVfxNodeScore = (x) => x.score;

    public BattleInputManager iptmgr;

    protected int missionTriggerLocalId = TERMINATING_EVTSUB_ID_INT;
    protected bool isOnlineMode;
    protected int localExtraInputDelayFrames = 0;

    public Camera gameplayCamera;
    protected GameObject loadCharacterPrefab(CharacterConfig chConfig) {
        string path = String.Format("Prefabs/{0}", chConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadTrapPrefab(TrapConfig trapConfig) {
        string path = String.Format("TrapPrefabs/{0}", trapConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadTriggerPrefab(TriggerConfig triggerConfig) {
        string path = String.Format("TriggerPrefabs/{0}", triggerConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadPickablePrefab(Pickable pickable) {
        return Resources.Load("Prefabs/Pickable") as GameObject;
    }

    public ReadyGo readyGoPanel;
    public SettlementPanel settlementPanel;

    protected Vector3 newPosHolder = new Vector3();
    protected Vector3 newTlPosHolder = new Vector3(), newTrPosHolder = new Vector3(), newBlPosHolder = new Vector3(), newBrPosHolder = new Vector3();
    protected Vector3 pointInCamViewPortHolder = new Vector3();

    protected void spawnPlayerNode(int joinIndex, uint speciesId, float wx, float wy, int bulletTeamId) {
        var characterPrefab = loadCharacterPrefab(characters[speciesId]);
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(wx, wy, characterZ), Quaternion.identity, underlyingMap.transform);
        playerGameObjs[joinIndex - 1] = newPlayerNode;
        newPlayerNode.GetComponent<CharacterAnimController>().SetSpeciesId(speciesId);

        if (joinIndex == selfPlayerInfo.JoinIndex) {
            selfPlayerInfo.BulletTeamId = bulletTeamId;
        }
        int colorSwapRuleOrder = 1;
        if (joinIndexToColorSwapRuleLock.ContainsKey(joinIndex)) {
            colorSwapRuleOrder = joinIndexToColorSwapRuleLock[joinIndex];
        } else if (playerSpeciesIdOccurrenceCnt.ContainsKey(speciesId)) {
            colorSwapRuleOrder += playerSpeciesIdOccurrenceCnt[speciesId];
        }
        var spr = newPlayerNode.GetComponent<SpriteRenderer>();
        var material = spr.material;
        if (TeamRibbon.COLOR_SWAP_RULE.ContainsKey(speciesId) && TeamRibbon.COLOR_SWAP_RULE[speciesId].ContainsKey(colorSwapRuleOrder)) {
            var rule = TeamRibbon.COLOR_SWAP_RULE[speciesId][colorSwapRuleOrder];
            material.SetColor("_Palette1From", rule.P1From);
            material.SetColor("_Palette1To", rule.P1To);
            material.SetFloat("_Palette1FromRange", rule.P1FromRange);
            material.SetFloat("_Palette1ToFuzziness", rule.P1ToFuzziness);
        }

        joinIndexToColorSwapRuleLock[joinIndex] = colorSwapRuleOrder;
        if (playerSpeciesIdOccurrenceCnt.ContainsKey(speciesId)) {
            playerSpeciesIdOccurrenceCnt[speciesId] += 1;
        } else {
            playerSpeciesIdOccurrenceCnt[speciesId] = 1;
        }
    }

    protected void spawnDynamicTrapNode(int speciesId, float wx, float wy) {
        var trapPrefab = loadTrapPrefab(trapConfigs[speciesId]);
        GameObject newTrapNode = Instantiate(trapPrefab, new Vector3(wx, wy, triggerZ), Quaternion.identity, underlyingMap.transform);
        dynamicTrapGameObjs.Add(newTrapNode);
    }

    protected void spawnTriggerNode(int triggerLocalId, int speciesId, float wx, float wy) {
        var triggerPrefab = loadTriggerPrefab(triggerConfigs[speciesId]);
        if (null == triggerPrefab) return;
        GameObject newTriggerNode = Instantiate(triggerPrefab, new Vector3(wx, wy, triggerZ), Quaternion.identity, underlyingMap.transform);
        triggerGameObjs[triggerLocalId] = newTriggerNode;
    }

    protected (ulong, ulong) getOrPrefabInputFrameUpsync(int inputFrameId, bool canConfirmSelf, ulong[] prefabbedInputList) {
        if (null == selfPlayerInfo) {
            string msg = String.Format("noDelayInputFrameId={0:D} couldn't be generated due to selfPlayerInfo being null", inputFrameId);
            throw new ArgumentException(msg);
        }

        ulong previousSelfInput = 0,
          currSelfInput = 0;
        int joinIndex = selfPlayerInfo.JoinIndex;
        ulong selfJoinIndexMask = (1UL << (joinIndex - 1));
        var (_, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
        var (_, previousInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId - 1);
        previousSelfInput = (null == previousInputFrameDownsync ? 0 : previousInputFrameDownsync.InputList[joinIndex - 1]);

        bool selfConfirmedInExistingInputFrame = (null != existingInputFrame && 0 < (existingInputFrame.ConfirmedList & selfJoinIndexMask)); 
        if (selfConfirmedInExistingInputFrame) {
            /*
            [WARNING] 

            As shown in "https://github.com/genxium/DelayNoMoreUnity/blob/v1.6.5/frontend/Assets/Scripts/Abstract/AbstractMapController.cs#L1180", "playerRdfId" would NEVER be rewinded even under the most clumsy condition, i.e. "RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet" is carry-forth only (see "https://github.com/genxium/DelayNoMoreUnity/blob/v1.6.5/shared/FrameRingBuffer.cs#L80").

            The only possibility that "true == selfConfirmedInExistingInputFrame" is met here would be due to "putting `getOrPrefabInputFrameUpsync(..., canConfirmSelf=true, ...) > sendInputFrameUpsyncBatch(...)` before `lockstep`" by mistake -- in that case, "playerRdfId" is stuck at the same value thus we might be overwriting already confirmed input history for self (yet backend and other peers will certainly reject the overwrite!).
            */ 
            return (previousSelfInput, existingInputFrame.InputList[joinIndex - 1]);
        }
        if (
          null != existingInputFrame
          &&
          (true != canConfirmSelf)
        ) {
            return (previousSelfInput, existingInputFrame.InputList[joinIndex - 1]);
        }

        Array.Fill<ulong>(prefabbedInputList, 0);
        for (int k = 0; k < roomCapacity; ++k) {
            /**
            TODO: If "inArenaPracticeMode", call "deriveNpcOpPattern(...)" here for other players! 
            */
            if (null != existingInputFrame) {
                // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here, we just have to assign "prefabbedInputList[(joinIndex-1)]" specifically and copy all others
                prefabbedInputList[k] = existingInputFrame.InputList[k];
            } else if (lastIndividuallyConfirmedInputFrameId[k] <= inputFrameId) {
                // Don't predict "btnB" -- yet predicting "btnA" for better "jump holding" consistency
                ulong encodedIdx = (lastIndividuallyConfirmedInputList[k] & 15UL);
                prefabbedInputList[k] = encodedIdx;
                bool shouldPredictBtnAHold = false;
                bool shouldPredictBtnBHold = false;
                bool shouldPredictBtnEHold = false;
                if (null != previousInputFrameDownsync && 0 < (previousInputFrameDownsync.InputList[k] & 16UL) && JUMP_HOLDING_IFD_CNT_THRESHOLD_1 > inputFrameId-lastIndividuallyConfirmedInputFrameId[k]) {
                    shouldPredictBtnAHold = true;
                    if (2 == encodedIdx || 6 == encodedIdx || 7 == encodedIdx) {
                        // Don't predict slip-jump!
                        shouldPredictBtnAHold = false;
                    }
                } 
                if (null != previousInputFrameDownsync && 0 < (previousInputFrameDownsync.InputList[k] & 256UL) && BTN_E_HOLDING_IFD_CNT_THRESHOLD_1 > inputFrameId-lastIndividuallyConfirmedInputFrameId[k]) {
                    shouldPredictBtnEHold = true;
                } 
                if (null != previousInputFrameDownsync && 0 < (previousInputFrameDownsync.InputList[k] & 32UL)) {
                    var (_, rdf) = renderBuffer.GetByFrameId(playerRdfId);
                    if (null != rdf) {
                        var chDownsync = rdf.PlayersArr[k];
                        if (BTN_B_HOLDING_RDF_CNT_THRESHOLD_1 < chDownsync.BtnBHoldingRdfCount) {       
                            shouldPredictBtnBHold = true;
                        }
                    }
                }
                if (shouldPredictBtnAHold) prefabbedInputList[k] |= (lastIndividuallyConfirmedInputList[k] & 16UL); 
                if (shouldPredictBtnEHold) prefabbedInputList[k] |= (lastIndividuallyConfirmedInputList[k] & 256UL); 
                if (shouldPredictBtnBHold) prefabbedInputList[k] |= (lastIndividuallyConfirmedInputList[k] & 32UL); 
            } else if (null != previousInputFrameDownsync) {
                // When "self.lastIndividuallyConfirmedInputFrameId[k] > inputFrameId", don't use it to predict a historical input!
                // Don't predict jump/atk holding in this case.
                prefabbedInputList[k] = (previousInputFrameDownsync.InputList[k] & 15UL);
            }
        }

        // [WARNING] Do not blindly use "selfJoinIndexMask" here, as the "actuallyUsedInput for self" couldn't be confirmed while prefabbing, otherwise we'd have confirmed a wrong self input by "_markConfirmationIfApplicable()"!
        ulong initConfirmedList = 0;
        if (null != existingInputFrame) {
            // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here
            initConfirmedList = (existingInputFrame.ConfirmedList | selfJoinIndexMask);
        }
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

    protected (RoomDownsyncFrame, RoomDownsyncFrame) rollbackAndChase(int playerRdfIdSt, int playerRdfIdEd, CollisionSpace collisionSys, bool isChasing) {
        RoomDownsyncFrame prevLatestRdf = null, latestRdf = null;
        for (int i = playerRdfIdSt; i < playerRdfIdEd; i++) {
            var (ok1, currRdf) = renderBuffer.GetByFrameId(i);
            if (false == ok1 || null == currRdf) {
                var msg = String.Format("Couldn't find renderFrame for i={0} to rollback, playerRdfId={1}, might've been interrupted by onRoomDownsyncFrame; renderBuffer:{2}", i, playerRdfId, renderBuffer.toSimpleStat());
                Debug.LogWarning(msg);
                throw new ArgumentNullException(msg);
            }
            if (currRdf.Id != i) {
                throw new ArgumentException(String.Format("Corrupted historic rdf for i={0} to rollback, currRdf={1}! renderBuffer:{2}", i, currRdf, renderBuffer.toSimpleStat()));
            }
            int j = ConvertToDelayedInputFrameId(i);
            var (ok2, delayedInputFrame) = inputBuffer.GetByFrameId(j);
            if (false == ok2 || null == delayedInputFrame) {
                throw new ArgumentNullException(String.Format("Couldn't find delayedInputFrame for j={0} to rollback, playerRdfId={1}; inputBuffer:{2}", j, playerRdfId, inputBuffer.toSimpleStat()));
            }

            bool allowUpdateInputFrameInPlaceUponDynamics = (!isChasing);
            if (allowUpdateInputFrameInPlaceUponDynamics) {
                bool hasInputBeenMutated = UpdateInputFrameInPlaceUponDynamics(currRdf, inputBuffer, j, roomCapacity, delayedInputFrame.ConfirmedList, delayedInputFrame.InputList, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, selfPlayerInfo.JoinIndex);
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

            bool hasIncorrectlyPredictedRenderFrame = false;
            bool selfNotEnoughMp = false;
            Step(inputBuffer, i, roomCapacity, collisionSys, renderBuffer, ref overlapResult, ref primaryOverlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, residueCollided, triggerEditorIdToLocalId, triggerEditorIdToConfigFromTiled, trapLocalIdToColliderAttrs, completelyStaticTrapColliders, unconfirmedBattleResult, ref confirmedBattleResult, pushbackFrameLogBuffer, frameLogEnabled, playerRdfId, shouldDetectRealtimeRenderHistoryCorrection, out hasIncorrectlyPredictedRenderFrame, historyRdfHolder, missionTriggerLocalId, selfPlayerInfo.JoinIndex, joinIndexRemap, ref justTriggeredStoryPointId, ref justTriggeredBgmId, justDeadNpcIndices, out fulfilledTriggerSetMask, ref selfNotEnoughMp, _loggerBridge);
            if (hasIncorrectlyPredictedRenderFrame) {
                Debug.LogFormat("@playerRdfId={0}, hasIncorrectlyPredictedRenderFrame=true for i:{1} -> i+1:{2}", playerRdfId, i, i + 1);
            }
            
            if (selfNotEnoughMp) {
                selfBattleHeading.BlinkMpNotEnough();
            }

            if (frameLogEnabled) {
                rdfIdToActuallyUsedInput[i] = delayedInputFrame.Clone();
            }

            var (ok3, nextRdf) = renderBuffer.GetByFrameId(i + 1);
            if (false == ok3 || null == nextRdf) {
                if (isChasing) {
                    throw new ArgumentNullException(String.Format("Couldn't find nextRdf for i+1={0} to rollback, playerRdfId={1}; renderBuffer:{2}", i + 1, playerRdfId, renderBuffer.toSimpleStat()));
                } else {
                    throw new ArgumentNullException(String.Format("Couldn't find nextRdf for i+1={0} to generate, playerRdfId={1} while rendering; renderBuffer:{2}", i + 1, playerRdfId, renderBuffer.toSimpleStat()));
                }
            }
            if (nextRdf.Id != i + 1) {
                throw new ArgumentException(String.Format("Corrupted historic rdf for i+1={0} to rollback/generate, nextRdf={1}! renderBuffer:{2}", i, nextRdf, renderBuffer.toSimpleStat()));
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

    private int _markConfirmationIfApplicable() {
        int newAllConfirmedCnt = 0;
        int candidateInputFrameId = (lastAllConfirmedInputFrameId + 1);
        if (candidateInputFrameId < inputBuffer.StFrameId) {
            candidateInputFrameId = inputBuffer.StFrameId;
        }
        while (inputBuffer.StFrameId <= candidateInputFrameId && candidateInputFrameId < inputBuffer.EdFrameId) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(candidateInputFrameId);
            if (false == res1 || null == inputFrameDownsync) break;
            if (false == isAllConfirmed(inputFrameDownsync.ConfirmedList, roomCapacity)) break;
            ++candidateInputFrameId;
            ++newAllConfirmedCnt;
        }
        if (0 < newAllConfirmedCnt) {
            lastAllConfirmedInputFrameId = candidateInputFrameId - 1;
        }
        return newAllConfirmedCnt;
    }

    protected void _handleIncorrectlyRenderedPrediction(int mismatchedInputFrameId, bool fromUDP) {
        if (TERMINATING_INPUT_FRAME_ID == mismatchedInputFrameId) return;
        int playerRdfId1 = ConvertToFirstUsedRenderFrameId(mismatchedInputFrameId);
        if (playerRdfId1 >= chaserRenderFrameId) return;
        // By now playerRdfId1 < chaserRenderFrameId, it's pretty impossible that "playerRdfId1 > playerRdfId" but we're still checking.
        if (playerRdfId1 > playerRdfId) return; // The incorrect prediction is not yet rendered, no visual impact for player.
        int playerRdfId2 = ConvertToLastUsedRenderFrameId(mismatchedInputFrameId);
        if (playerRdfId2 < chaserRenderFrameIdLowerBound) {
            /*
            [WARNING]
            
            There's no need to reset "chaserRenderFrameId" if the impact of this input mismatch couldn't even reach "chaserRenderFrameIdLowerBound".
            */
            //Debug.Log(String.Format("@playerRdfId={0}, IGNORING mismatchedInputFrameId: {1} whose last used rdfId: {2} is smaller than chaserRenderFrameIdLowerBound: {3}; chaserRenderFrameId={4}, lastAllConfirmedInputFrameId={5}, fromUDP={6}", playerRdfId, mismatchedInputFrameId, playerRdfId2, chaserRenderFrameIdLowerBound, chaserRenderFrameId, lastAllConfirmedInputFrameId, fromUDP));
            return;
        }
        /*
		   A typical case is as follows.
		   --------------------------------------------------------
		   <playerRdfId1>                           :              36


		   <this.chaserRenderFrameId>                 :              62

		   [this.playerRdfId]                       :              64
		   --------------------------------------------------------
		 */

        // The actual rollback-and-chase would later be executed in "Update()". 
        chaserRenderFrameId = playerRdfId1;

        /* 
        [WARNING] The incorrect prediction was already rendered, there MIGHT BE a visual impact for player.

        However, due to the use of 
        - `UpdateInputFrameInPlaceUponDynamics`, and  
        - `processInertiaWalking` 
        , even if an "inputFrame" for "already rendered renderFrame" was incorrectly predicted, there's still chance that no visual impact is induced. See relevant sections in `README` for details.  

        Printing of this message might induce a performance impact.
            
        TODO: Instead of printing, add frameLog for (currRenderFrameId, rolledBackInputFrameDownsyncId, rolledBackToRenderFrameId)!
            */
        /*
        if (fromUDP) {
            Debug.Log(String.Format("@playerRdfId={5}, mismatched input for rendered history detected, resetting chaserRenderFrameId: {0}->{1}; mismatchedInputFrameId: {2}, lastAllConfirmedInputFrameId={3}, fromUDP={4}", chaserRenderFrameId, playerRdfId1, mismatchedInputFrameId, lastAllConfirmedInputFrameId, fromUDP, playerRdfId));
        }
        */
    }

    public void applyRoomDownsyncFrameDynamics(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf) {
        // Put teamRibbons and hpBars to infinitely far first
        for (int i = cachedTeamRibbons.vals.StFrameId; i < cachedTeamRibbons.vals.EdFrameId; i++) {
            var (res, teamRibbon) = cachedTeamRibbons.vals.GetByFrameId(i);
            if (!res || null == teamRibbon) throw new ArgumentNullException(String.Format("There's no cachedTeamRibbon for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedTeamRibbons.vals.StFrameId, cachedTeamRibbons.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, inplaceHpBarZ);
            teamRibbon.gameObject.transform.position = newPosHolder;
        }

        for (int i = cachedKeyChPointers.vals.StFrameId; i < cachedKeyChPointers.vals.EdFrameId; i++) {
            var (res, keyChPointer) = cachedKeyChPointers.vals.GetByFrameId(i);
            if (!res || null == keyChPointer) throw new ArgumentNullException(String.Format("There's no cachedKeyChPointer for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedKeyChPointers.vals.StFrameId, cachedKeyChPointers.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, inplaceHpBarZ);
            keyChPointer.gameObject.transform.position = newPosHolder;
        }

        for (int i = cachedHpBars.vals.StFrameId; i < cachedHpBars.vals.EdFrameId; i++) {
            var (res, hpBar) = cachedHpBars.vals.GetByFrameId(i);
            if (!res || null == hpBar) throw new ArgumentNullException(String.Format("There's no cachedHpBar for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedHpBars.vals.StFrameId, cachedHpBars.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, inplaceHpBarZ);
            hpBar.gameObject.transform.position = newPosHolder;
        }

        // Put all pixel-vfx nodes to infinitely far first
        for (int i = cachedPixelVfxNodes.vals.StFrameId; i < cachedPixelVfxNodes.vals.EdFrameId; i++) {
            var (res, holder) = cachedPixelVfxNodes.vals.GetByFrameId(i);
            if (!res || null == holder) throw new ArgumentNullException(String.Format("There's no pixelVfxHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPixelVfxNodes.vals.StFrameId, cachedPixelVfxNodes.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, holder.gameObject.transform.position.z);
            holder.gameObject.transform.position = newPosHolder;
        }

        for (int i = cachedPixelPlasmaVfxNodes.vals.StFrameId; i < cachedPixelPlasmaVfxNodes.vals.EdFrameId; i++) {
            var (res, holder) = cachedPixelPlasmaVfxNodes.vals.GetByFrameId(i);
            if (!res || null == holder) throw new ArgumentNullException(String.Format("There's no pixelPlasmaVfxHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPixelPlasmaVfxNodes.vals.StFrameId, cachedPixelPlasmaVfxNodes.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, holder.gameObject.transform.position.z);
            holder.gameObject.transform.position = newPosHolder;
        }

        float selfPlayerWx = 0f, selfPlayerWy = 0f;
        
        for (int k = 0; k < roomCapacity; k++) {
            var currCharacterDownsync = rdf.PlayersArr[k];
            var prevCharacterDownsync = (null == prevRdf ? null : prevRdf.PlayersArr[k]);
            //Debug.Log(String.Format("At rdf.Id={0}, currCharacterDownsync[k:{1}] at [vGridX: {2}, vGridY: {3}, velX: {4}, velY: {5}, chState: {6}, framesInChState: {7}, dirx: {8}]", rdf.Id, k, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, currCharacterDownsync.VelX, currCharacterDownsync.VelY, currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, currCharacterDownsync.DirX));

            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);

            var playerGameObj = playerGameObjs[k]; 
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();

            if (chAnimCtrl.GetSpeciesId() != currCharacterDownsync.SpeciesId) {
                Destroy(playerGameObjs[k]);
                spawnPlayerNode(k + 1, chConfig.SpeciesId, wx, wy, currCharacterDownsync.BulletTeamId);
                playerGameObj = playerGameObjs[k];
                chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            }

            setCharacterGameObjectPosByInterpolation(prevCharacterDownsync, currCharacterDownsync, chConfig, playerGameObj, wx, wy);

            playerGameObj.transform.position = newPosHolder; // [WARNING] Even if not selfPlayer, we have to set position of the other players regardless of new positions being visible within camera or not, otherwise outdated other players' node might be rendered within camera! 

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            int teamRibbonLookupKey = KV_PREFIX_PLAYER + currCharacterDownsync.JoinIndex;
            if (currCharacterDownsync.JoinIndex == selfPlayerInfo.JoinIndex) {
                selfBattleHeading.SetCharacter(currCharacterDownsync);
                selfPlayerWx = wx;
                selfPlayerWy = wy;
                //newPosHolder.Set(wx, wy, chGameObj.transform.position.z);
                //selfPlayerLights.gameObject.transform.position = newPosHolder;
                //selfPlayerLights.setDirX(currCharacterDownsync.DirX);

                int effInventoryCount = 0;
                for (int i = 0; i < currCharacterDownsync.Inventory.Slots.Count; i++) {
                    var slotData = currCharacterDownsync.Inventory.Slots[i];
                    if (InventorySlotStockType.NoneIv == slotData.StockType) break;
                    if (InventorySlotStockType.DummyIv == slotData.StockType) continue;
                    var targetBtn = (0 == i ? iptmgr.btnC : (1 == i ? iptmgr.btnD : iptmgr.btnB)); // TODO: Don't hardcode them
                    targetBtn.gameObject.SetActive(true);
                    var ivSlotGui = targetBtn.GetComponent<InventorySlot>();
                    ivSlotGui.updateData(slotData);
                    if (i < 2) {
                        // TODO: Don't hardcode this!
                        effInventoryCount++;
                    }
                }

                if (0 >= effInventoryCount) {
                    iptmgr.btnC.gameObject.SetActive(false);
                    iptmgr.btnD.gameObject.SetActive(false);
                }
                /*
                if (CharacterState.Atked1 == currCharacterDownsync.CharacterState || CharacterState.Def1Broken == currCharacterDownsync.CharacterState || CharacterState.Def1 == currCharacterDownsync.CharacterState) {
                    Debug.LogFormat("ChState={0}, FramesInChState={1}, FramesToRecover={2}", currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, currCharacterDownsync.FramesToRecover);
                }

                if (CharacterState.CrouchAtk1 == currCharacterDownsync.CharacterState || CharacterState.CrouchIdle1 == currCharacterDownsync.CharacterState) {
                    Debug.LogFormat("ChState={0}, FramesInChState={1}, FramesToRecover={2}, activeSkillId={3}, activeSkillHit={4}", currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, currCharacterDownsync.FramesToRecover, currCharacterDownsync.ActiveSkillId, currCharacterDownsync.ActiveSkillHit);
                }
                */
            } else {
                newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, characterZ);
                newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, characterZ);
                newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, characterZ);
                newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, characterZ);

                if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) {
                    // No need to update the actual anim if the other players are out of
                    
                    if (isOnlineMode && CharacterState.Dying != currCharacterDownsync.CharacterState && CharacterState.Dimmed != currCharacterDownsync.CharacterState) {
                        showKeyChPointer(rdf.Id, currCharacterDownsync, wx, wy, halfBoxCw, halfBoxCh, teamRibbonLookupKey);
                    }
                    continue;
                }

                // Add teamRibbon if same team as self, allowing characters of other teams to hide under foreground
                if (CharacterState.Dying != currCharacterDownsync.CharacterState && selfPlayerInfo.BulletTeamId == currCharacterDownsync.BulletTeamId) {
                    showTeamRibbon(rdf.Id, currCharacterDownsync, wx, wy, halfBoxCw, halfBoxCh, teamRibbonLookupKey);
                }
            }

            chAnimCtrl.updateCharacterAnim(currCharacterDownsync, currCharacterDownsync.CharacterState, prevCharacterDownsync, false, chConfig);

            // Add character vfx
            float distanceAttenuationZ = Math.Abs(wx - selfPlayerWx) + Math.Abs(wy - selfPlayerWy);
            var spr = chAnimCtrl.GetComponent<SpriteRenderer>();
            var material = spr.material;
            playCharacterDamagedVfx(rdf.Id, currCharacterDownsync, chConfig, prevCharacterDownsync, playerGameObj, wx, wy, halfBoxCh, chAnimCtrl, teamRibbonLookupKey, material, false);
            
            playCharacterSfx(currCharacterDownsync, prevCharacterDownsync, chConfig, wx, wy, rdf.Id, distanceAttenuationZ);
            playCharacterVfx(currCharacterDownsync, prevCharacterDownsync, chConfig, chAnimCtrl, wx, wy, rdf.Id);
        }

        // Put unused npcNodes to infinitely far first
        usedNpcNodes.Clear();
        currRdfNpcAnimHoldersCnt = 0;
        bool hasActiveBoss = false;
        for (int k = 0; k < rdf.NpcsArr.Count; k++) {
            var currNpcDownsync = rdf.NpcsArr[k];

            if (TERMINATING_PLAYER_ID == currNpcDownsync.Id) break;
            var prevNpcDownsync = (null == prevRdf ? null : prevRdf.NpcsArr[k]);
            // Debug.Log(String.Format("At rdf.Id={0}, currNpcDownsync[k:{1}] at [vx: {2}, vy: {3}, chState: {4}, framesInChState: {5}]", rdf.Id, k, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY, currNpcDownsync.CharacterState, currNpcDownsync.FramesInChState));

            var chConfig = characters[currNpcDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currNpcDownsync, chConfig, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, characterZ);
            newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, characterZ);
            newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, characterZ);
            newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, characterZ);
            int lookupKey = KV_PREFIX_NPC + currNpcDownsync.Id;

            ++currRdfNpcAnimHoldersCnt;

            bool isActiveBoss = (null != bossBattleHeading && bossSpeciesSet.Contains(currNpcDownsync.SpeciesId) && CharacterState.Dimmed != currNpcDownsync.CharacterState);
            if (isActiveBoss) {
                hasActiveBoss = true;
                bossBattleHeading.SetCharacter(currNpcDownsync);
            }

            if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) {
                if (isOnlineMode && chConfig.IsKeyCh && CharacterState.Dying != currNpcDownsync.CharacterState && CharacterState.Dimmed != currNpcDownsync.CharacterState) {
                    showKeyChPointer(rdf.Id, currNpcDownsync, wx, wy, halfBoxCw, halfBoxCh, lookupKey);
                }
                continue;
            }
            // if the current position is within camera FOV
            var speciesKvPq = cachedNpcs[currNpcDownsync.SpeciesId];
            var npcAnimHolder = speciesKvPq.PopAny(lookupKey);
            if (null == npcAnimHolder) {
                npcAnimHolder = speciesKvPq.Pop();
                //Debug.Log(String.Format("@rdf.Id={0} using a new npcAnimHolder for rendering for npcId={1}, joinIndex={2} at wpos=({3}, {4})", rdf.Id, currNpcDownsync.Id, currNpcDownsync.JoinIndex, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0} using a cached vfxAnimHolder for rendering for npcId={1}, joinIndex={2} at wpos=({3}, {4})", rdf.Id, currNpcDownsync.Id, currNpcDownsync.JoinIndex, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY));
            }

            if (null == npcAnimHolder) {
                throw new ArgumentNullException(String.Format("No available npcAnimHolder node for lookupKey={0}", lookupKey));
            }

            var npcGameObj = npcAnimHolder.gameObject;
            currRdfNpcAnimHolders[k] = npcAnimHolder;
            if (npcAnimHolder.GetNpcId() == currNpcDownsync.Id) {
                setCharacterGameObjectPosByInterpolation(prevNpcDownsync, currNpcDownsync, chConfig, npcGameObj, wx, wy);
            } else {
                newPosHolder.Set(wx, wy, calcEffCharacterZ(currNpcDownsync, chConfig));
            }
            npcGameObj.transform.position = newPosHolder;

            npcAnimHolder.updateCharacterAnim(currNpcDownsync, currNpcDownsync.CharacterState, prevNpcDownsync, false, chConfig);
            npcAnimHolder.score = rdf.Id;
            usedNpcNodes.Add((currNpcDownsync.Id, currNpcDownsync.SpeciesId));
            bool hasColorSwapByTeam = false;
            var spr = npcGameObj.GetComponent<SpriteRenderer>();
            var material = spr.material;
            if (isOnlineMode) {
                int colorSwapRuleOrder = currNpcDownsync.BulletTeamId;
                if (TeamRibbon.COLOR_SWAP_RULE.ContainsKey(currNpcDownsync.SpeciesId) && TeamRibbon.COLOR_SWAP_RULE[currNpcDownsync.SpeciesId].ContainsKey(colorSwapRuleOrder)) {
                    var rule = TeamRibbon.COLOR_SWAP_RULE[currNpcDownsync.SpeciesId][colorSwapRuleOrder];
                    material.SetColor("_Palette1From", rule.P1From);
                    material.SetColor("_Palette1To", rule.P1To);
                    material.SetFloat("_Palette1FromRange", rule.P1FromRange);
                    material.SetFloat("_Palette1ToFuzziness", rule.P1ToFuzziness);
                    hasColorSwapByTeam = true;
                }

                // Add teamRibbon if same team as self, allowing characters of other teams to hide under foreground
                if (!hasColorSwapByTeam && CharacterState.Dying != currNpcDownsync.CharacterState && selfPlayerInfo.BulletTeamId == currNpcDownsync.BulletTeamId) {
                    showTeamRibbon(rdf.Id, currNpcDownsync, wx, wy, halfBoxCw, halfBoxCh, lookupKey);
                }
            } else {
                if (CharacterState.Dying != currNpcDownsync.CharacterState && selfPlayerInfo.BulletTeamId == currNpcDownsync.BulletTeamId) {
                    showTeamRibbon(rdf.Id, currNpcDownsync, wx, wy, halfBoxCw, halfBoxCh, lookupKey);
                }
            }

            speciesKvPq.Put(lookupKey, npcAnimHolder);

            // Add character vfx
            if (currNpcDownsync.NewBirth) {
                DOTween.Sequence().Append(
                    DOTween.To(() => material.GetFloat(MATERIAL_REF_THICKNESS), x => material.SetFloat(MATERIAL_REF_THICKNESS, x), 1.5f, 0.5f))
                    .Append(DOTween.To(() => material.GetFloat(MATERIAL_REF_THICKNESS), x => material.SetFloat(MATERIAL_REF_THICKNESS, x), 0f, 0.5f));
            }

            playCharacterDamagedVfx(rdf.Id, currNpcDownsync, chConfig, prevNpcDownsync, npcGameObj, wx, wy, halfBoxCh, npcAnimHolder, lookupKey, material, isActiveBoss);
            float distanceAttenuationZ = Math.Abs(wx - selfPlayerWx) + Math.Abs(wy - selfPlayerWy);
            playCharacterSfx(currNpcDownsync, prevNpcDownsync, chConfig, wx, wy, rdf.Id, distanceAttenuationZ);
            playCharacterVfx(currNpcDownsync, prevNpcDownsync, chConfig, npcAnimHolder, wx, wy, rdf.Id);
        }
        foreach (var entry in cachedNpcs) {
            var speciesId = entry.Key;
            var speciesKvPq = entry.Value;
            for (int i = speciesKvPq.vals.StFrameId; i < speciesKvPq.vals.EdFrameId; i++) {
                var (res, npcAnimHolder) = speciesKvPq.vals.GetByFrameId(i);
                if (!res || null == npcAnimHolder) throw new ArgumentNullException(String.Format("There's no npcAnimHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, speciesKvPq.vals.StFrameId, speciesKvPq.vals.EdFrameId));
                if (usedNpcNodes.Contains((npcAnimHolder.GetNpcId(), npcAnimHolder.GetSpeciesId()))) {
                    continue;
                }
                newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, npcAnimHolder.gameObject.transform.position.z);
                npcAnimHolder.SetNpcId(TERMINATING_PLAYER_ID);
                npcAnimHolder.gameObject.transform.position = newPosHolder;
            }
        }

        if (null != bossBattleHeading) {
            bossBattleHeading.gameObject.SetActive(hasActiveBoss);
        }

        int kDynamicTrap = 0;
        for (int k = 0; k < rdf.TrapsArr.Count; k++) {
            var currTrap = rdf.TrapsArr[k];
            if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) break;
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currTrap.VirtualGridX, currTrap.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var dynamicTrapObj = dynamicTrapGameObjs[kDynamicTrap];
            // [WARNING] When placing a trap tile object in Tiled editor, the anchor is ALWAYS (0.5, 0.5) -- and in our "Virtual Grid Coordinates" we also use (0.5, 0.5) anchor ubiquitously. Therefore to achieve a "what you see is what you get effect", the compensation is done here, i.e. only at rendering.
            var trapConfig = trapConfigs[currTrap.ConfigFromTiled.SpeciesId];
            if (!trapConfig.IsRotary) {
                newPosHolder.Set(wx, wy, dynamicTrapObj.transform.position.z);
            } else {
                var (anchorCompensateWx, anchorCompensateWy) = (trapConfig.SpinAnchorX - 0.5f * currTrap.ConfigFromTiled.BoxCw, trapConfig.SpinAnchorY - 0.5f * currTrap.ConfigFromTiled.BoxCh);
                newPosHolder.Set(wx + anchorCompensateWx, wy + anchorCompensateWy, dynamicTrapObj.transform.position.z);
            }

            dynamicTrapObj.transform.position = newPosHolder;
            var animCtrl = dynamicTrapObj.GetComponent<TrapAnimationController>();
            animCtrl.updateAnim(currTrap.TrapState, currTrap, trapConfig, currTrap.FramesInTrapState, currTrap.DirX);
            kDynamicTrap++;
        }

        // Put all fireball nodes to infinitely far first
        for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
            var (res, fireballHolder) = cachedFireballs.vals.GetByFrameId(i);
            if (!res || null == fireballHolder) throw new ArgumentNullException(String.Format("There's no fireballHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballHolder.gameObject.transform.position.z);
            fireballHolder.gameObject.transform.position = newPosHolder;
        }

        for (int k = 0; k < rdf.Bullets.Count; k++) {
            var bullet = rdf.Bullets[k];
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BulletLocalId) break;
            var (skillConfig, bulletConfig) = FindBulletConfig(bullet.SkillId, bullet.ActiveSkillHit);
            if (null == skillConfig || null == bulletConfig) continue;
            var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
            var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bulletConfig.HitboxSizeX, bulletConfig.HitboxSizeY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, 0);
            newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, 0);
            newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, 0);
            newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, 0);
            if (!bulletConfig.BeamCollision && !bulletConfig.BeamRendering) {
                if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) continue;
            }

            bool isVanishing = IsBulletVanishing(bullet, bulletConfig);
            bool isExploding = IsBulletExploding(bullet, bulletConfig); // [WARNING] "isVanishing" will also yield "isExploding"
            bool isStartup = (BulletState.StartUp == bullet.BlState);
            var prevHitBulletConfig = (2 <= bullet.ActiveSkillHit ? skillConfig.Hits[bullet.ActiveSkillHit - 2] : null); // TODO: Make this compatible with simultaneous bullets after a "FromPrevHitXxx" bullet!

            bool isInMhHomogeneousSeq = (null != prevHitBulletConfig && (MultiHitType.FromPrevHitActual == prevHitBulletConfig.MhType || MultiHitType.FromPrevHitAnyway == prevHitBulletConfig.MhType) && prevHitBulletConfig.BType == bulletConfig.BType);

            int lookupKey = KV_PREFIX_BULLET + bullet.BulletLocalId;
            string animName = null;

            int explosionSpeciesId = isVanishing ? bulletConfig.InplaceVanishExplosionSpeciesId : bulletConfig.ExplosionSpeciesId;
            if (EXPLOSION_SPECIES_FOLLOW == explosionSpeciesId) {
                explosionSpeciesId = bulletConfig.SpeciesId;
            }
            switch (bulletConfig.BType) {
                case BulletType.Melee:
                    if (isExploding) {
                        animName = String.Format("Melee_Explosion{0}", explosionSpeciesId);
                    }
                    break;
                case BulletType.Fireball:
                case BulletType.GroundWave:
                case BulletType.MissileLinear:
                    if (IsBulletActive(bullet, bulletConfig, rdf.Id) || isInMhHomogeneousSeq || isExploding) {
                        animName = isExploding ? String.Format("Explosion{0}", explosionSpeciesId) : String.Format("Fireball{0}", bulletConfig.SpeciesId);
                    }
                    break;
                default:
                    break;
            }
            if (null != animName) {
                var fireballOrExplosionAnimHolder = cachedFireballs.PopAny(lookupKey);
                if (null == fireballOrExplosionAnimHolder) {
                    fireballOrExplosionAnimHolder = cachedFireballs.Pop();
                    //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a new fireball node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
                } else {
                    //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a cached node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
                }

                if (null != fireballOrExplosionAnimHolder) {
                    if (fireballOrExplosionAnimHolder.lookUpTable.ContainsKey(animName)) {
                        fireballOrExplosionAnimHolder.updateAnim(lookupKey, animName, bullet.FramesInBlState, bullet.DirX, bulletConfig, bullet, rdf, bullet.VelX, bullet.VelY, (bullet.TeamId == selfPlayerInfo.BulletTeamId));
                        newPosHolder.Set(wx, wy, fireballOrExplosionAnimHolder.gameObject.transform.position.z);
                        if (IsBulletRotary(bulletConfig)) {
                            // Special handling for spinned bullet positioning
                            if (bulletConfig.BeamCollision || bulletConfig.BeamRendering) {
                                var (beamBoxCw, beamBoxCh) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX - bullet.OriginatedVirtualGridX, bulletConfig.HitboxSizeY + bulletConfig.HitboxSizeIncY * bullet.FramesInBlState);
                                float newDx, newDy;
                                Vector.Rotate(beamBoxCw, 0, bullet.SpinCos, bullet.SpinSin, out newDx, out newDy);
                                var (cx2, cy2) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                                cx2 += newDx;
                                cy2 += newDy;
                                var (wx2, wy2) = CollisionSpacePositionToWorldPosition(cx2, cy2, spaceOffsetX, spaceOffsetY);
                                newPosHolder.Set(wx2, wy2, fireballZ - 1);
                            }
                        }
                        fireballOrExplosionAnimHolder.gameObject.transform.position = newPosHolder;
                    }
                    fireballOrExplosionAnimHolder.score = rdf.Id;
                    cachedFireballs.Put(lookupKey, fireballOrExplosionAnimHolder);
                } else {
                    // null == explosionAnimHolder
                    if (EXPLOSION_SPECIES_NONE != explosionSpeciesId) {
                        // Explosion of fireballs is now allowed to use pure particle vfx
                        throw new ArgumentNullException(String.Format("No available fireball node for lookupKey={0}, animName={1}", lookupKey, animName));
                    }
                }
            }

            float distanceAttenuationZ = Math.Abs(wx - selfPlayerWx) + Math.Abs(wy - selfPlayerWy);
            playBulletSfx(bullet, bulletConfig, isExploding, wx, wy, rdf.Id, distanceAttenuationZ);
            playBulletVfx(bullet, bulletConfig, isStartup, isExploding, wx, wy, rdf);
        }

        for (int k = 0; k < rdf.TriggersArr.Count; k++) {
            var trigger = rdf.TriggersArr[k];
            if (!triggerGameObjs.ContainsKey(trigger.TriggerLocalId)) continue;
            if (TERMINATING_TRIGGER_ID == trigger.TriggerLocalId) break;
            var triggerGameObj = triggerGameObjs[trigger.TriggerLocalId];
            var animCtrl = triggerGameObj.GetComponent<TriggerAnimationController>();
            animCtrl.updateAnim(trigger.State, trigger, trigger.FramesInState, trigger.DirX);
        }

        // Put all pickable nodes to infinitely far first
        for (int i = cachedPickables.vals.StFrameId; i < cachedPickables.vals.EdFrameId; i++) {
            var (res, pickableHolder) = cachedPickables.vals.GetByFrameId(i);
            if (!res || null == pickableHolder) throw new ArgumentNullException(String.Format("There's no pickableHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPickables.vals.StFrameId, cachedPickables.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, pickableHolder.gameObject.transform.position.z);
            pickableHolder.gameObject.transform.position = newPosHolder;
        }

        for (int k = 0; k < rdf.Pickables.Count; k++) {
            var pickable = rdf.Pickables[k];
            if (TERMINATING_PICKABLE_LOCAL_ID == pickable.PickableLocalId) break;
            if (!IsPickableAlive(pickable, rdf.Id)) {
                continue;
            }
            var (cx, cy) = VirtualGridToPolygonColliderCtr(pickable.VirtualGridX, pickable.VirtualGridY);
            var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(DEFAULT_PICKABLE_HITBOX_SIZE_X, DEFAULT_PICKABLE_HITBOX_SIZE_Y);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, 0);
            newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, 0);
            newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, 0);
            newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, 0);

            if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) continue;

            playPickableSfx(pickable, wx, wy, rdf.Id);

            // By now only "consumable" is available
            if (TERMINATING_CONSUMABLE_SPECIES_ID != pickable.ConfigFromTiled.ConsumableSpeciesId) {
                var consumableConfig = consumableConfigs[pickable.ConfigFromTiled.ConsumableSpeciesId];
                if (PickableState.Pidle == pickable.PkState) {
                    int lookupKey = pickable.PickableLocalId;
                    string animName = null;
                    animName = "Consumable" + pickable.ConfigFromTiled.ConsumableSpeciesId;
                    var pickableAnimHolder = cachedPickables.PopAny(lookupKey);
                    if (null == pickableAnimHolder) {
                        pickableAnimHolder = cachedPickables.Pop();
                        //Debug.Log(String.Format("@rdf.Id={0}, using a new pickable node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                    } else {
                        //Debug.Log(String.Format("@rdf.Id={0}, using a cached pickable node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                    }

                    if (null != pickableAnimHolder && null != pickableAnimHolder.lookUpTable) {
                        if (pickableAnimHolder.lookUpTable.ContainsKey(animName)) {
                            pickableAnimHolder.updateAnim(animName);
                            newPosHolder.Set(wx, wy, pickableAnimHolder.gameObject.transform.position.z);
                            pickableAnimHolder.gameObject.transform.position = newPosHolder;
                        }
                        pickableAnimHolder.score = rdf.Id;
                        cachedPickables.Put(lookupKey, pickableAnimHolder);
                    }
                } else if (PickableState.Pconsumed == pickable.PkState) {
                    int vfxLookupKey = KV_PREFIX_PK + pickable.PickableLocalId;
                    if (NO_VFX_ID != consumableConfig.VfxIdOnPicker && pixelatedVfxDict.ContainsKey(consumableConfig.VfxIdOnPicker)) {
                        var vfxConfig = pixelatedVfxDict[consumableConfig.VfxIdOnPicker];
                        string vfxAnimName = vfxConfig.Name;
                        var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
                        if (null == pixelVfxHolder) {
                            pixelVfxHolder = cachedPixelVfxNodes.Pop();
                            //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                        } else {
                            //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                        }

                        if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                            if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                                pixelVfxHolder.updateAnim(vfxAnimName, pickable.FramesInPkState, 0, false, rdf.Id);
                                var playerObj = playerGameObjs[pickable.PickedByJoinIndex-1]; // Guaranteed to be bound to player controlled characters
                                newPosHolder.Set(playerObj.transform.position.x, playerObj.transform.position.y, pixelVfxHolder.gameObject.transform.position.z);
                                pixelVfxHolder.gameObject.transform.position = newPosHolder;
                            }
                            pixelVfxHolder.score = rdf.Id;
                            cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
                        }
                    }
                }
            } else if (NO_SKILL != pickable.ConfigFromTiled.SkillId) {
                var pickableSkillConfig = pickableSkillConfigs[pickable.ConfigFromTiled.SkillId];
                if (PickableState.Pidle == pickable.PkState) {
                    int lookupKey = pickable.PickableLocalId;
                    string animName = null;
                    animName = String.Format("Skill{0}", pickable.ConfigFromTiled.SkillId);
                    var pickableAnimHolder = cachedPickables.PopAny(lookupKey);
                    if (null == pickableAnimHolder) {
                        pickableAnimHolder = cachedPickables.Pop();
                        //Debug.Log(String.Format("@rdf.Id={0}, using a new pickable node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                    } else {
                        //Debug.Log(String.Format("@rdf.Id={0}, using a cached pickable node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                    }

                    if (null != pickableAnimHolder && null != pickableAnimHolder.lookUpTable) {
                        if (pickableAnimHolder.lookUpTable.ContainsKey(animName)) {
                            pickableAnimHolder.updateAnim(animName);
                            newPosHolder.Set(wx, wy, pickableAnimHolder.gameObject.transform.position.z);
                            pickableAnimHolder.gameObject.transform.position = newPosHolder;
                        }
                        pickableAnimHolder.score = rdf.Id;
                        cachedPickables.Put(lookupKey, pickableAnimHolder);
                    }
                } else if (PickableState.Pconsumed == pickable.PkState) {
                    int vfxLookupKey = KV_PREFIX_PK + pickable.PickableLocalId;
                    if (NO_VFX_ID != pickableSkillConfig.VfxIdOnPicker && pixelatedVfxDict.ContainsKey(pickableSkillConfig.VfxIdOnPicker)) {
                        var vfxConfig = pixelatedVfxDict[pickableSkillConfig.VfxIdOnPicker];
                        string vfxAnimName = vfxConfig.Name;
                        var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
                        if (null == pixelVfxHolder) {
                            pixelVfxHolder = cachedPixelVfxNodes.Pop();
                            //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                        } else {
                            //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                        }

                        if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                            if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                                pixelVfxHolder.updateAnim(vfxAnimName, pickable.FramesInPkState, 0, false, rdf.Id);
                                var playerObj = playerGameObjs[pickable.PickedByJoinIndex - 1]; // Guaranteed to be bound to player controlled characters
                                newPosHolder.Set(playerObj.transform.position.x, playerObj.transform.position.y, pixelVfxHolder.gameObject.transform.position.z);
                                pixelVfxHolder.gameObject.transform.position = newPosHolder;
                            }
                            pixelVfxHolder.score = rdf.Id;
                            cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
                        }
                    }
                }
            }
        }

        foreach (var stl in windyLayers) {
            var tmr = stl.GetComponent<TilemapRenderer>();
            var material = tmr.material;
            if (null != material) {
                material.SetFloat("_Seed", Time.realtimeSinceStartup);
            }
        }
    }

    protected void preallocatePixelVfxNodes() {
        Debug.Log("preallocatePixelVfxNodes begins");
        if (null != cachedPixelVfxNodes) {
            while (0 < cachedPixelVfxNodes.Cnt()) {
                var g = cachedPixelVfxNodes.Pop();
                if (null != g) {
                    Destroy(g.gameObject);
                }
            }
        }
        int pixelVfxNodeCacheCapacity = 32;
        cachedPixelVfxNodes = new KvPriorityQueue<int, PixelVfxNodeController>(pixelVfxNodeCacheCapacity, pixelVfxNodeScore);
        for (int i = 0; i < pixelVfxNodeCacheCapacity; i++) {
            GameObject newPixelVfxNode = Instantiate(pixelVfxNodePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ), Quaternion.identity, underlyingMap.transform);
            PixelVfxNodeController newPixelVfxSource = newPixelVfxNode.GetComponent<PixelVfxNodeController>();
            newPixelVfxSource.score = -1;
            int initLookupKey = i;
            cachedPixelVfxNodes.Put(initLookupKey, newPixelVfxSource);
        }

        if (null != cachedPixelPlasmaVfxNodes) {
            while (0 < cachedPixelPlasmaVfxNodes.Cnt()) {
                var g = cachedPixelPlasmaVfxNodes.Pop();
                if (null != g) {
                    Destroy(g.gameObject);
                }
            }
        }
        int pixelPlasmaVfxNodeCacheCapacity = 16;
        cachedPixelPlasmaVfxNodes = new KvPriorityQueue<int, PixelVfxNodeController>(pixelPlasmaVfxNodeCacheCapacity, pixelVfxNodeScore);
        for (int i = 0; i < pixelPlasmaVfxNodeCacheCapacity; i++) {
            GameObject newPixelVfxNode = Instantiate(pixelPlasmaVfxNodePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ), Quaternion.identity, underlyingMap.transform);
            PixelVfxNodeController newPixelVfxSource = newPixelVfxNode.GetComponent<PixelVfxNodeController>();
            newPixelVfxSource.score = -1;
            int initLookupKey = i;
            cachedPixelPlasmaVfxNodes.Put(initLookupKey, newPixelVfxSource);
        }

        Debug.Log("preallocatePixelVfxNodes ends");
    }

    protected void preallocateSfxNodes() {
        // TODO: Shall I use the same preallocation strategy for VFX? Would run for a while and see the difference...
        Debug.Log("preallocateSfxNodes begins");
        if (null != cachedSfxNodes) {
            while (0 < cachedSfxNodes.Cnt()) {
                var g = cachedSfxNodes.Pop();
                if (null != g) {
                    Destroy(g.gameObject);
                }
            }
        }
        int sfxNodeCacheCapacity = 24;
        cachedSfxNodes = new KvPriorityQueue<int, SFXSource>(sfxNodeCacheCapacity, sfxNodeScore);
        string[] allSfxClipsNames = new string[] {
            "Explosion1",
            "Explosion2",
            "Explosion3",
            "Explosion4",
            "Explosion8",
            "Piercing",
            "Jump1",
            "Landing1",
            "Melee_Explosion1",
            "Melee_Explosion2",
            "Melee_Explosion3",
            "Melee_Explosion4",
            "PistolEmit",
            "FlameBurning1",
            "FlameEmit1",
            "SlashEmitSpd1",
            "SlashEmitSpd2",
            "SlashEmitSpd3",
            "FootStep1",
            "DoorOpen",
            "DoorClose",
            "WaterSplashSpd1",
            "Pickup1"
        };
        var audioClipDict = new Dictionary<string, AudioClip>();
        foreach (string name in allSfxClipsNames) {
            string prefabPathUnderResources = "SFX/" + name;
            var theClip = Resources.Load(prefabPathUnderResources) as AudioClip;
            audioClipDict[name] = theClip;
        }

        for (int i = 0; i < sfxNodeCacheCapacity; i++) {
            GameObject newSfxNode = Instantiate(sfxSourcePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ), Quaternion.identity, underlyingMap.transform);
            SFXSource newSfxSource = newSfxNode.GetComponent<SFXSource>();
            newSfxSource.score = -1;
            newSfxSource.maxDistanceInWorld = effectivelyInfinitelyFar * 0.25f;
            newSfxSource.audioClipDict = audioClipDict;
            int initLookupKey = i;
            cachedSfxNodes.Put(initLookupKey, newSfxSource);
        }

        Debug.Log("preallocateSfxNodes ends");
    }

    protected void preallocateNpcNodes() {
        Debug.Log("preallocateNpcNodes begins");

        if (0 >= preallocNpcCapacity) {
            throw new ArgumentException(String.Format("preallocNpcCapacity={0} is non-positive, please initialize it first!", preallocNpcCapacity));
        }

        if (null != cachedNpcs) {
            foreach (var (_, v) in cachedNpcs) {
                while (0 < v.Cnt()) {
                    var g = v.Pop();
                    if (null != g) {
                        Destroy(g.gameObject);
                    }
                }
            }
        }

        var mapProps = underlyingMap.GetComponent<SuperCustomProperties>();
        CustomProperty npcPreallocCapDict, levelIdProp, defaultBgmIdProp;
        mapProps.TryGetCustomProperty("npcPreallocCapDict", out npcPreallocCapDict);
        mapProps.TryGetCustomProperty("levelId", out levelIdProp);
        mapProps.TryGetCustomProperty("bgmId", out defaultBgmIdProp);
        if (null == npcPreallocCapDict || npcPreallocCapDict.IsEmpty) {
            throw new ArgumentNullException("No `npcPreallocCapDict` found on map-scope properties, it's required! Example\n\tvalue `1:16;3:15;4096:1` means that we preallocate 16 slots for species 1, 15 slots for species 3 and 1 slot for species 4096");
        }
        levelId = (null == levelIdProp || levelIdProp.IsEmpty ? LEVEL_NONE : levelIdProp.GetValueAsInt());
        int defaultBgmId = (null == defaultBgmIdProp || defaultBgmIdProp.IsEmpty ? BGM_NO_CHANGE : defaultBgmIdProp.GetValueAsInt());
        if (BGM_NO_CHANGE != defaultBgmId) {
            bgmSource.PlaySpecifiedBgm(defaultBgmId);
        } else {
            bgmSource.PlaySpecifiedBgm(1);
        }
        Dictionary<uint, int> npcPreallocCapDictVal = new Dictionary<uint, int>();
        string npcPreallocCapDictStr = npcPreallocCapDict.GetValueAsString();
        foreach (var kvPairPart in npcPreallocCapDictStr.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries)) {
            var intraKvPairParts = kvPairPart.Split(':', StringSplitOptions.RemoveEmptyEntries);
            uint speciesId = (uint)intraKvPairParts[0].Trim().ToInt();
            int speciesCapacity = intraKvPairParts[1].Trim().ToInt();
            npcPreallocCapDictVal[speciesId] = speciesCapacity;
        }
        npcSpeciesPrefabDict = new Dictionary<uint, GameObject>();
        usedNpcNodes = new HashSet<(int, uint)>();
        cachedNpcs = new Dictionary<uint, KvPriorityQueue<int, CharacterAnimController>>();
        foreach (var kvPair in npcPreallocCapDictVal) {
            uint speciesId = kvPair.Key;
            int speciesCapacity = kvPair.Value;
            var cachedNpcNodesOfThisSpecies = new KvPriorityQueue<int, CharacterAnimController>(speciesCapacity, cachedNpcScore);
            var thePrefab = loadCharacterPrefab(characters[speciesId]);
            npcSpeciesPrefabDict[speciesId] = thePrefab;
            for (int i = 0; i < speciesCapacity; i++) {
                GameObject newNpcNode = Instantiate(thePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, characterZ), Quaternion.identity, underlyingMap.transform);
                CharacterAnimController newNpcNodeController = newNpcNode.GetComponent<CharacterAnimController>();
                newNpcNodeController.score = -1;
                newNpcNodeController.SetNpcId(TERMINATING_PLAYER_ID);
                int initLookupKey = i;
                cachedNpcNodesOfThisSpecies.Put(initLookupKey, newNpcNodeController);
            }
            cachedNpcs[speciesId] = cachedNpcNodesOfThisSpecies;
        }

        Debug.Log("preallocateNpcNodes ends");
    }

    protected void preallocateBattleDynamicsHolder() {
        preallocateStepHolders(
            roomCapacity,
            384,
            preallocNpcCapacity,
            preallocBulletCapacity,
            preallocTrapCapacity,
            preallocTriggerCapacity,
            preallocPickableCapacity,
            out renderBuffer,
            out pushbackFrameLogBuffer,
            out inputBuffer,
            out lastIndividuallyConfirmedInputFrameId,
            out lastIndividuallyConfirmedInputList,
            out effPushbacks,
            out hardPushbackNormsArr,
            out softPushbacks,
            out decodedInputHolder,
            out prevDecodedInputHolder,
            out confirmedBattleResult,
            out softPushbackEnabled,
            frameLogEnabled
        );

        joinIndexRemap = new Dictionary<int, int>();
        disconnectedPeerJoinIndices = new HashSet<int>();
        justDeadNpcIndices = new HashSet<int>();
        fulfilledTriggerSetMask = 0;
        othersForcedDownsyncRenderFrameDict = new Dictionary<int, RoomDownsyncFrame>();
        missionTriggerLocalId = TERMINATING_EVTSUB_ID_INT;
        bossSavepointMask = 0u;
        triggerForceCtrlMask = 0u;
        bossSpeciesSet.Clear();
        latestBossSavepoint = null;
    }

    protected void preallocateFrontendOnlyHolders() {
        //---------------------------------------------FRONTEND USE ONLY SEPERARTION---------------------------------------------
        joinIndexToColorSwapRuleLock = new Dictionary<int, int>();
        playerSpeciesIdOccurrenceCnt = new Dictionary<uint, int>();

        prefabbedInputListHolder = new ulong[roomCapacity];
        Array.Fill<ulong>(prefabbedInputListHolder, 0);
        // windy layers
        windyLayers = new List<SuperTileLayer>();

        // fireball
        int fireballHoldersCap = 48;
        if (null != cachedFireballs) {
            for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
                var (res, fireball) = cachedFireballs.vals.GetByFrameId(i);
                if (!res || null == fireball) throw new ArgumentNullException(String.Format("There's no cachedFireball for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));
                Destroy(fireball.gameObject);
            }
        }
        cachedFireballs = new KvPriorityQueue<int, FireballAnimController>(fireballHoldersCap, cachedFireballScore);

        for (int i = 0; i < fireballHoldersCap; i++) {
            // Fireballs & explosions should be drawn above any character
            GameObject newFireballNode = Instantiate(fireballPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            FireballAnimController holder = newFireballNode.GetComponent<FireballAnimController>();
            holder.score = -1;
            int initLookupKey = (-(i + 1)); // there's definitely no such "bulletLocalId"
            cachedFireballs.Put(initLookupKey, holder);
        }

        // pickable
        int pickableHoldersCap = 16;
        if (null != cachedPickables) {
            for (int i = cachedPickables.vals.StFrameId; i < cachedPickables.vals.EdFrameId; i++) {
                var (res, pickable) = cachedPickables.vals.GetByFrameId(i);
                if (!res || null == pickable) throw new ArgumentNullException(String.Format("There's no cachedPickable for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPickables.vals.StFrameId, cachedPickables.vals.EdFrameId));
                Destroy(pickable.gameObject);
            }
        }
        cachedPickables = new KvPriorityQueue<int, PickableAnimController>(pickableHoldersCap, cachedPickableScore);

        for (int i = 0; i < pickableHoldersCap; i++) {
            GameObject newPickableNode = Instantiate(pickablePrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            PickableAnimController holder = newPickableNode.GetComponent<PickableAnimController>();
            holder.score = -1;
            int initLookupKey = (-(i + 1)); // there's definitely no such "bulletLocalId"
            cachedPickables.Put(initLookupKey, holder);
        }

        // team ribbon
        int teamRibbonHoldersCap = roomCapacity + preallocNpcCapacity;
        if (null != cachedTeamRibbons) {
            for (int i = cachedTeamRibbons.vals.StFrameId; i < cachedTeamRibbons.vals.EdFrameId; i++) {
                var (res, teamRibbons) = cachedTeamRibbons.vals.GetByFrameId(i);
                if (!res || null == teamRibbons) throw new ArgumentNullException(String.Format("There's no cachedTeamRibbon for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedTeamRibbons.vals.StFrameId, cachedTeamRibbons.vals.EdFrameId));
                Destroy(teamRibbons.gameObject);
            }
        }
        cachedTeamRibbons = new KvPriorityQueue<int, TeamRibbon>(teamRibbonHoldersCap, cachedTeamRibbonScore);
        for (int i = 0; i < teamRibbonHoldersCap; i++) {
            GameObject newTeamRibbonNode = Instantiate(teamRibbonPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            TeamRibbon holder = newTeamRibbonNode.GetComponent<TeamRibbon>();
            holder.score = -1;
            int initLookupKey = (-(i + 1));
            cachedTeamRibbons.Put(initLookupKey, holder);
        }

        int keyChPointerHoldersCap = roomCapacity + (preallocNpcCapacity >> 2);
        cachedKeyChPointers = new KvPriorityQueue<int, KeyChPointer>(keyChPointerHoldersCap, cachedKeyChPointerScore);
        for (int i = 0; i < keyChPointerHoldersCap; i++) {
            GameObject newKeyChPointerNode = Instantiate(keyChPointerPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            KeyChPointer holder = newKeyChPointerNode.GetComponent<KeyChPointer>();
            holder.score = -1;
            int initLookupKey = (-(i + 1));
            cachedKeyChPointers.Put(initLookupKey, holder);
        }

        // hp bar
        if (null != cachedHpBars) {
            for (int i = cachedHpBars.vals.StFrameId; i < cachedHpBars.vals.EdFrameId; i++) {
                var (res, hpBar) = cachedHpBars.vals.GetByFrameId(i);
                if (!res || null == hpBar) throw new ArgumentNullException(String.Format("There's no cachedHpBar for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedHpBars.vals.StFrameId, cachedHpBars.vals.EdFrameId));
                Destroy(hpBar.gameObject);
            }
        }
        int hpBarHoldersCap = teamRibbonHoldersCap;
        cachedHpBars = new KvPriorityQueue<int, InplaceHpBar>(teamRibbonHoldersCap, cachedHpBarScore);

        for (int i = 0; i < hpBarHoldersCap; i++) {
            GameObject newHpBarNode = Instantiate(inplaceHpBarPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            InplaceHpBar holder = newHpBarNode.GetComponent<InplaceHpBar>();
            holder.score = -1;
            int initLookupKey = (-(i + 1));
            cachedHpBars.Put(initLookupKey, holder);
        }

        // debug line
        if (debugDrawingAllocation) {
            int lineHoldersCap = 64;
            if (null != cachedLineRenderers) {
                for (int i = cachedLineRenderers.vals.StFrameId; i < cachedLineRenderers.vals.EdFrameId; i++) {
                    var (res, line) = cachedLineRenderers.vals.GetByFrameId(i);
                    if (!res || null == line) throw new ArgumentNullException(String.Format("There's no line for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedLineRenderers.vals.StFrameId, cachedLineRenderers.vals.EdFrameId));
                    Destroy(line.gameObject);
                }
            }

            cachedLineRenderers = new KvPriorityQueue<int, DebugLine>(lineHoldersCap, cachedLineScore);
            for (int i = 0; i < lineHoldersCap; i++) {
                GameObject newLineObj = Instantiate(linePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, lineRendererZ), Quaternion.identity, underlyingMap.transform);
                DebugLine newLine = newLineObj.GetComponent<DebugLine>();
                newLine.score = -1;
                newLine.SetWidth(2.0f);
                int initLookupKey = i;
                cachedLineRenderers.Put(initLookupKey, newLine);
            }
        }
    }

    protected void calcCameraCaps() {
        int camFovW = (int)(2.0f * gameplayCamera.orthographicSize * gameplayCamera.aspect);
        int camFovH = (int)(2.0f * gameplayCamera.orthographicSize);
        int paddingX = (camFovW >> 1);
        int paddingY = (camFovH >> 1);
        cameraCapMinX = 0 + paddingX;
        cameraCapMaxX = (spaceOffsetX << 1) - paddingX;

        cameraCapMinY = -(spaceOffsetY << 1) + paddingY;
        cameraCapMaxY = 0 - paddingY;

        effectivelyInfinitelyFar = 4f * Math.Max(spaceOffsetX, spaceOffsetY);
    }

    protected virtual void resetCurrentMatch(string theme) {
        if (null != underlyingMap) {
            Destroy(underlyingMap);
        }
        Debug.Log(String.Format("resetCurrentMatch with roomCapacity={0}", roomCapacity));

        battleState = ROOM_STATE_IMPOSSIBLE;
        levelId = LEVEL_NONE;
        justTriggeredStoryPointId = STORY_POINT_NONE;
        justTriggeredBgmId = BGM_NO_CHANGE;
        playerRdfId = 0;
        settlementRdfId = 0;
        chaserRenderFrameId = -1;
        chaserRenderFrameIdLowerBound = -1;
        lastAllConfirmedInputFrameId = -1;
        lastUpsyncInputFrameId = -1;
        localExtraInputDelayFrames = 0;
        if (null != confirmedBattleResult) {
            resetBattleResult(ref confirmedBattleResult);
        }

        /*
         [WARNING]

         By observing "NetworkDoctorInfo.XxxIndicator", it's found that "chasedToPlayerRdfIdIndicator" is most often lit, even during obvious graphical inconsistencies. Therefore the combination "smallChasingRenderFramesPerUpdate = 2 && bigChasingRenderFramesPerUpdate = 4" back then was considered too small. 
        
         The current combination is having much better field test results in terms of graphical consistencies.
         */
        smallChasingRenderFramesPerUpdate = (int)(1UL << INPUT_SCALE_FRAMES); // [WARNING] When using "smallChasingRenderFramesPerUpdate", we're giving more chance to "lockstep"
        bigChasingRenderFramesPerUpdate = (int)(2UL << INPUT_SCALE_FRAMES) - 1;
        rdfIdToActuallyUsedInput = new Dictionary<int, InputFrameDownsync>();
        unconfirmedBattleResult = new Dictionary<int, BattleResult>();

        playerGameObjs = new GameObject[roomCapacity];
        currRdfNpcAnimHolders = new CharacterAnimController[DEFAULT_PREALLOC_NPC_CAPACITY];
        dynamicTrapGameObjs = new List<GameObject>();
        triggerGameObjs = new Dictionary<int, GameObject>();
        string path = String.Format("Tiled/{0}/map", theme);
        var underlyingMapPrefab = Resources.Load(path) as GameObject;
        if (null == underlyingMapPrefab) {
            Debug.LogErrorFormat("underlyingMapPrefab is null for theme={0}", theme);
        }
        underlyingMap = GameObject.Instantiate(underlyingMapPrefab, this.gameObject.transform);

        var superMap = underlyingMap.GetComponent<SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        selfBattleHeading.ResetSelf();
        readyGoPanel.resetCountdown();
        settlementPanel.gameObject.SetActive(false);

        iptmgr.btnB.GetComponent<InventorySlot>().resumeRegularBtnB();
    }

    public void onInputFrameDownsyncBatch(RepeatedField<InputFrameDownsync> batch) {
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

        NetworkDoctor.Instance.LogInputFrameDownsync(batch[0].InputFrameId, batch[batch.Count - 1].InputFrameId);
        int firstPredictedYetIncorrectInputFrameId = TERMINATING_INPUT_FRAME_ID;
        foreach (var inputFrameDownsync in batch) {
            int inputFrameDownsyncId = inputFrameDownsync.InputFrameId;
            if (inputFrameDownsyncId <= lastAllConfirmedInputFrameId) {
                continue;
            }
            if (inputFrameDownsyncId > inputBuffer.EdFrameId) {
                //Debug.LogWarning(String.Format("Possibly resyncing#1 for inputFrameDownsyncId={0}! Now inputBuffer: {1}", inputFrameDownsyncId, inputBuffer.toSimpleStat()));
            }
            // [WARNING] Now that "inputFrameDownsyncId > self.lastAllConfirmedInputFrameId", we should make an update immediately because unlike its backend counterpart "Room.LastAllConfirmedInputFrameId", the frontend "mapIns.lastAllConfirmedInputFrameId" might inevitably get gaps among discrete values due to "either type#1 or type#2 forceConfirmation" -- and only "onInputFrameDownsyncBatch" can catch this! 
            lastAllConfirmedInputFrameId = inputFrameDownsyncId;
            var (res1, localInputFrame) = inputBuffer.GetByFrameId(inputFrameDownsyncId);
            int playerRdfId2 = ConvertToLastUsedRenderFrameId(inputFrameDownsyncId);

            if (null != localInputFrame
              &&
              TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId
              && 
              playerRdfId2 >= chaserRenderFrameIdLowerBound // [WARNING] Such that "inputFrameDownsyncId" has a meaningful impact.
              &&
              !Battle.EqualInputLists(localInputFrame.InputList, inputFrameDownsync.InputList)
            ) {
                firstPredictedYetIncorrectInputFrameId = inputFrameDownsyncId;
            } else if (
                TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId
                &&
                unconfirmedBattleResult.ContainsKey(inputFrameDownsyncId)
                ) {
                // [WARNING] Unconfirmed battle results must be revisited! TODO: Regardless of "playerRdfId2 < chaserRenderFrameIdLowerBound"?
                firstPredictedYetIncorrectInputFrameId = inputFrameDownsyncId;
                unconfirmedBattleResult.Remove(inputFrameDownsyncId);
            }
            // [WARNING] Take all "inputFrameDownsyncBatch" from backend as all-confirmed, it'll be later checked by "rollbackAndChase". 
            inputFrameDownsync.ConfirmedList = (1UL << roomCapacity) - 1;

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
            } else if (RingBuffer<InputFrameDownsync>.RING_BUFF_NON_CONSECUTIVE_SET == res2) {
                Debug.LogWarning(String.Format("Possibly resyncing#2! Now inputBuffer: {0}", inputBuffer.toSimpleStat()));
            }
        }
        _markConfirmationIfApplicable();
        _handleIncorrectlyRenderedPrediction(firstPredictedYetIncorrectInputFrameId, false);
    }

    public void onRoomDownsyncFrame(RoomDownsyncFrame pbRdf, RepeatedField<InputFrameDownsync> accompaniedInputFrameDownsyncBatch, bool usingOthersForcedDownsyncRenderFrameDict = false) {
        // This function is also applicable to "re-joining".
        onInputFrameDownsyncBatch(accompaniedInputFrameDownsyncBatch); // Important to do this step before setting IN_BATTLE
        if (null == renderBuffer) {
            return;
        }
        if (ROOM_STATE_IN_SETTLEMENT == battleState) {
            return;
        }
        int rdfId = pbRdf.Id;
        if (rdfId <= chaserRenderFrameIdLowerBound) {
            //Debug.LogWarningFormat("No need to handle downsynced rdfId={0} because chaserRenderFrameIdLowerBound={1}! @playerRdfId={2}, chaserRenderFrameId={3}, renderBuffer=[{4}], inputBuffer=[{5}]", rdfId, chaserRenderFrameIdLowerBound, playerRdfId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
            return;
        }

        bool shouldForceDumping1 = (DOWNSYNC_MSG_ACT_BATTLE_START == rdfId || usingOthersForcedDownsyncRenderFrameDict);
        bool shouldForceDumping2 = (rdfId > playerRdfId); // In "OnlineMapController", the call sequence per "Update" is "[pollAndHandleWsRecvBuffer >> onRoomDownsyncFrame] > [doUpdate >> rollbackAndChase(playerRdfId, playerRdfId+1)]", thus using strict inequality here.
        bool shouldForceResync = pbRdf.ShouldForceResync;
        ulong selfJoinIndexMask = ((ulong)1 << (selfPlayerInfo.JoinIndex - 1));
        bool selfUnconfirmed = (0 < (pbRdf.BackendUnconfirmedMask & selfJoinIndexMask));
        bool selfConfirmed = !selfUnconfirmed;
        if (selfConfirmed && shouldForceDumping2) {
            /*
            [WARNING]

            When "selfConfirmed && false == shouldForceDumping2", it allows "shouldForceResync" to remain true!
            */
            shouldForceDumping2 = false;
            shouldForceResync = false;
            if (useOthersForcedDownsyncRenderFrameDict) {
                othersForcedDownsyncRenderFrameDict[rdfId] = pbRdf;
            }
        }

        var (oldRdfExists, oldRdf) = renderBuffer.GetByFrameId(rdfId);
        var (dumpRenderCacheRet, oldStRenderFrameId, oldEdRenderFrameId) = (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) ? renderBuffer.SetByFrameId(pbRdf, rdfId) : (RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET, TERMINATING_RENDER_FRAME_ID, TERMINATING_RENDER_FRAME_ID);

        if (RingBuffer<RoomDownsyncFrame>.RING_BUFF_FAILED_TO_SET == dumpRenderCacheRet) {
            throw new ArgumentException(String.Format("Failed to dump render cache#1 (maybe recentRenderCache too small)! rdfId={0}", rdfId));
        }

        bool isRingBuffConsecutiveSet = (RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet);
        bool hasRollbackBurst = false;

        if (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) {
            /*
            [WARNING] 

            "selfConfirmed && rdfId > playerRdfId" WOULD NOT IMMEDIATELY enter this block due to the mutation of "shouldForceDumping2" and "shouldForceResync" -- however it'd defer to enter here by "shouldForceDumping1 = usingOthersForcedDownsyncRenderFrameDict".  
            */
            if (DOWNSYNC_MSG_ACT_BATTLE_START == rdfId) {
                Debug.LogFormat("On battle started! received rdfId={0}", rdfId);
            } else {
                /*  
                    [WARNING] DON'T allow silent replacement of incorrectly calculated history!
                
                    Regarding the assignment to "chaserRenderFrameId", there is no need to calculate "chaserRenderFrameIdCandidate = ConvertToFirstUsedRenderFrameId(ConvertToDelayedInputFrameId(rdfId))", because no correction to input history is made here (even if there were, it would've been done in "onInputFrameDownsyncBatch"). 

                    The consideration behind 
                    ```
                    if (!EqualRdfs(...) && chaserRenderFrameId > rdfId) {
                        chaserRenderFrameId = rdfId;
                    }
                    if (chaserRenderFrameId < rdfId) {
                        chaserRenderFrameId = rdfId;
                    }
                    ```

                    is as follows:
                    - when we're having a "history update", it's implied that the local calculation of "renderBuffer" for "(rdfId-1) => rdfId" was incorrect w.r.t. backend dynamics, so we can only re-chase from "rdfId", i.e. neither (rdfId-1) nor (rdfId+1);
                    - as of the framelogs, updating "chaserRenderFrameId" would only impact "rollbackAndChase(...)" which only updates "rdfIdToActuallyUsedInput" -- yet no correction to input history as aforementioned, and "renderBuffer" would only be wrapped up at the end;
                    - if "chaserRenderFrameId < rdfId", we need pump up "chaserRenderFrameId" also by "chaserRenderFrameId = rdfId", OTHERWISE IF "false == EqualRdfs(oldRdf, pbRdf)" for now there's a chance that by later "rollbackAndChase(...)" we go through the same local calculation errors AGAIN and overwrite "renderBuffer" with a "wrong & new frame at rdfId"; OR IF "true == EqualRdfs(oldRdf, pbRdf)" for now there's no harm to just pump up to a ground truth and reduce calculation.   
                */
                if (null == accompaniedInputFrameDownsyncBatch) {
                    if (usingOthersForcedDownsyncRenderFrameDict) {
                        // [WARNING] "!EqualRdfs(oldRdf, pbRdf, roomCapacity)" already checked
                        //Debug.LogFormat("On battle resynced history update from othersForcedDownsyncRenderFrameDict#1! @playerRdfId={0}, chaserRenderFrameId={1}, renderBuffer=[{2}], inputBuffer=[{3}], isRingBuffConsecutiveSet={4}, chaserRenderFrameIdLowerBound={5}", playerRdfId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat(), isRingBuffConsecutiveSet, chaserRenderFrameIdLowerBound);

                        // [WARNING] It's impossible that "true == usingOthersForcedDownsyncRenderFrameDict && chaserRenderFrameId > rdfId" because "rdfId == playerRdfId+1" in this case -- hence there's no chance of rollback burst and we should update "chaserRenderFrameIdLowerBound" as well.
                        chaserRenderFrameId = rdfId;
                        chaserRenderFrameIdLowerBound = rdfId;
                        NetworkDoctor.Instance.LogForceResyncFutureApplied();
                    } else {
                        // Check for "hasRollbackBurst" 
                        if (oldRdfExists && null != oldRdf && !EqualRdfs(oldRdf, pbRdf, roomCapacity)) {         
                            //Debug.LogFormat("On battle resynced history update {4}#1! @playerRdfId={0}, chaserRenderFrameId={1}; received rdfId={2} & isRingBuffConsecutiveSet={3}, chaserRenderFrameIdLowerBound={5}", playerRdfId, chaserRenderFrameId, rdfId, isRingBuffConsecutiveSet, selfUnconfirmed ? "for self" : "from another player", chaserRenderFrameIdLowerBound);
                            if (0 > chaserRenderFrameId || chaserRenderFrameId > rdfId) {
                                chaserRenderFrameId = rdfId;
                                hasRollbackBurst = true;
                            }
                        }

                        // Even if "false == hasRollbackBurst", there's no point keeping "chaserRenderFrameId < rdfId" now because "pbRdf" is a ground truth from backend.
                        if (chaserRenderFrameId < rdfId) {
                            chaserRenderFrameId = rdfId;
                        }
                        if (chaserRenderFrameIdLowerBound < rdfId) {
                            chaserRenderFrameIdLowerBound = rdfId;
                        }

                        // Kindly note that if "chaserRenderFrameId > rdfId && (!oldRdfExists || EqualRdfs(oldRdf, pbRdf, roomCapacity))", then "chaserRenderFrameId" will remain unchanged
                    }
                } else {
                    if (usingOthersForcedDownsyncRenderFrameDict) {
                        // [WARNING] "!EqualRdfs(oldRdf, pbRdf, roomCapacity)" already checked
                        //Debug.LogFormat("On battle resynced history update from othersForcedDownsyncRenderFrameDict#2! @playerRdfId={3}, chaserRenderFrameId={4}, renderBuffer=[{5}], inputBuffer=[{6}], isRingBuffConsecutiveSet={7}, chaserRenderFrameIdLowerBound={8}; received rdfId={0} & accompaniedInputFrameDownsyncBatch[{1}, ..., {2}]", rdfId, accompaniedInputFrameDownsyncBatch[0].InputFrameId, accompaniedInputFrameDownsyncBatch[accompaniedInputFrameDownsyncBatch.Count - 1].InputFrameId, playerRdfId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat(), isRingBuffConsecutiveSet, chaserRenderFrameIdLowerBound);

                        // [WARNING] It's impossible that "true == usingOthersForcedDownsyncRenderFrameDict && chaserRenderFrameId > rdfId" because "rdfId == playerRdfId+1" in this case -- hence there's no chance of rollback burst and we should update "chaserRenderFrameIdLowerBound" as well.
                        chaserRenderFrameId = rdfId;
                        chaserRenderFrameIdLowerBound = rdfId;
                        NetworkDoctor.Instance.LogForceResyncFutureApplied();
                    } else {
                        // Check for "hasRollbackBurst" 
                        if (oldRdfExists && null != oldRdf && !EqualRdfs(oldRdf, pbRdf, roomCapacity)) {         
                            //Debug.LogFormat("On battle resynced history update {4}#2! @playerRdfId={0}, chaserRenderFrameId={1}; received rdfId={2} & isRingBuffConsecutiveSet={3}, chaserRenderFrameIdLowerBound={5}", playerRdfId, chaserRenderFrameId, rdfId, isRingBuffConsecutiveSet, selfUnconfirmed ? "for self" : "from another player", chaserRenderFrameIdLowerBound);
                            if (0 > chaserRenderFrameId || chaserRenderFrameId > rdfId) {
                                chaserRenderFrameId = rdfId;
                                hasRollbackBurst = true;
                            }
                        }

                        // Even if "false == hasRollbackBurst", there's no point keeping "chaserRenderFrameId < rdfId" now because "pbRdf" is a ground truth from backend.
                        if (chaserRenderFrameId < rdfId) {
                            chaserRenderFrameId = rdfId;
                        }
                        if (chaserRenderFrameIdLowerBound < rdfId) {
                            chaserRenderFrameIdLowerBound = rdfId;
                        }

                        // Kindly note that if "chaserRenderFrameId > rdfId && (!oldRdfExists || EqualRdfs(oldRdf, pbRdf, roomCapacity))", then "chaserRenderFrameId" will remain unchanged
                    }
                }
            }

            if (DOWNSYNC_MSG_ACT_BATTLE_START == rdfId || RingBuffer<RoomDownsyncFrame>.RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet) {
                playerRdfId = rdfId; // [WARNING] It's important NOT to re-assign "playerRdfId" when "RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet", e.g. when "true == usingOthersForcedDownsyncRenderFrameDict" (on the ACTIVE NORMAL TICKER side).
                NetworkDoctor.Instance.LogForceResyncImmediatePump(); // [WARNING] "selfUnconfirmed" DOESN'T imply "RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet" and this is verified in practice by several tens of internet battle tests.
                pushbackFrameLogBuffer.Clear();
                pushbackFrameLogBuffer.StFrameId = rdfId;
                pushbackFrameLogBuffer.EdFrameId = rdfId;
                // [WARNING] Don't break chasing in other "RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet" cases (except for "usingOthersForcedDownsyncRenderFrameDict" and "self-unconfirmed"), otherwise the "unchased" history rdfs & ifds between "[chaserRenderFrameId, rdfId)" can become incorrectly remained in framelog (which is written by rollbackAndChase)! 
                chaserRenderFrameId = rdfId;
                chaserRenderFrameIdLowerBound = rdfId;
            }

            // [WARNING] Validate and correct "chaserRenderFrameId" at the end of "onRoomDownsyncFrame", it's sometimes necessary when "RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet"
            if (chaserRenderFrameId < renderBuffer.StFrameId) {
                chaserRenderFrameId = renderBuffer.StFrameId;
            }
            if (chaserRenderFrameIdLowerBound < renderBuffer.StFrameId) {
                chaserRenderFrameIdLowerBound = renderBuffer.StFrameId;
            }

            if (pbRdf.ShouldForceResync) {
                bool exclusivelySelfConfirmedAtLastForceResync = ((0 < pbRdf.BackendUnconfirmedMask) && selfConfirmed);
                ulong allConfirmedMask = (1UL << roomCapacity) - 1;
                bool exclusivelySelfUnconfirmedAtLastForceResync = (allConfirmedMask != pbRdf.BackendUnconfirmedMask && selfUnconfirmed);
                int lastForceResyncedIfdId = lastAllConfirmedInputFrameId; // Because "[onInputFrameDownsyncBatch > _markConfirmationIfApplicable]" is already called
                NetworkDoctor.Instance.LogForceResyncedIfdId(lastForceResyncedIfdId, selfConfirmed, selfUnconfirmed, exclusivelySelfConfirmedAtLastForceResync, exclusivelySelfUnconfirmedAtLastForceResync, hasRollbackBurst, inputFrameUpsyncDelayTolerance);

                if (selfConfirmed) {
                    if (null == accompaniedInputFrameDownsyncBatch) {
                        //Debug.LogFormat("On battle resynced for another player#1! @playerRdfId={2}, renderBuffer=[{3}], inputBuffer=[{4}]; received rdfId={0} & no accompaniedInputFrameDownsyncBatch & isRingBuffConsecutiveSet={1}", rdfId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                    } else {
                        //Debug.LogFormat("On battle resynced for another player#2! @playerRdfId={4}, renderBuffer=[{5}], inputBuffer=[{6}]; received rdfId={0} & accompaniedInputFrameDownsyncBatch[{1}, ..., {2}] & isRingBuffConsecutiveSet={3}", rdfId, accompaniedInputFrameDownsyncBatch[0].InputFrameId, accompaniedInputFrameDownsyncBatch[accompaniedInputFrameDownsyncBatch.Count - 1].InputFrameId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                    }
                } else {
                    // selfUnconfirmed
                    if (!usingOthersForcedDownsyncRenderFrameDict) {
                        if (null == accompaniedInputFrameDownsyncBatch) {
                            //Debug.LogFormat("On battle resynced for self#1! @playerRdfId={2}, renderBuffer=[{3}], inputBuffer=[{4}]; received rdfId={0} & no accompaniedInputFrameDownsyncBatch & isRingBuffConsecutiveSet={1};", rdfId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                        } else {
                            //Debug.LogFormat("On battle resynced for self#2! @playerRdfId={4}, renderBuffer=[{5}], inputBuffer=[{6}]; received rdfId={0} & accompaniedInputFrameDownsyncBatch[{1}, ..., {2}] & isRingBuffConsecutiveSet={3}", rdfId, accompaniedInputFrameDownsyncBatch[0].InputFrameId, accompaniedInputFrameDownsyncBatch[accompaniedInputFrameDownsyncBatch.Count - 1].InputFrameId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                        }
                    }
                }
            }

            battleState = ROOM_STATE_IN_BATTLE;
        }

        // [WARNING] Leave all graphical updates in "Update()" by "applyRoomDownsyncFrameDynamics"
    }

    // Update is called once per frame
    protected void doUpdate() {
        int toGenerateInputFrameId = ConvertToDynamicallyGeneratedDelayInputFrameId(playerRdfId, localExtraInputDelayFrames);
        ulong prevSelfInput = 0, currSelfInput = 0;
        if (ShouldGenerateInputFrameUpsync(playerRdfId)) {
            (prevSelfInput, currSelfInput) = getOrPrefabInputFrameUpsync(toGenerateInputFrameId, true, prefabbedInputListHolder);
            if (inputBuffer.EdFrameId <= toGenerateInputFrameId) {
                Debug.LogWarningFormat("After getOrPrefabInputFrameUpsync at playerRdfId={0}, toGenerateInputFrameId={1}; inputBuffer={2}", playerRdfId, toGenerateInputFrameId, inputBuffer.toSimpleStat());
            }
        }
        int delayedInputFrameId = ConvertToDelayedInputFrameId(playerRdfId);
        var (delayedInputFrameExists, _) = inputBuffer.GetByFrameId(delayedInputFrameId);
        if (!delayedInputFrameExists) {
            // Possible edge case after resync, kindly note that it's OK to prefab a "future inputFrame" here, because "sendInputFrameUpsyncBatch" would be capped by "noDelayInputFrameId from this.playerRdfId". 
            // Debug.LogWarning(String.Format("@playerRdfId={0}, prefabbing delayedInputFrameId={1} while lastAllConfirmedInputFrameId={2}, inputBuffer:{3}", playerRdfId, delayedInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat()));
            getOrPrefabInputFrameUpsync(delayedInputFrameId, false, prefabbedInputListHolder);
        }

        bool battleResultIsSet = isBattleResultSet(confirmedBattleResult);

        if (isOnlineMode && (battleResultIsSet || shouldSendInputFrameUpsyncBatch(prevSelfInput, currSelfInput, toGenerateInputFrameId))) {
            // [WARNING] If "true == battleResultIsSet", we MUST IMMEDIATELY flush the local inputs to our peers to favor the formation of all-confirmed inputFrameDownsync asap! 
            // TODO: Does the following statement run asynchronously in an implicit manner? Should I explicitly run it asynchronously?
            sendInputFrameUpsyncBatch(toGenerateInputFrameId);
        }

        if (battleResultIsSet) {
            var (ok1, currRdf) = renderBuffer.GetByFrameId(playerRdfId);
            if (ok1 && null != currRdf) {
                cameraTrack(currRdf, null, true);
            }
            return;
        }

        if (!isOnlineMode && 0 < remainingTriggerForceCtrlRdfCount) {
            var (ok1, delayedInputFrameDownsync) = inputBuffer.GetByFrameId(delayedInputFrameId);
            if (ok1 && null != delayedInputFrameDownsync) {
                delayedInputFrameDownsync.InputList[0] = latestTriggerForceCtrlCmd;
                delayedInputFrameDownsync.ConfirmedList = ((1u << roomCapacity) - 1);
            }
            --remainingTriggerForceCtrlRdfCount;
        }

        // Inside the following "rollbackAndChase" actually ROLLS FORWARD w.r.t. the corresponding delayedInputFrame, REGARDLESS OF whether or not "chaserRenderFrameId == playerRdfId" now. 
        var (prevRdf, rdf) = rollbackAndChase(playerRdfId, playerRdfId + 1, collisionSys, false); // Having "prevRdf.Id == playerRdfId" & "rdf.Id == playerRdfId+1" 

        if (useOthersForcedDownsyncRenderFrameDict) {
            // [WARNING] The following calibration against "othersForcedDownsyncRenderFrameDict" can also be placed inside "chaseRolledbackRdfs" for a more rigorous treatment. However when "othersForcedDownsyncRenderFrameDict" is updated, the corresponding "resynced rdf" always has an id not smaller than "playerRdfId", thus no need to take those wasting calibrations.  
            if (othersForcedDownsyncRenderFrameDict.ContainsKey(rdf.Id)) {
                var othersForcedDownsyncRenderFrame = othersForcedDownsyncRenderFrameDict[rdf.Id];
                if (!EqualRdfs(othersForcedDownsyncRenderFrame, rdf, roomCapacity)) {
                    Debug.LogWarningFormat("Mismatched render frame@rdf.id={0} w/ delayedInputFrameId={1}:\nrdf={2}\nothersForcedDownsyncRenderFrame={3}\nnow inputBuffer:{4}, renderBuffer:{5}", rdf.Id, delayedInputFrameId, stringifyRdf(rdf), stringifyRdf(othersForcedDownsyncRenderFrame), inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat());
                    // [WARNING] When this happens, something is intrinsically wrong -- to avoid having an inconsistent history in the "renderBuffer", thus a wrong prediction all the way from here on, clear the history!
                    othersForcedDownsyncRenderFrame.ShouldForceResync = true;
                    othersForcedDownsyncRenderFrame.BackendUnconfirmedMask = ((1ul << roomCapacity) - 1);
                    onRoomDownsyncFrame(othersForcedDownsyncRenderFrame, null, true);
                    Debug.LogWarningFormat("Handled mismatched render frame@rdf.id={0} w/ delayedInputFrameId={1}, playerRdfId={2}:\nnow inputBuffer:{3}, renderBuffer:{4}", rdf.Id, delayedInputFrameId, playerRdfId, inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat());
                }
                othersForcedDownsyncRenderFrameDict.Remove(rdf.Id); // [WARNING] Removes anyway because we won't revisit the same "playerRdfId" in a same battle!
            }
        }

        applyRoomDownsyncFrameDynamics(rdf, prevRdf);
        cameraTrack(rdf, prevRdf, false);

        bool battleResultIsSetAgain = isBattleResultSet(confirmedBattleResult);
        if (!battleResultIsSetAgain) {
            ++playerRdfId;
        }
    }

    protected virtual int chaseRolledbackRdfs() {
        int prevChaserRenderFrameId = chaserRenderFrameId;
        int biggestAllConfirmedRdfId = ConvertToLastUsedRenderFrameId(lastAllConfirmedInputFrameId);
        /*
        [WARNING] 

        As commented in "onPeerInputFrameUpsync", received UDP packets would NOT advance "lastAllConfirmedInputFrameId", hence when "prevChaserRenderFrameId >= biggestAllConfirmedRdfId" we can chase by "smallChasingRenderFramesPerUpdate" and just hope that the UDP packets are advanced enough to make a good prediction!    
        */
        int nextChaserRenderFrameId = (prevChaserRenderFrameId >= biggestAllConfirmedRdfId) ? (prevChaserRenderFrameId + smallChasingRenderFramesPerUpdate) : (prevChaserRenderFrameId + bigChasingRenderFramesPerUpdate);

        if (nextChaserRenderFrameId > playerRdfId) {
            nextChaserRenderFrameId = playerRdfId;
        }

        if (prevChaserRenderFrameId < nextChaserRenderFrameId) {
            // Do not execute "rollbackAndChase" when "prevChaserRenderFrameId == nextChaserRenderFrameId", otherwise if "nextChaserRenderFrameId == self.playerRdfId" we'd be wasting computing power once. 
            rollbackAndChase(prevChaserRenderFrameId, nextChaserRenderFrameId, collisionSys, true);
        }

        return nextChaserRenderFrameId;
    }

    protected virtual void onBattleStopped() {
        if (ROOM_STATE_IMPOSSIBLE != battleState && ROOM_STATE_IN_BATTLE != battleState && ROOM_STATE_IN_SETTLEMENT != battleState) {
            Debug.LogWarningFormat("@playerRdfId={0}, unable to stop battle due to invalid state transition; now battleState={1}", playerRdfId, battleState);
            return;
        }
        playerRdfId = 0;
        bgmSource.Stop();
        battleState = ROOM_STATE_STOPPED;
        
        // Reset the preallocated
        if (null != lastIndividuallyConfirmedInputFrameId) {
            Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        }
        if (null != lastIndividuallyConfirmedInputList) {
            Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        }
        if (frameLogEnabled) {
            if (null != pushbackFrameLogBuffer) {
                pushbackFrameLogBuffer.Clear();
            } 
        }
        if (null != residueCollided) {
            residueCollided.Clear();
        } 
        if (null != prefabbedInputListHolder) {
            Array.Fill<ulong>(prefabbedInputListHolder, 0);
        }
        if (null != iptmgr) {
            iptmgr.ResetSelf();
        }

        Debug.LogWarningFormat("onBattleStopped; now battleState={0}", battleState);
    }

    protected abstract IEnumerator delayToShowSettlementPanel();

    protected abstract bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId);

    protected abstract void sendInputFrameUpsyncBatch(int latestLocalInputFrameId);

    protected void enableBattleInput(bool yesOrNo) {
        iptmgr.enable(yesOrNo);
        iptmgr.gameObject.SetActive(yesOrNo);
    }

    protected string ArrToString(uint[] speciesIdList) {
        var ret = "";
        for (int i = 0; i < speciesIdList.Length; i++) {
            ret += speciesIdList[i].ToString();
            if (i < speciesIdList.Length - 1) ret += ", ";
        }
        return ret;
    }

    protected void patchStartRdf(RoomDownsyncFrame startRdf, uint[] speciesIdList) {
        for (int i = 0; i < roomCapacity; i++) {
            if (SPECIES_NONE_CH == speciesIdList[i]) continue;
            var playerInRdf = startRdf.PlayersArr[i];
            var (playerCposX, playerCposY) = VirtualGridToPolygonColliderCtr(playerInRdf.VirtualGridX, playerInRdf.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(playerCposX, playerCposY, spaceOffsetX, spaceOffsetY);

            if (selfPlayerInfo.JoinIndex == i + 1) {
                spawnPlayerNode(playerInRdf.JoinIndex, playerInRdf.SpeciesId, wx, wy, playerInRdf.BulletTeamId);
                continue;
            }

            //Debug.LogFormat("Patching speciesIdList={0} for selfJoinIndex={1}", ArrToString(speciesIdList), selfPlayerInfo.JoinIndex);
            // Only copied species specific part from "mockStartRdf"
            playerInRdf.SpeciesId = speciesIdList[i];
            var chConfig = characters[playerInRdf.SpeciesId];
            playerInRdf.Hp = chConfig.Hp;
            playerInRdf.Mp = chConfig.Mp;
            playerInRdf.Speed = chConfig.Speed;
            playerInRdf.OmitGravity = chConfig.OmitGravity;
            playerInRdf.OmitSoftPushback = chConfig.OmitSoftPushback;
            playerInRdf.RepelSoftPushback = chConfig.RepelSoftPushback;

            if (null != chConfig.InitInventorySlots) {
                for (int t = 0; t < chConfig.InitInventorySlots.Count; t++) {
                    var initIvSlot = chConfig.InitInventorySlots[t];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, initIvSlot.SkillIdAir, initIvSlot.GaugeCharged, initIvSlot.GaugeRequired, initIvSlot.FullChargeSkillId, initIvSlot.FullChargeBuffSpeciesId, playerInRdf.Inventory.Slots[t]);
                }
            }
            spawnPlayerNode(playerInRdf.JoinIndex, playerInRdf.SpeciesId, wx, wy, playerInRdf.BulletTeamId);
        }
    }

    protected (RoomDownsyncFrame, RepeatedField<SerializableConvexPolygon>, RepeatedField<SerializedCompletelyStaticPatrolCueCollider>, RepeatedField<SerializedCompletelyStaticTrapCollider>, RepeatedField<SerializedCompletelyStaticTriggerCollider>, SerializedTrapLocalIdToColliderAttrs, SerializedTriggerEditorIdToLocalId, int) mockStartRdf(uint[] speciesIdList, FinishedLvOption finishedLvOption = FinishedLvOption.StoryAndBoss) {
        Debug.LogFormat("mockStartRdf with speciesIdList={0} for selfJoinIndex={1}, finishedLvOption={2}", ArrToString(speciesIdList), selfPlayerInfo.JoinIndex, finishedLvOption);
        var serializedBarrierPolygons = new RepeatedField<SerializableConvexPolygon>();
        var serializedStaticPatrolCues = new RepeatedField<SerializedCompletelyStaticPatrolCueCollider>();
        var serializedCompletelyStaticTraps = new RepeatedField<SerializedCompletelyStaticTrapCollider>();
        var serializedStaticTriggers = new RepeatedField<SerializedCompletelyStaticTriggerCollider>();
        var serializedTrapLocalIdToColliderAttrs = new SerializedTrapLocalIdToColliderAttrs();
        var serializedTriggerEditorIdToLocalId = new SerializedTriggerEditorIdToLocalId();

        var grid = underlyingMap.GetComponentInChildren<Grid>();
        var playerStartingCposList = new List<(Vector, int, int)>();
        var npcsStartingCposList = new List<(Vector, int, int, uint, int, NpcGoal, int, ulong, int, uint, uint, uint)>();
        var trapList = new List<Trap>();
        var triggerList = new List<(Trigger, float, float)>();
        var pickableList = new List<(Pickable, float, float)>();
        float defaultPatrolCueRadius = 5;
        int trapLocalId = 1;
        int triggerLocalId = 1;
        int pickableLocalId = 1;
        int patrolCueLocalId = 1;

        var mapProps = underlyingMap.GetComponent<SuperCustomProperties>();
        CustomProperty battleDurationSeconds;
        mapProps.TryGetCustomProperty("battleDurationSeconds", out battleDurationSeconds);
        int battleDurationSecondsVal = (null == battleDurationSeconds || battleDurationSeconds.IsEmpty) ? 60 : battleDurationSeconds.GetValueAsInt();

        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "WindyGround1": 
                    {
                        var tmr = child.GetComponent<TilemapRenderer>();
                        var material = tmr.material;
                        if (null != material) {
                            material.SetFloat("_WindSpeed", 2.0f);
                            material.SetFloat("_StepScale", 9.0f);
                        }
                    }
                    windyLayers.Add(child.GetComponent<SuperTileLayer>());
                    break;
                case "WindyGround2": 
                    {
                        var tmr = child.GetComponent<TilemapRenderer>();
                        var material = tmr.material;
                        if (null != material) {
                            material.SetFloat("_WindSpeed", 1.2f);
                            material.SetFloat("_StepScale", 18.0f);
                        }
                    }
                    windyLayers.Add(child.GetComponent<SuperTileLayer>());
                    break;
                case "Barrier":
                    foreach (Transform barrierChild in child) {
                        var barrierTileObj = barrierChild.GetComponent<SuperObject>();
                        var inMapCollider = barrierChild.GetComponent<EdgeCollider2D>();

                        if (null == inMapCollider || 0 >= inMapCollider.pointCount) {
                            var (tiledRectCx, tiledRectCy) = (barrierTileObj.m_X + barrierTileObj.m_Width * 0.5f, barrierTileObj.m_Y + barrierTileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            /*
                             [WARNING] 

                            The "Unity World (0, 0)" is aligned with the top-left corner of the rendered "TiledMap (via SuperMap)".

                            It's noticeable that all the "Collider"s in "CollisionSpace" must be of positive coordinates to work due to the implementation details of "resolv". Thus I'm using a "Collision Space (0, 0)" aligned with the bottom-left of the rendered "TiledMap (via SuperMap)". 
                            */
                            var srcPolygon = NewRectPolygon(rectCx, rectCy, barrierTileObj.m_Width, barrierTileObj.m_Height, 0, 0, 0, 0);
                            serializedBarrierPolygons.Add(srcPolygon.Serialize());
                        } else {
                            var points = inMapCollider.points;
                            List<float> points2 = new List<float>();
                            foreach (var point in points) {
                                points2.Add(point.x);
                                points2.Add(point.y);
                            }
                            var (anchorCx, anchorCy) = TiledLayerPositionToCollisionSpacePosition(barrierTileObj.m_X, barrierTileObj.m_Y, spaceOffsetX, spaceOffsetY);
                            var srcPolygon = new ConvexPolygon(anchorCx, anchorCy, points2.ToArray());
                            serializedBarrierPolygons.Add(srcPolygon.Serialize());
                        }

                        // TODO: By now I have to enable the import of all colliders to see the "inMapCollider: EdgeCollider2D" component, then remove unused components here :(
                        Destroy(barrierChild.GetComponent<EdgeCollider2D>());
                        Destroy(barrierChild.GetComponent<BoxCollider2D>());
                        Destroy(barrierChild.GetComponent<SuperColliderComponent>());
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
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
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "NpcStartingPos":
                    foreach (Transform npcPos in child) {
                        var tileObj = npcPos.gameObject.GetComponent<SuperObject>();
                        var tileProps = npcPos.gameObject.gameObject.GetComponent<SuperCustomProperties>();
                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        if (0 != tileObj.m_Width) {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);
                            (cx, cy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        }
                        
                        CustomProperty dirY, speciesId, teamId, initGoal, publishingEvtSubIdUponKilled, publishingEvtMaskUponKilled, subscriptionId, killedToDropConsumableSpeciesId, killedToDropBuffSpeciesId, killedToDropPickupSkillId;
                        tileProps.TryGetCustomProperty("dirY", out dirY);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("teamId", out teamId);
                        tileProps.TryGetCustomProperty("initGoal", out initGoal);
                        tileProps.TryGetCustomProperty("publishingEvtSubIdUponKilled", out publishingEvtSubIdUponKilled);
                        tileProps.TryGetCustomProperty("publishingEvtMaskUponKilled", out publishingEvtMaskUponKilled);
                        tileProps.TryGetCustomProperty("subscriptionId", out subscriptionId);
                        tileProps.TryGetCustomProperty("killedToDropConsumableSpeciesId", out killedToDropConsumableSpeciesId);
                        tileProps.TryGetCustomProperty("killedToDropBuffSpeciesId", out killedToDropBuffSpeciesId);
                        tileProps.TryGetCustomProperty("killedToDropPickupSkillId", out killedToDropPickupSkillId);

                        uint speciesIdVal = null == speciesId || speciesId.IsEmpty ? SPECIES_NONE_CH : (uint)speciesId.GetValueAsInt();
                        if (SPECIES_BRICK1 == speciesIdVal) {
                            (cx, cy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X + 0.5f*tileObj.m_Width, tileObj.m_Y - 0.5f*tileObj.m_Height, spaceOffsetX, spaceOffsetY);
                        }

                        NpcGoal initGoalVal = NpcGoal.Npatrol;
                        if (null != initGoal && !initGoal.IsEmpty) {
                            var initGoalStr = initGoal.GetValueAsString();
                            Enum.TryParse(initGoalStr, out initGoalVal);
                        }

                        bool xFlipped = isXFlipped(tileObj.m_TileId);

                        npcsStartingCposList.Add((
                                                    new Vector(cx, cy),
                                                    xFlipped ? -2 : +2,
                                                    null == dirY || dirY.IsEmpty ? 0 : dirY.GetValueAsInt(),
                                                    speciesIdVal,
                                                    null == teamId || teamId.IsEmpty ? DEFAULT_BULLET_TEAM_ID : teamId.GetValueAsInt(),
                                                    initGoalVal,
                                                    null == publishingEvtSubIdUponKilled || publishingEvtSubIdUponKilled.IsEmpty ? TERMINATING_EVTSUB_ID_INT : publishingEvtSubIdUponKilled.GetValueAsInt(),
                                                    null == publishingEvtMaskUponKilled || publishingEvtMaskUponKilled.IsEmpty ? 0ul : (ulong)publishingEvtMaskUponKilled.GetValueAsInt(),
                                                    null == subscriptionId || subscriptionId.IsEmpty ? TERMINATING_EVTSUB_ID_INT : subscriptionId.GetValueAsInt(),
                                                    null == killedToDropConsumableSpeciesId || killedToDropConsumableSpeciesId.IsEmpty ? TERMINATING_CONSUMABLE_SPECIES_ID : (uint)killedToDropConsumableSpeciesId.GetValueAsInt(),
                                                    null == killedToDropBuffSpeciesId || killedToDropBuffSpeciesId.IsEmpty ? TERMINATING_BUFF_SPECIES_ID : (uint)killedToDropBuffSpeciesId.GetValueAsInt(),
                                                    null == killedToDropPickupSkillId || killedToDropPickupSkillId.IsEmpty ? NO_SKILL : (uint)killedToDropPickupSkillId.GetValueAsInt()
                        ));
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "PatrolCue":
                    foreach (Transform patrolCueChild in child) {
                        var tileObj = patrolCueChild.GetComponent<SuperObject>();
                        var tileProps = patrolCueChild.GetComponent<SuperCustomProperties>();
                        
                        var (patrolCueCx, patrolCueCy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        if (0 != tileObj.m_Width) {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f);
                            (patrolCueCx, patrolCueCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        }

                        CustomProperty flAct, frAct, flCaptureFrames, frCaptureFrames, fdAct, fuAct, fdCaptureFrames, fuCaptureFrames, collisionTypeMask;
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
                            Id = patrolCueLocalId,
                            FlAct = (null == flAct || flAct.IsEmpty) ? 0 : (ulong)flAct.GetValueAsInt(),
                            FrAct = (null == frAct || frAct.IsEmpty) ? 0 : (ulong)frAct.GetValueAsInt(),
                            FlCaptureFrames = (null == flCaptureFrames || flCaptureFrames.IsEmpty) ? 0 : flCaptureFrames.GetValueAsInt(),
                            FrCaptureFrames = (null == frCaptureFrames || frCaptureFrames.IsEmpty) ? 0 : frCaptureFrames.GetValueAsInt(),

                            FdAct = (null == fdAct || fdAct.IsEmpty) ? 0 : (ulong)fdAct.GetValueAsInt(),
                            FuAct = (null == fuAct || fuAct.IsEmpty) ? 0 : (ulong)fuAct.GetValueAsInt(),
                            FdCaptureFrames = (null == fdCaptureFrames || fdCaptureFrames.IsEmpty) ? 0 : fdCaptureFrames.GetValueAsInt(),
                            FuCaptureFrames = (null == fuCaptureFrames || fuCaptureFrames.IsEmpty) ? 0 : fuCaptureFrames.GetValueAsInt(),
                            CollisionTypeMask = collisionTypeMaskVal
                        };

                        float cueWidth = (0 == tileObj.m_Width ? 2 * defaultPatrolCueRadius : tileObj.m_Width);
                        float cueHeight = (0 == tileObj.m_Height ? 2 * defaultPatrolCueRadius : tileObj.m_Height);

                        var srcPolygon = NewRectPolygon(patrolCueCx, patrolCueCy, cueWidth, cueHeight, 0, 0, 0, 0);
                        serializedStaticPatrolCues.Add(new SerializedCompletelyStaticPatrolCueCollider {
                            Polygon = srcPolygon.Serialize(),
                            Attr = newPatrolCue,
                        });

                        patrolCueLocalId++;
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "TrapStartingPos":
                    foreach (Transform trapChild in child) {
                        var tileObj = trapChild.GetComponent<SuperObject>();
                        var tileProps = trapChild.GetComponent<SuperCustomProperties>();

                        CustomProperty speciesId, providesHardPushback, providesDamage, providesEscape, providesSlipJump, forcesCrouching, isCompletelyStatic, collisionTypeMask, dirY, speed, prohibitsWallGrabbing, subscribesToId, subscribesToIdAfterInitialFire, subscribesToIdAlt, onlyAllowsAlignedVelX, onlyAllowsAlignedVelY, initNoAngularVel;
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("providesHardPushback", out providesHardPushback);
                        tileProps.TryGetCustomProperty("providesDamage", out providesDamage);
                        tileProps.TryGetCustomProperty("providesEscape", out providesEscape);
                        tileProps.TryGetCustomProperty("providesSlipJump", out providesSlipJump);
                        tileProps.TryGetCustomProperty("forcesCrouching", out forcesCrouching);
                        tileProps.TryGetCustomProperty("static", out isCompletelyStatic);
                        tileProps.TryGetCustomProperty("dirY", out dirY);
                        tileProps.TryGetCustomProperty("speed", out speed);
                        tileProps.TryGetCustomProperty("prohibitsWallGrabbing", out prohibitsWallGrabbing);
                        tileProps.TryGetCustomProperty("subscribesToId", out subscribesToId);
                        tileProps.TryGetCustomProperty("subscribesToIdAfterInitialFire", out subscribesToIdAfterInitialFire);
                        tileProps.TryGetCustomProperty("subscribesToIdAlt", out subscribesToIdAlt);
                        tileProps.TryGetCustomProperty("onlyAllowsAlignedVelX", out onlyAllowsAlignedVelX);
                        tileProps.TryGetCustomProperty("onlyAllowsAlignedVelY", out onlyAllowsAlignedVelY);
                        tileProps.TryGetCustomProperty("initNoAngularVel", out initNoAngularVel);

                        int speciesIdVal = speciesId.GetValueAsInt(); // Not checking null or empty for this property because it shouldn't be, and in case it comes empty anyway, this automatically throws an error 
                        bool providesHardPushbackVal = (null != providesHardPushback && !providesHardPushback.IsEmpty && 1 == providesHardPushback.GetValueAsInt()) ? true : false;
                        bool providesDamageVal = (null != providesDamage && !providesDamage.IsEmpty && 1 == providesDamage.GetValueAsInt()) ? true : false;
                        bool providesEscapeVal = (null != providesEscape && !providesEscape.IsEmpty && 1 == providesEscape.GetValueAsInt()) ? true : false;
                        bool providesSlipJumpVal = (null != providesSlipJump && !providesSlipJump.IsEmpty && 1 == providesSlipJump.GetValueAsInt()) ? true : false;
                        bool forcesCrouchingVal = (null != forcesCrouching && !forcesCrouching.IsEmpty && 1 == forcesCrouching.GetValueAsInt()) ? true : false;
                        bool isCompletelyStaticVal = (null != isCompletelyStatic && !isCompletelyStatic.IsEmpty && 1 == isCompletelyStatic.GetValueAsInt()) ? true : false;
                        bool prohibitsWallGrabbingVal = (null != prohibitsWallGrabbing && !prohibitsWallGrabbing.IsEmpty && 1 == prohibitsWallGrabbing.GetValueAsInt()) ? true : false;

                        bool xFlipped = isXFlipped(tileObj.m_TileId);
                        int dirXVal = xFlipped ? -2 : +2;
                        int dirYVal = (null == dirY || dirY.IsEmpty ? 0 : dirY.GetValueAsInt());
                        int speedVal = (null == speed || speed.IsEmpty ? 0 : speed.GetValueAsInt());
                        int subscribesToIdVal = (null == subscribesToId || subscribesToId.IsEmpty ? TERMINATING_EVTSUB_ID_INT : subscribesToId.GetValueAsInt());
                        int subscribesToIdAltVal = (null == subscribesToIdAlt || subscribesToIdAlt.IsEmpty ? TERMINATING_EVTSUB_ID_INT : subscribesToIdAlt.GetValueAsInt());
                        int subscribesToIdAfterInitialFireVal = (null == subscribesToIdAfterInitialFire || subscribesToIdAfterInitialFire.IsEmpty ? TERMINATING_EVTSUB_ID_INT : subscribesToIdAfterInitialFire.GetValueAsInt());
                        bool initNoAngularVelVal = (null != initNoAngularVel && !initNoAngularVel.IsEmpty && 1 == initNoAngularVel.GetValueAsInt()) ? true : false;
                
                        var trapDirMagSq = dirXVal * dirXVal + dirYVal * dirYVal;
                        var invTrapDirMag = InvSqrt32(trapDirMagSq);
                        var trapSpeedXfac = invTrapDirMag * dirXVal;
                        var trapSpeedYfac = invTrapDirMag * dirYVal;

                        int trapVelX = (int)(trapSpeedXfac * speedVal);
                        int trapVelY = (int)(trapSpeedYfac * speedVal);

                        int onlyAllowsAlignedVelXVal = (null == onlyAllowsAlignedVelX || onlyAllowsAlignedVelX.IsEmpty ? 0 : onlyAllowsAlignedVelX.GetValueAsInt());
                        int onlyAllowsAlignedVelYVal = (null == onlyAllowsAlignedVelY || onlyAllowsAlignedVelY.IsEmpty ? 0 : onlyAllowsAlignedVelY.GetValueAsInt());

                        TrapConfig trapConfig = trapConfigs[speciesIdVal];

                        if (TERMINATING_EVTSUB_ID_INT == subscribesToIdVal && TERMINATING_EVTSUB_ID_INT != subscribesToIdAltVal) {
                            throw new ArgumentException("trap species " + trapConfig.SpeciesName + "is having NO subscribesToId BUT subscribesToIdAltVal= " + subscribesToIdAltVal + "!");
                        }

                        if (TERMINATING_EVTSUB_ID_INT != subscribesToIdVal && subscribesToIdVal == subscribesToIdAltVal) {
                            throw new ArgumentException("trap species " + trapConfig.SpeciesName + "is having equal subscribesToId and subscribesToIdAltVal= " + subscribesToIdVal + "!");
                        }         

                        if (TERMINATING_EVTSUB_ID_INT != subscribesToIdAltVal && TERMINATING_EVTSUB_ID_INT != subscribesToIdAfterInitialFireVal) {
                            throw new ArgumentException("trap species " + trapConfig.SpeciesName + "is having both subscribesToId and subscribesToIdAfterInitialFire!");
                        }

                        TrapConfigFromTiled trapConfigFromTiled = new TrapConfigFromTiled {
                            SpeciesId = speciesIdVal,
                            Quota = MAGIC_QUOTA_INFINITE,
                            Speed = speedVal,
                            DirX = dirXVal,
                            DirY = dirYVal,
                            SubscribesToId = subscribesToIdVal, 
                            SubscribesToIdAfterInitialFire = subscribesToIdAfterInitialFireVal,
                            SubscribesToIdAlt = subscribesToIdAltVal, 
                            BoxCw = tileObj.m_Width,
                            BoxCh = tileObj.m_Height,
                            InitNoAngularVel = initNoAngularVelVal, 
                        };

                        tileProps.TryGetCustomProperty("collisionTypeMask", out collisionTypeMask);
                        ulong collisionTypeMaskVal = (null != collisionTypeMask && !collisionTypeMask.IsEmpty) ? (ulong)collisionTypeMask.GetValueAsInt() : 0;

                        TrapColliderAttrArray colliderAttrs = new TrapColliderAttrArray();
                        if (isCompletelyStaticVal) {
                            bool isBottomAnchor = (null != tileObj.m_SuperTile && (null != tileObj.m_SuperTile.m_Sprite || null != tileObj.m_SuperTile.m_AnimationSprites));
                            var (tiledRectCx, tiledRectCy) = isBottomAnchor ? (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f) : (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            var (rectVw, rectVh) = PolygonColliderCtrToVirtualGridPos(tileObj.m_Width, tileObj.m_Height);
                            var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);

                            TrapColliderAttr colliderAttr = new TrapColliderAttr {
                                ProvidesDamage = providesDamageVal,
                                ProvidesHardPushback = providesHardPushbackVal,
                                ProvidesEscape = providesEscapeVal,
                                ProvidesSlipJump = providesSlipJumpVal,
                                ProhibitsWallGrabbing = prohibitsWallGrabbingVal,
                                ForcesCrouching = forcesCrouchingVal,
                                HitboxOffsetX = 0,
                                HitboxOffsetY = 0,
                                HitboxSizeX = rectVw,
                                HitboxSizeY = rectVh,
                                CollisionTypeMask = collisionTypeMaskVal,
                                SpeciesId = speciesIdVal,
                                OnlyAllowsAlignedVelX = onlyAllowsAlignedVelXVal,                               
                                OnlyAllowsAlignedVelY = onlyAllowsAlignedVelYVal,
                                TrapLocalId = TERMINATING_TRAP_ID
                            };

                            colliderAttrs.List.Add(colliderAttr); // [WARNING] A single completely static trap only supports 1 colliderAttr for now.
                            serializedTrapLocalIdToColliderAttrs.Dict[trapLocalId] = colliderAttrs;

                            var srcPolygon = NewRectPolygon(rectCx, rectCy, tileObj.m_Width, tileObj.m_Height, 0, 0, 0, 0);
                            serializedCompletelyStaticTraps.Add(new SerializedCompletelyStaticTrapCollider {
                                Polygon = srcPolygon.Serialize(),
                                Attr = colliderAttr,
                            });

                            bool hasOnlyAllowedDir = (0 != onlyAllowsAlignedVelXVal || 0 != onlyAllowsAlignedVelYVal);
                            // Debug.Log(String.Format("new completely static trap created {0}", trap));
                            Destroy(trapChild.gameObject);
                            if ((TrapBarrier.SpeciesId != speciesIdVal && LinearSpike.SpeciesId != speciesIdVal) || (!hasOnlyAllowedDir && !providesDamageVal && !providesSlipJumpVal && !prohibitsWallGrabbingVal && (0 >= (COLLISION_REFRACTORY_INDEX_PREFIX & collisionTypeMaskVal)))) {
                                // [WARNING] Slipjump platforms often have their own tilelayer painting 
                                var trapPrefab = loadTrapPrefab(trapConfigs[speciesIdVal]);
                                var (wx, wy) = CollisionSpacePositionToWorldPosition(rectCx, rectCy, spaceOffsetX, spaceOffsetY);
                                Instantiate(trapPrefab, new Vector3(wx, wy, triggerZ), Quaternion.identity, underlyingMap.transform);
                            }
                        } else {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                            float spinCos = 1f, spinSin = 0f;
                            float zAngleDegs = trapChild.localEulerAngles.z;
                            spinCos = (float)Math.Cos(Mathf.Deg2Rad*zAngleDegs);
                            spinSin = (float)Math.Sin(Mathf.Deg2Rad*zAngleDegs);
                            Trap trap = new Trap {
                                TrapLocalId = trapLocalId,
                                ConfigFromTiled = trapConfigFromTiled,
                                VirtualGridX = rectCenterVx,
                                VirtualGridY = rectCenterVy,
                                DirX = dirXVal,
                                DirY = dirYVal,
                                VelX = trapVelX,
                                VelY = trapVelY,
                                SpinCos = spinCos,
                                SpinSin = spinSin,
                                IsCompletelyStatic = false,
                            };
                            if (null != tileObj.m_SuperTile && null != tileObj.m_SuperTile.m_CollisionObjects) {
                                var collisionObjs = tileObj.m_SuperTile.m_CollisionObjects;
                                foreach (var collisionObj in collisionObjs) {
                                    bool childProvidesHardPushbackVal = false, childProvidesDamageVal = false, childProvidesEscapeVal = false, childProvidesSlipJumpVal = false, childProhibitsWallGrabbingVal = false;
                                    int childOnlyAllowsAlignedVelXVal = 0, childOnlyAllowsAlignedVelYVal = 0;
                                    foreach (var collisionObjProp in collisionObj.m_CustomProperties) {
                                        if ("providesHardPushback".Equals(collisionObjProp.m_Name)) {
                                            childProvidesHardPushbackVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("providesDamage".Equals(collisionObjProp.m_Name)) {
                                            childProvidesDamageVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("providesEscape".Equals(collisionObjProp.m_Name)) {
                                            childProvidesEscapeVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("providesSlipJump".Equals(collisionObjProp.m_Name)) {
                                            childProvidesSlipJumpVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("collisionTypeMask".Equals(collisionObjProp.m_Name) && !collisionObjProp.IsEmpty) {
                                            collisionTypeMaskVal = (ulong)collisionObjProp.GetValueAsInt();
                                        }
                                        if ("prohibitsWallGrabbing".Equals(collisionObjProp.m_Name)) {
                                            childProhibitsWallGrabbingVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("onlyAllowsAlignedVelX".Equals(collisionObjProp.m_Name)) {
                                            childOnlyAllowsAlignedVelXVal = (collisionObjProp.IsEmpty ? 0 : collisionObjProp.GetValueAsInt());
                                        }
                                        if ("onlyAllowsAlignedVelY".Equals(collisionObjProp.m_Name)) {
                                            childOnlyAllowsAlignedVelYVal = (collisionObjProp.IsEmpty ? 0 : collisionObjProp.GetValueAsInt());
                                        }
                                    }

                                    // [WARNING] The offset (0, 0) of the tileObj within TSX is the top-left corner, but SuperTiled2Unity converted that to bottom-left corner and reverted y-axis by itself...
                                    var (hitboxOffsetCx, hitboxOffsetCy) = (-tileObj.m_Width * 0.5f + collisionObj.m_Position.x + collisionObj.m_Size.x * 0.5f, collisionObj.m_Position.y - collisionObj.m_Size.y * 0.5f - tileObj.m_Height * 0.5f);
                                    var (hitboxOffsetVx, hitboxOffsetVy) = PolygonColliderCtrToVirtualGridPos(hitboxOffsetCx, hitboxOffsetCy);
                                    var (hitboxSizeVx, hitboxSizeVy) = PolygonColliderCtrToVirtualGridPos(collisionObj.m_Size.x, collisionObj.m_Size.y);
                                    TrapColliderAttr colliderAttr = new TrapColliderAttr {
                                        ProvidesDamage = childProvidesDamageVal,
                                        ProvidesHardPushback = childProvidesHardPushbackVal,
                                        ProvidesEscape = childProvidesEscapeVal,
                                        ProvidesSlipJump = childProvidesSlipJumpVal,
                                        ProhibitsWallGrabbing = childProhibitsWallGrabbingVal,
                                        OnlyAllowsAlignedVelX = childOnlyAllowsAlignedVelXVal,
                                        OnlyAllowsAlignedVelY = childOnlyAllowsAlignedVelYVal,
                                        HitboxOffsetX = hitboxOffsetVx,
                                        HitboxOffsetY = hitboxOffsetVy,
                                        HitboxSizeX = hitboxSizeVx,
                                        HitboxSizeY = hitboxSizeVy,
                                        SpeciesId = speciesIdVal,
                                        CollisionTypeMask = collisionTypeMaskVal,
                                        TrapLocalId = trapLocalId
                                    };
                                    colliderAttrs.List.Add(colliderAttr);
                                }
                            }
                            serializedTrapLocalIdToColliderAttrs.Dict[trapLocalId] = colliderAttrs;
                            trapList.Add(trap);
                            trapLocalId++;
                            Destroy(trapChild.gameObject); // [WARNING] It'll be animated by "TrapPrefab" in "applyRoomDownsyncFrame" instead!
                        }
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                 case "TriggerPos":
                    foreach (Transform triggerChild in child) {
                        var tileObj = triggerChild.GetComponent<SuperObject>();
                        var tileProps = triggerChild.GetComponent<SuperCustomProperties>();
                        CustomProperty id, speciesId;
                        tileProps.TryGetCustomProperty("id", out id);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        int editorId = id.GetValueAsInt();
                        if (serializedTriggerEditorIdToLocalId.Dict.ContainsKey(editorId)) {
                            throw new ArgumentException("You've assigned duplicated editorId=" + editorId + " for more than one trigger!");
                        }
                        int speciesIdVal = speciesId.GetValueAsInt(); // must have 
                        if ((StoryPointMv.SpeciesId == speciesIdVal || StoryPointTrivial.SpeciesId == speciesIdVal) && FinishedLvOption.StoryAndBoss != finishedLvOption) {
                            continue;
                        }
                        serializedTriggerEditorIdToLocalId.Dict.Add(editorId, triggerLocalId); 
                        triggerLocalId++;
                    }

                    foreach (Transform triggerChild in child) {
                        var tileObj = triggerChild.GetComponent<SuperObject>();
                        var tileProps = triggerChild.GetComponent<SuperCustomProperties>();
                        CustomProperty id, bulletTeamId, delayedFrames, quota, recoveryFrames, speciesId, subCycleTriggerFrames, subCycleQuota, characterSpawnerTimeSeq, pickableSpawnerTimeSeq, subscribesToIdList, subscribesToExhaustedIdList, newRevivalX, newRevivalY, storyPointId, bgmId, demandedEvtMaskProp, publishingEvtMaskUponExhausted, initDirX, initDirY, isBossSavepoint, bossSpeciesIds, forceCtrlRdfCount, forceCtrlCmd;

                        tileProps.TryGetCustomProperty("id", out id);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("bulletTeamId", out bulletTeamId);
                        tileProps.TryGetCustomProperty("delayedFrames", out delayedFrames);
                        tileProps.TryGetCustomProperty("quota", out quota);
                        tileProps.TryGetCustomProperty("recoveryFrames", out recoveryFrames);
                        tileProps.TryGetCustomProperty("subCycleTriggerFrames", out subCycleTriggerFrames);
                        tileProps.TryGetCustomProperty("subCycleQuota", out subCycleQuota);
                        tileProps.TryGetCustomProperty("characterSpawnerTimeSeq", out characterSpawnerTimeSeq);
                        tileProps.TryGetCustomProperty("pickableSpawnerTimeSeq", out pickableSpawnerTimeSeq);
                        tileProps.TryGetCustomProperty("subscribesToIdList", out subscribesToIdList);
                        tileProps.TryGetCustomProperty("subscribesToExhaustedIdList", out subscribesToExhaustedIdList);
                        tileProps.TryGetCustomProperty("newRevivalX", out newRevivalX);
                        tileProps.TryGetCustomProperty("newRevivalY", out newRevivalY);
                        tileProps.TryGetCustomProperty("storyPointId", out storyPointId);
                        tileProps.TryGetCustomProperty("bgmId", out bgmId);
                        tileProps.TryGetCustomProperty("demandedEvtMask", out demandedEvtMaskProp);
                        tileProps.TryGetCustomProperty("publishingEvtMaskUponExhausted", out publishingEvtMaskUponExhausted);
                        tileProps.TryGetCustomProperty("initDirX", out initDirX);
                        tileProps.TryGetCustomProperty("initDirY", out initDirY);
                        tileProps.TryGetCustomProperty("isBossSavepoint", out isBossSavepoint);
                        tileProps.TryGetCustomProperty("bossSpeciesIds", out bossSpeciesIds);
                        tileProps.TryGetCustomProperty("forceCtrlRdfCount", out forceCtrlRdfCount);
                        tileProps.TryGetCustomProperty("forceCtrlCmd", out forceCtrlCmd);

                        int speciesIdVal = speciesId.GetValueAsInt(); // must have 
                        if ((StoryPointMv.SpeciesId == speciesIdVal || StoryPointTrivial.SpeciesId == speciesIdVal) && FinishedLvOption.StoryAndBoss != finishedLvOption) {
                            continue;
                        }

                        int editorId = id.GetValueAsInt();
                        int targetTriggerLocalId = serializedTriggerEditorIdToLocalId.Dict[editorId];
                        bool xFlipped = isXFlipped(tileObj.m_TileId);
                        int dirXVal = xFlipped ? -2 : +2;
                        int initDirXVal = (null != initDirX && !initDirX.IsEmpty ? initDirX.GetValueAsInt() : 0);
                        int initDirYVal = (null != initDirY && !initDirY.IsEmpty ? initDirY.GetValueAsInt() : 0);

                        int bulletTeamIdVal = (null != bulletTeamId && !bulletTeamId.IsEmpty ? bulletTeamId.GetValueAsInt() : 0);
                        int delayedFramesVal = (null != delayedFrames && !delayedFrames.IsEmpty ? delayedFrames.GetValueAsInt() : 0);
                        int quotaVal = (null != quota && !quota.IsEmpty ? quota.GetValueAsInt() : 1);
                        int recoveryFramesVal = (null != recoveryFrames && !recoveryFrames.IsEmpty ? recoveryFrames.GetValueAsInt() : delayedFramesVal + 1); // By default we must have "recoveryFramesVal > delayedFramesVal"
                        var subscribesToIdListStr = (null != subscribesToIdList && !subscribesToIdList.IsEmpty ? subscribesToIdList.GetValueAsString() : "");
                        var subscribesToExhaustIdListStr = (null != subscribesToExhaustedIdList && !subscribesToExhaustedIdList.IsEmpty ? subscribesToExhaustedIdList.GetValueAsString() : "");
                        int subCycleTriggerFramesVal = (null != subCycleTriggerFrames && !subCycleTriggerFrames.IsEmpty ? subCycleTriggerFrames.GetValueAsInt() : 0);
                        int subCycleQuotaVal = (null != subCycleQuota && !subCycleQuota.IsEmpty ? subCycleQuota.GetValueAsInt() : 0);
                        var characterSpawnerTimeSeqStr = (null != characterSpawnerTimeSeq && !characterSpawnerTimeSeq.IsEmpty ? characterSpawnerTimeSeq.GetValueAsString() : "");
                        var pickableSpawnerTimeSeqStr = (null != pickableSpawnerTimeSeq && !pickableSpawnerTimeSeq.IsEmpty ? pickableSpawnerTimeSeq.GetValueAsString() : "");
                        bool isBossSavepointVal = (null != isBossSavepoint && !isBossSavepoint.IsEmpty && 1 == isBossSavepoint.GetValueAsInt()) ? true : false;
                        var bossSpeciesIdsStr = (null != bossSpeciesIds && !bossSpeciesIds.IsEmpty ? bossSpeciesIds.GetValueAsString() : "");
                        var triggerConfig = triggerConfigs[speciesIdVal];

                        ulong demandedEvtMaskConfigVal = (null != demandedEvtMaskProp && !demandedEvtMaskProp.IsEmpty ? (ulong)demandedEvtMaskProp.GetValueAsInt() : EVTSUB_NO_DEMAND_MASK);
                        ulong demandedEvtMask = EVTSUB_NO_DEMAND_MASK;
                        string[] subscribesToIdListStrParts = subscribesToIdListStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        string[] subscribesToExhaustIdListStrParts = subscribesToExhaustIdListStr.Split(',', StringSplitOptions.RemoveEmptyEntries);

                        if (EVTSUB_NO_DEMAND_MASK != demandedEvtMaskConfigVal) {
                            demandedEvtMask = demandedEvtMaskConfigVal;
                        } else {
                            if (0 < subscribesToIdListStrParts.Length) {
                                demandedEvtMask |= (1UL << subscribesToIdListStrParts.Length) - 1;
                            }
                            if (0 < subscribesToExhaustIdListStrParts.Length) {
                                demandedEvtMask |= (1UL << subscribesToExhaustIdListStrParts.Length) - 1;
                            }
                            if (EVTSUB_NO_DEMAND_MASK == demandedEvtMask && TRIGGER_SPECIES_TIMED_WAVE_DOOR_1 != triggerConfig.SpeciesId && TRIGGER_SPECIES_TIMED_WAVE_PICKABLE_DROPPER != triggerConfig.SpeciesId) {
                                demandedEvtMask = 1UL;
                            }
                        }

                        int newRevivalXVal = 0, newRevivalYVal = 0;
                        if (null != newRevivalX && !newRevivalX.IsEmpty && null != newRevivalY && !newRevivalY.IsEmpty) {   
                            float newRevivalXTiled = newRevivalX.GetValueAsFloat(); 
                            float newRevivalYTiled = newRevivalY.GetValueAsFloat();
                            var (newRevivalCx, newRevivalCy) = TiledLayerPositionToCollisionSpacePosition(newRevivalXTiled, newRevivalYTiled, spaceOffsetX, spaceOffsetY);
                            (newRevivalXVal, newRevivalYVal) = PolygonColliderCtrToVirtualGridPos(newRevivalCx, newRevivalCy);
                        }

                        ulong publishingEvtMaskUponExhaustedVal = (null != publishingEvtMaskUponExhausted && !publishingEvtMaskUponExhausted.IsEmpty ? (ulong)publishingEvtMaskUponExhausted.GetValueAsInt() : EVTSUB_NO_DEMAND_MASK);

                        int storyPointIdVal =  (null != storyPointId && !storyPointId.IsEmpty ? storyPointId.GetValueAsInt() : STORY_POINT_NONE);
                        int bgmIdVal = (null != bgmId && !bgmId.IsEmpty ? bgmId.GetValueAsInt() : BGM_NO_CHANGE);
                        var trigger = new Trigger {
                            EditorId = editorId,
                            TriggerLocalId = targetTriggerLocalId,
                            BulletTeamId = bulletTeamIdVal,
                            State = TriggerState.Tready,
                            SubCycleQuotaLeft = subCycleQuotaVal,
                            FramesToFire = MAX_INT,
                            FramesToRecover = (TriggerType.TtCyclicTimed == triggerConfig.TriggerType ? delayedFramesVal : 0),
                            DirX = dirXVal,
                            DemandedEvtMask = demandedEvtMask,
                        };

                        var forceCtrlRdfCountVal = (null != forceCtrlRdfCount && !forceCtrlRdfCount.IsEmpty ? forceCtrlRdfCount.GetValueAsInt() : 0);
                        var forceCtrlCmdVal = (null != forceCtrlCmd && !forceCtrlCmd.IsEmpty ? (ulong)forceCtrlCmd.GetValueAsInt() : 0u);
                        var configFromTiled = new TriggerConfigFromTiled {
                            EditorId = editorId,
                            SpeciesId = speciesIdVal,
                            BulletTeamId = bulletTeamIdVal,
                            DelayedFrames = delayedFramesVal,
                            RecoveryFrames = recoveryFramesVal,
                            SubCycleTriggerFrames = subCycleTriggerFramesVal,
                            SubCycleQuota = subCycleQuotaVal,
                            QuotaCap = quotaVal,
                            NewRevivalX = newRevivalXVal, 
                            NewRevivalY = newRevivalYVal, 
                            StoryPointId = storyPointIdVal,
                            PublishingEvtMaskUponExhausted = publishingEvtMaskUponExhaustedVal,
                            BgmId = bgmIdVal,
                            InitDirX = initDirXVal,
                            InitDirY = initDirYVal,
                            IsBossSavepoint = isBossSavepointVal,
                            ForceCtrlRdfCount = forceCtrlRdfCountVal,
                            ForceCtrlCmd = forceCtrlCmdVal,
                        };
                        serializedTriggerEditorIdToLocalId.Dict2[editorId] = configFromTiled;

                        if (IndiWaveGroupTriggerTrivial.SpeciesId == triggerConfig.SpeciesId || IndiWaveGroupTriggerMv.SpeciesId == triggerConfig.SpeciesId) {
                            trigger.Quota = 1;
                        } else {
                            trigger.Quota = quotaVal;
                        }

                        foreach (var subscribesToIdListStrPart in subscribesToIdListStrParts) {
                            if (String.IsNullOrEmpty(subscribesToIdListStrPart)) continue;
                            var subscribesToEditorId = subscribesToIdListStrPart.ToInt();
                            configFromTiled.SubscribesToIdList.Add(subscribesToEditorId);
                        }

                        foreach (var subscribesToExhaustIdListStrPart in subscribesToExhaustIdListStrParts) {
                            if (String.IsNullOrEmpty(subscribesToExhaustIdListStrPart)) continue;
                            var subscribesToExhaustEditorId = subscribesToExhaustIdListStrPart.ToInt();
                            configFromTiled.SubscribesToExhaustedIdList.Add(subscribesToExhaustEditorId);
                        }
                        string[] characterSpawnerTimeSeqStrParts = characterSpawnerTimeSeqStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in characterSpawnerTimeSeqStrParts) {
                            if (String.IsNullOrEmpty(part)) continue;
                            string[] subParts = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                            if (2 != subParts.Length) continue;
                            if (String.IsNullOrEmpty(subParts[0])) continue;
                            if (String.IsNullOrEmpty(subParts[1])) continue;
                            int cutoffRdfFrameId = subParts[0].ToInt();
                            var chSpawnerConfig = new CharacterSpawnerConfig {
                                CutoffRdfFrameId = cutoffRdfFrameId
                            };
                            string[] speciesIdAndOpParts = subParts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var speciesIdAndOpPart in speciesIdAndOpParts) {
                                string[] speciesIdAndOpSplitted = speciesIdAndOpPart.Split('|', StringSplitOptions.RemoveEmptyEntries);
                                chSpawnerConfig.SpeciesIdList.Add((uint)speciesIdAndOpSplitted[0].ToInt());
                                if (2 <= speciesIdAndOpSplitted.Length) {
                                    chSpawnerConfig.InitOpList.Add(Convert.ToUInt64(speciesIdAndOpSplitted[1]));
                                } else {
                                    if (0 == initDirXVal && 0 == initDirYVal) {
                                        var (_, _, defafultInitOp) = Battle.DiscretizeDirection(dirXVal, 0); 
                                        chSpawnerConfig.InitOpList.Add(Convert.ToUInt64(defafultInitOp));
                                    } else {
                                        var (_, _, defafultInitOp) = Battle.DiscretizeDirection(initDirXVal, initDirYVal); 
                                        chSpawnerConfig.InitOpList.Add(Convert.ToUInt64(defafultInitOp));
                                    }
                                }
                            }
                            configFromTiled.CharacterSpawnerTimeSeq.Add(chSpawnerConfig);
                        }

                        string[] pickableSpawnerTimeSeqStrParts = pickableSpawnerTimeSeqStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in pickableSpawnerTimeSeqStrParts) {
                            if (String.IsNullOrEmpty(part)) continue;
                            string[] subParts = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
                            if (2 != subParts.Length) continue;
                            if (String.IsNullOrEmpty(subParts[0])) continue;
                            if (String.IsNullOrEmpty(subParts[1])) continue;
                            int cutoffRdfFrameId = subParts[0].ToInt();
                            var pickableSpawnerConfig = new PickableSpawnerConfig {
                                CutoffRdfFrameId = cutoffRdfFrameId
                            };
                            string[] speciesIdAndTypeAndOpParts = subParts[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var speciesIdAndTypeAndOpPart in speciesIdAndTypeAndOpParts) {
                                string[] speciesIdAndTypeAndOpSplitted = speciesIdAndTypeAndOpPart.Split('|', StringSplitOptions.RemoveEmptyEntries);
                                pickableSpawnerConfig.SpeciesIdList.Add((uint)speciesIdAndTypeAndOpSplitted[0].ToInt());
                                pickableSpawnerConfig.InitOpList.Add(Convert.ToUInt64(speciesIdAndTypeAndOpSplitted[1]));
                                PickupType pickupTypeVal = ("PutIntoInventory" ==  speciesIdAndTypeAndOpSplitted[2] ? PickupType.PutIntoInventory : PickupType.Immediate);
                                pickableSpawnerConfig.PickupTypeList.Add(pickupTypeVal);
                            }
                            configFromTiled.PickableSpawnerTimeSeq.Add(pickableSpawnerConfig);
                            // Debug.Log("Added pickableSpawnerConfig=" + pickableSpawnerConfig);
                        }

                        var bossSpeciesIdsStrParts = bossSpeciesIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in bossSpeciesIdsStrParts) {
                            if (String.IsNullOrEmpty(part)) continue;
                            configFromTiled.BossSpeciesSet[Convert.ToUInt32(part)] = true;
                        }

                        bool isBottomAnchor = (null != tileObj.m_SuperTile && (null != tileObj.m_SuperTile.m_Sprite || null != tileObj.m_SuperTile.m_AnimationSprites));
                        var (tiledRectCx, tiledRectCy) = isBottomAnchor ? (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f) : (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f);

                        var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                        trigger.VirtualGridX = rectCenterVx;
                        trigger.VirtualGridY = rectCenterVy;
                        var (wx, wy) = CollisionSpacePositionToWorldPosition(rectCx, rectCy, spaceOffsetX, spaceOffsetY);
                        triggerList.Add((trigger, wx, wy));

                        if (COLLISION_NONE_INDEX  == triggerConfig.CollisionTypeMask) {
                            continue;
                        }
                        var triggerColliderAttr = new TriggerColliderAttr {
                            TriggerLocalId = targetTriggerLocalId,
                            SpeciesId = speciesIdVal
                        };
                        var srcPolygon = NewRectPolygon(rectCx, rectCy, tileObj.m_Width, tileObj.m_Height, 0, 0, 0, 0);
                        serializedStaticTriggers.Add(new SerializedCompletelyStaticTriggerCollider {
                            Polygon = srcPolygon.Serialize(),
                            Attr = triggerColliderAttr,
                        });
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "Pickable":
                    foreach (Transform pickableChild in child) {
                        var tileObj = pickableChild.GetComponent<SuperObject>();
                        var tileProps = pickableChild.GetComponent<SuperCustomProperties>();
                        CustomProperty consumableSpeciesId, skillId, pickupType, stockQuota, takesGravity;
                        tileProps.TryGetCustomProperty("consumableSpeciesId", out consumableSpeciesId);
                        tileProps.TryGetCustomProperty("stockQuota", out stockQuota);
                        tileProps.TryGetCustomProperty("skillId", out skillId);
                        tileProps.TryGetCustomProperty("pickupType", out pickupType);
                        tileProps.TryGetCustomProperty("takesGravity", out takesGravity);

                        uint consumableSpeciesIdVal = (null == consumableSpeciesId || consumableSpeciesId.IsEmpty) ? TERMINATING_CONSUMABLE_SPECIES_ID : (uint)consumableSpeciesId.GetValueAsInt();
                        uint skillIdVal = (null == skillId || skillId.IsEmpty) ? NO_SKILL : (uint)skillId.GetValueAsInt();
                        uint stockQuotaVal = (null == stockQuota || stockQuota.IsEmpty) ? 0 : (uint)stockQuota.GetValueAsInt();

                        PickupType pickupTypeVal = (
                            null != pickupType && !pickupType.IsEmpty 
                            ?  
                            ("PutIntoInventory" == pickupType.GetValueAsString() ? PickupType.PutIntoInventory : PickupType.Immediate)  
                            : 
                            PickupType.Immediate
                        );
                        bool takesGravityVal = (
                            null != takesGravity && !takesGravity.IsEmpty 
                            ?  
                            (1 == takesGravity.GetValueAsInt())  
                            : 
                            false
                        );

                        var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);

                        var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                        var pickable = new Pickable {
                            PickableLocalId = pickableLocalId,
                            VirtualGridX = rectCenterVx,
                            VirtualGridY = rectCenterVy, 
                            VelY = 0,
                            RemainingLifetimeRdfCount = MAX_INT, // TODO: Read from the map
                            ConfigFromTiled = new PickableConfigFromTiled {
                                TakesGravity = takesGravityVal,
                                InitVirtualGridX = rectCenterVx,
                                InitVirtualGridY = rectCenterVy, 
                                BuffSpeciesId = TERMINATING_BUFF_SPECIES_ID,
                                ConsumableSpeciesId = consumableSpeciesIdVal,
                                StockQuotaPerOccurrence = stockQuotaVal,
                                SkillId = skillIdVal,
                                PickupType = pickupTypeVal,
                            },
                        };
                        var (wx, wy) = CollisionSpacePositionToWorldPosition(rectCx, rectCy, spaceOffsetX, spaceOffsetY);
                        pickableList.Add((pickable, wx, wy));

                        ++pickableLocalId;
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                default:
                    break;
            }
        }

        // Sorting to make sure that if "roomCapacity" is smaller than the position counts in Tiled, we take only the smaller teamIds
        playerStartingCposList.Sort(delegate ((Vector, int, int) lhs, (Vector, int, int) rhs) {
            return Math.Sign(lhs.Item2 - rhs.Item2);
        });

        var startRdf = NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocPickableCapacity);
        historyRdfHolder = NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocPickableCapacity);

        startRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
        startRdf.ShouldForceResync = false;
        for (int i = 0; i < roomCapacity; i++) {
            int joinIndex = i + 1;
            var (cpos, teamId, dirX) = playerStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            teamId = (DEFAULT_BULLET_TEAM_ID == teamId ? joinIndex : teamId);
            var playerInRdf = startRdf.PlayersArr[i];
            playerInRdf.JoinIndex = joinIndex;
            playerInRdf.BulletTeamId = teamId;
            playerInRdf.ChCollisionTeamId = teamId; // If we want to stand on certain teammates' shoulder, then this value should be tuned accordingly. 

            var (playerVposX, playerVposY) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y); // World and CollisionSpace coordinates have the same scale, just translated
            playerInRdf.VirtualGridX = playerVposX;
            playerInRdf.VirtualGridY = playerVposY;
            playerInRdf.RevivalVirtualGridX = playerVposX;
            playerInRdf.RevivalVirtualGridY = playerVposY;
            playerInRdf.RevivalDirX = dirX;
            playerInRdf.RevivalDirY = 0;
            playerInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
            playerInRdf.FramesToRecover = 0;
            playerInRdf.DirX = dirX;
            playerInRdf.DirY = 0;
            playerInRdf.VelX = 0;
            playerInRdf.VelY = 0;
            playerInRdf.InAir = true;
            playerInRdf.OnWall = false;
            playerInRdf.SubscribesToTriggerLocalId = TERMINATING_TRIGGER_ID;

            if (SPECIES_NONE_CH == speciesIdList[i]) continue;

            // Species specific
            playerInRdf.SpeciesId = speciesIdList[i];
            var chConfig = Battle.characters[playerInRdf.SpeciesId];
            playerInRdf.Hp = chConfig.Hp;
            playerInRdf.Mp = chConfig.Mp; 
            playerInRdf.Speed = chConfig.Speed;
            playerInRdf.OmitGravity = chConfig.OmitGravity;
            playerInRdf.OmitSoftPushback = chConfig.OmitSoftPushback;
            playerInRdf.RepelSoftPushback = chConfig.RepelSoftPushback;
            if (null != chConfig.InitInventorySlots) {
                for (int t = 0; t < chConfig.InitInventorySlots.Count; t++) {
                    var initIvSlot = chConfig.InitInventorySlots[t];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, initIvSlot.SkillIdAir, initIvSlot.GaugeCharged, initIvSlot.GaugeRequired, initIvSlot.FullChargeSkillId, initIvSlot.FullChargeBuffSpeciesId, playerInRdf.Inventory.Slots[t]);
                }
            }
            
            if (!isOnlineMode) {
                spawnPlayerNode(playerInRdf.JoinIndex, playerInRdf.SpeciesId, wx, wy, playerInRdf.BulletTeamId);
            }
        }

        int npcLocalId = 1;
        for (int i = 0; i < npcsStartingCposList.Count; i++) {
            int joinIndex = roomCapacity + i + 1;
            var (cpos, dirX, dirY, characterSpeciesId, teamId, initGoal, publishingEvtSubIdUponKilledVal, publishingEvtMaskUponKilledVal, subscriptionId, killedToDropConsumableSpeciesId, killedToDropBuffSpeciesId, killedToDropPickupSkillId) = npcsStartingCposList[i];
            if (TERMINATING_EVTSUB_ID_INT != publishingEvtSubIdUponKilledVal && !serializedTriggerEditorIdToLocalId.Dict.ContainsKey(publishingEvtSubIdUponKilledVal)) {
                throw new ArgumentException(String.Format("Preset NPC with speciesId={0}, teamId={1} is set to publish to an non-existent trigger editor id={2}", characterSpeciesId, teamId, publishingEvtSubIdUponKilledVal));
            }
            if (MAGIC_EVTSUB_ID_DUMMY != subscriptionId && TERMINATING_EVTSUB_ID_INT != subscriptionId && !serializedTriggerEditorIdToLocalId.Dict.ContainsKey(subscriptionId)) {
                throw new ArgumentException(String.Format("Preset NPC with speciesId={0}, teamId={1} is set to subscribe to an non-existent trigger editor id={2}", characterSpeciesId, teamId, subscriptionId));
            }
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            var chConfig = Battle.characters[characterSpeciesId];
            var npcInRdf = startRdf.NpcsArr[i];
            var (vx, vy) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y);
            npcInRdf.Id = npcLocalId; // Just for not being excluded 
            npcInRdf.JoinIndex = joinIndex;
            npcInRdf.VirtualGridX = vx;
            npcInRdf.VirtualGridY = vy;
            npcInRdf.RevivalVirtualGridX = vx;
            npcInRdf.RevivalVirtualGridY = vy;
            npcInRdf.RevivalDirX = dirX;
            npcInRdf.RevivalDirY = dirY;
            npcInRdf.Speed = chConfig.Speed;
            npcInRdf.CharacterState = (chConfig.AntiGravityWhenIdle && 0 != dirX) ? CharacterState.Walking : CharacterState.InAirIdle1NoJump;
            if (TERMINATING_EVTSUB_ID_INT != subscriptionId && chConfig.HasDimmedAnim) {
                npcInRdf.CharacterState = CharacterState.Dimmed;
            }
            npcInRdf.FramesToRecover = 0;
            npcInRdf.DirX = dirX;
            npcInRdf.DirY = dirY;
            npcInRdf.VelX = 0;
            npcInRdf.VelY = 0;
            npcInRdf.InAir = true;
            npcInRdf.OnWall = false;
            npcInRdf.Hp = chConfig.Hp;
            npcInRdf.Mp = chConfig.Mp;
            npcInRdf.SpeciesId = characterSpeciesId;
            npcInRdf.BulletTeamId = teamId;
            npcInRdf.ChCollisionTeamId = teamId;
            npcInRdf.GoalAsNpc = initGoal;
            npcInRdf.OmitGravity = chConfig.OmitGravity;
            npcInRdf.OmitSoftPushback = chConfig.OmitSoftPushback;
            npcInRdf.RepelSoftPushback = chConfig.RepelSoftPushback;
            npcInRdf.PublishingToTriggerLocalIdUponKilled = TERMINATING_EVTSUB_ID_INT == publishingEvtSubIdUponKilledVal ? TERMINATING_TRIGGER_ID : serializedTriggerEditorIdToLocalId.Dict[publishingEvtSubIdUponKilledVal];
            npcInRdf.PublishingEvtMaskUponKilled = publishingEvtMaskUponKilledVal;
            if (MAGIC_EVTSUB_ID_DUMMY == subscriptionId) {
                npcInRdf.SubscribesToTriggerLocalId = subscriptionId;
            } else {
                npcInRdf.SubscribesToTriggerLocalId = (TERMINATING_EVTSUB_ID_INT == subscriptionId ? TERMINATING_TRIGGER_ID : serializedTriggerEditorIdToLocalId.Dict[subscriptionId]);
            }
            npcInRdf.KilledToDropConsumableSpeciesId = killedToDropConsumableSpeciesId;
            npcInRdf.KilledToDropBuffSpeciesId = killedToDropBuffSpeciesId;
            npcInRdf.KilledToDropPickupSkillId = killedToDropPickupSkillId;
            if (null != chConfig.InitInventorySlots) {
                for (int t = 0; t < chConfig.InitInventorySlots.Count; t++) {
                    var initIvSlot = chConfig.InitInventorySlots[t];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, initIvSlot.SkillIdAir, initIvSlot.GaugeCharged, initIvSlot.GaugeRequired, initIvSlot.FullChargeSkillId, initIvSlot.FullChargeBuffSpeciesId, npcInRdf.Inventory.Slots[t]);
                }
            }

            startRdf.NpcsArr[i] = npcInRdf;
            
            npcLocalId++;
        }
        startRdf.NpcLocalIdCounter = npcLocalId;

        for (int i = 0; i < trapList.Count; i++) {
            var trap = trapList[i];
            var trapConfig = trapConfigs[trap.ConfigFromTiled.SpeciesId];
            if (TERMINATING_EVTSUB_ID_INT != trap.ConfigFromTiled.SubscribesToId) {
                if (!serializedTriggerEditorIdToLocalId.Dict.ContainsKey(trap.ConfigFromTiled.SubscribesToId)) {
                    throw new ArgumentException(String.Format("trap speciesName={0} is set to subscribe to an non-existent trigger editor id={1}", trapConfig.SpeciesName, trap.ConfigFromTiled.SubscribesToId));
                }
                trap.SubscribesToTriggerLocalId = serializedTriggerEditorIdToLocalId.Dict[trap.ConfigFromTiled.SubscribesToId];
            } else {
                trap.SubscribesToTriggerLocalId = TERMINATING_TRIGGER_ID;
            }

            if (TERMINATING_EVTSUB_ID_INT != trap.ConfigFromTiled.SubscribesToIdAlt) {
                if (!serializedTriggerEditorIdToLocalId.Dict.ContainsKey(trap.ConfigFromTiled.SubscribesToIdAlt)) {
                    throw new ArgumentException(String.Format("trap speciesName={0} is set to subscribe to an non-existent trigger editor id={1} as an alternative", trapConfig.SpeciesName, trap.ConfigFromTiled.SubscribesToIdAlt));
                }
                trap.SubscribesToTriggerLocalIdAlt = serializedTriggerEditorIdToLocalId.Dict[trap.ConfigFromTiled.SubscribesToIdAlt];
            } else {
                trap.SubscribesToTriggerLocalIdAlt = TERMINATING_TRIGGER_ID;
            }
            
            if (0 != trapConfig.AngularFrameVelCos || 0 != trapConfig.AngularFrameVelSin) {
                if (!trap.ConfigFromTiled.InitNoAngularVel) {
                    trap.AngularFrameVelCos = trapConfig.AngularFrameVelCos;
                    trap.AngularFrameVelSin = trapConfig.AngularFrameVelSin;
                }
            }
            startRdf.TrapsArr[i] = trap;
            var (cx, cy) = VirtualGridToPolygonColliderCtr(trap.VirtualGridX, trap.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);
            spawnDynamicTrapNode(trapConfig.SpeciesId, wx, wy);
        }

        for (int i = 0; i < triggerList.Count; i++) {
            var (trigger, wx, wy) = triggerList[i];
            startRdf.TriggersArr[i] = trigger;
            // [WARNING] "trigger.SubscriberLocalIdsMask" is initialized from "trigger.ConfigFromTiled.SubscribesToIdList", but doesn't necessarily stay the initial value during battle! 

            var triggerConfigFromTiled = serializedTriggerEditorIdToLocalId.Dict2[trigger.EditorId]; 
            var subscribesToIdList = triggerConfigFromTiled.SubscribesToIdList;
            foreach (var subscribesToEditorId in subscribesToIdList) {
                var subscribesToTriggerLocalId = serializedTriggerEditorIdToLocalId.Dict[subscribesToEditorId]; 
                var (subscribesToTrigger, _, _) = triggerList[subscribesToTriggerLocalId-1];
                subscribesToTrigger.SubscriberLocalIdsMask |= (1UL << (trigger.TriggerLocalId-1));
            }

            var subscribesToExhaustIdList = triggerConfigFromTiled.SubscribesToExhaustedIdList;
            foreach (var subscribesToExhaustEditorId in subscribesToExhaustIdList) {
                var subscribesToTriggerLocalId = serializedTriggerEditorIdToLocalId.Dict[subscribesToExhaustEditorId]; 
                var (subscribesToExhaustTrigger, _, _) = triggerList[subscribesToTriggerLocalId-1];
                subscribesToExhaustTrigger.ExhaustSubscriberLocalIdsMask |= (1UL << (trigger.TriggerLocalId - 1));
            }
            spawnTriggerNode(trigger.TriggerLocalId, triggerConfigFromTiled.SpeciesId, wx, wy);
        }
        // A final check on conflicting "SubscriberLocalIdsMask v.s. ExhaustSubscriberLocalIdsMask"
        for (int i = 0; i < triggerList.Count; i++) {
            var (trigger, _, _) = triggerList[i];
            if (0 < (trigger.SubscriberLocalIdsMask & trigger.ExhaustSubscriberLocalIdsMask)) {
                throw new ArgumentException("At least one other trigger is subscribing simultaneously to both regular-cycle and exhaust of trigger editor id = " + trigger.EditorId + ", please double check your map config!");
            }
        }

        for (int i = 0; i < pickableList.Count; i++) {
            var (pickable, wx, wy) = pickableList[i];
            startRdf.Pickables[i] = pickable;
        }
        startRdf.PickableLocalIdCounter = pickableLocalId;
        startRdf.BulletLocalIdCounter = 1;

        CustomProperty missionEvtSubIdProp;
        mapProps.TryGetCustomProperty("missionEvtSubId", out missionEvtSubIdProp);
        int missionEvtSubId = (null == missionEvtSubIdProp || missionEvtSubIdProp.IsEmpty ? TERMINATING_EVTSUB_ID_INT : missionEvtSubIdProp.GetValueAsInt());
        if (!serializedTriggerEditorIdToLocalId.Dict.ContainsKey(missionEvtSubId)) {
            throw new ArgumentException("missionEvtSubId = " + missionEvtSubId + " not found, please double check your map config!");
        }
        missionTriggerLocalId = (TERMINATING_EVTSUB_ID_INT == missionEvtSubId ? TERMINATING_TRIGGER_ID : serializedTriggerEditorIdToLocalId.Dict[missionEvtSubId]);

        return (startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, battleDurationSecondsVal);
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
    protected void clampToMapBoundary(ref Vector3 posHolder) {
        float newX = posHolder.x, newY = posHolder.y, newZ = posHolder.z;
        if (newX > cameraCapMaxX) newX = cameraCapMaxX;
        if (newX < cameraCapMinX) newX = cameraCapMinX;
        if (newY > cameraCapMaxY) newY = cameraCapMaxY;
        if (newY < cameraCapMinY) newY = cameraCapMinY;
        posHolder.Set(newX, newY, newZ);
    }

    protected void cameraTrack(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf, bool battleResultIsSet) {
        if (null == selfPlayerInfo) return;
        int targetJoinIndex = battleResultIsSet ? confirmedBattleResult.WinnerJoinIndex : selfPlayerInfo.JoinIndex;
        int targetBulletTeamId = battleResultIsSet ? confirmedBattleResult.WinnerBulletTeamId : selfPlayerInfo.BulletTeamId;
        if (0 >= targetJoinIndex) {
            // TODO: Use bullet team id to traverse players instead!
            return;
        }
        if (targetJoinIndex > roomCapacity) {
            // TODO: locate that NPC by virtual grid coordinates
            return;
        }
        var chGameObj = playerGameObjs[targetJoinIndex - 1];
        var chd = rdf.PlayersArr[targetJoinIndex - 1];

        var (velCX, velCY) = VirtualGridToPolygonColliderCtr(chd.Speed, chd.Speed);
        camSpeedHolder.Set(velCX, velCY);
        var cameraSpeedInWorld = camSpeedHolder.magnitude * 100;

        var prevPlayerCharacterDownsync = (null == prevRdf || null == prevRdf.PlayersArr) ? null : prevRdf.PlayersArr[targetJoinIndex - 1];
        bool justDead = (null != prevPlayerCharacterDownsync && (CharacterState.Dying == prevPlayerCharacterDownsync.CharacterState || 0 >= prevPlayerCharacterDownsync.Hp));
        justDead |= chd.NewBirth;
        if (justDead || battleResultIsSet) {
            cameraSpeedInWorld *= 500;
        }

        var camOldPos = gameplayCamera.transform.position;
        var dst = chGameObj.transform.position;
        camDiffDstHolder.Set(dst.x - camOldPos.x, dst.y - camOldPos.y);

        float camDiffMagnitude = camDiffDstHolder.magnitude; 

        //Debug.Log(String.Format("cameraTrack, camOldPos={0}, dst={1}, deltaTime={2}", camOldPos, dst, Time.deltaTime));
        var stepLength = Time.deltaTime * cameraSpeedInWorld;
        if (DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START == rdf.Id || DOWNSYNC_MSG_ACT_BATTLE_START == rdf.Id || stepLength > camDiffMagnitude || justDead) {
            // Immediately teleport
            newPosHolder.Set(dst.x, dst.y, defaultGameplayCamZ);
        } else {
            var newMapPosDiff2 = camDiffDstHolder.normalized * stepLength;
            newPosHolder.Set(camOldPos.x + newMapPosDiff2.x, camOldPos.y + newMapPosDiff2.y, defaultGameplayCamZ);
        }
        clampToMapBoundary(ref newPosHolder);
        gameplayCamera.transform.position = newPosHolder;

        // TODO: In OfflineMap shall I move the "mainCamera" too such that the audio listener is placed correctly?
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

    public void toggleDebugDrawingEnabled() {
        debugDrawingEnabled = !debugDrawingEnabled;
    }

    protected void urpDrawDebug() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        for (int i = cachedLineRenderers.vals.StFrameId; i < cachedLineRenderers.vals.EdFrameId; i++) {
            var (res, line) = cachedLineRenderers.vals.GetByFrameId(i);
            if (!res || null == line) throw new ArgumentNullException(String.Format("There's no line for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedLineRenderers.vals.StFrameId, cachedLineRenderers.vals.EdFrameId));

            resetLine(line);
        }
        if (!debugDrawingEnabled) {
            return;
        }
        var (_, rdf) = renderBuffer.GetByFrameId(playerRdfId);
        if (null == rdf) return;

        // Draw static colliders
        int lineIndex = 0;
        for (int s = 0; s < staticCollidersCnt; s++) {
            var collider = staticColliders[s];
            if (null == collider) {
                break;
            }
            if (null == collider.Shape) {
                throw new ArgumentNullException("barrierCollider.Shape is null when drawing staticRectangleColliders");
            }
            if (null == collider.Shape.Points) {
                throw new ArgumentNullException("barrierCollider.Shape.Points is null when drawing staticRectangleColliders");
            }

            var (wx, wy) = CollisionSpacePositionToWorldPosition(collider.X, collider.Y, spaceOffsetX, spaceOffsetY); ;
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            int key = KV_PREFIX_STATIC_COLLLIDER + lineIndex;
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
                TrapColliderAttr? trapColliderAttr = collider.Data as TrapColliderAttr;
                if (null != trapColliderAttr) {
                    if (trapColliderAttr.ProvidesHardPushback) {
                        line.SetColor(Color.green);
                    } else if (trapColliderAttr.ProvidesDamage) {
                        line.SetColor(Color.red);
                    }
                } else {
                    TriggerColliderAttr? triggerColliderAttr = collider.Data as TriggerColliderAttr;
                    if (null != triggerColliderAttr) {
                        var trigger = rdf.TriggersArr[triggerColliderAttr.TriggerLocalId - 1];
                        if (null == trigger) {
                            continue;
                        }
                        var configFromTiled = triggerEditorIdToConfigFromTiled[trigger.EditorId];
                        if (null != configFromTiled && triggerConfigs.ContainsKey(configFromTiled.SpeciesId)) {
                            var triggerConfig = triggerConfigs[configFromTiled.SpeciesId];
                            if (TriggerType.TtMovement == triggerConfig.TriggerType || TriggerType.TtAttack == triggerConfig.TriggerType) {
                                line.SetColor(Color.magenta);
                            } else {
                                line.SetColor(Color.cyan);
                            }
                        }
                    }
                }
#nullable disable
            }
            int m = collider.Shape.Points.Cnt;
            line.GetPositions(debugDrawPositionsHolder);
            for (int i = 0; i < 4; i++) {
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
            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            int key = KV_PREFIX_DYNAMIC_COLLLIDER + KV_PREFIX_PLAYER + currCharacterDownsync.JoinIndex;
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.white);
            line.GetPositions(debugDrawPositionsHolder);

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
            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);

            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            int key = KV_PREFIX_DYNAMIC_COLLLIDER + KV_PREFIX_NPC + currCharacterDownsync.JoinIndex;
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.gray);
            line.GetPositions(debugDrawPositionsHolder);

            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);

            int keyVision = KV_PREFIX_DYNAMIC_COLLLIDER + KV_PREFIX_DYNAMIC_COLLLIDER_VISION + KV_PREFIX_NPC + currCharacterDownsync.JoinIndex;
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
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BulletLocalId) break;
            var (skillConfig, bulletConfig) = FindBulletConfig(bullet.SkillId, bullet.ActiveSkillHit);
            if (null == skillConfig || null == bulletConfig) continue;
            if (!bulletConfig.BeamCollision) {
                var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
                var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY); ;
                newPosHolder.Set(wx, wy, 0);
                if (!isGameObjPositionWithinCamera(newPosHolder)) {
                    continue; // To save memory
                }

                int key = KV_PREFIX_DYNAMIC_COLLLIDER + KV_PREFIX_BULLET + bullet.BulletLocalId;
                var line = cachedLineRenderers.PopAny(key);
                if (null == line) {
                    line = cachedLineRenderers.Pop();
                }
                if (null == line) {
                    throw new ArgumentNullException("Cached line is null for key:" + key);
                }
                if (!IsBulletActive(bullet, bulletConfig, rdf.Id)) {
                    cachedLineRenderers.Put(key, line);
                    continue;
                }
                line.SetColor(Color.red);
                line.GetPositions(debugDrawPositionsHolder);

                var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bulletConfig.HitboxSizeX + bulletConfig.HitboxSizeIncX * (int)bullet.FramesInBlState, bulletConfig.HitboxSizeY + bulletConfig.HitboxSizeIncY * (int)bullet.FramesInBlState);
                (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
                (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

                RotatePoints(debugDrawPositionsHolder, Math.Abs(boxCw), 4, bullet.SpinCos, bullet.SpinSin, bulletConfig.SpinAnchorX, bulletConfig.SpinAnchorY, 0 > bullet.DirX);
                
                //Debug.Log("Active Bullet " + bullet.BattleAttr.BulletLocalId.ToString() + ": wx=" + wx.ToString() + ", wy=" + wy.ToString() + ", boxCw=" + boxCw.ToString() + ", boxCh=" + boxCh.ToString());
                line.SetPositions(debugDrawPositionsHolder);
                line.score = rdf.Id;
                cachedLineRenderers.Put(key, line);
            } else {
                int key = KV_PREFIX_DYNAMIC_COLLLIDER + KV_PREFIX_BULLET + bullet.BulletLocalId;
                var line = cachedLineRenderers.PopAny(key);
                if (null == line) {
                    line = cachedLineRenderers.Pop();
                }
                if (null == line) {
                    throw new ArgumentNullException("Cached line is null for key:" + key);
                }
                if (!IsBulletActive(bullet, bulletConfig, rdf.Id)) {
                    cachedLineRenderers.Put(key, line);
                    continue;
                }
                line.SetColor(Color.red);
                line.GetPositions(debugDrawPositionsHolder);
                    
                //Debug.LogFormat("Beam head vel=({0}, {1}), spin angle = {2}", bullet.VelX, bullet.VelY, Mathf.Atan2(bullet.SpinSin, bullet.SpinCos) * Mathf.Rad2Deg);

                var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX - bullet.OriginatedVirtualGridX, bulletConfig.HitboxSizeY + bulletConfig.HitboxSizeIncY * (int)bullet.FramesInBlState);
                var (cx, cy) = VirtualGridToPolygonColliderCtr(((bullet.OriginatedVirtualGridX + bullet.VirtualGridX) >> 1), bullet.VirtualGridY);
                var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);
                (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
                (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

                RotatePoints(debugDrawPositionsHolder, Math.Abs(boxCw), 4, bullet.SpinCos, bullet.SpinSin, bulletConfig.SpinAnchorX, bulletConfig.SpinAnchorY, (0 > bullet.DirX));

                // Debug.Log("Active Bullet " + bullet.BattleAttr.BulletLocalId.ToString() + ": wx=" + wx.ToString() + ", wy=" + wy.ToString() + ", boxCw=" + boxCw.ToString() + ", boxCh=" + boxCh.ToString());
                line.SetPositions(debugDrawPositionsHolder);
                line.score = rdf.Id;
                cachedLineRenderers.Put(key, line);
            }
        }

        for (int i = 0; i < rdf.TrapsArr.Count; i++) {
            var currTrap = rdf.TrapsArr[i];
            if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) continue;
            List<TrapColliderAttr> colliderAttrs = trapLocalIdToColliderAttrs[currTrap.TrapLocalId];
            foreach (var colliderAttr in colliderAttrs) {
                float boxCx, boxCy, boxCw, boxCh;
                calcTrapBoxInCollisionSpace(colliderAttr, currTrap.VirtualGridX, currTrap.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
                var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(wx, wy, 0);
                if (!isGameObjPositionWithinCamera(newPosHolder)) {
                    continue; // To save memory
                }

                int key = KV_PREFIX_DYNAMIC_COLLLIDER + KV_PREFIX_TRAP + currTrap.TrapLocalId;

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

                (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
                (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

                var (anchorOffsetCx, anchorOffsetCy) = VirtualGridToPolygonColliderCtr((colliderAttr.HitboxOffsetX << 1), (colliderAttr.HitboxOffsetY << 1));
                var trapConfig = trapConfigs[currTrap.ConfigFromTiled.SpeciesId];
                RotatePoints(debugDrawPositionsHolder, Math.Abs(boxCw), 4, currTrap.SpinCos, currTrap.SpinSin, trapConfig.SpinAnchorX - anchorOffsetCx, trapConfig.SpinAnchorY - anchorOffsetCy, 0 > currTrap.DirX);

                line.SetPositions(debugDrawPositionsHolder);
                line.score = rdf.Id;
                cachedLineRenderers.Put(key, line);
            }
        }
    }

    public bool isGameObjPositionWithinCamera(Vector3 positionHolder) {
        var posInMainCamViewport = gameplayCamera.WorldToViewportPoint(positionHolder);
        return (0f <= posInMainCamViewport.x && posInMainCamViewport.x <= 1f && 0f <= posInMainCamViewport.y && posInMainCamViewport.y <= 1f && 0f < posInMainCamViewport.z);
    }

    public void showTeamRibbon(int rdfId, CharacterDownsync currCharacterDownsync, float wx, float wy, float halfBoxCw, float halfBoxCh, int lookupKey) {
        var teamRibbon = cachedTeamRibbons.PopAny(lookupKey);
        if (null == teamRibbon) {
            teamRibbon = cachedTeamRibbons.Pop();
        }

        if (null == teamRibbon) {
            throw new ArgumentNullException(String.Format("No available teamRibbon node for lookupKey={0}", lookupKey));
        }

        newPosHolder.Set(wx + teamRibbonOffset.x, wy + halfBoxCh + teamRibbonOffset.y, inplaceHpBarZ);
        teamRibbon.gameObject.transform.position = newPosHolder;
        teamRibbon.score = rdfId;
        teamRibbon.setBulletTeamId(currCharacterDownsync.BulletTeamId);
        cachedTeamRibbons.Put(lookupKey, teamRibbon);
    }

    public void showKeyChPointer(int rdfId, CharacterDownsync currCharacterDownsync, float wx, float wy, float halfBoxCw, float halfBoxCh, int lookupKey) {
        var keyChPointer = cachedKeyChPointers.PopAny(lookupKey);
        if (null == keyChPointer) {
            keyChPointer = cachedKeyChPointers.Pop();
        }

        if (null == keyChPointer) {
            throw new ArgumentNullException(String.Format("No available teamRibbon node for lookupKey={0}", lookupKey));
        }
        newPosHolder.Set(wx, wy, inplaceHpBarZ);
        pointInCamViewPortHolder = gameplayCamera.WorldToViewportPoint(newPosHolder);
        float xInCamViewPort, yInCamViewPort;
        keyChPointer.SetProps(currCharacterDownsync, wx, wy, pointInCamViewPortHolder, out xInCamViewPort, out yInCamViewPort);

        newPosHolder.Set(xInCamViewPort, yInCamViewPort, inplaceHpBarZ);
        keyChPointer.gameObject.transform.position = gameplayCamera.ViewportToWorldPoint(newPosHolder);
        keyChPointer.score = rdfId;
        cachedKeyChPointers.Put(lookupKey, keyChPointer);
    }

    public void showInplaceHpBar(int rdfId, CharacterDownsync currCharacterDownsync, float wx, float wy, float halfBoxCw, float halfBoxCh, int lookupKey) {
        var hpBar = cachedHpBars.PopAny(lookupKey);
        if (null == hpBar) {
            hpBar = cachedHpBars.Pop();
        }

        if (null == hpBar) {
            throw new ArgumentNullException(String.Format("No available hpBar node for lookupKey={0}", lookupKey));
        }
        var chConfig = characters[currCharacterDownsync.SpeciesId];
        hpBar.score = rdfId;
        hpBar.updateHpByValsAndCaps(currCharacterDownsync.Hp, chConfig.Hp);
        newPosHolder.Set(wx + inplaceHpBarOffset.x, wy + halfBoxCh + inplaceHpBarOffset.y, inplaceHpBarZ);
        hpBar.gameObject.transform.position = newPosHolder;
        cachedHpBars.Put(lookupKey, hpBar);
    }

    public bool playCharacterDamagedVfx(int rdfId, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync prevCharacterDownsync, GameObject theGameObj, float wx, float wy, float halfBoxCh, CharacterAnimController chAnimCtrl, int lookupKey, Material material, bool isActiveBoss) {
        var spr = theGameObj.GetComponent<SpriteRenderer>();
        material.SetFloat("_CrackOpacity", 0f);

        if (!isActiveBoss && currCharacterDownsync.JoinIndex != selfPlayerInfo.JoinIndex && CharacterState.Dying != currCharacterDownsync.CharacterState && 0 < currCharacterDownsync.FramesSinceLastDamaged) {
            showInplaceHpBar(rdfId, currCharacterDownsync, wx, wy, halfBoxCh, inplaceHpBarZ, lookupKey); 
            if (null != toast && isOnlineMode && Battle.SPECIES_DARKBEAMTOWER == currCharacterDownsync.SpeciesId && currCharacterDownsync.BulletTeamId == selfPlayerInfo.BulletTeamId) {
                toast.showAdvice("Main tower under attack!"); 
            } 
        }

        if (null != currCharacterDownsync.DebuffList) {
            for (int i = 0; i < currCharacterDownsync.DebuffList.Count; i++) {
                Debuff debuff = currCharacterDownsync.DebuffList[i];
                if (TERMINATING_DEBUFF_SPECIES_ID == debuff.SpeciesId) break;
                var debuffConfig = debuffConfigs[debuff.SpeciesId];
                switch (debuffConfig.Type) {
                    case DebuffType.FrozenPositionLocked:
                        if (0 < debuff.Stock) {
                            material.SetFloat("_CrackOpacity", 0.90f);
                            CharacterState overwriteChState = currCharacterDownsync.CharacterState;
                            if (!noOpSet.Contains(overwriteChState)) {
                                overwriteChState = CharacterState.Atked1;
                            }
                            chAnimCtrl.updateCharacterAnim(currCharacterDownsync, overwriteChState, prevCharacterDownsync, false, chConfig);
                        }
                        break;
                    case DebuffType.PositionLockedOnly:
                        if (0 < debuff.Stock) {
                            int vfxSpeciesId = VfxThunderCharged.SpeciesId;
                            if (!pixelatedVfxDict.ContainsKey(vfxSpeciesId)) return false;
                          
                            var vfxConfig = pixelatedVfxDict[vfxSpeciesId];
                            var vfxAnimName = vfxConfig.Name;
                            int vfxLookupKey = KV_PREFIX_VFX_CH_ELE_DEBUFF + currCharacterDownsync.JoinIndex;
                            newPosHolder.Set(wx, wy, fireballZ);

                            if (0 < vfxLookupKey) {
                                var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
                                if (null == pixelVfxHolder) {
                                    pixelVfxHolder = cachedPixelVfxNodes.Pop();
                                }
                                if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                                    if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                                        pixelVfxHolder.updateAnim(vfxAnimName, debuff.Stock, currCharacterDownsync.DirX, false, rdfId);
                                        pixelVfxHolder.gameObject.transform.position = newPosHolder;
                                    }
                                    pixelVfxHolder.score = rdfId;
                                    cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
                                }
                            }
                        }
                        break;
                }
            }
        }

        return true;
    }

    public bool playPickableSfx(Pickable currPickable, float wx, float wy, int rdfId) {
            if (0 != currPickable.FramesInPkState || PickableState.Pconsumed != currPickable.PkState) {
                return false;
            }
            bool usingSameAudSrc = true;
            int ftSfxLookupKey = KV_PREFIX_SFX_FT + KV_PREFIX_PK + currPickable.PickableLocalId;
            var ftSfxSourceHolder = cachedSfxNodes.PopAny(ftSfxLookupKey);
            if (null == ftSfxSourceHolder) {
                ftSfxSourceHolder = cachedSfxNodes.Pop();
                usingSameAudSrc = false;
            }

            if (null == ftSfxSourceHolder) {
                return false;
                // throw new ArgumentNullException(String.Format("No available ftSfxSourceHolder node for ftSfxLookupKey={0}", ftSfxLookupKey));
            }

            try {
                var clipName = "Pickup1";
                if (!ftSfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                    return false;
                }

                newPosHolder.Set(wx, wy, footstepAttenuationZ);
                ftSfxSourceHolder.gameObject.transform.position = newPosHolder;
                if (!usingSameAudSrc || !ftSfxSourceHolder.audioSource.isPlaying) {
                    ftSfxSourceHolder.audioSource.volume = calcSfxVolume(ftSfxSourceHolder, footstepAttenuationZ);
                    ftSfxSourceHolder.audioSource.PlayOneShot(ftSfxSourceHolder.audioClipDict[clipName]);
                }
                ftSfxSourceHolder.score = rdfId;
            } finally {
                cachedSfxNodes.Put(ftSfxLookupKey, ftSfxSourceHolder);
            }
            return true;
    }

    public bool playCharacterSfx(CharacterDownsync currCharacterDownsync, CharacterDownsync prevCharacterDownsync, CharacterConfig chConfig, float wx, float wy, int rdfId, float distanceAttenuationZ) {
        // Cope with footstep sounds first

        if (currCharacterDownsync.JoinIndex <= roomCapacity) {
            if (CharacterState.Walking == currCharacterDownsync.CharacterState || CharacterState.WalkingAtk1 == currCharacterDownsync.CharacterState) {
                bool usingSameAudSrc = true;
                int ftSfxLookupKey = KV_PREFIX_SFX_FT + KV_PREFIX_PLAYER + currCharacterDownsync.JoinIndex;
                var ftSfxSourceHolder = cachedSfxNodes.PopAny(ftSfxLookupKey);
                if (null == ftSfxSourceHolder) {
                    ftSfxSourceHolder = cachedSfxNodes.Pop();
                    usingSameAudSrc = false;
                }

                if (null == ftSfxSourceHolder) {
                    return false;
                    // throw new ArgumentNullException(String.Format("No available ftSfxSourceHolder node for ftSfxLookupKey={0}", ftSfxLookupKey));
                }

                try {
                    var clipName = calcFootStepSfxName(currCharacterDownsync);
                    if (null == clipName) {
                        return false;
                    }
                    if (!ftSfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                        return false;
                    }

                    float totAttZ = distanceAttenuationZ + footstepAttenuationZ;
                    newPosHolder.Set(wx, wy, totAttZ);
                    ftSfxSourceHolder.gameObject.transform.position = newPosHolder;
                    if (!usingSameAudSrc || !ftSfxSourceHolder.audioSource.isPlaying) {
                        ftSfxSourceHolder.audioSource.volume = calcSfxVolume(ftSfxSourceHolder, totAttZ);
                        ftSfxSourceHolder.audioSource.PlayOneShot(ftSfxSourceHolder.audioClipDict[clipName]);
                    }
                    ftSfxSourceHolder.score = rdfId;
                } finally {
                    cachedSfxNodes.Put(ftSfxLookupKey, ftSfxSourceHolder);
                }
            }
            if (null != prevCharacterDownsync) {
                if (prevCharacterDownsync.InAir && !currCharacterDownsync.InAir) {
                    int k = KV_PREFIX_SFX_FT + KV_PREFIX_PLAYER + currCharacterDownsync.JoinIndex;
                    var holder = cachedSfxNodes.PopAny(k);
                    if (null == holder) {
                        holder = cachedSfxNodes.Pop();
                    }

                    if (null == holder) {
                        return false;
                        // throw new ArgumentNullException(String.Format("No available sfxSourceHolder node for sfxLookupKey={0}", k));
                    }

                    try {
                        string clipName = "Landing1";                 
                        if (!holder.audioClipDict.ContainsKey(clipName)) {
                            return false;
                        }

                        newPosHolder.Set(wx, wy, 0);
                        holder.gameObject.transform.position = newPosHolder;
                        holder.audioSource.PlayOneShot(holder.audioClipDict[clipName]);
                        holder.score = rdfId;
                    } finally {
                        cachedSfxNodes.Put(k, holder);
                    }
                } else if (isJumpStartupJustEnded(prevCharacterDownsync, currCharacterDownsync, chConfig)) {
                    int k = KV_PREFIX_SFX_FT + KV_PREFIX_PLAYER + currCharacterDownsync.JoinIndex;
                    var holder = cachedSfxNodes.PopAny(k);
                    if (null == holder) {
                        holder = cachedSfxNodes.Pop();
                    }

                    if (null == holder) {
                        return false;
                        // throw new ArgumentNullException(String.Format("No available sfxSourceHolder node for sfxLookupKey={0}", k));
                    }

                    try {
                        string clipName = "Jump1";                 
                        if (!holder.audioClipDict.ContainsKey(clipName)) {
                            return false;
                        }

                        newPosHolder.Set(wx, wy, distanceAttenuationZ);
                        holder.gameObject.transform.position = newPosHolder;
                        holder.audioSource.volume = calcSfxVolume(holder, distanceAttenuationZ);
                        holder.audioSource.PlayOneShot(holder.audioClipDict[clipName]);
                        holder.score = rdfId;
                    } finally {
                        cachedSfxNodes.Put(k, holder);
                    }
                }
            }
        }

        bool isInitialFrame = (0 == currCharacterDownsync.FramesInChState);
        if (!isInitialFrame) {
            return false;
        }

        if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return false;
        var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
        if (0 > currCharacterDownsync.ActiveSkillHit || currSkillConfig.Hits.Count <= currCharacterDownsync.ActiveSkillHit) return false;
        var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
        if (null == currBulletConfig || null == currBulletConfig.CharacterEmitSfxName || currBulletConfig.CharacterEmitSfxName.IsEmpty()) return false;

        int sfxLookupKey = KV_PREFIX_SFX_CH_EMIT + currCharacterDownsync.JoinIndex;
        var sfxSourceHolder = cachedSfxNodes.PopAny(sfxLookupKey);
        if (null == sfxSourceHolder) {
            sfxSourceHolder = cachedSfxNodes.Pop();
        }

        if (null == sfxSourceHolder) {
            return false;
            // throw new ArgumentNullException(String.Format("No available sfxSourceHolder node for sfxLookupKey={0}", sfxLookupKey));
        }

        try {
            string clipName = currBulletConfig.CharacterEmitSfxName;
            if (null == clipName) {
                return false;
            }
            if (!sfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                return false;
            }

            newPosHolder.Set(wx, wy, distanceAttenuationZ);
            sfxSourceHolder.gameObject.transform.position = newPosHolder;
            sfxSourceHolder.audioSource.volume = calcSfxVolume(sfxSourceHolder, distanceAttenuationZ);
            sfxSourceHolder.audioSource.PlayOneShot(sfxSourceHolder.audioClipDict[clipName]);
            sfxSourceHolder.score = rdfId;
        } finally {
            cachedSfxNodes.Put(sfxLookupKey, sfxSourceHolder);
        }

        return true;
    }

    protected RoomDownsyncFrame getRdfBackthen(int rdfId) {
        if (rdfId < renderBuffer.StFrameId) {
            rdfId = renderBuffer.StFrameId;
        }
        var (_, rdfBackthen) = renderBuffer.GetByFrameId(rdfId);
        return rdfBackthen;
    }

    public bool playCharacterVfx(CharacterDownsync currCharacterDownsync, CharacterDownsync prevCharacterDownsync, CharacterConfig chConfig, CharacterAnimController chAnimCtrl, float wx, float wy, int rdfId) {
        int vfxSpeciesId = NO_VFX_ID;
        int vfxLookupKeySecondPrefix = 0;
        var framesInState = currCharacterDownsync.FramesInChState;
        int dirX = currCharacterDownsync.DirX;

        chAnimCtrl.spr.transform.localRotation = Quaternion.AngleAxis(0, Vector3.forward);

        if (CharacterState.InAirIdle2ByJump == currCharacterDownsync.CharacterState && NO_VFX_ID != chConfig.AirJumpVfxSpeciesId) {
            int airJumpVfxLookupKey = KV_PREFIX_CHARACTER_SECONDARY + currCharacterDownsync.JoinIndex;
            var pixelVfxHolder = cachedPixelVfxNodes.PopAny(airJumpVfxLookupKey);

            if (null == pixelVfxHolder) {
                pixelVfxHolder = cachedPixelVfxNodes.Pop();
                //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for joinIndex={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for joinIndex={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
            }

            if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                var airJumpVfxConfig = pixelatedVfxDict[chConfig.AirJumpVfxSpeciesId];
                string airJumpVfxAnimName = airJumpVfxConfig.Name;
                if (pixelVfxHolder.lookUpTable.ContainsKey(airJumpVfxAnimName)) {
                    pixelVfxHolder.updateAnim(airJumpVfxAnimName, framesInState, dirX, false, rdfId);
                    var rdfBackthen = getRdfBackthen(rdfId - currCharacterDownsync.FramesInChState);
                    if (null != rdfBackthen) {
                        var chdBackthen = getChdFromRdf(currCharacterDownsync.JoinIndex, roomCapacity, rdfBackthen);
                        if (null != chdBackthen && chdBackthen.Id == currCharacterDownsync.Id) {
                            var (cxBackthen, cyBackthen) = VirtualGridToPolygonColliderCtr(chdBackthen.VirtualGridX, chdBackthen.VirtualGridY);
                            var (wxBackthen, wyBackthen) = CollisionSpacePositionToWorldPosition(cxBackthen, cyBackthen, spaceOffsetX, spaceOffsetY);
                            newPosHolder.Set(wxBackthen, wyBackthen, characterZ);
                            pixelVfxHolder.gameObject.transform.position = newPosHolder;
                            pixelVfxHolder.score = rdfId;
                        }
                    }
                }
                cachedPixelVfxNodes.Put(airJumpVfxLookupKey, pixelVfxHolder);
            }
        }

        if (CharacterState.Def1 == currCharacterDownsync.CharacterState || CharacterState.Def1Broken == currCharacterDownsync.CharacterState) {
            vfxLookupKeySecondPrefix = KV_PREFIX_VFX_DEF;
            if (0 < currCharacterDownsync.RemainingDef1Quota) {
                if (0 == currCharacterDownsync.FramesSinceLastDamaged) {
                    // Either maintain Def1 or is still starting the skill.
                    if (chConfig.Def1StartupFrames < currCharacterDownsync.FramesInChState) vfxSpeciesId = chConfig.Def1ActiveVfxSpeciesId;
                    else vfxSpeciesId = NO_VFX_ID;
                } else if (null != prevCharacterDownsync && CharacterState.Def1 == prevCharacterDownsync.CharacterState) {
                    vfxSpeciesId = chConfig.Def1AtkedVfxSpeciesId;
                    framesInState = DEFAULT_FRAMES_TO_SHOW_DAMAGED - currCharacterDownsync.FramesSinceLastDamaged;
                    if (chConfig.HasDef1Atked1Anim) {
                        chAnimCtrl.updateCharacterAnim(currCharacterDownsync, CharacterState.Def1Atked1, prevCharacterDownsync, false, chConfig);
                    }
                }
            } else {
                vfxSpeciesId = chConfig.Def1BrokenVfxSpeciesId;
            }
        } else if (chConfig.HasBtnBCharging && 0 < currCharacterDownsync.BtnBHoldingRdfCount) {
            vfxLookupKeySecondPrefix = KV_PREFIX_VFX_CHARGE;
            if (BTN_B_HOLDING_RDF_CNT_THRESHOLD_2 > currCharacterDownsync.BtnBHoldingRdfCount) {
                vfxSpeciesId = VfxSharedChargingPreparation.SpeciesId;
            } else {
                vfxSpeciesId = chConfig.BtnBChargedVfxSpeciesId;
            }
        }

        if (NO_VFX_ID == vfxSpeciesId) return false;
        if (!pixelatedVfxDict.ContainsKey(vfxSpeciesId)) return false;
        // For convenience, character vfx is only pixelated from now on
      
        var vfxConfig = pixelatedVfxDict[vfxSpeciesId];
        var vfxAnimName = vfxConfig.Name;
        newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ);
        
        int vfxLookupKey = vfxLookupKeySecondPrefix + currCharacterDownsync.JoinIndex;
        newPosHolder.Set(wx, wy, fireballZ);

        if (!isGameObjPositionWithinCamera(newPosHolder)) {
            return false;
        }

        if (0 < vfxLookupKey) {
            var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
            if (null == pixelVfxHolder) {
                pixelVfxHolder = cachedPixelVfxNodes.Pop();
                //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
            }

            if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                    pixelVfxHolder.updateAnim(vfxAnimName, framesInState, dirX, false, rdfId);
                    pixelVfxHolder.gameObject.transform.position = newPosHolder;
                }
                pixelVfxHolder.score = rdfId;
                cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
            }
        }

        return true;
    }

    public bool playBulletSfx(Bullet bullet, BulletConfig bulletConfig, bool isExploding, float wx, float wy, int rdfId, float distanceAttenuationZ) {
        // Play "ActiveSfx" if configured
        bool shouldPlayActiveSfx = (0 < bullet.FramesInBlState && BulletState.Active == bullet.BlState && null != bulletConfig.ActiveSfxName);
        if (shouldPlayActiveSfx) {
            bool usingSameAudSrc = true;
            int atSfxLookupKey = KV_PREFIX_SFX_BULLET_ACTIVE + bullet.BulletLocalId;
            var atSfxSourceHolder = cachedSfxNodes.PopAny(atSfxLookupKey);
            if (null == atSfxSourceHolder) {
                atSfxSourceHolder = cachedSfxNodes.Pop();
                usingSameAudSrc = false;
            }

            if (null == atSfxSourceHolder) {
                return false;
                // throw new ArgumentNullException(String.Format("No available atSfxSourceHolder node for ftSfxLookupKey={0}", ftSfxLookupKey));
            }

            try {
                if (!atSfxSourceHolder.audioClipDict.ContainsKey(bulletConfig.ActiveSfxName)) {
                    return false;
                }

                float totAttZ = distanceAttenuationZ + footstepAttenuationZ; // Use footstep built-in attenuation for now
                newPosHolder.Set(wx, wy, totAttZ);
                atSfxSourceHolder.gameObject.transform.position = newPosHolder;
                if (!usingSameAudSrc || !atSfxSourceHolder.audioSource.isPlaying) {
                    atSfxSourceHolder.audioSource.volume = calcSfxVolume(atSfxSourceHolder, totAttZ);
                    atSfxSourceHolder.audioSource.PlayOneShot(atSfxSourceHolder.audioClipDict[bulletConfig.ActiveSfxName]);
                }
                atSfxSourceHolder.score = rdfId;
            } finally {
                cachedSfxNodes.Put(atSfxLookupKey, atSfxSourceHolder);
            }
        }

        // Play initla sfx for state
        bool isInitialFrame = (0 == bullet.FramesInBlState && (BulletState.Active != bullet.BlState || (BulletState.Active == bullet.BlState && 0 < bullet.ActiveSkillHit)));
        if (!isInitialFrame) {
            return false;
        }
        int sfxLookupKey = KV_PREFIX_SFX_BULLET_EXPLODE + bullet.BulletLocalId;
        var sfxSourceHolder = cachedSfxNodes.PopAny(sfxLookupKey);
        if (null == sfxSourceHolder) {
            sfxSourceHolder = cachedSfxNodes.Pop();
        }

        if (null == sfxSourceHolder) {
            return false;
            // throw new ArgumentNullException(String.Format("No available sfxSourceHolder node for sfxLookupKey={0}", sfxLookupKey));
        }

        try {
            string clipName = bulletConfig.FireballEmitSfxName;
            if (isExploding) {
                clipName = bulletConfig.ExplosionSfxName;
                if (IfaceCat.Rock == bullet.ExplodedOnIfc && !bulletConfig.ExplosionOnRockSfxName.IsEmpty()) {
                    clipName = bulletConfig.ExplosionOnRockSfxName;
                } else if (IfaceCat.Flesh == bullet.ExplodedOnIfc && !bulletConfig.ExplosionOnFleshSfxName.IsEmpty()) {
                    clipName = bulletConfig.ExplosionOnFleshSfxName;
                } else if (IfaceCat.Metal == bullet.ExplodedOnIfc && !bulletConfig.ExplosionOnMetalSfxName.IsEmpty()) {
                    clipName = bulletConfig.ExplosionOnMetalSfxName;
                } else if (IfaceCat.Wood == bullet.ExplodedOnIfc && !bulletConfig.ExplosionOnWoodSfxName.IsEmpty()) {
                    clipName = bulletConfig.ExplosionOnWoodSfxName;
                }
            }
            if (null == clipName) {
                return false;
            }
            if (!sfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                return false;
            }

            newPosHolder.Set(wx, wy, distanceAttenuationZ);
            sfxSourceHolder.gameObject.transform.position = newPosHolder;
            sfxSourceHolder.audioSource.volume = calcSfxVolume(sfxSourceHolder, distanceAttenuationZ);
            sfxSourceHolder.audioSource.PlayOneShot(sfxSourceHolder.audioClipDict[clipName]);
            sfxSourceHolder.score = rdfId;
        } finally {
            cachedSfxNodes.Put(sfxLookupKey, sfxSourceHolder);
        }

        return true;
    }

    public bool playBulletVfx(Bullet bullet, BulletConfig bulletConfig, bool isStartup, bool isExploding, float wx, float wy, RoomDownsyncFrame rdf) {
        bool bulletFromCharacter = (0 < bullet.OffenderJoinIndex);
        int j = (bulletFromCharacter ? bullet.OffenderJoinIndex - 1 : bullet.OffenderTrapLocalId - 1);
        if (bulletFromCharacter && bulletConfig.RotateOffenderWithSpin && IsBulletActive(bullet, bulletConfig, rdf.Id) && 0 <= j && j < roomCapacity + currRdfNpcAnimHoldersCnt) {
            var chAnimCtrl = (roomCapacity > j ? playerGameObjs[j].GetComponent<CharacterAnimController>() : currRdfNpcAnimHolders[j - roomCapacity]);
            if (null!= chAnimCtrl) {
                var spr = chAnimCtrl.GetComponent<SpriteRenderer>();
                // already flipped, so need respect the current flip
                spr.transform.localRotation = Quaternion.AngleAxis(math.atan2(bullet.SpinSin, bullet.SpinCos) * Mathf.Rad2Deg, Vector3.forward);
            }
        }
        int vfxSpeciesId = isExploding ? 
                           bulletConfig.ExplosionVfxSpeciesId 
                           : 
                           isStartup ? bulletConfig.StartupVfxSpeciesId : bulletConfig.ActiveVfxSpeciesId;

        if (NO_VFX_ID == vfxSpeciesId) return false;
        if (!pixelatedVfxDict.ContainsKey(vfxSpeciesId)) return false;
        // For convenience, bullet vfx is only pixelated from now on
        var vfxConfig = pixelatedVfxDict[vfxSpeciesId];
        var vfxAnimName = vfxConfig.Name;
        int vfxLookupKey = 0;
        var framesInState = MAX_INT;
        int dirX = 0;
        newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ);
        if (vfxConfig.OnBullet) {
            if (!isExploding && !isStartup && !IsBulletActive(bullet, bulletConfig, rdf.Id)) return false;
            vfxLookupKey = KV_PREFIX_VFX + bullet.BulletLocalId;
            framesInState = bullet.FramesInBlState;
            dirX = bullet.DirX;
            if (VfxMotionType.Tracing == vfxConfig.MotionType) {
                newPosHolder.Set(wx, wy, fireballZ);
            } else if (VfxMotionType.Dropped == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            }
        } else if (vfxConfig.OnCharacter) {
            if ((0 > j || j >= roomCapacity+rdf.NpcsArr.Count)) {
                return false;
            }
            vfxLookupKey = KV_PREFIX_VFX_CH_EMIT + bullet.BulletLocalId;
            var ch = getChdFromRdf(bullet.OffenderJoinIndex, roomCapacity, rdf);
            if (MAGIC_JOIN_INDEX_INVALID == ch.JoinIndex || MAGIC_JOIN_INDEX_DEFAULT == ch.JoinIndex || ch.ActiveSkillId != bullet.SkillId) return false;
            framesInState = ch.FramesInChState;
            dirX = ch.DirX;
            if (VfxMotionType.Tracing == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(ch.VirtualGridX, ch.VirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            } else if (VfxMotionType.Dropped == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            }
        } else if (vfxConfig.OnTrap) {
            vfxLookupKey = KV_PREFIX_VFX_CH_EMIT + bullet.BulletLocalId;
            var trap = rdf.TrapsArr[j];
            framesInState = trap.FramesInTrapState;
            dirX = trap.DirX;
            if (VfxMotionType.Tracing == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(trap.VirtualGridX, trap.VirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            } else if (VfxMotionType.Dropped == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            }
        }

        if (!bulletConfig.BeamCollision && !bulletConfig.BeamRendering && !isGameObjPositionWithinCamera(newPosHolder)) {
            return false;
        }

        if (NO_VFX_ID != vfxLookupKey) {
            if (!bulletConfig.BeamCollision && !bulletConfig.BeamRendering) {
                var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
                if (null == pixelVfxHolder) {
                    pixelVfxHolder = cachedPixelVfxNodes.Pop();
                    //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
                } else {
                    //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
                }

                if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                    if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                        pixelVfxHolder.updateAnim(vfxAnimName, framesInState, dirX, false, rdf.Id);
                        pixelVfxHolder.gameObject.transform.position = newPosHolder;
                    }
                    pixelVfxHolder.score = rdf.Id;
                    cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
                }
            } else {
                if (isExploding) {
                    return false;
                }
                var plasmaVfxHolder = cachedPixelPlasmaVfxNodes.PopAny(vfxLookupKey);
                if (null == plasmaVfxHolder) {
                    plasmaVfxHolder = cachedPixelPlasmaVfxNodes.Pop();
                    //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
                } else {
                    //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
                }

                if (null != plasmaVfxHolder && null != plasmaVfxHolder.lookUpTable) {
                    if (plasmaVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                        bool spinFlipX = (0 > dirX);
                        var spr = plasmaVfxHolder.GetComponent<SpriteRenderer>();
                        plasmaVfxHolder.updateAnim(vfxAnimName, framesInState, dirX, false, rdf.Id);
                        var (boxCw, boxCh) = bulletConfig.BeamCollision 
                                            ? 
                                            VirtualGridToPolygonColliderCtr(bullet.VirtualGridX - bullet.OriginatedVirtualGridX, bulletConfig.HitboxSizeY + bulletConfig.HitboxSizeIncY * (int)bullet.FramesInBlState) // [WARNING] For beam collision, the "change rate of hitbox" depends solely on (bullet.VelX, bullet.Config.AngularVel), i.e. no role for "bullet.VelY"!
                                            :
                                            VirtualGridToPolygonColliderCtr(bullet.VirtualGridX - bullet.OriginatedVirtualGridX, bullet.VirtualGridY - bullet.OriginatedVirtualGridY)
                                            ;
                        float beamVisualLength2 = bulletConfig.BeamCollision
                                                ?
                                                (boxCw * boxCw)
                                                :
                                                (boxCw * boxCw + boxCh * boxCh);
                        float invBeamVisualLength = Battle.InvSqrt32(beamVisualLength2);
                        float absBeamVisualLength = beamVisualLength2 * invBeamVisualLength;
                        var (_, beamVisualCh) = VirtualGridToPolygonColliderCtr(0, bulletConfig.BeamVisualSizeY + bulletConfig.HitboxSizeIncY * (int)bullet.FramesInBlState);
                        var (cx2, cy2) = VirtualGridToPolygonColliderCtr((spinFlipX ? bullet.VirtualGridX : bullet.OriginatedVirtualGridX), (spinFlipX ? bullet.VirtualGridY : bullet.OriginatedVirtualGridY));
                        var (wx2, wy2) = CollisionSpacePositionToWorldPosition(cx2, cy2, spaceOffsetX, spaceOffsetY);
                        newPosHolder.Set(wx2, wy2, fireballZ-1);
                        plasmaVfxHolder.gameObject.transform.position = newPosHolder;
                        if (IsBulletRotary(bulletConfig) || (bulletConfig.RotatesAlongVelocity && 0 != bullet.VelX)) {
                            if (bulletConfig.BeamCollision) {
                                if (spinFlipX) {
                                    /*
                                     [WARNING] The sprite anchor of any plasma vfx is (0, 0.5), regardless of "plasmaVfxHolder.GetComponent<SpriteRenderer>().flipX", the anchor x position is always at "0 == boxCw". 
                                    */
                                    float newDx, newDy;
                                    var (ccx2, ccy2) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                                    Vector.Rotate(boxCw, 0, bullet.SpinCos, bullet.SpinSin, out newDx, out newDy);
                                    (wx2, wy2) = CollisionSpacePositionToWorldPosition((ccx2 + newDx), (ccy2 + newDy), spaceOffsetX, spaceOffsetY);
                                    newPosHolder.Set(wx2, wy2, fireballZ - 1);
                                    plasmaVfxHolder.gameObject.transform.position = newPosHolder;
                                } else {
                                    // The sprite anchor of any plasma vfx is (0, 0.5), there's no translation needed when "false == spinFlipX". 
                                }
                                plasmaVfxHolder.transform.localRotation = Quaternion.AngleAxis(math.atan2(bullet.SpinSin, bullet.SpinCos) * Mathf.Rad2Deg, Vector3.forward);
                            } else {
                                plasmaVfxHolder.transform.localRotation = Quaternion.AngleAxis(math.atan2(bullet.VelY, bullet.VelX) * Mathf.Rad2Deg, Vector3.forward);
                                spr.flipX = false;
                            }
                        } else {
                            plasmaVfxHolder.transform.localRotation = Quaternion.AngleAxis(0, Vector3.forward);
                        }
                        camSpeedHolder.Set(0 > boxCw ? -absBeamVisualLength : absBeamVisualLength, beamVisualCh);
                        spr.size = camSpeedHolder;
                    }
                    plasmaVfxHolder.score = rdf.Id;
                    cachedPixelPlasmaVfxNodes.Put(vfxLookupKey, plasmaVfxHolder);
                }
            }
        }
        
        return true;
    }

    public float calcSfxVolume(SFXSource sfxSource, float totAttZ) {
        if (totAttZ <= 0) return 1f;
        if (totAttZ >= sfxSource.maxDistanceInWorld) return 0f;
        return (float)Math.Pow((double)12f, (double)(-totAttZ / sfxSource.maxDistanceInWorld));
    }

    public string calcFootStepSfxName(CharacterDownsync currCharacterDownsync) {
        // TODO: Record the contacted barrier material ID in "CharacterDownsync" to achieve more granular footstep sound derivation!  
        return "FootStep1";
    }

    public void pauseAllAnimatingCharacters(bool toPause) {
        iptmgr.gameObject.SetActive(!toPause);

        for (int k = 0; k < roomCapacity; k++) {
            var playerGameObj = playerGameObjs[k];
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.pause(toPause);
        }

        var (ok, playerRdf) = renderBuffer.GetByFrameId(playerRdfId);
        if (!ok || null == playerRdf) {
            Debug.LogWarning("Unable to get playerRdf by playerRdfId=" + playerRdfId);
            return;
        }
        for (int k = 0; k < playerRdf.NpcsArr.Count; k++) {
            var currNpcDownsync = playerRdf.NpcsArr[k];
            if (TERMINATING_PLAYER_ID == currNpcDownsync.Id) break;
            var speciesKvPq = cachedNpcs[currNpcDownsync.SpeciesId];
            int lookupKey = KV_PREFIX_NPC + currNpcDownsync.Id;
            var npcAnimHolder = speciesKvPq.PopAny(lookupKey);
            if (null == npcAnimHolder) continue;
            npcAnimHolder.pause(toPause);
            speciesKvPq.Put(lookupKey, npcAnimHolder);
        }
    }

    protected void attachParallaxEffect() {
        var grid = underlyingMap.GetComponentInChildren<Grid>();
        foreach (SuperTileLayer layer in grid.GetComponentsInChildren<SuperTileLayer>()) {
            if (1.0f == layer.m_ParallaxX) continue;
            var parallaxEffect = layer.gameObject.AddComponent<ParallaxEffect>();
            parallaxEffect.SetParallaxAmount(layer.m_ParallaxX, gameplayCamera);
        }
    }

#nullable enable
    protected void setCharacterGameObjectPosByInterpolation(CharacterDownsync? prevCharacterDownsync, CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, GameObject chGameObj, float newWx, float newWy) {
        float effZ = calcEffCharacterZ(currCharacterDownsync, chConfig);
        bool justDead = (null != prevCharacterDownsync && (CharacterState.Dying == prevCharacterDownsync.CharacterState || 0 >= prevCharacterDownsync.Hp));
        justDead |= currCharacterDownsync.NewBirth;
        if (justDead) {
            // Revived from Dying state.
            newPosHolder.Set(newWx, newWy, effZ);
            return;
        }

        float dWx = (newWx-chGameObj.transform.position.x);
        float dWy = (newWy-chGameObj.transform.position.y);
        float dis2 = dWx*dWx + dWy*dWy;
        var (velXWorld, velYWorld) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VelX + currCharacterDownsync.FrictionVelX, 0 < currCharacterDownsync.FrictionVelY ? currCharacterDownsync.FrictionVelY : (currCharacterDownsync.VelY + currCharacterDownsync.FrictionVelY)); // Just roughly, using "currCharacterDownsync" wouldn't cause NullPointerException thus more effective, and "CharacterDownsync.VelX & VelY" is already normalized to "per frame distance"
        var speedReachable2 = (velXWorld*velXWorld + velYWorld*velYWorld);
        var (chConfigSpeedReachable, _) = VirtualGridToPolygonColliderCtr(chConfig.Speed, 0);
        float defaultSpeedReachable2 = (chConfigSpeedReachable * chConfigSpeedReachable);
        float tolerance2 = 0.01f*Math.Max(speedReachable2, defaultSpeedReachable2);

        if (dis2 <= tolerance2) {
            newPosHolder.Set(newWx, newWy, effZ);
        } else {
            // dis2 > tolerance2 >= 0
            float invMag = InvSqrt32(dis2);
            float ratio = 0;
            if (0 < speedReachable2 && speedReachable2 > defaultSpeedReachable2) {
                float speedReachable = speedReachable2 * InvSqrt32(speedReachable2);
                ratio = speedReachable*invMag; 
            } else {
                ratio = chConfigSpeedReachable*invMag; 
            }
            ratio *= 2.0f; // [WARNING] Empirically, either "speedReachable" or "chConfigSpeedReachable" could be too small when being dragged by a Rider (i.e. with hardPushback)
            if (ratio > 1.0f) ratio = 1.0f;
            float interpolatedWx = chGameObj.transform.position.x + ratio * dWx;
            float interpolatedWy = chGameObj.transform.position.y + ratio * dWy;
            newPosHolder.Set(interpolatedWx, interpolatedWy, effZ);
        }
    }
#nullable disable
    public static void RotatePoints(Vector3[] noAnchorPts, float absBoxCw, int cnt, float cosDelta, float sinDelta, float spinAnchorX, float spinAnchorY, bool spinFlipX) {
        if (0 == sinDelta && 0 == cosDelta) return;
        int bottomMostAndLeftMostJ = 0;
        for (int j = 0; j < cnt; j++) {
            var v = noAnchorPts[j];
            if (v.y > noAnchorPts[bottomMostAndLeftMostJ].y) continue;
            if (v.y < noAnchorPts[bottomMostAndLeftMostJ].y || v.x < noAnchorPts[bottomMostAndLeftMostJ].x) {
                bottomMostAndLeftMostJ = j;
            }
        }
        float anchorCx = noAnchorPts[bottomMostAndLeftMostJ].x, anchorCy = noAnchorPts[bottomMostAndLeftMostJ].y;
        if (absBoxCw < spinAnchorX) absBoxCw = spinAnchorX;
        float effSpinAnchorOffsetX = (!spinFlipX ? spinAnchorX : absBoxCw - spinAnchorX);
        float spinAnchorCx = anchorCx + effSpinAnchorOffsetX, spinAnchorCy = anchorCy + spinAnchorY;
        for (int i = 0; i < cnt; i++) {
            float dx = noAnchorPts[i].x - spinAnchorCx;
            float dy = noAnchorPts[i].y - spinAnchorCy;
            float newDx, newDy;
            Vector.Rotate(dx, dy, cosDelta, sinDelta, out newDx, out newDy);
            noAnchorPts[i].x = spinAnchorCx + newDx;
            noAnchorPts[i].y = spinAnchorCy + newDy;
        }
    }

    private float calcEffCharacterZ(CharacterDownsync currCd, CharacterConfig chConfig) {
        bool isNonAttacking = nonAttackingSet.Contains(currCd.CharacterState);
        if (!isNonAttacking) {
            return flyingCharacterZ - 1;
        } else {
            return (!currCd.OmitGravity && !chConfig.OmitGravity) ? characterZ : flyingCharacterZ;
        }
    }

    private bool isXFlipped(uint superTileId) {
        TileIdMath v = new TileIdMath(superTileId);
        return v.HasHorizontalFlip;
    }
}
