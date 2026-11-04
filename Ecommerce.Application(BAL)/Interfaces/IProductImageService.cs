using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ecommerce.Application_BAL_.DTOs.Products;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface IProductImageService
{
    Task<ProductImageDto?> GetByIdAsync(long imageId);

    Task<IEnumerable<ProductImageDto>> GetByProductIdAsync(long productId);

    Task<ProductImageDto?> GetMainImageByProductIdAsync(long productId);

    Task<ProductImageDto> CreateAsync(long productId,CreateProductImageDto dto);

    Task<bool> UpdateAsync(long productId,long imageId,UpdateProductImageDto dto);

    Task<bool> SetMainImageAsync(long productId, long imageId);

    Task<bool> DeleteAsync(long productId, long imageId);
}
