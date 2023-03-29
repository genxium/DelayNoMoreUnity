namespace backend.Battle;

using System;
using shared;

public class Player {
    // other in-battle info fields

    public int LastConsecutiveRecvInputFrameId;

    public int LastSentInputFrameId;

    public int AckingFrameId;

    public int AckingInputFrameId;

    public enum PlayerBattleState {
        IMPOSSIBLE = -2,
        ADDED_PENDING_BATTLE_COLLIDER_ACK = 0,
        READDED_PENDING_BATTLE_COLLIDER_ACK = 1,
        READDED_BATTLE_COLLIDER_ACKED = 2,
        ACTIVE = 3,
        DISCONNECTED = 4,
        LOST = 5,
        EXPELLED_DURING_GAME = 6,
        EXPELLED_IN_DISMISSAL = 7
    }

    public PlayerBattleState BattleState;
    public PlayerDownsync PlayerDownsync;

    public Player(PlayerDownsync playerDownsync) {
        BattleState = PlayerBattleState.IMPOSSIBLE;
        PlayerDownsync = playerDownsync;
    }
}