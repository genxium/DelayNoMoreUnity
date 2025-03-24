namespace backend.Storage;
public interface IAuthTokenCache {
    public bool GenerateNewLoginRecord(string playerId, out string? newToken, out DateTimeOffset absoluteExpiryTime);
    public bool ValidateToken(string token, string proposedPlayerId);
    public (bool, string?) ValidateTokenAndRetrieveUname(string token, string proposedPlayerId);
}
