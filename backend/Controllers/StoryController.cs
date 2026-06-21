using System;
using System.Collections.Generic;
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
    public class StoryController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly IGeminiService _geminiService;

        public StoryController(EcoPilotDbContext context, IGeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthlyStory()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var user = await _context.Users
                .Include(u => u.Activities)
                .Include(u => u.Badges)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("User not found.");

            // Fetch logs for the current calendar month
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var monthlyLogs = user.Activities
                .Where(a => a.LogDate >= startOfMonth)
                .ToList();

            double totalEmissions = monthlyLogs.Sum(a => a.DailyCO2Kg);
            
            // Baseline hypothetical emissions (average citizen benchmark: ~13 tonnes annual = 35 kg daily)
            double hypotheticalBaseline = monthlyLogs.Count * 35.0; 
            double co2Saved = Math.Max(5.0, hypotheticalBaseline - totalEmissions); // ensure positive savings for stories

            // Fuel saved proxy: 1L fuel ≈ 2.3 kg CO2
            double fuelSaved = Math.Round(co2Saved / 2.3, 1);
            
            // Electricity percentage cut (baseline hypothetical 15 kWh/day vs actual)
            double actualElectricity = monthlyLogs.Any() ? monthlyLogs.Average(a => a.ElectricityKwh) : 10;
            double pctElectricityCut = Math.Clamp(Math.Round(((15.0 - actualElectricity) / 15.0) * 100), 0, 100);

            // Trees equivalent (1 tree absorbs ~22 kg CO2 per year, so 1 month offset is proportional)
            int treesEquivalent = Math.Max(1, (int)Math.Round(co2Saved / 15.0));

            // Call Gemini
            var storyText = await _geminiService.GetImpactStoryAsync(
                co2Saved,
                fuelSaved,
                pctElectricityCut,
                treesEquivalent
            );

            // Fetch achievement titles unlocked this month
            var recentBadges = user.Badges
                .Where(b => b.UnlockedAt >= startOfMonth)
                .Select(b => b.Title)
                .ToList();

            if (!recentBadges.Any())
            {
                recentBadges.Add("Active Green tracker");
            }

            return Ok(new
            {
                monthName = now.ToString("MMMM yyyy"),
                storySummary = storyText,
                carbonReductionKg = Math.Round(co2Saved, 1),
                fuelSavedLiters = fuelSaved,
                electricityCutPercentage = pctElectricityCut,
                environmentalEquivalents = new
                {
                    virtualTreesPlanted = treesEquivalent,
                    homesPoweredDay = Math.Round(co2Saved / 10.0, 1) // proxy: 1 home uses ~10 kg CO2 grid energy daily
                },
                recentAchievements = recentBadges
            });
        }
    }
}
