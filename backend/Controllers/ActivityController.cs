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
    public class ActivityController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly ICarbonEngine _carbonEngine;

        public ActivityController(EcoPilotDbContext context, ICarbonEngine carbonEngine)
        {
            _context = context;
            _carbonEngine = carbonEngine;
        }

        [HttpPost("log")]
        public async Task<IActionResult> LogActivity([FromBody] LogActivityRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users
                .Include(u => u.Badges)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            // Standardise LogDate to UTC Date (no time component)
            var logDate = request.LogDate.Date;

            // Check if user already logged for this date, if so, we overwrite it (update pattern)
            var existingActivity = await _context.DailyActivities
                .FirstOrDefaultAsync(da => da.UserId == userId && da.LogDate == logDate);

            var activity = existingActivity ?? new DailyActivity { UserId = userId, LogDate = logDate };

            // Update metrics
            activity.CarKm = request.CarKm;
            activity.BikeKm = request.BikeKm;
            activity.PublicTransitHours = request.PublicTransitHours;
            activity.CarFuelType = request.CarFuelType;
            activity.ElectricityKwh = request.ElectricityKwh;
            activity.AcHours = request.AcHours;
            activity.AppliancesUsedCount = request.AppliancesUsedCount;
            activity.MeatServings = request.MeatServings;
            activity.VegetarianServings = request.VegetarianServings;
            activity.DairyServings = request.DairyServings;
            activity.ClothingItemsBought = request.ClothingItemsBought;
            activity.ElectronicsBought = request.ElectronicsBought;
            activity.HouseholdSpent = request.HouseholdSpent;
            activity.RecycledWasteKg = request.RecycledWasteKg;
            activity.PlasticWasteKg = request.PlasticWasteKg;
            activity.OrganicWasteKg = request.OrganicWasteKg;

            // Compute carbon emissions
            activity.DailyCO2Kg = _carbonEngine.CalculateDailyCO2(activity);

            if (existingActivity == null)
            {
                _context.DailyActivities.Add(activity);
                
                // Reward tracking XP and Points
                user.XP += 20;
                user.Points += 15;
            }
            else
            {
                // Updated logs get a minor XP correction
                user.XP += 5;
            }

            // Check Level Up
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

            // Check Carbon Sentinel Badge
            if (activity.DailyCO2Kg <= 5.5 && !user.Badges.Any(b => b.Title == "Carbon Sentinel"))
            {
                user.Badges.Add(new UserBadge
                {
                    Title = "Carbon Sentinel",
                    Description = "Logged daily footprint under the climate-safe budget limit (5.5 kg).",
                    IconEmoji = "🛡️",
                    UnlockedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                activity.Id,
                activity.DailyCO2Kg,
                Score = _carbonEngine.CalculateCarbonScore(activity.DailyCO2Kg),
                LeveledUp = leveledUp,
                UserStats = new { user.Level, user.XP, user.Points, user.Streak }
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var activities = await _context.DailyActivities
                .Where(da => da.UserId == userId)
                .OrderByDescending(da => da.LogDate)
                .Take(30)
                .Select(da => new
                {
                    da.Id,
                    da.LogDate,
                    da.DailyCO2Kg,
                    Score = _carbonEngine.CalculateCarbonScore(da.DailyCO2Kg),
                    da.CarKm,
                    da.BikeKm,
                    da.PublicTransitHours,
                    da.CarFuelType,
                    da.ElectricityKwh,
                    da.AcHours,
                    da.MeatServings,
                    da.VegetarianServings,
                    da.DairyServings,
                    da.PlasticWasteKg,
                    da.RecycledWasteKg
                })
                .ToListAsync();

            return Ok(activities);
        }
    }

    public class LogActivityRequest
    {
        public DateTime LogDate { get; set; }
        public double CarKm { get; set; }
        public double BikeKm { get; set; }
        public double PublicTransitHours { get; set; }
        public string CarFuelType { get; set; } = "Petrol";
        public double ElectricityKwh { get; set; }
        public double AcHours { get; set; }
        public int AppliancesUsedCount { get; set; }
        public double MeatServings { get; set; }
        public double VegetarianServings { get; set; }
        public double DairyServings { get; set; }
        public int ClothingItemsBought { get; set; }
        public int ElectronicsBought { get; set; }
        public double HouseholdSpent { get; set; }
        public double RecycledWasteKg { get; set; }
        public double PlasticWasteKg { get; set; }
        public double OrganicWasteKg { get; set; }
    }
}
