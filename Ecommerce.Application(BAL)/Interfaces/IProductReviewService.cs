using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ecommerce.Application_BAL_.DTOs.Products;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface IProductReviewService
{
    Task<ProductReviewDto?> GetByIdAsync(long reviewId);

    Task<IEnumerable<ProductReviewDto>> GetByProductIdAsync(long productId);

    Task<IEnumerable<ProductReviewDto>> GetByUserIdAsync(long userId);

    Task<ProductRatingSummaryDto> GetRatingSummaryAsync(long productId);

    Task<ProductReviewDto> CreateAsync(long productId,long userId,CreateProductReviewDto dto);

    Task<bool> UpdateAsync(long productId,long reviewId,long userId, UpdateProductReviewDto dto);

    Task<bool> DeleteAsync(long productId,long reviewId,long userId,bool isAdmin = false);
}