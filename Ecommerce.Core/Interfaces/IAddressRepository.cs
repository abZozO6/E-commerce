using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces
{
    public interface IAddressRepository
    {
        Task<Address?> GetByIdAsync(long addressId);

        Task<IEnumerable<Address>> GetByUserIdAsync(long userId);

        Task<long> CreateAsync(Address address);

        Task<bool> UpdateAsync(Address address);

        Task<bool> DeleteAsync(long addressId, long userId);

        Task<bool> SetDefaultAddressAsync(long addressId, long userId);
    }
}