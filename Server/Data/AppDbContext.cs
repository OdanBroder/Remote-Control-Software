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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<InputAction>()
                .HasKey(i => i.Id);

            modelBuilder.Entity<BlacklistedToken>()
                .HasIndex(b => b.Token)
                .IsUnique();

            modelBuilder.Entity<FileTransfer>()
                .HasIndex(f => f.Status);

            base.OnModelCreating(modelBuilder);
        }
    }
}
