using Ecommerce.Application_BAL_.DTOs.Carts;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

/// <summary>
/// Handles cart operations and ensures carts belong to valid users.
/// </summary>
public class CartService : ICartService
{
    private readonly ICartRepository _cartRepository;
    private readonly IUserRepository _userRepository;

    public CartService( ICartRepository cartRepository, IUserRepository userRepository)
    {
        _cartRepository = cartRepository;
        _userRepository = userRepository;
    }

    public async Task<CartDto?> GetByUserIdAsync(long userId)
    {
        await EnsureUserExistsAsync(userId);

        var cart = await _cartRepository.GetByUserIdAsync(userId);

        return cart is null ? null : MapToDto(cart);
    }

    public async Task<CartDto> GetOrCreateAsync(long userId)
    {
        await EnsureUserExistsAsync(userId);

        var cart = await _cartRepository.GetOrCreateByUserIdAsync(userId);

        return MapToDto(cart);
    }

    public async Task<bool> TouchUpdatedAtAsync(long userId)
    {
        await EnsureUserExistsAsync(userId);

        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart is null)
            return false;

        return await _cartRepository.TouchUpdatedAtAsync(cart.CartId);
    }

    public async Task<bool> DeleteAsync(long userId)
    {
        await EnsureUserExistsAsync(userId);

        return await _cartRepository.DeleteByUserIdAsync(userId);
    }

    private async Task EnsureUserExistsAsync(long userId)
    {
        if (userId <= 0)
            throw new ArgumentException("Invalid user ID.");

        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new KeyNotFoundException("User was not found.");
    }

    private static CartDto MapToDto(Ecommerce.Core.Entities.Cart cart)
    {
        return new CartDto
        {
            CartId = cart.CartId,
            UserId = cart.UserId,
            CreatedAt = cart.CreatedAt,
            UpdatedAt = cart.UpdatedAt
        };
    }
}