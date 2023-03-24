using backend.Storage.Dao;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;

namespace backend.Storage;
public class SimpleRamAuthTokenCache : IAuthTokenCache {

    private readonly ILogger<SimpleRamAuthTokenCache> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly Random _randGenerator = new Random();
    private const string tokenAllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789$_-";

    private MemoryCache inRamCache { get; } = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 2048
        });
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(2))
        .SetSize(1); // Always use size=1 for AuthToken

    public SimpleRamAuthTokenCache(ILogger<SimpleRamAuthTokenCache> logger, IWebHostEnvironment environment) {
        _logger = logger;
        _environment = environment;
    }
    
    private string genToken() {
        return string.Join("", Enumerable.Repeat(0, 32).Select(n => tokenAllowedChars[_randGenerator.Next(0, tokenAllowedChars.Length)]));
    }

    public bool GenerateNewLoginRecord(int playerId, out string? newToken, out DateTimeOffset? absoluteExpiryTime) {
        newToken = null;
        absoluteExpiryTime = null;
        if (_environment.IsDevelopment()) {
            newToken = genToken();
            absoluteExpiryTime = (DateTimeOffset.Now + _cacheEntryOptions.SlidingExpiration);
            string newKey = (newToken + "/" + playerId.ToString()); // To avoid conflicts across different players
            inRamCache.Set(newKey, playerId, _cacheEntryOptions); 
        }
        return (null != newToken);
    }

    public bool ValidateToken(string token, int proposedPlayerId) {
        int? cachedPlayerId = null;
        string proposedKey = (token + "/" + proposedPlayerId.ToString());
        bool res = inRamCache.TryGetValue(proposedKey, out cachedPlayerId);
        return (res && cachedPlayerId == proposedPlayerId);
    }
}
