using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/products/{productId:long}/specifications")]
public class ProductSpecificationsController : ControllerBase
{
    private readonly IProductSpecificationService _specificationService;

    public ProductSpecificationsController(IProductSpecificationService specificationService)
    {
        _specificationService = specificationService;
    }

    // GET: api/products/5/specifications
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductSpecificationDto>>> GetByProductId(long productId)
    {
        var specs = await _specificationService.GetByProductIdAsync(productId);
        return Ok(specs);
    }

    // GET: api/products/5/specifications/10
    [HttpGet("{specId:long}")]
    public async Task<ActionResult<ProductSpecificationDto>> GetById(long productId, long specId)
    {
        var spec = await _specificationService.GetByIdAsync(specId);

        if (spec is null || spec.ProductId != productId)
            return NotFound();

        return Ok(spec);
    }

    // POST: api/products/5/specifications
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductSpecificationDto>> Upsert(long productId, UpsertProductSpecificationDto dto)
    {
        var spec = await _specificationService.UpsertAsync(productId, dto);

        return CreatedAtAction(
            nameof(GetById),
            new { productId, specId = spec.SpecId },
            spec);
    }

    // PUT: api/products/5/specifications (Bulk Replace)
    [Authorize(Roles = "Admin")]
    [HttpPut]
    public async Task<IActionResult> ReplaceAll(long productId, IEnumerable<UpsertProductSpecificationDto> specifications)
    {
        bool replaced = await _specificationService.ReplaceProductSpecificationsAsync(productId, specifications);

        if (!replaced)
            return NotFound();

        return NoContent();
    }

    // PUT: api/products/5/specifications/10
    [Authorize(Roles = "Admin")]
    [HttpPut("{specId:long}")]
    public async Task<IActionResult> Update(long productId, long specId, UpsertProductSpecificationDto dto)
    {
        bool updated = await _specificationService.UpdateAsync(productId, specId, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/products/5/specifications/10
    [Authorize(Roles = "Admin")]
    [HttpDelete("{specId:long}")]
    public async Task<IActionResult> Delete(long productId, long specId)
    {
        bool deleted = await _specificationService.DeleteAsync(productId, specId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
