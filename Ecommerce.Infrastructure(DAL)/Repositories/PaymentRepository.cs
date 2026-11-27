using System.Data;
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

        return await connection.QueryAsync<Payment>(sql, new { OrderId = orderId });
    }

    public async Task<Payment?> GetByTransactionReferenceAsync(string transactionReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionReference);

        using var connection = _connectionFactory.CreateConnection();

        // The database guarantees uniqueness by (Provider, ProviderReference),
        // not by ProviderReference alone. Detect ambiguity instead of returning
        // an arbitrary payment from a different provider.
        const string sql = """
            SELECT TOP (2)
                   PaymentId, OrderId, Provider, ProviderReference,
                   Amount, Status, PaidAt, CreatedAt
            FROM dbo.Payments
            WHERE ProviderReference = @ProviderReference
            ORDER BY CreatedAt DESC, PaymentId DESC;
            """;

        var matches = (await connection.QueryAsync<Payment>(
            sql, new { ProviderReference = transactionReference.Trim() })).AsList();

        if (matches.Count > 1)
            throw new InvalidOperationException(
                "Transaction reference is ambiguous. Look it up using both Provider and ProviderReference.");

        return matches.SingleOrDefault();
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
            sql,
            new { Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim() });
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
            throw new ArgumentOutOfRangeException(
                nameof(payment.Amount), "Payment amount must be greater than zero.");

        // Never trust caller-supplied initial state.
        payment.Status = "Pending";
        payment.PaidAt = null;

        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // All payment write operations lock Order first, then Payment rows.
            // This consistent order reduces deadlock risk and serializes attempts per order.
            const string orderSql = """
                SELECT OrderId, Status, PaymentStatus, TotalAmount
                FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK)
                WHERE OrderId = @OrderId;
                """;

            var order = await connection.QuerySingleOrDefaultAsync<OrderPaymentCreationSnapshot>(
                orderSql, new { payment.OrderId }, transaction);

            if (order is null)
                throw new InvalidOperationException("Order was not found.");

            EnsureOrderCanAcceptPayment(order.Status, order.PaymentStatus);

            if (order.TotalAmount <= 0 || payment.Amount != order.TotalAmount)
                throw new InvalidOperationException("Payment amount does not match the order total.");

            const string pendingPaymentSql = """
                SELECT COUNT(1)
                FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
                WHERE OrderId = @OrderId
                  AND Status = 'Pending';
                """;

            var pendingCount = await connection.ExecuteScalarAsync<int>(
                pendingPaymentSql, new { payment.OrderId }, transaction);

            if (pendingCount > 0)
                throw new InvalidOperationException("This order already has a pending payment.");

            const string insertSql = """
                INSERT INTO dbo.Payments
                    (OrderId, Provider, ProviderReference, Amount, Status, PaidAt)
                VALUES
                    (@OrderId, @Provider, @ProviderReference, @Amount, 'Pending', NULL);

                SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
                """;

            var paymentId = await connection.ExecuteScalarAsync<long>(insertSql, payment, transaction);
            transaction.Commit();
            return paymentId;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            TryRollback(transaction);
            throw new InvalidOperationException(
                "A conflicting payment already exists for this order or transaction reference.", ex);
        }
        catch
        {
            TryRollback(transaction);
            throw;
        }
    }

    /// <summary>
    /// Applies a verified provider result. The caller must authenticate the provider callback
    /// before invoking this method. Payment and order updates are atomic.
    /// </summary>
    public async Task<bool> UpdateStatusAsync(
        long paymentId,
        string newStatus,
        string? transactionReference = null)
    {
        ValidateStatusUpdateArguments(paymentId, newStatus, transactionReference);

        var result = await ApplyStatusUpdateAsync(
            paymentId,
            NormalizeStatus(newStatus),
            NormalizeReference(transactionReference),
            adminUserId: null,
            reason: null);

        return result != PaymentStatusUpdateResult.NotFound;
    }

    /// <summary>
    /// Admin-only status update. The payment, order, and audit record are committed atomically.
    /// </summary>
    public async Task<PaymentStatusUpdateResult> AdminUpdateStatusAsync(
        long paymentId,
        string newStatus,
        string? transactionReference,
        long adminUserId,
        string reason)
    {
        ValidateStatusUpdateArguments(paymentId, newStatus, transactionReference);

        if (adminUserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(adminUserId));

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        reason = reason.Trim();

        if (reason.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        return await ApplyStatusUpdateAsync(
            paymentId,
            NormalizeStatus(newStatus),
            NormalizeReference(transactionReference),
            adminUserId,
            reason);
    }

    /// <summary>
    /// System-initiated expiration of a pending payment. Atomically transitions to Cancelled and records an audit log.
    /// </summary>
    public async Task<PaymentStatusUpdateResult> ExpirePaymentAsync(long paymentId, string reason)
    {
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        reason = reason.Trim();

        if (reason.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        return await ApplyStatusUpdateAsync(
            paymentId,
            newStatus: "Cancelled",
            normalizedReference: null,
            adminUserId: null,
            reason: reason);
    }

    private async Task<PaymentStatusUpdateResult> ApplyStatusUpdateAsync(
        long paymentId,
        string newStatus,
        string? normalizedReference,
        long? adminUserId,
        string? reason)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // First read only the immutable OrderId. Do not take a payment update lock yet.
            const string findOrderIdSql = """
                SELECT OrderId
                FROM dbo.Payments
                WHERE PaymentId = @PaymentId;
                """;

            var orderId = await connection.QuerySingleOrDefaultAsync<long?>(
                findOrderIdSql, new { PaymentId = paymentId }, transaction);

            if (!orderId.HasValue)
            {
                transaction.Rollback();
                return PaymentStatusUpdateResult.NotFound;
            }

            // Lock order first. CreateAsync and all status updates use this same lock order.
            const string orderSql = """
                SELECT OrderId, Status AS OrderStatus, PaymentStatus AS OrderPaymentStatus
                FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK)
                WHERE OrderId = @OrderId;
                """;

            var order = await connection.QuerySingleOrDefaultAsync<OrderPaymentStatusSnapshot>(
                orderSql, new { OrderId = orderId.Value }, transaction);

            if (order is null)
                throw new InvalidOperationException("The order associated with this payment was not found.");

            // Now lock the payment row after acquiring the order lock.
            const string paymentSql = """
                SELECT PaymentId, OrderId, Provider, ProviderReference, Status
                FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
                WHERE PaymentId = @PaymentId AND OrderId = @OrderId;
                """;

            var payment = await connection.QuerySingleOrDefaultAsync<PaymentStatusSnapshot>(
                paymentSql,
                new { PaymentId = paymentId, OrderId = orderId.Value },
                transaction);

            if (payment is null)
            {
                transaction.Rollback();
                return PaymentStatusUpdateResult.NotFound;
            }

            // Enforce provider-reference uniqueness before both normal updates and retries.
            if (normalizedReference is not null)
            {
                const string duplicateReferenceSql = """
                    SELECT TOP (1) PaymentId
                    FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
                    WHERE Provider = @Provider
                      AND ProviderReference = @ProviderReference
                      AND PaymentId <> @PaymentId;
                    """;

                var duplicatePaymentId = await connection.QuerySingleOrDefaultAsync<long?>(
                    duplicateReferenceSql,
                    new
                    {
                        payment.Provider,
                        ProviderReference = normalizedReference,
                        PaymentId = paymentId
                    },
                    transaction);

                if (duplicatePaymentId.HasValue)
                    throw new InvalidOperationException(
                        "Transaction reference is already associated with another payment.");
            }

            // Idempotent retry: same status is accepted, but conflicting references are rejected.
            if (string.Equals(payment.Status, newStatus, StringComparison.OrdinalIgnoreCase))
            {
                if (normalizedReference is not null &&
                    payment.ProviderReference is not null &&
                    !string.Equals(payment.ProviderReference, normalizedReference,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Payment has already been finalized with a different transaction reference.");
                }

                // If the first request did not include a reference, safely attach it on retry.
                if (normalizedReference is not null && payment.ProviderReference is null)
                {
                    const string attachReferenceSql = """
                        UPDATE dbo.Payments
                        SET ProviderReference = @ProviderReference
                        WHERE PaymentId = @PaymentId
                          AND ProviderReference IS NULL;
                        """;

                    await connection.ExecuteAsync(
                        attachReferenceSql,
                        new { PaymentId = paymentId, ProviderReference = normalizedReference },
                        transaction);
                }

                transaction.Commit();
                return PaymentStatusUpdateResult.NoChange;
            }

            if (!AllowedTransitions.TryGetValue(payment.Status, out var allowed) ||
                !allowed.Contains(newStatus))
            {
                throw new InvalidOperationException(
                    $"Payment cannot transition from '{payment.Status}' to '{newStatus}'.");
            }

            if (string.Equals(newStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                EnsureOrderCanBeMarkedPaid(order.OrderStatus, order.OrderPaymentStatus);

                // Defense in depth: even if historical or manually inserted data exists,
                // only one payment for this order may become successful.
                const string succeededPaymentSql = """
                    SELECT TOP (1) PaymentId
                    FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
                    WHERE OrderId = @OrderId
                      AND Status = 'Succeeded'
                      AND PaymentId <> @PaymentId;
                    """;

                var succeededPaymentId = await connection.QuerySingleOrDefaultAsync<long?>(
                    succeededPaymentSql,
                    new { OrderId = payment.OrderId, PaymentId = paymentId },
                    transaction);

                if (succeededPaymentId.HasValue)
                    throw new InvalidOperationException(
                        "Another payment has already succeeded for this order.");
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

            var affectedPayments = await connection.ExecuteAsync(
                updatePaymentSql,
                new
                {
                    PaymentId = paymentId,
                    NewStatus = newStatus,
                    ProviderReference = normalizedReference
                },
                transaction);

            if (affectedPayments != 1)
                throw new InvalidOperationException("Payment status could not be updated.");

            var targetOrderPaymentStatus = ResolveOrderPaymentStatus(
                newStatus, order.OrderPaymentStatus);

            if (!string.Equals(targetOrderPaymentStatus, order.OrderPaymentStatus,
                    StringComparison.OrdinalIgnoreCase))
            {
                const string updateOrderSql = """
                    UPDATE dbo.Orders
                    SET PaymentStatus = @PaymentStatus
                    WHERE OrderId = @OrderId;
                    """;

                var affectedOrders = await connection.ExecuteAsync(
                    updateOrderSql,
                    new
                    {
                        OrderId = order.OrderId,
                        PaymentStatus = targetOrderPaymentStatus
                    },
                    transaction);

                if (affectedOrders != 1)
                    throw new InvalidOperationException("Order payment status could not be updated.");
            }

            // Record audit log if this is an admin action or if a reason was provided (e.g. system expiration).
            if (adminUserId.HasValue || !string.IsNullOrWhiteSpace(reason))
            {
                const string insertAuditSql = """
                    INSERT INTO dbo.PaymentStatusAuditLogs
                        (PaymentId, AdminUserId, OldStatus, NewStatus,
                         TransactionReference, Reason, ChangedAt)
                    VALUES
                        (@PaymentId, @AdminUserId, @OldStatus, @NewStatus,
                         @TransactionReference, @Reason, SYSUTCDATETIME());
                    """;

                await connection.ExecuteAsync(
                    insertAuditSql,
                    new
                    {
                        PaymentId = paymentId,
                        AdminUserId = adminUserId,
                        OldStatus = payment.Status,
                        NewStatus = newStatus,
                        TransactionReference = normalizedReference,
                        Reason = reason ?? "Status update"
                    },
                    transaction);
            }

            transaction.Commit();
            return PaymentStatusUpdateResult.Updated;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            TryRollback(transaction);
            throw new InvalidOperationException(
                "A duplicate payment transaction reference or active payment was detected.", ex);
        }
        catch
        {
            TryRollback(transaction);
            throw;
        }
    }

    private static void EnsureOrderCanAcceptPayment(string orderStatus, string orderPaymentStatus)
    {
        if (string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot create a payment for a cancelled order.");

        if (string.Equals(orderPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(orderPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(orderPaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This order cannot accept another payment.");
        }
    }

    private static void EnsureOrderCanBeMarkedPaid(string orderStatus, string orderPaymentStatus)
    {
        if (string.Equals(orderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot mark a cancelled order as paid.");

        if (string.Equals(orderPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This order has already been paid by another payment.");

        if (string.Equals(orderPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(orderPaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot mark a refunded order as paid.");
        }
    }

    private static void ValidateStatusUpdateArguments(
        long paymentId, string newStatus, string? transactionReference)
    {
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        ArgumentException.ThrowIfNullOrWhiteSpace(newStatus);
        var normalizedStatus = NormalizeStatus(newStatus);

        if (!IsKnownStatus(normalizedStatus))
            throw new ArgumentException("Invalid payment status.", nameof(newStatus));

        var normalizedReference = NormalizeReference(transactionReference);
        if (normalizedReference?.Length > 200)
            throw new ArgumentException(
                "Transaction reference cannot exceed 200 characters.",
                nameof(transactionReference));
    }

    private static string? NormalizeReference(string? reference) =>
        string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();

    private static void TryRollback(IDbTransaction transaction)
    {
        try { transaction.Rollback(); }
        catch { /* Preserve the original exception. */ }
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

    private static string ResolveOrderPaymentStatus(
        string newPaymentStatus,
        string currentOrderPaymentStatus)
    {
        if (string.Equals(newPaymentStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
            return "Paid";

        if (string.Equals(newPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase))
            return "Refunded";

        if (string.Equals(newPaymentStatus, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            // A late failure must never downgrade an order already paid or refunded.
            if (string.Equals(currentOrderPaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentOrderPaymentStatus, "Refunded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(currentOrderPaymentStatus, "PartiallyRefunded", StringComparison.OrdinalIgnoreCase))
            {
                return currentOrderPaymentStatus;
            }

            return "Failed";
        }

        // Pending and Cancelled do not change the order's current payment status.
        return currentOrderPaymentStatus;
    }

    private sealed class PaymentStatusSnapshot
    {
        public long PaymentId { get; set; }
        public long OrderId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string? ProviderReference { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class OrderPaymentStatusSnapshot
    {
        public long OrderId { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string OrderPaymentStatus { get; set; } = string.Empty;
    }

    private sealed class OrderPaymentCreationSnapshot
    {
        public long OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}
