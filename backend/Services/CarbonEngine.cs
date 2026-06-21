using System;
using System.Collections.Generic;
using System.Linq;
using EcoPilot.Api.Models;

namespace EcoPilot.Api.Services
{
    public interface ICarbonEngine
    {
        double CalculateDailyCO2(DailyActivity activity);
        int CalculateCarbonScore(double dailyCO2);
        (double score3m, double score6m, double score12m) ProjectCarbonScores(List<DailyActivity> history);
    }

    public class CarbonEngine : ICarbonEngine
    {
        // Carbon emission coefficients (kg CO2 per unit)
        private const double CarPetrolKgPerKm = 0.17;
        private const double CarDieselKgPerKm = 0.15;
        private const double CarHybridKgPerKm = 0.09;
        private const double CarEVKgPerKm = 0.04;
        
        private const double PublicTransitKgPerHour = 1.2;
        private const double ElectricityKgPerKwh = 0.38;
        private const double AcKgPerHour = 0.8;
        private const double ApplianceKgPerActive = 0.05;

        private const double MeatKgPerServing = 2.5;
        private const double VegKgPerServing = 0.4;
        private const double DairyKgPerServing = 0.8;

        private const double ClothingKgPerItem = 15.0;
        private const double ElectronicsKgPerItem = 80.0;
        private const double HouseholdKgPerSpentDollar = 0.1;

        private const double PlasticWasteKgPerKg = 2.0;
        private const double OrganicWasteKgPerKg = 1.2;
        private const double RecycledCreditKgPerKg = -0.5; // Negative emission credit

        // Target daily emissions to achieve climate safe target (approx 2.0 tonnes per year)
        private const double ClimateSafeDailyTargetKg = 5.5; 

        public double CalculateDailyCO2(DailyActivity activity)
        {
            if (activity == null) return 0;

            // 1. Transportation
            double carFactor = activity.CarFuelType?.ToLower() switch
            {
                "petrol" => CarPetrolKgPerKm,
                "diesel" => CarDieselKgPerKm,
                "hybrid" => CarHybridKgPerKm,
                "ev" => CarEVKgPerKm,
                _ => 0
            };
            double transportCO2 = (activity.CarKm * carFactor) + (activity.PublicTransitHours * PublicTransitKgPerHour);

            // 2. Energy
            double energyCO2 = (activity.ElectricityKwh * ElectricityKgPerKwh) + 
                               (activity.AcHours * AcKgPerHour) + 
                               (activity.AppliancesUsedCount * ApplianceKgPerActive);

            // 3. Food
            double foodCO2 = (activity.MeatServings * MeatKgPerServing) + 
                             (activity.VegetarianServings * VegKgPerServing) + 
                             (activity.DairyServings * DairyKgPerServing);

            // 4. Shopping
            double shoppingCO2 = (activity.ClothingItemsBought * ClothingKgPerItem) + 
                                 (activity.ElectronicsBought * ElectronicsKgPerItem) + 
                                 (activity.HouseholdSpent * HouseholdKgPerSpentDollar);

            // 5. Waste
            double wasteCO2 = (activity.PlasticWasteKg * PlasticWasteKgPerKg) + 
                              (activity.OrganicWasteKg * OrganicWasteKgPerKg) + 
                              (activity.RecycledWasteKg * RecycledCreditKgPerKg);

            double total = transportCO2 + energyCO2 + foodCO2 + shoppingCO2 + wasteCO2;
            return Math.Max(0.1, Math.Round(total, 2));
        }

        public int CalculateCarbonScore(double dailyCO2)
        {
            // If emissions are below target, score is 100
            if (dailyCO2 <= ClimateSafeDailyTargetKg) return 100;

            // Otherwise, subtract points for each kg above target
            // 25 kg daily emissions drops score to ~20.
            double excess = dailyCO2 - ClimateSafeDailyTargetKg;
            double rawScore = 100.0 - (excess * 4.0);

            return Math.Clamp((int)Math.Round(rawScore), 0, 100);
        }

        public (double score3m, double score6m, double score12m) ProjectCarbonScores(List<DailyActivity> history)
        {
            if (history == null || history.Count < 2)
            {
                // Fallback default projections if not enough history
                return (85, 90, 95);
            }

            // Order by date to establish chronological history
            var orderedHistory = history.OrderBy(h => h.LogDate).ToList();
            
            // Perform linear regression of DailyCO2Kg against time (in index days)
            int n = orderedHistory.Count;
            double sumX = 0;
            double sumY = 0;
            double sumXY = 0;
            double sumXX = 0;

            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = orderedHistory[i].DailyCO2Kg;
                
                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumXX += x * x;
            }

            double denominator = (n * sumXX) - (sumX * sumX);
            double slope = 0;
            double intercept = sumY / n;

            if (Math.Abs(denominator) > 0.0001)
            {
                slope = ((n * sumXY) - (sumX * sumY)) / denominator;
                intercept = (sumY - (slope * sumX)) / n;
            }

            // Project emissions 3, 6, and 12 months out (assuming avg 30 days per month)
            double currentDayIndex = n - 1;
            double co2_3m = Math.Max(1.0, (slope * (currentDayIndex + 90)) + intercept);
            double co2_6m = Math.Max(1.0, (slope * (currentDayIndex + 180)) + intercept);
            double co2_12m = Math.Max(1.0, (slope * (currentDayIndex + 365)) + intercept);

            // Cap positive slope if user is trending worse, to keep predictions realistic (max 40kg)
            if (slope > 0)
            {
                co2_3m = Math.Min(40.0, co2_3m);
                co2_6m = Math.Min(45.0, co2_6m);
                co2_12m = Math.Min(50.0, co2_12m);
            }

            double score3m = CalculateCarbonScore(co2_3m);
            double score6m = CalculateCarbonScore(co2_6m);
            double score12m = CalculateCarbonScore(co2_12m);

            return (score3m, score6m, score12m);
        }
    }
}
