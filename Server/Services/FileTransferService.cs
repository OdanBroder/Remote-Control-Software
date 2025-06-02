using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Hubs;
using Server.Models;
using System.Security.Cryptography;

namespace Server.Services
{
    public class FileTransferService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RemoteControlHub> _hubContext;
        private readonly ILogger<FileTransferService> _logger;
        private readonly string _tempDirectory;

        public FileTransferService(
            AppDbContext context,
            IHubContext<RemoteControlHub> hubContext,
            ILogger<FileTransferService> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _tempDirectory = Path.Combine(Path.GetTempPath(), "RemoteControlFiles");
            Directory.CreateDirectory(_tempDirectory);
        }

        public string GetTempDirectory() => _tempDirectory;

        public async Task<FileTransfer> InitiateFileTransfer(
            int sessionId,
            Guid senderUserId,
            Guid receiverUserId,
            string fileName,
            long fileSize)
        {
            var transfer = new FileTransfer
            {
                SessionId = sessionId,
                SenderUserId = senderUserId,
                ReceiverUserId = receiverUserId,
                FileName = fileName,
                FileSize = fileSize,
                Status = "transferring"
            };

            _context.FileTransfers.Add(transfer);
            await _context.SaveChangesAsync();
            
            // Notify receiver about new file transfer
            var session = await _context.RemoteSessions
                .Include(s => s.ClientUser)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session?.ClientConnectionId != null)
            {
                await _hubContext.Clients.Client(session.ClientConnectionId)
                    .SendAsync("FileTransferRequested", transfer.Id, fileName, fileSize);
            }

            return transfer;
        }

        public async Task<bool> ProcessFileChunk(int transferId, byte[] chunk, int offset)
        {
            try
            {
                var transfer = await _context.FileTransfers.FindAsync(transferId);
                if (transfer == null)
                {
                    _logger.LogError($"Transfer {transferId} not found");
                    return false;
                }

                if (transfer.Status != "transferring")
                {
                    _logger.LogError($"Transfer {transferId} is not in transferring state. Current status: {transfer.Status}");
                    return false;
                }

                var tempFilePath = Path.Combine(_tempDirectory, $"{transferId}_{transfer.FileName}");

                // Ensure the directory exists
                Directory.CreateDirectory(_tempDirectory);

                // Write the chunk to the file
                using (var fileStream = new FileStream(tempFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    fileStream.Seek(offset, SeekOrigin.Begin);
                    await fileStream.WriteAsync(chunk, 0, chunk.Length);
                    await fileStream.FlushAsync();
                }

                // Calculate progress
                var fileInfo = new FileInfo(tempFilePath);
                var progress = (int)((fileInfo.Length * 100) / transfer.FileSize);

                // Notify both parties about progress
                var session = await _context.RemoteSessions
                    .Include(s => s.HostUser)
                    .Include(s => s.ClientUser)
                    .FirstOrDefaultAsync(s => s.Id == transfer.SessionId);

                if (session?.HostConnectionId != null)
                {
                    await _hubContext.Clients.Client(session.HostConnectionId)
                        .SendAsync("FileTransferProgress", transferId, progress);
                }

                if (session?.ClientConnectionId != null)
                {
                    await _hubContext.Clients.Client(session.ClientConnectionId)
                        .SendAsync("FileTransferProgress", transferId, progress);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing file chunk for transfer {transferId}");
                var transfer = await _context.FileTransfers.FindAsync(transferId);
                if (transfer != null)
                {
                    transfer.Status = "failed";
                    transfer.ErrorMessage = ex.Message;
                    await _context.SaveChangesAsync();
                }
                return false;
            }
        }

        public async Task CompleteFileTransfer(int transferId)
        {
            var transfer = await _context.FileTransfers.FindAsync(transferId);
            if (transfer == null)
            {
                _logger.LogError($"Transfer {transferId} not found");
                return;
            }

            var tempFilePath = Path.Combine(_tempDirectory, $"{transferId}_{transfer.FileName}");
            
            try
            {
                if (!File.Exists(tempFilePath))
                {
                    throw new FileNotFoundException($"Temporary file not found: {tempFilePath}");
                }

                // Verify file integrity
                var fileInfo = new FileInfo(tempFilePath);
                if (fileInfo.Length != transfer.FileSize)
                {
                    throw new Exception($"File size mismatch. Expected: {transfer.FileSize}, Actual: {fileInfo.Length}");
                }

                transfer.Status = "completed";
                transfer.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();


                // Notify both parties about completion
                var session = await _context.RemoteSessions
                    .Include(s => s.HostUser)
                    .Include(s => s.ClientUser)
                    .FirstOrDefaultAsync(s => s.Id == transfer.SessionId);

                if (session?.HostConnectionId != null)
                {
                    try
                    {
                        await _hubContext.Clients.Client(session.HostConnectionId)
                            .SendAsync("FileTransferCompleted", transferId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send FileTransferCompleted event to host connection: {session.HostConnectionId}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Host connection ID is null for transfer {transferId}");
                }

                if (session?.ClientConnectionId != null)
                {
                    try
                    {
                        await _hubContext.Clients.Client(session.ClientConnectionId)
                            .SendAsync("FileTransferCompleted", transferId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to send FileTransferCompleted event to client connection: {session.ClientConnectionId}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Client connection ID is null for transfer {transferId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing file transfer {transferId}");
                transfer.Status = "failed";
                transfer.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync();
            }
        }

        public async Task CleanupFile(int transferId)
        {
            var transfer = await _context.FileTransfers.FindAsync(transferId);
            if (transfer == null)
            {
                _logger.LogError($"Transfer {transferId} not found for cleanup");
                return;
            }

            var tempFilePath = Path.Combine(_tempDirectory, $"{transferId}_{transfer.FileName}");
            try
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting temporary file: {tempFilePath}");
            }
        }
    }
} 