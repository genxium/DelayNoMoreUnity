namespace backend.Storage;
public interface ISimpleCache {
    public bool getPlayerId(string token, out int? playerId);
    public bool setPlayerLoginRecord(string token, int playerId);
}
