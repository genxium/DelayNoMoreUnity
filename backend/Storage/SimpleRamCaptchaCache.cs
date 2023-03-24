using System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using backend.Storage.Dao;
using Microsoft.EntityFrameworkCore;

namespace backend.Storage;

public class SimpleRamCaptchaCache : ICaptchaCache {

    private readonly ILogger<SimpleRamCaptchaCache> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MemoryCacheEntryOptions _cacheEntryOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(2))
        .SetSize(1); // Always use size=1 for Captcha

    private readonly Random _randNumGenerator = new Random();

    private MemoryCache inRamCache { get; } = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 2048
        });

    public SimpleRamCaptchaCache(ILogger<SimpleRamCaptchaCache> logger, IConfiguration configuration, IWebHostEnvironment environment, IServiceScopeFactory scopeFactory) {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _scopeFactory = scopeFactory;
    }

    public bool GenerateNewCaptchaForUname(string uname, out string? newCaptcha) {
        newCaptcha = null;
        // [WARNING] This is NOT the simplest way to use SQLite, I'm just trying out the DbContext approach. For a simpler & more primitive way please refer to https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/?tabs=netcore-cli!
        if (_environment.IsDevelopment()) {
            // DbContext is a scoped service, see https://stackoverflow.com/questions/36332239/use-dbcontext-in-asp-net-singleton-injected-class for more information.
            using (var scope = _scopeFactory.CreateScope()) {
                var db = scope.ServiceProvider.GetRequiredService<DevEnvResourcesSqliteContext>();
                SqlitePlayer? testPlayer = db.Players.Where(p => p.name == uname).First<SqlitePlayer?>();
                if (null != testPlayer) {
                    newCaptcha = _randNumGenerator.Next(10000, 99999).ToString();
                    inRamCache.Set<string>(uname, newCaptcha, _cacheEntryOptions);
                    _logger.LogInformation("Generated newCaptcha for uname={0}: newCaptcha={1}", uname, newCaptcha);
                }
            }
            
        }
        return (null != newCaptcha);
    }

    public bool ValidateUnameCaptchaPair(string uname, string captcha) {
        _logger.LogInformation("Validating player by uname={0}, captcha={1}", uname, captcha);
        string? cachedCaptcha = null;
        inRamCache.TryGetValue<string?>(uname, out cachedCaptcha);
        return captcha.Equals(cachedCaptcha);
    }
}

