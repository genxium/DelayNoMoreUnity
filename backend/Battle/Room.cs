using shared;
using static shared.Battle;
using System.Net.WebSockets;
using System.Net.Sockets;
using Google.Protobuf;
using Pbc = Google.Protobuf.Collections;

namespace backend.Battle;
public class Room {

    public int id;
    public int capacity;
    public int battleDurationFrames;
    public int estimatedMillisPerFrame;

    public string stageName;
    public int inputFrameUpsyncDelayTolerance;
    public int maxChasingRenderFramesPerUpdate;
    public bool frameDataLoggingEnabled;

    int renderFrameId;
    int curDynamicsRenderFrameId;
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
    long state;
    int effectivePlayerCount;

    FrameRingBuffer<InputFrameDownsync> inputBuffer; // Indices are STRICTLY consecutive

    Mutex inputBufferLock;         // Guards [inputBuffer, latestPlayerUpsyncedInputFrameId, lastAllConfirmedInputFrameId, lastAllConfirmedInputList, lastAllConfirmedInputFrameIdWithChange, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, player.LastConsecutiveRecvInputFrameId]

    Mutex joinerLock;         // Guards [AddPlayerIfPossible, ReAddPlayerIfPossible, OnPlayerDisconnected, dismiss, effectivePlayerCount]
    int lastAllConfirmedInputFrameId;
    int lastAllConfirmedInputFrameIdWithChange;
    int latestPlayerUpsyncedInputFrameId;
    ulong[] lastAllConfirmedInputList;
    bool[] joinIndexBooleanArr;

    bool backendDynamicsEnabled;

    int[] lastIndividuallyConfirmedInputFrameId;
    ulong[] lastIndividuallyConfirmedInputList;

    Mutex battleUdpTunnelLock;
    public PeerUdpAddr? battleUdpTunnelAddr;
    UdpClient? battleUdpTunnel;

    ILoggerFactory _loggerFactory;
    ILogger<Room> _logger;
    public Room(ILoggerFactory loggerFactory, int roomId, int roomCapacity) {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<Room>();
        id = roomId;
        capacity = roomCapacity;
        renderFrameId = 0;
        curDynamicsRenderFrameId = 0;
        battleDurationFrames = 10 * 60;
        estimatedMillisPerFrame = 17; // ceiling 16.66667 to dilute the framerate on server 
        stageName = "Dungeon";
        inputFrameUpsyncDelayTolerance = ConvertToNoDelayInputFrameId(nstDelayFrames) - 1; // this value should be strictly smaller than (NstDelayFrames >> InputScaleFrames), otherwise "type#1 forceConfirmation" might become a lag avalanche
        maxChasingRenderFramesPerUpdate = 9; // Don't set this value too high to avoid exhausting frontend CPU within a single frame, roughly as the "turn-around frames to recover" is empirically OK                                                    

        nstDelayFrames = 24;
        state = ROOM_STATE_IDLE;
        effectivePlayerCount = 0;
        backendDynamicsEnabled = false;

        int renderBufferSize = 1024;
        inputBuffer = new FrameRingBuffer<InputFrameDownsync>((renderBufferSize >> 1) + 1);
        players = new Dictionary<int, Player>();
        playersArr = new Player[capacity];

        playerActiveWatchdogDict = new Dictionary<int, PlayerSessionAckWatchdog>();
        playerDownsyncSessionDict = new Dictionary<int, WebSocket>();
        playerSignalToCloseDict = new Dictionary<int, CancellationTokenSource>();

        lastAllConfirmedInputFrameId = TERMINATING_INPUT_FRAME_ID;
        latestPlayerUpsyncedInputFrameId = TERMINATING_INPUT_FRAME_ID;
        lastAllConfirmedInputList = new ulong[capacity];
        joinIndexBooleanArr = new bool[capacity];

        lastIndividuallyConfirmedInputFrameId = new int[capacity];
        lastIndividuallyConfirmedInputList = new ulong[capacity];

        joinerLock = new Mutex();
        inputBufferLock = new Mutex();
        battleUdpTunnelLock = new Mutex();
    }

    public int GetRenderCacheSize() {
        return ((inputBuffer.N - 1) << 1);
    }

    public int AddPlayerIfPossible(Player pPlayerFromDbInit, int playerId, int speciesId, WebSocket session, CancellationTokenSource signalToCloseConnOfThisPlayer) {
        joinerLock.WaitOne();
        try {
            if (ROOM_STATE_IDLE != state && ROOM_STATE_WAITING != state) {
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

            pPlayerFromDbInit.PlayerDownsync = new PlayerDownsync();
            pPlayerFromDbInit.PlayerDownsync.Id = playerId;
            pPlayerFromDbInit.PlayerDownsync.SpeciesId = speciesId;
            pPlayerFromDbInit.PlayerDownsync.ColliderRadius = DEFAULT_PLAYER_RADIUS; // Hardcoded
            pPlayerFromDbInit.PlayerDownsync.InAir = true;                           // Hardcoded

            players[playerId] = pPlayerFromDbInit;

            playerDownsyncSessionDict[playerId] = session;
            playerSignalToCloseDict[playerId] = signalToCloseConnOfThisPlayer;

            var newWatchdog = new PlayerSessionAckWatchdog(10000, signalToCloseConnOfThisPlayer, String.Format("[ RoomId={0}, PlayerId={1} ] session watchdog ticked.", id, playerId), _loggerFactory);
            newWatchdog.Stop();
            playerActiveWatchdogDict[playerId] = newWatchdog;

            effectivePlayerCount++;

            if (1 == effectivePlayerCount) {
                state = ROOM_STATE_WAITING;
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

            PlayerSessionAckWatchdog? watchdog;
            if (playerActiveWatchdogDict.TryGetValue(playerId, out watchdog)) {
                watchdog.Kick();
            }

            switch (state) {
                case ROOM_STATE_WAITING:
                    clearPlayerNetworkSession(playerId);
                    effectivePlayerCount--;
                    joinIndexBooleanArr[thatPlayer.PlayerDownsync.JoinIndex - 1] = false;

                    players.Remove(playerId);
                    if (0 == effectivePlayerCount) {
                        state = ROOM_STATE_IDLE;
                    }
                    _logger.LogWarning("OnPlayerDisconnected finished: [ roomId={0}, playerId={1}, RoomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
                    break;
                default:
                    Interlocked.Exchange(ref thatPlayer.BattleState, PLAYER_BATTLE_STATE_DISCONNECTED);
                    clearPlayerNetworkSession(playerId);
                    _logger.LogWarning("OnPlayerDisconnected finished: [ roomId={0}, playerId={1}, RoomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
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
            _logger.LogInformation("clearPlayerNetworkSession finished: [ roomId={0}, playerId={1}, RoomState={2}, nowRoomEffectivePlayerCount={3} ]", id, playerId, state, effectivePlayerCount);
        }
    }

    public float calRoomScore() {
        var x = ((float)effectivePlayerCount) / capacity;
        var d = (x - 0.5f); // Such that when the room is half-full, the score is at minimum 
        var d2 = d * d;
        return 7.8125f * d2 - 5.0f + (float)(state);
    }

    private Pbc.RepeatedField<PlayerDownsync> clonePlayersArrToPb() {
        var bridgeArr = new PlayerDownsync[players.Count]; // RepeatedField doesn't have a constructor to preallocate by size
        foreach (var (playerId, player) in players) {
            bridgeArr[player.PlayerDownsync.JoinIndex - 1] = player.PlayerDownsync.Clone();
        }
        var ret = new Pbc.RepeatedField<PlayerDownsync> {
            bridgeArr
        };
        return ret;
    }

    public async Task<bool> OnPlayerBattleColliderAcked(int targetPlayerId) {
        Player? targetPlayer;
        if (!players.TryGetValue(targetPlayerId, out targetPlayer)) {
            return false;
        }
        bool shouldTryToStartBattle = true;
        var targetPlayerBattleState = Interlocked.Read(ref targetPlayer.BattleState);
        _logger.LogInformation("OnPlayerBattleColliderAcked-before: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);
        switch (targetPlayerBattleState) {
            case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
                var playerAckedFrame = new RoomDownsyncFrame {
                    Id = renderFrameId
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

                        tList.Add(sendSafelyAsync(playerAckedFrame, null, DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED, thatPlayerId, MAGIC_JOIN_INDEX_DEFAULT));
                    }
                }
                await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
                Interlocked.Exchange(ref targetPlayer.BattleState, PLAYER_BATTLE_STATE_ACTIVE);
                break;
            default:
                break;
        }

        _logger.LogInformation("OnPlayerBattleColliderAcked-post-downsync: roomId={0}, roomState={1}, targetPlayerId={2}, targetPlayerBattleState={3}, capacity={4}, effectivePlayerCount={5}", id, state, targetPlayerId, targetPlayerBattleState, capacity, effectivePlayerCount);

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
                    await startBattleAsync(); // WON'T run if the battle state is not in WAITING.
                }
            }
        }
        return true;
    }

    private async Task startBattleAsync() {
        var nowRoomState = Interlocked.Read(ref state);
        if (ROOM_STATE_WAITING != nowRoomState) {
            _logger.LogWarning("[StartBattle] Battle not started due to not being WAITING: roomId={0}, roomState={1}", id, state);
            return;
        }

        renderFrameId = 0;

        // Initialize the "collisionSys" as well as "RenderFrameBuffer"
        curDynamicsRenderFrameId = 0;

        Interlocked.Exchange(ref state, ROOM_STATE_PREPARE);

        foreach (var (_, player) in players) {
            int joinIndex = player.PlayerDownsync.JoinIndex;
            playersArr[joinIndex - 1] = player;
        }

        _logger.LogWarning("Battle state transitted to ROOM_STATE_PREPARE for roomId={0}", id);

        var battleReadyToStartFrame = new RoomDownsyncFrame {
            Id = DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START
        };
        battleReadyToStartFrame.PlayersArr.AddRange(clonePlayersArrToPb());

        _logger.LogWarning("Sending out frame for ROOM_STATE_PREPARE: {0}", battleReadyToStartFrame);

        var tList = new List<Task>();
        foreach (var (playerId, player) in players) {
            tList.Add(sendSafelyAsync(battleReadyToStartFrame, null, DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START, playerId, MAGIC_JOIN_INDEX_DEFAULT));
        }
        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel

        await Task.Delay(3000);

        /**
		  [WARNING] We actually need the "battleMainLoop" immediately switch into another thread for running, such that we can avoid putting an unevenly heavy load on the current thread (i.e. which is of a specific player session)! See "GOROUTINE_TO_ASYNC_TASK.md" for more information.

          Moreover, I'm deliberately NOT AWAITING here, because the execution of "OnPlayerBattleColliderAcked > startBattleAsync" should continue without the result of "battleMainLoopActionAsync"!
		 */
        _ = Task.Run(battleMainLoopActionAsync);
    }

    public async Task SettleBattleAsync() {
        var nowRoomState = Interlocked.Read(ref state);
        if (ROOM_STATE_IN_BATTLE != nowRoomState && ROOM_STATE_PREPARE != nowRoomState && ROOM_STATE_WAITING != nowRoomState) {
            return;
        }
        battleUdpTunnelLock.WaitOne();
        if (null != battleUdpTunnel) {
            battleUdpTunnel.Close();
        }
        battleUdpTunnelLock.ReleaseMutex();

        Interlocked.Exchange(ref state, ROOM_STATE_STOPPING_BATTLE_FOR_SETTLEMENT);

        _logger.LogInformation("Stopping the `battleMainLoop` for: roomId={0}", id);
        var assembledFrame = new RoomDownsyncFrame {
            Id = renderFrameId
        };

        var tList = new List<Task>();
        // It's important to send kickoff frame iff  "0 == renderFrameId && nextRenderFrameId > renderFrameId", otherwise it might send duplicate kickoff frames
        foreach (var (playerId, player) in players) {
            var thatPlayerBattleState = Interlocked.Read(ref player.BattleState); // Might be changed in "OnPlayerDisconnected/OnPlayerLost" from other threads

            // [WARNING] DON'T try to send any message to an inactive player!
            switch (thatPlayerBattleState) {
                case PLAYER_BATTLE_STATE_DISCONNECTED:
                case PLAYER_BATTLE_STATE_LOST:
                case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                    continue;
            }

            tList.Add(sendSafelyAsync(assembledFrame, null, DOWNSYNC_MSG_ACT_BATTLE_STOPPED, playerId, MAGIC_JOIN_INDEX_DEFAULT));
        }
        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel

        Interlocked.Exchange(ref state, ROOM_STATE_IN_SETTLEMENT);
        _logger.LogInformation("The room is in settlement: roomId={0}", id);
    }

    private void dismiss() {
        joinerLock.WaitOne();
        try {
            var nowRoomState = Interlocked.Read(ref state);
            if (ROOM_STATE_IN_SETTLEMENT != nowRoomState) {
                return;
            }

            Interlocked.Exchange(ref state, ROOM_STATE_IN_DISMISSAL);
            foreach (var (_, cancellationTokenSource) in playerSignalToCloseDict) {
                if (!cancellationTokenSource.Token.IsCancellationRequested) {
                    cancellationTokenSource.Cancel();
                }
            }

            players = new Dictionary<int, Player>();
            playersArr = new Player[capacity];
            foreach (var item in playerActiveWatchdogDict) {
                if (null == item.Value) continue;
                item.Value.Stop();
            }
            playerActiveWatchdogDict = new Dictionary<int, PlayerSessionAckWatchdog>(); // Would allow the destructor of each "Watchdog" value to dispose its timer  
            playerDownsyncSessionDict = new Dictionary<int, WebSocket>();
            playerSignalToCloseDict = new Dictionary<int, CancellationTokenSource>();
            state = ROOM_STATE_IDLE;
            effectivePlayerCount = 0;

            int oldInputBufferSize = inputBuffer.N;
            inputBuffer = new FrameRingBuffer<InputFrameDownsync>(oldInputBufferSize);

            lastAllConfirmedInputFrameId = TERMINATING_INPUT_FRAME_ID;
            latestPlayerUpsyncedInputFrameId = TERMINATING_INPUT_FRAME_ID;
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

    private async void battleMainLoopActionAsync() {
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
                        var startRdf = NewPreallocatedRoomDownsyncFrame(capacity, 128);
                        startRdf.Id = shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_START;

                        var tList = new List<Task>();
                        // It's important to send kickoff frame iff  "0 == renderFrameId && nextRenderFrameId > renderFrameId", otherwise it might send duplicate kickoff frames
                        foreach (var (playerId, player) in players) {
                            var thatPlayerBattleState = Interlocked.Read(ref player.BattleState); // Might be changed in "OnPlayerDisconnected/OnPlayerLost" from other threads
                                                                                                  // [WARNING] DON'T try to send any message to an inactive player!
                            switch (thatPlayerBattleState) {
                                case PLAYER_BATTLE_STATE_DISCONNECTED:
                                case PLAYER_BATTLE_STATE_LOST:
                                case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
                                case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
                                    continue;
                            }

                            tList.Add(sendSafelyAsync(startRdf, null, DOWNSYNC_MSG_ACT_BATTLE_START, playerId, MAGIC_JOIN_INDEX_DEFAULT));
                        }
                        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
                        _logger.LogInformation("In `battleMainLoop` for roomId={0} sent out startRdf with {1} bytes", id, startRdf.ToByteArray().Length);
                    }

                    renderFrameId = nextRenderFrameId;

                    var elapsedInCalculation = (nowMillis - stCalculation);
                    toSleepMillis = (int)(estimatedMillisPerFrame - elapsedInCalculation);
                }

                await Task.Delay(toSleepMillis);
            }
        } finally {
            await SettleBattleAsync();
            _logger.LogInformation("The `battleMainLoop` for roomId={0} is settled@renderFrameId={1}", id, renderFrameId);
            dismiss();
            _logger.LogInformation("The `battleMainLoop` for roomId={0} is dismissed@renderFrameId={1}", id, renderFrameId);
        }
    }

    public async Task OnBattleCmdReceived(WsReq pReq, int playerId, bool fromUDP) {
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

        PlayerSessionAckWatchdog? watchdog;
        if (playerActiveWatchdogDict.TryGetValue(playerId, out watchdog)) {
            watchdog.Kick();
        }

        // I've been seeking a totally lock-free approach for this whole operation for a long time, but still it's safer to keep using "mutex inputBufferLock"... 
        Interlocked.Exchange(ref player.AckingFrameId, ackingFrameId);
        Interlocked.Exchange(ref player.AckingInputFrameId, ackingInputFrameId);

        //_logger.LogInformation("OnBattleCmdReceived-inputBufferLock about to lock: roomId={0}, fromPlayerId={1}", id, playerId);
        inputBufferLock.WaitOne();
        try {
            var inputBufferSnapshot = markConfirmationIfApplicable(inputFrameUpsyncBatch, playerId, player, fromUDP);
            if (null != inputBufferSnapshot) {
                await downsyncToAllPlayersAsync(inputBufferSnapshot);
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
                _logger.LogDebug("Omitting obsolete inputFrameUpsync#1: roomId={0}, playerId={1}, clientInputFrameId={2}", id, playerId, clientInputFrameId);
                continue;
            }
            if (clientInputFrameId < player.LastConsecutiveRecvInputFrameId) {
                // [WARNING] It's important for correctness that we use "player.LastConsecutiveRecvInputFrameId" instead of "pR.LastIndividuallyConfirmedInputFrameId[player.JoinIndex-1]" here!
                _logger.LogDebug("Omitting obsolete inputFrameUpsync#2: roomId={0}, playerId={1}, clientInputFrameId={2}, playerLastConsecutiveRecvInputFrameId={3}", id, playerId, clientInputFrameId, player.LastConsecutiveRecvInputFrameId);
                continue;
            }
            if (clientInputFrameId > inputBuffer.EdFrameId) {
                _logger.LogWarning("Dropping too advanced inputFrameUpsync: roomId={0}, playerId={1}, clientInputFrameId={2}, ; is this player cheating?", id, playerId, clientInputFrameId);
                continue;
            }
            // by now "clientInputFrameId <= inputBuffer.EdFrameId"
            var targetInputFrameDownsync = getOrPrefabInputFrameDownsync(clientInputFrameId);
            targetInputFrameDownsync.InputList[player.PlayerDownsync.JoinIndex - 1] = inputFrameUpsync.Encoded;
            targetInputFrameDownsync.ConfirmedList |= ((ulong)1 << (player.PlayerDownsync.JoinIndex - 1));

            if (false == fromUDP) {
                /**
				  [WARNING] We have to distinguish whether or not the incoming batch is from UDP here, otherwise "pR.LatestPlayerUpsyncedInputFrameId - pR.LastAllConfirmedInputFrameId" might become unexpectedly large in case of "UDP packet loss + slow ws session"!

				  Moreover, only ws session upsyncs should advance "player.LastConsecutiveRecvInputFrameId" & "pR.LatestPlayerUpsyncedInputFrameId".

				  Kindly note that the updates of "player.LastConsecutiveRecvInputFrameId" could be discrete before and after reconnection.
				 */
                player.LastConsecutiveRecvInputFrameId = clientInputFrameId;
                if (clientInputFrameId > latestPlayerUpsyncedInputFrameId) {
                    latestPlayerUpsyncedInputFrameId = clientInputFrameId;
                }
            }

            if (clientInputFrameId > lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex - 1]) {
                // No need to update "lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex-1]" only when "true == fromUDP", we should keep "lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex-1] >= player.LastConsecutiveRecvInputFrameId" at any moment.
                lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex - 1] = clientInputFrameId;
                // It's safe (in terms of getting an eventually correct "RenderFrameBuffer") to put the following update of "lastIndividuallyConfirmedInputList" which is ONLY used for prediction in "inputBuffer" out of "false == fromUDP" block.
                lastIndividuallyConfirmedInputList[player.PlayerDownsync.JoinIndex - 1] = inputFrameUpsync.Encoded;
            }
        }

        // Step#2, mark confirmation without forcing
        int newAllConfirmedCount = 0;
        int inputFrameId1 = lastAllConfirmedInputFrameId + 1;
        if (inputFrameId1 < inputBuffer.StFrameId) {
            inputFrameId1 = inputBuffer.StFrameId;
        }
        ulong allConfirmedMask = ((ulong)1 << capacity) - 1;

        for (int inputFrameId = inputFrameId1; inputFrameId < inputBuffer.EdFrameId; inputFrameId++) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId);
            if (false == res1 || null == inputFrameDownsync) {
                throw new ArgumentException(String.Format("inputFrameId={0} doesn't exist for roomId={1}: lastAllConfirmedInputFrameId={2}, inputFrameId1={3}, inputBuffer.StFrameId={4}, inputBuffer.EdFrameId={5}", inputFrameId, id, lastAllConfirmedInputFrameId, inputFrameId1, inputBuffer.StFrameId, inputBuffer.EdFrameId));
            }
            bool shouldBreakConfirmation = false;

            if (allConfirmedMask != inputFrameDownsync.ConfirmedList) {
                foreach (var thatPlayer in playersArr) {
                    var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
                    var thatPlayerJoinMask = ((ulong)1 << (thatPlayer.PlayerDownsync.JoinIndex - 1));
                    bool isSlowTicker = (0 == (inputFrameDownsync.ConfirmedList & thatPlayerJoinMask));
                    bool isActiveSlowTicker = (isSlowTicker && thatPlayerBattleState == PLAYER_BATTLE_STATE_ACTIVE);
                    if (isActiveSlowTicker) {
                        shouldBreakConfirmation = true; // Could be an `ACTIVE SLOW TICKER` here, but no action needed for now
                        break;
                    }
                    _logger.LogDebug("markConfirmationIfApplicable for roomId={0}, skipping UNCONFIRMED BUT INACTIVE player(id:{1}, joinIndex:{2}) while checking inputFrameId=[{3}, {4})", id, thatPlayer.PlayerDownsync.Id, thatPlayer.PlayerDownsync.JoinIndex, inputFrameId1, inputBuffer.EdFrameId);
                }
            }

            if (shouldBreakConfirmation) {
                break;
            }
            newAllConfirmedCount += 1;
            onInputFrameDownsyncAllConfirmed(inputFrameDownsync, INVALID_DEFAULT_PLAYER_ID);
        }

        if (0 < newAllConfirmedCount) {
            /**
			[WARNING]

			If "inputBufferLock" was previously held by "doBattleMainLoopPerTickBackendDynamicsWithProperLocking", then "snapshotStFrameId" would be just (LastAllConfirmedInputFrameId - newAllConfirmedCount).

			However if "inputBufferLock" was previously held by another "OnBattleCmdReceived", the proper value for "snapshotStFrameId" might be smaller than (pR.LastAllConfirmedInputFrameId - newAllConfirmedCount) -- but why? Especially when we've already wrapped this whole function in "inputBufferLock", the order of "markConfirmationIfApplicable" generated snapshots is preserved for sending, isn't (LastAllConfirmedInputFrameId - newAllConfirmedCount) good enough here?

			Unfortunately no, for a reconnected player to get recovered asap (of course with BackendDynamicsEnabled), we put a check of READDED_BATTLE_COLLIDER_ACKED in "downsyncToSinglePlayer" -- which could be called right after "markConfirmationIfApplicable" yet without going through "forceConfirmationIfApplicable" -- and if a READDED_BATTLE_COLLIDER_ACKED player is found there we need a proper "(refRenderFrameId, snapshotStFrameId)" pair for that player!
			*/
            int snapshotStFrameId = (lastAllConfirmedInputFrameId - newAllConfirmedCount);
            if (backendDynamicsEnabled) {
                int refRenderFrameIdIfNeeded = curDynamicsRenderFrameId - 1;
                int refSnapshotStFrameId = ConvertToDelayedInputFrameId(refRenderFrameIdIfNeeded);
                if (refSnapshotStFrameId < snapshotStFrameId) {
                    snapshotStFrameId = refSnapshotStFrameId;
                }
            }
            _logger.LogDebug("markConfirmationIfApplicable for roomId={0} returning newAllConfirmedCount={1}", id, newAllConfirmedCount);
            return produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(0, snapshotStFrameId, lastAllConfirmedInputFrameId + 1);
        } else {
            return null;
        }
    }

    private InputFrameDownsync getOrPrefabInputFrameDownsync(int inputFrameId) {
        /*
		   [WARNING] This function MUST BE called while "inputBufferLock" is locked.
		*/
        var (res1, currInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId);

        if (false == res1 || null == currInputFrameDownsync) {
            currInputFrameDownsync = new InputFrameDownsync {
                InputFrameId = inputFrameId,
                ConfirmedList = 0
            };

            /**
				[WARNING] Don't reference "inputBuffer.GetByFrameId(j-1)" to prefab here!

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
                // Don't predict "btnA & btnB"!
                currInputFrameDownsync.InputList.Add((lastIndividuallyConfirmedInputList[i] & (ulong)15));
            }

            while (inputBuffer.EdFrameId <= inputFrameId) {
                int j = inputBuffer.EdFrameId;
                var cloned = currInputFrameDownsync.Clone();
                cloned.InputFrameId = j;
                inputBuffer.Put(cloned);
            }
            return currInputFrameDownsync;
        } else {
            return currInputFrameDownsync;
        }
    }

    private void onInputFrameDownsyncAllConfirmed(InputFrameDownsync inputFrameDownsync, int playerId) {
        // [WARNING] This function MUST BE called while "inputBufferLock" is locked!
        int inputFrameId = inputFrameDownsync.InputFrameId;
        if (TERMINATING_INPUT_FRAME_ID == lastAllConfirmedInputFrameIdWithChange || false == shared.Battle.EqualInputLists(inputFrameDownsync.InputList, lastAllConfirmedInputList)) {
            if (INVALID_DEFAULT_PLAYER_ID == playerId) {
                _logger.LogDebug("Key inputFrame change: roomId={0}, newInputFrameId={1}, lastInputFrameId={2}, newInputList={2}, lastAllConfirmedInputList={3}", id, inputFrameId, lastAllConfirmedInputFrameId, inputFrameDownsync.InputList, lastAllConfirmedInputList);
            } else {
                _logger.LogDebug("Key inputFrame change: roomId={0}, playerId={1}, newInputFrameId={2}, lastInputFrameId={3}, newInputList={4}, lastAllConfirmedInputList={5}", id, playerId, inputFrameId, lastAllConfirmedInputFrameId, inputFrameDownsync.InputList, lastAllConfirmedInputList);
            }
            lastAllConfirmedInputFrameIdWithChange = inputFrameId;
        }
        lastAllConfirmedInputFrameId = inputFrameId;
        for (int i = 0; i < capacity; i++) {
            lastAllConfirmedInputList[i] = inputFrameDownsync.InputList[i];
        }
        if (INVALID_DEFAULT_PLAYER_ID == playerId) {
            _logger.LogDebug("inputFrame lifecycle#2[forced-allconfirmed]: roomId={0}", id);
        } else {
            _logger.LogDebug("inputFrame lifecycle#2[allconfirmed]: roomId={0}, playerId={1}", id, playerId);
        }
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

    private async Task downsyncToAllPlayersAsync(InputBufferSnapshot inputBufferSnapshot) {
        /*
        [WARNING] This function MUST BE called while "pR.inputBufferLock" is LOCKED to **preserve the order of generation of "inputBufferSnapshot" for sending** -- see comments in "OnBattleCmdReceived" and [this issue](https://github.com/genxium/DelayNoMore/issues/12).
        */
        if (true == backendDynamicsEnabled) {
            foreach (var player in playersArr) {
                var playerBattleState = Interlocked.Read(ref player.BattleState);
                if (PLAYER_BATTLE_STATE_READDED_BATTLE_COLLIDER_ACKED == playerBattleState) {
                    inputBufferSnapshot.ShouldForceResync = true;
                    break;
                }

                /*
                [WARNING] The comment of this part in Golang version is obsolete. The field "ForceAllResyncOnAnyActiveSlowTicker" is always true, and setting "ShouldForceResync = true" here is only going to impact unconfirmed players on frontend, i.e. there's a filter on frontend to ignore "nonSelfForceConfirmation". 
                */
                ulong thatPlayerJoinMask = ((ulong)1 << (player.PlayerDownsync.JoinIndex - 1));

                bool isActiveSlowTicker = (0 < (thatPlayerJoinMask & inputBufferSnapshot.UnconfirmedMask)) && (PLAYER_BATTLE_STATE_ACTIVE == playerBattleState);

                if (isActiveSlowTicker) {
                    inputBufferSnapshot.ShouldForceResync = true;
                    break;
                }
            }
        }

        var tList = new List<Task>();
        foreach (var player in playersArr) {
            /*
               [WARNING] While the order of generation of "inputBufferSnapshot" is preserved for sending, the underlying network I/O blocking action is dispatched to "downsyncLoop of each player" such that "markConfirmationIfApplicable & forceConfirmationIfApplicable" can re-hold "pR.inputBufferLock" asap and proceed with more inputFrameUpsyncs.

               The use of "downsyncLoop of each player" also waives the need of guarding each "pR.PlayerDownsyncSessionDict[playerId]" from multithread-access (e.g. by a "pR.PlayerDownsyncSessionMutexDict[playerId]"), i.e. Gorilla v1.2.0 "conn.WriteMessage" isn't thread-safe https://github.com/gorilla/websocket/blob/v1.2.0/conn.go#L585.
            */
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

            tList.Add(downsyncToSinglePlayerAsync(player.PlayerDownsync.Id, player, inputBufferSnapshot));
        }
        await Task.WhenAll(tList); // Run the async network I/O tasks in parallel
    }

    private async Task sendSafelyAsync(RoomDownsyncFrame? roomDownsyncFrame, Pbc.RepeatedField<InputFrameDownsync>? toSendInputFrameDownsyncs, int act, int playerId, int peerJoinIndex) {
        WebSocket? wsSession;
        CancellationTokenSource? cancellationTokenSource;
        if (!playerDownsyncSessionDict.TryGetValue(playerId, out wsSession)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #1", id, playerId);
            return;
        }

        if (!playerSignalToCloseDict.TryGetValue(playerId, out cancellationTokenSource)) {
            _logger.LogWarning("Ws session for (roomId: {0}, playerId: {1}) doesn't exist! #2", id, playerId);
            return;
        }

        var resp = new WsResp {
            Ret = ErrCode.Ok,
            Act = act,
            Rdf = roomDownsyncFrame,
            PeerJoinIndex = peerJoinIndex
        };
        if (null != toSendInputFrameDownsyncs) {
            resp.InputFrameDownsyncBatch.AddRange(toSendInputFrameDownsyncs);
        }
        await wsSession.SendAsync(new ArraySegment<byte>(resp.ToByteArray()), WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
    }

    private async Task downsyncToSinglePlayerAsync(int playerId, Player player, InputBufferSnapshot inputBufferSnapshot) {
        /*
           [WARNING] This function MUST BE called while "pR.inputBufferLock" is unlocked -- otherwise the network I/O blocking of "sendSafelyAsync" might cause significant lag for "markConfirmationIfApplicable & forceConfirmationIfApplicable"!

           We hereby assume that Golang runtime allocates & frees small amount of RAM quickly enough compared to either network I/O blocking in worst cases or the high frequency "per inputFrameDownsync*player" locking (though "OnBattleCmdReceived" locks at the same frequency but it's inevitable).
        */

        int playerJoinIndexInBooleanArr = player.PlayerDownsync.JoinIndex - 1;
        var playerBattleState = Interlocked.Read(ref player.BattleState);

        switch (playerBattleState) {
            case PLAYER_BATTLE_STATE_DISCONNECTED:
            case PLAYER_BATTLE_STATE_LOST:
            case PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME:
            case PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL:
            case PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK:
            case PLAYER_BATTLE_STATE_READDED_PENDING_BATTLE_COLLIDER_ACK:
                return;
        }

        bool isSlowTicker = (0 < (inputBufferSnapshot.UnconfirmedMask & ((ulong)1 << (playerJoinIndexInBooleanArr))));

        bool shouldResync1 = (PLAYER_BATTLE_STATE_READDED_BATTLE_COLLIDER_ACKED == playerBattleState); // i.e. implies that "MAGIC_LAST_SENT_INPUT_FRAME_ID_READDED == player.LastSentInputFrameId"

        bool shouldResync2 = isSlowTicker;                                                              // This condition is critical, if we don't send resync upon this condition, the "reconnected or slowly-clocking player" might never get its input synced

        bool shouldResync3 = inputBufferSnapshot.ShouldForceResync;

        bool shouldResyncOverall = (shouldResync1 || shouldResync2 || shouldResync3);

        /*
            Resync helps
            1. when player with a slower frontend clock lags significantly behind and thus wouldn't get its inputUpsync recognized due to faster "forceConfirmation"
            2. reconnection
        */
        var toSendInputFrameIdSt = inputBufferSnapshot.ToSendInputFrameDownsyncs[0].InputFrameId;
        var toSendInputFrameIdEd = inputBufferSnapshot.ToSendInputFrameDownsyncs[inputBufferSnapshot.ToSendInputFrameDownsyncs.Count - 1].InputFrameId + 1;

        if (backendDynamicsEnabled && shouldResyncOverall) {
            // TODO
        } else {
            await sendSafelyAsync(null, inputBufferSnapshot.ToSendInputFrameDownsyncs, DOWNSYNC_MSG_ACT_INPUT_BATCH, playerId, MAGIC_JOIN_INDEX_DEFAULT);
        }
        player.LastSentInputFrameId = toSendInputFrameIdEd - 1;

        if (shouldResync1) {
            Interlocked.Exchange(ref player.BattleState, PLAYER_BATTLE_STATE_ACTIVE);
        }
    }

    ~Room() {
        joinerLock.Dispose();
        inputBufferLock.Dispose();
        battleUdpTunnelLock.Dispose();
    }
}
