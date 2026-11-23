using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/products/{productId:long}/images")]
public class ProductImagesController : ControllerBase
{
    private readonly IProductImageService _productImageService;

    public ProductImagesController(IProductImageService productImageService)
    {
        _productImageService = productImageService;
    }

    // GET: api/products/5/images
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductImageDto>>> GetByProductId(long productId)
    {
        var images = await _productImageService.GetByProductIdAsync(productId);
        return Ok(images);
    }

    // GET: api/products/5/images/main
    [HttpGet("main")]
    public async Task<ActionResult<ProductImageDto>> GetMainImage(long productId)
    {
        var image = await _productImageService.GetMainImageByProductIdAsync(productId);

        if (image is null)
            return NotFound();

        return Ok(image);
    }

    // GET: api/products/5/images/10
    [HttpGet("{imageId:long}")]
    public async Task<ActionResult<ProductImageDto>> GetById(long productId, long imageId)
    {
        var image = await _productImageService.GetByIdAsync(imageId);

        if (image is null || image.ProductId != productId)
            return NotFound();

        return Ok(image);
    }

    // POST: api/products/5/images
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductImageDto>> Create(long productId, CreateProductImageDto dto)
    {
        var image = await _productImageService.CreateAsync(productId, dto);

        return CreatedAtAction(
            nameof(GetById),
            new { productId, imageId = image.ProductImageId },
            image);
    }

    // PUT: api/products/5/images/10
    [Authorize(Roles = "Admin")]
    [HttpPut("{imageId:long}")]
    public async Task<IActionResult> Update(long productId, long imageId, UpdateProductImageDto dto)
    {
        bool updated = await _productImageService.UpdateAsync(productId, imageId, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // PATCH: api/products/5/images/10/main
    [Authorize(Roles = "Admin")]
    [HttpPatch("{imageId:long}/main")]
    [HttpPut("{imageId:long}/main")]
    public async Task<IActionResult> SetMainImage(long productId, long imageId)
    {
        bool updated = await _productImageService.SetMainImageAsync(productId, imageId);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/products/5/images/10
    [Authorize(Roles = "Admin")]
    [HttpDelete("{imageId:long}")]
    public async Task<IActionResult> Delete(long productId, long imageId)
    {
        bool deleted = await _productImageService.DeleteAsync(productId, imageId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
