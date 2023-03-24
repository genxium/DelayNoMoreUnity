namespace backend.Storage;
public interface IAuthTokenCache {
    public bool getPlayerId(string token, out int? playerId);
    public bool setPlayerLoginRecord(string token, int playerId);
}
