using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EcoPilot.Api.Data;
using EcoPilot.Api.Models;

namespace EcoPilot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly EcoPilotDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(EcoPilotDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Name))
            {
                return BadRequest("All registration fields are required.");
            }

            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return Conflict("Email is already registered.");
            }

            var passwordHash = HashPassword(request.Password);
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,
                Level = 1,
                XP = 20,
                Points = 30,
                Streak = 1,
                LastActiveDate = DateTime.UtcNow
            };

            // Set up some default starter badges and missions for the user
            user.Badges.Add(new UserBadge
            {
                Title = "Eco Pilot Cadet",
                Description = "Initiated session with EcoPilot AI platform.",
                IconEmoji = "🛩️",
                UnlockedAt = DateTime.UtcNow
            });

            user.Missions.Add(new UserMission
            {
                Title = "First Check-in",
                Description = "Complete your daily carbon activities tracking form.",
                Category = "General",
                Difficulty = "Easy",
                RewardXP = 20,
                RewardPoints = 15,
                IsCompleted = false,
                AssignedDate = DateTime.UtcNow
            });

            user.Missions.Add(new UserMission
            {
                Title = "Meatless Lunch",
                Description = "Have a vegetarian lunch plate today.",
                Category = "Food",
                Difficulty = "Easy",
                RewardXP = 15,
                RewardPoints = 10,
                IsCompleted = false,
                AssignedDate = DateTime.UtcNow
            });

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { token, user = new { user.Id, user.Name, user.Email, user.Level, user.XP, user.Points, user.Streak } });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest("Email and password are required.");
            }

            var user = await _context.Users
                .Include(u => u.Badges)
                .Include(u => u.Missions)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || user.PasswordHash != HashPassword(request.Password))
            {
                return Unauthorized("Invalid credentials.");
            }

            // Simple daily check-in streak logic
            if (user.LastActiveDate.HasValue)
            {
                var diff = DateTime.UtcNow.Date - user.LastActiveDate.Value.Date;
                if (diff.TotalDays == 1)
                {
                    user.Streak += 1;
                }
                else if (diff.TotalDays > 1)
                {
                    user.Streak = 1; // reset streak if gap exists
                }
            }
            user.LastActiveDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);
            return Ok(new { token, user = new { user.Id, user.Name, user.Email, user.Level, user.XP, user.Points, user.Streak } });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized();
            }

            var userId = int.Parse(userIdClaim.Value);
            var user = await _context.Users
                .Include(u => u.Badges)
                .Include(u => u.Missions)
                .Include(u => u.Activities)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            return Ok(new
            {
                user.Id,
                user.Name,
                user.Email,
                user.Level,
                user.XP,
                user.Points,
                user.Streak,
                Badges = user.Badges.Select(b => new { b.Title, b.Description, b.IconEmoji, b.UnlockedAt }),
                Missions = user.Missions.Select(m => new { m.Id, m.Title, m.Description, m.Category, m.Difficulty, m.RewardXP, m.RewardPoints, m.IsCompleted }),
                RecentActivitiesCount = user.Activities.Count
            });
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(hashedBytes);
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"] ?? "superSecretEcoPilotJwtKeyWithSecureEntropy2026!";
            var key = Encoding.ASCII.GetBytes(jwtKey);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Name)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _configuration["Jwt:Issuer"] ?? "EcoPilotServer",
                Audience = _configuration["Jwt:Audience"] ?? "EcoPilotClient"
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
