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
    public class LeaderboardController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly ICarbonEngine _carbonEngine;

        public LeaderboardController(EcoPilotDbContext context, ICarbonEngine carbonEngine)
        {
            _context = context;
            _carbonEngine = carbonEngine;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaderboard()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var dbUsers = await _context.Users
                .Include(u => u.Activities)
                .ToListAsync();

            var leaderboard = new List<LeaderboardEntry>();

            foreach (var user in dbUsers)
            {
                double avgCO2 = 12.5; // default baseline
                double improvementSlope = 0;
                
                if (user.Activities.Any())
                {
                    avgCO2 = user.Activities.TakeLast(7).Average(a => a.DailyCO2Kg);
                    
                    // Simple improvement slope calculation (last day vs previous)
                    if (user.Activities.Count >= 2)
                    {
                        var ordered = user.Activities.OrderBy(a => a.LogDate).ToList();
                        improvementSlope = ordered.Last().DailyCO2Kg - ordered.First().DailyCO2Kg;
                    }
                }

                int score = _carbonEngine.CalculateCarbonScore(avgCO2);
                bool isCurrentUser = user.Id == userId;

                leaderboard.Add(new LeaderboardEntry
                {
                    AnonymousName = isCurrentUser ? $"{user.Name} (You)" : $"EcoPilot #{user.Id * 3 + 17}",
                    CarbonScore = score,
                    WeeklyAverageCO2 = Math.Round(avgCO2, 1),
                    ImprovementRate = Math.Round(-improvementSlope, 2), // positive values mean they reduced emissions
                    Level = user.Level,
                    IsCurrentUser = isCurrentUser
                });
            }

            // If database is empty or has very few users, inject mock entries to make it engaging
            if (leaderboard.Count < 5)
            {
                var mockNames = new[] { "GreenWarrior", "CarbonSlayer", "SolarStar", "ForestGaurdian", "PlantLover" };
                var random = new Random();
                
                for (int i = 0; i < mockNames.Length; i++)
                {
                    if (leaderboard.Any(e => e.AnonymousName == mockNames[i])) continue;
                    
                    leaderboard.Add(new LeaderboardEntry
                    {
                        AnonymousName = mockNames[i],
                        CarbonScore = random.Next(75, 98),
                        WeeklyAverageCO2 = Math.Round(5.5 + random.NextDouble() * 8.0, 1),
                        ImprovementRate = Math.Round(random.NextDouble() * 3.0, 2),
                        Level = random.Next(3, 12),
                        IsCurrentUser = false
                    });
                }
            }

            // Sort entries
            var scoreRankings = leaderboard.OrderByDescending(e => e.CarbonScore).ToList();
            var improvementRankings = leaderboard.OrderByDescending(e => e.ImprovementRate).ToList();

            // Set ranks
            for (int i = 0; i < scoreRankings.Count; i++) scoreRankings[i].Rank = i + 1;
            for (int i = 0; i < improvementRankings.Count; i++) improvementRankings[i].Rank = i + 1;

            return Ok(new
            {
                scoreLeaderboard = scoreRankings.Take(10),
                improvementLeaderboard = improvementRankings.Take(10)
            });
        }
    }

    public class LeaderboardEntry
    {
        public int Rank { get; set; }
        public string AnonymousName { get; set; } = string.Empty;
        public int CarbonScore { get; set; }
        public double WeeklyAverageCO2 { get; set; }
        public double ImprovementRate { get; set; }
        public int Level { get; set; }
        public bool IsCurrentUser { get; set; }
    }
}
