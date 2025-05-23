using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Server.Services
{
    public class RemoteSessionService
    {
        private readonly ConcurrentDictionary<string, RemoteSession> _sessions = new();
        private readonly ConcurrentDictionary<string, string> _connectionToSession = new();
        private readonly ILogger<RemoteSessionService> _logger;
        private readonly AppDbContext _context;

        public RemoteSessionService(ILogger<RemoteSessionService> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<RemoteSession?> GetSession(string sessionIdentifier)
        {
            return await _context.RemoteSessions
                .Include(s => s.HostUser)
                .Include(s => s.ClientUser)
                .FirstOrDefaultAsync(s => s.SessionIdentifier == sessionIdentifier);
        }

        public async Task AddConnection(string sessionIdentifier, string connectionId, Guid userId)
        {
            var session = await GetSession(sessionIdentifier);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionIdentifier} not found");
            }

            if (session.HostUserId == userId)
            {
                session.HostConnectionId = connectionId;
            }
            else if (session.ClientUserId == userId)
            {
                session.ClientConnectionId = connectionId;
            }
            else
            {
                throw new InvalidOperationException($"User {userId} is not authorized for session {sessionIdentifier}");
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task RemoveConnection(string sessionIdentifier, string connectionId)
        {
            var session = await GetSession(sessionIdentifier);
            if (session == null)
            {
                return;
            }

            if (session.HostConnectionId == connectionId)
            {
                session.HostConnectionId = null;
            }
            else if (session.ClientConnectionId == connectionId)
            {
                session.ClientConnectionId = null;
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ValidateSession(string sessionIdentifier, string connectionId)
        {
            var session = await GetSession(sessionIdentifier);
            if (session == null)
            {
                return false;
            }

            return session.HostConnectionId == connectionId || session.ClientConnectionId == connectionId;
        }

        public async Task<string?> GetTargetConnectionId(string sessionIdentifier, string connectionId)
        {
            var session = await GetSession(sessionIdentifier);
            if (session == null)
            {
                return null;
            }

            return session.HostConnectionId == connectionId 
                ? session.ClientConnectionId 
                : session.HostConnectionId;
        }

        public async Task<string?> GetSessionId(string connectionId)
        {
            var session = await _context.RemoteSessions
                .FirstOrDefaultAsync(s => s.HostConnectionId == connectionId || s.ClientConnectionId == connectionId);
            
            return session?.SessionIdentifier;
        }
    }
}