using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using EcoPilot.Api.Data;
using EcoPilot.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add DB Context (PostgreSQL with SQLite fallback for local demo)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddDbContext<EcoPilotDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // Local SQLite fallback
    builder.Services.AddDbContext<EcoPilotDbContext>(options =>
        options.UseSqlite("Data Source=ecopilot.db"));
}

// 2. Add DI Services
builder.Services.AddScoped<ICarbonEngine, CarbonEngine>();
builder.Services.AddHttpClient<IGeminiService, GeminiService>();

// 3. Add Authentication & JWT Bearer configurations
var jwtKey = builder.Configuration["Jwt:Key"] ?? "superSecretEcoPilotJwtKeyWithSecureEntropy2026!";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "EcoPilotServer",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "EcoPilotClient",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 4. Add Controllers
builder.Services.AddControllers();

// 5. Add CORS to support Next.js frontend calls
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNextjs", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 6. Swagger API Documentation Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EcoPilot API", Version = "v1" });
    
    // Add JWT Token support in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 7. Auto migrate database structure on startup (very useful for deployments!)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EcoPilotDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        Console.WriteLine("Database initialized successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
    }
}

// 8. Configure HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "EcoPilot API v1"));
}

app.UseCors("AllowNextjs");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new 
{ 
    message = "EcoPilot AI Backend API is running successfully!", 
    swaggerUrl = "/swagger" 
}));

app.Run();
