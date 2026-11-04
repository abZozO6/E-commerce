using Ecommerce.Application_BAL_.DTOs.Products;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(long productId);

    Task<ProductDto?> GetBySlugAsync(string slug);

    Task<IEnumerable<ProductDto>> GetAllAsync();

    Task<IEnumerable<ProductDto>> GetActiveFilteredAsync(
        long? categoryId = null,
        long? brandId = null,
        string? searchTerm = null,
        decimal? minPrice = null,
        decimal? maxPrice = null);

    Task<ProductDto> CreateAsync(CreateProductDto dto);

    Task<bool> UpdateAsync(long productId, UpdateProductDto dto);

    Task<bool> AdjustStockAsync(long productId, AdjustStockDto dto);

    Task<bool> SetActiveStatusAsync(long productId, bool isActive);
}