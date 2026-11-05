
using Ecommerce.Application_BAL_.DTOs.CartItems;
using Ecommerce.Application_BAL_.DTOs.Carts;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

/// <summary>
/// Handles cart item operations and enforces cart ownership and product rules.
/// </summary>
public class CartItemService : ICartItemService
{
    private readonly ICartItemRepository _cartItemRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUserRepository _userRepository;

    public CartItemService(
        ICartItemRepository cartItemRepository,
        ICartRepository cartRepository,
        IProductRepository productRepository,
        IUserRepository userRepository)
    {
        _cartItemRepository = cartItemRepository
            ?? throw new ArgumentNullException(nameof(cartItemRepository));

        _cartRepository = cartRepository
            ?? throw new ArgumentNullException(nameof(cartRepository));

        _productRepository = productRepository
            ?? throw new ArgumentNullException(nameof(productRepository));

        _userRepository = userRepository
            ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <summary>
    /// Gets all items belonging to the user's cart.
    /// </summary>
    public async Task<IEnumerable<CartItemDto>> GetMyCartItemsAsync(long userId)
    {
        await EnsureUserExistsAsync(userId);

        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart is null)
            return Enumerable.Empty<CartItemDto>();

        var items = await _cartItemRepository.GetByCartIdAsync(cart.CartId);

        return items.Select(MapToDto);
    }

    /// <summary>
    /// Adds a product to the user's cart or increments its quantity.
    /// </summary>
    public async Task<CartItemDto> AddItemAsync(
        long userId,
        AddCartItemDto dto)
    {
        await EnsureUserExistsAsync(userId);

        ArgumentNullException.ThrowIfNull(dto);

        ValidateId(dto.ProductId, nameof(dto.ProductId));

        if (dto.Quantity <= 0)
            throw new ArgumentException(
                "Quantity must be greater than zero.",
                nameof(dto.Quantity));

        var product = await _productRepository.GetByIdAsync(dto.ProductId);

        if (product is null)
            throw new KeyNotFoundException("Product was not found.");

        if (!product.IsActive)
            throw new InvalidOperationException(
                "Cannot add an inactive product to the cart.");

        var cart = await _cartRepository.GetOrCreateByUserIdAsync(userId);

        var existingItem = await _cartItemRepository.GetByCartAndProductAsync(
            cart.CartId,
            dto.ProductId);

        int currentQuantity = existingItem?.Quantity ?? 0;

        int totalRequestedQuantity = checked(
            currentQuantity + dto.Quantity);

        if (totalRequestedQuantity > product.StockQuantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock. Available: {product.StockQuantity}, " +
                $"currently in your cart: {currentQuantity}.");
        }

        var cartItemId = await _cartItemRepository.AddOrIncrementAsync(
            new CartItem
            {
                CartId = cart.CartId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            });

        var savedItem = await _cartItemRepository.GetByIdAsync(cartItemId);

        if (savedItem is null)
            throw new InvalidOperationException(
                "Cart item was saved but could not be retrieved.");

        return MapToDto(savedItem);
    }

    /// <summary>
    /// Updates the quantity of an item belonging to the user's cart.
    /// </summary>
    public async Task<bool> UpdateQuantityAsync(
        long userId,
        long cartItemId,
        UpdateCartItemQuantityDto dto)
    {
        await EnsureUserExistsAsync(userId);

        ValidateId(cartItemId, nameof(cartItemId));

        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Quantity <= 0)
            throw new ArgumentException(
                "Quantity must be greater than zero.",
                nameof(dto.Quantity));

        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart is null)
            return false;

        var existingItem = await _cartItemRepository.GetByIdAsync(cartItemId);

        if (existingItem is null || existingItem.CartId != cart.CartId)
            return false;

        var product = await _productRepository.GetByIdAsync(
            existingItem.ProductId);

        if (product is null || !product.IsActive)
            throw new InvalidOperationException(
                "Product is no longer available.");

        if (dto.Quantity > product.StockQuantity)
        {
            throw new InvalidOperationException(
                $"Insufficient stock. Maximum available quantity is " +
                $"{product.StockQuantity}.");
        }

        return await _cartItemRepository.UpdateQuantityAsync(
            cartItemId,
            cart.CartId,
            dto.Quantity);
    }

    /// <summary>
    /// Removes an item from the user's cart.
    /// </summary>
    public async Task<bool> RemoveItemAsync(
        long userId,
        long cartItemId)
    {
        await EnsureUserExistsAsync(userId);

        ValidateId(cartItemId, nameof(cartItemId));

        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart is null)
            return false;

        return await _cartItemRepository.DeleteAsync(
            cartItemId,
            cart.CartId);
    }

    /// <summary>
    /// Removes all items from the user's cart.
    /// </summary>
    public async Task<bool> ClearCartAsync(long userId)
    {
        await EnsureUserExistsAsync(userId);

        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart is null)
            return false;

        return await _cartItemRepository.ClearCartAsync(cart.CartId);
    }

    private async Task EnsureUserExistsAsync(long userId)
    {
        ValidateId(userId, nameof(userId));

        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new KeyNotFoundException("User was not found.");
    }

    private static void ValidateId(long id, string paramName)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Invalid {paramName}.");
    }

    private static CartItemDto MapToDto(CartItem item)
    {
        return new CartItemDto
        {
            CartItemId = item.CartItemId,
            CartId = item.CartId,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            AddedAt = item.AddedAt
        };
    }
}
