using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Server.Services;
using Server.Hubs;
using System.Security.Claims;
using System.Text.Json;
using System;

using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace Server.Controllers
{
    [ApiController]
    [Route("api/remote")]
    public class RemoteControlController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<RemoteControlController> _logger;
        private readonly IHubContext<RemoteControlHub> _hubContext;
        private readonly InputHandlerService _inputHandler;

        public RemoteControlController(
            AppDbContext context,
            ILogger<RemoteControlController> logger,
            IHubContext<RemoteControlHub> hubContext,
            InputHandlerService inputHandler)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _inputHandler = inputHandler;
        }

        public class InputActionRequest
        {
            public required string SessionIdentifier { get; set; }
            public required InputActionData Action { get; set; }
        }

        public class InputActionData
        {
            public required string Type { get; set; }
            public required string Action { get; set; }
            public string? Key { get; set; }
            public int? X { get; set; }
            public int? Y { get; set; }
            public string? Button { get; set; }
            public string[]? Modifiers { get; set; }
        }

        [HttpPost("send-input")]
        public async Task<IActionResult> SendInput([FromBody] InputActionRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Request body is required",
                    code = "REQUEST_REQUIRED"
                });
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated",
                        code = "AUTH_REQUIRED"
                    });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

                if (user == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "User not found",
                        code = "USER_NOT_FOUND"
                    });
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == request.SessionIdentifier &&
                        (s.HostUserId == user.Id || s.ClientUserId == user.Id));

                if (session == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Session not found or not authorized",
                        code = "SESSION_NOT_FOUND"
                    });
                }

                if (session.Status != "active")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Session is not active",
                        code = "SESSION_INACTIVE"
                    });
                }

                // Check if client is connected
                if (string.IsNullOrEmpty(session.ClientConnectionId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Client is not connected",
                        code = "CLIENT_DISCONNECTED"
                    });
                }

                // Only allow host to send input actions
                if (user.Id != session.HostUserId)
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        message = "Only host can send input actions",
                        code = "HOST_ONLY"
                    });
                }

                var inputAction = new InputAction
                {
                    SessionIdentifier = request.SessionIdentifier,
                    Action = System.Text.Json.JsonSerializer.Serialize(request.Action),
                    CreatedAt = DateTime.UtcNow
                };

                // Validate the input action
                if (!_inputHandler.ValidateInput(inputAction))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Invalid input action",
                        code = "INVALID_INPUT"
                    });
                }

                // Save to database
                _context.InputActions.Add(inputAction);
                await _context.SaveChangesAsync();

                // Send the input action to the client
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };

                    string serializedAction = JsonConvert.SerializeObject(request.Action, settings);

                    //var serializedAction = JsonSerializer.Serialize(request.Action);
                    Console.WriteLine($"Action json: {serializedAction}");
                    await _hubContext.Clients.Client(session.ClientConnectionId)
                        .SendAsync("ReceiveInput", serializedAction);

                    return Ok(new
                    {
                        success = true,
                        message = "Input action processed successfully",
                        code = "INPUT_SENT"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to send input to client {session.ClientConnectionId}");
                    return StatusCode(503, new
                    {
                        success = false,
                        message = "Client connection lost",
                        code = "CLIENT_DISCONNECTED"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing input action in session {request.SessionIdentifier}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing the input action",
                    code = "SERVER_ERROR"
                });
            }
        }

        public class WebRTCSignalRequest
        {
            public required string SessionIdentifier { get; set; }
            public required string ConnectionId { get; set; }
            public required string SignalType { get; set; } // "offer", "answer", "ice-candidate"
            public required object SignalData { get; set; }
        }

        [HttpPost("webrtc/signal")]
        public async Task<IActionResult> HandleWebRTCSignal([FromBody] WebRTCSignalRequest request)
        {
            if (request == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Request body is required",
                    code = "REQUEST_REQUIRED"
                });
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated",
                        code = "AUTH_REQUIRED"
                    });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));
                if (user == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "User not found",
                        code = "USER_NOT_FOUND"
                    });
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.SessionIdentifier == request.SessionIdentifier &&
                        (s.HostUserId == user.Id || s.ClientUserId == user.Id));

                if (session == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Session not found or not authorized",
                        code = "SESSION_NOT_FOUND"
                    });
                }

                if (session.Status != "active")
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Session is not active",
                        code = "SESSION_INACTIVE"
                    });
                }

                // Determine if user is host or client
                var connectionType = user.Id == session.HostUserId ? "host" : "client";
                var targetConnectionId = connectionType == "host" ? session.ClientConnectionId : session.HostConnectionId;

                if (string.IsNullOrEmpty(targetConnectionId))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Target peer is not connected",
                        code = "PEER_DISCONNECTED"
                    });
                }

                // Update or create WebRTC connection record
                var webrtcConnection = await _context.WebRTCConnections
                    .FirstOrDefaultAsync(w => w.SessionId == session.Id && w.ConnectionId == request.ConnectionId);

                if (webrtcConnection == null)
                {
                    webrtcConnection = new WebRTCConnection
                    {
                        SessionId = session.Id,
                        ConnectionId = request.ConnectionId,
                        ConnectionType = connectionType,
                        Status = "pending"
                    };
                    _context.WebRTCConnections.Add(webrtcConnection);
                }

                // Update connection data based on signal type
                var signalDataJson = System.Text.Json.JsonSerializer.Serialize(request.SignalData);
                switch (request.SignalType.ToLower())
                {
                    case "offer":
                        webrtcConnection.Offer = signalDataJson;
                        webrtcConnection.Status = "pending";
                        break;
                    case "answer":
                        webrtcConnection.Answer = signalDataJson;
                        webrtcConnection.Status = "connected";
                        break;
                    case "ice-candidate":
                        webrtcConnection.IceCandidates = signalDataJson;
                        break;
                    default:
                        return BadRequest(new
                        {
                            success = false,
                            message = "Invalid signal type",
                            code = "INVALID_SIGNAL_TYPE"
                        });
                }

                // Save screen data for tracking
                var screenData = new ScreenData
                {
                    SessionId = session.Id,
                    SenderConnectionId = request.ConnectionId,
                    SignalTypeId = await GetSignalTypeId(request.SignalType),
                    CreatedAt = DateTime.UtcNow
                };
                screenData.SetSignalData(request.SignalData);
                _context.ScreenData.Add(screenData);

                await _context.SaveChangesAsync();

                // Forward the signal to the target peer
                try
                {
                    await _hubContext.Clients.Client(targetConnectionId)
                        .SendAsync("WebRTCSignal", new
                        {
                            signalType = request.SignalType,
                            signalData = request.SignalData,
                            connectionId = request.ConnectionId
                        });

                    return Ok(new
                    {
                        success = true,
                        message = "Signal forwarded successfully",
                        code = "SIGNAL_FORWARDED"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to forward WebRTC signal to peer {targetConnectionId}");
                    return StatusCode(503, new
                    {
                        success = false,
                        message = "Failed to forward signal to peer",
                        code = "SIGNAL_FORWARD_FAILED"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing WebRTC signal for session {request.SessionIdentifier}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while processing the WebRTC signal",
                    code = "SERVER_ERROR"
                });
            }
        }

        private async Task<int> GetSignalTypeId(string signalType)
        {
            // Map incoming signal types to database values
            string mappedType = signalType.ToLower() switch
            {
                "offer" => "sdp_offer",
                "answer" => "sdp_answer",
                "ice-candidate" => "ice_candidate",
                _ => signalType.ToLower()
            };

            var type = await _context.SignalTypes
                .FirstOrDefaultAsync(t => t.TypeName == mappedType);

            if (type == null)
            {
                type = new SignalType
                {
                    TypeName = mappedType,
                    Description = $"WebRTC {signalType} signal",
                    CreatedAt = DateTime.UtcNow
                };
                _context.SignalTypes.Add(type);
                await _context.SaveChangesAsync();
            }

            return type.Id;
        }

        [HttpPost("webrtc/stats")]
        public async Task<IActionResult> UpdateWebRTCStats([FromBody] WebRTCStats stats)
        {
            if (stats == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Stats data is required",
                    code = "STATS_REQUIRED"
                });
            }

            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        message = "User not authenticated",
                        code = "AUTH_REQUIRED"
                    });
                }

                var session = await _context.RemoteSessions
                    .FirstOrDefaultAsync(s => s.Id == stats.SessionId &&
                        (s.HostUserId == Guid.Parse(userId) || s.ClientUserId == Guid.Parse(userId)));

                if (session == null)
                {
                    return NotFound(new
                    {
                        success = false,
                        message = "Session not found or not authorized",
                        code = "SESSION_NOT_FOUND"
                    });
                }

                stats.Timestamp = DateTime.UtcNow;
                _context.WebRTCStats.Add(stats);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Stats updated successfully",
                    code = "STATS_UPDATED"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating WebRTC stats for session {stats.SessionId}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred while updating WebRTC stats",
                    code = "SERVER_ERROR"
                });
            }
        }
    }
}