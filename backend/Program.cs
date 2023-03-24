using backend.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
if (builder.Environment.IsDevelopment()) {
    builder.Services.AddDbContext<DevEnvResourcesSqliteContext>(options => {
        // This sqlite file will be copied into executable directory upon "dotnet build" too, configured in "backend.csproj"
        var DevEnvResourcesDbPath = builder.Configuration.GetValue<string>("DevEnvResourcesDbPath");
        options.UseSqlite($"Data Source={DevEnvResourcesDbPath}"); // [WARNING] NuGet package "Microsoft.EntityFrameworkCore.Sqlite" is required to provide "DbContextOptionsBuilder.UseSqlite" method here.
    });
}
builder.Services.AddSingleton<IAuthTokenCache, SimpleRamAuthTokenCache>();
builder.Services.AddSingleton<ICaptchaCache, SimpleRamCaptchaCache>();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.AddConsole();

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

app.MapControllers();

app.Run();
