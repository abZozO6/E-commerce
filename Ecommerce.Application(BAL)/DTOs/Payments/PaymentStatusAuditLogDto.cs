using System;

namespace Ecommerce.Application_BAL_.DTOs.Payments
{
    public class PaymentStatusAuditLogDto
    {
        public long AuditLogId { get; set; }
        public long PaymentId { get; set; }
        public long? AdminUserId { get; set; }
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string? TransactionReference { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }
}
