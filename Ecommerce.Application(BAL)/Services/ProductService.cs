
using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Helpers;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IBrandRepository _brandRepository;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IBrandRepository brandRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _brandRepository = brandRepository;
    }

    public async Task<ProductDto?> GetByIdAsync(long productId)
    {
        if (productId <= 0)
            throw new ArgumentException("Invalid product ID.");

        var product = await _productRepository.GetByIdAsync(productId);

        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto?> GetBySlugAsync(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required.");

        var product = await _productRepository.GetBySlugAsync(slug.Trim());

        return product is null ? null : MapToDto(product);
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync()
    {
        var products = await _productRepository.GetAllAsync();

        return products.Select(MapToDto);
    }

    public async Task<IEnumerable<ProductDto>> GetActiveFilteredAsync(
        long? categoryId = null,
        long? brandId = null,
        string? searchTerm = null,
        decimal? minPrice = null,
        decimal? maxPrice = null)
    {
        if (categoryId.HasValue && categoryId.Value <= 0)
            throw new ArgumentException("Invalid category ID.");

        if (brandId.HasValue && brandId.Value <= 0)
            throw new ArgumentException("Invalid brand ID.");

        if (minPrice.HasValue && minPrice.Value < 0)
            throw new ArgumentException("Minimum price cannot be negative.");

        if (maxPrice.HasValue && maxPrice.Value < 0)
            throw new ArgumentException("Maximum price cannot be negative.");

        if (minPrice.HasValue &&
            maxPrice.HasValue &&
            minPrice.Value > maxPrice.Value)
        {
            throw new ArgumentException(
                "Minimum price cannot be greater than maximum price.");
        }

        var products = await _productRepository.GetActiveFilteredAsync(
            categoryId,
            brandId,
            searchTerm,
            minPrice,
            maxPrice);

        return products.Select(MapToDto);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        string name = ValidateAndNormalizeName(dto.Name);
        string slug = SlugGenerator.Generate(name);

        if (slug.Length > 220)
            throw new ArgumentException("Product slug cannot exceed 220 characters.");

        ValidatePricingAndWarranty(
            dto.Price,
            dto.WarrantyMonths,
            dto.InitialStockQuantity);

        await ValidateCategoryAndBrandAsync(dto.CategoryId, dto.BrandId);

        bool slugExists = await _productRepository.ExistsBySlugAsync(slug);

        if (slugExists)
            throw new InvalidOperationException(
                "A product with the same slug already exists.");

        var product = new Product
        {
            CategoryId = dto.CategoryId,
            BrandId = dto.BrandId,
            Name = name,
            Slug = slug,
            Description = NormalizeDescription(dto.Description),
            Price = dto.Price,
            StockQuantity = dto.InitialStockQuantity,
            WarrantyMonths = dto.WarrantyMonths,
            IsActive = true
        };

        // The repository generates the SKU automatically.
        long productId = await _productRepository.CreateAsync(product);

        var createdProduct = await _productRepository.GetByIdAsync(productId);

        if (createdProduct is null)
            throw new InvalidOperationException(
                "Product was created but could not be retrieved.");

        return MapToDto(createdProduct);
    }

    public async Task<bool> UpdateAsync(long productId,UpdateProductDto dto)
    {
        if (productId <= 0)
            throw new ArgumentException("Invalid product ID.");

        ArgumentNullException.ThrowIfNull(dto);

        byte[] rowVersionBytes = DecodeRowVersion(dto.RowVersion);

        string name = ValidateAndNormalizeName(dto.Name);

        ValidatePricingAndWarranty(
            dto.Price,
            dto.WarrantyMonths,
            stockQuantity: 0);

        var existingProduct = await _productRepository.GetByIdAsync(productId);

        if (existingProduct is null)
            return false;

        await ValidateCategoryAndBrandAsync(dto.CategoryId, dto.BrandId);

        // Keep the existing slug to avoid breaking existing product URLs.
        existingProduct.CategoryId = dto.CategoryId;
        existingProduct.BrandId = dto.BrandId;
        existingProduct.Name = name;
        existingProduct.Description = NormalizeDescription(dto.Description);
        existingProduct.Price = dto.Price;
        existingProduct.WarrantyMonths = dto.WarrantyMonths;
        existingProduct.IsActive = dto.IsActive;
        existingProduct.RowVersion = rowVersionBytes;

        // SKU is immutable and is never updated here.
        bool updated = await _productRepository.UpdateWithRowVersionAsync(
            existingProduct);

        if (!updated)
        {
            throw new InvalidOperationException(
                "The product was modified by another user. Please refresh and try again.");
        }

        return true;
    }

    public async Task<bool> AdjustStockAsync(long productId,AdjustStockDto dto)
    {
        if (productId <= 0)
            throw new ArgumentException("Invalid product ID.");

        ArgumentNullException.ThrowIfNull(dto);

        if (dto.QuantityChange == 0)
            throw new ArgumentException("Quantity change cannot be zero.");

        if (dto.TransactionType != "Restock" &&
            dto.TransactionType != "Adjustment")
        {
            throw new ArgumentException(
                "TransactionType must be 'Restock' or 'Adjustment'.");
        }

        if (dto.TransactionType == "Restock" && dto.QuantityChange < 0)
        {
            throw new ArgumentException(
                "Restock transactions must increase stock.");
        }

        byte[] rowVersionBytes = DecodeRowVersion(dto.RowVersion);
        string? notes = NormalizeDescription(dto.Notes);

        if (notes?.Length > 500)
            throw new ArgumentException("Notes cannot exceed 500 characters.");

        var existingProduct = await _productRepository.GetByIdAsync(productId);

        if (existingProduct is null)
            return false;

        long resultingStock =
            (long)existingProduct.StockQuantity + dto.QuantityChange;

        if (resultingStock < 0)
            throw new InvalidOperationException(
                "Insufficient stock for this adjustment.");

        if (resultingStock > int.MaxValue)
            throw new InvalidOperationException(
                "Stock quantity exceeds the maximum allowed value.");

        bool adjusted = await _productRepository.AdjustStockWithTransactionAsync(
            productId,
            dto.QuantityChange,
            dto.TransactionType,
            notes,
            rowVersionBytes);

        if (!adjusted)
        {
            throw new InvalidOperationException(
                "Stock adjustment failed due to concurrent modification or insufficient stock. Please refresh.");
        }

        return true;
    }

    public async Task<bool> SetActiveStatusAsync(long productId,bool isActive)
    {
        if (productId <= 0)
            throw new ArgumentException("Invalid product ID.");

        return await _productRepository.SetActiveStatusAsync(
            productId,
            isActive);
    }

    private async Task ValidateCategoryAndBrandAsync(long categoryId,long? brandId)
    {
        if (categoryId <= 0)
            throw new ArgumentException("Invalid category ID.");

        var category = await _categoryRepository.GetByIdAsync(categoryId);

        if (category is null)
            throw new KeyNotFoundException("Category was not found.");

        if (!category.IsActive)
            throw new InvalidOperationException(
                "Cannot assign a product to an inactive category.");

        if (brandId.HasValue)
        {
            if (brandId.Value <= 0)
                throw new ArgumentException("Invalid brand ID.");

            var brand = await _brandRepository.GetByIdAsync(brandId.Value);

            if (brand is null)
                throw new KeyNotFoundException("Brand was not found.");

            if (!brand.IsActive)
                throw new InvalidOperationException(
                    "Cannot assign a product to an inactive brand.");
        }
    }

    public async Task<ProductDetailsDto?> GetDetailsByIdAsync(long productId)
    {
        if (productId <= 0)
            throw new ArgumentException("Invalid product ID.");

        var product = await _productRepository.GetDetailsByIdAsync(productId);

        if (product is null)
            return null;

        return new ProductDetailsDto
        {
            ProductId = product.ProductId,
            BrandId = product.BrandId,
            CategoryId = product.CategoryId,
            Name = product.Name,
            Slug = product.Slug,
            SKU = product.SKU,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            WarrantyMonths = product.WarrantyMonths,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            RowVersion = Convert.ToBase64String(product.RowVersion),

            CategoryName = product.CategoryName,
            BrandName = product.BrandName,
            ImageUrl = product.ImageUrl
        };
    }

    private static string ValidateAndNormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.");

        name = name.Trim();

        if (name.Length > 200)
            throw new ArgumentException(
                "Product name cannot exceed 200 characters.");

        return name;
    }

    private static void ValidatePricingAndWarranty(decimal price, int? warrantyMonths,int stockQuantity)
    {
        if (price < 0)
            throw new ArgumentException(
                "Product price cannot be negative.");

        if (warrantyMonths.HasValue && warrantyMonths.Value < 0)
            throw new ArgumentException(
                "Warranty months cannot be negative.");

        if (stockQuantity < 0)
            throw new ArgumentException(
                "Stock quantity cannot be negative.");
    }

    private static string? NormalizeDescription(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static byte[] DecodeRowVersion(string rowVersionBase64)
    {
        if (string.IsNullOrWhiteSpace(rowVersionBase64))
            throw new ArgumentException(
                "RowVersion is required for concurrency check.");

        try
        {
            byte[] bytes = Convert.FromBase64String(rowVersionBase64.Trim());

            if (bytes.Length != 8)
                throw new ArgumentException(
                    "Invalid RowVersion length.");

            return bytes;
        }
        catch (FormatException)
        {
            throw new ArgumentException(
                "Invalid RowVersion format. Expected a valid Base64 string.");
        }
    }

    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            ProductId = product.ProductId,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            Name = product.Name,
            Slug = product.Slug,
            Sku = product.SKU,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            WarrantyMonths = product.WarrantyMonths,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            RowVersion = product.RowVersion is not null
                ? Convert.ToBase64String(product.RowVersion)
                : string.Empty
        };
    }
}
