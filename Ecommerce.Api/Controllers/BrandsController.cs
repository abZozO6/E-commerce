
using Ecommerce.Application_BAL_.DTOs.Brands;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BrandsController : ControllerBase
{
    private readonly IBrandService _brandService;

    public BrandsController(IBrandService brandService)
    {
        _brandService = brandService;
    }

    // GET: api/Brands
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BrandDto>>> GetAll()
    {
        var brands = await _brandService.GetAllAsync();
        return Ok(brands);
    }

    // GET: api/Brands/active
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<BrandDto>>> GetAllActive()
    {
        var brands = await _brandService.GetAllActiveAsync();
        return Ok(brands);
    }

    // GET: api/Brands/5
    [HttpGet("{id:long}")]
    public async Task<ActionResult<BrandDto>> GetById(long id)
    {
        var brand = await _brandService.GetByIdAsync(id);

        if (brand is null)
            return NotFound();

        return Ok(brand);
    }

    // GET: api/Brands/slug/nike
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<BrandDto>> GetBySlug(string slug)
    {
        var brand = await _brandService.GetBySlugAsync(slug);

        if (brand is null)
            return NotFound();

        return Ok(brand);
    }

    // POST: api/Brands
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<BrandDto>> Create(CreateBrandDto dto)
    {
        var brand = await _brandService.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = brand.BrandId },
            brand);
    }

    // PUT: api/Brands/5
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateBrandDto dto)
    {
        bool updated = await _brandService.UpdateAsync(id, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/Brands/5
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        bool deleted = await _brandService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
