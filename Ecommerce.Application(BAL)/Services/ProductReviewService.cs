using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class ProductReviewService : IProductReviewService
{
    private readonly IProductReviewRepository _reviewRepository;

    private readonly IProductRepository _productRepository;

    public ProductReviewService(IProductReviewRepository reviewRepository, IProductRepository productRepository)
    {
        _reviewRepository = reviewRepository
            ?? throw new ArgumentNullException(nameof(reviewRepository));

        _productRepository = productRepository
            ?? throw new ArgumentNullException(nameof(productRepository));
    }

    public async Task<ProductReviewDto?> GetByIdAsync(long reviewId)
    {
        if (reviewId <= 0)
            throw new ArgumentOutOfRangeException(nameof(reviewId));

        var review = await _reviewRepository.GetByIdAsync(reviewId);

        return review is null ? null : MapToDto(review);
    }

    public async Task<IEnumerable<ProductReviewDto>> GetByProductIdAsync(long productId)
    {
        await EnsureProductExistsAsync(productId);

        var reviews = await _reviewRepository.GetByProductIdAsync(productId);

        return reviews.Select(MapToDto);
    }

    public async Task<IEnumerable<ProductReviewDto>> GetByUserIdAsync(long userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        var reviews = await _reviewRepository.GetByUserIdAsync(userId);

        return reviews.Select(MapToDto);
    }

    public async Task<ProductRatingSummaryDto> GetRatingSummaryAsync(long productId)
    {
        await EnsureProductExistsAsync(productId);

        var summary = await _reviewRepository
            .GetRatingSummaryAsync(productId);

        return new ProductRatingSummaryDto
        {
            ProductId = productId,
            AverageRating = Math.Round(summary.AverageRating, 2),
            TotalReviews = summary.TotalReviews
        };
    }

    public async Task<ProductReviewDto> CreateAsync(
        long productId,
        long userId,
        CreateProductReviewDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        await EnsureProductExistsAsync(productId);

        ValidateRating(dto.Rating);

        string? comment = NormalizeComment(dto.Comment);

        var existingReview = await _reviewRepository
            .GetByProductAndUserAsync(productId, userId);

        if (existingReview is not null)
        {
            throw new InvalidOperationException(
                "You have already reviewed this product.");
        }

        var review = new ProductReview
        {
            ProductId = productId,
            UserId = userId,
            Rating = dto.Rating,
            Comment = comment
        };

        long reviewId = await _reviewRepository.CreateAsync(review);

        var createdReview = await _reviewRepository.GetByIdAsync(reviewId);

        if (createdReview is null)
        {
            throw new InvalidOperationException(
                "The review was created but could not be retrieved.");
        }

        return MapToDto(createdReview);
    }

    public async Task<bool> UpdateAsync(
        long productId,
        long reviewId,
        long userId,
        UpdateProductReviewDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (reviewId <= 0)
            throw new ArgumentOutOfRangeException(nameof(reviewId));

        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        await EnsureProductExistsAsync(productId);

        ValidateRating(dto.Rating);

        var existingReview = await _reviewRepository.GetByIdAsync(reviewId);

        if (existingReview is null ||
            existingReview.ProductId != productId ||
            existingReview.UserId != userId)
        {
            return false;
        }

        existingReview.Rating = dto.Rating;
        existingReview.Comment = NormalizeComment(dto.Comment);

        return await _reviewRepository.UpdateAsync(existingReview);
    }

    public async Task<bool> DeleteAsync(
        long productId,
        long reviewId,
        long userId,
        bool isAdmin = false)
    {
        if (reviewId <= 0)
            throw new ArgumentOutOfRangeException(nameof(reviewId));

        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        await EnsureProductExistsAsync(productId);

        var existingReview = await _reviewRepository.GetByIdAsync(reviewId);

        if (existingReview is null || existingReview.ProductId != productId)
            return false;

        if (!isAdmin && existingReview.UserId != userId)
            return false;

        return await _reviewRepository.DeleteAsync(
            reviewId,
            userId,
            isAdmin);
    }

    private async Task EnsureProductExistsAsync(long productId)
    {
        if (productId <= 0)
            throw new ArgumentOutOfRangeException(nameof(productId));

        var product = await _productRepository.GetByIdAsync(productId);

        if (product is null)
        {
            throw new KeyNotFoundException(
                $"Product with ID {productId} was not found.");
        }
    }

    private static void ValidateRating(byte rating)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rating),
                "Rating must be between 1 and 5.");
        }
    }

    private static string? NormalizeComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;

        comment = comment.Trim();

        if (comment.Length > 1000)
        {
            throw new ArgumentException(
                "Comment cannot exceed 1000 characters.",
                nameof(comment));
        }

        return comment;
    }

    private static ProductReviewDto MapToDto(ProductReview review)
    {
        return new ProductReviewDto
        {
            ReviewId = review.ReviewId,
            ProductId = review.ProductId,
            UserId = review.UserId,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt
        };
    }
}