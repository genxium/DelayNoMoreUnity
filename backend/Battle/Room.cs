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
    private readonly Random _randGenerator = new Random();
    //private String[] _availableStageNames = new String[] {"ForestVersus", "CaveVersus", "FlatVersus"};
    private String[] _availableStageNames = new String[] {"FlatVersus"};
    //private String[] _availableStageNames = new String[] {"FlatVersusTraining"};

    private TimeSpan DEFAULT_BACK_TO_FRONT_WS_WRITE_TIMEOUT = TimeSpan.FromMilliseconds(5000);
    private bool type1ForceConfirmationEnabled = false;
    private bool type3ForceConfirmationEnabled = false;
    private bool insituForceConfirmationEnabled = true;
    public int id;
    public int capacity;
    public ulong allConfirmedMask; 
    public int preallocNpcCapacity = DEFAULT_PREALLOC_NPC_CAPACITY;
    public int preallocBulletCapacity = DEFAULT_PREALLOC_BULLET_CAPACITY;
    public int preallocTrapCapacity = DEFAULT_PREALLOC_TRAP_CAPACITY;
    public int preallocTriggerCapacity = DEFAULT_PREALLOC_TRIGGER_CAPACITY;
    public int preallocPickableCapacity = DEFAULT_PREALLOC_PICKABLE_CAPACITY;

    public int justTriggeredStoryPointId = 0; // Not used in backend
    public int justTriggeredBgmId = 0; // Not used in backend

    public int battleDurationFrames;
    public int elongatedBattleDurationFrames;
    public bool elongatedBattleDurationFramesShortenedOnce;
    public int estimatedMillisPerFrame;

    public string stageName;
    public int inputFrameUpsyncDelayTolerance;
    public int maxChasingRenderFramesPerUpdate;
    public bool frameLogEnabled;

    int backendTimerRdfId;
    int curDynamicsRenderFrameId;
    int lastForceResyncedRdfId;
    int nstDelayFrames;

    private int FORCE_RESYNC_INTERVAL_THRESHOLD = 5*BATTLE_DYNAMICS_FPS;

    Player[] playersArr; // ordered by joinIndex

    ILoggerBridge loggerBridge;

    int localPlayerWsDownsyncQueBattleReadTimeoutMillis = 2000; 

    int localPlayerWsDownsyncQueClearingReadTimeoutMillis = 800; // [WARNING] By reaching "clearPlayerNetworkSession(playerId)", no more elements will be enqueing "playerWsDownsyncQueDict[playerId]", yet the "playerSignalToCloseDict[playerId]" could've already been cancelled -- hence if the queue has been empty for several hundred milliseconds, we see it as truly empty. 
    public long state;
    int effectivePlayerCount;
    int participantChangeId;

    protected int missionEvtSubId;
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
    protected Dictionary<int, int> triggerEditorIdToLocalId;
    protected Dictionary<int, TriggerConfigFromTiled> triggerEditorIdToConfigFromTiled;
    protected Dictionary<int, int> joinIndexRemap;
    protected HashSet<int> justDeadJoinIndices;
    protected ulong fulfilledTriggerSetMask;

    int lastAllConfirmedInputFrameId;
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

    Dictionary<string, Player> players;
    Dictionary<string, PlayerSessionAckWatchdog> playerActiveWatchdogDict;
    int stdWatchdogKeepAliveMillis = 3500;
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
    Dictionary<string, WebSocket> playerDownsyncSessionDict;
    Dictionary<string, (CancellationTokenSource, CancellationToken)> playerSignalToCloseDict;
    Dictionary<string, BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>> playerWsDownsyncQueDict;
    Dictionary<string, Task> playerDownsyncLoopDict;
    //////////////////////////////Battle lifecycle disposables////////////////////////////////// 

    //////////////////////////////Room lifecycle disposables////////////////////////////////// 
    Mutex joinerLock;         // Guards [AddPlayerIfPossible, ReAddPlayerIfPossible, OnPlayerDisconnected, dismiss]
    Mutex inputBufferLock;         // Guards [*renderBuffer*, inputBuffer, latestPlayerUpsyncedInputFrameId, lastAllConfirmedInputFrameId, lastAllConfirmedInputList, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, player.LastConsecutiveRecvInputFrameId]
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
        allConfirmedMask = (1u << roomCapacity)-1;
        backendTimerRdfId = 0;
        stageName = _availableStageNames[_randGenerator.Next(_availableStageNames.Length)];
        curDynamicsRenderFrameId = 0;
        lastForceResyncedRdfId = 0;
        frameLogEnabled = false;
        battleDurationFrames = 0;
        elongatedBattleDurationFrames = 0;
        elongatedBattleDurationFramesShortenedOnce = false;
        estimatedMillisPerFrame = (int)Math.Ceiling(1000.0f/BATTLE_DYNAMICS_FPS); // ceiling to dilute the framerate on server 
        maxChasingRenderFramesPerUpdate = 9; // Don't set this value too high to avoid exhausting frontend CPU within a single frame, roughly as the "turn-around frames to recover" is empirically OK                                                    
        nstDelayFrames = 20;
        inputFrameUpsyncDelayTolerance = ConvertToDynamicallyGeneratedDelayInputFrameId(nstDelayFrames, 0) - 1; // this value should be strictly smaller than (NstDelayFrames >> InputScaleFrames), otherwise "type#1 forceConfirmation" might become a lag avalanche
        state = ROOM_STATE_IDLE;
        effectivePlayerCount = 0;
        backendDynamicsEnabled = true;

        rdfIdToActuallyUsedInput = new Dictionary<int, InputFrameDownsync>();
        trapLocalIdToColliderAttrs = new Dictionary<int, List<TrapColliderAttr>>();
        triggerEditorIdToLocalId = new Dictionary<int, int>();
        triggerEditorIdToConfigFromTiled = new Dictionary<int, TriggerConfigFromTiled>();
        joinIndexRemap = new Dictionary<int, int>();
        justDeadJoinIndices = new HashSet<int>();
        fulfilledTriggerSetMask = 0;
        unconfirmedBattleResult = new Dictionary<int, BattleResult>();
        historyRdfHolder = NewPreallocatedRoomDownsyncFrame(capacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocPickableCapacity);

        int renderBufferSize = getRenderBufferSize();
        if (!type1ForceConfirmationEnabled && !type3ForceConfirmationEnabled) {
            // [WARNING] If "!type1ForceConfirmationEnabled && !type3ForceConfirmationEnabled", make sure that an "extreme/malicious slow ticker" will be timed out by the watchdog before either "renderBuffer" or "inputBuffer" is drained!
            int newWatchdogKeepAliveMillis = (getRenderBufferSize()*1000/BATTLE_DYNAMICS_FPS) - 1;
            if (newWatchdogKeepAliveMillis < stdWatchdogKeepAliveMillis) {
                stdWatchdogKeepAliveMillis = newWatchdogKeepAliveMillis; 
            } 
        } 

        // Preallocate battle dynamic fields other than "Collider related" ones
        preallocateStepHolders(capacity, renderBufferSize, DEFAULT_BACKEND_INPUT_BUFFER_SIZE, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocPickableCapacity, out renderBuffer, out pushbackFrameLogBuffer, out inputBuffer, out lastIndividuallyConfirmedInputFrameId, out lastIndividuallyConfirmedInputList, out effPushbacks, out hardPushbackNormsArr, out softPushbacks, out decodedInputHolder, out prevDecodedInputHolder, out confirmedBattleResult, out softPushbackEnabled, frameLogEnabled);

        // "Collider related" fields Will be reset in "refreshCollider" anyway
        dynamicRectangleColliders = new Collider[0];
        staticColliders = new Collider[0];
        completelyStaticTrapColliders = new List<Collider>();
        collisionSys = new CollisionSpace(1, 1, 1, 1); 
        collisionHolder = new Collision();
        residueCollided = new FrameRingBuffer<Collider>(0);

        // Preallocate network management fields
        players = new Dictionary<string, Player>();
        playersArr = new Player[capacity];

        playerActiveWatchdogDict = new Dictionary<string, PlayerSessionAckWatchdog>();
        playerDownsyncSessionDict = new Dictionary<string, WebSocket>();
        playerSignalToCloseDict = new Dictionary<string, (CancellationTokenSource, CancellationToken)>();
        playerWsDownsyncQueDict = new Dictionary<string, BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>>();
        playerDownsyncLoopDict = new Dictionary<string, Task>();

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

    private int getRenderBufferSize() {
        /*
           [WARNING]

           After v1.6.4, the netcode preference is changed to "freeze normal tickers for awaiting slow tickers", therefore "type#1 forceConfirmation" can be disabled to favor smoother graphics on the "slow tickers", yet it will cost a larger "inputBuffer" on the backend capped by the interval of "type#3 forceConfirmation".
         */
        return (type1ForceConfirmationEnabled ? 96 : FORCE_RESYNC_INTERVAL_THRESHOLD);
    }

    public bool IsFull() {
        return capacity <= effectivePlayerCount;
    }

    public int GetRenderCacheSize() {
        return ((inputBuffer.N - 1) << 1);
    }

    public int AddPlayerIfPossible(Player pPlayerFromDbInit, string playerId, uint speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer, CancellationToken signalToCloseConnOfThisPlayerToken) {
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
            pPlayerFromDbInit.Id = playerId;
            pPlayerFromDbInit.CharacterDownsync.SpeciesId = speciesId;
            pPlayerFromDbInit.CharacterDownsync.InAir = true;

            pPlayerFromDbInit.BattleUdpTunnelAuthKey = _randGenerator.Next();
            players[playerId] = pPlayerFromDbInit;

            playerDownsyncSessionDict[playerId] = session;
            playerSignalToCloseDict[playerId] = (signalToCloseConnOfThisPlayer, signalToCloseConnOfThisPlayerToken);

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

            var onTickMsg = String.Format("[ roomId={0}, playerId={1}, joinIndex={2} ] session watchdog ticked.", id, playerId, pPlayerFromDbInit.CharacterDownsync.JoinIndex); 
            var newWatchdog = new PlayerSessionAckWatchdog(stdWatchdogKeepAliveMillis, OnPlayerDisconnected, playerId, onTickMsg, _loggerFactory);
            newWatchdog.Stop();
            playerActiveWatchdogDict[playerId] = newWatchdog;

            return ErrCode.Ok;
        } finally {
            joinerLock.ReleaseMutex();
        }
    }

    public (int, Player?) ReAddPlayerIfPossible(string playerId, uint speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer, CancellationToken signalToCloseConnOfThisPlayerToken) {
        joinerLock.WaitOne();
        try {
            long nowRoomState = Interlocked.Read(ref state); 
            if (ROOM_STATE_IN_BATTLE != nowRoomState) {
                _logger.LogInformation("Battle is inactive when calling `ReAddPlayerIfPossible` for (roomId={0}, playerId={1})", id, playerId);
                return (ErrCode.BattleStopped, null);
            }

            if (!players.ContainsKey(playerId)) {
                _logger.LogInformation("The active battle doesn't contain playerId when calling `ReAddPlayerIfPossible` for (roomId={0}, playerId={1})", id, playerId);
                return (ErrCode.PlayerNotFound, null);
            }

            var existingPlayer = players[playerId];
            int existingJoinIndex = existingPlayer.CharacterDownsync.JoinIndex;
            var oldPlayerBattleState = existingPlayer.BattleState;
            if (PLAYER_BATTLE_STATE_DISCONNECTED != oldPlayerBattleState) {
                _logger.LogInformation("The existingPlayer in active battle is not at required state when calling `ReAddPlayerIfPossible` for (roomId={0}, playerId={1}, joinIndex={2}, oldPlayerBattleState={3}): proactively disconnecting it", id, playerId, existingJoinIndex, oldPlayerBattleState);
                OnPlayerDisconnected(playerId);
                return (ErrCode.PlayerNotReAddableToRoom, null);
            }

            existingPlayer.BattleState = PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC;

            // [WARNING] Deliberately inheriting the same "BattleUdpTunnelAuthKey" to favor already hole-punched UDP session.

            playerDownsyncSessionDict[playerId] = session;
            playerSignalToCloseDict[playerId] = (signalToCloseConnOfThisPlayer, signalToCloseConnOfThisPlayerToken);

            var newGenOrderPreservedMsgs = new BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>();
            playerWsDownsyncQueDict[playerId] = newGenOrderPreservedMsgs;

            var t = Task.Run(async () => await downsyncToSinglePlayerAsyncLoop(playerId, existingPlayer));
            playerDownsyncLoopDict[playerId] = t;

            var onTickMsg = String.Format("[ roomId={0}, playerId={1}, joinIndex={2} ] reentry session watchdog ticked.", id, playerId, existingJoinIndex); 
            var newWatchdog = new PlayerSessionAckWatchdog(stdWatchdogKeepAliveMillis, OnPlayerDisconnected, playerId, onTickMsg, _loggerFactory);
            newWatchdog.Stop();
            playerActiveWatchdogDict[playerId] = newWatchdog;

            return (ErrCode.Ok, existingPlayer);
        } finally {
            joinerLock.ReleaseMutex();
        }
    }

    public async void OnPlayerDisconnected(string playerId) {
        bool shouldDismiss = false;
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
                        _logger.LogInformation("Room OnPlayerDisconnected[early return #1] [ roomId={0}, joinIndex={1}, playerId={2}, playerBattleState={3}, nowRoomState={4}, nowRoomEffectivePlayerCount={5} ]", id, thatPlayer.CharacterDownsync.JoinIndex, playerId, thatPlayerBattleState, state, effectivePlayerCount);
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
                        shouldDismiss = true;
                    }
                    _logger.LogInformation("OnPlayerDisconnected finished: [ roomId={0}, joinIndex={1}, playerId={2}, roomState={3}, nowRoomEffectivePlayerCount={4} ]", id, thatPlayer.CharacterDownsync.JoinIndex, playerId, state, effectivePlayerCount);
                    break;
                default:
                    Interlocked.Exchange(ref thatPlayer.BattleState, PLAYER_BATTLE_STATE_DISCONNECTED);

                    var tList = new List<Task>();
                    if (ROOM_STATE_IN_BATTLE == state) {
                        // Notify other active players of the disconnection
                        foreach (var (otherPlayerId, otherPlayer) in players) {
                            if (otherPlayerId == playerId || null == otherPlayer) continue;
                            if (PLAYER_BATTLE_STATE_ACTIVE != otherPlayer.BattleState && PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC != otherPlayer.BattleState) continue;
                            _logger.LogInformation($"OnPlayerDisconnected notifying [ roomId={id}, roomState={state}, otherPlayerJoinIndex={otherPlayer.CharacterDownsync.JoinIndex}, otherPlayerId={otherPlayerId}, nowRoomEffectivePlayerCount={effectivePlayerCount} ]");
                            tList.Add(sendSafelyAsync(null, null, null, DOWNSYNC_MSG_ACT_PLAYER_DISCONNECTED, otherPlayerId, otherPlayer, thatPlayer.CharacterDownsync.JoinIndex));
                        }
                    }
                    await Task.WhenAll(tList);

                    // [WARNING] "clearPlayerNetworkSession(playerId)" MUST BE PUT AFTER "await Task.WhenAll(tList)" here, otherwise the other players would also time out due to awaiting from this disconnected player!
                    clearPlayerNetworkSession(playerId);
                    _logger.LogInformation("OnPlayerDisconnected finished: [ roomId={0}, joinIndex={1}, playerId={2}, roomState={3}, nowRoomEffectivePlayerCount={4} ]", id, thatPlayer.CharacterDownsync.JoinIndex, playerId, state, effectivePlayerCount);
                    break;
            }
        } finally {
            joinerLock.ReleaseMutex();
        }

        if (shouldDismiss) {
            await DismissBattleAsync();
            Interlocked.Exchange(ref state, ROOM_STATE_IDLE);
            _roomManager.Put(this);
        }
    }

    private async void clearPlayerNetworkSession(string playerId) {
        Player? player;
        if (players.TryGetValue(playerId, out player)) {
            var joinIndex = player.CharacterDownsync.JoinIndex;
            if (null != player.BattleUdpTunnelAddr) {
                // [WARNING] "player.BattleUdpTunnelAuthKey" remains the same in a same battle!
                player.BattleUdpTunnelAddr = null;
            }
            if (null != peerUdpAddrList && null != peerUdpAddrList[joinIndex]) {      
                // [WARNING] Invalidates UDP addr but keeps "AuthKey & SeqNo"
                peerUdpAddrList[joinIndex].Ip = ""; 
                peerUdpAddrList[joinIndex].Port = 0; 
            } 
            if (playerDownsyncSessionDict.ContainsKey(playerId)) {
                // [WARNING] No need to close "pR.CharacterDownsyncChanDict[playerId]" immediately!
                if (playerActiveWatchdogDict.ContainsKey(playerId)) {
                    if (null != playerActiveWatchdogDict[playerId]) {
                        playerActiveWatchdogDict[playerId].Stop();
                        playerActiveWatchdogDict[playerId].ExplicitlyDispose();
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
                    try { 
                        if (!cancellationTokenSource.IsCancellationRequested) {
                            cancellationTokenSource.Cancel();
                        }
                    } catch (Exception ex) {
                        _logger.LogWarning($"clearPlayerNetworkSession exception when cancelling cancellationTokenSource of playerSignalToCloseDict[{playerId}] and joinIndex={joinIndex}: {ex}");
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

                _logger.LogInformation($"clearPlayerNetworkSession finished: [ roomId={id}, playerId={playerId}, joinIndex={joinIndex}, roomState={state}, nowRoomEffectivePlayerCount={effectivePlayerCount} ]");
            } else {
                _logger.LogWarning($"clearPlayerNetworkSession couldn't playerDownsyncSession for: [ roomId={id}, playerId={playerId}, joinIndex={joinIndex}, roomState={state}, nowRoomEffectivePlayerCount={effectivePlayerCount} ]");
            }
        } else {
            _logger.LogWarning("clearPlayerNetworkSession couldn't find player info for: [ roomId={0}, playerId={1}, roomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
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

    public async Task<bool> OnPlayerBattleColliderAcked(string targetPlayerId, RoomDownsyncFrame selfParsedRdf, RepeatedField<SerializableConvexPolygon> serializedBarrierPolygons, RepeatedField<SerializedCompletelyStaticPatrolCueCollider> serializedStaticPatrolCues, RepeatedField<SerializedCompletelyStaticTrapCollider> serializedCompletelyStaticTraps, RepeatedField<SerializedCompletelyStaticTriggerCollider> serializedStaticTriggers, SerializedTrapLocalIdToColliderAttrs serializedTrapLocalIdToColliderAttrs, SerializedTriggerEditorIdToLocalId serializedTriggerEditorIdToLocalId, int spaceOffsetX, int spaceOffsetY, int battleDurationSeconds) {
        Player? targetPlayer;
        if (!players.TryGetValue(targetPlayerId, out targetPlayer)) {
            return false;
        }
        battleDurationFrames = battleDurationSeconds * BATTLE_DYNAMICS_FPS;
        elongatedBattleDurationFrames = 3*(battleDurationFrames >> 1);
        bool shouldTryToStartBattle = true;
        var targetPlayerBattleState = Interlocked.Read(ref targetPlayer.BattleState);
        _logger.LogInformation("OnPlayerBattleColliderAcked-before: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);
        switch (targetPlayerBattleState) {
            case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                var playerAckedFrame = new RoomDownsyncFrame {
                    Id = backendTimerRdfId,
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
                provisionStepHolders(capacity, renderBuffer, pushbackFrameLogBuffer, inputBuffer, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, effPushbacks, hardPushbackNormsArr, softPushbacks, confirmedBattleResult);
                _logger.LogInformation("OnPlayerBattleColliderAcked-post-provisionStepHolders: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);

                renderBuffer.Put(selfParsedRdf);

                //_logger.LogInformation("OnPlayerBattleColliderAcked-post-downsync details: roomId={0}, selfParsedRdf={1}, serializedBarrierPolygons={2}", id, selfParsedRdf, serializedBarrierPolygons);

                refreshColliders(selfParsedRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerEditorIdToLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, out staticCollidersCnt, ref collisionHolder, ref residueCollided, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerEditorIdToLocalId, ref triggerEditorIdToConfigFromTiled);

                _logger.LogInformation("OnPlayerBattleColliderAcked-post-downsync: Initialized renderBuffer by incoming startRdf for roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}, staticCollidersCnt={6}; now renderBuffer: {7}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount, staticCollidersCnt, renderBuffer.toSimpleStat());
            } else {
                var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                if (!ok1 || null == startRdf) {
                    throw new ArgumentNullException(String.Format("OnPlayerBattleColliderAcked-post-downsync: No existing startRdf for roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}; now renderBuffer: {6}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount, renderBuffer.toSimpleStat()));
                }
                var src = selfParsedRdf.PlayersArr[targetPlayer.CharacterDownsync.JoinIndex - 1];
                var dst = startRdf.PlayersArr[targetPlayer.CharacterDownsync.JoinIndex - 1];

                AssignToCharacterDownsync(src.Id, src.SpeciesId, src.VirtualGridX, src.VirtualGridY, src.DirX, src.DirY, src.VelX, src.FrictionVelX, src.VelY, src.FrictionVelY, src.FramesToRecover, src.FramesInChState, src.ActiveSkillId, src.ActiveSkillHit, src.FramesInvinsible, src.Speed, src.CharacterState, src.JoinIndex, src.Hp, src.InAir, src.OnWall, src.OnWallNormX, src.OnWallNormY, src.FramesCapturedByInertia, src.BulletTeamId, src.ChCollisionTeamId, src.RevivalVirtualGridX, src.RevivalVirtualGridY, src.RevivalDirX, src.RevivalDirY, src.JumpTriggered, src.SlipJumpTriggered, src.PrimarilyOnSlippableHardPushback, src.CapturedByPatrolCue, src.FramesInPatrolCue, src.BeatsCnt, src.BeatenCnt, src.Mp, src.OmitGravity, src.OmitSoftPushback, src.RepelSoftPushback, src.GoalAsNpc, src.WaivingPatrolCueId, src.OnSlope, src.OnSlopeFacingDown, src.ForcedCrouching, src.NewBirth, src.JumpStarted, src.FramesToStartJump, src.FramesSinceLastDamaged, src.RemainingDef1Quota, src.BuffList, src.DebuffList, src.Inventory, false, src.PublishingToTriggerLocalIdUponKilled, src.PublishingEvtMaskUponKilled, src.SubscribesToTriggerLocalId, src.JumpHoldingRdfCnt, src.BtnBHoldingRdfCount, src.BtnCHoldingRdfCount, src.BtnDHoldingRdfCount, src.BtnEHoldingRdfCount, src.ParryPrepRdfCntDown, src.RemainingAirJumpQuota, src.RemainingAirDashQuota, src.KilledToDropConsumableSpeciesId, src.KilledToDropBuffSpeciesId, src.KilledToDropPickupSkillId, src.BulletImmuneRecords, src.ComboHitCnt, src.ComboFramesRemained, src.DamageElementalAttrs, src.LastDamagedByJoinIndex, src.LastDamagedByBulletTeamId, src.ActivatedRdfId, src.CachedCueCmd, src.MpRegenRdfCountdown, src.FlyingRdfCountdown, src.LockingOnJoinIndex, dst);
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

    private async Task downsyncToSinglePlayerAsyncLoop(string playerId, Player player) {
        WebSocket? wsSession;
        BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>? genOrderPreservedMsgs;
        int joinIndex = player.CharacterDownsync.JoinIndex;
        if (!playerDownsyncSessionDict.TryGetValue(playerId, out wsSession)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}, joinIndex: {2}) doesn't exist! #1", id, playerId, joinIndex);
            return;
        }

        if (!playerSignalToCloseDict.TryGetValue(playerId, out (CancellationTokenSource c1, CancellationToken c2) cancellationSignal)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}, joinIndex: {2}) doesn't exist! #2", id, playerId, joinIndex);
            return;
        }

        if (!playerWsDownsyncQueDict.TryGetValue(playerId, out genOrderPreservedMsgs)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}, joinIndex: {2}) doesn't exist! #3", id, playerId, joinIndex);
            return;
        }

        _logger.LogInformation("Started downsyncToSinglePlayerAsyncLoop for (roomId: {0}, playerId: {1}, joinIndex: {2})", id, playerId, joinIndex);
        try {
            while (WebSocketState.Open == wsSession.State && !cancellationSignal.c1.IsCancellationRequested) {
                // [WARNING] If "TryTake" timed out while reading, it simply returns false and enters another round of reading.
                if (genOrderPreservedMsgs.TryTake(out (ArraySegment<byte> content, InputBufferSnapshot inputBufferSnapshot) msg, localPlayerWsDownsyncQueBattleReadTimeoutMillis, cancellationSignal.c2)) {
                    var inputBufferSnapshot = msg.inputBufferSnapshot;
                    var content = msg.content;
                    bool shouldResync = inputBufferSnapshot.ShouldForceResync;
                    /*
                       [WARNING] 

                       Reasons behind putting this "downsyncToSinglePlayerAsync" under a "Task.Run(...)" as follows.

                       - When "downsyncToAllPlayers" is invoked by "OnBattleCmdReceived(...)" which is in turn very frequently called **even upon UDP reception**, we want it to be "as I/O non-blocking as possible" -- that said, the need for "downsyncToAllPlayers" to **preserve the order of generation of "inputBufferSnapshot" for sending** still exists -- creating a somewhat dilemma. 

                       - The ideal behavior for me in this case is a "wsSession.PutToSendLater(...)" which executes synchronously in the current calling thread, thus returns immediately without "yielding at I/O awaiting". However "wsSession.SendAsync" returns an "async Task" -- certainly not the ideal form, i.e. "wsSession.SendAsync" respects "TCP flow control" to wait for corresponding ACKs when invocation rate is too high, which puts a significant "function return rate limit" of it ("yielding at I/O awaiting" is NOT a "return").  
                     */
                    var oldPlayerBattleState = Interlocked.Read(ref player.BattleState);

                    switch (oldPlayerBattleState) {
                        case PLAYER_BATTLE_STATE_DISCONNECTED:
                        case PLAYER_BATTLE_STATE_LOST:
                        case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                        case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                        case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                            // There're two additional conditions for early return here compared to "sendSafelyAsync", because "downsyncToSinglePlayerAsync" is dedicated for active players in active battle!
                            return;
                    }

                    int refRenderFrameId = inputBufferSnapshot.RefRenderFrameId;
                    var toSendInputFrameIdSt = (null == inputBufferSnapshot.ToSendInputFrameDownsyncs || 0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) ? TERMINATING_INPUT_FRAME_ID : inputBufferSnapshot.ToSendInputFrameDownsyncs[0].InputFrameId;
                    var toSendInputFrameIdEd = (null == inputBufferSnapshot.ToSendInputFrameDownsyncs || 0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) ? TERMINATING_INPUT_FRAME_ID : inputBufferSnapshot.ToSendInputFrameDownsyncs[inputBufferSnapshot.ToSendInputFrameDownsyncs.Count - 1].InputFrameId + 1;
                    if (backendDynamicsEnabled && shouldResync && PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC == oldPlayerBattleState) {
                        // [WARNING] It's important to notify "PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC" peer the up-to-date disconnected information of other peers BEFORE "[readded-resync] RoomDownsyncFrame", because in extreme cases even "doBattleMainLoopPerTickBackendDynamicsWithProperLocking > _moveForwardLastAllConfirmedInputFrameIdWithoutForcing" couldn't guarantee to unfreeze the "PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC" peer. 
                        foreach (var (otherPlayerId, otherPlayer) in players) { 
                            if (otherPlayerId == playerId) continue;
                            var oldOtherPlayerBattleState = Interlocked.Read(ref otherPlayer.BattleState);
                            if (PLAYER_BATTLE_STATE_ACTIVE == oldOtherPlayerBattleState) continue;
                            // Tell the re-added player who's inactive in the same room
                            var resp = new WsResp {
                                Ret = ErrCode.Ok,
                                    Act = DOWNSYNC_MSG_ACT_PLAYER_DISCONNECTED,
                                    PeerJoinIndex = otherPlayer.CharacterDownsync.JoinIndex
                            };
                            await wsSession.SendAsync(new ArraySegment<byte>(resp.ToByteArray()), WebSocketMessageType.Binary, true, cancellationSignal.c2).WaitAsync(DEFAULT_BACK_TO_FRONT_WS_WRITE_TIMEOUT);
                        }
                    }

                    /*
                       Resync helps
                       1. when player with a slower frontend clock lags significantly behind and thus wouldn't get its inputUpsync recognized due to faster "forceConfirmation"
                       2. reconnection
                     */

                    // [WARNING] Preserving generated order (of inputBufferSnapshot) while sending per player by simply "awaiting" the "wsSession.SendAsync(...)" calls
                    await wsSession.SendAsync(content, WebSocketMessageType.Binary, true, cancellationSignal.c2).WaitAsync(DEFAULT_BACK_TO_FRONT_WS_WRITE_TIMEOUT);

                    player.LastSentInputFrameId = toSendInputFrameIdEd - 1;

                    if (backendDynamicsEnabled && shouldResync && PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC == oldPlayerBattleState) {
                        PlayerSessionAckWatchdog? watchdog;
                        if (playerActiveWatchdogDict.TryGetValue(playerId, out watchdog)) {
                            // Needs wait for frontend UDP start up which might be time consuming
                            watchdog.KickWithOneoffInterval(8000);
                            Interlocked.Exchange(ref player.BattleState, PLAYER_BATTLE_STATE_ACTIVE);
                            _logger.LogInformation($"[readded-resync] @LastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}; Sent refRenderFrameId={refRenderFrameId} & inputFrameIds [{toSendInputFrameIdSt}, {toSendInputFrameIdEd}), for roomId={id}, playerId={playerId}, playerJoinIndex={joinIndex} just became ACTIVE, backendTimerRdfId={backendTimerRdfId}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}, playerLastSentInputFrameId={player.LastSentInputFrameId}: REENTRY WATCHDOG STARTED, contentByteLength={content.Count}, now inputBuffer={inputBuffer.toSimpleStat()}");
                        } else {
                            _logger.LogWarning($"[readded-resync FAILED] roomId={id}, playerId={playerId}, playerJoinIndex={joinIndex} REENTRY WATCHDOG NOT FOUND, backendTimerRdfId={backendTimerRdfId}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}, playerLastSentInputFrameId={player.LastSentInputFrameId}: proactively disconnecting this player, now inputBuffer={inputBuffer.toSimpleStat()}");
                            OnPlayerDisconnected(playerId);
                            return;
                        }
                        foreach (var (otherPlayerId, otherPlayer) in players) { 
                            if (otherPlayerId == playerId) continue;
                            var oldOtherPlayerBattleState = Interlocked.Read(ref otherPlayer.BattleState);
                            if (oldOtherPlayerBattleState == PLAYER_BATTLE_STATE_ACTIVE) {
                                // Broadcast re-added player info to all active players in the same room
                                _ = sendSafelyAsync(null, null, null, DOWNSYNC_MSG_ACT_PLAYER_READDED_AND_ACKED, otherPlayerId, otherPlayer, player.CharacterDownsync.JoinIndex);
                            }
                        }
                    }
                }
            }
        } catch (OperationCanceledException cEx) {
            _logger.LogWarning("downsyncToSinglePlayerAsyncLoop cancelled for (roomId: {0}, playerId: {1}, joinIndex: {2}). cEx={3}", id, playerId, joinIndex, cEx.Message);
        } catch (Exception ex) {
            _logger.LogError(ex, "Exception occurred during downsyncToSinglePlayerAsyncLoop to (roomId: {0}, playerId: {1}, joinIndex: {2})", id, playerId, joinIndex);
        } finally {
            _logger.LogInformation("Ended downsyncToSinglePlayerAsyncLoop for (roomId: {0}, playerId: {1}, joinIndex: {2})", id, playerId, joinIndex);
        }
    }

    private async void startBattleAsync() {
        var nowRoomState = Interlocked.Read(ref state);
        if (ROOM_STATE_WAITING != nowRoomState) {
            _logger.LogWarning("[StartBattle] Battle not started due to not being WAITING: roomId={0}, roomState={1}", id, state);
            return;
        }

        backendTimerRdfId = 0;

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
            Id = backendTimerRdfId
        };

        var tList = new List<Task>();
        // It's important to send kickoff frame iff  "0 == backendTimerRdfId && nextRenderFrameId > backendTimerRdfId", otherwise it might send duplicate kickoff frames
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
            if (ROOM_STATE_IN_SETTLEMENT != nowRoomState && ROOM_STATE_WAITING != nowRoomState) {
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
            battleDurationFrames = 0;
            elongatedBattleDurationFrames = 0;
            elongatedBattleDurationFramesShortenedOnce = false;
            stageName = _availableStageNames[_randGenerator.Next(_availableStageNames.Length)];

            joinIndexRemap = new Dictionary<int, int>();
            justDeadJoinIndices = new HashSet<int>();
            fulfilledTriggerSetMask = 0;
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
            while (backendTimerRdfId <= elongatedBattleDurationFrames) {
                nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var stCalculation = nowMillis;
                nowRoomState = Interlocked.Read(ref state);
                if (ROOM_STATE_IN_BATTLE != nowRoomState) {
                    break;
                }

                nowMillis = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var totalElapsedMillis = nowMillis - battleStartedAt;

                int toSleepMillis = estimatedMillisPerFrame;
                int nextRenderFrameId = (int)((totalElapsedMillis + estimatedMillisPerFrame - 1) / estimatedMillisPerFrame); // fast ceiling

                if (curDynamicsRenderFrameId >= battleDurationFrames) {
                    _logger.LogInformation("In `battleMainLoop` for roomId={0}, curDynamicsRenderFrameId={1} already surpassed battleDurationFrames={2}@backendTimerRdfId={3}, elongatedBattleDurationFrames={4}; awaiting backendTimerRdfId to surpass elongatedBattleDurationFrames", id, curDynamicsRenderFrameId, battleDurationFrames, backendTimerRdfId, elongatedBattleDurationFrames);
                    backendTimerRdfId = nextRenderFrameId; 
                } else {
                    toSleepMillis = (estimatedMillisPerFrame >> 1); // Sleep half-frame time by default
                    if (nextRenderFrameId > backendTimerRdfId) {
                        if (0 == backendTimerRdfId) {
                            var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                            if (!ok1 || null == startRdf) {
                                throw new ArgumentNullException(String.Format("OnPlayerBattleColliderAcked-post-downsync: No existing startRdf for roomId={0}, roomState={1}, capacity={2}, effectivePlayerCount={3}; now renderBuffer: {4}", id, state, capacity, effectivePlayerCount, renderBuffer.toSimpleStat()));
                            }
                            var tList = new List<Task>();
                            // It's important to send kickoff frame iff  "0 == backendTimerRdfId && nextRenderFrameId > backendTimerRdfId", otherwise it might send duplicate kickoff frames

                            foreach (var (playerId, player) in players) {
                                tList.Add(sendSafelyAsync(startRdf, null, null, DOWNSYNC_MSG_ACT_BATTLE_START, playerId, player, MAGIC_JOIN_INDEX_DEFAULT));
                            }
                            await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
                            _logger.LogInformation("In `battleMainLoop` for roomId={0} sent out startRdf with {1} bytes", id, startRdf.ToByteArray().Length);
                        }

                        int prevRenderFrameId = backendTimerRdfId;
                        backendTimerRdfId = nextRenderFrameId;

                        ulong dynamicsDuration = 0ul;
                        // Prefab and buffer backend inputFrameDownsync
                        if (backendDynamicsEnabled) {
                            int oldCurDynamicsRenderFrameId = curDynamicsRenderFrameId;
                            doBattleMainLoopPerTickBackendDynamicsWithProperLocking(prevRenderFrameId, ref dynamicsDuration);
                            if (oldCurDynamicsRenderFrameId < curDynamicsRenderFrameId && curDynamicsRenderFrameId+(int)(4UL << INPUT_SCALE_FRAMES) >= battleDurationFrames) {
                                int oldElongatedBattleDurationFrame = elongatedBattleDurationFrames; 
                                int proposedElongatedBattleDurationFrame = (backendTimerRdfId + 3*BATTLE_DYNAMICS_FPS); // [WARNING] Now that CONFIRMED LAST INPUT FRAMES from all players are received, we shouldn't be awaiting too long from here on.
                                if (false == elongatedBattleDurationFramesShortenedOnce) {
                                    elongatedBattleDurationFrames = oldElongatedBattleDurationFrame < proposedElongatedBattleDurationFrame ? oldElongatedBattleDurationFrame : proposedElongatedBattleDurationFrame;  
                                    elongatedBattleDurationFramesShortenedOnce = true;
                                } else {
                                    elongatedBattleDurationFrames = oldElongatedBattleDurationFrame > proposedElongatedBattleDurationFrame ? oldElongatedBattleDurationFrame : proposedElongatedBattleDurationFrame;  
                                }
                                _logger.LogInformation("In `battleMainLoop` for roomId={0}, curDynamicsRenderFrameId={1} just surpassed battleDurationFrames={2}, elongating per required: backendTimerRdfId={3}, oldElongatedBattleDurationFrame={4}, newElongatedBattleDurationFrames={5}", id, curDynamicsRenderFrameId, battleDurationFrames, backendTimerRdfId, oldElongatedBattleDurationFrame, elongatedBattleDurationFrames);
                            }
                        }

                        var elapsedInCalculation = (DateTimeOffset.Now.ToUnixTimeMilliseconds() - stCalculation);
                        toSleepMillis = (int)(estimatedMillisPerFrame - elapsedInCalculation);
                        if (0 > toSleepMillis) toSleepMillis = 0; 
                    }
                }

                await Task.Delay(toSleepMillis);
            }

            _logger.LogInformation("Times up, will settle `battleMainLoopActionAsync` for roomId={0} @backendTimerRdfId={1}, elongatedBattleDurationFrames={2}", id, backendTimerRdfId, elongatedBattleDurationFrames);
        } catch (Exception ex) {
            _logger.LogError(ex, $"Error running battleMainLoopActionAsync for roomId={id}, backendTimerRdfId={backendTimerRdfId}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}, renderBuffer={renderBuffer.toSimpleStat()}, inputBuffer={inputBuffer.toSimpleStat()}");
        } finally {
            await SettleBattleAsync();
            _logger.LogInformation("The `battleMainLoop` for roomId={0} is settled@backendTimerRdfId={1}", id, backendTimerRdfId);
            await DismissBattleAsync();
            _logger.LogInformation("The `battleMainLoop` for roomId={0} is dismissed@backendTimerRdfId={1}", id, backendTimerRdfId);
            Interlocked.Exchange(ref state, ROOM_STATE_IDLE);
            _roomManager.Put(this);
        }
    }

    public void OnBattleCmdReceived(WsReq pReq, string playerId, bool fromUDP, bool fromReentryWsSession=false) {
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
        if (0 >= inputFrameUpsyncBatch.Count) {
            _logger.LogWarning("OnBattleCmdReceived early return because inputFrameUpsyncBatch is empty: roomId={0}, fromPlayerId={1}, nowRoomState={2}", id, playerId, nowRoomState);
            return;
        }
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
            /*
               if (fromReentryWsSession) {
               _logger.LogInformation("[AFTER REENTRY] roomId={0}, playerId={1}, joinIndex={2}, upsynced from reentry ws session inputFrameId range: [{3}, {4}], lastAllConfirmedInputFrameId={5}", id, playerId, player.CharacterDownsync.JoinIndex, inputFrameUpsyncBatch[0].InputFrameId, inputFrameUpsyncBatch[inputFrameUpsyncBatch.Count-1].InputFrameId, lastAllConfirmedInputFrameId);
               }
             */
        }

        player.AckingFrameId = ackingFrameId;
        player.AckingInputFrameId = ackingInputFrameId;

        //_logger.LogInformation("OnBattleCmdReceived-inputBufferLock about to lock: roomId={0}, fromPlayerId={1}", id, playerId);
        // I've been seeking a totally lock-free approach for this whole operation for a long time, but still it's safer to keep using "mutex inputBufferLock"... 
        inputBufferLock.WaitOne();
        try {
            var inputBufferSnapshot = markConfirmationIfApplicable(inputFrameUpsyncBatch, playerId, player, fromUDP, fromReentryWsSession);
            if (null != inputBufferSnapshot) {
                downsyncToAllPlayers(inputBufferSnapshot);
            }
        } finally {
            inputBufferLock.ReleaseMutex();
            //_logger.LogInformation("OnBattleCmdReceived-inputBufferLock unlocked: roomId={0}, fromPlayerId={1}", id, playerId);
        }
    }

    private int _moveForwardLastAllConfirmedInputFrameIdWithoutForcing(int proposedIfdEdFrameId, ref ulong unconfirmedMask) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!

        int incCnt = 0;
        int proposedIfdStFrameId = lastAllConfirmedInputFrameId+1;
        for (int inputFrameId = proposedIfdStFrameId; inputFrameId < proposedIfdEdFrameId; inputFrameId++) {
            // See comments for the traversal in "markConfirmationIfApplicable".
            if (inputFrameId < inputBuffer.StFrameId) {
                continue;
            }
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId);
            if (false == res1 || null == inputFrameDownsync) {
                throw new ArgumentException($"[_moveForwardLastAllConfirmedInputFrameIdWithoutForcing] inputFrameId={inputFrameId} doesn't exist for roomId={id}: lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}, proposedIfdStFrameId={proposedIfdStFrameId}, proposedIfdEdFrameId={proposedIfdEdFrameId}, inputBuffer={inputBuffer.toSimpleStat()}");
            }
            bool shouldBreakConfirmation = false;

            if (allConfirmedMask != inputFrameDownsync.ConfirmedList) {
                //_logger.LogInformation("Found a non-all-confirmed inputFrame for roomId={0}, upsync player(id:{1}, joinIndex:{2}) while checking inputFrameId=[{3}, {4}) inputFrameId={5}, confirmedList={6}", id, playerId, player.CharacterDownsync.JoinIndex, inputFrameId1, inputBuffer.EdFrameId, inputFrameId, inputFrameDownsync.ConfirmedList);
                foreach (var thatPlayer in playersArr) {
                    var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
                    int j = (thatPlayer.CharacterDownsync.JoinIndex - 1);
                    var thatPlayerJoinMask = (1UL << j);
                    bool isSlowTicker = (0 == (inputFrameDownsync.ConfirmedList & thatPlayerJoinMask));
                    bool isActiveSlowTicker = (isSlowTicker && PLAYER_BATTLE_STATE_ACTIVE == thatPlayerBattleState);
                    if (isActiveSlowTicker) {
                        shouldBreakConfirmation = true; // Could be an `ACTIVE SLOW TICKER` here, but no action needed for now
                        break;
                    }
                    if (isSlowTicker) {
                        unconfirmedMask |= (1ul << j);
                        inputFrameDownsync.InputList[j] = 0; // For UNCONFIRMED BUT INACTIVE player input, always predict it to zero.
                                                             //_logger.LogInformation("markConfirmationIfApplicable for roomId={0}, skipping UNCONFIRMED BUT INACTIVE player(id:{1}, joinIndex:{2}) while checking inputFrameId=[{3}, {4})", id, thatPlayer.CharacterDownsync.Id, thatPlayer.CharacterDownsync.JoinIndex, inputFrameId1, inputBuffer.EdFrameId);
                    }
                }
            }

            if (shouldBreakConfirmation) {
                break;
            }
            incCnt += 1;
            inputFrameDownsync.ConfirmedList = allConfirmedMask;
            onInputFrameDownsyncAllConfirmed(inputFrameDownsync, INVALID_DEFAULT_PLAYER_ID);
        }
        return incCnt;
    }

    private InputBufferSnapshot? markConfirmationIfApplicable(Pbc.RepeatedField<InputFrameUpsync> inputFrameUpsyncBatch, string playerId, Player player, bool fromUDP, bool fromReentryWsSession=false) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!

        int oldLastAllConfirmedInputFrameId = lastAllConfirmedInputFrameId;
        int newAllConfirmedCount = 0;
        ulong unconfirmedMask = 0;
        int joinIndex = player.CharacterDownsync.JoinIndex;
        int clientInputFrameIdEd = lastAllConfirmedInputFrameId + 1; 
        var virtualBackendTimerGenIfdIdWithTolerance = (0 < backendTimerRdfId ? ConvertToDynamicallyGeneratedDelayInputFrameId(backendTimerRdfId + BATTLE_DYNAMICS_FPS*3, 0) : MAX_INT);
        foreach (var inputFrameUpsync in inputFrameUpsyncBatch) {
            var clientInputFrameId = inputFrameUpsync.InputFrameId;
            if (clientInputFrameId < inputBuffer.StFrameId) {
                // The updates to "inputBuffer.StFrameId" is monotonically increasing, thus if "clientInputFrameId < inputBuffer.StFrameId" at any moment of time, it is obsolete in the future.
                //_logger.LogInformation("Omitting obsolete inputFrameUpsync#1: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat());
                continue;
            }
            if (clientInputFrameId < player.LastConsecutiveRecvInputFrameId) {
                // [WARNING] It's important for correctness that we use "player.LastConsecutiveRecvInputFrameId" instead of "lastIndividuallyConfirmedInputFrameId[player.JoinIndex-1]" here!
                /*
                   if (fromReentryWsSession) {
                   _logger.LogInformation("Omitting obsolete inputFrameUpsync#2: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}, playerLastConsecutiveRecvInputFrameId={5}", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat(), player.LastConsecutiveRecvInputFrameId);
                   }
                 */
                continue;
            }
            if (clientInputFrameId <= lastAllConfirmedInputFrameId) {
                /*
                   if (fromReentryWsSession) {
                   _logger.LogInformation("Omitting obsolete inputFrameUpsync#3: roomId={0}, playerId={1}, clientInputFrameId={2}, lastAllConfirmedInputFrameId={3}, inputBuffer={4}", id, playerId, clientInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat());
                   }
                 */
                continue;
            }

            bool willEvict = (clientInputFrameId >= inputBuffer.EdFrameId && inputBuffer.Cnt >= inputBuffer.N);
            if (willEvict) {
                var toEvictIfd = inputBuffer.GetFirst();
                if (null == toEvictIfd) {
                    _logger.LogWarning($"[markConfirmationIfApplicable] early return because clientInputFrameId={clientInputFrameId} against a full inputBuffer={inputBuffer.toSimpleStat()} is having a null toEvictIfd in this room: roomId={id}, backendTimerRdfId={backendTimerRdfId}, fromPlayerId={playerId}, fromPlayerJoinIndex={joinIndex}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}: breaking and ignoring the rest inputFrameUpsyncs from this player");
                    break;
                }
                var toEvictIfdId = toEvictIfd.InputFrameId;
                var curDynamicsToUseIfdId = ConvertToDelayedInputFrameId(curDynamicsRenderFrameId);
                bool toEvictIfdAlreadyUsed = (curDynamicsToUseIfdId > toEvictIfdId);
                if (!toEvictIfdAlreadyUsed) {
                    bool tooAdvanced = (virtualBackendTimerGenIfdIdWithTolerance < toEvictIfdId);
                    if (tooAdvanced) {
                        _logger.LogWarning($"[markConfirmationIfApplicable] early return because toEvictIfdId={toEvictIfdId} >= virtualBackendTimerGenIfdIdWithTolerance={virtualBackendTimerGenIfdIdWithTolerance}@inputBuffer={inputBuffer.toSimpleStat()} in this room: roomId={id}, backendTimerRdfId={backendTimerRdfId}, fromPlayerId={playerId}, fromPlayerJoinIndex={joinIndex}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}: breaking and ignoring the rest inputFrameUpsyncs from this player");
                        break;
                    }
                    bool shouldBreakBatchTraversal = false;
                    int insituForceConfirmationInc = 0;
                    if (insituForceConfirmationEnabled) {
                        // [WARNING] By now "toEvictIfdId" has already passed "tooAdvanced" check.

                        /**
                        When "insituForceConfirmation" is triggered, the key pointers are always as follows. 
        
                        ---------------------------------------------------------------------------------------------------------------
                        lastAllConfirmedRdfId | [inputBuffer.StFrameId, ..., inputBuffer.EdFrameId) | ... | clientInputFrameId
                        ---------------------------------------------------------------------------------------------------------------
    
                        and we want them to become

            
                        ---------------------------------------------------------------------------------------------------------------
                        [inputBuffer.StFrameId, ..., lastAllConfirmedRdfId, ... inputBuffer.EdFrameId) | ... | clientInputFrameId
                        ---------------------------------------------------------------------------------------------------------------

                        where (lastAllConfirmedRdfId - inputBuffer.StFrameId) >= (clientInputFrameId - inputBuffer.EdFrameId)
                        */
                        int headGap = (lastAllConfirmedInputFrameId - inputBuffer.StFrameId), fixedTailGap = (clientInputFrameId - inputBuffer.EdFrameId);
                        while (headGap < fixedTailGap && toEvictIfdId < inputBuffer.EdFrameId) {
                            unconfirmedMask |= (allConfirmedMask ^ toEvictIfd.ConfirmedList);
                            toEvictIfd.ConfirmedList = allConfirmedMask;
                            onInputFrameDownsyncAllConfirmed(toEvictIfd, INVALID_DEFAULT_PLAYER_ID); // i.e. Moves forward "lastAllConfirmedInputFrameId" such that "multiStep" can advance "curDynamicsRenderFrameId".
                            headGap = (lastAllConfirmedInputFrameId - inputBuffer.StFrameId);
                            ++toEvictIfdId;
                            insituForceConfirmationInc++;
                            (_, toEvictIfd) = inputBuffer.GetByFrameId(toEvictIfdId);
                            if (null == toEvictIfd) {
                                _logger.LogWarning($"[markConfirmationIfApplicable] early return because clientInputFrameId={clientInputFrameId} against a full inputBuffer={inputBuffer.toSimpleStat()} is having a null toEvictIfd by toEvictIfdId={toEvictIfdId} in this room: roomId={id}, backendTimerRdfId={backendTimerRdfId}, fromPlayerId={playerId}, fromPlayerJoinIndex={joinIndex}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}, lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}: breaking and ignoring the rest inputFrameUpsyncs from this player");
                                shouldBreakBatchTraversal = true;
                                break;
                            }
                        }
                    }
                    int nextDynamicsRenderFrameId = (0 <= lastAllConfirmedInputFrameId ? ConvertToLastUsedRenderFrameId(lastAllConfirmedInputFrameId) + 1 : -1);
                    if (0 < nextDynamicsRenderFrameId && nextDynamicsRenderFrameId > curDynamicsRenderFrameId) {
                        _logger.LogInformation($"[markConfirmationIfApplicable] advancing curDynamicsRenderFrameId={curDynamicsRenderFrameId} to nextDynamicsRenderFrameId={nextDynamicsRenderFrameId} to resolve eviction of toEvictIfdId={toEvictIfdId}, insituForceConfirmationInc={insituForceConfirmationInc}, curDynamicsToUseIfdId={curDynamicsToUseIfdId} while clientInputFrameId={clientInputFrameId} is evicting inputBuffer={inputBuffer.toSimpleStat()} n this room: roomId={id}, backendTimerRdfId={backendTimerRdfId}, fromPlayerId={playerId}, fromPlayerJoinIndex={joinIndex}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}: continuing and accepting more inputFrameUpsyncs from this player");
                        // Apply "all-confirmed inputFrames" to move forward "curDynamicsRenderFrameId"
                        multiStep(curDynamicsRenderFrameId, nextDynamicsRenderFrameId);
                        if (shouldBreakBatchTraversal) {
                            break;
                        }
                    } else {
                        _logger.LogWarning($"[markConfirmationIfApplicable] early return because toEvictIfdId={toEvictIfdId}, curDynamicsToUseIfdId={curDynamicsToUseIfdId} not being safe for eviction while clientInputFrameId={clientInputFrameId} is evicting inputBuffer={inputBuffer.toSimpleStat()} in this room: roomId={id}, backendTimerRdfId={backendTimerRdfId}, fromPlayerId={playerId}, fromPlayerJoinIndex={joinIndex}, curDynamicsRenderFrameId={curDynamicsRenderFrameId}, lastAllConfirmedInputFrameId={lastAllConfirmedInputFrameId}: breaking and ignoring the rest inputFrameUpsyncs from this player");
                        // OnPlayerDisconnected(playerId); // [WARNING] This line is tried but outcome is not good, need more data collection and analysis for a better approach.
                        break;
                    } 
                } 
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

            if (clientInputFrameId > clientInputFrameIdEd) clientInputFrameIdEd = clientInputFrameId;
        }

        if (clientInputFrameIdEd > inputBuffer.EdFrameId) clientInputFrameIdEd = inputBuffer.EdFrameId;
        newAllConfirmedCount += _moveForwardLastAllConfirmedInputFrameIdWithoutForcing(clientInputFrameIdEd, ref unconfirmedMask);

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
            int snapshotEdFrameId = lastAllConfirmedInputFrameId + 1;
            /*
               if (fromReentryWsSession) {
               _logger.LogInformation("markConfirmationIfApplicable for roomId={0} returning newAllConfirmedCount={1}, snapshotStFrameId={2}, snapshotEdFrameId={3}", id, newAllConfirmedCount, snapshotStFrameId, snapshotEdFrameId);
               }
             */
            if (snapshotStFrameId >= snapshotEdFrameId) return null;
            return produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(unconfirmedMask, snapshotStFrameId, snapshotEdFrameId);
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
                    ulong encodedIdx = lastIndividuallyConfirmedInputList[i];
                    ifdHolder.InputList[i] = encodedIdx;
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
        if (lastAllConfirmedInputFrameId >= inputFrameId) {
            // Such that "lastAllConfirmedInputFrameId" is monotonic.
            return;
        }
        lastAllConfirmedInputFrameId = inputFrameId;
        inputFrameDownsync.ConfirmedList = allConfirmedMask; // Most likely a redundancy here, but still it's safer this way.
        inputFrameDownsync.InputList.CopyTo(lastAllConfirmedInputList, 0);
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

        if (0 >= inputBufferSnapshot.ToSendInputFrameDownsyncs.Count) {
            _logger.LogWarning("inputBufferSnapshot contains no content when calling downsyncToAllPlayers for (roomId: {0})", id);
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
                    if (PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC == playerBattleState) {
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
            _logger.LogWarning(String.Format("[content too big!] refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, backendTimerRdfId={4}, curDynamicsRenderFrameId={5}: contentByteLength={6} > FRONTEND_WS_RECV_BYTELENGTH={7}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, backendTimerRdfId, curDynamicsRenderFrameId, content.Count, FRONTEND_WS_RECV_BYTELENGTH));
        }

        foreach (var (playerId, player) in players) {
            var playerBattleState = Interlocked.Read(ref player.BattleState);

            switch (playerBattleState) {
                case PLAYER_BATTLE_STATE_DISCONNECTED:
                case PLAYER_BATTLE_STATE_LOST:
                case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                    continue;
            }

            // Method "downsyncToAllPlayers" is called very frequently during active battle, thus deliberately tweaked websocket downsync sending for better throughput.
            BlockingCollection<(ArraySegment<byte>, InputBufferSnapshot)>? playerWsDownsyncQue;
            if (!playerWsDownsyncQueDict.TryGetValue(player.Id, out playerWsDownsyncQue)) {
                _logger.LogWarning("playerWsDownsyncQue for (roomId: {0}, playerId: {1}) doesn't exist! #3", id, playerId);
                return;
            }

            playerWsDownsyncQue.Add((content, inputBufferSnapshot));
        }

        /*
           if (backendDynamicsEnabled && shouldResync) {
           _logger.LogInformation(String.Format("[resync] Sent refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, backendTimerRdfId={4}, curDynamicsRenderFrameId={5}: contentByteLength={6}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, backendTimerRdfId, curDynamicsRenderFrameId, content.Count));
           } else {
           _logger.LogInformation(String.Format("[ipt-sync] Sent refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, backendTimerRdfId={4}, curDynamicsRenderFrameId={5}: contentByteLength={6}", refRenderFrameId, toSendInputFrameIdSt, toSendInputFrameIdEd, id, backendTimerRdfId, curDynamicsRenderFrameId, content.Count));
           }
         */
    }

    private ArraySegment<byte> allocBytesFromInputBufferSnapshot(InputBufferSnapshot inputBufferSnapshot) {
        /*
           [WARNING] This function MUST BE called while "pR.inputBufferLock" is LOCKED such that "renderBuffer.GetByFrameId(...)" is synchronized across different threads!
         */
        var (ok1, refRenderFrame) = renderBuffer.GetByFrameId(inputBufferSnapshot.RefRenderFrameId);
        if (!ok1 || null == refRenderFrame) {
            throw new ArgumentNullException(String.Format("allocBytesFromInputBufferSnapshot-Required refRenderFrameId={0} for (roomId={1}, backendTimerRdfId={2}) doesn't exist! inputBuffer={3}, renderBuffer={4}", inputBufferSnapshot.RefRenderFrameId, id, backendTimerRdfId, inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat()));
        }
        if (refRenderFrame.Id != inputBufferSnapshot.RefRenderFrameId) {
            throw new ArgumentException(String.Format("allocBytesFromInputBufferSnapshot-Required refRenderFrameId={0} for (roomId={1}, backendTimerRdfId={2}) but got refRenderFrame.Id={5}! inputBuffer={3}, renderBuffer={4}", inputBufferSnapshot.RefRenderFrameId, id, backendTimerRdfId, inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat(), refRenderFrame.Id));
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

    private async Task sendSafelyAsync(RoomDownsyncFrame? roomDownsyncFrame, RepeatedField<InputFrameDownsync>? toSendInputFrameDownsyncs, RepeatedField<PeerUdpAddr>? peerUdpAddrList, int act, string playerId, Player player, int peerJoinIndex) {
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

        if (null == peerUdpAddrList) {
            // The "finally" block would help close "battleUdpTunnel".
            _logger.LogWarning("Returning `startBattleUdpTunnelAsyncTask` early#3 for roomId={0} due to null `battleUdpTunnelAddr`", id);
            return;
        }

        _logger.LogInformation("`battleUdpTask` starting for roomId={0}", id);

        battleUdpTunnelCancellationTokenSource = new CancellationTokenSource(); // [WARNING] Will be disposed along with "battleUdpTask".
        CancellationToken battleUdpTunnelCancellationToken = battleUdpTunnelCancellationTokenSource.Token;
        try {
            _logger.LogInformation("`battleUdpTask` started for roomId={0} @ now peerUdpAddrList={1}", id, peerUdpAddrList);

            while (!battleUdpTunnelCancellationTokenSource.IsCancellationRequested) {
                var recvResult = await battleUdpTunnel.ReceiveAsync(battleUdpTunnelCancellationToken);
                //_logger.LogInformation("`battleUdpTunnel` received for roomId={0}: recvResult.Buffer.Length={1}", id, recvResult.Buffer.Length);
                WsReq pReq = WsReq.Parser.ParseFrom(recvResult.Buffer);
                //_logger.LogInformation("`battleUdpTunnel` received for roomId={0}: pReq={1}", id, pReq);
                string playerId = pReq.PlayerId;
                Player? player;
                if (players.TryGetValue(playerId, out player)) {
                    int reqAuthKey = pReq.AuthKey;
                    if (reqAuthKey != player.BattleUdpTunnelAuthKey) {
                        _logger.LogWarning("In `battleUdpTask`, received mismatch BattleUdpTunnelAuthKey for (roomId: {0}, playerId: {1}, reqAuthKey: {2}, requiredKey: {3}): ", id, playerId, reqAuthKey, player.BattleUdpTunnelAuthKey);
                        continue;
                    }
                } else {
                    _logger.LogWarning("In `battleUdpTask`, player for (roomId: {0}, playerId: {1}) doesn't exist!", id, playerId);
                    continue;
                }

                if (UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL == pReq.Act) {
                    if (null != peerUdpAddrList[player.CharacterDownsync.JoinIndex] && peerUdpAddrList[player.CharacterDownsync.JoinIndex].SeqNo >= pReq.SeqNo) {
                       continue; 
                    }
                    player.BattleUdpTunnelAddr = recvResult.RemoteEndPoint;
                    peerUdpAddrList[player.CharacterDownsync.JoinIndex] = new PeerUdpAddr {
                        Ip = recvResult.RemoteEndPoint.Address.ToString(),
                           Port = recvResult.RemoteEndPoint.Port,
                           SeqNo = pReq.SeqNo
                    };
                    //_logger.LogInformation("`battleUdpTask` for roomId={0} updated udp addr for playerId={1} to be {2}", id, playerId, peerUdpAddrList[player.CharacterDownsync.JoinIndex]);
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
                        if (String.Equals(otherPlayerId, playerId)) {
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

        _logger.LogInformation("`battleUdpTask` stopped for (roomId={0})@backendTimerRdfId={1}", id, backendTimerRdfId);
    }

    private void doBattleMainLoopPerTickBackendDynamicsWithProperLocking(int prevRenderFrameId, ref ulong dynamicsDuration) {
        inputBufferLock.WaitOne();
        try {
            int oldLastAllConfirmedInputFrameId = lastAllConfirmedInputFrameId;
            // Confirming buffered inputFrames for inactive players.This step is necessary because in an extreme case where the BattleServer itself has a traffic jam such that no player input reaches it within each one's watchdog, the BattleServer will proactively disconnects everyone without calling "OnBattleCmdReceived > markConfirmationIfApplicable > _moveForwardLastAllConfirmedInputFrameIdWithoutForcing", leaving a DISCONTINUOUSLY CONFIRMED INPUTBUFFER AND NOT RECOVERABLE after any player rejoining.
            ulong unconfirmedMask = 0;
            int newAllConfirmedCount1 = _moveForwardLastAllConfirmedInputFrameIdWithoutForcing(inputBuffer.EdFrameId, ref unconfirmedMask);
            // Force setting all-confirmed of buffered inputFrames periodically, kindly note that if "backendDynamicsEnabled", what we want to achieve is "recovery upon reconnection", which certainly requires "forceConfirmationIfApplicable" to move "lastAllConfirmedInputFrameId" forward as much as possible
            ulong forceUnconfirmedMask = forceConfirmationIfApplicable();
            unconfirmedMask |= forceUnconfirmedMask;

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

            if (0 < forceUnconfirmedMask) {
                // [WARNING] As "curDynamicsRenderFrameId" was just incremented above, "refSnapshotStFrameId" is most possibly larger than "oldLastAllConfirmedInputFrameId + 1", therefore this initial assignment is critical for `ACTIVE NORMAL TICKER`s to receive consecutive ids of inputFrameDownsync.
                int snapshotStFrameId = oldLastAllConfirmedInputFrameId + 1;
                int refSnapshotStFrameId = ConvertToDelayedInputFrameId(curDynamicsRenderFrameId - 1);
                if (refSnapshotStFrameId < snapshotStFrameId) {
                    snapshotStFrameId = refSnapshotStFrameId;
                }
                if (snapshotStFrameId < lastAllConfirmedInputFrameId + 1) {
                    var inputBufferSnapshot = produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(unconfirmedMask, snapshotStFrameId, lastAllConfirmedInputFrameId + 1);
                    downsyncToAllPlayers(inputBufferSnapshot);
                }
            } else if (!type1ForceConfirmationEnabled && !type3ForceConfirmationEnabled) {
                bool isLastRenderFrame = (backendTimerRdfId >= battleDurationFrames && curDynamicsRenderFrameId >= battleDurationFrames);
                if ((0 <= lastAllConfirmedInputFrameId && 0 < curDynamicsRenderFrameId && (backendTimerRdfId - lastForceResyncedRdfId > FORCE_RESYNC_INTERVAL_THRESHOLD || isLastRenderFrame))) {
                    // [WARNING] With no forceConfirmation enabled, the following logic is based on "allConfirmed" input frames which all frontends already hold full information of, hence no need to be concerned with fast-forwarding issues. 
                    int snapshotStFrameId = oldLastAllConfirmedInputFrameId + 1;
                    int refSnapshotStFrameId = ConvertToDelayedInputFrameId(curDynamicsRenderFrameId - 1);
                    if (refSnapshotStFrameId < snapshotStFrameId) {
                        snapshotStFrameId = refSnapshotStFrameId;
                    }
                    if (snapshotStFrameId < lastAllConfirmedInputFrameId + 1) {
                        unconfirmedMask = allConfirmedMask;
                        var inputBufferSnapshot = produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(unconfirmedMask, snapshotStFrameId, lastAllConfirmedInputFrameId + 1);
                        downsyncToAllPlayers(inputBufferSnapshot);
                        lastForceResyncedRdfId = backendTimerRdfId;
                    }
                    /*
                       if (null != inputBufferSnapshot) {
                       _logger.LogInformation(String.Format("[no forceConfirmation, just resync] Sent refRenderFrameId={0} & inputFrameIds [{1}, {2}), for roomId={3}, backendTimerRdfId={4}, curDynamicsRenderFrameId={5}", inputBufferSnapshot.RefRenderFrameId, snapshotStFrameId, lastAllConfirmedInputFrameId, id, backendTimerRdfId, curDynamicsRenderFrameId));
                       }
                     */
                }
            }
        } finally {
            inputBufferLock.ReleaseMutex();
            //_logger.LogInformation("doBattleMainLoopPerTickBackendDynamicsWithProperLocking-inputBufferLock unlocked: roomId={0}, fromPlayerId={1}", id, playerId);
        }
    }

    private ulong forceConfirmationIfApplicable() {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        int totPlayerCnt = capacity;
        ulong unconfirmedMask = 0;
        // As "lastAllConfirmedInputFrameId" can be advanced by UDP but "latestPlayerUpsyncedInputFrameId" could only be advanced by ws session, when the following condition is met we know that the slow ticker is really in trouble!
        bool isLastRenderFrame = (backendTimerRdfId >= battleDurationFrames && curDynamicsRenderFrameId >= battleDurationFrames);
        if (type3ForceConfirmationEnabled && (0 < latestPlayerUpsyncedInputFrameId && 0 < curDynamicsRenderFrameId && (backendTimerRdfId - lastForceResyncedRdfId > FORCE_RESYNC_INTERVAL_THRESHOLD || isLastRenderFrame))) {
            // Type#3 forceResync regularly
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
            if (isLastRenderFrame) {
                unconfirmedMask = allConfirmedMask;
            }
            _logger.LogInformation(String.Format("[type#3 forceConfirmation] For roomId={0}@backendTimerRdfId={1}, curDynamicsRenderFrameId={2}, LatestPlayerUpsyncedInputFrameId:{3}, LastAllConfirmedInputFrameId:{4} -> {5}, lastForceResyncedRdfId={6}, unconfirmedMask={7}", id, backendTimerRdfId, curDynamicsRenderFrameId, latestPlayerUpsyncedInputFrameId, oldLastAllConfirmedInputFrameId, lastAllConfirmedInputFrameId, lastForceResyncedRdfId, unconfirmedMask));
            /*
               [WARNING] Only "type#1" will delay "conditional graphical update" from the force-resync snapshot for non-self-unconfirmed players, both "type#2" and "type#3" might have immediate impact on the "ACTIVE NORMAL TICKER"s, e.g. abrupt & inconsistent graphical update.  

               However, experiments show that for type#3 "curDynamicsRenderFrameId" is NEVER TOO ADVANCED compared to "OnlineMapController.playerRdfId", possibly due to the absence of any SLOW TICKER (including disconnected) -- hence when executing "OnlineMap.onRoomDownsyncFrame", the force-resync snapshot would only get "RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet", i.e. only effectively trigger "OnlineMapController.onInputFrameDownsyncBatch(accompaniedInputFrameDownsyncBatch) -> _handleIncorrectlyRenderedPrediction(...)" -- thus relatively smooth graphical updates. 
             */
            lastForceResyncedRdfId = backendTimerRdfId;
        } else if (type1ForceConfirmationEnabled && latestPlayerUpsyncedInputFrameId > (lastAllConfirmedInputFrameId + inputFrameUpsyncDelayTolerance)) {
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
                _logger.LogInformation(String.Format("[type#1 forceConfirmation] For roomId={0}@backendTimerRdfId={1}, curDynamicsRenderFrameId={2}, LatestPlayerUpsyncedInputFrameId:{3}, LastAllConfirmedInputFrameId:{4} -> {5}, InputFrameUpsyncDelayTolerance:{6}, unconfirmedMask={7}, lastForceResyncedRdfId={8}; there's a slow ticker suspect, forcing all-confirmation", id, backendTimerRdfId, curDynamicsRenderFrameId, latestPlayerUpsyncedInputFrameId, oldLastAllConfirmedInputFrameId, lastAllConfirmedInputFrameId, inputFrameUpsyncDelayTolerance, unconfirmedMask, lastForceResyncedRdfId));
                lastForceResyncedRdfId = backendTimerRdfId;
            }
        } else {
            // Type#2 helps resolve the edge case when all players are disconnected temporarily
            bool shouldForceResync = false;
            foreach (var player in playersArr) {
                var playerBattleState = Interlocked.Read(ref player.BattleState);
                if (PLAYER_BATTLE_STATE_READDED_PENDING_FORCE_RESYNC == playerBattleState) {
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
                //_logger.LogInformation(String.Format("[type#2 forceConfirmation] For roomId={0}@backendTimerRdfId={1}, curDynamicsRenderFrameId={2}, LatestPlayerUpsyncedInputFrameId:{3}, LastAllConfirmedInputFrameId:{4} -> {5}, lastForceResyncedRdfId={6}", id, backendTimerRdfId, curDynamicsRenderFrameId, latestPlayerUpsyncedInputFrameId, oldLastAllConfirmedInputFrameId, lastAllConfirmedInputFrameId, lastForceResyncedRdfId));
                unconfirmedMask = allConfirmedMask;
                lastForceResyncedRdfId = backendTimerRdfId;
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
        bool selfNotEnoughMp = false;
        for (var i = fromRenderFrameId; i < toRenderFrameId; i++) {
            if (frameLogEnabled) {
                int j = ConvertToDelayedInputFrameId(i);
                var (ok, delayedInputFrame) = inputBuffer.GetByFrameId(j);
                if (false == ok || null == delayedInputFrame) {
                    throw new ArgumentNullException(String.Format("Couldn't find delayedInputFrame for j={0} to log frame info", j));
                }
                rdfIdToActuallyUsedInput[i] = delayedInputFrame.Clone();
            }
            Step(inputBuffer, i, capacity, collisionSys, renderBuffer, ref overlapResult, ref primaryOverlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, residueCollided, triggerEditorIdToLocalId, triggerEditorIdToConfigFromTiled, trapLocalIdToColliderAttrs, completelyStaticTrapColliders, unconfirmedBattleResult, ref confirmedBattleResult, pushbackFrameLogBuffer, frameLogEnabled, TERMINATING_RENDER_FRAME_ID, false, out hasIncorrectlyPredictedRenderFrame, historyRdfHolder, missionEvtSubId, MAGIC_JOIN_INDEX_INVALID, joinIndexRemap, ref justTriggeredStoryPointId, ref justTriggeredBgmId, justDeadJoinIndices, out fulfilledTriggerSetMask, ref selfNotEnoughMp, loggerBridge);
            curDynamicsRenderFrameId++;
        }
    }

    ~Room() {
        joinerLock.Dispose();
        inputBufferLock.Dispose();
    }
}
