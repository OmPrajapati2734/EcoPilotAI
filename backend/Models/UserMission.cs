using System;

namespace EcoPilot.Api.Models
{
    public class UserMission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty; // Transport, Energy, Food, Waste
        public string Difficulty { get; set; } = "Easy"; // Easy, Medium, Hard
        public int RewardXP { get; set; }
        public int RewardPoints { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime AssignedDate { get; set; }
        public DateTime? CompletedDate { get; set; }

        // Navigation property
        public User? User { get; set; }
    }
}
