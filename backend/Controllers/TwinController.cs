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
    public class TwinController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly ICarbonEngine _carbonEngine;
        private readonly IGeminiService _geminiService;

        public TwinController(EcoPilotDbContext context, ICarbonEngine carbonEngine, IGeminiService geminiService)
        {
            _context = context;
            _carbonEngine = carbonEngine;
            _geminiService = geminiService;
        }

        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            var history = await _context.DailyActivities
                .Where(da => da.UserId == userId)
                .OrderBy(da => da.LogDate)
                .ToListAsync();

            double currentCO2 = 12.0; // Default baseline if empty
            if (history.Any())
            {
                // Take average of last 5 entries to represent "Current You" stable rate
                currentCO2 = history.TakeLast(5).Average(da => da.DailyCO2Kg);
            }

            int currentScore = _carbonEngine.CalculateCarbonScore(currentCO2);

            // Compute regression slope locally for trends
            double slope = 0;
            if (history.Count >= 2)
            {
                int n = history.Count;
                double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = i;
                    double y = history[i].DailyCO2Kg;
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumXX += x * x;
                }
                double denominator = (n * sumXX) - (sumX * sumX);
                if (Math.Abs(denominator) > 0.0001)
                {
                    slope = ((n * sumXY) - (sumX * sumY)) / denominator;
                }
            }

            // Project carbon scores (3m, 6m, 12m)
            var projections = _carbonEngine.ProjectCarbonScores(history);

            // Translate scores back to CO2 equivalents to show on graphs
            // Score = 100 - (excess * 4), excess = (100 - Score)/4, CO2 = target + excess
            double co2_3m = Math.Round(5.5 + (100 - projections.score3m) / 4.0, 2);
            double co2_6m = Math.Round(5.5 + (100 - projections.score6m) / 4.0, 2);
            double co2_12m = Math.Round(5.5 + (100 - projections.score12m) / 4.0, 2);

            // Query Gemini to explain *why* they have this forecast
            var explanation = await _geminiService.GetTwinExplanationAsync(currentCO2, co2_12m, slope);

            return Ok(new
            {
                current = new { co2 = Math.Round(currentCO2, 2), score = currentScore },
                projection3m = new { co2 = co2_3m, score = projections.score3m },
                projection6m = new { co2 = co2_6m, score = projections.score6m },
                projection12m = new { co2 = co2_12m, score = projections.score12m },
                slope,
                explanation
            });
        }
    }
}
