using Microsoft.Extensions.Caching.Memory;

namespace backend.Storage;
public class TokenCache : ISimpleCache {

    private readonly ILogger<TokenCache> _logger;

    private MemoryCache inRamCache { get; } = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 2048
        });
    public TokenCache(ILogger<TokenCache> logger) {
        _logger = logger;
    }

    public bool getPlayerId(string token, out int? playerId) {
        _logger.LogInformation("Getting playerId by token={0}", token);
        playerId = null;
        return inRamCache.TryGetValue<int?>(token, out playerId);
    }

    public bool setPlayerLoginRecord(string token, int playerId) {
        inRamCache.Set<int>(token, playerId, TimeSpan.FromMinutes(2));
        return true;
    }

}
