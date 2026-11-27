using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Enums;

namespace Ecommerce.Core.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(long paymentId);

    Task<IEnumerable<Payment>> GetByOrderIdAsync(long orderId);

    Task<Payment?> GetByTransactionReferenceAsync(string transactionReference);

    Task<IEnumerable<Payment>> GetAllAsync(string? status = null);

    Task<long> CreateAsync(Payment payment);

    Task<bool> UpdateStatusAsync(long paymentId, string newStatus, string? transactionReference = null);

    /// <summary>
    /// Admin-only: updates a payment status and atomically records an audit log entry.
    /// Reuses the same transition rules and locking as UpdateStatusAsync.
    /// </summary>
    Task<PaymentStatusUpdateResult> AdminUpdateStatusAsync(
        long paymentId,
        string newStatus,
        string? transactionReference,
        long adminUserId,
        string reason);

    /// <summary>
    /// System-initiated expiration of a pending payment. Atomically transitions to Cancelled and records an audit log.
    /// </summary>
    Task<PaymentStatusUpdateResult> ExpirePaymentAsync(long paymentId, string reason);
}
