using Ecommerce.Application_BAL_.DTOs.Orders;
using Ecommerce.Core.Enums;

namespace Ecommerce.Application_BAL_.Interfaces
{
    public interface IOrderService
    {
        // Customer
        Task<IEnumerable<OrderDto>> GetMyOrdersAsync(long userId);

        Task<OrderDto?> GetMyOrderByIdAsync(long orderId, long userId);

        Task<long> CreateAsync(long userId, CreateOrderDto dto);

        Task<bool> CancelMyOrderAsync(long orderId, long userId);


        // Admin
        Task<IEnumerable<OrderDto>> GetAllOrdersAsync(string? status = null);

        Task<OrderDto?> GetOrderByIdAsync(long orderId);

        Task<bool> UpdateStatusAsync(long orderId, UpdateOrderStatusDto dto);

        Task<bool> CancelOrderAsync(long orderId);

        /// <summary>
        /// Admin-only: validate the transition, update the order status,
        /// and record an audit log entry atomically.
        /// </summary>
        Task<OrderStatusUpdateResult> AdminUpdateStatusAsync(
            long orderId,
            long adminUserId,
            AdminUpdateOrderStatusDto dto);

        /// <summary>
        /// Returns all order status audit log entries for the specified order, newest first.
        /// </summary>
        Task<IEnumerable<OrderStatusAuditLogDto>> GetAuditLogsAsync(long orderId);
    }
}
