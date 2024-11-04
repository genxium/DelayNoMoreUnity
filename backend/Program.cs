using backend.Battle;
using backend.Storage;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var serilogOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] Th#{ThreadId} {Message} {NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId() // From nuget package "Serilog.Enrichers.Thread"
    .Enrich.WithThreadName() // From nuget package "Serilog.Enrichers.Thread"
    .WriteTo.Console(outputTemplate: serilogOutputTemplate)
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
//if (builder.Environment.IsDevelopment()) {
    builder.Services.AddDbContext<DevEnvResourcesSqliteContext>(options => {
        // This sqlite file will be copied into executable directory upon "dotnet build" too, configured in "backend.csproj"
        var DevEnvResourcesDbPath = builder.Configuration.GetValue<string>("DevEnvResourcesDbPath");
        options.UseSqlite($"Data Source={DevEnvResourcesDbPath}"); // [WARNING] NuGet package "Microsoft.EntityFrameworkCore.Sqlite" is required to provide "DbContextOptionsBuilder.UseSqlite" method here.
    });
//}

builder.Services.AddSingleton<IAuthTokenCache, SimpleRamAuthTokenCache>();
builder.Services.AddSingleton<ICaptchaCache, SimpleRamCaptchaCache>();
builder.Services.AddSingleton<IRoomManager, PriorityBasedRoomManager>();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host
    .UseConsoleLifetime()
    .UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithThreadId() // From nuget package "Serilog.Enrichers.Thread"
    .Enrich.WithThreadName() // From nuget package "Serilog.Enrichers.Thread"
    .WriteTo.Console(outputTemplate: serilogOutputTemplate));

var app = builder.Build();

// Reference https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-7.0 -- just ignore the SignalR part of this page which is pure noise, this official document points me to the sample codebase at https://github.com/dotnet/AspNetCore.Docs/tree/main/aspnetcore/fundamentals/websockets/samples which is by far the most compact sample of C# Websocket server without SignalR!
var webSocketOptions = new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

// The following 2 lines make "app" use "wwwroot" as the default web root for serving static file, e.g. "index.html". 
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment()) {
    // Enable swagger in Development
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Lifetime.ApplicationStopping.Register(async () => {
    Log.Logger.Warning("Running tailored Application stopping cleanup");
    var roomManager = app.Services.GetService<IRoomManager>();
    if (null != roomManager) {
        while (true) {
            var room = roomManager.Pop();
            if (null == room) { break; }
            await room.SettleBattleAsync();
        }
    }
});
app.MapControllers();

app.Run();
