using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Services;
using System.Security.Claims;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/remote")]
    public class RemoteControlController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RemoteSessionService _sessionService;
        private readonly ILogger<RemoteControlController> _logger;

        public RemoteControlController(AppDbContext context, RemoteSessionService sessionService, ILogger<RemoteControlController> logger)
        {
            _context = context;
            _sessionService = sessionService;
            _logger = logger;
        }

        [HttpPost("send-input")]
        public async Task<IActionResult> SendInput([FromBody] InputAction action)
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
                .FirstOrDefaultAsync(s => s.SessionIdentifier == action.SessionIdentifier && 
                    (s.HostUserId == user.InternalId || s.ClientUserId == user.InternalId));

            if (session == null)
            {
                return NotFound(new { Message = "Session not found or not authorized" });
            }

            if (session.Status != "active")
            {
                return BadRequest(new { Message = "Session is not active" });
            }

            var inputAction = new InputAction
            {
                SessionId = session.Id,
                Action = action.Action,
                CreatedAt = DateTime.UtcNow
            };

            _context.InputActions.Add(inputAction);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Input action sent by user {user.Username} in session {session.SessionIdentifier}");
            return Ok(new { Message = "Input action sent successfully" });
        }

        [HttpGet("screen")]
        public async Task<IActionResult> GetScreenData([FromQuery] string sessionIdentifier)
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
                .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionIdentifier && 
                    (s.HostUserId == user.InternalId || s.ClientUserId == user.InternalId));

            if (session == null)
            {
                return NotFound(new { Message = "Session not found or not authorized" });
            }

            if (session.Status != "active")
            {
                return BadRequest(new { Message = "Session is not active" });
            }

            var screenData = await _context.ScreenData
                .Where(sd => sd.SessionId == session.Id)
                .OrderByDescending(sd => sd.CreatedAt)
                .FirstOrDefaultAsync();

            if (screenData == null)
            {
                return NotFound(new { Message = "No screen data available" });
            }

            return Ok(new { Data = screenData.Data });
        }

        [HttpPost("screen")]
        public async Task<IActionResult> UpdateScreenData([FromBody] ScreenData screenData)
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
                .FirstOrDefaultAsync(s => s.SessionIdentifier == screenData.SessionIdentifier && 
                    s.HostUserId == user.InternalId);

            if (session == null)
            {
                return NotFound(new { Message = "Session not found or not authorized" });
            }

            if (session.Status != "active")
            {
                return BadRequest(new { Message = "Session is not active" });
            }

            var newScreenData = new ScreenData
            {
                SessionId = session.Id,
                Data = screenData.Data,
                CreatedAt = DateTime.UtcNow
            };

            _context.ScreenData.Add(newScreenData);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Screen data updated by host user {user.Username} in session {session.SessionIdentifier}");
            return Ok(new { Message = "Screen data updated successfully" });
        }
    }
}