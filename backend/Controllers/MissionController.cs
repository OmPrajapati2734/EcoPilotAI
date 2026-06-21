using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoPilot.Api.Data;
using EcoPilot.Api.Models;
using EcoPilot.Api.Services;

namespace EcoPilot.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MissionController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly IGeminiService _geminiService;

        public MissionController(EcoPilotDbContext context, IGeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        [HttpGet]
        public async Task<IActionResult> GetMissions()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users
                .Include(u => u.Missions)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            // Clear old completed missions and assign new ones if empty
            var activeMissions = user.Missions.Where(m => !m.IsCompleted).ToList();

            if (!activeMissions.Any())
            {
                // Fetch recent logs to personalize
                var recentLogs = await _context.DailyActivities
                    .Where(da => da.UserId == userId)
                    .OrderByDescending(da => da.LogDate)
                    .Take(7)
                    .ToListAsync();

                double carKm = recentLogs.Any() ? recentLogs.Average(da => da.CarKm) : 25;
                double acHours = recentLogs.Any() ? recentLogs.Average(da => da.AcHours) : 4;
                double meatServings = recentLogs.Any() ? recentLogs.Average(da => da.MeatServings) : 1.5;

                // Retrieve missions via Gemini
                var newMissions = await _geminiService.GetPersonalizedMissionsAsync(userId, carKm, acHours, meatServings);
                
                foreach (var mission in newMissions)
                {
                    _context.UserMissions.Add(mission);
                }
                
                await _context.SaveChangesAsync();
                activeMissions = newMissions;
            }

            return Ok(activeMissions.Select(m => new
            {
                m.Id,
                m.Title,
                m.Description,
                m.Category,
                m.Difficulty,
                m.RewardXP,
                m.RewardPoints,
                m.IsCompleted
            }));
        }

        [HttpPost("complete/{id}")]
        public async Task<IActionResult> CompleteMission(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users
                .Include(u => u.Badges)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            var mission = await _context.UserMissions
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (mission == null) return NotFound("Mission not found.");
            if (mission.IsCompleted) return BadRequest("Mission is already completed.");

            // Mark as complete and log date
            mission.IsCompleted = true;
            mission.CompletedDate = DateTime.UtcNow;

            // Award Rewards
            user.XP += mission.RewardXP;
            user.Points += mission.RewardPoints;

            // Level Up logic
            bool leveledUp = false;
            if (user.XP >= 100)
            {
                user.Level += 1;
                user.XP -= 100;
                leveledUp = true;

                user.Badges.Add(new UserBadge
                {
                    Title = $"Level {user.Level} Pilot",
                    Description = $"Reached leveling status Level {user.Level}.",
                    IconEmoji = "⭐",
                    UnlockedAt = DateTime.UtcNow
                });
            }

            // Streak badges check
            if (user.Streak >= 3 && !user.Badges.Any(b => b.Title == "Streak King"))
            {
                user.Badges.Add(new UserBadge
                {
                    Title = "Streak King",
                    Description = "Maintained a continuous daily check-in streak of 3+ days.",
                    IconEmoji = "🔥",
                    UnlockedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Mission completed successfully!",
                leveledUp,
                UserStats = new { user.Level, user.XP, user.Points, user.Streak }
            });
        }
    }
}
