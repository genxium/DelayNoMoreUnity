using shared;
using static shared.Battle;
using System.Net.WebSockets;
using System.Net.Sockets;
using backend.Battle;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System.Diagnostics.Metrics;
using static backend.Battle.Room;

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

    public int Id;
    public int Capacity;

    int BattleDurationFrames;
    int NstDelayFrames;

    Dictionary<int, Player> Players;
    Player[] PlayersArr; // ordered by joinIndex

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
    Dictionary<int, WebSocket> PlayerDownsyncSessionDict;
    Dictionary<int, CancellationTokenSource> PlayerSignalToCloseDict;
    Dictionary<int, Watchdog> PlayerActiveWatchdogDict;
    RoomBattleState State;
    int EffectivePlayerCount;

    FrameRingBuffer<InputFrameDownsync> InputBuffer; // Indices are STRICTLY consecutive

    Mutex inputBufferLock;         // Guards [InputsBuffer, LatestPlayerUpsyncedInputFrameId, LastAllConfirmedInputFrameId, LastAllConfirmedInputList, LastAllConfirmedInputFrameIdWithChange, LastIndividuallyConfirmedInputFrameId, LastIndividuallyConfirmedInputList, player.LastConsecutiveRecvInputFrameId]
    int LatestPlayerUpsyncedInputFrameId;
    ulong[] LastAllConfirmedInputList;
    bool[] JoinIndexBooleanArr;

    bool BackendDynamicsEnabled;

    long dilutedRollbackEstimatedDtNanos;

    BattleColliderInfo colliderInfo; // Compositing to send centralized ma
    int[] LastIndividuallyConfirmedInputFrameId;
    ulong[] LastIndividuallyConfirmedInputList;

    Mutex battleUdpTunnelLock;
    PeerUdpAddr? battleUdpTunnelAddr;
    UdpClient? battleUdpTunnel;

    ILogger<Room> _logger;
    public Room(ILoggerFactory loggerFactory) {
        _logger = loggerFactory.CreateLogger<Room>();
        inputBufferLock = new Mutex();
        battleUdpTunnelLock = new Mutex();
    }

    public int AddPlayerIfPossible(Player pPlayerFromDbInit, int playerId, int speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer) {
        if (RoomBattleState.IDLE != State && RoomBattleState.WAITING != State) {
            return ErrCode.PlayerNotAddableToRoom;
        }

        if (Players.ContainsKey(playerId)) {
            return ErrCode.SamePlayerAlreadyInSameRoom;
        }

        pPlayerFromDbInit.AckingFrameId = -1;
        pPlayerFromDbInit.AckingInputFrameId = -1;
        pPlayerFromDbInit.LastSentInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
        pPlayerFromDbInit.LastConsecutiveRecvInputFrameId = MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED;
        pPlayerFromDbInit.BattleState = Player.PlayerBattleState.ADDED_PENDING_BATTLE_COLLIDER_ACK;

        pPlayerFromDbInit.PlayerDownsync = new PlayerDownsync();
        pPlayerFromDbInit.PlayerDownsync.ColliderRadius = DEFAULT_PLAYER_RADIUS; // Hardcoded
        pPlayerFromDbInit.PlayerDownsync.InAir = true;                           // Hardcoded

        Players[playerId] = pPlayerFromDbInit;

        PlayerDownsyncSessionDict[playerId] = session;
        PlayerSignalToCloseDict[playerId] = signalToCloseConnOfThisPlayer;

        var signalToCloseConnOfThisPlayerCapture = signalToCloseConnOfThisPlayer;
        var newWatchdog = new Watchdog(5000, new TimerCallback((timerState) => {
            signalToCloseConnOfThisPlayerCapture.Cancel();
        }));
        newWatchdog.Kick();
        PlayerActiveWatchdogDict[playerId] = newWatchdog;

        return ErrCode.Ok;
    }


    public void OnDismissed() {
        inputBufferLock.Dispose();
        inputBufferLock = new Mutex();

        battleUdpTunnelLock.WaitOne();
        battleUdpTunnelAddr = null;
        battleUdpTunnel = null;
        battleUdpTunnelLock.ReleaseMutex();
        battleUdpTunnelLock.Dispose();
        battleUdpTunnelLock = new Mutex();
    }

    ~Room() {
        inputBufferLock.Dispose();
        battleUdpTunnelLock.Dispose();
    }
}