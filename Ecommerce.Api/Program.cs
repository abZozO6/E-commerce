
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

using Ecommerce.API.Middleware;
using Ecommerce.Application_BAL_;
using Ecommerce.Application_BAL_.Helpers;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure;
using Ecommerce.Infrastructure_DAL_.BackgroundServices;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// Controllers and Swagger
// --------------------------------------------------

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --------------------------------------------------
// Exception Handling
// --------------------------------------------------

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --------------------------------------------------
// Application and Infrastructure
// --------------------------------------------------

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure();

// --------------------------------------------------
// Payment Expiration Background Service
// --------------------------------------------------

builder.Services.Configure<PaymentExpirationOptions>(
    builder.Configuration.GetSection(PaymentExpirationOptions.SectionName));

builder.Services.AddHostedService<PaymentExpirationService>();

// --------------------------------------------------
// Rate Limiting
// --------------------------------------------------

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global limit: 100 requests per minute per IP.
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    // Login: 5 requests per minute per IP.
    options.AddPolicy("Login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // Register: 3 requests per 10 minutes per IP.
    options.AddPolicy("Register", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // Response when a rate limit is exceeded.
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;

        if (context.Lease.TryGetMetadata(
                MetadataName.RetryAfter,
                out var retryAfter))
        {
            response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
        }

        await response.WriteAsJsonAsync(
            new
            {
                status = StatusCodes.Status429TooManyRequests,
                message = "Too many requests. Please try again later."
            },
            cancellationToken);
    };

    options.AddPolicy("CouponValidation", httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey:
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));

});

// --------------------------------------------------
// JWT Authentication Configuration
// --------------------------------------------------

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
                context.Fail(
                    "User account is inactive or unavailable.");
            }
        }
    };
});

// --------------------------------------------------
// Build Application
// --------------------------------------------------

var app = builder.Build();

// Global Exception Handling
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// --------------------------------------------------
// Middleware Pipeline
// --------------------------------------------------

app.UseHttpsRedirection();

app.UseRouting();

// Rate Limiting must run before authentication.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
