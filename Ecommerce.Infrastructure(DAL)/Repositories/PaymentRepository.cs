using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Enums;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using Microsoft.Data.SqlClient;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedTransitions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Succeeded", "Failed", "Cancelled"
            },
            ["Failed"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Cancelled"
            },
            ["Succeeded"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Refunded"
            },
            ["Cancelled"] = new(StringComparer.OrdinalIgnoreCase),
            ["Refunded"] = new(StringComparer.OrdinalIgnoreCase)
        };

    public PaymentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Payment?> GetByIdAsync(long paymentId)
    {
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE PaymentId = @PaymentId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Payment>(
            sql, new { PaymentId = paymentId });
    }

    public async Task<IEnumerable<Payment>> GetByOrderIdAsync(long orderId)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE OrderId = @OrderId
            ORDER BY CreatedAt DESC, PaymentId DESC;
            """;

        return await connection.QueryAsync<Payment>(
            sql, new { OrderId = orderId });
    }

    public async Task<Payment?> GetByTransactionReferenceAsync(string transactionReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionReference);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE ProviderReference = @ProviderReference;
            """;

        return await connection.QuerySingleOrDefaultAsync<Payment>(
            sql, new { ProviderReference = transactionReference.Trim() });
    }

    public async Task<IEnumerable<Payment>> GetAllAsync(string? status = null)
    {
        if (!string.IsNullOrWhiteSpace(status) && !IsKnownStatus(status))
            throw new ArgumentException("Invalid payment status.", nameof(status));

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC, PaymentId DESC;
            """;

        return await connection.QueryAsync<Payment>(
            sql, new { Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim() });
    }

    public async Task<long> CreateAsync(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (payment.OrderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(payment.OrderId));

        ArgumentException.ThrowIfNullOrWhiteSpace(payment.Provider);
        payment.Provider = payment.Provider.Trim();

        if (payment.Provider.Length > 50)
            throw new ArgumentException("Provider cannot exceed 50 characters.", nameof(payment));

        payment.ProviderReference = string.IsNullOrWhiteSpace(payment.ProviderReference)
            ? null
            : payment.ProviderReference.Trim();

        if (payment.ProviderReference?.Length > 200)
            throw new ArgumentException("Provider reference cannot exceed 200 characters.", nameof(payment));

        if (payment.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(payment.Amount), "Payment amount must be greater than zero.");

        // A newly-created payment must always start as Pending.
        payment.Status = "Pending";
        payment.PaidAt = null;

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.Payments (
                OrderId, Provider, ProviderReference,
                Amount, Status, PaidAt
            )
            VALUES (
                @OrderId, @Provider, @ProviderReference,
                @Amount, 'Pending', NULL
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, payment);
    }

    /// <summary>
    /// Applies a verified payment-provider status update and synchronizes
    /// Orders.PaymentStatus in the same database transaction.
    /// This method must only be called after authenticating the provider callback/webhook.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(long paymentId, string newStatus, string? transactionReference = null)
    {
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        newStatus = NormalizeStatus(newStatus);

        if (!IsKnownStatus(newStatus))
            throw new ArgumentException("Invalid payment status.", nameof(newStatus));

        var normalizedReference = string.IsNullOrWhiteSpace(transactionReference)
            ? null
            : transactionReference.Trim();

        if (normalizedReference?.Length > 200)
            throw new ArgumentException(
                "Transaction reference cannot exceed 200 characters.",
                nameof(transactionReference));

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string selectSql = """
                SELECT
                    p.PaymentId,
                    p.OrderId,
                    p.Provider,
                    p.ProviderReference,
                    p.Status,
                    o.Status AS OrderStatus,
                    o.PaymentStatus AS OrderPaymentStatus
                FROM dbo.Payments AS p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Orders AS o WITH (UPDLOCK, HOLDLOCK)
                    ON o.OrderId = p.OrderId
                WHERE p.PaymentId = @PaymentId;
                """;

            var payment = await connection.QuerySingleOrDefaultAsync<PaymentStatusSnapshot>(
                selectSql,
                new { PaymentId = paymentId },
                transaction);

            if (payment is null)
            {
                transaction.Rollback();
                return false;
            }

            if (string.Equals(payment.Status, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedReference is not null &&
                    payment.ProviderReference is not null &&
                    !string.Equals(payment.ProviderReference, normalizedReference, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Payment has already been finalized with a different transaction reference.");
                }

                transaction.Commit();
                return true;
            }

            if (!AllowedTransitions.TryGetValue(payment.Status, out var allowed) ||
                !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException(
                    $"Payment cannot transition from '{payment.Status}' to '{newStatus}'.");
            }

            if (string.Equals(newStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(payment.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Cannot mark a cancelled order as paid.");
                }

                if (string.Equals(payment.OrderPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(payment.OrderPaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Cannot mark a refunded order as paid.");
                }
            }

            if (normalizedReference is not null)
            {
                const string duplicateRefSql = """
                    SELECT TOP (1) PaymentId
                    FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
                    WHERE Provider = @Provider
                      AND ProviderReference = @ProviderReference
                      AND PaymentId <> @PaymentId;
                    """;

                var conflictingPaymentId = await connection.QuerySingleOrDefaultAsync<long?>(
                    duplicateRefSql,
                    new
                    {
                        payment.Provider,
                        ProviderReference = normalizedReference,
                        PaymentId = paymentId
                    },
                    transaction);

                if (conflictingPaymentId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Transaction reference '{normalizedReference}' is already used by another payment.");
                }
            }

            const string updatePaymentSql = """
                UPDATE dbo.Payments
                SET Status = @NewStatus,
                    ProviderReference = COALESCE(@ProviderReference, ProviderReference),
                    PaidAt = CASE
                        WHEN @NewStatus = 'Succeeded' THEN COALESCE(PaidAt, SYSUTCDATETIME())
                        ELSE PaidAt
                    END
                WHERE PaymentId = @PaymentId;
                """;

            await connection.ExecuteAsync(
                updatePaymentSql,
                new
                {
                    PaymentId = paymentId,
                    NewStatus = newStatus,
                    ProviderReference = normalizedReference
                },
                transaction);

            var targetOrderPaymentStatus = ResolveOrderPaymentStatus(
                newStatus,
                payment.OrderPaymentStatus);

            if (!string.Equals(targetOrderPaymentStatus, payment.OrderPaymentStatus, StringComparison.OrdinalIgnoreCase))
            {
                const string updateOrderSql = """
                    UPDATE dbo.Orders
                    SET PaymentStatus = @PaymentStatus
                    WHERE OrderId = @OrderId;
                    """;

                var orderRows = await connection.ExecuteAsync(
                    updateOrderSql,
                    new
                    {
                        OrderId = payment.OrderId,
                        PaymentStatus = targetOrderPaymentStatus
                    },
                    transaction);

                if (orderRows == 0)
                    throw new InvalidOperationException("The order associated with this payment was not found.");
            }

            transaction.Commit();
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            try { transaction.Rollback(); } catch { }
            throw new InvalidOperationException(
                "Transaction reference is already associated with another payment.",
                ex);
        }
        catch
        {
            try
            {
                transaction.Rollback();
            }
            catch
            {
                // Preserve the original exception.
            }

            throw;
        }
    }

    /// <summary>
    /// Admin-only: updates a payment status and atomically inserts an audit log
    /// record — both inside the same SQL Server transaction.
    /// Reuses the identical transition rules, locking, and order-sync logic as
    /// <see cref="UpdateStatusAsync"/>.
    /// </summary>
    public async Task<PaymentStatusUpdateResult> AdminUpdateStatusAsync(
        long paymentId,
        string newStatus,
        string? transactionReference,
        long adminUserId,
        string reason)
    {
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        if (adminUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(adminUserId));

        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        newStatus = NormalizeStatus(newStatus);

        if (!IsKnownStatus(newStatus))
            throw new ArgumentException("Invalid payment status.", nameof(newStatus));

        reason = reason.Trim();
        if (reason.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        var normalizedReference = string.IsNullOrWhiteSpace(transactionReference)
            ? null
            : transactionReference.Trim();

        if (normalizedReference?.Length > 200)
            throw new ArgumentException(
                "Transaction reference cannot exceed 200 characters.",
                nameof(transactionReference));

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string selectSql = """
                SELECT
                    p.PaymentId,
                    p.OrderId,
                    p.Provider,
                    p.ProviderReference,
                    p.Status,
                    o.Status AS OrderStatus,
                    o.PaymentStatus AS OrderPaymentStatus
                FROM dbo.Payments AS p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.Orders AS o WITH (UPDLOCK, HOLDLOCK)
                    ON o.OrderId = p.OrderId
                WHERE p.PaymentId = @PaymentId;
                """;

            var snapshot = await connection.QuerySingleOrDefaultAsync<PaymentStatusSnapshot>(
                selectSql,
                new { PaymentId = paymentId },
                transaction);

            if (snapshot is null)
            {
                transaction.Rollback();
                return PaymentStatusUpdateResult.NotFound;
            }

            var oldStatus = snapshot.Status;

            // No-op if already in the target status (idempotent for same reference)
            if (string.Equals(oldStatus, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedReference is not null &&
                    snapshot.ProviderReference is not null &&
                    !string.Equals(snapshot.ProviderReference, normalizedReference, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Payment has already been finalized with a different transaction reference.");
                }

                transaction.Commit();
                return PaymentStatusUpdateResult.NoChange;
            }

            // Validate transition
            if (!AllowedTransitions.TryGetValue(oldStatus, out var allowed) ||
                !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException(
                    $"Payment cannot transition from '{oldStatus}' to '{newStatus}'.");
            }

            // Guard: cannot mark a cancelled order as paid
            if (string.Equals(newStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(snapshot.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Cannot mark a cancelled order as paid.");

                if (string.Equals(snapshot.OrderPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(snapshot.OrderPaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Cannot mark a refunded order as paid.");
            }

            // Duplicate transaction-reference guard
            if (normalizedReference is not null)
            {
                const string duplicateRefSql = """
                    SELECT TOP (1) PaymentId
                    FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
                    WHERE Provider = @Provider
                      AND ProviderReference = @ProviderReference
                      AND PaymentId <> @PaymentId;
                    """;

                var conflictingId = await connection.QuerySingleOrDefaultAsync<long?>(
                    duplicateRefSql,
                    new
                    {
                        snapshot.Provider,
                        ProviderReference = normalizedReference,
                        PaymentId = paymentId
                    },
                    transaction);

                if (conflictingId.HasValue)
                    throw new InvalidOperationException(
                        $"Transaction reference '{normalizedReference}' is already used by another payment.");
            }

            // Update payment
            const string updatePaymentSql = """
                UPDATE dbo.Payments
                SET Status = @NewStatus,
                    ProviderReference = COALESCE(@ProviderReference, ProviderReference),
                    PaidAt = CASE
                        WHEN @NewStatus = 'Succeeded' THEN COALESCE(PaidAt, SYSUTCDATETIME())
                        ELSE PaidAt
                    END
                WHERE PaymentId = @PaymentId;
                """;

            await connection.ExecuteAsync(
                updatePaymentSql,
                new
                {
                    PaymentId = paymentId,
                    NewStatus = newStatus,
                    ProviderReference = normalizedReference
                },
                transaction);

            // Synchronize order payment status
            var targetOrderPaymentStatus = ResolveOrderPaymentStatus(
                newStatus,
                snapshot.OrderPaymentStatus);

            if (!string.Equals(targetOrderPaymentStatus, snapshot.OrderPaymentStatus, StringComparison.OrdinalIgnoreCase))
            {
                const string updateOrderSql = """
                    UPDATE dbo.Orders
                    SET PaymentStatus = @PaymentStatus
                    WHERE OrderId = @OrderId;
                    """;

                var orderRows = await connection.ExecuteAsync(
                    updateOrderSql,
                    new
                    {
                        OrderId = snapshot.OrderId,
                        PaymentStatus = targetOrderPaymentStatus
                    },
                    transaction);

                if (orderRows == 0)
                    throw new InvalidOperationException("The order associated with this payment was not found.");
            }

            // Insert audit log — same transaction (atomic)
            const string insertAuditSql = """
                INSERT INTO dbo.PaymentStatusAuditLogs
                    (PaymentId, AdminUserId, OldStatus, NewStatus,
                     TransactionReference, Reason, ChangedAt)
                VALUES
                    (@PaymentId, @AdminUserId, @OldStatus, @NewStatus,
                     @TransactionReference, @Reason, SYSUTCDATETIME());
                """;

            var auditLog = new PaymentStatusAuditLog
            {
                PaymentId            = paymentId,
                AdminUserId          = adminUserId,
                OldStatus            = oldStatus,
                NewStatus            = newStatus,
                TransactionReference = normalizedReference,
                Reason               = reason
            };

            await connection.ExecuteAsync(
                insertAuditSql,
                auditLog,
                transaction);

            transaction.Commit();
            return PaymentStatusUpdateResult.Updated;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            try { transaction.Rollback(); } catch { }
            throw new InvalidOperationException(
                "Transaction reference is already associated with another payment.",
                ex);
        }
        catch
        {
            try { transaction.Rollback(); } catch { }
            throw;
        }
    }

    private static bool IsKnownStatus(string status) =>
        status.Equals("Pending", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("Refunded", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeStatus(string status)
    {
        var normalized = status.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "pending" => "Pending",
            "succeeded" => "Succeeded",
            "failed" => "Failed",
            "cancelled" => "Cancelled",
            "refunded" => "Refunded",
            _ => normalized
        };
    }

    private static string ResolveOrderPaymentStatus(string newPaymentStatus, string currentOrderPaymentStatus)
    {
        if (string.Equals(newPaymentStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            return "Paid";

        if (string.Equals(newPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase))
            return "Refunded";

        if (string.Equals(newPaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            // Prevent late failure callbacks from downgrading already Paid/Refunded orders.
            if (string.Equals(currentOrderPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentOrderPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentOrderPaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
            {
                return currentOrderPaymentStatus;
            }

            return "Failed";
        }

        // Cancelled (and Pending) preserve the existing order payment status.
        return currentOrderPaymentStatus;
    }

    private sealed class PaymentStatusSnapshot
    {
        public long PaymentId { get; set; }
        public long OrderId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string? ProviderReference { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string OrderPaymentStatus { get; set; } = string.Empty;
    }
}
