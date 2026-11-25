using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Enums;

namespace Ecommerce.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(long orderId);

    Task<IEnumerable<Order>> GetByUserIdAsync(long userId);

    Task<IEnumerable<Order>> GetAllAsync(string? status = null);

    Task<long> CreateAsync(Order order);

    Task<long> CreateOrderWithTransactionAsync(Order order, IEnumerable<OrderItem> items, IDictionary<long, byte[]> productRowVersions, long? cartIdToClear = null);

    Task<bool> UpdateStatusAsync(long orderId, string newStatus);

    Task<bool> CancelOrderWithTransactionAsync(long orderId, long userId, bool isAdmin = false);

    /// <summary>
    /// Admin-only: validates the transition, updates order status, and inserts an audit log
    /// record — all in a single atomic transaction.
    /// Returns an <see cref="OrderStatusUpdateResult"/> describing the outcome;
    /// never throws for expected business rejections.
    /// </summary>
    Task<OrderStatusUpdateResult> AdminUpdateStatusAsync(
        long orderId,
        string newStatus,
        string reason,
        long adminUserId);
}
