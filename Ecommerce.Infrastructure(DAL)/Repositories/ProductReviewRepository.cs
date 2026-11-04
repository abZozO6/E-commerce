using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

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

        const string sql = """
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE ReviewId = @ReviewId;
            """;

        return await connection.QuerySingleOrDefaultAsync<ProductReview>(
            sql, new { ReviewId = reviewId });
    }

    public async Task<IEnumerable<ProductReview>> GetByProductIdAsync(long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE ProductId = @ProductId
            ORDER BY CreatedAt DESC, ReviewId DESC;
            """;

        return await connection.QueryAsync<ProductReview>(
            sql, new { ProductId = productId });
    }

    public async Task<IEnumerable<ProductReview>> GetByUserIdAsync(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC, ReviewId DESC;
            """;

        return await connection.QueryAsync<ProductReview>(
            sql, new { UserId = userId });
    }

    public async Task<ProductReview?> GetByProductAndUserAsync(long productId,long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT ReviewId, ProductId, UserId, Rating, Comment, CreatedAt
            FROM dbo.ProductReviews
            WHERE ProductId = @ProductId
              AND UserId = @UserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<ProductReview>(
            sql,
            new { ProductId = productId, UserId = userId });
    }

    public async Task<(double AverageRating, int TotalReviews)> GetRatingSummaryAsync(long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT
                COALESCE(AVG(CAST(Rating AS FLOAT)), 0.0) AS AverageRating,
                COUNT(*) AS TotalReviews
            FROM dbo.ProductReviews
            WHERE ProductId = @ProductId;
            """;

        var result = await connection.QuerySingleAsync<RatingSummaryRow>(
            sql, new { ProductId = productId });

        return (result.AverageRating, result.TotalReviews);
    }

    public async Task<long> CreateAsync(ProductReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        ValidateReview(review);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.ProductReviews
                (ProductId, UserId, Rating, Comment, CreatedAt)
            VALUES
                (@ProductId, @UserId, @Rating, @Comment, SYSUTCDATETIME());

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, review);
    }

    public async Task<bool> UpdateAsync(ProductReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        ValidateReview(review);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.ReviewId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.ProductReviews
            SET Rating = @Rating,
                Comment = @Comment
            WHERE ReviewId = @ReviewId
              AND ProductId = @ProductId
              AND UserId = @UserId;
            """;

        int rowsAffected = await connection.ExecuteAsync(sql, review);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync( long reviewId,long userId,bool isAdmin = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reviewId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            DELETE FROM dbo.ProductReviews
            WHERE ReviewId = @ReviewId
              AND (@IsAdmin = 1 OR UserId = @UserId);
            """;

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

    private static void ValidateReview(ProductReview review)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.ProductId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(review.UserId);

        if (review.Rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(review.Rating),
                "Rating must be between 1 and 5.");
        }

        if (review.Comment?.Length > 1000)
        {
            throw new ArgumentException(
                "Comment cannot exceed 1000 characters.",
                nameof(review.Comment));
        }

        review.Comment = string.IsNullOrWhiteSpace(review.Comment)
            ? null
            : review.Comment.Trim();
    }

    private sealed class RatingSummaryRow
    {
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }
}