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

    Task<IEnumerable<PaymentDto>> GetAllAsync(string? status = null);

    Task<InitiatePaymentResult> InitiatePaymentAsync(long orderId, long userId, CreatePaymentDto dto);

    Task<bool> HandleVerifiedCallbackAsync(
        long paymentId,
        string newStatus,
        string? transactionReference = null);

    /// <summary>
    /// Verifies provider signature and processes an inbound webhook notification.
    /// </summary>
    Task<bool> ProcessWebhookAsync(string payload, string signature);

    /// <summary>
    /// Admin-only: manually updates a payment status and records an audit log entry atomically.
    /// AdminUserId must come from the authenticated JWT claim, not from the request body.
    /// </summary>
    Task<PaymentStatusUpdateResult> AdminUpdateStatusAsync(
        long paymentId,
        long adminUserId,
        AdminUpdatePaymentStatusDto dto);

    /// <summary>
    /// Admin-only: initiates a full refund via the provider, updates status to Refunded, and writes an audit log.
    /// </summary>
    Task<PaymentStatusUpdateResult> RefundAsync(
        long paymentId,
        long adminUserId,
        string reason);

    /// <summary>
    /// Admin-only: retrieves the audit history for a specific payment.
    /// </summary>
    Task<IEnumerable<PaymentStatusAuditLogDto>> GetAuditLogsAsync(long paymentId);
}
