using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace Server.Controllers
{
    /// <summary>
    /// Controller for handling audio streaming between clients
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AudioController : ControllerBase
    {
        private readonly ILogger<AudioController> _logger;
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions;
        private readonly int _udpPort = 5000;

        public AudioController(ILogger<AudioController> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = new ConcurrentDictionary<string, SessionInfo>();
        }

        /// <summary>
        /// Starts an audio stream for a session
        /// </summary>
        [HttpPost("start-stream")]
        public IActionResult StartStream([FromBody] StreamRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required");
            }

            try
            {
                if (string.IsNullOrEmpty(request.SessionId))
                {
                    return BadRequest("Session ID is required");
                }

                var sessionInfo = _sessions.GetOrAdd(request.SessionId, _ => new SessionInfo());
                var clientId = Guid.NewGuid().ToString();
                
                var udpClient = new UdpClient(_udpPort);
                sessionInfo.Clients.TryAdd(clientId, new ClientInfo 
                { 
                    UdpClient = udpClient,
                    EndPoint = null
                });

                _ = Task.Run(() => ListenForAudioData(request.SessionId, clientId, udpClient));

                return Ok(new { 
                    Port = _udpPort, 
                    ClientId = clientId,
                    Message = "Audio stream started successfully" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting audio stream");
                return StatusCode(500, "Failed to start audio stream");
            }
        }

        /// <summary>
        /// Stops an audio stream for a session
        /// </summary>
        [HttpPost("stop-stream")]
        public IActionResult StopStream([FromBody] StreamRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required");
            }

            try
            {
                if (string.IsNullOrEmpty(request.SessionId) || string.IsNullOrEmpty(request.ClientId))
                {
                    return BadRequest("Session ID and Client ID are required");
                }

                if (_sessions.TryGetValue(request.SessionId, out var sessionInfo))
                {
                    if (sessionInfo.Clients.TryRemove(request.ClientId, out var clientInfo))
                    {
                        clientInfo.UdpClient?.Close();
                        
                        if (sessionInfo.Clients.IsEmpty)
                        {
                            _sessions.TryRemove(request.SessionId, out _);
                        }
                        
                        return Ok(new { Message = "Audio stream stopped successfully" });
                    }
                }

                return NotFound("No active stream found for the given session and client");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping audio stream");
                return StatusCode(500, "Failed to stop audio stream");
            }
        }

        private async Task ListenForAudioData(string sessionId, string clientId, UdpClient udpClient)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentNullException(nameof(clientId));
            if (udpClient == null) throw new ArgumentNullException(nameof(udpClient));

            try
            {
                while (_sessions.TryGetValue(sessionId, out var sessionInfo) && 
                       sessionInfo.Clients.ContainsKey(clientId))
                {
                    var result = await udpClient.ReceiveAsync();
                    var audioData = result.Buffer;
                    var senderEndPoint = result.RemoteEndPoint;

                    if (sessionInfo.Clients.TryGetValue(clientId, out var clientInfo))
                    {
                        clientInfo.EndPoint = senderEndPoint;
                    }

                    await ForwardAudioData(sessionId, clientId, audioData, senderEndPoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in audio stream for session {SessionId}, client {ClientId}", sessionId, clientId);
                if (_sessions.TryGetValue(sessionId, out var sessionInfo))
                {
                    sessionInfo.Clients.TryRemove(clientId, out _);
                }
            }
        }

        private async Task ForwardAudioData(string sessionId, string senderClientId, byte[] audioData, IPEndPoint senderEndPoint)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            if (string.IsNullOrEmpty(senderClientId)) throw new ArgumentNullException(nameof(senderClientId));
            if (audioData == null) throw new ArgumentNullException(nameof(audioData));
            if (senderEndPoint == null) throw new ArgumentNullException(nameof(senderEndPoint));

            try
            {
                if (_sessions.TryGetValue(sessionId, out var sessionInfo))
                {
                    var forwardTasks = sessionInfo.Clients
                        .Where(c => c.Key != senderClientId && c.Value.EndPoint != null && c.Value.UdpClient != null)
                        .Select(async client =>
                        {
                            try
                            {
                                await client.Value.UdpClient!.SendAsync(audioData, audioData.Length, client.Value.EndPoint!);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error forwarding audio to client {ClientId}", client.Key);
                            }
                        });

                    await Task.WhenAll(forwardTasks);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding audio data for session {SessionId}", sessionId);
            }
        }
    }

    /// <summary>
    /// Request model for audio streaming operations
    /// </summary>
    public class StreamRequest
    {
        public string? SessionId { get; set; }
        public string? ClientId { get; set; }
    }

    /// <summary>
    /// Information about an audio streaming session
    /// </summary>
    public class SessionInfo
    {
        public ConcurrentDictionary<string, ClientInfo> Clients { get; } = new();
    }

    /// <summary>
    /// Information about a client in an audio streaming session
    /// </summary>
    public class ClientInfo
    {
        public UdpClient? UdpClient { get; set; }
        public IPEndPoint? EndPoint { get; set; }
    }
} 