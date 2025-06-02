using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Services;
using Server.Data;
using System.Security.Claims;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileTransferController : ControllerBase
    {
        private readonly FileTransferService _fileTransferService;
        private readonly AppDbContext _context;
        private readonly ILogger<FileTransferController> _logger;

        public FileTransferController(
            FileTransferService fileTransferService,
            AppDbContext context,
            ILogger<FileTransferController> logger)
        {
            _fileTransferService = fileTransferService;
            _context = context;
            _logger = logger;
        }

        [HttpPost("initiate")]
        public async Task<IActionResult> InitiateFileTransfer(
            [FromQuery] string sessionId,
            [FromQuery] string fileName,
            [FromQuery] long fileSize)
        {
            try
            {
                var senderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? throw new UnauthorizedAccessException("User ID claim not found");
                var senderUserId = Guid.Parse(senderIdClaim);

                // Get the session by GUID
                var session = await _context.RemoteSessions
                    .Include(s => s.HostUser)
                    .Include(s => s.ClientUser)
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId)
                    ?? throw new KeyNotFoundException($"Session {sessionId} not found");

                // Determine receiver ID based on sender's role
                var receiverUserId = senderUserId == session.HostUserId 
                    ? session.ClientUserId ?? throw new InvalidOperationException("No client connected to session")
                    : session.HostUserId;

                var transfer = await _fileTransferService.InitiateFileTransfer(
                    session.Id,
                    senderUserId,
                    receiverUserId,
                    fileName,
                    fileSize);

                return Ok(new { success = true, data = transfer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating file transfer");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("chunk/{transferId}")]
        public async Task<IActionResult> ProcessFileChunk(
            int transferId,
            [FromBody] byte[] chunk,
            [FromQuery] int offset)
        {
            try
            {
                var success = await _fileTransferService.ProcessFileChunk(transferId, chunk, offset);
                return Ok(new { success });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing file chunk for transfer {transferId}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("complete/{transferId}")]
        public async Task<IActionResult> CompleteFileTransfer(int transferId)
        {
            try
            {
                await _fileTransferService.CompleteFileTransfer(transferId);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing file transfer {transferId}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
} 