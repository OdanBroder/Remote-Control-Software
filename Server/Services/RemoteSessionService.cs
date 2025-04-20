using System.Collections.Concurrent;
using Server.Models;
using Microsoft.Extensions.Logging;

namespace Server.Services
{
    public class RemoteSessionService
    {
        private readonly ConcurrentDictionary<string, RemoteSession> _sessions = new();
        private readonly ConcurrentDictionary<string, string> _connectionToSession = new();
        private readonly ILogger<RemoteSessionService> _logger;

        public RemoteSessionService(ILogger<RemoteSessionService> logger)
        {
            _logger = logger;
        }

        public Task AddConnection(string sessionId, string connectionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                session = new RemoteSession 
                { 
                    SessionIdentifier = sessionId,
                    HostUserId = 0,  // Will be set when host connects
                    ClientUserId = 0, // Will be set when client connects
                    Status = "active",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _sessions.TryAdd(sessionId, session);
            }

            if (session.HostConnectionId == null)
            {
                session.HostConnectionId = connectionId;
                session.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation($"Host connected to session {sessionId}");
            }
            else if (session.ClientConnectionId == null)
            {
                session.ClientConnectionId = connectionId;
                session.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation($"Client connected to session {sessionId}");
            }
            else
            {
                _logger.LogWarning($"Attempt to connect to full session {sessionId}");
                throw new InvalidOperationException("Session is full");
            }

            _connectionToSession.TryAdd(connectionId, sessionId);
            return Task.CompletedTask;
        }

        public Task RemoveConnection(string sessionId, string connectionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                if (session.HostConnectionId == connectionId)
                {
                    session.HostConnectionId = null;
                    session.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation($"Host disconnected from session {sessionId}");
                }
                else if (session.ClientConnectionId == connectionId)
                {
                    session.ClientConnectionId = null;
                    session.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation($"Client disconnected from session {sessionId}");
                } else {
                    _logger.LogWarning($"Connection {connectionId} not authorized for session {sessionId}");
                }

                if (session.HostConnectionId == null && session.ClientConnectionId == null)
                {
                    session.Status = "ended";
                    session.UpdatedAt = DateTime.UtcNow;
                    _sessions.TryRemove(sessionId, out _);
                    _logger.LogInformation($"Session {sessionId} ended");
                }
            }

            _connectionToSession.TryRemove(connectionId, out _);
            return Task.CompletedTask;
        }

        public Task<string?> GetSessionId(string connectionId)
        {
            _connectionToSession.TryGetValue(connectionId, out var sessionId);
            if(sessionId == null) {
                _logger.LogWarning($"Connection {connectionId} not found in session");
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>(sessionId);
        }

        public Task<bool> ValidateSession(string sessionId, string connectionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                _logger.LogWarning($"Invalid session ID: {sessionId}");
                return Task.FromResult(false);
            }

            if (session.Status != "active")
            {
                _logger.LogWarning($"Session {sessionId} is not active");
                return Task.FromResult(false);
            }

            if (session.HostConnectionId != connectionId && session.ClientConnectionId != connectionId)
            {
                _logger.LogWarning($"Connection {connectionId} not authorized for session {sessionId}");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public Task<string?> GetTargetConnectionId(string sessionId, string currentConnectionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return Task.FromResult<string?>(null);
            }

            if (session.Status != "active")
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult(session.HostConnectionId == currentConnectionId ? 
                session.ClientConnectionId : 
                session.HostConnectionId);
        }
    }
}