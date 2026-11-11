using Ecommerce.Application_BAL_.DTOs.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.Interfaces
{
    public interface IInventoryTransactionService
    {
        Task<InventoryTransactionDto?> GetByIdAsync(long transactionId);

        Task<IEnumerable<InventoryTransactionDto>> GetByProductIdAsync(long productId);

        Task<IEnumerable<InventoryTransactionDto>> GetByOrderIdAsync(long orderId);
    }
}
