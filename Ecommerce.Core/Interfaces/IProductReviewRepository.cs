using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IProductReviewRepository
{
    Task<ProductReview?> GetByIdAsync(long reviewId);

    Task<IEnumerable<ProductReview>> GetByProductIdAsync(long productId);

    Task<IEnumerable<ProductReview>> GetByUserIdAsync(long userId);

    Task<ProductReview?> GetByProductAndUserAsync(long productId, long userId);

    Task<(double AverageRating, int TotalReviews)> GetRatingSummaryAsync(long productId);

    Task<long> CreateAsync(ProductReview review);

    Task<bool> UpdateAsync(ProductReview review);

    Task<bool> DeleteAsync(long reviewId, long userId, bool isAdmin = false);
}
