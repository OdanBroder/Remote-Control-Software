using Microsoft.AspNetCore.SignalR;
using Server.Services;
using System.Security.Claims;

namespace Server.Hubs
{
    public class RemoteControlHub : Hub
    {
        private readonly RemoteSessionService _sessionService;
        private readonly FileTransferService _fileTransferService;
        private readonly ILogger<RemoteControlHub> _logger;

        public RemoteControlHub(
            RemoteSessionService sessionService,
            FileTransferService fileTransferService,
            ILogger<RemoteControlHub> logger)
        {
            _sessionService = sessionService;
            _fileTransferService = fileTransferService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Connection attempt without sessionId");
                Context.Abort();
                return;
            }

            await _sessionService.AddConnection(sessionId, Context.ConnectionId);
            _logger.LogInformation($"Client connected: {Context.ConnectionId} for session: {sessionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var sessionId = await _sessionService.GetSessionId(Context.ConnectionId);
            if (!string.IsNullOrEmpty(sessionId))
            {
                await _sessionService.RemoveConnection(sessionId, Context.ConnectionId);
                _logger.LogInformation($"Client disconnected: {Context.ConnectionId} from session: {sessionId}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendScreenData(string sessionId, byte[] imageData)
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

                await Clients.Client(targetConnectionId).SendAsync("ReceiveScreenData", sessionId, imageData);
                _logger.LogDebug($"Screen data sent for session: {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending screen data for session: {sessionId}");
                throw;
            }
        }

        public async Task SendInputAction(string sessionId, string action)
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

                await Clients.Client(targetConnectionId).SendAsync("ReceiveInputAction", sessionId, action);
                _logger.LogDebug($"Input action sent for session: {sessionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending input action for session: {sessionId}");
                throw;
            }
        }

        public async Task InitiateFileTransfer(string sessionId, string fileName, long fileSize)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var session = await _sessionService.GetSession(sessionId);
                if (session == null)
                {
                    return;
                }

                var senderUserId = session.HostConnectionId == Context.ConnectionId 
                    ? session.HostUserId 
                    : session.ClientUserId;
                var receiverUserId = session.HostConnectionId == Context.ConnectionId 
                    ? session.ClientUserId 
                    : session.HostUserId;

                var transfer = await _fileTransferService.InitiateFileTransfer(
                    session.Id,
                    senderUserId,
                    receiverUserId,
                    fileName,
                    fileSize);

                await Clients.Caller.SendAsync("FileTransferInitiated", transfer.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error initiating file transfer for session: {sessionId}");
                throw;
            }
        }

        public async Task SendFileChunk(string sessionId, int transferId, byte[] chunk, int offset)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var success = await _fileTransferService.ProcessFileChunk(transferId, chunk, offset);
                if (!success)
                {
                    await Clients.Caller.SendAsync("FileTransferError", transferId, "Failed to process file chunk");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending file chunk for session: {sessionId}");
                throw;
            }
        }

        public async Task CompleteFileTransfer(string sessionId, int transferId)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                await _fileTransferService.CompleteFileTransfer(transferId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error completing file transfer for session: {sessionId}");
                throw;
            }
        }

        public async Task AcceptFileTransfer(string sessionId, int transferId)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var session = await _sessionService.GetSession(sessionId);
                if (session == null)
                {
                    return;
                }

                // Notify sender that transfer was accepted
                var senderConnectionId = session.HostConnectionId == Context.ConnectionId 
                    ? session.ClientConnectionId 
                    : session.HostConnectionId;

                if (senderConnectionId != null)
                {
                    await Clients.Client(senderConnectionId)
                        .SendAsync("FileTransferAccepted", transferId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error accepting file transfer for session: {sessionId}");
                throw;
            }
        }

        public async Task RejectFileTransfer(string sessionId, int transferId)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var session = await _sessionService.GetSession(sessionId);
                if (session == null)
                {
                    return;
                }

                // Notify sender that transfer was rejected
                var senderConnectionId = session.HostConnectionId == Context.ConnectionId 
                    ? session.ClientConnectionId 
                    : session.HostConnectionId;

                if (senderConnectionId != null)
                {
                    await Clients.Client(senderConnectionId)
                        .SendAsync("FileTransferRejected", transferId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting file transfer for session: {sessionId}");
                throw;
            }
        }
    }
}