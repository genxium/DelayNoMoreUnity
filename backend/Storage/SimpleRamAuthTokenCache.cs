using Microsoft.Extensions.Caching.Memory;

namespace backend.Storage;
public class SimpleRamAuthTokenCache : IAuthTokenCache {

    private readonly ILogger<SimpleRamAuthTokenCache> _logger;

    private MemoryCache inRamCache { get; } = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 2048
        });

    public SimpleRamAuthTokenCache(ILogger<SimpleRamAuthTokenCache> logger) {
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
