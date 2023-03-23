using backend.Storage;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<ISimpleCache, TokenCache>();

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

    var DevEnvResourcesDbPath = builder.Configuration.GetValue<string>("DevEnvResourcesDbPath"); // This sqlite file will be copied into executable directory upon "dotnet build" too, configured in "backend.csproj"
    app.Logger.LogInformation("DevEnvResourcesDbPath={0}", DevEnvResourcesDbPath);
    using (var connection = new SqliteConnection(String.Format("Data Source={0}", DevEnvResourcesDbPath))) {
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
        SELECT *
        FROM test_player
        ";

        using (var reader = command.ExecuteReader()) {
            if (reader.HasRows) {
                app.Logger.LogInformation("Loaded Development env specific test players:");
            }
            while (reader.Read()) {
                var name = reader.GetString(0);
                app.Logger.LogInformation("{0}", name);
            }
        }
    }
}

app.MapControllers();

app.Run();
