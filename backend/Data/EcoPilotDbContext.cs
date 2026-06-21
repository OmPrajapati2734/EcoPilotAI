using Microsoft.EntityFrameworkCore;
using EcoPilot.Api.Models;

namespace EcoPilot.Api.Data
{
    public class EcoPilotDbContext : DbContext
    {
        public EcoPilotDbContext(DbContextOptions<EcoPilotDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<DailyActivity> DailyActivities => Set<DailyActivity>();
        public DbSet<UserMission> UserMissions => Set<UserMission>();
        public DbSet<UserBadge> UserBadges => Set<UserBadge>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<DailyActivity>()
                .HasIndex(da => new { da.UserId, da.LogDate })
                .IsUnique();
        }
    }
}
