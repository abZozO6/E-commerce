using Dapper;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure_DAL_.BackgroundServices;

/// <summary>
/// Configuration options for <see cref="PaymentExpirationService"/>.
/// Bind from <c>appsettings.json</c> section <c>"PaymentExpiration"</c>.
/// </summary>
public sealed class PaymentExpirationOptions
{
    public const string SectionName = "PaymentExpiration";

    /// <summary>
    /// How old a Pending payment must be (in minutes) before it is expired.
    /// Default: 30 minutes.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// How often the background service polls for expired payments (in minutes).
    /// Default: 5 minutes.
    /// </summary>
    public int PollingIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Background service that periodically cancels Pending payments whose
/// <c>CreatedAt</c> age exceeds <see cref="PaymentExpirationOptions.ExpirationMinutes"/>.
///
/// Design:
///   - Uses a dedicated SQL query to find and cancel expired payments atomically.
///   - Each expiration is recorded in <c>PaymentStatusAuditLogs</c> with a system-level
///     AdminUserId of 0 (reserved for automated system actions; never a real user).
///   - Order's <c>PaymentStatus</c> is updated to <c>Failed</c> for expired pending payments.
///   - Uses UPDLOCK to serialize with concurrent payment settlement attempts.
///
/// Replace with a proper job scheduler (Hangfire, Quartz) for production workloads.
/// </summary>
public sealed class PaymentExpirationService : BackgroundService
{
    private const string ExpirationReason = "Payment expired: exceeded the maximum allowed pending duration.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentExpirationService> _logger;
    private readonly PaymentExpirationOptions _options;

    public PaymentExpirationService(
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentExpirationService> logger,
        IOptions<PaymentExpirationOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PaymentExpirationService started. ExpirationMinutes={ExpirationMinutes}, PollingIntervalMinutes={PollingIntervalMinutes}",
            _options.ExpirationMinutes,
            _options.PollingIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpirePaymentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentExpirationService encountered an unexpected error.");
            }

            await Task.Delay(
                TimeSpan.FromMinutes(_options.PollingIntervalMinutes),
                stoppingToken);
        }

        _logger.LogInformation("PaymentExpirationService stopped.");
    }

    private async Task ExpirePaymentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var connectionFactory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        // Find all Pending payments older than the configured threshold.
        const string findExpiredSql = """
            SELECT PaymentId
            FROM dbo.Payments WITH (NOLOCK)
            WHERE Status = 'Pending'
              AND CreatedAt < DATEADD(MINUTE, @NegativeMinutes, SYSUTCDATETIME());
            """;

        using var connection = connectionFactory.CreateConnection();

        var expiredIds = (await connection.QueryAsync<long>(
            findExpiredSql,
            new { NegativeMinutes = -_options.ExpirationMinutes })).AsList();

        if (expiredIds.Count == 0)
            return;

        _logger.LogInformation(
            "PaymentExpirationService found {Count} expired payment(s) to cancel.",
            expiredIds.Count);

        var expiredCount = 0;
        var skippedCount = 0;

        foreach (var paymentId in expiredIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // ExpirePaymentAsync applies UPDLOCK and validates transitions.
                // Pending → Cancelled is an allowed transition.
                // If the payment was settled concurrently, the transition guard rejects it safely.
                var result = await paymentRepository.ExpirePaymentAsync(
                    paymentId,
                    ExpirationReason);

                switch (result)
                {
                    case Core.Enums.PaymentStatusUpdateResult.Updated:
                        expiredCount++;
                        _logger.LogInformation(
                            "Payment {PaymentId} expired and cancelled by system.",
                            paymentId);
                        break;

                    case Core.Enums.PaymentStatusUpdateResult.NoChange:
                    case Core.Enums.PaymentStatusUpdateResult.NotFound:
                        // Concurrently settled or already cancelled — both are fine.
                        skippedCount++;
                        break;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Transition blocked (e.g. already Succeeded) — log and continue.
                skippedCount++;
                _logger.LogDebug(
                    ex,
                    "Payment {PaymentId} could not be expired: {Reason}",
                    paymentId,
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expire payment {PaymentId}.", paymentId);
            }
        }

        _logger.LogInformation(
            "PaymentExpirationService completed. Expired={Expired}, Skipped={Skipped}.",
            expiredCount,
            skippedCount);
    }
}
