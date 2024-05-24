using backend.Storage.Dao;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;

namespace backend.Storage;
public class SimpleRamAuthTokenCache : IAuthTokenCache {

    private readonly ILogger<SimpleRamAuthTokenCache> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly Random _randGenerator = new Random();
    private readonly IServiceScopeFactory _scopeFactory;
    private const string tokenAllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789$_-";

    private MemoryCache inRamCache { get; } = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 2048
        });
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(30))
        .SetSize(1); // Always use size=1 for AuthToken

    public SimpleRamAuthTokenCache(ILogger<SimpleRamAuthTokenCache> logger, IWebHostEnvironment environment, IServiceScopeFactory scopeFactory) {
        _logger = logger;
        _environment = environment;
        _scopeFactory = scopeFactory;
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

    public (bool, string?) ValidateTokenAndRetrieveUname(string token, int proposedPlayerId) {
        bool res = ValidateToken(token, proposedPlayerId);
        if (!res) return (false, null);

        if (_environment.IsDevelopment()) {
            using (var scope = _scopeFactory.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<DevEnvResourcesSqliteContext>();
                SqlitePlayer? testPlayer = db.Players.Where(p => p.id == proposedPlayerId).First();
                if (null != testPlayer) {
                    return (true, testPlayer.name);
                }
            }
        }

        return (false, null);
    }
}
