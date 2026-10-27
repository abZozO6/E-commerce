using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IOrderItemRepository
{
    Task<OrderItem?> GetByIdAsync(long orderItemId);

    Task<IEnumerable<OrderItem>> GetByOrderIdAsync(long orderId);

    Task<long> CreateAsync(OrderItem item);

    Task<bool> HasUserPurchasedProductAsync(long userId, long productId);
}
