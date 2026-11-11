using Ecommerce.Application_BAL_.DTOs.Brands;
using Ecommerce.Application_BAL_.Helpers;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class BrandService : IBrandService
{
    private readonly IBrandRepository _brandRepository;

    public BrandService(IBrandRepository brandRepository)
    {
        _brandRepository = brandRepository;
    }

    public async Task<IEnumerable<BrandDto>> GetAllActiveAsync()
    {
        var brands = await _brandRepository.GetAllActiveAsync();

        return brands.Select(MapToDto);
    }

    public async Task<IEnumerable<BrandDto>> GetAllAsync()
    {
        var brands = await _brandRepository.GetAllAsync();

        return brands.Select(MapToDto);
    }

    public async Task<BrandDto?> GetByIdAsync(long brandId)
    {
        if (brandId <= 0)
            throw new ArgumentException("Invalid brand ID.");

        var brand = await _brandRepository.GetByIdAsync(brandId);

        return brand is null ? null : MapToDto(brand);
    }

    public async Task<BrandDto?> GetBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.");

        var brand = await _brandRepository.GetBySlugAsync(slug.Trim());

        return brand is null ? null : MapToDto(brand);
    }

    public async Task<BrandDto> CreateAsync(CreateBrandDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        string name = ValidateAndNormalizeName(dto.Name);
        string slug = SlugGenerator.Generate(name);

        if (slug.Length > 120)
            throw new ArgumentException("Brand slug cannot exceed 120 characters.");

        string? logoUrl = ValidateAndNormalizeLogoUrl(dto.LogoUrl);

        bool alreadyExists =
            await _brandRepository.ExistsByNameOrSlugAsync(name, slug);

        if (alreadyExists)
            throw new InvalidOperationException(
                "A brand with the same name or slug already exists.");

        var brand = new Brand
        {
            Name = name,
            Slug = slug,
            LogoUrl = logoUrl,
            IsActive = true,
            IsDeleted = false
        };

        long brandId = await _brandRepository.CreateAsync(brand);

        var createdBrand = await _brandRepository.GetByIdAsync(brandId);

        if (createdBrand is null)
            throw new InvalidOperationException(
                "Brand was created but could not be retrieved.");

        return MapToDto(createdBrand);
    }

    public async Task<bool> UpdateAsync(long brandId, UpdateBrandDto dto)
    {
        if (brandId <= 0)
            throw new ArgumentException("Invalid brand ID.");

        ArgumentNullException.ThrowIfNull(dto);

        string name = ValidateAndNormalizeName(dto.Name);
        string? logoUrl = ValidateAndNormalizeLogoUrl(dto.LogoUrl);

        var existingBrand = await _brandRepository.GetByIdAsync(brandId);

        if (existingBrand is null)
            return false;

        // Preserve the existing slug to avoid breaking indexed URLs.
        string slug = existingBrand.Slug;

        bool alreadyExists =
            await _brandRepository.ExistsByNameOrSlugAsync(
                name,
                slug,
                brandId);

        if (alreadyExists)
            throw new InvalidOperationException(
                "Another brand with the same name or slug already exists.");

        existingBrand.Name = name;
        existingBrand.LogoUrl = logoUrl;
        existingBrand.IsActive = dto.IsActive;

        return await _brandRepository.UpdateAsync(existingBrand);
    }

    public async Task<bool> DeleteAsync(long brandId)
    {
        if (brandId <= 0)
            throw new ArgumentException("Invalid brand ID.");

        return await _brandRepository.DeleteAsync(brandId);
    }

    private static string ValidateAndNormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Brand name is required.");

        name = name.Trim();

        if (name.Length > 100)
            throw new ArgumentException("Brand name cannot exceed 100 characters.");

        return name;
    }

    private static string? ValidateAndNormalizeLogoUrl(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            return null;

        logoUrl = logoUrl.Trim();

        if (logoUrl.Length > 1000)
            throw new ArgumentException("Logo URL cannot exceed 1000 characters.");

        return logoUrl;
    }

    private static BrandDto MapToDto(Brand brand)
    {
        return new BrandDto
        {
            BrandId = brand.BrandId,
            Name = brand.Name,
            Slug = brand.Slug,
            LogoUrl = brand.LogoUrl,
            IsActive = brand.IsActive
        };
    }
}
