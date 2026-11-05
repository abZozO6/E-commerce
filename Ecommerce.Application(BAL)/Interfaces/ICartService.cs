using Ecommerce.Application_BAL_.DTOs.Carts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.Interfaces
{
    public interface ICartService
    {
        Task<CartDto?> GetByUserIdAsync(long userId);

        Task<CartDto> GetOrCreateAsync(long userId);

        Task<bool> TouchUpdatedAtAsync(long userId);

        Task<bool> DeleteAsync(long userId);
    }
}
