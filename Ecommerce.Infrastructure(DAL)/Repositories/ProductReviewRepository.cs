
using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class ProductReviewRepository : IProductReviewRepository
{
    private readonly IDbConnectionFactory _connectionFactory;


    public ProductReviewRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ProductReview?> GetByIdAsync(long reviewId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE ReviewId = @ReviewId;";

        return await connection.QuerySingleOrDefaultAsync<ProductReview>(
            sql,
            new { ReviewId = reviewId });
    }

    public async Task<IEnumerable<ProductReview>> GetByProductIdAsync(long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE ProductId = @ProductId
            ORDER BY CreatedAt DESC;";

        return await connection.QueryAsync<ProductReview>(
            sql,
            new { ProductId = productId });
    }

    //"تقيماتي"
    public async Task<IEnumerable<ProductReview>> GetByUserIdAsync(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC;";

        return await connection.QueryAsync<ProductReview>(
            sql,
            new { UserId = userId });
    }

    /// <summary>
    /// Retrieves a user's review for a specific product.
    /// Returns null when no matching review exists.
    /// This method assumes that each user can review a product only once.
    /// </summary>
    public async Task<ProductReview?> GetByProductAndUserAsync(long productId,long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE ProductId = @ProductId
              AND UserId = @UserId;";

        return await connection.QuerySingleOrDefaultAsync<ProductReview>(
            sql,
            new
            {
                ProductId = productId,
                UserId = userId
            });
    }

    /// <summary>
    /// Calculates the average rating and total number of reviews
    /// for a product. Returns zero for both values when no reviews exist.
    /// </summary>
    public async Task<(double AverageRating, int TotalReviews)>GetRatingSummaryAsync(long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                COALESCE(AVG(CAST(Rating AS FLOAT)), 0.0) AS AverageRating,
                COUNT(*) AS TotalReviews
            FROM dbo.ProductReviews
            WHERE ProductId = @ProductId;";

        var result = await connection.QuerySingleAsync<RatingSummaryRow>(
            sql,
            new { ProductId = productId });

        return (result.AverageRating, result.TotalReviews);
    }

    /// <summary>
    /// Creates a new product review and returns its generated identifier.
    /// Validates the rating before inserting the record.
    /// </summary>
    public async Task<long> CreateAsync(ProductReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        ValidateRating(review.Rating);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.ProductId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.UserId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO dbo.ProductReviews
                (ProductId, UserId, Rating, Comment)
            VALUES
                (@ProductId, @UserId, @Rating, @Comment);

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

        return await connection.ExecuteScalarAsync<long>(sql, review);
    }

    /// <summary>
    /// Updates the rating and comment of an existing review.
    /// Only the review's owner can update it through this method.
    /// Returns false when no matching review is found.
    /// </summary>
    public async Task<bool> UpdateAsync(ProductReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        ValidateRating(review.Rating);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.ProductReviewId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.UserId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.ProductReviews
            SET Rating = @Rating,
                Comment = @Comment
            WHERE ReviewId = @ReviewId
              AND UserId = @UserId;";

        int rowsAffected = await connection.ExecuteAsync(sql, review);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Deletes a review if the requester owns it or has administrator
    /// privileges. The isAdmin value must come from trusted,
    /// server-side authorization rather than direct user input.
    /// Returns false when no matching review is found.
    /// </summary>
    public async Task<bool> DeleteAsync(long reviewId,long userId,bool isAdmin = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM dbo.ProductReviews
            WHERE ReviewId = @ReviewId
              AND (@IsAdmin = 1 OR UserId = @UserId);";

        int rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                ReviewId = reviewId,
                UserId = userId,
                IsAdmin = isAdmin
            });

        return rowsAffected > 0;
    }

    private static void ValidateRating(int rating)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "Rating must be between 1 and 5.");
        }
    }

    /// <summary>
    /// Represents the database result returned by the rating summary query.
    /// </summary>
    private sealed class RatingSummaryRow
    {
        public double AverageRating { get; set; }

        public int TotalReviews { get; set; }
    }
}
