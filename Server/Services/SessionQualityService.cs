using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Models;

namespace Server.Services
{
    public class SessionQualityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SessionQualityService> _logger;

        public SessionQualityService(AppDbContext context, ILogger<SessionQualityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task UpdateSessionStatistics(int sessionId, double bandwidthUsage, int frameRate, 
            double latency, double packetLoss, string qualityLevel, string compressionLevel)
        {
            try
            {
                var stats = new SessionStatistics
                {
                    SessionId = sessionId,
                    BandwidthUsage = bandwidthUsage,
                    FrameRate = frameRate,
                    Latency = latency,
                    PacketLoss = packetLoss,
                    QualityLevel = qualityLevel,
                    CompressionLevel = compressionLevel
                };

                _context.SessionStatistics.Add(stats);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating session statistics for session: {sessionId}");
                throw;
            }
        }

        public async Task LogSessionActivity(int sessionId, Guid userId, string action, string details, string ipAddress)
        {
            try
            {
                var log = new SessionAuditLog
                {
                    SessionId = sessionId,
                    UserId = userId,
                    Action = action,
                    Details = details,
                    IpAddress = ipAddress
                };

                _context.SessionAuditLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging session activity for session: {sessionId}");
                throw;
            }
        }

        public async Task UpdateMonitorInfo(int sessionId, List<MonitorInfo> monitors)
        {
            try
            {
                // Remove existing monitor info
                var existingMonitors = await _context.MonitorInfos
                    .Where(m => m.SessionId == sessionId)
                    .ToListAsync();

                _context.MonitorInfos.RemoveRange(existingMonitors);

                // Add new monitor info
                foreach (var monitor in monitors)
                {
                    monitor.SessionId = sessionId;
                    _context.MonitorInfos.Add(monitor);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating monitor info for session: {sessionId}");
                throw;
            }
        }

        public async Task<List<MonitorInfo>> GetMonitorInfo(int sessionId)
        {
            return await _context.MonitorInfos
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.MonitorIndex)
                .ToListAsync();
        }

        public async Task<List<SessionStatistics>> GetSessionStatistics(int sessionId, DateTime startTime, DateTime endTime)
        {
            return await _context.SessionStatistics
                .Where(s => s.SessionId == sessionId && s.Timestamp >= startTime && s.Timestamp <= endTime)
                .OrderBy(s => s.Timestamp)
                .ToListAsync();
        }

        public async Task<List<SessionAuditLog>> GetSessionAuditLogs(int sessionId, DateTime startTime, DateTime endTime)
        {
            return await _context.SessionAuditLogs
                .Where(l => l.SessionId == sessionId && l.Timestamp >= startTime && l.Timestamp <= endTime)
                .OrderBy(l => l.Timestamp)
                .ToListAsync();
        }
    }
} 