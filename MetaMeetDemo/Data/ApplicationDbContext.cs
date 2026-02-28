using Microsoft.EntityFrameworkCore;
using MetaMeetDemo.Models;

namespace MetaMeetDemo.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        public DbSet<MeetingEntity> Meetings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.AzureAdUserId)
                .IsUnique();

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.UserPrincipalName)
                .IsUnique();

            modelBuilder.Entity<Friendship>()
                .HasOne(f => f.User)
                .WithMany(u => u.FriendsInitiated)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Friendship>()
                .HasOne(f => f.Friend)
                .WithMany(u => u.FriendsReceived)
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Friendship>()
                .HasIndex(f => new { f.UserId, f.FriendId })
                .IsUnique();
        }
    }
}