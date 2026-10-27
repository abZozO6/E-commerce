using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IProductImageRepository
{
    Task<ProductImage?> GetByIdAsync(long imageId);

    Task<IEnumerable<ProductImage>> GetByProductIdAsync(long productId);

    Task<ProductImage?> GetMainImageByProductIdAsync(long productId);

    Task<long> CreateAsync(ProductImage image);

    Task<bool> SetMainImageAsync(long imageId, long productId);

    Task<bool> UpdateAsync(ProductImage image);

    Task<bool> DeleteAsync(long imageId, long productId);
}
