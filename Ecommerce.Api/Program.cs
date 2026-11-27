
using System.Security.Claims;
using System.Text;
using Ecommerce.API.Middleware;
using Ecommerce.Application_BAL_;
using Ecommerce.Application_BAL_.Helpers;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure;
using Ecommerce.Infrastructure_DAL_.BackgroundServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Application and Infrastructure
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// Payment expiration background service
builder.Services.Configure<PaymentExpirationOptions>(
    builder.Configuration.GetSection(PaymentExpirationOptions.SectionName));
builder.Services.AddHostedService<PaymentExpirationService>();

// JWT Authentication Configuration
var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are missing.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key) ||
    Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException(
        "JWT signing key must be at least 32 bytes.");
}

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
    string.IsNullOrWhiteSpace(jwtSettings.Audience) ||
    jwtSettings.ExpirationMinutes <= 0)
{
    throw new InvalidOperationException(
        "JWT settings are invalid.");
}

var signingKey = new SymmetricSecurityKey(
    Encoding.UTF8.GetBytes(jwtSettings.Key));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,

        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,

        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),

        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };

    // Check whether the user account is still active.
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var userIdValue = context.Principal?
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!long.TryParse(userIdValue, out var userId) ||
                userId <= 0)
            {
                context.Fail("Invalid user identity.");
                return;
            }

            var userRepository = context.HttpContext.RequestServices
                .GetRequiredService<IUserRepository>();

            var user = await userRepository.GetByIdAsync(userId);

            if (user is null || !user.IsActive || user.IsDeleted)
            {
                context.Fail("User account is inactive or unavailable.");
            }
        }
    };
});

var app = builder.Build();

// Global Exception Handling
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Authentication must run before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
