using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaMeetDemo.Models
{
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string AzureAdUserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string UserPrincipalName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Friendship> FriendsInitiated { get; set; } = new List<Friendship>();
        public virtual ICollection<Friendship> FriendsReceived { get; set; } = new List<Friendship>();
    }

    public class Friendship
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int FriendId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public virtual AppUser User { get; set; } = null!;

        [ForeignKey(nameof(FriendId))]
        public virtual AppUser Friend { get; set; } = null!;
    }

    public class UserPreference
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int AppUserId { get; set; }

        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Value { get; set; } = string.Empty;

        [ForeignKey(nameof(AppUserId))]
        public virtual AppUser AppUser { get; set; } = null!;
    }
}