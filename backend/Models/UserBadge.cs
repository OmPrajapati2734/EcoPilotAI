using System;

namespace EcoPilot.Api.Models
{
    public class UserBadge
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconEmoji { get; set; } = "🏆";
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User? User { get; set; }
    }
}
