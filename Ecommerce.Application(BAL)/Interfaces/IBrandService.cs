using Ecommerce.Application_BAL_.DTOs.Brands;

namespace Ecommerce.Application_BAL_.Interfaces;

/// <summary>
/// Defines business operations for managing brands.
/// </summary>
public interface IBrandService
{

    Task<IEnumerable<BrandDto>> GetAllActiveAsync();

    Task<IEnumerable<BrandDto>> GetAllAsync();

    Task<BrandDto?> GetByIdAsync(long brandId);

    Task<BrandDto?> GetBySlugAsync(string slug);

    Task<BrandDto> CreateAsync(CreateBrandDto dto);

    Task<bool> UpdateAsync(long brandId, UpdateBrandDto dto);

    Task<bool> DeleteAsync(long brandId);
}