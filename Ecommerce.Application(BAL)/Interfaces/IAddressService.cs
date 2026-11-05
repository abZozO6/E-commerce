using Ecommerce.Application_BAL_.DTOs.Addresses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.Interfaces
{
    public interface IAddressService
    {
        Task<AddressDto?> GetByIdAsync(long addressId, long userId);

        Task<IEnumerable<AddressDto>> GetByUserIdAsync(long userId);

        Task<AddressDto> CreateAsync(long userId, CreateAddressDto dto);

        Task<bool> UpdateAsync(long addressId,long userId,UpdateAddressDto dto);

        Task<bool> DeleteAsync(long addressId, long userId);

        Task<bool> SetDefaultAddressAsync(long addressId, long userId);
    }
}
