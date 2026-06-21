using System;
using System.Collections.Generic;

namespace EcoPilot.Api.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public int XP { get; set; } = 0;
        public int Points { get; set; } = 0;
        public int Streak { get; set; } = 0;
        public DateTime? LastActiveDate { get; set; }
        
        // Navigation properties
        public List<DailyActivity> Activities { get; set; } = new();
        public List<UserMission> Missions { get; set; } = new();
        public List<UserBadge> Badges { get; set; } = new();
    }
}
