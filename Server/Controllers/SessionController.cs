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
                return NotFound(new { Message = "Session not found" });
            }

            // Validate session state
            if (!await ValidateSessionState(session))
            {
                return BadRequest(new { Message = "Session is not active" });
            }

            return Ok(new 
            { 
                SessionId = session.SessionIdentifier,
                Status = session.Status,
                HostUsername = session.HostUser?.Username,
                ClientUsername = session.ClientUser?.Username,
                CreatedAt = session.CreatedAt,
                LastActivity = session.UpdatedAt
            });
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession()
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

            // Check if user already has an active session
            var existingSession = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.Status == "active" && 
                    (s.HostUserId == user.Id || s.ClientUserId == user.Id));

            if (existingSession != null)
            {
                return BadRequest(new { Message = "User already has an active session" });
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

            _logger.LogInformation($"Session {sessionId} started by user {user.Username}");
            return Ok(new { SessionId = sessionId });
        }

        [HttpPost("join/{sessionId}")]
        public async Task<IActionResult> JoinSession(string sessionId)
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

            // Check if user already has an active session
            var existingSession = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.Status == "active" && 
                    (s.HostUserId == user.Id || s.ClientUserId == user.Id));

            if (existingSession != null)
            {
                return BadRequest(new { Message = "User already has an active session" });
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

            if (session.ClientUserId != null)
            {
                return BadRequest(new { Message = "Session is full" });
            }

            session.ClientUserId = user.Id;
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"User {user.Username} joined session {sessionId}");
            return Ok(new { Message = "Successfully joined session" });
        }

        [HttpPost("stop/{sessionId}")]
        public async Task<IActionResult> StopSession(string sessionId)
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

            _logger.LogInformation($"Session {sessionId} stopped by user {user.Username}");
            return Ok(new { Message = "Session stopped" });
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

            var connectionId = Guid.NewGuid().ToString();

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

            _logger.LogInformation($"User {user.Username} connected to session {sessionId} with connection ID {connectionId}");
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

            _logger.LogInformation($"User disconnected from session {sessionId}");
            return Ok(new { Message = "Successfully disconnected" });
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSessions()
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
                    LastActivity = s.UpdatedAt
                })
                .ToListAsync();

            return Ok(sessions);
        }
    }
}
