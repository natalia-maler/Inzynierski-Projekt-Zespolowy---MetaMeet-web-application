using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MetaMeetDemo.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int RecipientId { get; set; }

        public int? SenderId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "FriendRequest";

        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;
        public bool IsHandled { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(RecipientId))]
        public virtual AppUser Recipient { get; set; } = null!;

        [ForeignKey(nameof(SenderId))]
        public virtual AppUser? Sender { get; set; }
    }
}