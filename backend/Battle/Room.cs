using shared;
using static shared.Battle;
using System.Net;
using System.Net.WebSockets;
using System.Net.Sockets;
using Google.Protobuf;
using Pbc = Google.Protobuf.Collections;
using Google.Protobuf.Collections;
using System.Collections.Concurrent;

namespace backend.Battle;
public class Room {

    private TimeSpan DEFAULT_BACK_TO_FRONT_WS_WRITE_TIMEOUT = TimeSpan.FromMilliseconds(5000);
    private int renderBufferSize = 96;
    public int id;
    public int capacity;
    public int preallocNpcCapacity = DEFAULT_PREALLOC_NPC_CAPACITY;
    public int preallocBulletCapacity = DEFAULT_PREALLOC_BULLET_CAPACITY;
    public int preallocTrapCapacity = DEFAULT_PREALLOC_TRAP_CAPACITY;
    public int preallocTriggerCapacity = DEFAULT_PREALLOC_TRIGGER_CAPACITY;
    public int preallocEvtSubCapacity = DEFAULT_PREALLOC_EVTSUB_CAPACITY;
    public int preallocPickableCapacity = DEFAULT_PREALLOC_PICKABLE_CAPACITY;

    public int justTriggeredStoryPointId = 0; // Not used in backend

    public int battleDurationFrames;
    public int estimatedMillisPerFrame;

    public string stageName;
    public int inputFrameUpsyncDelayTolerance;
    public int maxChasingRenderFramesPerUpdate;
    public bool frameLogEnabled;

    int renderFrameId;
    int curDynamicsRenderFrameId;
    int lastForceResyncedRdfId;
    int nstDelayFrames;

    private int FORCE_RESYNC_INTERVAL_THRESHOLD = 8*BATTLE_DYNAMICS_FPS;

    Dictionary<int, Player> players;
    Player[] playersArr; // ordered by joinIndex

    private readonly Random _randGenerator = new Random();

    ILoggerBridge loggerBridge;

    int localPlayerWsDownsyncQueBattleReadTimeoutMillis = 2000; 

    int localPlayerWsDownsyncQueClearingReadTimeoutMillis = 800; // [WARNING] By reaching "clearPlayerNetworkSession(playerId)", no more elements will be enqueing "playerWsDownsyncQueDict[playerId]", yet the "playerSignalToCloseDict[playerId]" could've already been cancelled -- hence if the queue has been empty for several hundred milliseconds, we see it as truly empty. 
    Dictionary<int, PlayerSessionAckWatchdog> playerActiveWatchdogDict;
    public long state;
    int effectivePlayerCount;
    int participantChangeId;

    protected int missionEvtSubId;
    protected int[] justFulfilledEvtSubArr;
    protected int justFulfilledEvtSubCnt;
    protected int[] lastIndividuallyConfirmedInputFrameId;
    protected ulong[] lastIndividuallyConfirmedInputList;
    protected FrameRingBuffer<RoomDownsyncFrame> renderBuffer;
    protected FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer;
    protected FrameRingBuffer<InputFrameDownsync> inputBuffer;

    protected RoomDownsyncFrame historyRdfHolder;
    protected SatResult overlapResult, primaryOverlapResult;
    protected Dictionary<int, BattleResult> unconfirmedBattleResult;
    protected BattleResult confirmedBattleResult;
    protected Vector[] effPushbacks, softPushbacks;
    protected Vector[][] hardPushbackNormsArr;
    protected bool softPushbackEnabled;

    //////////////////////////////Collider related fields////////////////////////////////// 
    protected Collision collisionHolder;
    protected FrameRingBuffer<Collider> residueCollided;
    protected Collider[] dynamicRectangleColliders;
    protected Collider[] staticColliders;
    protected int staticCollidersCnt;
    protected List<Collider> completelyStaticTrapColliders;
    protected CollisionSpace collisionSys;
    protected int maxTouchingCellsCnt;
    //////////////////////////////Collider related fields////////////////////////////////// 

    protected InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    protected Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput;
    protected Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs;
    protected Dictionary<int, int> triggerTrackingIdToTrapLocalId;
    protected Dictionary<int, int> joinIndexRemap;
    protected HashSet<int> justDeadJoinIndices;

    int lastAllConfirmedInputFrameId;
    int lastAllConfirmedInputFrameIdWithChange;
    int latestPlayerUpsyncedInputFrameId;
    ulong[] lastAllConfirmedInputList;
    bool[] joinIndexBooleanArr;

    bool backendDynamicsEnabled;

    public PeerUdpAddr? battleUdpTunnelAddr;
    public RepeatedField<PeerUdpAddr>? peerUdpAddrList;
    
    IRoomManager _roomManager;
    ILoggerFactory _loggerFactory;
    ILogger<Room> _logger;

    //////////////////////////////Battle lifecycle disposables////////////////////////////////// 
    Task? battleMainLoopTask;
    Task? battleUdpTask;
    UdpClient? battleUdpTunnel;
    CancellationTokenSource? battleUdpTunnelCancellationTokenSource;

    /**
     * The following `CharacterDownsyncSessionDict` is NOT individually put
     * under `type Player struct` for a reason.
     *
     * Upon each connection establishment, a new instance `player Player` is created for the given `playerId`.

     * To be specific, if
     *   - that `playerId == 42` accidentally reconnects in just several milliseconds after a passive disconnection, e.g. due to bad wireless signal strength, and
     *   - that `type Player struct` contains a `DownsyncSession` field
     *
     * , then we might have to
     *   - clean up `previousPlayerInstance.DownsyncSession`
     *   - initialize `currentPlayerInstance.DownsyncSession`
     *
     * to avoid chaotic flaws.
     *
     * Moreover, during the invocation of `PlayerSignalToCloseDict`, the `Player` instance is supposed to be deallocated (though not synchronously).
*/
    Dictionary<int, WebSocket> playerDownsyncSessionDict;
    Dictionary<int, (CancellationTokenSource, CancellationToken)> playerSignalToCloseDict;
    Dictionary<int, BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>> playerWsDownsyncQueDict;
    Dictionary<int, Task> playerDownsyncLoopDict;
    //////////////////////////////Battle lifecycle disposables////////////////////////////////// 

    //////////////////////////////Room lifecycle disposables////////////////////////////////// 
    Mutex joinerLock;         // Guards [AddPlayerIfPossible, ReAddPlayerIfPossible, OnPlayerDisconnected, dismiss]
    Mutex inputBufferLock;         // Guards [*renderBuffer*, inputBuffer, latestPlayerUpsyncedInputFrameId, lastAllConfirmedInputFrameId, lastAllConfirmedInputList, lastAllConfirmedInputFrameIdWithChange, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, player.LastConsecutiveRecvInputFrameId]
    //////////////////////////////Room lifecycle disposables////////////////////////////////// 

    public class RoomLoggerBridge : ILoggerBridge {
        private ILogger<Room> _intLogger;

        public RoomLoggerBridge(ILogger<Room> extLogger) {
            _intLogger = extLogger;
        }
        public void LogError(string str, Exception ex) {
            _intLogger.LogError(ex, str);
        }

        public void LogInfo(string str) {
            _intLogger.LogInformation(str);
        }

        public void LogWarn(string str) {
            _intLogger.LogWarning(str);
        }
    }

    public Room(IRoomManager roomManager, ILoggerFactory loggerFactory, int roomId, int roomCapacity) {
        _roomManager = roomManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<Room>();
        id = roomId;
        capacity = roomCapacity;
        renderFrameId = 0;
        curDynamicsRenderFrameId = 0;
        lastForceResyncedRdfId = 0;
        frameLogEnabled = true;
        int durationSeconds = 60;
        battleDurationFrames = durationSeconds * BATTLE_DYNAMICS_FPS;
        estimatedMillisPerFrame = (int)Math.Ceiling(1000.0f/BATTLE_DYNAMICS_FPS); // ceiling to dilute the framerate on server 
        stageName = "Dungeon";
        maxChasingRenderFramesPerUpdate = 9; // Don't set this value too high to avoid exhausting frontend CPU within a single frame, roughly as the "turn-around frames to recover" is empirically OK                                                    
        nstDelayFrames = 24;
        inputFrameUpsyncDelayTolerance = ConvertToNoDelayInputFrameId(nstDelayFrames) - 1; // this value should be strictly smaller than (NstDelayFrames >> InputScaleFrames), otherwise "type#1 forceConfirmation" might become a lag avalanche
        state = ROOM_STATE_IDLE;
        effectivePlayerCount = 0;
        backendDynamicsEnabled = true;

        rdfIdToActuallyUsedInput = new Dictionary<int, InputFrameDownsync>();
        trapLocalIdToColliderAttrs = new Dictionary<int, List<TrapColliderAttr>>();
        triggerTrackingIdToTrapLocalId = new Dictionary<int, int>();
        joinIndexRemap = new Dictionary<int, int>();
        justDeadJoinIndices = new HashSet<int>();
        unconfirmedBattleResult = new Dictionary<int, BattleResult>();
        historyRdfHolder = NewPreallocatedRoomDownsyncFrame(capacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocEvtSubCapacity, preallocPickableCapacity);

        // Preallocate battle dynamic fields other than "Collider related" ones
        preallocateStepHolders(capacity, renderBufferSize, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocEvtSubCapacity, preallocPickableCapacity, out justFulfilledEvtSubCnt, out justFulfilledEvtSubArr, out renderBuffer, out pushbackFrameLogBuffer, out inputBuffer, out lastIndividuallyConfirmedInputFrameId, out lastIndividuallyConfirmedInputList, out effPushbacks, out hardPushbackNormsArr, out softPushbacks, out decodedInputHolder, out prevDecodedInputHolder, out confirmedBattleResult, out softPushbackEnabled, frameLogEnabled);

        // "Collider related" fields Will be reset in "refreshCollider" anyway
        dynamicRectangleColliders = new Collider[0];
        staticColliders = new Collider[0];
        completelyStaticTrapColliders = new List<Collider>();
        collisionSys = new CollisionSpace(1, 1, 1, 1); 
        collisionHolder = new Collision();
        residueCollided = new FrameRingBuffer<Collider>(0);
        
        // Preallocate network management fields
        players = new Dictionary<int, Player>();
        playersArr = new Player[capacity];

        playerActiveWatchdogDict = new Dictionary<int, PlayerSessionAckWatchdog>();
        playerDownsyncSessionDict = new Dictionary<int, WebSocket>();
        playerSignalToCloseDict = new Dictionary<int, (CancellationTokenSource, CancellationToken)>();
        playerWsDownsyncQueDict = new Dictionary<int, BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>>();
        playerDownsyncLoopDict = new Dictionary<int, Task>();

        lastAllConfirmedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
        latestPlayerUpsyncedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
        lastAllConfirmedInputList = new ulong[capacity];
        joinIndexBooleanArr = new bool[capacity];

        lastIndividuallyConfirmedInputFrameId = new int[capacity];
        lastIndividuallyConfirmedInputList = new ulong[capacity];

        loggerBridge = new RoomLoggerBridge(_logger);

        joinerLock = new Mutex();
        inputBufferLock = new Mutex();
    }

    public bool IsFull() {
        return capacity <= effectivePlayerCount;
    }

    public int GetRenderCacheSize() {
        return ((inputBuffer.N - 1) << 1);
    }

    public int AddPlayerIfPossible(Player pPlayerFromDbInit, int playerId, int speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer, CancellationToken signalToCloseConnOfThisPlayerToken) {
        joinerLock.WaitOne();
        try {
            long nowRoomState = Interlocked.Read(ref state); 
            if (ROOM_STATE_IDLE != nowRoomState && ROOM_STATE_WAITING != nowRoomState) {
                return ErrCode.PlayerNotAddableToRoom;
            }

            if (players.ContainsKey(playerId)) {
                return ErrCode.SamePlayerAlreadyInSameRoom;
            }

            pPlayerFromDbInit.AckingFrameId = -1;
            pPlayerFromDbInit.AckingInputFrameId = -1;
            pPlayerFromDbInit.LastSentInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
            pPlayerFromDbInit.LastConsecutiveRecvInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
            pPlayerFromDbInit.BattleState = PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK;

            pPlayerFromDbInit.CharacterDownsync = new CharacterDownsync();
            pPlayerFromDbInit.CharacterDownsync.Id = playerId;
            pPlayerFromDbInit.CharacterDownsync.SpeciesId = speciesId;
            pPlayerFromDbInit.CharacterDownsync.InAir = true;

            pPlayerFromDbInit.BattleUdpTunnelAuthKey = _randGenerator.Next();
            players[playerId] = pPlayerFromDbInit;

            playerDownsyncSessionDict[playerId] = session;
            playerSignalToCloseDict[playerId] = (signalToCloseConnOfThisPlayer, signalToCloseConnOfThisPlayerToken);

            var newWatchdog = new PlayerSessionAckWatchdog(10000, signalToCloseConnOfThisPlayer, String.Format("[ RoomId={0}, PlayerId={1} ] session watchdog ticked.", id, playerId), _loggerFactory);
            newWatchdog.Stop();
            playerActiveWatchdogDict[playerId] = newWatchdog;

            var newGenOrderPreservedMsgs = new BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>();
            playerWsDownsyncQueDict[playerId] = newGenOrderPreservedMsgs;

            effectivePlayerCount++;

            if (1 == effectivePlayerCount) {
                Interlocked.Exchange(ref state, ROOM_STATE_WAITING);
                // [WARNING] Each player starts hole-punching after receiving "DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO".
                if (null == battleUdpTask && initUdpClient()) {
                    _logger.LogInformation("starting `battleUdpTask` for (roomId={0})", id);
                    battleUdpTask = Task.Run(startBattleUdpTunnelAsyncTask);
                }
            }

            for (int i = 0; i < capacity; i++) {
                if (joinIndexBooleanArr[i]) continue;
                var targetPlayer = players[playerId];
                if (null == targetPlayer) continue;
                if (null == targetPlayer.CharacterDownsync) continue;
                joinIndexBooleanArr[i] = true;
                targetPlayer.CharacterDownsync.JoinIndex = i + 1;
                var chosenCh = characters[targetPlayer.CharacterDownsync.SpeciesId];
                targetPlayer.CharacterDownsync.Speed = chosenCh.Speed;
                break;
            }

            return ErrCode.Ok;
        } finally {
            joinerLock.ReleaseMutex();
        }
    }

    public void OnPlayerDisconnected(int playerId) {
        // [WARNING] Unlike the Golang version, Room.OnDisconnected here is only triggered AFTER "signalToCloseConnOfThisPlayerCapture.Cancel()".
        joinerLock.WaitOne();
        try {
            Player? thatPlayer;
            if (players.TryGetValue(playerId, out thatPlayer)) {
                var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
                switch (thatPlayerBattleState) {
                    case PLAYER_BATTLE_STATE_DISCONNECTED:
                    case PLAYER_BATTLE_STATE_LOST:
                    case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                    case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                        _logger.LogInformation("Room OnPlayerDisconnected[early return #1] [ roomId={0}, playerId={1}, playerBattleState={2}, nowRoomState={3}, nowRoomEffectivePlayerCount={4} ]", id, playerId, thatPlayerBattleState, state, effectivePlayerCount);
                        return;
                    default:
                        break;
                }
            } else {
                _logger.LogInformation("Room OnPlayerDisconnected[early return #2] [ roomId={0}, playerId={1} doesn't exist! nowRoomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                return;
            }

            switch (state) {
                case ROOM_STATE_WAITING:
                    clearPlayerNetworkSession(playerId);
                    effectivePlayerCount--;
                    joinIndexBooleanArr[thatPlayer.CharacterDownsync.JoinIndex - 1] = false;

                    players.Remove(playerId);
                    if (0 == effectivePlayerCount) {
                        Interlocked.Exchange(ref state, ROOM_STATE_IDLE);
                    }
                    _logger.LogInformation("OnPlayerDisconnected finished: [ roomId={0}, playerId={1}, roomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                    break;
                default:
                    Interlocked.Exchange(ref thatPlayer.BattleState, PLAYER_BATTLE_STATE_DISCONNECTED);
                    clearPlayerNetworkSession(playerId);
                    _logger.LogInformation("OnPlayerDisconnected finished: [ roomId={0}, playerId={1}, roomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                    break;
            }
        } finally {
            joinerLock.ReleaseMutex();
        }
    }

    private async void clearPlayerNetworkSession(int playerId) {
        if (playerDownsyncSessionDict.ContainsKey(playerId)) {
            // [WARNING] No need to close "pR.CharacterDownsyncChanDict[playerId]" immediately!
            if (playerActiveWatchdogDict.ContainsKey(playerId)) {
                if (null != playerActiveWatchdogDict[playerId]) {
                    playerActiveWatchdogDict[playerId].Stop();
                }
                playerActiveWatchdogDict.Remove(playerId);
            }
            
            if (playerSignalToCloseDict.ContainsKey(playerId)) {
                var (cancellationTokenSource, _) = playerSignalToCloseDict[playerId];
                /*
                 [WARNING]

                 "clearPlayerNetworkSession" will only be called by 
                - OnPlayerDisconnected(...) which will only be called by "WebSocketController.HandleNewPlayerPrimarySession" after proactive close received or cancelled on the same signal
                - DismissBattleAsync()

                thus calling "cancellationTokenSource.Cancel()" here would NOT by any chance interrupt this function itself.
                 */
                if (!cancellationTokenSource.IsCancellationRequested) {
                    cancellationTokenSource.Cancel();
                }
                playerSignalToCloseDict.Remove(playerId);
                // [WARNING] Disposal of each "playerSignalToClose" is automatically managed in "WebSocketController"
            }

            if (playerWsDownsyncQueDict.ContainsKey(playerId)) {
                var genOrderPreservedMsgs = playerWsDownsyncQueDict[playerId]; 
                while (genOrderPreservedMsgs.TryTake(out _, localPlayerWsDownsyncQueClearingReadTimeoutMillis)) { }
                genOrderPreservedMsgs.Dispose();
                playerWsDownsyncQueDict.Remove(playerId);
            }

            if (playerDownsyncLoopDict.ContainsKey(playerId)) {
                var downsyncLoop = playerDownsyncLoopDict[playerId];
                await downsyncLoop;
                downsyncLoop.Dispose();
                playerDownsyncLoopDict.Remove(playerId);
            }

            playerDownsyncSessionDict.Remove(playerId);
            // [WARNING] Disposal of each "wsSession" is automatically managed in "WebSocketController"

            _logger.LogInformation("clearPlayerNetworkSession finished: [ roomId={0}, playerId={1}, roomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
        }
    }

    public float calRoomScore() {
        var x = ((float)effectivePlayerCount) / capacity;
        var d = (x - 0.5f); // Such that when the room is half-full, the score is at minimum 
        var d2 = d * d;
        return 7.8125f * d2 - 5.0f + (float)(state);
    }

    private Pbc.RepeatedField<CharacterDownsync> clonePlayersArrToPb() {
        var bridgeArr = new CharacterDownsync[capacity]; // RepeatedField doesn't have a constructor to preallocate by size
        for (int i = 0; i < capacity; i++) {
            bridgeArr[i] = new CharacterDownsync();
            bridgeArr[i].Id = TERMINATING_PLAYER_ID;
        }
        foreach (var (_, player) in players) {
            bridgeArr[player.CharacterDownsync.JoinIndex - 1] = player.CharacterDownsync.Clone();
        }
        var ret = new Pbc.RepeatedField<CharacterDownsync> {
            bridgeArr
        };
        return ret;
    }

    public async Task<bool> OnPlayerBattleColliderAcked(int targetPlayerId, RoomDownsyncFrame selfParsedRdf, RepeatedField<SerializableConvexPolygon> serializedBarrierPolygons, RepeatedField<SerializedCompletelyStaticPatrolCueCollider> serializedStaticPatrolCues, RepeatedField<SerializedCompletelyStaticTrapCollider> serializedCompletelyStaticTraps, RepeatedField<SerializedCompletelyStaticTriggerCollider> serializedStaticTriggers, SerializedTrapLocalIdToColliderAttrs serializedTrapLocalIdToColliderAttrs, SerializedTriggerTrackingIdToTrapLocalId serializedTriggerTrackingIdToTrapLocalId, int spaceOffsetX, int spaceOffsetY, int battleDurationSeconds) {
        Player? targetPlayer;
        if (!players.TryGetValue(targetPlayerId, out targetPlayer)) {
            return false;
        }
        battleDurationFrames = battleDurationSeconds * BATTLE_DYNAMICS_FPS;
        bool shouldTryToStartBattle = true;
        var targetPlayerBattleState = Interlocked.Read(ref targetPlayer.BattleState);
        _logger.LogInformation("OnPlayerBattleColliderAcked-before: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);
        switch (targetPlayerBattleState) {
            case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                var playerAckedFrame = new RoomDownsyncFrame {
                    Id = renderFrameId,
                    ParticipantChangeId = participantChangeId++
                };
                playerAckedFrame.PlayersArr.AddRange(clonePlayersArrToPb());
                var tList = new List<Task>();
                // Broadcast normally added player info to all players in the same room
                foreach (var (thatPlayerId, thatPlayer) in players) {
                    /*
                       [WARNING]
                       This `playerAckedFrame` is the first ever "RoomDownsyncFrame" for every "PersistentSessionClient on the frontend", and it goes right after each "BattleColliderInfo".

                       By making use of the sequential nature of each ws session, all later "RoomDownsyncFrame"s generated after `pRoom.StartBattle()` will be put behind this `playerAckedFrame`.

                       This function is triggered by an upsync message via WebSocket, thus downsync sending is also available by now.
                    */
                    var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
                    _logger.LogInformation("OnPlayerBattleColliderAcked-middle: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={2}, thatPlayerId={3}, thatPlayerBattleState={4}", id, state, targetPlayerId, targetPlayerBattleState, thatPlayerId, thatPlayerBattleState);
                    if (thatPlayerId == targetPlayerId || (PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK == thatPlayerBattleState || PLAYER_BATTLE_STATE_ACTIVE == thatPlayerBattleState)) {
                        _logger.LogInformation("OnPlayerBattleColliderAcked-sending DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);

                        tList.Add(sendSafelyAsync(playerAckedFrame, null, null, DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED, thatPlayerId, thatPlayer, MAGIC_JOIN_INDEX_DEFAULT));
                    }
                }
                await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
                Interlocked.Exchange(ref targetPlayer.BattleState, PLAYER_BATTLE_STATE_ACTIVE);
                break;
            default:
                break;
        }

        _logger.LogInformation("OnPlayerBattleColliderAcked-post-downsync: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);
        
        try {
            joinerLock.WaitOne();
            if (0 >= renderBuffer.Cnt) {
                provisionStepHolders(capacity, out justFulfilledEvtSubCnt, renderBuffer, pushbackFrameLogBuffer, inputBuffer, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, effPushbacks, hardPushbackNormsArr, softPushbacks, confirmedBattleResult);
                    _logger.LogInformation("OnPlayerBattleColliderAcked-post-provisionStepHolders: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);
                
                renderBuffer.Put(selfParsedRdf);

                //_logger.LogInformation("OnPlayerBattleColliderAcked-post-downsync details: roomId={0}, selfParsedRdf={1}, serializedBarrierPolygons={2}", id, selfParsedRdf, serializedBarrierPolygons);

                refreshColliders(selfParsedRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerTrackingIdToTrapLocalId);

                _logger.LogInformation("OnPlayerBattleColliderAcked-post-downsync: Initialized renderBuffer by incoming startRdf for roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}, staticCollidersCnt={6}; now renderBuffer: {7}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount, staticCollidersCnt, renderBuffer.toSimpleStat());
            } else {
                var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                if (!ok1 || null == startRdf) {
                    throw new ArgumentNullException(String.Format("OnPlayerBattleColliderAcked-post-downsync: No existing startRdf for roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}; now renderBuffer: {6}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount, renderBuffer.toSimpleStat()));
                }
                var src = selfParsedRdf.PlayersArr[targetPlayer.CharacterDownsync.JoinIndex - 1];
                var dst = startRdf.PlayersArr[targetPlayer.CharacterDownsync.JoinIndex - 1];

                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FrictionVelY, src.FramesToRecover, src.FramesInChState, src.ActiveSkillId, src.ActiveSkillHit, src.FramesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.InAir, src.OnWall, src.OnWallNormX, src.OnWallNormY, src.FramesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, src.PrimarilyOnSlippableHardPushback, src.CapturedByPatrolCue, src.FramesInPatrolCue, src.BeatsCnt, src.BeatenCnt, src.Mp, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.WaivingSpontaneousPatrol, src.WaivingPatrolCueId, src.OnSlope, src.ForcedCrouching, src.NewBirth, src.LowerPartFramesInChState, src.JumpStarted, src.FramesToStartJump, src.BuffList, src.DebuffList, src.Inventory, false, src.PublishingEvtSubIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscriptionId, src.JumpHolding, src.BtnBHoldingRdfCount, src.RemainingAirJumpQuota, src.RemainingAirDashQuota, src.KilledToDropConsumableSpeciesId, src.KilledToDropBuffSpeciesId, src.BulletImmuneRecords, dst);

            }
        } finally {
            joinerLock.ReleaseMutex();
        }

        if (shouldTryToStartBattle) {
            if (capacity == effectivePlayerCount) {
                bool allAcked = true;
                foreach (var (thatPlayerId, thatPlayer) in players) {
                    var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
                    if (PLAYER_BATTLE_STATE_ACTIVE != thatPlayerBattleState) {
                        _logger.LogInformation("Unexpectedly got an inactive player: roomId={0}, thatPlayerId={1}, thatPlayerBattleState={2}", id, thatPlayerId, thatPlayerBattleState);
                        allAcked = false;
                        break;
                    }
                }
                if (true == allAcked) {
                    if (null != battleMainLoopTask) {
                        _logger.LogInformation("About to wait and dispose previous `battleMainLoopTask` for roomId={0}, targetPlayerId={1} for starting a new battle", id, targetPlayerId);
                        await battleMainLoopTask;
                        battleMainLoopTask.Dispose();
                    }
                    foreach (var (thatPlayerId, thatPlayer) in players) {
                        // Not checking "playerBattleState" again here
                        var t = Task.Run(async () => await downsyncToSinglePlayerAsyncLoop(thatPlayerId, thatPlayer));
                        playerDownsyncLoopDict[thatPlayerId] = t;
                    }
                    int gracePreReadyPeriodMillis = 1000;
                    await Task.Delay(gracePreReadyPeriodMillis);
                    startBattleAsync(); // WON'T run if the battle state is not in WAITING.
                }
            }
        }
        return true;
    }

    private async Task downsyncToSinglePlayerAsyncLoop(int playerId, Player player) {
        WebSocket? wsSession;
        BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>? genOrderPreservedMsgs;
        if (!playerDownsyncSessionDict.TryGetValue(playerId, out wsSession)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #1", id, playerId);
            return;
        }

        if (!playerSignalToCloseDict.TryGetValue(playerId, out (CancellationTokenSource c1, CancellationToken c2) cancellationSignal)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #2", id, playerId);
            return;
        }

        if (!playerWsDownsyncQueDict.TryGetValue(playerId, out genOrderPreservedMsgs)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #3", id, playerId);
            return;
        }

        _logger.LogInformation("Started downsyncToSinglePlayerAsyncLoop for (roomId: {0}, playerId: {1})", id, playerId);
        try {
            while (WebSocketState.Open == wsSession.State && !cancellationSignal.c1.IsCancellationRequested) {
                // [WARNING] If "TryTake" timed out while reading, it simply returns false and enters another round of reading.
                if (genOrderPreservedMsgs.TryTake(out (ArraySegment<byte> content, InputBufferSnapshot inputBufferSnapshot) msg, localPlayerWsDownsyncQueBattleReadTimeoutMillis, cancellationSignal.c2)) {
                    var inputBufferSnapshot = msg.inputBufferSnapshot;
                    var content = msg.content;
                    int refRenderFrameId = inputBufferSnapshot.RefRenderFrameId;
                    bool shouldResync = inputBufferSnapshot.ShouldForceResync;
                    var toSendInputFrameIdSt = (null == inputBufferSnapshot.ToSendInputFrameDownsyncs || 0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) ? TERMINATING_INPUT_FRAME_ID : inputBufferSnapshot.ToSendInputFrameDownsyncs[0].InputFrameId;
                    var toSendInputFrameIdEd = (null == inputBufferSnapshot.ToSendInputFrameDownsyncs || 0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) ? TERMINATING_INPUT_FRAME_ID : inputBufferSnapshot.ToSendInputFrameDownsyncs[inputBufferSnapshot.ToSendInputFrameDownsyncs.Count - 1].InputFrameId + 1;

                    /*
                    [WARNING] 

                    Reasons behind putting this "downsyncToSinglePlayerAsync" under a "Task.Run(...)" as follows.

                    - When "downsyncToAllPlayers" is invoked by "OnBattleCmdReceived(...)" which is in turn very frequently called **even upon UDP reception**, we want it to be "as I/O non-blocking as possible" -- that said, the need for "downsyncToAllPlayers" to **preserve the order of generation of "inputBufferSnapshot" for sending** still exists -- creating a somewhat dilemma. 

                    - The ideal behavior for me in this case is a "wsSession.PutToSendLater(...)" which executes synchronously in the current calling thread, thus returns immediately without "yielding at I/O awaiting". However "wsSession.SendAsync" returns an "async Task" -- certainly not the ideal form, i.e. "wsSession.SendAsync" respects "TCP flow control" to wait for corresponding ACKs when invocation rate is too high, which puts a significant "function return rate limit" of it ("yielding at I/O awaiting" is NOT a "return").  
                    */
                    var playerBattleState = Interlocked.Read(ref player.BattleState);

                    switch (playerBattleState) {
                        case PLAYER_BATTLE_STATE_DISCONNECTED:
                        case PLAYER_BATTLE_STATE_LOST:
                        case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                        case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                        case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                        case PLAYER_BATTLE_STATE_READDED_PENDING_BATTLE_COLLIDER_ACK:
                            // There're two additional conditions for early return here compared to "sendSafelyAsync", because "downsyncToSinglePlayerAsync" is dedicated for active players in active battle!
                            return;
                    }

                    /*
                        Resync helps
                        1. when player with a slower frontend clock lags significantly behind and thus wouldn't get its inputUpsync recognized due to faster "forceConfirmation"
                        2. reconnection
                    */

                    // [WARNING] Preserving generated order (of inputBufferSnapshot) while sending per player by simply "awaiting" the "wsSession.SendAsync(...)" calls
                    await wsSession.SendAsync(content, WebSocketMessageType.Binary, true, cancellationSignal.c2);

                    player.LastSentInputFrameId = toSendInputFrameIdEd - 1;

                    if (PLAYER_BATTLE_STATE_READDED_BATTLE_COLLIDER_ACKED == playerBattleState) {
                        if (backendDynamicsEnabled && shouldResync) {
                            _logger.LogInformation(String.Format("[readded-resync] Sent refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, playerId={4}, playerJoinIndex={5}, renderFrameId={6}, curDynamicsRenderFrameId={7}, playerLastSentInputFrameId={8}: playerBattleState={9}, contentByteLength={10}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, playerId, player.CharacterDownsync.JoinIndex, renderFrameId, curDynamicsRenderFrameId, player.LastSentInputFrameId, playerBattleState, content.Count));
                        }
                        Interlocked.Exchange(ref player.BattleState, PLAYER_BATTLE_STATE_ACTIVE);
                    }
                }
            }
        } catch (OperationCanceledException cEx) {
            _logger.LogWarning("downsyncToSinglePlayerAsyncLoop cancelled for (roomId: {0}, playerId: {1}). cEx={2}", id, playerId, cEx.Message);
        } catch (Exception ex) {
            _logger.LogError(ex, "Exception occurred during downsyncToSinglePlayerAsyncLoop to (roomId: {0}, playerId: {1})", id, playerId);
        } finally {
            _logger.LogInformation("Ended downsyncToSinglePlayerAsyncLoop for (roomId: {0}, playerId: {1})", id, playerId);
        }
    }

    private async void startBattleAsync() {
        var nowRoomState = Interlocked.Read(ref state);
        if (ROOM_STATE_WAITING != nowRoomState) {
            _logger.LogWarning("[StartBattle] Battle not started due to not being WAITING: roomId={0}, roomState={1}", id, state);
            return;
        }

        renderFrameId = 0;

        // Initialize the "collisionSys" as well as "RenderFrameBuffer"
        curDynamicsRenderFrameId = 0;

        lastForceResyncedRdfId = 0;

        Interlocked.Exchange(ref state, ROOM_STATE_PREPARE);

        foreach (var (_, player) in players) {
            int joinIndex = player.CharacterDownsync.JoinIndex;
            playersArr[joinIndex - 1] = player;
        }

        _logger.LogWarning("Battle state transited to ROOM_STATE_PREPARE for roomId={0}", id);

        var battleReadyToStartFrame = new RoomDownsyncFrame {
            Id = DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START
        };
        battleReadyToStartFrame.PlayersArr.AddRange(clonePlayersArrToPb());

        _logger.LogWarning("Sending out frame for ROOM_STATE_PREPARE: {0}", battleReadyToStartFrame);

        var tList = new List<Task>();
        foreach (var (playerId, player) in players) {
            tList.Add(sendSafelyAsync(battleReadyToStartFrame, null, null,DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START, playerId, player, MAGIC_JOIN_INDEX_DEFAULT));
        }
        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
        await Task.Delay(1500);

        /**
		  [WARNING] We actually need the "battleMainLoop" immediately switch into another thread for running, such that we can avoid putting an unevenly heavy load on the current thread (i.e. which is of a specific player session)! See "GOROUTINE_TO_ASYNC_TASK.md" for more information.

          Moreover, I'm deliberately NOT AWAITING here, because the execution of "OnPlayerBattleColliderAcked -> startBattleAsync" should continue without the result of "battleMainLoopActionAsync"!
		 */
        battleMainLoopTask = Task.Run(battleMainLoopAsync);
    }

    public async Task SettleBattleAsync() {
        var nowRoomState = Interlocked.Read(ref state);
        if (ROOM_STATE_IN_BATTLE != nowRoomState && ROOM_STATE_PREPARE != nowRoomState && ROOM_STATE_WAITING != nowRoomState) {
            return;
        }

        Interlocked.Exchange(ref state, ROOM_STATE_IN_SETTLEMENT);

        _logger.LogInformation("Stopping the `battleMainLoop` for: roomId={0}", id);
        var assembledFrame = new RoomDownsyncFrame {
            Id = renderFrameId
        };

        var tList = new List<Task>();
        // It's important to send kickoff frame iff  "0 == renderFrameId && nextRenderFrameId > renderFrameId", otherwise it might send duplicate kickoff frames
        foreach (var (playerId, player) in players) {
            tList.Add(sendSafelyAsync(assembledFrame, null, null, DOWNSYNC_MSG_ACT_BATTLE_STOPPED, playerId, player, MAGIC_JOIN_INDEX_DEFAULT));
        }
        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel

        _logger.LogInformation("The room is in settlement: roomId={0}", id);
    }

    private async Task DismissBattleAsync() {
        joinerLock.WaitOne();
        try {
            var nowRoomState = Interlocked.Read(ref state);
            if (ROOM_STATE_IN_SETTLEMENT != nowRoomState) {
                return;
            }

            if (frameLogEnabled) {
                wrapUpFrameLogs(renderBuffer, inputBuffer, rdfIdToActuallyUsedInput, true, pushbackFrameLogBuffer, Directory.GetCurrentDirectory(), String.Format("room-{0}.log", id));
            }

            /**
			[WARNING] "OnPlayerDisconnected" could be called after "SettleBattleAsync" and during "dismiss", but it's guaranteed that 
			- when "dismiss" is called, "Room.players" is always full, i.e. any "OnPlayerDisconnected" called in active battle wouldn't remove any entry in "Room.players"
			- if "OnPlayerDisconnected" is called during settlement or dismissal, it wouldn't remove any entry in "Room.players"	
			- if "OnPlayerDisconnected" is called during settlement or dismissal, the call to "clearPlayerNetworkSession" is always safe due to use of "joinerLock"	
			*/
            foreach (var (playerId, _) in players) {
                clearPlayerNetworkSession(playerId);
            }
            players.Clear();
            playersArr = new Player[capacity];

            rdfIdToActuallyUsedInput.Clear();
            renderBuffer.Clear();
            inputBuffer.Clear();

            lastAllConfirmedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED; // Such that the initial "lastAllConfirmedInputFrameId + 1" is 0, for use in "markConfirmationIfApplicable" 
            latestPlayerUpsyncedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
            Array.Fill<ulong>(lastAllConfirmedInputList, 0);
            Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, 0);
            Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
            Array.Fill<bool>(joinIndexBooleanArr, false);

            effectivePlayerCount = 0; // guaranteed to succeed at the end of "dismiss"
            participantChangeId = 0;
            battleDurationFrames = 10 * BATTLE_DYNAMICS_FPS;

            joinIndexRemap = new Dictionary<int, int>();
        } finally {
            joinerLock.ReleaseMutex();
        }

        if (null != battleUdpTask) {
            if (null != battleUdpTunnelCancellationTokenSource) {
                if (!battleUdpTunnelCancellationTokenSource.IsCancellationRequested) {
                    _logger.LogInformation("Cancelling `battleUdpTask` for: roomId={0} during dismissal", id);
                    battleUdpTunnelCancellationTokenSource.Cancel();
                } else {
                    _logger.LogInformation("`battleUdpTask` for: roomId={0} is already cancelled during dismissal", id);
                }
                battleUdpTunnelCancellationTokenSource.Dispose();
            } else {
                _logger.LogWarning("`battleUdpTask` for: roomId={0} is not null but `battleUdpTunnelCancellationTokenSource` is null during dismissal!", id);
            }
            battleUdpTunnelCancellationTokenSource = null;
            await battleUdpTask;
            battleUdpTask.Dispose();
            battleUdpTask = null;
            _logger.LogInformation("`battleUdpTask` for: roomId={0} fully disposed during dismissal!", id);
        }

        clearColliders(ref collisionSys, ref dynamicRectangleColliders, ref staticColliders, ref collisionHolder, ref completelyStaticTrapColliders, ref residueCollided);
        _logger.LogWarning("`Colliders` cleared for: roomId={0} fully disposed during dismissal!", id);
    }

    private async Task battleMainLoopAsync() {
        try {
            var nowRoomState = Interlocked.Read(ref this.state);
            if (ROOM_STATE_PREPARE != nowRoomState) {
                return;
            }

            var nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var battleStartedAt = nowMillis;

            Interlocked.Exchange(ref this.state, ROOM_STATE_IN_BATTLE);
            _logger.LogInformation("The `battleMainLoop` is started for: roomId={0}", id);
            foreach (var (_, watchdog) in playerActiveWatchdogDict) {
                watchdog.Kick();
            }
            while (renderFrameId <= battleDurationFrames) {
                nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var stCalculation = nowMillis;
                nowRoomState = Interlocked.Read(ref state);
                if (ROOM_STATE_IN_BATTLE != nowRoomState) {
                    break;
                }

                nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var totalElapsedMillis = nowMillis - battleStartedAt;

                int nextRenderFrameId = (int)((totalElapsedMillis + estimatedMillisPerFrame - 1) / estimatedMillisPerFrame); // fast ceiling

                int toSleepMillis = (estimatedMillisPerFrame >> 1); // Sleep half-frame time by default

                if (nextRenderFrameId > renderFrameId) {
                    if (0 == renderFrameId) {
                        var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                        if (!ok1 || null == startRdf) {
                            throw new ArgumentNullException(String.Format("OnPlayerBattleColliderAcked-post-downsync: No existing startRdf for roomId={0}, roomState={1}, capacity={2}, effectivePlayerCount={3}; now renderBuffer: {4}", id, state, capacity, effectivePlayerCount, renderBuffer.toSimpleStat()));
                        }
                        var tList = new List<Task>();
                        // It's important to send kickoff frame iff  "0 == renderFrameId && nextRenderFrameId > renderFrameId", otherwise it might send duplicate kickoff frames
                        
                        foreach (var (playerId, player) in players) {
                            tList.Add(sendSafelyAsync(startRdf, null, null, DOWNSYNC_MSG_ACT_BATTLE_START, playerId, player, MAGIC_JOIN_INDEX_DEFAULT));
                        }
                        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
                        _logger.LogInformation("In `battleMainLoop` for roomId={0} sent out startRdf with {1} bytes", id, startRdf.ToByteArray().Length);
                    }

                    int prevRenderFrameId = renderFrameId;
                    renderFrameId = nextRenderFrameId;
                    
                    ulong dynamicsDuration = 0ul;
                    // Prefab and buffer backend inputFrameDownsync
                    if (backendDynamicsEnabled) {
                        doBattleMainLoopPerTickBackendDynamicsWithProperLocking(prevRenderFrameId, ref dynamicsDuration);
                    }

                    var elapsedInCalculation = (DateTimeOffset.Now.ToUnixTimeMilliseconds() - stCalculation);
                    toSleepMillis = (int)(estimatedMillisPerFrame - elapsedInCalculation);
                    if (0 > toSleepMillis) toSleepMillis = 0; 
                }

                await Task.Delay(toSleepMillis);
            }

            _logger.LogInformation("Times up, will settle `battleMainLoopActionAsync` for roomId={0} @renderFrameId={1}", id, renderFrameId);
        } catch (Exception ex) {
            _logger.LogError(ex, "Error running battleMainLoopActionAsync for roomId={0}", id);
        } finally {
            await SettleBattleAsync();
            _logger.LogInformation("The `battleMainLoop` for roomId={0} is settled@renderFrameId={1}", id, renderFrameId);
            await DismissBattleAsync();
            _logger.LogInformation("The `battleMainLoop` for roomId={0} is dismissed@renderFrameId={1}", id, renderFrameId);
            Interlocked.Exchange(ref state, ROOM_STATE_IDLE);
            _roomManager.Push(calRoomScore(), this);
        }
    }

    public void OnBattleCmdReceived(WsReq pReq, int playerId, bool fromUDP) {
        /*
		   [WARNING] This function "OnBattleCmdReceived" could be called by different ws sessions and thus from different threads!

		   That said, "markConfirmationIfApplicable" will still work as expected. Here's an example of weird call orders.
		   ---------------------------------------------------
		   now lastAllConfirmedInputFrameId: 42; each "()" below indicates a "Lock/Unlock cycle of inputBufferLock", and "x" indicates no new all-confirmed snapshot is created
		   A: ([44,50],x)                                            ([49,54],snapshot=[51,53])
		   B:           ([54,58],x)
		   C:                               ([42,53],snapshot=[43,50])
		   D:                     ([51,55],x)
		   ---------------------------------------------------
		*/
        // TODO: Put a rate limiter on this function!
        var nowRoomState = Interlocked.Read(ref this.state);
        if (ROOM_STATE_IN_BATTLE != nowRoomState) {
            _logger.LogWarning("OnBattleCmdReceived early return because room state is not in battle: roomId={0}, fromPlayerId={1}, nowRoomState={2}", id, playerId, nowRoomState);
            return;
        }

        var inputFrameUpsyncBatch = pReq.InputFrameUpsyncBatch;
        var ackingFrameId = pReq.AckingFrameId;
        var ackingInputFrameId = pReq.AckingInputFrameId;

        Player? player;
        if (!players.TryGetValue(playerId, out player)) {
            _logger.LogWarning("OnBattleCmdReceived early return because player id is not in this room: roomId={0}, fromPlayerId={1}, nowRoomState={2}", id, playerId, nowRoomState);
            return;
        }

        if (false == fromUDP) {
            /*
             [WARNING]
            
             When an Android client changes network AP from 4g to Wi-Fi, the "ws backend session" held by this C# WebSocket service could no longer send/receive packets to/from that Android client (HOWEVER, there's NO automatic close callback invoked for that "ws backend session"), therefore we only kick the watchdog upon ws session rx/tx for better sensitivity.
             */
            PlayerSessionAckWatchdog? watchdog;
            if (playerActiveWatchdogDict.TryGetValue(playerId, out watchdog)) {
                watchdog.Kick();
            }
        }

        // I've been seeking a totally lock-free approach for this whole operation for a long time, but still it's safer to keep using "mutex inputBufferLock"... 
        Interlocked.Exchange(ref player.AckingFrameId, ackingFrameId);
        Interlocked.Exchange(ref player.AckingInputFrameId, ackingInputFrameId);

        //_logger.LogInformation("OnBattleCmdReceived-inputBufferLock about to lock: roomId={0}, fromPlayerId={1}", id, playerId);
        inputBufferLock.WaitOne();
        try {
            var inputBufferSnapshot = markConfirmationIfApplicable(inputFrameUpsyncBatch, playerId, player, fromUDP);
            if (null != inputBufferSnapshot) {
                downsyncToAllPlayers(inputBufferSnapshot);
            }
        } finally {
            inputBufferLock.ReleaseMutex();
            //_logger.LogInformation("OnBattleCmdReceived-inputBufferLock unlocked: roomId={0}, fromPlayerId={1}", id, playerId);
        }
    }

    private InputBufferSnapshot? markConfirmationIfApplicable(Pbc.RepeatedField<InputFrameUpsync> inputFrameUpsyncBatch, int playerId, Player player, bool fromUDP) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        // Step#1, put the received "inputFrameUpsyncBatch" into "inputBuffer"
        foreach (var inputFrameUpsync in inputFrameUpsyncBatch) {
            var clientInputFrameId = inputFrameUpsync.InputFrameId;
            if (clientInputFrameId < inputBuffer.StFrameId) {
                // The updates to "inputBuffer.StFrameId" is monotonically increasing, thus if "clientInputFrameId < inputBuffer.StFrameId" at any moment of time, it is obsolete in the future.
                _logger.LogInformation("Omitting obsolete inputFrameUpsync#1: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat());
                continue;
            }
            if (clientInputFrameId < player.LastConsecutiveRecvInputFrameId) {
                // [WARNING] It's important for correctness that we use "player.LastConsecutiveRecvInputFrameId" instead of "lastIndividuallyConfirmedInputFrameId[player.JoinIndex-1]" here!
                //_logger.LogInformation("Omitting obsolete inputFrameUpsync#2: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}, playerLastConsecutiveRecvInputFrameId={5}", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat(), player.LastConsecutiveRecvInputFrameId);
                continue;
            }
            if (clientInputFrameId < lastAllConfirmedInputFrameId) {
                //_logger.LogInformation("Omitting obsolete inputFrameUpsync#3: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat());
                continue;
            }
            if (clientInputFrameId > inputBuffer.EdFrameId) {
                _logger.LogWarning("Dropping too advanced inputFrameUpsync#1: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}; is this player cheating?", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat());
                continue;
            }
            // by now "clientInputFrameId <= inputBuffer.EdFrameId"
            var targetInputFrameDownsync = getOrPrefabInputFrameDownsync(clientInputFrameId);
            targetInputFrameDownsync.InputList[player.CharacterDownsync.JoinIndex - 1] = inputFrameUpsync.Encoded;
            targetInputFrameDownsync.ConfirmedList = (targetInputFrameDownsync.ConfirmedList | (1UL << (player.CharacterDownsync.JoinIndex - 1)));

            if (false == fromUDP) {
                /**
				  [WARNING] We have to distinguish whether or not the incoming batch is from UDP here, otherwise "pR.LatestPlayerUpsyncedInputFrameId - pR.LastAllConfirmedInputFrameId" might become unexpectedly large in case of "UDP packet loss + slow ws session"!

				  Moreover, only ws session upsyncs should advance "player.LastConsecutiveRecvInputFrameId" & "latestPlayerUpsyncedInputFrameId".

				  Kindly note that the updates of "player.LastConsecutiveRecvInputFrameId" could be discrete before and after reconnection.
				 */
                player.LastConsecutiveRecvInputFrameId = clientInputFrameId;
                if (clientInputFrameId > latestPlayerUpsyncedInputFrameId) {
                    latestPlayerUpsyncedInputFrameId = clientInputFrameId;
                }
            }

            if (clientInputFrameId > lastIndividuallyConfirmedInputFrameId[player.CharacterDownsync.JoinIndex - 1]) {
                // No need to update "lastIndividuallyConfirmedInputFrameId[player.CharacterDownsync.JoinIndex-1]" only when "true == fromUDP", we should keep "lastIndividuallyConfirmedInputFrameId[player.CharacterDownsync.JoinIndex-1] >= player.LastConsecutiveRecvInputFrameId" at any moment.
                lastIndividuallyConfirmedInputFrameId[player.CharacterDownsync.JoinIndex - 1] = clientInputFrameId;
                // It's safe (in terms of getting an eventually correct "RenderFrameBuffer") to put the following update of "lastIndividuallyConfirmedInputList" which is ONLY used for prediction in "inputBuffer" out of "false == fromUDP" block.
                lastIndividuallyConfirmedInputList[player.CharacterDownsync.JoinIndex - 1] = inputFrameUpsync.Encoded;
            }
        }

        // Step#2, mark confirmation without forcing
        int newAllConfirmedCount = 0;
        int inputFrameId1 = lastAllConfirmedInputFrameId + 1;
        ulong allConfirmedMask = (1UL << capacity) - 1;

        for (int inputFrameId = inputFrameId1; inputFrameId < inputBuffer.EdFrameId; inputFrameId++) {
            // See comments for the traversal above.
            if (inputFrameId < inputBuffer.StFrameId) {
                continue;
            }
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId);
            if (false == res1 || null == inputFrameDownsync) {
                throw new ArgumentException(String.Format("inputFrameId={0} doesn't exist for roomId={1}: lastAllConfirmedInputFrameId={2}, inputFrameId1={3}, inputBuffer.StFrameId={4}, inputBuffer.EdFrameId={5}", inputFrameId, id, lastAllConfirmedInputFrameId, inputFrameId1, inputBuffer.StFrameId, inputBuffer.EdFrameId));
            }
            bool shouldBreakConfirmation = false;

            if (allConfirmedMask != inputFrameDownsync.ConfirmedList) {
                //_logger.LogInformation("Found a non-all-confirmed inputFrame for roomId={0}, upsync player(id:{1}, joinIndex:{2}) while checking inputFrameId=[{3}, {4}) inputFrameId={5}, confirmedList={6}", id, playerId, player.CharacterDownsync.JoinIndex, inputFrameId1, inputBuffer.EdFrameId, inputFrameId, inputFrameDownsync.ConfirmedList);
                foreach (var thatPlayer in playersArr) {
                    var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
                    var thatPlayerJoinMask = (1UL << (thatPlayer.CharacterDownsync.JoinIndex - 1));
                    bool isSlowTicker = (0 == (inputFrameDownsync.ConfirmedList & thatPlayerJoinMask));
                    bool isActiveSlowTicker = (isSlowTicker && PLAYER_BATTLE_STATE_ACTIVE == thatPlayerBattleState);
                    if (isActiveSlowTicker) {
                        shouldBreakConfirmation = true; // Could be an `ACTIVE SLOW TICKER` here, but no action needed for now
                        break;
                    }
                    /*
                    if (isSlowTicker) {
                        _logger.LogInformation("markConfirmationIfApplicable for roomId={0}, skipping UNCONFIRMED BUT INACTIVE player(id:{1}, joinIndex:{2}) while checking inputFrameId=[{3}, {4})", id, thatPlayer.CharacterDownsync.Id, thatPlayer.CharacterDownsync.JoinIndex, inputFrameId1, inputBuffer.EdFrameId);
                    }
                    */
                }
            }

            if (shouldBreakConfirmation) {
                break;
            }
            newAllConfirmedCount += 1;
            inputFrameDownsync.ConfirmedList = allConfirmedMask;
            onInputFrameDownsyncAllConfirmed(inputFrameDownsync, INVALID_DEFAULT_PLAYER_ID);
        }

        if (0 < newAllConfirmedCount) {
            /**
			[WARNING]

			If "inputBufferLock" was previously held by "doBattleMainLoopPerTickBackendDynamicsWithProperLocking", then "snapshotStFrameId" would be just (LastAllConfirmedInputFrameId - newAllConfirmedCount).

			However if "inputBufferLock" was previously held by another "OnBattleCmdReceived", the proper value for "snapshotStFrameId" might be smaller than (pR.LastAllConfirmedInputFrameId - newAllConfirmedCount) -- but why? Especially when we've already wrapped this whole function in "inputBufferLock", the order of "markConfirmationIfApplicable" generated snapshots is preserved for sending, isn't (LastAllConfirmedInputFrameId - newAllConfirmedCount) good enough here?

			Unfortunately no, for a reconnected player to get recovered asap (of course with BackendDynamicsEnabled), we put a check of READDED_BATTLE_COLLIDER_ACKED in "downsyncToSinglePlayerAsync" -- which could be called right after "markConfirmationIfApplicable" yet without going through "forceConfirmationIfApplicable" -- and if a READDED_BATTLE_COLLIDER_ACKED player is found there we need a proper "(refRenderFrameId, snapshotStFrameId)" pair for that player!
			*/
            int snapshotStFrameId = (lastAllConfirmedInputFrameId - newAllConfirmedCount);
            if (backendDynamicsEnabled) {
                int refRenderFrameIdIfNeeded = curDynamicsRenderFrameId - 1;
                int refSnapshotStFrameId = ConvertToDelayedInputFrameId(refRenderFrameIdIfNeeded);
                if (refSnapshotStFrameId < snapshotStFrameId) {
                    snapshotStFrameId = refSnapshotStFrameId;
                }
            }
            //_logger.LogInformation("markConfirmationIfApplicable for roomId={0} returning newAllConfirmedCount={1}, snapshotStFrameId={2}, snapshotEdFrameId={3}", id, newAllConfirmedCount, snapshotStFrameId, lastAllConfirmedInputFrameId + 1);
            return produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(0, snapshotStFrameId, lastAllConfirmedInputFrameId + 1);
        } else {
            return null;
        }
    }

    private InputFrameDownsync getOrPrefabInputFrameDownsync(int inputFrameId) {
        /*
           [WARNING] This function MUST BE called while "inputBufferLock" is locked.
         */
        //_logger.LogInformation("getOrPrefabInputFrameDownsync#1 for roomId={0}, inputFrameId={1}", id, inputFrameId);
        var (res1, currInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId);
        var (_, prevInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId-1);
        //_logger.LogInformation("getOrPrefabInputFrameDownsync#2 for roomId={0}, inputFrameId={1}: res1={2}", id, inputFrameId, res1);

        if (false == res1 || null == currInputFrameDownsync) {
            while (null == currInputFrameDownsync || inputBuffer.EdFrameId <= inputFrameId) {
                int gapInputFrameId = inputBuffer.EdFrameId;
                inputBuffer.DryPut();
                var (ok, ifdHolder) = inputBuffer.GetByFrameId(gapInputFrameId);
                if (!ok || null == ifdHolder) {
                    throw new ArgumentNullException(String.Format("inputBuffer was not fully pre-allocated for gapInputFrameId={0}! Now inputBuffer: {1}", gapInputFrameId, inputBuffer.ToString()));
                }
                ifdHolder.InputFrameId = gapInputFrameId;
                /*
                   [WARNING] Don't reference "inputBuffer.GetByFrameId(gapInputFrameId-1)" to prefab here!

                   Otherwise if an ActiveSlowTicker got a forced confirmation sequence like
                   ```
                   inputFrame#42    {dx: -2} upsynced;
                   inputFrame#43-50 {dx: +2} ignored by [type#1 forceConfirmation];
                   inputFrame#51    {dx: +2} upsynced;
                   inputFrame#52-60 {dx: +2} ignored by [type#1 forceConfirmation];
                   inputFrame#61    {dx: +2} upsynced;

                   ...there would be more [type#1 forceConfirmation]s for this ActiveSlowTicker if it doesn't catch up the upsync pace...
                   ```
                   , the backend might've been prefabbing TOO QUICKLY and thus still replicating "inputFrame#42" by now for this ActiveSlowTicker, making its graphics inconsistent upon "[type#1 forceConfirmation] at inputFrame#52-60", i.e. as if always dragged to the left while having been controlled to the right for a few frames -- what's worse, the same graphical inconsistence could even impact later "[type#1 forceConfirmation]s" if this ActiveSlowTicker doesn't catch up with the upsync pace!
                 */
                for (int i = 0; i < capacity; i++) {
                    // [WARNING] The use of "inputBufferLock" guarantees that by now "inputFrameId >= inputBuffer.EdFrameId >= latestPlayerUpsyncedInputFrameId", thus it's safe to use "lastIndividuallyConfirmedInputList" for prediction.
                    // Don't predict "btnB"!
                    if (null != prevInputFrameDownsync && 0 < (prevInputFrameDownsync.InputList[i] & 16UL) && JUMP_HOLDING_INPUT_FRAME_ID_GAP > gapInputFrameId - lastIndividuallyConfirmedInputFrameId[i]) {
                        ifdHolder.InputList[i] = ((lastIndividuallyConfirmedInputList[i] & 31UL));
                    } else {
                        ifdHolder.InputList[i] = ((lastIndividuallyConfirmedInputList[i] & 15UL));
                    }
                }
                ifdHolder.ConfirmedList = 0;
                currInputFrameDownsync = ifdHolder; // make sure that we return a pointer inside the inputBuffer for later writing
            }
            return currInputFrameDownsync;
        } else {
            return currInputFrameDownsync;
        }
    }

    private void onInputFrameDownsyncAllConfirmed(InputFrameDownsync inputFrameDownsync, int playerId) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        int inputFrameId = inputFrameDownsync.InputFrameId;
        if (MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED == lastAllConfirmedInputFrameIdWithChange || false == shared.Battle.EqualInputLists(inputFrameDownsync.InputList, lastAllConfirmedInputList)) {
            /*
            if (INVALID_DEFAULT_PLAYER_ID == playerId) {
                _logger.LogInformation("Key inputFrame change: roomId={0}, newInputFrameId={1}, lastInputFrameId={2}, newInputList={2}, lastAllConfirmedInputList={3}", id, inputFrameId, lastAllConfirmedInputFrameId, inputFrameDownsync.InputList, lastAllConfirmedInputList);
            } else {
                _logger.LogInformation("Key inputFrame change: roomId={0}, playerId={1}, newInputFrameId={2}, lastInputFrameId={3}, newInputList={4}, lastAllConfirmedInputList={5}", id, playerId, inputFrameId, lastAllConfirmedInputFrameId, inputFrameDownsync.InputList, lastAllConfirmedInputList);
            }
            */
            lastAllConfirmedInputFrameIdWithChange = inputFrameId;
        }
        lastAllConfirmedInputFrameId = inputFrameId;
        for (int i = 0; i < capacity; i++) {
            lastAllConfirmedInputList[i] = inputFrameDownsync.InputList[i];
        }
        /*
        if (INVALID_DEFAULT_PLAYER_ID == playerId) {
            _logger.LogInformation("inputFrame lifecycle#2[forced-allconfirmed]: roomId={0}, inputFrameId={1}", id, inputFrameId);
        } else {
            _logger.LogInformation("inputFrame lifecycle#2[allconfirmed]: roomId={0}, playerId={1}, inputFrameId={2}", id, playerId, inputFrameId);
        }
        */
    }

    private InputBufferSnapshot? produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(ulong unconfirmedMask, int snapshotStFrameId, int snapshotEdFrameId) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        int refRenderFrameIdIfNeeded = curDynamicsRenderFrameId - 1;
        if (backendDynamicsEnabled && 0 > refRenderFrameIdIfNeeded) {
            return null;
        }
        // Duplicate downsynced inputFrameIds will be filtered out by frontend.
        // [WARNING] As this snapshot is to be sent via network, I'm not sure whether there's a more memory efficient way, i.e. to AVOID creating "a clone of [snapshotStFrameId, snapshotEdFrameId) of the inputBuffer".  
        var ret = new InputBufferSnapshot {
            RefRenderFrameId = refRenderFrameIdIfNeeded,
            UnconfirmedMask = unconfirmedMask,
        };
        ret.ToSendInputFrameDownsyncs.AddRange(cloneInputBuffer(snapshotStFrameId, snapshotEdFrameId));
        return ret;
    }

    private Pbc.RepeatedField<InputFrameDownsync> cloneInputBuffer(int stFrameId, int edFrameId) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        var cloned = new Pbc.RepeatedField<InputFrameDownsync>();
        bool prevFrameFound = false;
        int j = stFrameId;
        while (j < edFrameId) {
            var (res1, foo) = inputBuffer.GetByFrameId(j);
            if (!res1 || null == foo) {
                if (false == prevFrameFound) {
                    j++;
                    continue; // allowed to keep not finding the requested inputFrames at the beginning
                } else {
                    break; // The "id"s are always consecutive
                }
            }
            prevFrameFound = true;

            var bar = foo.Clone();
            cloned.Add(bar);
            j++;
        }

        return cloned;
    }

    public void broadcastPeerUdpAddrList(int forJoinIndex) {
        _logger.LogInformation("`broadcastPeerUdpAddrList` for roomId={0}, forJoinIndex={1}, now peerUdpAddrList={2}", id, forJoinIndex, peerUdpAddrList);
        foreach (var (playerId, player) in players) {
            _ = sendSafelyAsync(null, null, peerUdpAddrList, DOWNSYNC_MSG_ACT_PEER_UDP_ADDR, playerId, player, forJoinIndex); // [WARNING] It would not switch immediately to another thread for execution, but would yield CPU upon the blocking I/O operation, thus making the current thread non-blocking. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.   
        }
    }

    private void downsyncToAllPlayers(InputBufferSnapshot? inputBufferSnapshot) {
        /*
        [WARNING] 
        
        This function MUST BE called while "pR.inputBufferLock" is LOCKED to **preserve the order of generation of "inputBufferSnapshot" for sending** -- see comments in "OnBattleCmdReceived" and [this issue](https://github.com/genxium/DelayNoMore/issues/12).
        */

        if (null == inputBufferSnapshot) {
            _logger.LogWarning("inputBufferSnapshot is null when calling downsyncToAllPlayers for (roomId: {0})", id);
            return;
        }
        
        if (true == backendDynamicsEnabled) {
            /*
            [WARNING] The comment of this part in Golang version is obsolete. The field "ForceAllResyncOnAnyActiveSlowTicker" is always true, and setting "ShouldForceResync = true" here is only going to impact unconfirmed players on frontend, i.e. there's a filter on frontend to ignore "nonSelfForceConfirmation". 
            */
            if (0 < inputBufferSnapshot.UnconfirmedMask) {
                inputBufferSnapshot.ShouldForceResync = true;
            } else {
                foreach (var player in playersArr) {
                    var playerBattleState = Interlocked.Read(ref player.BattleState);
                    if (PLAYER_BATTLE_STATE_READDED_BATTLE_COLLIDER_ACKED == playerBattleState) {
                        inputBufferSnapshot.ShouldForceResync = true;
                        break;
                    }
                }
            }
        }

        ArraySegment<byte> content = allocBytesFromInputBufferSnapshot(inputBufferSnapshot); // [WARNING] To avoid thread-safety issues when accessing "renderBuffer.GetByFrameId(...)" as well as to reduce memory redundancy
        int refRenderFrameId = inputBufferSnapshot.RefRenderFrameId;
        var toSendInputFrameIdSt = (null == inputBufferSnapshot.ToSendInputFrameDownsyncs || 0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) ? TERMINATING_INPUT_FRAME_ID : inputBufferSnapshot.ToSendInputFrameDownsyncs[0].InputFrameId;
        var toSendInputFrameIdEd = (null == inputBufferSnapshot.ToSendInputFrameDownsyncs || 0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) ? TERMINATING_INPUT_FRAME_ID : inputBufferSnapshot.ToSendInputFrameDownsyncs[inputBufferSnapshot.ToSendInputFrameDownsyncs.Count - 1].InputFrameId + 1;

        if (FRONTEND_WS_RECV_BYTELENGTH < content.Count) {
            _logger.LogWarning(String.Format("[content too big!] refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, renderFrameId={4}, curDynamicsRenderFrameId={5}: contentByteLength={6} > FRONTEND_WS_RECV_BYTELENGTH={7}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, renderFrameId, curDynamicsRenderFrameId, content.Count, FRONTEND_WS_RECV_BYTELENGTH));
        }

        foreach (var (playerId, player) in players) {
            var playerBattleState = Interlocked.Read(ref player.BattleState);

            switch (playerBattleState) {
                case PLAYER_BATTLE_STATE_DISCONNECTED:
                case PLAYER_BATTLE_STATE_LOST:
                case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                case PLAYER_BATTLE_STATE_READDED_PENDING_BATTLE_COLLIDER_ACK:
                    continue;
            }

            // Method "downsyncToAllPlayers" is called very frequently during active battle, thus deliberately tweaked websocket downsync sending for better throughput.
            BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>? playerWsDownsyncQue;
            if (!playerWsDownsyncQueDict.TryGetValue(player.CharacterDownsync.Id, out playerWsDownsyncQue)) {
                _logger.LogWarning("playerWsDownsyncQue for (roomId: {0}, playerId: {1}) doesn't exist! #3", id, playerId);
                return;
            }

            playerWsDownsyncQue.Add((content, inputBufferSnapshot));
        }

        /*
        if (backendDynamicsEnabled && shouldResync) {
            _logger.LogInformation(String.Format("[resync] Sent refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, renderFrameId={4}, curDynamicsRenderFrameId={5}: contentByteLength={6}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, renderFrameId, curDynamicsRenderFrameId, content.Count));
        } else {
            _logger.LogInformation(String.Format("[ipt-sync] Sent refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, renderFrameId={4}, curDynamicsRenderFrameId={5}: contentByteLength={6}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, renderFrameId, curDynamicsRenderFrameId, content.Count));
        }
        */
    }

    private ArraySegment<byte> allocBytesFromInputBufferSnapshot(InputBufferSnapshot inputBufferSnapshot) {
        /*
        [WARNING] This function MUST BE called while "pR.inputBufferLock" is LOCKED such that "renderBuffer.GetByFrameId(...)" is synchronized across different threads!
        */
        var (ok1, refRenderFrame) = renderBuffer.GetByFrameId(inputBufferSnapshot.RefRenderFrameId);
        if (!ok1 || null == refRenderFrame) {
            throw new ArgumentNullException(String.Format("allocBytesFromInputBufferSnapshot-Required refRenderFrameId={0} for (roomId={1}, renderFrameId={2}) doesn't exist! inputBuffer={3}, renderBuffer={4}", inputBufferSnapshot.RefRenderFrameId, id, renderFrameId, inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat()));
        }
        if (refRenderFrame.Id != inputBufferSnapshot.RefRenderFrameId) {
            throw new ArgumentException(String.Format("allocBytesFromInputBufferSnapshot-Required refRenderFrameId={0} for (roomId={1}, renderFrameId={2}) but got refRenderFrame.Id={5}! inputBuffer={3}, renderBuffer={4}", inputBufferSnapshot.RefRenderFrameId, id, renderFrameId, inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat(), refRenderFrame.Id));
        }

        refRenderFrame.ShouldForceResync = inputBufferSnapshot.ShouldForceResync;
        refRenderFrame.BackendUnconfirmedMask = inputBufferSnapshot.UnconfirmedMask;

        var resp = new WsResp {
            Ret = ErrCode.Ok,
            Act = ((backendDynamicsEnabled && refRenderFrame.ShouldForceResync) ? DOWNSYNC_MSG_ACT_FORCED_RESYNC : DOWNSYNC_MSG_ACT_INPUT_BATCH),
            Rdf = ((backendDynamicsEnabled && refRenderFrame.ShouldForceResync) ? refRenderFrame : null),
            PeerJoinIndex = MAGIC_JOIN_INDEX_DEFAULT
        };
        if (null != inputBufferSnapshot.ToSendInputFrameDownsyncs) {
            resp.InputFrameDownsyncBatch.AddRange(inputBufferSnapshot.ToSendInputFrameDownsyncs);
        }

        return new ArraySegment<byte>(resp.ToByteArray());
    }

    private async Task sendSafelyAsync(RoomDownsyncFrame? roomDownsyncFrame, RepeatedField<InputFrameDownsync>? toSendInputFrameDownsyncs, RepeatedField<PeerUdpAddr>? peerUdpAddrList, int act, int playerId, Player player, int peerJoinIndex) {
        var thatPlayerBattleState = Interlocked.Read(ref player.BattleState); // Might be changed in "OnPlayerDisconnected/OnPlayerLost" from other threads

        // [WARNING] DON'T try to send any message to an inactive player!
        switch (thatPlayerBattleState) {
            case PLAYER_BATTLE_STATE_DISCONNECTED:
            case PLAYER_BATTLE_STATE_LOST:
            case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
            case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                return;
        }

        WebSocket? wsSession;
        if (!playerDownsyncSessionDict.TryGetValue(playerId, out wsSession)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #1", id, playerId);
            return;
        }

        if (!playerSignalToCloseDict.TryGetValue(playerId, out (CancellationTokenSource c1, CancellationToken c2) cancellationSignal)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #2", id, playerId);
            return;
        }

        var resp = new WsResp {
            Ret = ErrCode.Ok,
            Act = act,
            Rdf = roomDownsyncFrame,
            PeerJoinIndex = peerJoinIndex
        };
        if (null != peerUdpAddrList) {
            resp.PeerUdpAddrList.AddRange(peerUdpAddrList);
        }
        if (null != toSendInputFrameDownsyncs) {
            resp.InputFrameDownsyncBatch.AddRange(toSendInputFrameDownsyncs);
        }

        try {
            if (WebSocketState.Open != wsSession.State) {
              throw new ArgumentException(String.Format("sendSafelyAsync-Invalid websocket session state for (roomId: {0}, playerId: {1}): wsSession.State={2}", id, playerId, wsSession.State)); 
            }
            await wsSession.SendAsync(new ArraySegment<byte>(resp.ToByteArray()), WebSocketMessageType.Binary, true, cancellationSignal.c2).WaitAsync(DEFAULT_BACK_TO_FRONT_WS_WRITE_TIMEOUT);
        } catch (Exception ex) {
            _logger.LogError(ex, "Exception occurred during sendSafelyAsync to (roomId: {0}, playerId: {1})", id, playerId);
            if (!cancellationSignal.c1.IsCancellationRequested) cancellationSignal.c1.Cancel();
        }
    }

    private bool initUdpClient() {
        bool success = true;
        try {
            battleUdpTunnel = new UdpClient(port: 0);
            if (null != battleUdpTunnel && null != battleUdpTunnel.Client.LocalEndPoint) {
                var tunnelIpEndpoint = (IPEndPoint)battleUdpTunnel.Client.LocalEndPoint;
                battleUdpTunnelAddr = new PeerUdpAddr {
                    Port = tunnelIpEndpoint.Port
                };

                // initialize "peerUdpAddrBroadcastRdf" 
                peerUdpAddrList = new RepeatedField<PeerUdpAddr> {
                battleUdpTunnelAddr // i.e. "MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL == 0"
            };
                for (int i = 0; i < capacity; i++) {
                    // Prefill with invalid addrs
                    peerUdpAddrList.Add(new PeerUdpAddr { });
                }
                success = true;
            }
        } catch (Exception ex) {
            _logger.LogError(ex, "Error creating udp client for roomId={0}", id);
            success = false;
        }

        if (!success) {
            _logger.LogWarning("Disposing `battleUdpTunnel` early for roomId={0} due to non-success of initUdpClient", id);
            if (null != battleUdpTunnelAddr) {
                battleUdpTunnelAddr = null;
            } 
            if (null != battleUdpTunnel) {
                battleUdpTunnel.Close();
                battleUdpTunnel.Dispose();
                battleUdpTunnel = null;
            }
            _logger.LogWarning("Disposed `battleUdpTunnel` early for roomId={0} due to non-success of initUdpClient", id);
        }
        return true;
    }

    private async Task startBattleUdpTunnelAsyncTask() {
        if (null == battleUdpTunnel) {
            _logger.LogWarning("Returning `startBattleUdpTunnelAsyncTask` early#1 for roomId={0} due to null `battleUdpTunnel`", id);
            return;
        }

        if (null == battleUdpTunnelAddr) {
            _logger.LogWarning("Returning `startBattleUdpTunnelAsyncTask` early#2 for roomId={0} due to null `battleUdpTunnelAddr`", id);
            return;
        }

        _logger.LogInformation("`battleUdpTask` starting for roomId={0}", id);

        battleUdpTunnelCancellationTokenSource = new CancellationTokenSource(); // [WARNING] Will be disposed along with "battleUdpTask".
        CancellationToken battleUdpTunnelCancellationToken = battleUdpTunnelCancellationTokenSource.Token;
        try {
            if (null == battleUdpTunnelAddr) {
                // The "finally" block would help close "battleUdpTunnel".
                _logger.LogWarning("`battleUdpTask` failed to start#2 for roomId={0}: unable to obtain `battleUdpTunnelAddr`", id);
                return;
            }

            _logger.LogInformation("`battleUdpTask` started for roomId={0} @ now peerUdpAddrList={1}", id, peerUdpAddrList);

            while (!battleUdpTunnelCancellationTokenSource.IsCancellationRequested) {
                var recvResult = await battleUdpTunnel.ReceiveAsync(battleUdpTunnelCancellationToken);
                WsReq pReq = WsReq.Parser.ParseFrom(recvResult.Buffer);
                // _logger.LogInformation("`battleUdpTunnel` received for roomId={0}: pReq={1}", id, pReq);
                int playerId = pReq.PlayerId;
                Player? player;
                if (players.TryGetValue(playerId, out player)) {
                    int reqAuthKey = pReq.AuthKey;
                    if (reqAuthKey != player.BattleUdpTunnelAuthKey) {
                        continue;
                    }
                }

                if (null == player) {
                    _logger.LogWarning("In `battleUdpTask`, player for (roomId: {0}, playerId: {1}) doesn't exist!", id, playerId);
                    continue;
                }

                if (UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL == pReq.Act && null == player.BattleUdpTunnelAddr) {
                    player.BattleUdpTunnelAddr = recvResult.RemoteEndPoint;
                    peerUdpAddrList[player.CharacterDownsync.JoinIndex] = new PeerUdpAddr {
                        Ip = recvResult.RemoteEndPoint.Address.ToString(),
                        Port = recvResult.RemoteEndPoint.Port
                    };
                    _logger.LogInformation("`battleUdpTask` for roomId={0} updated udp addr for playerId={1} to be {2}", id, playerId, peerUdpAddrList[player.CharacterDownsync.JoinIndex]);
                    // Need broadcast to all, including the current "pReq.PlayerId", to favor p2p holepunching
                    broadcastPeerUdpAddrList(player.CharacterDownsync.JoinIndex);

                    // [WARNING] Don't forward "holepunching to server" to other players
                    continue;
                }

                var thatPlayerBattleState = Interlocked.Read(ref player.BattleState);
                if (PLAYER_BATTLE_STATE_ACTIVE != thatPlayerBattleState) {
                    //_logger.LogWarning("In `battleUdpTunnel`, player for (roomId: {0}, playerId: {1}) is {2}, rejecting its UDP upsync!", id, playerId, thatPlayerBattleState);
                    continue;
                }

                var batch = pReq.InputFrameUpsyncBatch;
                if (null != batch && 0 < batch.Count) {
                    var peerJoinIndex = pReq.JoinIndex;
                    // Broadcast to every other player in the same room/battle
                    // TODO: Can I apply some early filters ("inputFrameId" v.s. "lastAllConfirmedInputFrameId" or "inputBuffer.StFrameId" or "inputBuffer.EdFrameId") here like those at the very beginning of "markConfirmationIfApplicable(...)"?
                    foreach (var (otherPlayerId, otherPlayer) in players) {
                        if (otherPlayerId == playerId) {
                            continue;
                        }
                        if (null == otherPlayer.BattleUdpTunnelAddr) {
                            continue;
                        }
                        _ = battleUdpTunnel.SendAsync(new ReadOnlyMemory<byte>(recvResult.Buffer), otherPlayer.BattleUdpTunnelAddr); // [WARNING] It would not switch immediately to another thread for execution, but would yield CPU upon the blocking I/O operation, thus making the current thread non-blocking. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.
                    }
                    OnBattleCmdReceived(pReq, playerId, true);
                    /*
                    [WARNING] Different from frontend concerns, it's actually safe to update "ifd.ConfirmedList" (where "ifd" belongs to the "inputBuffer") by an UDP inputFrameUpsync, as long as all updates to "ifd.ConfirmedList" and "room.lastAllConfirmedInputFrameId" are guarded by "inputBufferLock" -- hence in Golang version, both "markConfirmationIfApplicable" and "forceConfirmationIfApplicable" are guarded by "inputBufferLock".
                    */
                }
            }
        } catch (OperationCanceledException ocEx) {
            _logger.LogWarning("`battleUdpTask` is interrupted by OperationCanceledException for roomId={0}, ocEx.Message={1}", id, ocEx.Message);
        } catch (ObjectDisposedException ex) {
            _logger.LogWarning("`battleUdpTask` is interrupted by ObjectDisposedException for roomId={0}, ex.Message={1}", id, ex.Message);
        } catch (Exception ex) {
            _logger.LogError(ex, "`battleUdpTask` is interrupted by unexpected exception for roomId={0}", id);
        } finally {
            try {
                _logger.LogInformation("Closing `battleUdpTunnel` for roomId={0}", id);
                battleUdpTunnel.Close();
                _logger.LogInformation("Closed `battleUdpTunnel` for roomId={0}, now disposing battleUdpTunnel", id);
                battleUdpTunnel.Dispose();
                _logger.LogInformation("Disposed `battleUdpTunnel` for roomId={0}", id);
                battleUdpTunnel = null;
                battleUdpTunnelAddr = null;
                peerUdpAddrList = null;
            } catch (Exception ex) {
                _logger.LogError(ex, "Closing of `battleUdpTunnel` is interrupted by unexpected exception for roomId={0}", id);
            }
        }

        _logger.LogInformation("`battleUdpTask` stopped for (roomId={0})@renderFrameId={1}", id, renderFrameId);
    }

    private void doBattleMainLoopPerTickBackendDynamicsWithProperLocking(int prevRenderFrameId, ref ulong dynamicsDuration) {
        inputBufferLock.WaitOne();
        try {
            var (ok, thatRenderFrameId) = ShouldPrefabInputFrameDownsync(prevRenderFrameId, renderFrameId);
            if (ok) {
                int noDelayInputFrameId = ConvertToNoDelayInputFrameId(thatRenderFrameId);
                getOrPrefabInputFrameDownsync(noDelayInputFrameId);
            }

            // Force setting all-confirmed of buffered inputFrames periodically, kindly note that if "backendDynamicsEnabled", what we want to achieve is "recovery upon reconnection", which certainly requires "forceConfirmationIfApplicable" to move "lastAllConfirmedInputFrameId" forward as much as possible
            int oldLastAllConfirmedInputFrameId = lastAllConfirmedInputFrameId;
            ulong unconfirmedMask = forceConfirmationIfApplicable();

            if (0 <= lastAllConfirmedInputFrameId) {
                // Apply "all-confirmed inputFrames" to move forward "curDynamicsRenderFrameId"
                int nextDynamicsRenderFrameId = ConvertToLastUsedRenderFrameId(lastAllConfirmedInputFrameId) + 1;
                multiStep(curDynamicsRenderFrameId, nextDynamicsRenderFrameId);
            }

            /*
               [WARNING]

               It's critical to create the snapshot AFTER "multiStep" for `ACTIVE SLOW TICKER` to avoid lag avalanche (see `<proj-root>/ConcerningEdgeCases.md` for introduction).

               Consider that in a 4-player battle, player#1 is once disconnected but soon reconnected in 2 seconds, during its absence, "markConfirmationIfApplicable" would skip it and increment "LastAllConfirmedInputFrameId" and when backend is sending "DOWNSYNC_MSG_ACT_FORCED_RESYNC" it'd be always based on "LatestPlayerUpsyncedInputFrameId == LastAllConfirmedInputFrameId" thus NOT triggering "[type#1 forceConfirmation]".

               However, if player#1 remains connected but ticks very slowly (i.e. an "ACTIVE SLOW TICKER"), "markConfirmationIfApplicable" couldn't increment "LastAllConfirmedInputFrameId", thus "[type#1 forceConfirmation]" will be triggered, but what's worse is that after "[type#1 forceConfirmation]" if the "refRenderFrameId" is not advanced enough, player#1 could never catch up even if it resumed from slow ticking!
            */

            if (0 < unconfirmedMask) {
                // [WARNING] As "curDynamicsRenderFrameId" was just incremented above, "refSnapshotStFrameId" is most possibly larger than "oldLastAllConfirmedInputFrameId + 1", therefore this initial assignment is critical for `ACTIVE NORMAL TICKER`s to receive consecutive ids of inputFrameDownsync.
                int snapshotStFrameId = oldLastAllConfirmedInputFrameId + 1;
                int refSnapshotStFrameId = ConvertToDelayedInputFrameId(curDynamicsRenderFrameId - 1);
                if (refSnapshotStFrameId < snapshotStFrameId) {
                    snapshotStFrameId = refSnapshotStFrameId;
                }
                var inputsBufferSnapshot = produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(unconfirmedMask, snapshotStFrameId, lastAllConfirmedInputFrameId + 1);
                downsyncToAllPlayers(inputsBufferSnapshot);
            }
        } finally {
            inputBufferLock.ReleaseMutex();
            //_logger.LogInformation("doBattleMainLoopPerTickBackendDynamicsWithProperLocking-inputBufferLock unlocked: roomId={0}, fromPlayerId={1}", id, playerId);
        }
    }

    private ulong forceConfirmationIfApplicable() {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        int totPlayerCnt = capacity;
        ulong allConfirmedMask = ((1ul << totPlayerCnt) - 1);
        ulong unconfirmedMask = 0;
        // As "lastAllConfirmedInputFrameId" can be advanced by UDP but "latestPlayerUpsyncedInputFrameId" could only be advanced by ws session, when the following condition is met we know that the slow ticker is really in trouble!
        if (0 < latestPlayerUpsyncedInputFrameId && 0 < curDynamicsRenderFrameId && (renderFrameId - lastForceResyncedRdfId > FORCE_RESYNC_INTERVAL_THRESHOLD || renderFrameId == battleDurationFrames - FORCE_RESYNC_INTERVAL_THRESHOLD)) {
            // Type#3 forceResync regularly
            int oldLastAllConfirmedInputFrameId = lastAllConfirmedInputFrameId;
            for (int j = lastAllConfirmedInputFrameId + 1; j <= latestPlayerUpsyncedInputFrameId; j++) {
                var (res1, foo) = inputBuffer.GetByFrameId(j);
                if (!res1 || null == foo) {
                    throw new ArgumentNullException(String.Format("inputFrameId={0} doesn't exist for roomId={1}! Now inputBuffer: {2}", j, id, inputBuffer.ToString()));
                }
                foo.ConfirmedList = allConfirmedMask;
                onInputFrameDownsyncAllConfirmed(foo, INVALID_DEFAULT_PLAYER_ID);
            }
            _logger.LogInformation(String.Format("[type#3 forceConfirmation] For roomId={0}@renderFrameId={1}, curDynamicsRenderFrameId={2}, LatestPlayerUpsyncedInputFrameId:{3}, LastAllConfirmedInputFrameId:{4} -> {5}, lastForceResyncedRdfId={6}", id, renderFrameId, curDynamicsRenderFrameId, latestPlayerUpsyncedInputFrameId, oldLastAllConfirmedInputFrameId, lastAllConfirmedInputFrameId, lastForceResyncedRdfId));
            /*
            [WARNING] Only "type#1" will delay "conditional graphical update" from the force-resync snapshot for non-self-unconfirmed players, both "type#2" and "type#3" might have immediate impact on the "ACTIVE NORMAL TICKER"s, e.g. abrupt & inconsistent graphical update.  

            However, experiments show that for type#3 "curDynamicsRenderFrameId" is NEVER TOO ADVANCED compared to "OnlineMapController.playerRdfId", possibly due to the absence of any SLOW TICKER (including disconnected) -- hence when executing "OnlineMap.onRoomDownsyncFrame", the force-resync snapshot would only get "RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet", i.e. only effectively trigger "OnlineMapController.onInputFrameDownsyncBatch(accompaniedInputFrameDownsyncBatch) -> _handleIncorrectlyRenderedPrediction(...)" -- thus relatively smooth graphical updates. 
            */
            unconfirmedMask = allConfirmedMask;
            lastForceResyncedRdfId = renderFrameId;
        } else if (latestPlayerUpsyncedInputFrameId > (lastAllConfirmedInputFrameId + inputFrameUpsyncDelayTolerance + 1)) {
            // Type#1 check whether there's a significantly slow ticker among players
            int oldLastAllConfirmedInputFrameId = lastAllConfirmedInputFrameId;
            for (int j = lastAllConfirmedInputFrameId + 1; j <= latestPlayerUpsyncedInputFrameId; j++) {
                var (res1, foo) = inputBuffer.GetByFrameId(j);
                if (!res1 || null == foo) {
                    throw new ArgumentNullException(String.Format("inputFrameId={0} doesn't exist for roomId={1}! Now inputBuffer: {2}", j, id, inputBuffer.ToString()));
                }
                unconfirmedMask |= (allConfirmedMask ^ foo.ConfirmedList);
                foo.ConfirmedList = allConfirmedMask;
                onInputFrameDownsyncAllConfirmed(foo, INVALID_DEFAULT_PLAYER_ID);
            }
            if (0 < unconfirmedMask) {
                _logger.LogInformation(String.Format("[type#1 forceConfirmation] For roomId={0}@renderFrameId={1}, curDynamicsRenderFrameId={2}, LatestPlayerUpsyncedInputFrameId:{3}, LastAllConfirmedInputFrameId:{4} -> {5}, InputFrameUpsyncDelayTolerance:{6}, unconfirmedMask={7}, lastForceResyncedRdfId={8}; there's a slow ticker suspect, forcing all-confirmation", id, renderFrameId, curDynamicsRenderFrameId, latestPlayerUpsyncedInputFrameId, oldLastAllConfirmedInputFrameId, lastAllConfirmedInputFrameId, inputFrameUpsyncDelayTolerance, unconfirmedMask, lastForceResyncedRdfId));
                lastForceResyncedRdfId = renderFrameId;
            }
        } else {
            // Type#2 helps resolve the edge case when all players are disconnected temporarily
            bool shouldForceResync = false;
            foreach (var player in playersArr) {
                var playerBattleState = Interlocked.Read(ref player.BattleState);
                if (PLAYER_BATTLE_STATE_READDED_PENDING_BATTLE_COLLIDER_ACK == playerBattleState) {
                    shouldForceResync = true;
                    break;
                }
            }
            if (shouldForceResync) {
                int oldLastAllConfirmedInputFrameId = lastAllConfirmedInputFrameId;
                for (int j = lastAllConfirmedInputFrameId + 1; j <= latestPlayerUpsyncedInputFrameId; j++) {
                    var (res1, foo) = inputBuffer.GetByFrameId(j);
                    if (!res1 || null == foo) {
                        throw new ArgumentNullException(String.Format("inputFrameId={0} doesn't exist for roomId={1}! Now inputBuffer: {2}", j, id, inputBuffer.ToString()));
                    }
                    foo.ConfirmedList = allConfirmedMask;
                    onInputFrameDownsyncAllConfirmed(foo, INVALID_DEFAULT_PLAYER_ID);
                }
                _logger.LogInformation(String.Format("[type#2 forceConfirmation] For roomId={0}@renderFrameId={1}, curDynamicsRenderFrameId={2}, LatestPlayerUpsyncedInputFrameId:{3}, LastAllConfirmedInputFrameId:{4} -> {5}, lastForceResyncedRdfId={6}", id, renderFrameId, curDynamicsRenderFrameId, latestPlayerUpsyncedInputFrameId, oldLastAllConfirmedInputFrameId, lastAllConfirmedInputFrameId, lastForceResyncedRdfId));
                unconfirmedMask = allConfirmedMask;
                lastForceResyncedRdfId = renderFrameId;
            }
        }

        return unconfirmedMask;
    }

    private void multiStep(int fromRenderFrameId, int toRenderFrameId) {
        // [WARNING] This function MUST BE called while "pR.InputsBufferLock" is locked!
        if (fromRenderFrameId >= toRenderFrameId) {
            return;
        }

        bool hasIncorrectlyPredictedRenderFrame = false;
        for (var i = fromRenderFrameId; i < toRenderFrameId; i++) {
            if (frameLogEnabled) {
                int j = ConvertToDelayedInputFrameId(i);
                var (ok, delayedInputFrame) = inputBuffer.GetByFrameId(j);
                if (false == ok || null == delayedInputFrame) {
                    throw new ArgumentNullException(String.Format("Couldn't find delayedInputFrame for j={0} to log frame info", j));
                }
                rdfIdToActuallyUsedInput[i] = delayedInputFrame.Clone();
            }
            Step(inputBuffer, i, capacity, collisionSys, renderBuffer, ref overlapResult, ref primaryOverlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, residueCollided, trapLocalIdToColliderAttrs, triggerTrackingIdToTrapLocalId, completelyStaticTrapColliders, unconfirmedBattleResult, ref confirmedBattleResult, pushbackFrameLogBuffer, frameLogEnabled, TERMINATING_RENDER_FRAME_ID, false, out hasIncorrectlyPredictedRenderFrame, historyRdfHolder, justFulfilledEvtSubArr, ref justFulfilledEvtSubCnt, missionEvtSubId, MAGIC_JOIN_INDEX_INVALID, joinIndexRemap, ref justTriggeredStoryPointId, justDeadJoinIndices, loggerBridge);
            curDynamicsRenderFrameId++;
        }
    }

    ~Room() {
        joinerLock.Dispose();
        inputBufferLock.Dispose();
    }
}
