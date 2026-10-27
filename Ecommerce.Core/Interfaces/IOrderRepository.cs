using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(long orderId);

    Task<IEnumerable<Order>> GetByUserIdAsync(long userId);

    Task<IEnumerable<Order>> GetAllAsync(string? status = null);

    Task<long> CreateAsync(Order order);

    Task<long> CreateOrderWithTransactionAsync(Order order,IEnumerable<OrderItem> items, IDictionary<long, byte[]> productRowVersions,long? cartIdToClear = null);

    Task<bool> UpdateStatusAsync(long orderId, string newStatus);

    Task<bool> CancelOrderWithTransactionAsync(long orderId, long userId, bool isAdmin = false);
}
