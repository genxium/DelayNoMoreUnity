using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

public class WebSocketController : ControllerBase {
    private readonly ILogger _logger; // Dependency injection reference https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-7.0#create-logs
    public WebSocketController(ILogger<WebSocketController> logger) {
        _logger = logger;
    }

    [HttpGet] // Adding this Attribute just for Swagger recognition :)
    [Route("/Ws")]
    public async Task Get() {
        if (HttpContext.WebSockets.IsWebSocketRequest) {
            _logger.LogInformation("Got a websocket connection request");
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await Echo(webSocket);
        } else {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private static async Task Echo(WebSocket webSocket) {
        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue) {
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                CancellationToken.None);

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }
}
