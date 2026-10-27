using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IProductSpecificationRepository
{
    Task<ProductSpecification?> GetByIdAsync(long specId);

    Task<IEnumerable<ProductSpecification>> GetByProductIdAsync(long productId);

    Task<long> UpsertAsync(ProductSpecification spec);

    Task<bool> ReplaceProductSpecificationsAsync(long productId, IEnumerable<ProductSpecification> specifications);

    Task<bool> UpdateAsync(ProductSpecification spec);

    Task<bool> DeleteAsync(long specId, long productId);

}
