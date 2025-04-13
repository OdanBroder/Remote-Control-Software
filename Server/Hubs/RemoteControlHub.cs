using Microsoft.AspNetCore.SignalR;
using Server.Services;
using System.Security.Claims;

namespace Server.Hubs
{
    public class RemoteControlHub : Hub
    {
        private readonly RemoteSessionService _sessionService;
        private readonly ILogger<RemoteControlHub> _logger;

        public RemoteControlHub(RemoteSessionService sessionService, ILogger<RemoteControlHub> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Connection attempt without sessionId");
                Context.Abort();
                return;
            }

            await _sessionService.AddConnection(sessionId, Context.ConnectionId);
            _logger.LogInformation($"Client connected: {Context.ConnectionId} for session: {sessionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var sessionId = await _sessionService.GetSessionId(Context.ConnectionId);
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _sessionService.RemoveConnection(sessionId, Context.ConnectionId);
                _logger.LogInformation($"Client disconnected: {Context.ConnectionId} from session: {sessionId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendScreenData(string sessionId, byte[] imageData)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var targetConnectionId = await _sessionService.GetTargetConnectionId(sessionId, Context.ConnectionId);
                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    _logger.LogWarning($"No target connection found for session: {sessionId}");
                    return;
                }

                await Clients.Client(targetConnectionId).SendAsync("ReceiveScreenData", sessionId, imageData);
                _logger.LogDebug($"Screen data sent for session: {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending screen data for session: {sessionId}");
                throw;
            }
        }

        public async Task SendInputAction(string sessionId, string action)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var targetConnectionId = await _sessionService.GetTargetConnectionId(sessionId, Context.ConnectionId);
                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    _logger.LogWarning($"No target connection found for session: {sessionId}");
                    return;
                }

                await Clients.Client(targetConnectionId).SendAsync("ReceiveInputAction", sessionId, action);
                _logger.LogDebug($"Input action sent for session: {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending input action for session: {sessionId}");
                throw;
            }
        }
    }
}