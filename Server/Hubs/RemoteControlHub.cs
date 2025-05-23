using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.Services;
using Server.Models;
using Server.Data;
using System.Security.Claims;

namespace Server.Hubs
{
    public class RemoteControlHub : Hub
    {
        private readonly RemoteSessionService _sessionService;
        private readonly ILogger<RemoteControlHub> _logger;
        private readonly AppDbContext _context;

        public RemoteControlHub(
            RemoteSessionService sessionService,
            ILogger<RemoteControlHub> logger,
            AppDbContext context)
        {
            _sessionService = sessionService;
            _logger = logger;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
                if (string.IsNullOrEmpty(sessionId))
                {
                    _logger.LogWarning("Connection attempt without sessionId");
                    await Clients.Caller.SendAsync("Error", "Session ID is required");
                    Context.Abort();
                    return;
                }

                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Connection attempt without user authentication");
                    await Clients.Caller.SendAsync("Error", "Authentication required");
                    Context.Abort();
                    return;
                }

                _logger.LogInformation($"Client connecting: {Context.ConnectionId} for session: {sessionId}");
                Console.WriteLine($"[DEBUG] Client connecting: {Context.ConnectionId} for session: {sessionId}");

                // Send initial connection success message
                await Clients.Caller.SendAsync("ConnectionEstablished", Context.ConnectionId);

                await _sessionService.AddConnection(sessionId, Context.ConnectionId, Guid.Parse(userId));
                _logger.LogInformation($"Client connected: {Context.ConnectionId} for session: {sessionId}");
                Console.WriteLine($"[DEBUG] Client connected: {Context.ConnectionId} for session: {sessionId}");

                // Notify other party about connection
                var session = await _sessionService.GetSession(sessionId);
                if (session != null)
                {
                    var targetConnectionId = session.HostConnectionId == Context.ConnectionId 
                        ? session.ClientConnectionId 
                        : session.HostConnectionId;

                    if (!string.IsNullOrEmpty(targetConnectionId))
                    {
                        await Clients.Client(targetConnectionId)
                            .SendAsync("PeerConnected", Context.ConnectionId);
                    }
                }

                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                Console.WriteLine($"[ERROR] Connection error: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                await Clients.Caller.SendAsync("Error", "Connection failed: " + ex.Message);
                Context.Abort();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var sessionId = await _sessionService.GetSessionId(Context.ConnectionId);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var session = await _sessionService.GetSession(sessionId);
                    if (session != null)
                    {
                        // Notify other party about disconnection
                        var targetConnectionId = session.HostConnectionId == Context.ConnectionId 
                            ? session.ClientConnectionId 
                            : session.HostConnectionId;

                        if (!string.IsNullOrEmpty(targetConnectionId))
                        {
                            await Clients.Client(targetConnectionId)
                                .SendAsync("PeerDisconnected", Context.ConnectionId);
                        }
                    }

                    await _sessionService.RemoveConnection(sessionId, Context.ConnectionId);
                    _logger.LogInformation($"Client disconnected: {Context.ConnectionId} from session: {sessionId}");
                    Console.WriteLine($"[DEBUG] Client disconnected: {Context.ConnectionId} from session: {sessionId}");
                }
                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
                Console.WriteLine($"[ERROR] Disconnection error: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task SendInputAction(string sessionId, string action)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    Console.WriteLine($"[WARNING] Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    await Clients.Caller.SendAsync("Error", "Invalid session");
                    return;
                }

                var targetConnectionId = await _sessionService.GetTargetConnectionId(sessionId, Context.ConnectionId);
                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    _logger.LogWarning($"No target connection found for session: {sessionId}");
                    Console.WriteLine($"[WARNING] No target connection found for session: {sessionId}");
                    await Clients.Caller.SendAsync("Error", "No target connection found");
                    return;
                }

                Console.WriteLine($"[DEBUG] Sending input action to client {targetConnectionId}:");
                Console.WriteLine($"[DEBUG] Session ID: {sessionId}");
                Console.WriteLine($"[DEBUG] From Connection ID: {Context.ConnectionId}");
                Console.WriteLine($"[DEBUG] Action: {action}");

                await Clients.Client(targetConnectionId).SendAsync("ReceiveInput", action);
                
                Console.WriteLine($"[SUCCESS] Input action sent successfully to client {targetConnectionId}");
                _logger.LogDebug($"Input action sent for session: {sessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send input action: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                _logger.LogError(ex, $"Error sending input action for session: {sessionId}");
                await Clients.Caller.SendAsync("Error", "Failed to send input action: " + ex.Message);
                throw;
            }
        }

        public async Task SendWebRTCSignal(string sessionId, string signal)
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

                await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCSignal", signal);
                _logger.LogDebug($"WebRTC signal sent for session: {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending WebRTC signal for session: {sessionId}");
                throw;
            }
        }
    }
}