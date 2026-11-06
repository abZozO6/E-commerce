using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ecommerce.Application_BAL_.DTOs.CartItems;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface ICartItemService
{
    Task<IEnumerable<CartItemDto>> GetMyCartItemsAsync(long userId);


    Task<IEnumerable<CartItemDetailsDto>> GetMyCartDetailsAsync(long userId);

    Task<CartItemDto> AddItemAsync(long userId, AddCartItemDto dto);

    Task<bool> UpdateQuantityAsync(long userId,long cartItemId,UpdateCartItemQuantityDto dto);

    Task<bool> RemoveItemAsync(long userId, long cartItemId);

    Task<bool> ClearCartAsync(long userId);
}