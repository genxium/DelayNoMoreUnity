namespace backend.Battle;

using System.Net;
using shared;

public class Player {
    // other in-battle info fields
    public string Id;

    public int LastConsecutiveRecvInputFrameId;

    public int LastSentInputFrameId;

    public int AckingFrameId;

    public int AckingInputFrameId;

    public long BattleState;
    public CharacterDownsync CharacterDownsync;

    public IPEndPoint? BattleUdpTunnelAddr;
    public int BattleUdpTunnelAuthKey;

    public Player(string id, CharacterDownsync chrc) {
        BattleState = Battle.PLAYER_BATTLE_STATE_IMPOSSIBLE;
        Id = id;
        CharacterDownsync = chrc;
    }
}
