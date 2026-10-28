using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private static readonly HashSet<string> AllowedStatuses =
    [
        "Pending",
        "Succeeded",
        "Failed",
        "Cancelled",
        "Refunded"
    ];

    public PaymentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Payment?> GetByIdAsync(long paymentId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE PaymentId = @PaymentId";

        return await connection.QuerySingleOrDefaultAsync<Payment>(
            sql, new { PaymentId = paymentId });
    }

    public async Task<IEnumerable<Payment>> GetByOrderIdAsync(long orderId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE OrderId = @OrderId
            ORDER BY CreatedAt DESC";

        return await connection.QueryAsync<Payment>(
            sql, new { OrderId = orderId });
    }

    public async Task<Payment?> GetByTransactionReferenceAsync(string transactionReference)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE ProviderReference = @ProviderReference";

        return await connection.QuerySingleOrDefaultAsync<Payment>(
            sql, new { ProviderReference = transactionReference });
    }

    public async Task<IEnumerable<Payment>> GetAllAsync(string? status = null)
    {
        if (status is not null && !AllowedStatuses.Contains(status))
        {
            throw new ArgumentException(
                "Invalid payment status.", nameof(status));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC";

        return await connection.QueryAsync<Payment>(
            sql, new { Status = status });
    }

    public async Task<long> CreateAsync(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!AllowedStatuses.Contains(payment.Status))
        {
            throw new ArgumentException(
                "Invalid payment status.", nameof(payment));
        }

        if (payment.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payment.Amount),
                "Payment amount cannot be negative.");
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO dbo.Payments (
                OrderId, Provider, ProviderReference,
                Amount, Status, PaidAt
            )
            VALUES (
                @OrderId, @Provider, @ProviderReference,
                @Amount, @Status, @PaidAt
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

        return await connection.ExecuteScalarAsync<long>(sql, payment);
    }

    /// <summary>
    /// Updates a payment's status and optionally its provider reference.
    /// Sets PaidAt when the payment first becomes Succeeded.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(long paymentId,string newStatus,string? transactionReference = null)
    {
        if (!AllowedStatuses.Contains(newStatus))
        {
            throw new ArgumentException(
                "Invalid payment status.", nameof(newStatus));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.Payments
            SET Status = @Status,
                ProviderReference = COALESCE(
                    @ProviderReference, ProviderReference),
                PaidAt = CASE
                    WHEN @Status = 'Succeeded' AND PaidAt IS NULL
                    THEN SYSUTCDATETIME()
                    ELSE PaidAt
                END
            WHERE PaymentId = @PaymentId";

        int rowsAffected = await connection.ExecuteAsync(sql, new
        {
            PaymentId = paymentId,
            Status = newStatus,
            ProviderReference = transactionReference
        });

        return rowsAffected > 0;
    }
}
