using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long productId);

    Task<Product?> GetBySlugAsync(string slug);

    Task<IEnumerable<Product>> GetAllAsync();

    Task<IEnumerable<Product>> GetActiveFilteredAsync( long? categoryId = null, long? brandId = null, string? searchTerm = null, decimal? minPrice = null, decimal? maxPrice = null);

    Task<bool> ExistsBySkuOrSlugAsync(string sku, string slug, long? excludeProductId = null);

    Task<long> CreateAsync(Product product);

    Task<bool> UpdateWithRowVersionAsync(Product product);

    Task<bool> AdjustStockWithTransactionAsync( long productId, int quantityChange, string transactionType, string? notes, byte[] rowVersion);

    Task<bool> SetActiveStatusAsync(long productId, bool isActive);
}
