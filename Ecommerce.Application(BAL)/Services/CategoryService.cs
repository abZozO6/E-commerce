
using Ecommerce.Application_BAL_.DTOs.Categories;
using Ecommerce.Application_BAL_.Helpers;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<IEnumerable<CategoryDto>> GetAllActiveAsync()
    {
        var categories = await _categoryRepository.GetAllActiveAsync();

        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<CategoryDto>> GetAllAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();

        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<CategoryDto>> GetRootCategoriesAsync()
    {
        var categories = await _categoryRepository.GetRootCategoriesAsync();

        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<CategoryDto>> GetSubCategoriesAsync(long parentCategoryId)
    {
        if (parentCategoryId <= 0)
            throw new ArgumentException("Invalid parent category ID.");

        var parent = await _categoryRepository.GetByIdAsync(parentCategoryId);

        if (parent is null)
            throw new KeyNotFoundException("Parent category was not found.");

        var categories =
            await _categoryRepository.GetSubCategoriesAsync(parentCategoryId);

        return categories.Select(MapToDto);
    }

    public async Task<CategoryDto?> GetByIdAsync(long categoryId)
    {
        if (categoryId <= 0)
            throw new ArgumentException("Invalid category ID.");

        var category = await _categoryRepository.GetByIdAsync(categoryId);

        return category is null ? null : MapToDto(category);
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.");

        var category =await _categoryRepository.GetBySlugAsync(slug.Trim());

        return category is null ? null : MapToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        string name = ValidateAndNormalizeName(dto.Name);
        string slug = SlugGenerator.Generate(name);

        await ValidateParentCategoryAsync(dto.ParentCategoryId);

        bool slugExists = await _categoryRepository.ExistsBySlugAsync(slug);

        if (slugExists)
        {
            throw new InvalidOperationException(
                "A category with the same slug already exists.");
        }

        var category = new Category
        {
            Name = name,
            Slug = slug,
            ParentCategoryId = dto.ParentCategoryId,
            IsActive = true,
            IsDeleted = false
        };

        long categoryId =
            await _categoryRepository.CreateAsync(category);

        var createdCategory =
            await _categoryRepository.GetByIdAsync(categoryId);

        if (createdCategory is null)
        {
            throw new InvalidOperationException(
                "Category was created but could not be retrieved.");
        }

        return MapToDto(createdCategory);
    }

    public async Task<bool> UpdateAsync(long categoryId,UpdateCategoryDto dto)
    {
        if (categoryId <= 0)
            throw new ArgumentException("Invalid category ID.");

        ArgumentNullException.ThrowIfNull(dto);

        string name = ValidateAndNormalizeName(dto.Name);

        var existingCategory = await _categoryRepository.GetByIdAsync(categoryId);

        if (existingCategory is null)
            return false;

        await ValidateParentCategoryAsync( dto.ParentCategoryId,categoryId);

        var allCategories = await _categoryRepository.GetAllAsync();

        if (WouldCreateCircularReference(categoryId,dto.ParentCategoryId,allCategories))
        {
            throw new InvalidOperationException("A category cannot be moved under one of its descendants.");
        }

        // Preserve the existing slug to avoid breaking URLs.
        string slug = existingCategory.Slug;

        existingCategory.Name = name;
        existingCategory.ParentCategoryId = dto.ParentCategoryId;
        existingCategory.IsActive = dto.IsActive;

        // Slug remains unchanged.
        existingCategory.Slug = slug;

        return await _categoryRepository.UpdateAsync(existingCategory);
    }

    public async Task<bool> DeleteAsync(long categoryId)
    {
        if (categoryId <= 0)
            throw new ArgumentException("Invalid category ID.");

        var category =await _categoryRepository.GetByIdAsync(categoryId);

        if (category is null)
            return false;

        var allCategories = await _categoryRepository.GetAllAsync();

        bool hasSubCategories = allCategories.Any(c => c.ParentCategoryId == categoryId);

        if (hasSubCategories)
        {
            throw new InvalidOperationException(
                "Cannot delete a category that has subcategories. " +
                "Delete or move its subcategories first.");
        }

        return await _categoryRepository.DeleteAsync(categoryId);
    }

    private async Task ValidateParentCategoryAsync(long? parentCategoryId,long? currentCategoryId = null)
    {
        if (parentCategoryId is null)
            return;

        if (parentCategoryId <= 0)
            throw new ArgumentException("Invalid parent category ID.");

        if (currentCategoryId == parentCategoryId)
        {
            throw new InvalidOperationException(
                "Category cannot be its own parent.");
        }

        var parent =
            await _categoryRepository.GetByIdAsync(parentCategoryId.Value);

        if (parent is null)
        {
            throw new KeyNotFoundException(
                "Parent category was not found.");
        }

        if (!parent.IsActive)
        {
            throw new InvalidOperationException(
                "Cannot use an inactive category as a parent.");
        }
    }

    // for bad senrio to avoid  CircularReference
    private static bool WouldCreateCircularReference(
     long categoryId,
     long? newParentCategoryId,
     IEnumerable<Category> allCategories)
    {
        if (newParentCategoryId is null)
            return false;

        var categories = allCategories.ToDictionary(
            c => c.CategoryId);

        var visited = new HashSet<long>();

        long currentParentId = newParentCategoryId.Value;

        while (true)
        {
            if (currentParentId == categoryId)
                return true;

            // Detect an existing circular reference.
            if (!visited.Add(currentParentId))
            {
                throw new InvalidOperationException(
                    "Circular reference detected in existing categories.");
            }

            if (!categories.TryGetValue(currentParentId, out var parent))
                return false;

            if (parent.ParentCategoryId is null)
                return false;

            currentParentId = parent.ParentCategoryId.Value;
        }
    }

    private static string ValidateAndNormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.");

        name = name.Trim();

        if (name.Length > 200)
        {
            throw new ArgumentException(
                "Category name cannot exceed 200 characters.");
        }

        return name;
    }

    private static CategoryDto MapToDto(Category category)
    {
        return new CategoryDto
        {
            CategoryId = category.CategoryId,
            Name = category.Name,
            Slug = category.Slug,
            ParentCategoryId = category.ParentCategoryId,
            IsActive = category.IsActive
        };
    }
}
