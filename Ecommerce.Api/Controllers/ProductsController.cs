using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    // GET: api/Products?categoryId=1&brandId=2&searchTerm=phone&minPrice=100&maxPrice=500
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetActiveFiltered(
        [FromQuery] long? categoryId = null,
        [FromQuery] long? brandId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null)
    {
        var products = await _productService.GetActiveFilteredAsync(
            categoryId,
            brandId,
            searchTerm,
            minPrice,
            maxPrice);

        return Ok(products);
    }

    // GET: api/Products/all (Admin view: includes inactive products)
    [Authorize(Roles = "Admin")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
    {
        var products = await _productService.GetAllAsync();
        return Ok(products);
    }

    // GET: api/Products/5
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ProductDto>> GetById(long id)
    {
        var product = await _productService.GetByIdAsync(id);

        if (product is null)
            return NotFound();

        return Ok(product);
    }

    // GET: api/Products/slug/iphone-15
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<ProductDto>> GetBySlug(string slug)
    {
        var product = await _productService.GetBySlugAsync(slug);

        if (product is null)
            return NotFound();

        return Ok(product);
    }

    // GET: api/Products/5/details
    [HttpGet("{id:long}/details")]
    public async Task<ActionResult<ProductDetailsDto>> GetDetailsById(long id)
    {
        var details = await _productService.GetDetailsByIdAsync(id);

        if (details is null)
            return NotFound();

        return Ok(details);
    }

    // POST: api/Products
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(CreateProductDto dto)
    {
        var product = await _productService.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.ProductId },
            product);
    }

    // PUT: api/Products/5
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateProductDto dto)
    {
        bool updated = await _productService.UpdateAsync(id, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // PATCH: api/Products/5/stock
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:long}/stock")]
    [HttpPut("{id:long}/stock")]
    public async Task<IActionResult> AdjustStock(long id, AdjustStockDto dto)
    {
        bool updated = await _productService.AdjustStockAsync(id, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // PATCH: api/Products/5/status
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:long}/status")]
    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> SetActiveStatus(long id, [FromBody] bool isActive)
    {
        bool updated = await _productService.SetActiveStatusAsync(id, isActive);

        if (!updated)
            return NotFound();

        return NoContent();
    }
}
