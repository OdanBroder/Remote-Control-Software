using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Services;
using System.Security.Claims;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/session")]
    public class SessionController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RemoteSessionService _sessionService;
        private readonly ILogger<SessionController> _logger;
        private const int SESSION_TIMEOUT_MINUTES = 30;

        public SessionController(AppDbContext context, RemoteSessionService sessionService, ILogger<SessionController> logger)
        {
            _context = context;
            _sessionService = sessionService;
            _logger = logger;
        }

        private async Task<bool> ValidateSessionState(RemoteSession session, string requiredStatus = "active")
        {
            if (session == null) return false;
            
            // Check if session has timed out
            if (session.Status == "active" && 
                session.UpdatedAt < DateTime.UtcNow.AddMinutes(-SESSION_TIMEOUT_MINUTES))
            {
                session.Status = "ended";
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return false;
            }

            return session.Status == requiredStatus;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSession(string id)
        {
            var session = await _context.RemoteSessions
                .Include(s => s.HostUser)
                .Include(s => s.ClientUser)
                .FirstOrDefaultAsync(s => s.SessionIdentifier == id);

            if (session == null)
            {
                return NotFound(new { 
                    success = false,
                    message = "Session not found",
                    code = "SESSION_NOT_FOUND"
                });
            }

            // Validate session state
            if (!await ValidateSessionState(session))
            {
                return BadRequest(new { 
                    success = false,
                    message = "Session is not active or has expired",
                    code = "SESSION_INACTIVE"
                });
            }

            return Ok(new 
            { 
                success = true,
                message = "Session details retrieved successfully",
                code = "SESSION_FOUND",
                data = new {
                    SessionId = session.SessionIdentifier,
                    Status = session.Status,
                    HostUsername = session.HostUser?.Username,
                    ClientUsername = session.ClientUser?.Username,
                    CreatedAt = session.CreatedAt,
                    LastActivity = session.UpdatedAt
                }
            });
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { 
                    success = false,
                    message = "Authentication required",
                    code = "AUTH_REQUIRED"
                });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
            if (user == null)
            {
                return NotFound(new { 
                    success = false,
                    message = "User account not found",
                    code = "USER_NOT_FOUND"
                });
            }

            // Check if user already has an active session
            var existingSession = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.Status == "active" && 
                    (s.HostUserId == user.Id || s.ClientUserId == user.Id));

            if (existingSession != null)
            {
                return BadRequest(new { 
                    success = false,
                    message = "User already has an active session",
                    code = "SESSION_EXISTS"
                });
            }

            var sessionId = Guid.NewGuid().ToString();
            var session = new RemoteSession
            {
                SessionIdentifier = sessionId,
                HostUserId = user.Id,
                ClientUserId = null,
                Status = "active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.RemoteSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new { 
                success = true,
                message = "Session started successfully",
                code = "SESSION_STARTED",
                data = new { SessionId = sessionId }
            });
        }

        [HttpPost("join/{sessionId}")]
        public async Task<IActionResult> JoinSession(string sessionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { 
                    success = false,
                    message = "Authentication required",
                    code = "AUTH_REQUIRED"
                });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
            if (user == null)
            {
                return NotFound(new { 
                    success = false,
                    message = "User account not found",
                    code = "USER_NOT_FOUND"
                });
            }

            // Check if user already has an active session
            var existingSession = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.Status == "active" && 
                    (s.HostUserId == user.Id || s.ClientUserId == user.Id));

            if (existingSession != null)
            {
                return BadRequest(new { 
                    success = false,
                    message = "User already has an active session",
                    code = "SESSION_EXISTS"
                });
            }

            var session = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

            if (session == null)
            {
                return NotFound(new { 
                    success = false,
                    message = "Session not found",
                    code = "SESSION_NOT_FOUND"
                });
            }

            if (!await ValidateSessionState(session))
            {
                return BadRequest(new { 
                    success = false,
                    message = "Session is not active or has expired",
                    code = "SESSION_INACTIVE"
                });
            }

            if (session.ClientUserId != null)
            {
                return BadRequest(new { 
                    success = false,
                    message = "Session is already full",
                    code = "SESSION_FULL"
                });
            }

            session.ClientUserId = user.Id;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { 
                success = true,
                message = "Successfully joined session",
                code = "SESSION_JOINED",
                data = new { SessionId = sessionId }
            });
        }

        [HttpPost("stop/{sessionId}")]
        public async Task<IActionResult> StopSession(string sessionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Message = "User not authenticated" });
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

                if (session == null)
                {
                    return NotFound(new { Message = "Session not found" });
                }

                if (session.Status == "ended")
                {
                    return BadRequest(new { Message = "Session is already stopped" });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
                if (user == null || (user.Id != session.HostUserId && user.Id != session.ClientUserId))
                {
                    return Unauthorized(new { Message = "Not authorized to stop this session" });
                }

                session.Status = "ended";
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Session stopped successfully", SessionId = sessionId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping session {sessionId}");
                return StatusCode(500, new { Message = "An error occurred while stopping the session" });
            }
        }

        [HttpPost("connect/{sessionId}")]
        public async Task<IActionResult> ConnectToSession(string sessionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User not authenticated" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            var session = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

            if (session == null)
            {
                return NotFound(new { Message = "Session not found" });
            }

            if (!await ValidateSessionState(session))
            {
                return BadRequest(new { Message = "Session is not active" });
            }

            // Get the actual SignalR connection ID from the request
            var connectionId = HttpContext.Request.Headers["X-SignalR-Connection-Id"].ToString();
            if (string.IsNullOrEmpty(connectionId))
            {
                return BadRequest(new { Message = "No SignalR connection ID provided" });
            }

            if (session.HostUserId == user.Id)
            {
                session.HostConnectionId = connectionId;
            }
            else if (session.ClientUserId == user.Id)
            {
                session.ClientConnectionId = connectionId;
            }
            else
            {
                return Unauthorized(new { Message = "User is not part of this session" });
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { ConnectionId = connectionId });
        }

        [HttpPost("disconnect/{sessionId}")]
        public async Task<IActionResult> DisconnectFromSession(string sessionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User not authenticated" });
            }

            var session = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

            if (session == null)
            {
                return NotFound(new { Message = "Session not found" });
            }

            if (!await ValidateSessionState(session))
            {
                return BadRequest(new { Message = "Session is not active" });
            }

            if (session.HostUserId == Guid.Parse(userId))
            {
                session.HostConnectionId = null;
            }
            else if (session.ClientUserId == Guid.Parse(userId))
            {
                session.ClientConnectionId = null;
            }
            else
            {
                return Unauthorized(new { Message = "User is not part of this session" });
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Successfully disconnected" });
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSessions()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { 
                    success = false,
                    message = "Authentication required",
                    code = "AUTH_REQUIRED"
                });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
            if (user == null)
            {
                return NotFound(new { 
                    success = false,
                    message = "User account not found",
                    code = "USER_NOT_FOUND"
                });
            }

            var sessions = await _context.RemoteSessions
                .Where(s => s.Status == "active" && (s.HostUserId == user.Id || s.ClientUserId == user.Id))
                .Include(s => s.HostUser)
                .Include(s => s.ClientUser)
                .Select(s => new
                {
                    SessionId = s.SessionIdentifier,
                    HostUsername = s.HostUser != null ? s.HostUser.Username : null,
                    ClientUsername = s.ClientUser != null ? s.ClientUser.Username : null,
                    CreatedAt = s.CreatedAt,
                    LastActivity = s.UpdatedAt,
                    Status = s.Status
                })
                .ToListAsync();

            return Ok(new {
                success = true,
                message = sessions.Count > 0 ? $"Found {sessions.Count} active session(s)" : "No active sessions found",
                code = sessions.Count > 0 ? "SESSIONS_FOUND" : "NO_SESSIONS",
                data = sessions
            });
        }

        [HttpGet("info/{sessionId}")]
        public async Task<IActionResult> GetSessionInfo(string sessionId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { 
                        success = false,
                        message = "Authentication required",
                        code = "AUTH_REQUIRED"
                    });
                }

                var session = await _context.RemoteSessions
                    .Include(s => s.HostUser)
                    .Include(s => s.ClientUser)
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

                if (session == null)
                {
                    return NotFound(new { 
                        success = false,
                        message = "Session not found",
                        code = "SESSION_NOT_FOUND"
                    });
                }

                // Validate session state
                if (!await ValidateSessionState(session))
                {
                    return BadRequest(new { 
                        success = false,
                        message = "Session is not active or has expired",
                        code = "SESSION_INACTIVE"
                    });
                }

                return Ok(new 
                { 
                    success = true,
                    message = "Session info retrieved successfully",
                    code = "SESSION_INFO_RETRIEVED",
                    data = new {
                        sessionId = session.SessionIdentifier,
                        status = session.Status,
                        hostConnectionId = session.HostConnectionId,
                        clientConnectionId = session.ClientConnectionId,
                        hostUsername = session.HostUser?.Username,
                        clientUsername = session.ClientUser?.Username,
                        createdAt = session.CreatedAt,
                        lastActivity = session.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting session info for session {sessionId}");
                return StatusCode(500, new { 
                    success = false,
                    message = "An error occurred while retrieving session info",
                    code = "SERVER_ERROR"
                });
            }
        }
    }
}
