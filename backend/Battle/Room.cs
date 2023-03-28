using shared;
using static shared.Battle;
using System.Net.WebSockets;
using System.Net.Sockets;
using backend.Battle;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System.Diagnostics.Metrics;
using static backend.Battle.Room;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Runtime.Intrinsics.X86;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace backend.Battle;
public class Room {
    public enum RoomBattleState {
        IDLE = 0,
        WAITING = -1,
        PREPARE = 10000000,
        IN_BATTLE = 10000001,
        STOPPING_BATTLE_FOR_SETTLEMENT = 10000002,
        IN_SETTLEMENT = 10000003,
        IN_DISMISSAL = 10000004
    }

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
    Dictionary<int, WebSocket?> playerDownsyncSessionDict;
    Dictionary<int, CancellationTokenSource?> playerSignalToCloseDict;
    Dictionary<int, Watchdog?> playerActiveWatchdogDict;
    RoomBattleState state;
    int effectivePlayerCount;

    FrameRingBuffer<InputFrameDownsync> inputBuffer; // Indices are STRICTLY consecutive

    Mutex inputBufferLock;         // Guards [InputsBuffer, LatestPlayerUpsyncedInputFrameId, LastAllConfirmedInputFrameId, LastAllConfirmedInputList, LastAllConfirmedInputFrameIdWithChange, LastIndividuallyConfirmedInputFrameId, LastIndividuallyConfirmedInputList, player.LastConsecutiveRecvInputFrameId]
    int LatestPlayerUpsyncedInputFrameId;
    ulong[] LastAllConfirmedInputList;
    bool[] joinIndexBooleanArr;

    bool backendDynamicsEnabled;

    long dilutedRollbackEstimatedDtNanos;

    int[] lastIndividuallyConfirmedInputFrameId;
    ulong[] lastIndividuallyConfirmedInputList;

    Mutex battleUdpTunnelLock;
    PeerUdpAddr? battleUdpTunnelAddr;
    UdpClient? battleUdpTunnel;

    ILogger<Room> _logger;
    public Room(ILoggerFactory loggerFactory, int roomId, int roomCapacity) {
        _logger = loggerFactory.CreateLogger<Room>();
        id = roomId;
        capacity = roomCapacity;
        onDismissed();
    }

    public int AddPlayerIfPossible(Player pPlayerFromDbInit, int playerId, int speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer) {
        if (RoomBattleState.IDLE != state && RoomBattleState.WAITING != state) {
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

        var signalToCloseConnOfThisPlayerCapture = signalToCloseConnOfThisPlayer;
        var newWatchdog = new Watchdog(5000, new TimerCallback((timerState) => {
            signalToCloseConnOfThisPlayerCapture.Cancel();
        }));
        newWatchdog.Kick();
        playerActiveWatchdogDict[playerId] = newWatchdog;

        return ErrCode.Ok;
    }

    public void OnDisconnected() {

    }

    public void onDismissed() {
        renderFrameId = 0;
        battleDurationFrames = 10*60;
        nstDelayFrames = 24;

        players = new Dictionary<int, Player>();
        playersArr = new Player[capacity];
        foreach (var item in playerActiveWatchdogDict) {
            if (null == item.Value) continue;
            item.Value.Stop();
        }
        playerActiveWatchdogDict = new Dictionary<int, Watchdog?>(); // Would allow the destructor of each "Watchdog" value to dispose its timer  
        playerDownsyncSessionDict = new Dictionary<int, WebSocket?>();
        playerSignalToCloseDict = new Dictionary<int, CancellationTokenSource?>();
        state = RoomBattleState.IDLE;
        effectivePlayerCount = 0;

        int renderBufferSize = 1024;
        inputBuffer = new FrameRingBuffer<InputFrameDownsync>((renderBufferSize >> 1) + 1);

  
        LatestPlayerUpsyncedInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
        LastAllConfirmedInputList = new ulong[capacity];
        joinIndexBooleanArr = new bool[capacity];

        backendDynamicsEnabled = false;

        lastIndividuallyConfirmedInputFrameId = new int[capacity];
        lastIndividuallyConfirmedInputList = new ulong[capacity];

        if (null != inputBufferLock) {
            inputBufferLock.Dispose();
        }
        inputBufferLock = new Mutex();

        if (null != battleUdpTunnelLock) {
            battleUdpTunnelLock.WaitOne();
            battleUdpTunnelAddr = null;
            battleUdpTunnel = null;
            battleUdpTunnelLock.ReleaseMutex();
            battleUdpTunnelLock.Dispose();
        }
        battleUdpTunnelLock = new Mutex();
    }

    void onPlayerAdded(int playerId) {
        this.effectivePlayerCount++;

        if (1 == effectivePlayerCount) {
            this.state = RoomBattleState.WAITING;
        }

        for (int i = 0; i < capacity; i++) {
            if (!joinIndexBooleanArr[i]
                &&
                null != players[playerId]
                &&
                null != players[playerId].PlayerDownsync) {
                players[playerId].PlayerDownsync.JoinIndex = i + 1;
                joinIndexBooleanArr[i] = true;
                var chosenCh = characters[players[playerId].PlayerDownsync.SpeciesId];
                players[playerId].PlayerDownsync.Speed = chosenCh.Speed;
            }
        }
    }

    ~Room() {
        inputBufferLock.Dispose();
        battleUdpTunnelLock.Dispose();
    }
}
