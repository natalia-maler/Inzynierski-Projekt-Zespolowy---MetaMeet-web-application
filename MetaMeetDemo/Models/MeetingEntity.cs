using System.ComponentModel.DataAnnotations;

namespace MetaMeetDemo.Models
{
    public class MeetingEntity
    {
        [Key]
        public int Id { get; set; }
        public string GraphEventId { get; set; }
        public string ICalUId { get; set; }
        public string Subject { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string OrganizerEmail { get; set; }
        public bool IsCancelled { get; set; }
        public string JoinUrl { get; set; }

        public string? AttendeesJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}