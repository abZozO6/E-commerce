using Ecommerce.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Interfaces
{
    public interface IBrandRepository
    {
        Task<IEnumerable<Brand>> GetAllActiveAsync();

        Task<IEnumerable<Brand>> GetAllAsync();

        Task<Brand?> GetByIdAsync(long brandId);

        Task<Brand?> GetBySlugAsync(string slug);

        Task<bool> ExistsByNameOrSlugAsync(string name, string slug, long? excludeBrandId = null);

        Task<long> CreateAsync(Brand brand);

        Task<bool> UpdateAsync(Brand brand);

        Task<bool> DeleteAsync(long brandId);
    }
}
