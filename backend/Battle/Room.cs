using shared;
using static shared.Battle;
using System.Net.WebSockets;
using System.Net.Sockets;

namespace backend.Battle;
public class Room {

    public int id;
    public int capacity;

    int renderFrameId;
    int battleDurationFrames;
    int nstDelayFrames;

    Dictionary<int, Player> players;
    Player[] playersArr; // ordered by joinIndex

    /**
		 * The following `PlayerDownsyncSessionDict` is NOT individually put
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
    Dictionary<int, CancellationTokenSource> playerSignalToCloseDict;
    Dictionary<int, PlayerSessionAckWatchdog> playerActiveWatchdogDict;
    RoomBattleState state;
    int effectivePlayerCount;

    FrameRingBuffer<InputFrameDownsync> inputBuffer; // Indices are STRICTLY consecutive

    Mutex inputBufferLock;         // Guards [InputsBuffer, LatestPlayerUpsyncedInputFrameId, LastAllConfirmedInputFrameId, LastAllConfirmedInputList, LastAllConfirmedInputFrameIdWithChange, LastIndividuallyConfirmedInputFrameId, LastIndividuallyConfirmedInputList, player.LastConsecutiveRecvInputFrameId]

    Mutex joinerLock;         // Guards [AddPlayerIfPossible, ReAddPlayerIfPossible, OnPlayerDisconnected, onDismissed, effectivePlayerCount]
    int latestPlayerUpsyncedInputFrameId;
    ulong[] lastAllConfirmedInputList;
    bool[] joinIndexBooleanArr;

    bool backendDynamicsEnabled;

    long dilutedRollbackEstimatedDtNanos;

    int[] lastIndividuallyConfirmedInputFrameId;
    ulong[] lastIndividuallyConfirmedInputList;

    Mutex battleUdpTunnelLock;
    PeerUdpAddr? battleUdpTunnelAddr;
    UdpClient? battleUdpTunnel;

    ILoggerFactory _loggerFactory;
    ILogger<Room> _logger;
    public Room(ILoggerFactory loggerFactory, int roomId, int roomCapacity) {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<Room>();
        id = roomId;
        capacity = roomCapacity;
        renderFrameId = 0;
        battleDurationFrames = 10 * 60;
        nstDelayFrames = 24;
        state = RoomBattleState.Idle;
        effectivePlayerCount = 0;
        backendDynamicsEnabled = false;

        int renderBufferSize = 1024;
        inputBuffer = new FrameRingBuffer<InputFrameDownsync>((renderBufferSize >> 1) + 1);
        players = new Dictionary<int, Player>();
        playersArr = new Player[capacity];

        playerActiveWatchdogDict = new Dictionary<int, PlayerSessionAckWatchdog>();
        playerDownsyncSessionDict = new Dictionary<int, WebSocket>();
        playerSignalToCloseDict = new Dictionary<int, CancellationTokenSource>();

        latestPlayerUpsyncedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
        lastAllConfirmedInputList = new ulong[capacity];
        joinIndexBooleanArr = new bool[capacity];

        lastIndividuallyConfirmedInputFrameId = new int[capacity];
        lastIndividuallyConfirmedInputList = new ulong[capacity];

        joinerLock = new Mutex();
        inputBufferLock = new Mutex();
        battleUdpTunnelLock = new Mutex();
    }

    public int AddPlayerIfPossible(Player pPlayerFromDbInit, int playerId, int speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer) {
        joinerLock.WaitOne();
        try {
            if (RoomBattleState.Idle != state && RoomBattleState.Waiting != state) {
                return ErrCode.PlayerNotAddableToRoom;
            }

            if (players.ContainsKey(playerId)) {
                return ErrCode.SamePlayerAlreadyInSameRoom;
            }

            pPlayerFromDbInit.AckingFrameId = -1;
            pPlayerFromDbInit.AckingInputFrameId = -1;
            pPlayerFromDbInit.LastSentInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
            pPlayerFromDbInit.LastConsecutiveRecvInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
            pPlayerFromDbInit.BattleState = Player.PlayerBattleState.ADDED_PENDING_BATTLE_COLLIDER_ACK;

            pPlayerFromDbInit.PlayerDownsync = new PlayerDownsync();
            pPlayerFromDbInit.PlayerDownsync.SpeciesId = speciesId;
            pPlayerFromDbInit.PlayerDownsync.ColliderRadius = DEFAULT_PLAYER_RADIUS; // Hardcoded
            pPlayerFromDbInit.PlayerDownsync.InAir = true;                           // Hardcoded

            players[playerId] = pPlayerFromDbInit;

            playerDownsyncSessionDict[playerId] = session;
            playerSignalToCloseDict[playerId] = signalToCloseConnOfThisPlayer;

            var newWatchdog = new PlayerSessionAckWatchdog(5000, signalToCloseConnOfThisPlayer, String.Format("[ RoomId={0}, PlayerId={1} ] session watchdog ticked.", id, playerId), _loggerFactory);
            playerActiveWatchdogDict[playerId] = newWatchdog;

            effectivePlayerCount++;

            if (1 == effectivePlayerCount) {
                state = RoomBattleState.Waiting;
            }

            for (int i = 0; i < capacity; i++) {
                if (joinIndexBooleanArr[i]) continue;
                var targetPlayer = players[playerId];
                if (null == targetPlayer) continue;
                if (null == targetPlayer.PlayerDownsync) continue;
                joinIndexBooleanArr[i] = true;
                targetPlayer.PlayerDownsync.JoinIndex = i + 1;
                var chosenCh = characters[targetPlayer.PlayerDownsync.SpeciesId];
                targetPlayer.PlayerDownsync.Speed = chosenCh.Speed;
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
            var thatPlayer = players[playerId];
            if (players.ContainsKey(playerId)) {
                var thatPlayerBattleState = thatPlayer.BattleState;
                switch (thatPlayerBattleState) {
                    case Player.PlayerBattleState.DISCONNECTED:
                    case Player.PlayerBattleState.LOST:
                    case Player.PlayerBattleState.EXPELLED_DURING_GAME:
                    case Player.PlayerBattleState.EXPELLED_IN_DISMISSAL:
                        _logger.LogInformation("Room OnPlayerDisconnected[early return #1] [ roomId={0}, playerId={1}, playerBattleState={2}, nowRoomBattleState={3}, nowRoomEffectivePlayerCount={4} ]", id, playerId, thatPlayerBattleState, state, effectivePlayerCount);
                        return;
                    default:
                        break;
                }
            } else {
                _logger.LogInformation("Room OnPlayerDisconnected[early return #2] [ roomId={0}, playerId={1} doesn't exist! nowRoomBattleState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                return;
            }

            switch (state) {
                case RoomBattleState.Waiting:
                    clearPlayerNetworkSession(playerId);
                    effectivePlayerCount--;
                    joinIndexBooleanArr[thatPlayer.PlayerDownsync.JoinIndex - 1] = false;

                    players.Remove(playerId);
                    if (0 == effectivePlayerCount) {
                        state = RoomBattleState.Idle;
                    }
                    _logger.LogWarning("OnPlayerDisconnected finished: [ roomId={0}, playerId={1}, nowBattleState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                    break;
                default:
                    thatPlayer.BattleState = Player.PlayerBattleState.DISCONNECTED;
                    clearPlayerNetworkSession(playerId);
                    _logger.LogWarning("OnPlayerDisconnected finished: [ roomId={0}, playerId={1}, nowBattleState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                    break;
            }
        } finally {
            joinerLock.ReleaseMutex();
        }
    }

    private void clearPlayerNetworkSession(int playerId) {
        if (playerDownsyncSessionDict.ContainsKey(playerId)) {
            // [WARNING] No need to close "pR.PlayerDownsyncChanDict[playerId]" immediately!
            if (playerActiveWatchdogDict.ContainsKey(playerId)) {
                if (null != playerActiveWatchdogDict[playerId]) {
                    playerActiveWatchdogDict[playerId].Stop();
                }
                playerActiveWatchdogDict.Remove(playerId);
            }
            playerDownsyncSessionDict.Remove(playerId);
            if (playerSignalToCloseDict.ContainsKey(playerId)) {
                playerSignalToCloseDict.Remove(playerId);
            }
            _logger.LogInformation("clearPlayerNetworkSession finished: [ roomId={0}, playerId={1}, nowBattleState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
        }
    }

    public void onDismissed() {
        joinerLock.WaitOne();
        try {
            players = new Dictionary<int, Player>();
            playersArr = new Player[capacity];
            foreach (var item in playerActiveWatchdogDict) {
                if (null == item.Value) continue;
                item.Value.Stop();
            }
            playerActiveWatchdogDict = new Dictionary<int, PlayerSessionAckWatchdog>(); // Would allow the destructor of each "Watchdog" value to dispose its timer  
            playerDownsyncSessionDict = new Dictionary<int, WebSocket>();
            playerSignalToCloseDict = new Dictionary<int, CancellationTokenSource>();
            state = RoomBattleState.Idle;
            effectivePlayerCount = 0;

            int oldInputBufferSize = inputBuffer.N;
            inputBuffer = new FrameRingBuffer<InputFrameDownsync>(oldInputBufferSize);

            latestPlayerUpsyncedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
            lastAllConfirmedInputList = new ulong[capacity];
            joinIndexBooleanArr = new bool[capacity];

            backendDynamicsEnabled = false;

            lastIndividuallyConfirmedInputFrameId = new int[capacity];
            lastIndividuallyConfirmedInputList = new ulong[capacity];

            battleUdpTunnelLock.WaitOne();
            battleUdpTunnelAddr = null;
            battleUdpTunnel = null;
            battleUdpTunnelLock.ReleaseMutex();
        } finally {
            joinerLock.ReleaseMutex();
        }
    }

    public float calRoomScore() {
        var x = ((float)effectivePlayerCount) / capacity;
        var d = (x - 0.5f); // Such that when the room is half-full, the score is at minimum 
        var d2 = d * d;
        return 7.8125f*d2 - 5.0f + (float)(state);
    }

    ~Room() {
        joinerLock.Dispose();
        inputBufferLock.Dispose();
        battleUdpTunnelLock.Dispose();
    }
}
