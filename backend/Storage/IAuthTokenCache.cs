namespace backend.Storage;
public interface IAuthTokenCache {
    public bool GenerateNewLoginRecord(int playerId, out string? newToken, out DateTimeOffset absoluteExpiryTime);
    public bool ValidateToken(string token, int proposedPlayerId);
    public (bool, string?) ValidateTokenAndRetrieveUname(string token, int proposedPlayerId);
}
