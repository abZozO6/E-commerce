using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/products/{productId:long}/reviews")]
public class ProductReviewsController : ControllerBase
{
    private readonly IProductReviewService _reviewService;

    public ProductReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    // GET: api/products/5/reviews
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductReviewDto>>> GetByProductId(long productId)
    {
        var reviews = await _reviewService.GetByProductIdAsync(productId);
        return Ok(reviews);
    }

    // GET: api/products/5/reviews/details
    [HttpGet("details")]
    public async Task<ActionResult<IEnumerable<ProductReviewDetailsDto>>> GetDetailsByProductId(long productId)
    {
        var details = await _reviewService.GetDetailsByProductIdAsync(productId);
        return Ok(details);
    }

    // GET: api/products/5/reviews/summary
    [HttpGet("summary")]
    public async Task<ActionResult<ProductRatingSummaryDto>> GetRatingSummary(long productId)
    {
        var summary = await _reviewService.GetRatingSummaryAsync(productId);
        return Ok(summary);
    }

    // GET: api/products/5/reviews/10
    [HttpGet("{reviewId:long}")]
    public async Task<ActionResult<ProductReviewDto>> GetById(long productId, long reviewId)
    {
        var review = await _reviewService.GetByIdAsync(reviewId);

        if (review is null || review.ProductId != productId)
            return NotFound();

        return Ok(review);
    }

    // POST: api/products/5/reviews
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<ProductReviewDto>> Create(long productId, CreateProductReviewDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var review = await _reviewService.CreateAsync(productId, userId, dto);

        return CreatedAtAction(
            nameof(GetById),
            new { productId, reviewId = review.ReviewId },
            review);
    }

    // PUT: api/products/5/reviews/10
    [Authorize]
    [HttpPut("{reviewId:long}")]
    public async Task<IActionResult> Update(long productId, long reviewId, UpdateProductReviewDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool updated = await _reviewService.UpdateAsync(productId, reviewId, userId, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/products/5/reviews/10
    [Authorize]
    [HttpDelete("{reviewId:long}")]
    public async Task<IActionResult> Delete(long productId, long reviewId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool isAdmin = User.IsInRole("Admin");
        bool deleted = await _reviewService.DeleteAsync(productId, reviewId, userId, isAdmin);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    // GET: api/reviews/my
    [Authorize]
    [HttpGet("/api/reviews/my")]
    public async Task<ActionResult<IEnumerable<ProductReviewDto>>> GetMyReviews()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var reviews = await _reviewService.GetByUserIdAsync(userId);
        return Ok(reviews);
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
