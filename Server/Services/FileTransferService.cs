using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Hubs;
using Server.Models;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Server.Services
{
    public class FileTransferService
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<RemoteControlHub> _hubContext;
        private readonly ILogger<FileTransferService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _tempDirectory;
        private readonly Dictionary<string, TcpListener> _tcpListeners = new();
        private readonly Dictionary<string, CancellationTokenSource> _listenerTokens = new();
        private readonly Dictionary<string, string> _pendingTransfers = new(); // sessionId -> fileName
        private readonly Dictionary<string, long> _fileSizes = new(); // sessionId -> fileSize
        private readonly Dictionary<string, TcpClient> _senderConnections = new(); // sessionId -> TcpClient
        private readonly Dictionary<string, TcpClient> _receiverConnections = new(); // sessionId -> TcpClient
        private readonly string _fileStoragePath;

        public FileTransferService(
            AppDbContext context,
            IHubContext<RemoteControlHub> hubContext,
            ILogger<FileTransferService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
            _configuration = configuration;
            _tempDirectory = Path.Combine(Path.GetTempPath(), "RemoteControlFiles");
            Directory.CreateDirectory(_tempDirectory);
            _fileStoragePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileStorage");
            Directory.CreateDirectory(_fileStoragePath);
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

            if (session?.HostConnectionId != null)
            {
                await _hubContext.Clients.Client(session.HostConnectionId)
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

        public async Task<(bool success, string message, int? port)> StartTcpFileTransfer(string sessionId, string fileName, long fileSize)
        {
            try
            {
                // Find an available port for sender
                var senderPort = GetAvailablePort();
                var senderListener = new TcpListener(IPAddress.Any, senderPort);
                _tcpListeners[sessionId] = senderListener;
                _listenerTokens[sessionId] = new CancellationTokenSource();
                _pendingTransfers[sessionId] = fileName;
                _fileSizes[sessionId] = fileSize;

                // Start listening for sender in background
                _ = Task.Run(async () => await ListenForSender(sessionId, _listenerTokens[sessionId].Token));

                return (true, "TCP file transfer started", senderPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start TCP file transfer");
                return (false, ex.Message, null);
            }
        }

        private int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private async Task ListenForSender(string sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var listener = _tcpListeners[sessionId];
                listener.Start();

                _logger.LogInformation($"TCP listener started on port {((IPEndPoint)listener.LocalEndpoint).Port} for session {sessionId}");

                using var sender = await listener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation($"Sender connected for file transfer in session {sessionId}");
                _senderConnections[sessionId] = sender;

                // Set TCP keep-alive
                sender.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                sender.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                sender.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                sender.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);

                // Set buffer sizes
                sender.ReceiveBufferSize = 8192;
                sender.SendBufferSize = 8192;

                // Notify receiver to connect
                await _hubContext.Clients.Group(sessionId)
                    .SendAsync("ConnectToReceiver", sessionId);

                // Wait for receiver to connect
                var receiverPort = GetAvailablePort();
                var receiverListener = new TcpListener(IPAddress.Any, receiverPort);
                receiverListener.Start();

                using var receiver = await receiverListener.AcceptTcpClientAsync(cancellationToken);
                _logger.LogInformation($"Receiver connected for file transfer in session {sessionId}");
                _receiverConnections[sessionId] = receiver;

                // Set TCP keep-alive for receiver
                receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                receiver.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 30);
                receiver.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                receiver.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);

                // Set buffer sizes for receiver
                receiver.ReceiveBufferSize = 8192;
                receiver.SendBufferSize = 8192;

                // Start forwarding data from sender to receiver
                await ForwardData(sender, receiver, sessionId, cancellationToken);

                receiverListener.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during TCP file transfer in session {sessionId}");
                await _hubContext.Clients.Group(sessionId)
                    .SendAsync("FileTransferError", sessionId, ex.Message);
            }
            finally
            {
                CleanupTcpTransfer(sessionId);
            }
        }

        private async Task ForwardData(TcpClient sender, TcpClient receiver, string sessionId, CancellationToken cancellationToken)
        {
            try
            {
                using var senderStream = sender.GetStream();
                using var receiverStream = receiver.GetStream();

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                var fileSize = _fileSizes[sessionId];

                while (totalBytesRead < fileSize)
                {
                    var bytesRead = await senderStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead == 0)
                    {
                        _logger.LogWarning($"Connection closed before transfer completed. Read {totalBytesRead} of {fileSize} bytes");
                        break;
                    }

                    await receiverStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalBytesRead += bytesRead;

                    // Notify progress through SignalR
                    var progress = (int)((totalBytesRead * 100) / fileSize);
                    await _hubContext.Clients.Group(sessionId)
                        .SendAsync("FileTransferProgress", sessionId, progress);
                }

                if (totalBytesRead == fileSize)
                {
                    _logger.LogInformation($"File transfer completed for session {sessionId}");
                    await _hubContext.Clients.Group(sessionId)
                        .SendAsync("FileTransferCompleted", sessionId);
                }
                else
                {
                    _logger.LogError($"File transfer incomplete for session {sessionId}. Received {totalBytesRead} of {fileSize} bytes");
                    await _hubContext.Clients.Group(sessionId)
                        .SendAsync("FileTransferError", sessionId, "File transfer incomplete");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding data in session {sessionId}");
                await _hubContext.Clients.Group(sessionId)
                    .SendAsync("FileTransferError", sessionId, ex.Message);
            }
        }

        private void CleanupTcpTransfer(string sessionId)
        {
            if (_senderConnections.TryGetValue(sessionId, out var sender))
            {
                sender.Dispose();
                _senderConnections.Remove(sessionId);
            }

            if (_receiverConnections.TryGetValue(sessionId, out var receiver))
            {
                receiver.Dispose();
                _receiverConnections.Remove(sessionId);
            }

            if (_tcpListeners.ContainsKey(sessionId))
            {
                _tcpListeners[sessionId].Stop();
                _tcpListeners.Remove(sessionId);
            }

            if (_listenerTokens.ContainsKey(sessionId))
            {
                _listenerTokens[sessionId].Dispose();
                _listenerTokens.Remove(sessionId);
            }

            _pendingTransfers.Remove(sessionId);
            _fileSizes.Remove(sessionId);
        }

        public Task<(bool success, string message, int? port)> ConnectToReceiver(string sessionId)
        {
            try
            {
                if (!_pendingTransfers.ContainsKey(sessionId))
                {
                    return Task.FromResult<(bool success, string message, int? port)>((false, "No pending transfer found", null));
                }

                var port = GetAvailablePort();
                return Task.FromResult<(bool success, string message, int? port)>((true, "Ready to receive file", port));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to receiver");
                return Task.FromResult<(bool success, string message, int? port)>((false, ex.Message, null));
            }
        }
    }
}