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

        public SessionController(AppDbContext context, RemoteSessionService sessionService, ILogger<SessionController> logger)
        {
            _context = context;
            _sessionService = sessionService;
            _logger = logger;
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

            return Ok(new 
            { 
                SessionId = session.SessionIdentifier,
                Status = session.Status,
                HostUsername = session.HostUser?.Username,
                ClientUsername = session.ClientUser?.Username,
                CreatedAt = session.CreatedAt
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

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            var sessionId = Guid.NewGuid().ToString();
            var session = new RemoteSession
            {
                SessionIdentifier = sessionId,
                HostUserId = user.InternalId,
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

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            var session = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId && s.Status == "active");

            if (session == null)
            {
                return NotFound(new { Message = "Session not found or not active" });
            }

            if (session.ClientUserId != 0)
            {
                return BadRequest(new { Message = "Session is full" });
            }

            session.ClientUserId = user.InternalId;
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

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null || (user.InternalId != session.HostUserId && user.InternalId != session.ClientUserId))
            {
                return Unauthorized(new { Message = "Not authorized to stop this session" });
            }

            session.Status = "ended";
            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Session {sessionId} stopped by user {user.Username}");
            return Ok(new { Message = "Session stopped" });
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveSessions()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User not authenticated" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound(new { Message = "User not found" });
            }

            var sessions = await _context.RemoteSessions
                .Where(s => s.Status == "active" && (s.HostUserId == user.InternalId || s.ClientUserId == user.InternalId))
                .Include(s => s.HostUser)
                .Include(s => s.ClientUser)
                .Select(s => new
                {
                    SessionId = s.SessionIdentifier,
                    HostUsername = s.HostUser.Username,
                    ClientUsername = s.ClientUser.Username,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(sessions);
        }
    }
}
