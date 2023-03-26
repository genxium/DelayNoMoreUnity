using shared;
using System.Net.WebSockets;
using System.Net.Sockets;

namespace backend.Battle;
public class Room {
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
    Dictionary<int, Action> PlayerSignalToCloseDict;
    Dictionary<int, Watchdog> PlayerActiveWatchdogDict;
    int State;
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