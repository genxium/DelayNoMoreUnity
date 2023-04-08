using shared;
using static shared.Battle;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Threading;
using Pbc = global::Google.Protobuf.Collections;

namespace backend.Battle;
public class Room {

    public int id;
    public int capacity;
    public int battleDurationFrames;
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

    Mutex joinerLock;         // Guards [AddPlayerIfPossible, ReAddPlayerIfPossible, OnPlayerDisconnected, onDismissed, effectivePlayerCount]
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
            pPlayerFromDbInit.PlayerDownsync.SpeciesId = speciesId;
            pPlayerFromDbInit.PlayerDownsync.ColliderRadius = DEFAULT_PLAYER_RADIUS; // Hardcoded
            pPlayerFromDbInit.PlayerDownsync.InAir = true;                           // Hardcoded

            players[playerId] = pPlayerFromDbInit;

            playerDownsyncSessionDict[playerId] = session;
            playerSignalToCloseDict[playerId] = signalToCloseConnOfThisPlayer;

            var newWatchdog = new PlayerSessionAckWatchdog(10000, signalToCloseConnOfThisPlayer, String.Format("[ RoomId={0}, PlayerId={1} ] session watchdog ticked.", id, playerId), _loggerFactory);
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

    public float calRoomScore() {
        var x = ((float)effectivePlayerCount) / capacity;
        var d = (x - 0.5f); // Such that when the room is half-full, the score is at minimum 
        var d2 = d * d;
        return 7.8125f*d2 - 5.0f + (float)(state);
    }

	public void OnBattleCmdReceived(WsReq pReq, bool fromUDP) {
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
        var expectedOldState = ROOM_STATE_IN_BATTLE;
		var trueOldState = Interlocked.CompareExchange(ref this.state, expectedOldState, expectedOldState);
		if (expectedOldState != trueOldState) {
			return;
		}

        var playerId = pReq.PlayerId;
		var inputFrameUpsyncBatch = pReq.InputFrameUpsyncBatch;
		var ackingFrameId = pReq.AckingFrameId;
		var ackingInputFrameId = pReq.AckingInputFrameId;

        Player? player;
        if (!players.TryGetValue(playerId, out player)) {
            return;
        }

        PlayerSessionAckWatchdog? watchdog;
		if (playerActiveWatchdogDict.TryGetValue(playerId, out watchdog)) {
			watchdog.Kick();
		}

		// I've been seeking a totally lock-free approach for this whole operation for a long time, but still it's safer to keep using "mutex inputBufferLock"... 
		Interlocked.Exchange(ref player.AckingFrameId, ackingFrameId);
		Interlocked.Exchange(ref player.AckingInputFrameId, ackingInputFrameId);

        /*
		_logger.LogInformation("OnBattleCmdReceived-inputBufferLock about to lock: roomId={0}, fromPlayerId={1}", id, playerId);
        inputBufferLock.WaitOne();
		try {
			var inputBufferSnapshot = markConfirmationIfApplicable(inputFrameUpsyncBatch, playerId, player, fromUDP);
			downsyncToAllPlayers(inputBufferSnapshot);
		} finally {
            inputBufferLock.ReleaseMutex();
			_logger.LogInformation("OnBattleCmdReceived-inputBufferLock unlocked: roomId={0}, fromPlayerId={1}", id, playerId);
		}
        */
	}

	private InputBufferSnapshot? markConfirmationIfApplicable(Pbc.RepeatedField<InputFrameUpsync> inputFrameUpsyncBatch, int playerId, Player player, bool fromUDP) {
		// [WARNING] This function MUST BE called while "inputBufferLock" is locked!
		// Step#1, put the received "inputFrameUpsyncBatch" into "inputBuffer"
		foreach (var inputFrameUpsync in inputFrameUpsyncBatch) {
            var clientInputFrameId = inputFrameUpsync.InputFrameId;
			if (clientInputFrameId < inputBuffer.StFrameId) {
                // The updates to "inputBuffer.StFrameId" is monotonically increasing, thus if "clientInputFrameId < inputBuffer.StFrameId" at any moment of time, it is obsolete in the future.
                _logger.LogDebug(String.Format("Omitting obsolete inputFrameUpsync#1: roomId={0}, playerId={1}, clientInputFrameId={2}", id, playerId, clientInputFrameId));
                continue;
			}
			if (clientInputFrameId < player.LastConsecutiveRecvInputFrameId) {
                // [WARNING] It's important for correctness that we use "player.LastConsecutiveRecvInputFrameId" instead of "pR.LastIndividuallyConfirmedInputFrameId[player.JoinIndex-1]" here!
                _logger.LogDebug(String.Format("Omitting obsolete inputFrameUpsync#2: roomId={0}, playerId={1}, clientInputFrameId={2}, playerLastConsecutiveRecvInputFrameId={3}", id, playerId, clientInputFrameId, player.LastConsecutiveRecvInputFrameId));
                continue;
			}
			if (clientInputFrameId > inputBuffer.EdFrameId) {
                _logger.LogWarning(String.Format("Dropping too advanced inputFrameUpsync: roomId={0}, playerId={1}, clientInputFrameId={2}, ; is this player cheating?", id, playerId, clientInputFrameId));
                continue;
			}
            // by now "clientInputFrameId <= inputBuffer.EdFrameId"
            /*
            var targetInputFrameDownsync = getOrPrefabInputFrameDownsync(clientInputFrameId);
            targetInputFrameDownsync.InputList[player.PlayerDownsync.JoinIndex - 1] = inputFrameUpsync.Encoded;
            targetInputFrameDownsync.ConfirmedList |= ((ulong)1 << (player.PlayerDownsync.JoinIndex - 1));
            */
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

			if (clientInputFrameId > lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex-1]) {
                // No need to update "lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex-1]" only when "true == fromUDP", we should keep "lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex-1] >= player.LastConsecutiveRecvInputFrameId" at any moment.
                lastIndividuallyConfirmedInputFrameId[player.PlayerDownsync.JoinIndex - 1] = clientInputFrameId;
                // It's safe (in terms of getting an eventually correct "RenderFrameBuffer") to put the following update of "lastIndividuallyConfirmedInputList" which is ONLY used for prediction in "inputBuffer" out of "false == fromUDP" block.
                lastIndividuallyConfirmedInputList[player.PlayerDownsync.JoinIndex - 1] = inputFrameUpsync.Encoded;
			}
		}

        // Step#2, mark confirmation without forcing
        int newAllConfirmedCount = 0;
        int inputFrameId1 = lastAllConfirmedInputFrameId + 1;
		ulong allConfirmedMask = ((ulong)1 << capacity) - 1;

		for (int inputFrameId = inputFrameId1; inputFrameId < inputBuffer.EdFrameId; inputFrameId++) {
			var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId);
			if (false == res1 || null == inputFrameDownsync) {
				throw new ArgumentException(String.Format("inputFrameId={0} doesn't exist for roomId={1}", inputFrameId, id));
			}
			bool shouldBreakConfirmation = false;

			if (allConfirmedMask != inputFrameDownsync.ConfirmedList) {
				foreach (var thatPlayer in playersArr) {
					var thatPlayerBattleState = Interlocked.Read(ref thatPlayer.BattleState);
					var thatPlayerJoinMask = ((ulong)1 << (thatPlayer.PlayerDownsync.JoinIndex-1));
					bool isSlowTicker = (0 == (inputFrameDownsync.ConfirmedList & thatPlayerJoinMask));
					bool isActiveSlowTicker = (isSlowTicker && thatPlayerBattleState == PLAYER_BATTLE_STATE_ACTIVE);
					if (isActiveSlowTicker) {
						shouldBreakConfirmation = true; // Could be an `ACTIVE SLOW TICKER` here, but no action needed for now
						break;
					}
					_logger.LogDebug(String.Format("markConfirmationIfApplicable for roomId={0}, skipping UNCONFIRMED BUT INACTIVE player(id:{1}, joinIndex:{2}) while checking inputFrameId=[{3}, {4})", id, thatPlayer.PlayerDownsync.Id, thatPlayer.PlayerDownsync.JoinIndex, inputFrameId1, inputBuffer.EdFrameId));
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
			_logger.LogDebug(String.Format("markConfirmationIfApplicable for roomId={0} returning newAllConfirmedCount={1}", id, newAllConfirmedCount));
			return produceInputBufferSnapshotWithCurDynamicsRenderFrameAsRef(0, snapshotStFrameId, lastAllConfirmedInputFrameId+1);
		} else {
            return null;
		}
	}

	private void onInputFrameDownsyncAllConfirmed(InputFrameDownsync inputFrameDownsync, int playerId) {
		// [WARNING] This function MUST BE called while "inputBufferLock" is locked!
		int inputFrameId = inputFrameDownsync.InputFrameId;
		if (TERMINATING_INPUT_FRAME_ID == lastAllConfirmedInputFrameIdWithChange || false == shared.Battle.EqualInputLists(inputFrameDownsync.InputList, lastAllConfirmedInputList)) {
			if (INVALID_DEFAULT_PLAYER_ID == playerId) {
				_logger.LogDebug(String.Format("Key inputFrame change: roomId={0}, newInputFrameId={1}, lastInputFrameId={2}, newInputList={2}, lastAllConfirmedInputList={3}", id, inputFrameId, lastAllConfirmedInputFrameId, inputFrameDownsync.InputList, lastAllConfirmedInputList));
			} else {
				_logger.LogDebug(String.Format("Key inputFrame change: roomId={0}, playerId={1}, newInputFrameId={2}, lastInputFrameId={3}, newInputList={4}, lastAllConfirmedInputList={5}", id, playerId, inputFrameId, lastAllConfirmedInputFrameId, inputFrameDownsync.InputList, lastAllConfirmedInputList));
			}
			lastAllConfirmedInputFrameIdWithChange = inputFrameId;
		}
		lastAllConfirmedInputFrameId = inputFrameId;
		for (int i = 0; i < capacity; i++) {
			lastAllConfirmedInputList[i] = inputFrameDownsync.InputList[i];
		}
		if (INVALID_DEFAULT_PLAYER_ID == playerId) {
			_logger.LogDebug(String.Format("inputFrame lifecycle#2[forced-allconfirmed]: roomId={0}", id));
		} else {
			_logger.LogDebug(String.Format("inputFrame lifecycle#2[allconfirmed]: roomId={0}, playerId={1}", id, playerId));
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

    ~Room() {
        joinerLock.Dispose();
        inputBufferLock.Dispose();
        battleUdpTunnelLock.Dispose();
    }
}
