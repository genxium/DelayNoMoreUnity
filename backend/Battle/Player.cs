namespace backend.Battle;

using System.Net;
using shared;

public class Player {
    // other in-battle info fields

    public int LastConsecutiveRecvInputFrameId;

    public int LastSentInputFrameId;

    public int AckingFrameId;

    public int AckingInputFrameId;

    public long BattleState;
    public PlayerDownsync PlayerDownsync;

    public IPEndPoint? BattleUdpTunnelAddr;
    public int BattleUdpTunnelAuthKey;

    public Player(PlayerDownsync playerDownsync) {
        BattleState = Battle.PLAYER_BATTLE_STATE_IMPOSSIBLE;
        PlayerDownsync = playerDownsync;
    }

}
