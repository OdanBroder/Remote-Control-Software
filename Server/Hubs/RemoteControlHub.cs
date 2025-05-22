using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.Services;
using Server.Models;
using Server.Data;
using System.IO;
using System.Security.Claims;
using System.Collections.Generic;

namespace Server.Hubs
{
    public class RemoteControlHub : Hub
    {
        private readonly RemoteSessionService _sessionService;
        private readonly FileTransferService _fileTransferService;
        private readonly ILogger<RemoteControlHub> _logger;
        private readonly AppDbContext _context;
        private readonly SessionQualityService _qualityService;

        public RemoteControlHub(
            RemoteSessionService sessionService,
            FileTransferService fileTransferService,
            ILogger<RemoteControlHub> logger,
            AppDbContext context,
            SessionQualityService qualityService)
        {
            _sessionService = sessionService;
            _fileTransferService = fileTransferService;
            _logger = logger;
            _context = context;
            _qualityService = qualityService;
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
                    : session.ClientUserId ?? Guid.Empty;
                var receiverUserId = session.HostConnectionId == Context.ConnectionId 
                    ? session.ClientUserId ?? Guid.Empty
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

        public async Task SendChatMessage(string sessionId, string message)
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
                    : session.ClientUserId ?? Guid.Empty;

                var chatMessage = new ChatMessage
                {
                    SessionId = session.Id,
                    SenderUserId = senderUserId,
                    Message = message,
                    MessageType = "text"
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                // Notify both parties about the new message
                if (session.HostConnectionId != null)
                {
                    await Clients.Client(session.HostConnectionId)
                        .SendAsync("ReceiveChatMessage", chatMessage.Id, message, senderUserId);
                }

                if (session.ClientConnectionId != null)
                {
                    await Clients.Client(session.ClientConnectionId)
                        .SendAsync("ReceiveChatMessage", chatMessage.Id, message, senderUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending chat message for session: {sessionId}");
                throw;
            }
        }

        public async Task StartRecording(string sessionId)
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

                var startedByUserId = session.HostConnectionId == Context.ConnectionId 
                    ? session.HostUserId 
                    : session.ClientUserId ?? Guid.Empty;

                var recording = new SessionRecording
                {
                    SessionId = session.Id,
                    StartedByUserId = startedByUserId,
                    FilePath = Path.Combine("Recordings", $"{sessionId}_{DateTime.UtcNow:yyyyMMddHHmmss}.mp4"),
                    Status = "recording"
                };

                _context.SessionRecordings.Add(recording);
                await _context.SaveChangesAsync();

                // Notify both parties about recording start
                if (session.HostConnectionId != null)
                {
                    await Clients.Client(session.HostConnectionId)
                        .SendAsync("RecordingStarted", recording.Id);
                }

                if (session.ClientConnectionId != null)
                {
                    await Clients.Client(session.ClientConnectionId)
                        .SendAsync("RecordingStarted", recording.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting recording for session: {sessionId}");
                throw;
            }
        }

        public async Task StopRecording(string sessionId, int recordingId)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }

                var recording = await _context.SessionRecordings.FindAsync(recordingId);
                if (recording == null || recording.Status != "recording")
                {
                    return;
                }

                recording.Status = "completed";
                recording.EndedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                var session = await _sessionService.GetSession(sessionId);
                if (session == null)
                {
                    return;
                }

                // Notify both parties about recording end
                if (session.HostConnectionId != null)
                {
                    await Clients.Client(session.HostConnectionId)
                        .SendAsync("RecordingStopped", recordingId, recording.FilePath);
                }

                if (session.ClientConnectionId != null)
                {
                    await Clients.Client(session.ClientConnectionId)
                        .SendAsync("RecordingStopped", recordingId, recording.FilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping recording for session: {sessionId}");
                throw;
            }
        }

        public async Task UpdateMonitorInfo(string sessionId, List<MonitorInfo> monitors)
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

                await _qualityService.UpdateMonitorInfo(session.Id, monitors);

                // Notify both parties about monitor info update
                if (session.HostConnectionId != null)
                {
                    await Clients.Client(session.HostConnectionId)
                        .SendAsync("MonitorInfoUpdated", monitors);
                }

                if (session.ClientConnectionId != null)
                {
                    await Clients.Client(session.ClientConnectionId)
                        .SendAsync("MonitorInfoUpdated", monitors);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating monitor info for session: {sessionId}");
                throw;
            }
        }

        public async Task SwitchMonitor(string sessionId, int monitorIndex)
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

                var targetConnectionId = await _sessionService.GetTargetConnectionId(sessionId, Context.ConnectionId);
                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    return;
                }

                await Clients.Client(targetConnectionId)
                    .SendAsync("SwitchMonitor", monitorIndex);

                await _qualityService.LogSessionActivity(
                    session.Id,
                    session.HostConnectionId == Context.ConnectionId ? session.HostUserId : session.ClientUserId ?? Guid.Empty,
                    "SwitchMonitor",
                    $"Switched to monitor {monitorIndex}",
                    Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error switching monitor for session: {sessionId}");
                throw;
            }
        }

        public async Task UpdateQualitySettings(string sessionId, string qualityLevel, string compressionLevel)
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

                var targetConnectionId = await _sessionService.GetTargetConnectionId(sessionId, Context.ConnectionId);
                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    return;
                }

                await Clients.Client(targetConnectionId)
                    .SendAsync("UpdateQualitySettings", qualityLevel, compressionLevel);

                await _qualityService.LogSessionActivity(
                    session.Id,
                    session.HostConnectionId == Context.ConnectionId ? session.HostUserId : session.ClientUserId ?? Guid.Empty,
                    "UpdateQualitySettings",
                    $"Updated quality settings to {qualityLevel} quality and {compressionLevel} compression",
                    Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating quality settings for session: {sessionId}");
                throw;
            }
        }

        public async Task ReportSessionStatistics(string sessionId, double bandwidthUsage, int frameRate, 
            double latency, double packetLoss, string qualityLevel, string compressionLevel)
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

                await _qualityService.UpdateSessionStatistics(
                    session.Id,
                    bandwidthUsage,
                    frameRate,
                    latency,
                    packetLoss,
                    qualityLevel,
                    compressionLevel
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reporting session statistics for session: {sessionId}");
                throw;
            }
        }
    }
}