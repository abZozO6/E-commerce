using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

/// <summary>
/// Read-only repository for order status audit records.
/// Writes are performed inline inside OrderRepository transactions.
/// </summary>
public interface IOrderStatusAuditRepository
{
    /// <summary>
    /// Returns all audit log entries for the specified order, newest first.
    /// </summary>
    Task<IEnumerable<OrderStatusAuditLog>> GetByOrderIdAsync(long orderId);
}
