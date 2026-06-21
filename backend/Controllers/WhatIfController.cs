using System;
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
    public class WhatIfController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly ICarbonEngine _carbonEngine;
        private readonly IGeminiService _geminiService;

        public WhatIfController(EcoPilotDbContext context, ICarbonEngine carbonEngine, IGeminiService geminiService)
        {
            _context = context;
            _carbonEngine = carbonEngine;
            _geminiService = geminiService;
        }

        [HttpPost("simulate")]
        public async Task<IActionResult> Simulate([FromBody] SimulationRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Unauthorized();
            var userId = int.Parse(userIdClaim.Value);

            // Fetch user's last recorded daily activity to establish their baseline
            var baseline = await _context.DailyActivities
                .Where(da => da.UserId == userId)
                .OrderByDescending(da => da.LogDate)
                .FirstOrDefaultAsync();

            // Setup a default baseline if none has been logged yet
            var current = baseline ?? new DailyActivity
            {
                CarKm = 30.0,
                BikeKm = 0,
                PublicTransitHours = 0,
                CarFuelType = "Petrol",
                ElectricityKwh = 12.0,
                AcHours = 4.0,
                AppliancesUsedCount = 5,
                MeatServings = 2.0,
                VegetarianServings = 0.5,
                DairyServings = 1.0,
                PlasticWasteKg = 0.5,
                RecycledWasteKg = 0.1
            };

            double currentCO2 = _carbonEngine.CalculateDailyCO2(current);

            // Clone current activity to apply simulation tweaks
            var simulated = new DailyActivity
            {
                CarKm = current.CarKm,
                BikeKm = current.BikeKm,
                PublicTransitHours = current.PublicTransitHours,
                CarFuelType = current.CarFuelType,
                ElectricityKwh = current.ElectricityKwh,
                AcHours = current.AcHours,
                AppliancesUsedCount = current.AppliancesUsedCount,
                MeatServings = current.MeatServings,
                VegetarianServings = current.VegetarianServings,
                DairyServings = current.DairyServings,
                ClothingItemsBought = current.ClothingItemsBought,
                ElectronicsBought = current.ElectronicsBought,
                HouseholdSpent = current.HouseholdSpent,
                PlasticWasteKg = current.PlasticWasteKg,
                RecycledWasteKg = current.RecycledWasteKg,
                OrganicWasteKg = current.OrganicWasteKg
            };

            double annualSavings = 0;
            string scenarioDescription = "";

            if (request.SwitchToEV)
            {
                simulated.CarFuelType = "EV";
                annualSavings += (current.CarKm * 365) * 0.08; // Fuel savings of approx $0.08 per km
                scenarioDescription += "Switch to EV, ";
            }
            if (request.WorkFromHome)
            {
                simulated.CarKm = 0;
                simulated.PublicTransitHours = 0;
                annualSavings += (current.CarKm * 365) * 0.10; // Fuel and wear savings
                scenarioDescription += "Work From Home, ";
            }
            if (request.InstallSolar)
            {
                simulated.ElectricityKwh = Math.Max(0.0, current.ElectricityKwh * 0.2); // 80% offset from solar
                annualSavings += (current.ElectricityKwh * 365) * 0.15; // Electricity savings of $0.15/kWh
                scenarioDescription += "Install Solar Panels, ";
            }
            if (request.UseBicycle)
            {
                simulated.BikeKm += current.CarKm;
                simulated.CarKm = 0;
                annualSavings += (current.CarKm * 365) * 0.12;
                scenarioDescription += "Commute by Bicycle, ";
            }
            if (request.ReduceMeat)
            {
                simulated.VegetarianServings += simulated.MeatServings;
                simulated.MeatServings = 0;
                annualSavings += (current.MeatServings * 365) * 1.50; // Grocery bills drop
                scenarioDescription += "Reduce Meat, ";
            }

            if (string.IsNullOrEmpty(scenarioDescription))
            {
                scenarioDescription = "Baseline (no changes)";
            }
            else
            {
                scenarioDescription = scenarioDescription.TrimEnd(',', ' ');
            }

            double simulatedCO2 = _carbonEngine.CalculateDailyCO2(simulated);
            double dailyCO2Saved = Math.Max(0.0, currentCO2 - simulatedCO2);
            double annualCO2Saved = dailyCO2Saved * 365;

            // Environmental Impact score out of 100 based on reduction
            int environmentalScore = _carbonEngine.CalculateCarbonScore(simulatedCO2);

            // Query Gemini to explain this specific simulation
            var explanation = await _geminiService.GetWhatIfExplanationAsync(
                scenarioDescription, 
                currentCO2, 
                simulatedCO2, 
                dailyCO2Saved
            );

            return Ok(new
            {
                currentEmissions = Math.Round(currentCO2, 2),
                predictedEmissions = Math.Round(simulatedCO2, 2),
                dailyCO2Reduction = Math.Round(dailyCO2Saved, 2),
                annualCO2Reduction = Math.Round(annualCO2Saved, 1),
                annualSavings = Math.Round(annualSavings, 2),
                environmentalScore,
                explanation
            });
        }
    }

    public class SimulationRequest
    {
        public bool SwitchToEV { get; set; }
        public bool WorkFromHome { get; set; }
        public bool InstallSolar { get; set; }
        public bool UseBicycle { get; set; }
        public bool ReduceMeat { get; set; }
    }
}
