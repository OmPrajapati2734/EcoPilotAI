using System;
using System.Collections.Generic;
using Xunit;
using EcoPilot.Api.Models;
using EcoPilot.Api.Services;

namespace EcoPilot.Api.Tests
{
    public class CarbonEngineTests
    {
        private readonly ICarbonEngine _carbonEngine;

        public CarbonEngineTests()
        {
            _carbonEngine = new CarbonEngine();
        }

        [Fact]
        public void CalculateDailyCO2_NullActivity_ReturnsZero()
        {
            var result = _carbonEngine.CalculateDailyCO2(null);
            Assert.Equal(0, result);
        }

        [Fact]
        public void CalculateDailyCO2_EmptyActivity_ReturnsMinimumEmissions()
        {
            var activity = new DailyActivity();
            var result = _carbonEngine.CalculateDailyCO2(activity);
            Assert.Equal(0.1, result); // Returns Math.Max(0.1, calculated)
        }

        [Fact]
        public void CalculateDailyCO2_CalculatesCorrectEmissions()
        {
            var activity = new DailyActivity
            {
                CarKm = 50,
                CarFuelType = "Petrol", // 50 * 0.17 = 8.5
                PublicTransitHours = 2, // 2 * 1.2 = 2.4
                ElectricityKwh = 10, // 10 * 0.38 = 3.8
                AcHours = 3, // 3 * 0.8 = 2.4
                AppliancesUsedCount = 4, // 4 * 0.05 = 0.2
                MeatServings = 2, // 2 * 2.5 = 5.0
                VegetarianServings = 3, // 3 * 0.4 = 1.2
                DairyServings = 1, // 1 * 0.8 = 0.8
                ClothingItemsBought = 1, // 1 * 15 = 15.0
                ElectronicsBought = 0,
                HouseholdSpent = 50, // 50 * 0.1 = 5.0
                PlasticWasteKg = 1.5, // 1.5 * 2.0 = 3.0
                OrganicWasteKg = 2.0, // 2.0 * 1.2 = 2.4
                RecycledWasteKg = 1.0 // 1.0 * -0.5 = -0.5
            };

            // Expected sum:
            // Transport: 8.5 + 2.4 = 10.9
            // Energy: 3.8 + 2.4 + 0.2 = 6.4
            // Food: 5.0 + 1.2 + 0.8 = 7.0
            // Shopping: 15.0 + 0 + 5.0 = 20.0
            // Waste: 3.0 + 2.4 - 0.5 = 4.9
            // Total: 10.9 + 6.4 + 7.0 + 20.0 + 4.9 = 49.2
            
            var result = _carbonEngine.CalculateDailyCO2(activity);
            Assert.Equal(49.2, result);
        }

        [Theory]
        [InlineData(5.5, 100)]
        [InlineData(4.0, 100)]
        [InlineData(6.5, 96)] // 100 - (1.0 * 4.0) = 96
        [InlineData(10.5, 80)] // 100 - (5.0 * 4.0) = 80
        [InlineData(35.5, 0)] // 100 - (30.0 * 4.0) = -20 clamp to 0
        public void CalculateCarbonScore_ReturnsExpectedScore(double dailyCO2, int expectedScore)
        {
            var result = _carbonEngine.CalculateCarbonScore(dailyCO2);
            Assert.Equal(expectedScore, result);
        }

        [Fact]
        public void ProjectCarbonScores_InsufficientHistory_ReturnsDefaultFallback()
        {
            var history = new List<DailyActivity>
            {
                new DailyActivity { DailyCO2Kg = 10.0, LogDate = DateTime.UtcNow }
            };

            var (score3m, score6m, score12m) = _carbonEngine.ProjectCarbonScores(history);
            
            Assert.Equal(85, score3m);
            Assert.Equal(90, score6m);
            Assert.Equal(95, score12m);
        }

        [Fact]
        public void ProjectCarbonScores_CalculatesRegressionCorrectly()
        {
            var today = DateTime.UtcNow.Date;
            var history = new List<DailyActivity>
            {
                new DailyActivity { DailyCO2Kg = 20.0, LogDate = today.AddDays(-4) },
                new DailyActivity { DailyCO2Kg = 18.0, LogDate = today.AddDays(-3) },
                new DailyActivity { DailyCO2Kg = 16.0, LogDate = today.AddDays(-2) },
                new DailyActivity { DailyCO2Kg = 14.0, LogDate = today.AddDays(-1) },
                new DailyActivity { DailyCO2Kg = 12.0, LogDate = today }
            };

            // Points: (0, 20), (1, 18), (2, 16), (3, 14), (4, 12)
            // Linear regression:
            // n = 5
            // x_avg = 2
            // y_avg = 16
            // slope = -2.0
            // intercept = 20.0
            // Projections (currentDayIndex = 4):
            // 3m (day 4 + 90 = 94): -2.0 * 94 + 20 = -188 + 20 = -168, max(1.0, -168) = 1.0 kg CO2
            // 6m (day 4 + 180 = 184): -2.0 * 184 + 20 = -348, max = 1.0 kg CO2
            // 12m (day 4 + 365 = 369): -2.0 * 369 + 20 = -718, max = 1.0 kg CO2
            // Expected score for 1.0 kg CO2: 100 (safe target is 5.5)

            var (score3m, score6m, score12m) = _carbonEngine.ProjectCarbonScores(history);
            
            Assert.Equal(100, score3m);
            Assert.Equal(100, score6m);
            Assert.Equal(100, score12m);
        }
    }
}
