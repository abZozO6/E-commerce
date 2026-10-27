using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface ICartItemRepository
{
    Task<CartItem?> GetByIdAsync(long cartItemId);

    Task<CartItem?> GetByCartAndProductAsync(long cartId, long productId);

    Task<IEnumerable<CartItem>> GetByCartIdAsync(long cartId);

    Task<long> AddOrIncrementAsync(CartItem item);

    Task<bool> UpdateQuantityAsync(long cartItemId, long cartId, int newQuantity);

    Task<bool> DeleteAsync(long cartItemId, long cartId);

    Task<bool> ClearCartAsync(long cartId);
}
