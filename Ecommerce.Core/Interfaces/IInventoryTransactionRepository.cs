using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IInventoryTransactionRepository
{
    Task<InventoryTransaction?> GetByIdAsync(long transactionId);

    Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(long productId);

    Task<IEnumerable<InventoryTransaction>> GetByOrderIdAsync(long orderId);

    Task<long> CreateAsync(InventoryTransaction transaction);
}
