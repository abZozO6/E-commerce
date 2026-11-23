using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ecommerce.Application_BAL_.DTOs.Payments;
using Ecommerce.Core.Enums;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface IPaymentService
{
    Task<PaymentDto?> GetMyPaymentByIdAsync(long paymentId, long userId);

    Task<IEnumerable<PaymentDto>> GetMyPaymentsByOrderIdAsync(long orderId, long userId);

    Task<long> InitiatePaymentAsync(long orderId, long userId, CreatePaymentDto dto);

    Task<bool> HandleVerifiedCallbackAsync(
        long paymentId,
        string newStatus,
        string? transactionReference = null);

    /// <summary>
    /// Admin-only: manually updates a payment status and records an audit log entry atomically.
    /// AdminUserId must come from the authenticated JWT claim, not from the request body.
    /// </summary>
    Task<PaymentStatusUpdateResult> AdminUpdateStatusAsync(
        long paymentId,
        long adminUserId,
        AdminUpdatePaymentStatusDto dto);

    /// <summary>
    /// Admin-only: retrieves the audit history for a specific payment.
    /// </summary>
    Task<IEnumerable<PaymentStatusAuditLogDto>> GetAuditLogsAsync(long paymentId);
}
