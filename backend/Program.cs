var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var app = builder.Build();

// Reference https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-7.0 -- just ignore the SignalR part of this page which is pure noise, this official document points me to the sample codebase at https://github.com/dotnet/AspNetCore.Docs/tree/main/aspnetcore/fundamentals/websockets/samples which is by far the most compact sample of C# Websocket server without SignalR!
var webSocketOptions = new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};
app.UseWebSockets(webSocketOptions);

// The following 2 lines make "app" use "wwwroot" as the default web root for serving static file, e.g. "index.html". 
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
