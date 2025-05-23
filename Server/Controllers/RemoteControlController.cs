using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Services;
using Server.Hubs;
using System.Security.Claims;
using System.Text.Json;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/remote")]
    public class RemoteControlController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RemoteControlController> _logger;
        private readonly IHubContext<RemoteControlHub> _hubContext;
        private readonly InputHandlerService _inputHandler;

        public RemoteControlController(
            AppDbContext context, 
            ILogger<RemoteControlController> logger,
            IHubContext<RemoteControlHub> hubContext,
            InputHandlerService inputHandler)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _inputHandler = inputHandler;
        }

        public class InputActionRequest
        {
            public required string SessionIdentifier { get; set; }
            public required InputActionData Action { get; set; }
        }

        public class InputActionData
        {
            public required string Type { get; set; }
            public required string Action { get; set; }
            public string? Key { get; set; }
            public int? X { get; set; }
            public int? Y { get; set; }
            public string? Button { get; set; }
            public string[]? Modifiers { get; set; }
        }

        [HttpPost("send-input")]
        public async Task<IActionResult> SendInput([FromBody] InputActionRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { 
                    success = false,
                    message = "Request body is required",
                    code = "REQUEST_REQUIRED"
                });
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { 
                        success = false,
                        message = "User not authenticated",
                        code = "AUTH_REQUIRED"
                    });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
                if (user == null)
                {
                    return NotFound(new { 
                        success = false,
                        message = "User not found",
                        code = "USER_NOT_FOUND"
                    });
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == request.SessionIdentifier &&
                        (s.HostUserId == user.Id || s.ClientUserId == user.Id));

                if (session == null)
                {
                    return NotFound(new { 
                        success = false,
                        message = "Session not found or not authorized",
                        code = "SESSION_NOT_FOUND"
                    });
                }

                if (session.Status != "active")
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Session is not active",
                        code = "SESSION_INACTIVE"
                    });
                }

                // Check if client is connected
                if (string.IsNullOrEmpty(session.ClientConnectionId))
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Client is not connected",
                        code = "CLIENT_DISCONNECTED"
                    });
                }

                // Only allow host to send input actions
                if (user.Id != session.HostUserId)
                {
                    return StatusCode(403, new { 
                        success = false,
                        message = "Only host can send input actions",
                        code = "HOST_ONLY"
                    });
                }

                var inputAction = new InputAction
                {
                    SessionIdentifier = request.SessionIdentifier,
                    Action = JsonSerializer.Serialize(request.Action),
                    CreatedAt = DateTime.UtcNow
                };

                // Validate the input action
                if (!_inputHandler.ValidateInput(inputAction))
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Invalid input action",
                        code = "INVALID_INPUT"
                    });
                }

                // Save to database
                _context.InputActions.Add(inputAction);
                await _context.SaveChangesAsync();

                // Send the input action to the client
                try
                {
                    var serializedAction = JsonSerializer.Serialize(request.Action);
                    Console.WriteLine($"[DEBUG] Sending input action to client:");
                    Console.WriteLine($"[DEBUG] Session ID: {session.SessionIdentifier}");
                    Console.WriteLine($"[DEBUG] Client Connection ID: {session.ClientConnectionId}");
                    Console.WriteLine($"[DEBUG] Action: {serializedAction}");
                    Console.WriteLine($"[DEBUG] Host Username: {session.HostUser?.Username}");

                    await _hubContext.Clients.Client(session.ClientConnectionId)
                        .SendAsync("ReceiveInput", serializedAction);
                    
                    Console.WriteLine($"[DEBUG] Input action sent successfully");
                    _logger.LogInformation($"Input action processed by host {user.Username} in session {session.SessionIdentifier}");
                    return Ok(new { 
                        success = true,
                        message = "Input action processed successfully",
                        code = "INPUT_SENT"
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to send input to client:");
                    Console.WriteLine($"[ERROR] Session ID: {session.SessionIdentifier}");
                    Console.WriteLine($"[ERROR] Client Connection ID: {session.ClientConnectionId}");
                    Console.WriteLine($"[ERROR] Error: {ex.Message}");
                    Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                    _logger.LogWarning(ex, $"Failed to send input to client {session.ClientConnectionId}");
                    return StatusCode(503, new { 
                        success = false,
                        message = "Client connection lost",
                        code = "CLIENT_DISCONNECTED"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing input action in session {request.SessionIdentifier}");
                return StatusCode(500, new { 
                    success = false,
                    message = "An error occurred while processing the input action",
                    code = "SERVER_ERROR"
                });
            }
        }

        [HttpGet("session-info")]
        public async Task<IActionResult> GetSessionInfo([FromQuery] string sessionIdentifier)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { 
                        success = false,
                        message = "User not authenticated",
                        code = "AUTH_REQUIRED"
                    });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
                if (user == null)
                {
                    return NotFound(new { 
                        success = false,
                        message = "User not found",
                        code = "USER_NOT_FOUND"
                    });
                }

                var session = await _context.RemoteSessions
                    .Include(s => s.HostUser)
                    .Include(s => s.ClientUser)
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionIdentifier &&
                        (s.HostUserId == user.Id || s.ClientUserId == user.Id));

                if (session == null)
                {
                    return NotFound(new { 
                        success = false,
                        message = "Session not found or not authorized",
                        code = "SESSION_NOT_FOUND"
                    });
                }

                var sessionInfo = new
                {
                    sessionId = session.SessionIdentifier,
                    status = session.Status,
                    host = new
                    {
                        id = session.HostUserId,
                        username = session.HostUser.Username,
                        isConnected = !string.IsNullOrEmpty(session.HostConnectionId),
                        connectionId = session.HostConnectionId
                    },
                    client = session.ClientUserId.HasValue ? new
                    {
                        id = session.ClientUserId,
                        username = session.ClientUser?.Username,
                        isConnected = !string.IsNullOrEmpty(session.ClientConnectionId),
                        connectionId = session.ClientConnectionId
                    } : null,
                    createdAt = session.CreatedAt,
                    lastActivity = session.UpdatedAt
                };

                return Ok(new { 
                    success = true,
                    message = "Session info retrieved successfully",
                    code = "SESSION_INFO_RETRIEVED",
                    data = sessionInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting session info for session {sessionIdentifier}");
                return StatusCode(500, new { 
                    success = false,
                    message = "An error occurred while getting session info",
                    code = "SERVER_ERROR"
                });
            }
        }
    }
}