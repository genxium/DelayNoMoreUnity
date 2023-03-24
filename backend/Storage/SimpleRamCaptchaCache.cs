using System;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using backend.Storage.Dao;

namespace backend.Storage;

public class SimpleRamCaptchaCache : ICaptchaCache {

    private readonly ILogger<SimpleRamCaptchaCache> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly DevEnvResourcesSqliteContext _devEnvResourcesSqliteContext;

    private readonly Random _randNumGenerator = new Random();

    private MemoryCache inRamCache { get; } = new MemoryCache(
        new MemoryCacheOptions {
            SizeLimit = 2048
        });

    public SimpleRamCaptchaCache(ILogger<SimpleRamCaptchaCache> logger, IConfiguration configuration, IWebHostEnvironment environment, DevEnvResourcesSqliteContext devEnvResourcesSqliteContext) {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
        _devEnvResourcesSqliteContext = devEnvResourcesSqliteContext;
    }

    public bool GenerateNewCaptchaForUname(string uname, out string? newCaptcha) {
        newCaptcha = null;
        // [WARNING] This is NOT the simplest way to use SQLite, I'm just trying out the DbContext approach. For a simpler & more primitive way please refer to https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/?tabs=netcore-cli!
        if (_environment.IsDevelopment()) {
            SqlitePlayer testPlayer = _devEnvResourcesSqliteContext.players.First<SqlitePlayer>(p => p.name == uname);
            if (null != testPlayer) {
                newCaptcha = _randNumGenerator.Next(100000, 99999).ToString();
                inRamCache.Set<string>(uname, newCaptcha, TimeSpan.FromMinutes(2));
                _logger.LogInformation("Generated newCaptcha for uname={0}: newCaptcha={1}", uname, newCaptcha);
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

