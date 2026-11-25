namespace Ecommerce.Core.Entities;

public class OrderStatusAuditLog
{
    public long AuditLogId { get; set; }
    public long OrderId { get; set; }
    public long AdminUserId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
