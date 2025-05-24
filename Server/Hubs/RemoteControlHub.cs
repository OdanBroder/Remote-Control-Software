using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.Services;
using Server.Models;
using Server.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

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

        public async Task<object> SendInputAction(string sessionId, string action)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Input action attempt without user authentication");
                    return new { success = false, message = "Authentication required", code = "AUTH_REQUIRED" };
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {userId}");
                    return new { success = false, message = "User not found", code = "USER_NOT_FOUND" };
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId &&
                        (s.HostUserId == user.Id || s.ClientUserId == user.Id));

                if (session == null)
                {
                    _logger.LogWarning($"Session not found or not authorized: {sessionId}");
                    return new { success = false, message = "Session not found or not authorized", code = "SESSION_NOT_FOUND" };
                }

                if (session.Status != "active")
                {
                    _logger.LogWarning($"Session is not active: {sessionId}");
                    return new { success = false, message = "Session is not active", code = "SESSION_INACTIVE" };
                }

                // Check if client is connected
                if (string.IsNullOrEmpty(session.ClientConnectionId))
                {
                    _logger.LogWarning($"Client is not connected for session: {sessionId}");
                    return new { success = false, message = "Client is not connected", code = "CLIENT_DISCONNECTED" };
                }

                // Only allow host to send input actions
                if (user.Id != session.HostUserId)
                {
                    _logger.LogWarning($"Non-host user attempted to send input: {userId}");
                    return new { success = false, message = "Only host can send input actions", code = "NOT_HOST" };
                }

                // Validate the input action
                var inputAction = new InputAction
                {
                    SessionIdentifier = sessionId,
                    Action = action,
                    CreatedAt = DateTime.UtcNow
                };

                // Save to database
                _context.InputActions.Add(inputAction);
                await _context.SaveChangesAsync();

                Console.WriteLine($"[DEBUG] Sending input action to client {session.ClientConnectionId}:");
                Console.WriteLine($"[DEBUG] Session ID: {sessionId}");
                Console.WriteLine($"[DEBUG] From Connection ID: {Context.ConnectionId}");
                Console.WriteLine($"[DEBUG] Action: {action}");
                Console.WriteLine($"[DEBUG] Host Username: {user.Username}");

                await Clients.Client(session.ClientConnectionId).SendAsync("ReceiveInput", action);
                
                Console.WriteLine($"[SUCCESS] Input action sent successfully to client {session.ClientConnectionId}");
                _logger.LogInformation($"Input action processed by host {user.Username} in session {sessionId}");
                
                return new { success = true, message = "Input action sent successfully", code = "INPUT_SENT" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to send input action: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                _logger.LogError(ex, $"Error sending input action for session: {sessionId}");
                return new { success = false, message = "Failed to send input action: " + ex.Message, code = "INPUT_ERROR" };
            }
        }

        public async Task ReportInputError(object errorData)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Error report attempt without user authentication");
                    return;
                }

                _logger.LogError($"Input error reported by user {userId}: {errorData}");
                Console.WriteLine($"[ERROR] Input error reported: {errorData}");
                
                // Save error to database
                var errorLog = new InputError
                {
                    UserId = Guid.Parse(userId),
                    ErrorData = errorData?.ToString() ?? "Unknown error",
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.InputErrors.Add(errorLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing input error report");
                Console.WriteLine($"[ERROR] Failed to process input error report: {ex.Message}");
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

                // Log the signal being forwarded
                _logger.LogInformation($"Forwarding WebRTC signal from {Context.ConnectionId} to {targetConnectionId}");
                Console.WriteLine($"[DEBUG] Forwarding WebRTC signal from {Context.ConnectionId} to {targetConnectionId}");

                // Forward the signal to the target connection
                await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCSignal", new
                {
                    signalType = signal.Split(':')[0],
                    signalData = signal.Split(':')[1],
                    fromConnectionId = Context.ConnectionId
                });

                _logger.LogInformation($"WebRTC signal forwarded successfully");
                Console.WriteLine($"[DEBUG] WebRTC signal forwarded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding WebRTC signal for session: {sessionId}");
                Console.WriteLine($"[ERROR] Failed to forward WebRTC signal: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task SendWebRTCState(string sessionId, string state)
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

                // Log the state update
                _logger.LogInformation($"Forwarding WebRTC state from {Context.ConnectionId} to {targetConnectionId}");
                Console.WriteLine($"[DEBUG] Forwarding WebRTC state from {Context.ConnectionId} to {targetConnectionId}");

                // Forward the state to the target connection
                await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCState", new
                {
                    state = state,
                    fromConnectionId = Context.ConnectionId
                });

                _logger.LogInformation($"WebRTC state forwarded successfully");
                Console.WriteLine($"[DEBUG] WebRTC state forwarded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding WebRTC state for session: {sessionId}");
                Console.WriteLine($"[ERROR] Failed to forward WebRTC state: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            }
        }
    }
}