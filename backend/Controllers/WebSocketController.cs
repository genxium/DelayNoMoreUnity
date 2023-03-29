using backend.Storage;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using backend.Battle;
using shared;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace backend.Controllers;

public class WebSocketController : ControllerBase {
    private readonly ILogger _logger; // Dependency injection reference https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-7.0#create-logs

    private readonly IAuthTokenCache _tokenCache;
    private readonly IRoomManager _roomManager;

    public WebSocketController(ILogger<WebSocketController> logger, IAuthTokenCache tokenCache, IRoomManager roomManager) {
        _logger = logger;
        _tokenCache = tokenCache;
        _roomManager = roomManager;
    }

    [HttpGet] // Adding this Attribute just for Swagger recognition :)
    [Route("/Ws")]
    public async Task Get([FromQuery] string authToken, [FromQuery] int playerId, [FromQuery] int speciesId) {
        if (HttpContext.WebSockets.IsWebSocketRequest) {
            _logger.LogInformation("Got a websocket connection request#1 [ authToken={0}, playerId={1} ]", authToken, playerId);
            if (_tokenCache.ValidateToken(authToken, playerId)) {
                _logger.LogInformation("Got a websocket connection request#2 Validated successfully [ authToken={0}, playerId={1}, speciesId={2} ]", authToken, playerId, speciesId);
                using var session = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await HandleNewPlayerPrimarySession(session, playerId, speciesId);
            } else {
                _logger.LogWarning("Got a websocket connection request#2 Failed validation [ authToken={0}, playerId={1} ]", authToken, playerId);
                HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
        } else {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private async Task HandleNewPlayerPrimarySession(WebSocket session, int playerId, int speciesId) {
        using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource()) {
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            WebSocketCloseStatus closeCode = WebSocketCloseStatus.Empty;
            string? closeReason = null;

            var room = _roomManager.Pop();
            int addPlayerToRoomResult = ErrCode.UnknownError;
            if (null == room) {
                _logger.LogWarning("No available room [ playerId={0} ]", playerId);
                cancellationTokenSource.Cancel();
            } else {
                var player = new Player(new PlayerDownsync());
                addPlayerToRoomResult = room.AddPlayerIfPossible(player, playerId, speciesId, session, cancellationTokenSource);
                if (ErrCode.Ok != addPlayerToRoomResult) {
                    _logger.LogWarning("Failed to add player to room [ roomId={0}, playerId={1}, result={2} ]", room.id, playerId, addPlayerToRoomResult);
                    cancellationTokenSource.Cancel();
                } else {
                    _logger.LogInformation("Added player to room [ roomId={0}, playerId={1} ]", room.id, playerId);
                    _roomManager.Push(room.calRoomScore(), room);
                }
            }

            var buffer = new byte[1024];
            while (!cancellationToken.IsCancellationRequested) {
                try {
                    var receiveResult = await session.ReceiveAsync(
                    new ArraySegment<byte>(buffer), cancellationToken);

                    if (receiveResult.CloseStatus.HasValue) {
                        closeCode = receiveResult.CloseStatus.Value;
                        closeReason = receiveResult.CloseStatusDescription;
                        break;
                    }
                } catch (OperationCanceledException ocEx) {
                    _logger.LogWarning("Session is cancelled for [ roomId={0}, playerId={1}, ocEx={2} ]", room.id, playerId, ocEx.Message);
                }
            }
            
            if (null != room && ErrCode.Ok == addPlayerToRoomResult) {
                room.OnPlayerDisconnected(playerId);
            }
            if (!cancellationToken.IsCancellationRequested) {
                await session.CloseAsync(
                closeCode,
                closeReason,
                CancellationToken.None); // We won't cancel the closing of a websocket session
            }
        }
    }
}
