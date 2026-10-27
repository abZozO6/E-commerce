using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetByIdAsync(long cartId);

    Task<Cart?> GetByUserIdAsync(long userId);

    Task<Cart> GetOrCreateByUserIdAsync(long userId);

    Task<bool> TouchUpdatedAtAsync(long cartId);

    Task<bool> DeleteByUserIdAsync(long userId);
}
