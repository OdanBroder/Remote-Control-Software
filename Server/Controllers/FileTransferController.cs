using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Services;
using Server.Data;
using System.Security.Claims;
using System.Net.Mime;

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
            [FromQuery] int offset)
        {
            try
            {
                if (Request.ContentLength == 0)
                {
                    return BadRequest(new { success = false, message = "No data received" });
                }

                // Read the raw request body
                using var ms = new MemoryStream();
                await Request.Body.CopyToAsync(ms);
                var chunk = ms.ToArray();

                if (chunk.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Empty chunk received" });
                }
                
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

        [HttpGet("download/{transferId}")]
        public async Task<IActionResult> DownloadFile(int transferId)
        {
            try
            {
                var transfer = await _context.FileTransfers.FindAsync(transferId);
                if (transfer == null)
                {
                    return NotFound(new { success = false, message = "File transfer not found" });
                }

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? throw new UnauthorizedAccessException("User ID claim not found");
                var userId = Guid.Parse(userIdClaim);

                if (userId != transfer.ReceiverUserId)
                {
                    return Forbid();
                }

                var tempFilePath = Path.Combine(_fileTransferService.GetTempDirectory(), $"{transferId}_{transfer.FileName}");
                if (!System.IO.File.Exists(tempFilePath))
                {
                    return NotFound(new { success = false, message = "File not found" });
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                
                // Clean up the file after reading it
                await _fileTransferService.CleanupFile(transferId);
                
                // Set proper Content-Disposition header with filename
                var contentDisposition = $"attachment; filename=\"{transfer.FileName}\"";
                Response.Headers["Content-Disposition"] = contentDisposition;
                
                return File(fileBytes, "application/octet-stream");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading file for transfer {transferId}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("tcp/initiate")]
        public async Task<IActionResult> InitiateTcpFileTransfer(
            [FromQuery] string sessionId,
            [FromQuery] string fileName,
            [FromQuery] long fileSize)
        {
            try
            {
                var result = await _fileTransferService.StartTcpFileTransfer(sessionId, fileName, fileSize);
                if (!result.success)
                {
                    return BadRequest(new { success = false, message = result.message });
                }

                return Ok(new
                {
                    success = true,
                    message = "TCP file transfer initiated",
                    data = new
                    {
                        id = Guid.NewGuid().ToString(),
                        port = result.port
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating TCP file transfer");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost("tcp/connect")]
        public async Task<IActionResult> ConnectToReceiver([FromQuery] string sessionId)
        {
            try
            {
                var result = await _fileTransferService.ConnectToReceiver(sessionId);
                if (!result.success)
                {
                    return BadRequest(new { success = false, message = result.message });
                }

                return Ok(new { success = true, data = new { port = result.port } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error connecting to receiver for session {sessionId}");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }
    }
} 