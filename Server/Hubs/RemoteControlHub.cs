using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Server.Services;
using Server.Models;
using Server.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Server.Hubs
{
    public class RemoteControlHub : Hub
    {
        private readonly RemoteSessionService _sessionService;
        private readonly ILogger<RemoteControlHub> _logger;
        private readonly AppDbContext _context;
        private readonly CryptoService _cryptoService;

        public RemoteControlHub(
            RemoteSessionService sessionService,
            ILogger<RemoteControlHub> logger,
            AppDbContext context,
            CryptoService cryptoService)
        {
            _sessionService = sessionService;
            _logger = logger;
            _context = context;
            _cryptoService = cryptoService;
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var sessionId = Context.GetHttpContext()?.Request.Query["sessionId"].ToString();
                if (string.IsNullOrEmpty(sessionId))
                {
                    _logger.LogWarning("Connection attempt without sessionId");
                    await Clients.Caller.SendAsync("Error", "Session ID is required");
                    Context.Abort();
                    return;
                }

                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Connection attempt without user authentication");
                    await Clients.Caller.SendAsync("Error", "Authentication required");
                    Context.Abort();
                    return;
                }

                // Generate key pair for this connection
                var (publicKey, privateKey) = _cryptoService.GenerateKeyPair();
                await Clients.Caller.SendAsync("ReceiveKeyPair", publicKey, privateKey);

                await _sessionService.AddConnection(sessionId, Context.ConnectionId, Guid.Parse(userId));

                // Notify other party about connection
                var session = await _sessionService.GetSession(sessionId);
                if (session != null)
                {
                    var targetConnectionId = session.HostConnectionId == Context.ConnectionId 
                        ? session.ClientConnectionId 
                        : session.HostConnectionId;

                    if (!string.IsNullOrEmpty(targetConnectionId))
                    {
                        await Clients.Client(targetConnectionId)
                            .SendAsync("PeerConnected", Context.ConnectionId);
                    }
                }
                _logger.LogDebug($"calling connection established for {Context.ConnectionId}");
                await Clients.Caller.SendAsync("ConnectionEstablished", Context.ConnectionId);
                await base.OnConnectedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnConnectedAsync");
                await Clients.Caller.SendAsync("Error", "Connection failed: " + ex.Message);
                Context.Abort();
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var sessionId = await _sessionService.GetSessionId(Context.ConnectionId);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var session = await _sessionService.GetSession(sessionId);
                    if (session != null)
                    {
                        // Notify other party about disconnection
                        var targetConnectionId = session.HostConnectionId == Context.ConnectionId 
                            ? session.ClientConnectionId 
                            : session.HostConnectionId;

                        if (!string.IsNullOrEmpty(targetConnectionId))
                        {
                            await Clients.Client(targetConnectionId)
                                .SendAsync("PeerDisconnected", Context.ConnectionId);
                        }
                    }

                    await _sessionService.RemoveConnection(sessionId, Context.ConnectionId);
                }

                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    _cryptoService.ClearSessionKey(userId);
                }

                await base.OnDisconnectedAsync(exception);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnDisconnectedAsync");
            }
        }

        public async Task<object> SendInputAction(string sessionId, string action)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Input action attempt without user authentication");
                    return new { success = false, message = "Authentication required", code = "AUTH_REQUIRED" };
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
                if (user == null)
                {
                    _logger.LogWarning($"User not found: {userId}");
                    return new { success = false, message = "User not found", code = "USER_NOT_FOUND" };
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId &&
                        (s.HostUserId == user.Id || s.ClientUserId == user.Id));

                if (session == null)
                {
                    _logger.LogWarning($"Session not found or not authorized: {sessionId}");
                    return new { success = false, message = "Session not found or not authorized", code = "SESSION_NOT_FOUND" };
                }

                if (session.Status != "active")
                {
                    _logger.LogWarning($"Session is not active: {sessionId}");
                    return new { success = false, message = "Session is not active", code = "SESSION_INACTIVE" };
                }

                // Check if client is connected
                if (string.IsNullOrEmpty(session.ClientConnectionId))
                {
                    _logger.LogWarning($"Client is not connected for session: {sessionId}");
                    return new { success = false, message = "Client is not connected", code = "CLIENT_DISCONNECTED" };
                }

                // Only allow host to send input actions
                if (user.Id != session.HostUserId)
                {
                    _logger.LogWarning($"Non-host user attempted to send input: {userId}");
                    return new { success = false, message = "Only host can send input actions", code = "NOT_HOST" };
                }

                // Validate the input action
                var inputAction = new InputAction
                {
                    SessionIdentifier = sessionId,
                    Action = action,
                    CreatedAt = DateTime.UtcNow
                };

                // Save to database
                _context.InputActions.Add(inputAction);
                await _context.SaveChangesAsync();

                await Clients.Client(session.ClientConnectionId).SendAsync("ReceiveInput", action);
                                
                return new { success = true, message = "Input action sent successfully", code = "INPUT_SENT" };
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, $"Error sending input action for session: {sessionId}");
                return new { success = false, message = "Failed to send input action: " + ex.Message, code = "INPUT_ERROR" };
            }
        }

        public async Task ReportInputError(object errorData)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Error report attempt without user authentication");
                    return;
                }

                _logger.LogError($"Input error reported by user {userId}: {errorData}");
                
                // Save error to database
                var errorLog = new InputError
                {
                    UserId = Guid.Parse(userId),
                    ErrorData = errorData?.ToString() ?? "Unknown error",
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.InputErrors.Add(errorLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing input error report");
            }
        }

        public async Task SendWebRTCSignal(string sessionId, WebRTCSignal signal)
        {
            try
            {
                if (!await _sessionService.ValidateSession(sessionId, Context.ConnectionId))
                {
                    _logger.LogWarning($"Invalid session attempt: {sessionId} by {Context.ConnectionId}");
                    return;
                }
                
                var targetConnectionId = await _sessionService.GetTargetConnectionId(sessionId, Context.ConnectionId);
                _logger.LogInformation($"Target Id is: {targetConnectionId}");
                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    _logger.LogWarning($"No target connection found for session: {sessionId}");
                    return;
                }

                // Forward the full signal to the peer
                var message = new WebRTCSignal
                {
                    SessionIdentifier = sessionId,
                    ConnectionId = Context.ConnectionId,
                    SignalType = signal.SignalType,
                    SignalData = signal.SignalData
                };

                await Clients.Client(targetConnectionId)
                    .SendAsync("ReceiveWebRTCSignal", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding WebRTC signal for session: {sessionId}");
            }
        }


        public async Task SendWebRTCState(string sessionId, string state)
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

                // Forward the state to the target connection
                await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCState", new
                {
                    state = state,
                    fromConnectionId = Context.ConnectionId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error forwarding WebRTC state for session: {sessionId}");
            }
        }

        public async Task AcceptFileTransfer(int transferId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("File transfer acceptance attempt without user authentication");
                    return;
                }

                var transfer = await _context.FileTransfers
                    .Include(t => t.Session)
                    .FirstOrDefaultAsync(t => t.Id == transferId);

                if (transfer == null)
                {
                    _logger.LogWarning($"File transfer {transferId} not found");
                    return;
                }

                if (transfer.ReceiverUserId.ToString() != userId)
                {
                    _logger.LogWarning($"User {userId} is not authorized to accept transfer {transferId}");
                    return;
                }

                transfer.Status = "transferring";
                await _context.SaveChangesAsync();

                // Notify both parties about the accepted transfer
                if (transfer.Session.HostConnectionId != null)
                {
                    await Clients.Client(transfer.Session.HostConnectionId)
                        .SendAsync("FileTransferAccepted", transferId);
                }

                if (transfer.Session.ClientConnectionId != null)
                {
                    await Clients.Client(transfer.Session.ClientConnectionId)
                        .SendAsync("FileTransferAccepted", transferId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error accepting file transfer {transferId}");
            }
        }

        public async Task RejectFileTransfer(int transferId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("File transfer rejection attempt without user authentication");
                    return;
                }

                var transfer = await _context.FileTransfers
                    .Include(t => t.Session)
                    .FirstOrDefaultAsync(t => t.Id == transferId);

                if (transfer == null)
                {
                    _logger.LogWarning($"File transfer {transferId} not found");
                    return;
                }

                if (transfer.ReceiverUserId.ToString() != userId)
                {
                    _logger.LogWarning($"User {userId} is not authorized to reject transfer {transferId}");
                    return;
                }

                transfer.Status = "rejected";
                transfer.ErrorMessage = "Transfer rejected by receiver";
                await _context.SaveChangesAsync();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rejecting file transfer {transferId}");
            }
        }

        public async Task FileTransferCompleted(int transferId)
        {
            try
            {
                var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("File transfer completion attempt without user authentication");
                    return;
                }

                var transfer = await _context.FileTransfers
                    .Include(t => t.Session)
                    .FirstOrDefaultAsync(t => t.Id == transferId);

                if (transfer == null)
                {
                    _logger.LogWarning($"File transfer {transferId} not found");
                    return;
                }

                if (transfer.SenderUserId.ToString() != userId && transfer.ReceiverUserId.ToString() != userId)
                {
                    _logger.LogWarning($"User {userId} is not authorized to complete transfer {transferId}");
                    return;
                }
                // Notify both parties about completion
                if (transfer.Session.HostConnectionId != null)
                {
                    await Clients.Client(transfer.Session.HostConnectionId)
                        .SendAsync("FileTransferCompleted", transferId);
                }

                if (transfer.Session.ClientConnectionId != null)
                {
                    await Clients.Client(transfer.Session.ClientConnectionId)
                        .SendAsync("FileTransferCompleted", transferId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error handling file transfer completion {transferId}");
            }
        }

        public async Task SendSignalingData(string sessionId, string peerId, string publicKey, byte[] signalingData)
        {
            try
            {
                // Encrypt the signaling data
                var encryptedData = _cryptoService.EncryptMessage(sessionId, signalingData);
                await Clients.User(peerId).SendAsync("ReceiveSignalingData", sessionId, Context.UserIdentifier, encryptedData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending signaling data");
                throw;
            }
        }

        public async Task ExchangePublicKey(string sessionId, string peerId, string publicKey)
        {
            try
            {
                await Clients.User(peerId).SendAsync("ReceivePublicKey", sessionId, Context.UserIdentifier, publicKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exchanging public key");
                throw;
            }
        }

        public async Task CompleteKeyExchange(string sessionId, string peerId, string privateKey, string peerPublicKey)
        {
            try
            {
                var sharedSecret = _cryptoService.DeriveSharedSecret(sessionId, privateKey, peerPublicKey);
                await Clients.User(peerId).SendAsync("KeyExchangeComplete", sessionId, Context.UserIdentifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing key exchange");
                throw;
            }
        }

        public async Task<string> Echo(string message)
        {
            try
            {
                _logger.LogInformation($"Echo received: {message}");
                await Task.Delay(1); // Make the method properly async
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Echo method");
                throw;
            }
        }

        public async Task NotifyFileTransferProgress(string sessionId, int progress)
        {
            try
            {
                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

                if (session == null)
                {
                    _logger.LogWarning($"Session {sessionId} not found for file transfer progress");
                    return;
                }

                // Notify both parties about progress
                if (session.HostConnectionId != null)
                {
                    await Clients.Client(session.HostConnectionId)
                        .SendAsync("FileTransferProgress", sessionId, progress);
                }

                if (session.ClientConnectionId != null)
                {
                    await Clients.Client(session.ClientConnectionId)
                        .SendAsync("FileTransferProgress", sessionId, progress);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying file transfer progress for session {sessionId}");
            }
        }

        public async Task NotifyFileTransferCompleted(string sessionId)
        {
            try
            {
                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionId);

                if (session == null)
                {
                    _logger.LogWarning($"Session {sessionId} not found for file transfer completion");
                    return;
                }

                // Notify both parties about completion
                if (session.HostConnectionId != null)
                {
                    await Clients.Client(session.HostConnectionId)
                        .SendAsync("FileTransferCompleted", sessionId);
                }

                if (session.ClientConnectionId != null)
                {
                    await Clients.Client(session.ClientConnectionId)
                        .SendAsync("FileTransferCompleted", sessionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying file transfer completion for session {sessionId}");
            }
        }
    }
}