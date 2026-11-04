
using Ecommerce.Application_BAL_.DTOs.Categories;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    // GET: api/Categories
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll()
    {
        var categories = await _categoryService.GetAllAsync();

        return Ok(categories);
    }

    // GET: api/Categories/active
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAllActive()
    {
        var categories = await _categoryService.GetAllActiveAsync();

        return Ok(categories);
    }

    // GET: api/Categories/root
    [HttpGet("root")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetRootCategories()
    {
        var categories = await _categoryService.GetRootCategoriesAsync();

        return Ok(categories);
    }

    // GET: api/Categories/1/subcategories
    [HttpGet("{parentCategoryId:long}/subcategories")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetSubCategories(
        long parentCategoryId)
    {
        var categories =
            await _categoryService.GetSubCategoriesAsync(parentCategoryId);

        return Ok(categories);
    }

    // GET: api/Categories/5
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CategoryDto>> GetById(long id)
    {
        var category = await _categoryService.GetByIdAsync(id);

        if (category is null)
            return NotFound();

        return Ok(category);
    }

    // GET: api/Categories/slug/gaming-screens
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<CategoryDto>> GetBySlug(string slug)
    {
        var category = await _categoryService.GetBySlugAsync(slug);

        if (category is null)
            return NotFound();

        return Ok(category);
    }

    // POST: api/Categories
    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(
        CreateCategoryDto dto)
    {
        var category = await _categoryService.CreateAsync(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = category.CategoryId },
            category);
    }

    // PUT: api/Categories/5
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateCategoryDto dto)
    {
        bool updated = await _categoryService.UpdateAsync(id, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/Categories/5
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        bool deleted = await _categoryService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
