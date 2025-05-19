using Microsoft.EntityFrameworkCore;
using Server.Models;

namespace Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<RemoteSession> RemoteSessions { get; set; }
        public DbSet<InputAction> InputActions { get; set; }
        public DbSet<ScreenData> ScreenData { get; set; }
        public DbSet<BlacklistedToken> BlacklistedTokens { get; set; }
        public DbSet<FileTransfer> FileTransfers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<SessionRecording> SessionRecordings { get; set; }
        public DbSet<MonitorInfo> MonitorInfos { get; set; }
        public DbSet<SessionStatistics> SessionStatistics { get; set; }
        public DbSet<SessionAuditLog> SessionAuditLogs { get; set; }
        public DbSet<TwoFactorAuth> TwoFactorAuths { get; set; }
        public DbSet<IpWhitelist> IpWhitelists { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InputAction>()
                .HasKey(i => i.Id);

            modelBuilder.Entity<BlacklistedToken>()
                .HasIndex(b => b.Token)
                .IsUnique();

            modelBuilder.Entity<FileTransfer>()
                .HasIndex(f => f.Status);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(c => c.Session)
                .WithMany()
                .HasForeignKey(c => c.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(c => c.Sender)
                .WithMany()
                .HasForeignKey(c => c.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SessionRecording>()
                .HasOne(r => r.Session)
                .WithMany()
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionRecording>()
                .HasOne(r => r.StartedBy)
                .WithMany()
                .HasForeignKey(r => r.StartedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MonitorInfo>()
                .HasOne(m => m.Session)
                .WithMany()
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionStatistics>()
                .HasOne(s => s.Session)
                .WithMany()
                .HasForeignKey(s => s.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionAuditLog>()
                .HasOne(a => a.Session)
                .WithMany()
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SessionAuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TwoFactorAuth>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IpWhitelist>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
