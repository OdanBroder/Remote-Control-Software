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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InputAction>()
            .HasKey(i => i.Id);

            base.OnModelCreating(modelBuilder);
        }
    }
}
