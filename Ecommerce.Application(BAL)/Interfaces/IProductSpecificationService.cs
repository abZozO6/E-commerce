using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ecommerce.Application_BAL_.DTOs.Products;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface IProductSpecificationService
{
    Task<ProductSpecificationDto?> GetByIdAsync(long specId);

    Task<IEnumerable<ProductSpecificationDto>> GetByProductIdAsync(long productId);

    Task<ProductSpecificationDto> UpsertAsync(long productId,UpsertProductSpecificationDto dto);

    Task<bool> ReplaceProductSpecificationsAsync(long productId,IEnumerable<UpsertProductSpecificationDto> specifications);

    Task<bool> UpdateAsync(long productId,long specId,UpsertProductSpecificationDto dto);

    Task<bool> DeleteAsync(long productId, long specId);
}
