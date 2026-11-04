using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class ProductSpecificationService : IProductSpecificationService
{
    private readonly IProductSpecificationRepository _specificationRepository;
    private readonly IProductRepository _productRepository;

    public ProductSpecificationService(
        IProductSpecificationRepository specificationRepository,
        IProductRepository productRepository)
    {
        _specificationRepository = specificationRepository;
        _productRepository = productRepository;
    }

    public async Task<ProductSpecificationDto?> GetByIdAsync(long specId)
    {
        if (specId <= 0)
            throw new ArgumentOutOfRangeException(nameof(specId));

        var specification =
            await _specificationRepository.GetByIdAsync(specId);

        return specification is null
            ? null
            : MapToDto(specification);
    }

    public async Task<IEnumerable<ProductSpecificationDto>> GetByProductIdAsync(
        long productId)
    {
        await EnsureProductExistsAsync(productId);

        var specifications =
            await _specificationRepository.GetByProductIdAsync(productId);

        return specifications.Select(MapToDto);
    }

    public async Task<ProductSpecificationDto> UpsertAsync(
        long productId,
        UpsertProductSpecificationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await EnsureProductExistsAsync(productId);

        var specification = MapToEntity(productId, dto);

        long specId =
            await _specificationRepository.UpsertAsync(specification);

        var savedSpecification =
            await _specificationRepository.GetByIdAsync(specId);

        if (savedSpecification is null)
        {
            throw new InvalidOperationException(
                "The specification was saved but could not be retrieved.");
        }

        return MapToDto(savedSpecification);
    }

    public async Task<bool> ReplaceProductSpecificationsAsync(
        long productId,
        IEnumerable<UpsertProductSpecificationDto> specifications)
    {
        ArgumentNullException.ThrowIfNull(specifications);

        await EnsureProductExistsAsync(productId);

        var specificationList = specifications
            .Select(dto =>
            {
                ArgumentNullException.ThrowIfNull(dto);
                return MapToEntity(productId, dto);
            })
            .ToList();

        return await _specificationRepository
            .ReplaceProductSpecificationsAsync(
                productId,
                specificationList);
    }

    public async Task<bool> UpdateAsync(
        long productId,
        long specId,
        UpsertProductSpecificationDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (specId <= 0)
            throw new ArgumentOutOfRangeException(nameof(specId));

        await EnsureProductExistsAsync(productId);

        var specification = MapToEntity(productId, dto);
        specification.SpecificationId = specId;

        return await _specificationRepository.UpdateAsync(specification);
    }

    public async Task<bool> DeleteAsync(long productId, long specId)
    {
        if (specId <= 0)
            throw new ArgumentOutOfRangeException(nameof(specId));

        await EnsureProductExistsAsync(productId);

        return await _specificationRepository.DeleteAsync(
            specId,
            productId);
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

    private static ProductSpecification MapToEntity(
        long productId,
        UpsertProductSpecificationDto dto)
    {
        string name = dto.SpecName?.Trim() ?? string.Empty;
        string value = dto.SpecValue?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Specification name is required.");

        if (name.Length > 100)
            throw new ArgumentException(
                "Specification name cannot exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Specification value is required.");

        if (value.Length > 500)
            throw new ArgumentException(
                "Specification value cannot exceed 500 characters.");

        if (dto.DisplayOrder < 0)
            throw new ArgumentOutOfRangeException(
                nameof(dto.DisplayOrder),
                "Display order cannot be negative.");

        return new ProductSpecification
        {
            ProductId = productId,
            SpecName = name,
            SpecValue = value,
            DisplayOrder = dto.DisplayOrder
        };
    }

    private static ProductSpecificationDto MapToDto(ProductSpecification specification)
    {
        return new ProductSpecificationDto
        {
            SpecId = specification.SpecificationId,
            ProductId = specification.ProductId,
            SpecName = specification.SpecName,
            SpecValue = specification.SpecValue,
            DisplayOrder = specification.DisplayOrder
        };
    }
}