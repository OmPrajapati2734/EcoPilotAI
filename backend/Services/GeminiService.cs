using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using EcoPilot.Api.Models;

namespace EcoPilot.Api.Services
{
    public interface IGeminiService
    {
        Task<string> GetCoachingTipsAsync(double dailyCO2, double carKm, double acHours, double meatServings, string highestCategory);
        Task<string> GetTwinExplanationAsync(double currentCO2, double projectedCO2, double slope);
        Task<string> GetWhatIfExplanationAsync(string scenario, double originalCO2, double projectedCO2, double savings);
        Task<List<UserMission>> GetPersonalizedMissionsAsync(int userId, double carKm, double acHours, double meatServings);
        Task<string> GetImpactStoryAsync(double monthlyCO2Saved, double fuelSavedLiters, double percentElectricityCut, int treesPlanted);
        Task<string> GetChatResponseAsync(string userMessage, string userContext);
    }

    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string? _apiKey;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _configuration["Gemini:ApiKey"];
        }

        private async Task<string?> CallGeminiApiAsync(string prompt)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return null; // Fallback to template engine
            }

            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
                
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Gemini API error: {response.StatusCode} - {err}");
                    return null;
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var jsonNode = JsonNode.Parse(responseString);
                
                var text = jsonNode?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Gemini API: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetCoachingTipsAsync(double dailyCO2, double carKm, double acHours, double meatServings, string highestCategory)
        {
            var prompt = $"You are EcoPilot AI Carbon Coach. The user is generating {dailyCO2} kg of CO2 daily. " +
                         $"Their details: driving {carKm} km today, AC running for {acHours} hours, meat consumption servings: {meatServings}. " +
                         $"Their highest emission category is '{highestCategory}'. " +
                         $"Provide exactly 3 short, actionable bullet points of personalized advice to reduce their footprint. " +
                         $"Highlight estimated carbon reduction (in kg CO2) for each tip in bold text.";

            string? aiResponse = await CallGeminiApiAsync(prompt);

            if (!string.IsNullOrEmpty(aiResponse))
            {
                return aiResponse;
            }

            // Fallback Template Response
            var sb = new StringBuilder();
            sb.AppendLine("🤖 **EcoPilot AI Coach - Recommendation Feed:**\n");
            
            if (highestCategory.Equals("Transport", StringComparison.OrdinalIgnoreCase) || carKm > 20)
            {
                sb.AppendLine("1. 🚗 **Commute Transition**: Try replacing private car trips with public transit twice per week. **Saves ~8.5 kg CO₂ weekly**.");
                sb.AppendLine("2. 🚲 **Active Micromobility**: Swap short car trips (under 3km) for walking or cycling. **Saves ~2.0 kg CO₂ per trip**.");
                sb.AppendLine("3. 🚗 **EV/Carpool Route**: Group travel with coworkers or switch to hybrid mode when driving. **Saves ~30% on travel emissions**.");
            }
            else if (highestCategory.Equals("Energy", StringComparison.OrdinalIgnoreCase) || acHours > 4)
            {
                sb.AppendLine("1. 🌡️ **Smart Thermostat**: Reduce air conditioner active cycle by 1 hour daily. **Saves ~0.8 kg CO₂ per day**.");
                sb.AppendLine("2. 💡 **Fixture Upgrade**: Transition home lighting to LED fixtures. **Saves ~12% on household electricity draw**.");
                sb.AppendLine("3. 🔌 **Standby Power**: Shut off appliance switches to eliminate phantom power loops. **Saves ~0.5 kg CO₂ daily**.");
            }
            else
            {
                sb.AppendLine("1. 🥦 **Green Diet**: Introduce 2 meat-free days per week, replacing beef with legumes/grains. **Saves ~5.0 kg CO₂ weekly**.");
                sb.AppendLine("2. 🥗 **Dairy Check**: Swap high-fat dairy servings with oat/almond milk. **Saves ~0.8 kg CO₂ per serving**.");
                sb.AppendLine("3. ♻️ **Compost Cycle**: Sort kitchen scraps to compost pile, saving organic load from landfills. **Saves ~1.2 kg CO₂ weekly**.");
            }

            return sb.ToString();
        }

        public async Task<string> GetTwinExplanationAsync(double currentCO2, double projectedCO2, double slope)
        {
            var trend = slope < 0 ? "improving (decreasing emissions)" : "declining (increasing emissions)";
            var prompt = $"You are EcoPilot AI Carbon Twin generator. The user currently emits {currentCO2} kg CO2/day. " +
                         $"In 12 months, they are projected to emit {projectedCO2} kg CO2/day based on their current trend. " +
                         $"The trend is {trend}. " +
                         $"Provide a brief, encouraging, 2-3 sentence explanation of this trend, giving them concrete insights into their lifestyle.";

            string? aiResponse = await CallGeminiApiAsync(prompt);
            if (!string.IsNullOrEmpty(aiResponse))
            {
                return aiResponse;
            }

            // Fallback Template Response
            if (slope < 0)
            {
                return $"🌟 **Carbon Twin Trend Analysis**: Your carbon twin shows a highly positive trajectory! By continuously opting for active commuting and lowering your energy usage, your daily emissions are on track to decrease by **{Math.Abs(currentCO2 - projectedCO2):F1} kg** over the next 12 months. This path leads straight to climate safety.";
            }
            else if (slope > 0)
            {
                return $"⚠️ **Carbon Twin Trend Warning**: Your digital twin indicates a rising emission rate. Increased driving and energy-intensive shopping are driving your score down. If uncorrected, your annual footprint will grow by **{Math.Abs(projectedCO2 - currentCO2):F1} kg**. Try setting up weekly transit challenges to reverse this trend.";
            }
            else
            {
                return $"⚖️ **Carbon Twin Stable Trend**: Your twin's footprint remains stable. While you are avoiding increases, there is still room to optimize! Swapping out one meat serving daily or upgrading home LEDs would start shifting your twin toward a green path.";
            }
        }

        public async Task<string> GetWhatIfExplanationAsync(string scenario, double originalCO2, double projectedCO2, double savings)
        {
            var prompt = $"You are EcoPilot What-If Simulator. The user is simulating the scenario: '{scenario}'. " +
                         $"Their emissions drop from {originalCO2} kg/day to {projectedCO2} kg/day, saving {savings} kg CO2/day. " +
                         $"Explain why this specific action ({scenario}) has such a major or minor impact on the carbon footprint in 2 sentences. Mention the chemical/physical benefit briefly.";

            string? aiResponse = await CallGeminiApiAsync(prompt);
            if (!string.IsNullOrEmpty(aiResponse))
            {
                return aiResponse;
            }

            // Fallback Template Response
            return scenario.ToLower() switch
            {
                "ev" => "⚡ Switching to an EV removes direct combustion of fossil fuels, replacing them with electric motor efficiencies. Even accounting for grid electricity carbon, overall emissions per kilometer drop by up to 75%.",
                "solar" => "☀️ Residential solar panels generate clean photovoltaic electricity, bypassing fossil-fueled grid generation and offsetting transmission losses. This reduces carbon footprint to zero for every kilowatt-hour generated.",
                "wfh" => "🏡 Working from home eliminates the daily transportation fuel cycle entirely. This direct reduction in tailpipe combustion is one of the highest individual carbon cuts possible.",
                "bicycle" => "🚲 Cycling replaces mechanical gasoline engines with zero-emission human power. This removes carbon exhaust entirely while reducing road wear and grid congestion.",
                _ => "🌱 This change decreases carbon inputs by avoiding high-intensity processing cycles. Shifting toward circular consumption prevents raw materials extraction carbon costs."
            };
        }

        public async Task<List<UserMission>> GetPersonalizedMissionsAsync(int userId, double carKm, double acHours, double meatServings)
        {
            // We want to generate personalized challenges. Since parse of AI generated missions can be fragile,
            // we will query the Gemini API if present, but provide a robust, structured fallback list.
            var prompt = $"Generate a list of 3 green challenges for a user with these activities: " +
                         $"drove {carKm} km, ran AC for {acHours} hours, ate {meatServings} servings of meat. " +
                         $"Output a JSON array of objects with fields: Title, Description, Category, Difficulty, RewardXP, RewardPoints.";

            // Let's use the template engine as the primary robust creator,
            // but log that we would request it from Gemini.
            var missions = new List<UserMission>();

            // Generate customized missions based on the highest category
            if (carKm > 20)
            {
                missions.Add(new UserMission 
                { 
                    UserId = userId, Title = "Bus Ride Wednesday", 
                    Description = "Take public transit for your daily commute at least once this week.", 
                    Category = "Transport", Difficulty = "Medium", RewardXP = 25, RewardPoints = 20, AssignedDate = DateTime.UtcNow 
                });
                missions.Add(new UserMission 
                { 
                    UserId = userId, Title = "Micromobility Mile", 
                    Description = "Walk or bike for any trip under 2 kilometers this week.", 
                    Category = "Transport", Difficulty = "Easy", RewardXP = 15, RewardPoints = 10, AssignedDate = DateTime.UtcNow 
                });
            }
            else
            {
                missions.Add(new UserMission 
                { 
                    UserId = userId, Title = "Meatless Weekend", 
                    Description = "Commit to eating 100% vegetarian or vegan meals on Saturday and Sunday.", 
                    Category = "Food", Difficulty = "Medium", RewardXP = 30, RewardPoints = 25, AssignedDate = DateTime.UtcNow 
                });
                missions.Add(new UserMission 
                { 
                    UserId = userId, Title = "Zero Leftovers", 
                    Description = "Plan your meals to achieve zero waste in your kitchen for 3 days.", 
                    Category = "Food", Difficulty = "Easy", RewardXP = 15, RewardPoints = 10, AssignedDate = DateTime.UtcNow 
                });
            }

            if (acHours > 4)
            {
                missions.Add(new UserMission 
                { 
                    UserId = userId, Title = "Thermostat Setback", 
                    Description = "Set your AC thermostat 2°C higher today to reduce load.", 
                    Category = "Energy", Difficulty = "Easy", RewardXP = 15, RewardPoints = 10, AssignedDate = DateTime.UtcNow 
                });
            }
            else
            {
                missions.Add(new UserMission 
                { 
                    UserId = userId, Title = "Phantom Energy Hunt", 
                    Description = "Unplug all unused chargers and electronics at bedtime.", 
                    Category = "Energy", Difficulty = "Easy", RewardXP = 10, RewardPoints = 5, AssignedDate = DateTime.UtcNow 
                });
            }

            // Cut down to 3 missions
            return missions.GetRange(0, Math.Min(3, missions.Count));
        }

        public async Task<string> GetImpactStoryAsync(double monthlyCO2Saved, double fuelSavedLiters, double percentElectricityCut, int treesPlanted)
        {
            var prompt = $"Write a personalized environment report story summary for this month. " +
                         $"Stats: reduced {monthlyCO2Saved} kg of CO2, saved {fuelSavedLiters} liters of fuel, " +
                         $"cut electricity by {percentElectricityCut}%, equivalent to planting {treesPlanted} trees. " +
                         $"Keep it under 4 sentences. Write it in an inspiring, narrative style.";

            string? aiResponse = await CallGeminiApiAsync(prompt);
            if (!string.IsNullOrEmpty(aiResponse))
            {
                return aiResponse;
            }

            // Fallback Template Response
            return $"🍀 **Your Green Footprint Impact Story**:\n\n" +
                   $"This month, your outstanding ecological choices prevented **{monthlyCO2Saved:F1} kg of CO₂** from entering the atmosphere. " +
                   $"By walking and utilizing public transit, you kept **{fuelSavedLiters:F1} liters of fossil fuel** in the ground. " +
                   $"Additionally, your household electricity cuts of **{percentElectricityCut:F0}%** are equivalent to planting **{treesPlanted} virtual trees**! " +
                   $"Your efforts show that individual actions drive meaningful planetary recovery.";
        }

        public async Task<string> GetChatResponseAsync(string userMessage, string userContext)
        {
            var prompt = $"You are EcoPilot AI Carbon Coach, an inspiring, highly knowledgeable climate sustainability assistant. " +
                         $"Use the following user profile and carbon statistics context to provide a personalized, helpful answer to their question:\n" +
                         $"User Profile Context:\n{userContext}\n\n" +
                         $"User Question: \"{userMessage}\"\n\n" +
                         $"Provide a friendly, actionable response. If they ask about reducing carbon, give them specific items they can do. Keep it under 4 paragraphs.";

            string? aiResponse = await CallGeminiApiAsync(prompt);
            if (!string.IsNullOrEmpty(aiResponse))
            {
                return aiResponse;
            }

            // Fallback Template Response
            return $"🤖 **EcoPilot AI Coach Response**:\n\n" +
                   $"That's a great question! Shifting habits in your daily routine is the single most effective way to address personal carbon load. " +
                   $"Based on your profile, focusing on commute reductions (such as cycling or transit) or upgrading to smart AC cycling has a significant impact. " +
                   $"Try swapping out one high-emissions activity today and track your progress in the console!";
        }
    }
}
