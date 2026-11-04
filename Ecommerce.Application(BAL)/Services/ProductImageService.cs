using Ecommerce.Application_BAL_.DTOs.Products;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class ProductImageService : IProductImageService
{
    private readonly IProductImageRepository _productImageRepository;
    private readonly IProductRepository _productRepository;

    public ProductImageService(IProductImageRepository productImageRepository, IProductRepository productRepository)
    {
        _productImageRepository = productImageRepository;
        _productRepository = productRepository;
    }

    public async Task<ProductImageDto?> GetByIdAsync(long imageId)
    {
        if (imageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));

        var image = await _productImageRepository.GetByIdAsync(imageId);

        return image is null ? null : MapToDto(image);
    }

    public async Task<IEnumerable<ProductImageDto>> GetByProductIdAsync(long productId)
    {
        await EnsureProductExistsAsync(productId);

        var images = await _productImageRepository.GetByProductIdAsync(productId);

        return images.Select(MapToDto);
    }

    public async Task<ProductImageDto?> GetMainImageByProductIdAsync( long productId)
    {
        await EnsureProductExistsAsync(productId);

        var image = await _productImageRepository
            .GetMainImageByProductIdAsync(productId);

        return image is null ? null : MapToDto(image);
    }

    public async Task<ProductImageDto> CreateAsync(long productId,CreateProductImageDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await EnsureProductExistsAsync(productId);

        string imageUrl = ValidateAndNormalizeImageUrl(dto.ImageUrl);
        string? altText = NormalizeAltText(dto.AltText);
        ValidateDisplayOrder(dto.DisplayOrder);

        // The first image of a product becomes its primary image.
        var currentMainImage = await _productImageRepository
            .GetMainImageByProductIdAsync(productId);

        bool isPrimary = dto.IsPrimary || currentMainImage is null;

        var image = new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl,
            AltText = altText,
            DisplayOrder = dto.DisplayOrder,
            IsPrimary = isPrimary
        };

        long imageId = await _productImageRepository.CreateAsync(image);

        var createdImage = await _productImageRepository.GetByIdAsync(imageId);

        if (createdImage is null)
            throw new InvalidOperationException(
                "The product image was created but could not be retrieved.");

        return MapToDto(createdImage);
    }

    public async Task<bool> UpdateAsync(long productId,long imageId,UpdateProductImageDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (imageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));

        await EnsureProductExistsAsync(productId);

        var existingImage = await _productImageRepository.GetByIdAsync(imageId);

        if (existingImage is null || existingImage.ProductId != productId)
            return false;

        existingImage.ImageUrl =
            ValidateAndNormalizeImageUrl(dto.ImageUrl);

        existingImage.AltText = NormalizeAltText(dto.AltText);

        ValidateDisplayOrder(dto.DisplayOrder);
        existingImage.DisplayOrder = dto.DisplayOrder;

        // IsPrimary is managed through SetMainImageAsync.
        return await _productImageRepository.UpdateAsync(existingImage);
    }

    public async Task<bool> SetMainImageAsync(long productId,long imageId)
    {
        if (imageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));

        await EnsureProductExistsAsync(productId);

        return await _productImageRepository
            .SetMainImageAsync(imageId, productId);
    }

    public async Task<bool> DeleteAsync(long productId, long imageId)
    {
        if (imageId <= 0)
            throw new ArgumentOutOfRangeException(nameof(imageId));

        await EnsureProductExistsAsync(productId);

        return await _productImageRepository.DeleteAsync(imageId, productId);
    }

    private async Task EnsureProductExistsAsync(long productId)
    {
        if (productId <= 0)
            throw new ArgumentOutOfRangeException(nameof(productId));

        var product = await _productRepository.GetByIdAsync(productId);

        if (product is null)
            throw new KeyNotFoundException(
                $"Product with ID {productId} was not found.");
    }

    private static string ValidateAndNormalizeImageUrl(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("Image URL is required.");

        imageUrl = imageUrl.Trim();

        if (imageUrl.Length > 1000)
            throw new ArgumentException(
                "Image URL cannot exceed 1000 characters.");

        bool isValidUrl = Uri.TryCreate(
            imageUrl,
            UriKind.Absolute,
            out var uri);

        if (!isValidUrl ||
            (uri!.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Image URL must be a valid HTTP or HTTPS URL.");
        }

        return imageUrl;
    }

    private static string? NormalizeAltText(string? altText)
    {
        if (string.IsNullOrWhiteSpace(altText))
            return null;

        altText = altText.Trim();

        if (altText.Length > 200)
            throw new ArgumentException(
                "Alt text cannot exceed 200 characters.");

        return altText;
    }

    private static void ValidateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                "Display order cannot be negative.");
    }

    private static ProductImageDto MapToDto(ProductImage image)
    {
        return new ProductImageDto
        {
            ProductImageId = image.ProductImageId,
            ProductId = image.ProductId,
            ImageUrl = image.ImageUrl,
            AltText = image.AltText,
            IsPrimary = image.IsPrimary,
            DisplayOrder = image.DisplayOrder
        };
    }
}