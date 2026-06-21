using System;

namespace EcoPilot.Api.Models
{
    public class DailyActivity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime LogDate { get; set; }

        // Transportation
        public double CarKm { get; set; }
        public double BikeKm { get; set; }
        public double PublicTransitHours { get; set; }
        public string CarFuelType { get; set; } = "Petrol"; // Petrol, Diesel, Hybrid, EV, None

        // Energy Usage
        public double ElectricityKwh { get; set; }
        public double AcHours { get; set; }
        public int AppliancesUsedCount { get; set; }

        // Food Consumption
        public double MeatServings { get; set; }
        public double VegetarianServings { get; set; }
        public double DairyServings { get; set; }

        // Shopping
        public int ClothingItemsBought { get; set; }
        public int ElectronicsBought { get; set; }
        public double HouseholdSpent { get; set; }

        // Waste
        public double RecycledWasteKg { get; set; }
        public double PlasticWasteKg { get; set; }
        public double OrganicWasteKg { get; set; }

        // Calculated values
        public double DailyCO2Kg { get; set; }

        // Navigation property
        public User? User { get; set; }
    }
}
