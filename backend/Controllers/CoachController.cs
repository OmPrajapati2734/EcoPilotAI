using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoPilot.Api.Data;
using EcoPilot.Api.Services;

namespace EcoPilot.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CoachController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly IGeminiService _geminiService;
        private readonly ICarbonEngine _carbonEngine;

        public CoachController(EcoPilotDbContext context, IGeminiService geminiService, ICarbonEngine carbonEngine)
        {
            _context = context;
            _geminiService = geminiService;
            _carbonEngine = carbonEngine;
        }

        [HttpGet("tips")]
        public async Task<IActionResult> GetTips()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var weekAgo = DateTime.UtcNow.Date.AddDays(-7);
            var recentActivities = await _context.DailyActivities
                .Where(da => da.UserId == userId && da.LogDate >= weekAgo)
                .ToListAsync();

            if (!recentActivities.Any())
            {
                // Fallback baseline statistics if no logs exist yet
                var baselineTips = await _geminiService.GetCoachingTipsAsync(12.5, 15.0, 3.0, 1.5, "Transport");
                return Ok(new { tips = baselineTips, message = "Tips generated based on standard baseline carbon profile. Log your daily activities to get tailored advice." });
            }

            // Compute averages
            double avgDailyCO2 = recentActivities.Average(da => da.DailyCO2Kg);
            double avgCarKm = recentActivities.Average(da => da.CarKm);
            double avgAcHours = recentActivities.Average(da => da.AcHours);
            double avgMeatServings = recentActivities.Average(da => da.MeatServings);

            // Compute sub-aggregates to locate the highest category
            double transportSum = recentActivities.Sum(da => (da.CarKm * (da.CarFuelType?.ToLower() == "ev" ? 0.04 : 0.16)) + (da.PublicTransitHours * 1.2));
            double energySum = recentActivities.Sum(da => (da.ElectricityKwh * 0.38) + (da.AcHours * 0.8) + (da.AppliancesUsedCount * 0.05));
            double foodSum = recentActivities.Sum(da => (da.MeatServings * 2.5) + (da.VegetarianServings * 0.4) + (da.DairyServings * 0.8));
            double wasteSum = recentActivities.Sum(da => (da.PlasticWasteKg * 2.0) + (da.OrganicWasteKg * 1.2) - (da.RecycledWasteKg * 0.5));

            string highestCategory = "Transport";
            double maxVal = transportSum;

            if (energySum > maxVal) { highestCategory = "Energy"; maxVal = energySum; }
            if (foodSum > maxVal) { highestCategory = "Food"; maxVal = foodSum; }
            if (wasteSum > maxVal) { highestCategory = "Waste"; maxVal = wasteSum; }

            // Query Gemini
            var coachingTips = await _geminiService.GetCoachingTipsAsync(
                avgDailyCO2, 
                avgCarKm, 
                avgAcHours, 
                avgMeatServings, 
                highestCategory
            );

            return Ok(new { tips = coachingTips, highestCategory });
        }

        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users
                .Include(u => u.Activities)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            // Fetch recent stats to build user profile context for Gemini
            var recentActivities = await _context.DailyActivities
                .Where(da => da.UserId == userId)
                .OrderByDescending(da => da.LogDate)
                .Take(7)
                .ToListAsync();

            double avgDailyCO2 = recentActivities.Any() ? recentActivities.Average(da => da.DailyCO2Kg) : 12.5;
            double avgCarKm = recentActivities.Any() ? recentActivities.Average(da => da.CarKm) : 15.0;
            double avgAcHours = recentActivities.Any() ? recentActivities.Average(da => da.AcHours) : 3.0;
            double avgMeatServings = recentActivities.Any() ? recentActivities.Average(da => da.MeatServings) : 1.5;

            var contextText = $"- User Name: {user.Name}\n" +
                              $"- Level: {user.Level} (Streak: {user.Streak} days)\n" +
                              $"- 7-Day Average Carbon Footprint: {Math.Round(avgDailyCO2, 2)} kg CO2/day\n" +
                              $"- Average Daily Driving Distance: {Math.Round(avgCarKm, 1)} km\n" +
                              $"- Average Daily AC usage: {Math.Round(avgAcHours, 1)} hours\n" +
                              $"- Average Meat consumption servings: {Math.Round(avgMeatServings, 1)} servings/day";

            var responseText = await _geminiService.GetChatResponseAsync(request.Message, contextText);
            return Ok(new { response = responseText });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
