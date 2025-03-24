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

    public bool GenerateNewLoginRecord(string playerId, out string? newToken, out DateTimeOffset absoluteExpiryTime) {
        newToken = null;
        //if (_environment.IsDevelopment()) {
            newToken = genToken();
            absoluteExpiryTime = DateTimeOffset.UtcNow;
            var slidingExpiration = _cacheEntryOptions.SlidingExpiration.GetValueOrDefault(new TimeSpan(0, 30, 0));
            absoluteExpiryTime = absoluteExpiryTime.AddTicks(slidingExpiration.Ticks);
            string newKey = (newToken + "/" + playerId.ToString()); // To avoid conflicts across different players
            inRamCache.Set(newKey, playerId, _cacheEntryOptions); 
        //}
        return (null != newToken);
    }

    public bool ValidateToken(string token, string proposedPlayerId) {
        string? cachedPlayerId;
        string proposedKey = (token + "/" + proposedPlayerId);
        bool res = inRamCache.TryGetValue(proposedKey, out cachedPlayerId);
        return (res && proposedPlayerId.Equals(cachedPlayerId));
    }

    public (bool, string?) ValidateTokenAndRetrieveUname(string token, string proposedPlayerId) {
        bool res = ValidateToken(token, proposedPlayerId);
        if (!res) return (false, null);

        //if (_environment.IsDevelopment()) {
            using (var scope = _scopeFactory.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<DevEnvResourcesSqliteContext>();
                SqlitePlayer? testPlayer = db.Players.Where(p => proposedPlayerId.Equals($"tst_{p.id}")).First();
                if (null != testPlayer) {
                    return (true, testPlayer.name);
                }
            }
        //}

        return (false, null);
    }
}
