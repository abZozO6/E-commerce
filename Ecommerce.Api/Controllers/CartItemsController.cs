using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.CartItems;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[Authorize]
[ApiController]
[Route("api/cart/items")]
[Route("api/[controller]")]
public class CartItemsController : ControllerBase
{
    private readonly ICartItemService _cartItemService;

    public CartItemsController(ICartItemService cartItemService)
    {
        _cartItemService = cartItemService;
    }

    // GET: api/cart/items
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CartItemDto>>> GetMyCartItems()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var items = await _cartItemService.GetMyCartItemsAsync(userId);
        return Ok(items);
    }

    // GET: api/cart/items/details
    // GET: api/cart/details
    [HttpGet("details")]
    [HttpGet("/api/cart/details")]
    public async Task<ActionResult<IEnumerable<CartItemDetailsDto>>> GetMyCartDetails()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var details = await _cartItemService.GetMyCartDetailsAsync(userId);
        return Ok(details);
    }

    // POST: api/cart/items
    [HttpPost]
    public async Task<ActionResult<CartItemDto>> AddItem(AddCartItemDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var item = await _cartItemService.AddItemAsync(userId, dto);
        return Ok(item);
    }

    // PUT: api/cart/items/5
    [HttpPut("{cartItemId:long}")]
    public async Task<IActionResult> UpdateQuantity(long cartItemId, UpdateCartItemQuantityDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool updated = await _cartItemService.UpdateQuantityAsync(userId, cartItemId, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/cart/items/5
    [HttpDelete("{cartItemId:long}")]
    public async Task<IActionResult> RemoveItem(long cartItemId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool removed = await _cartItemService.RemoveItemAsync(userId, cartItemId);

        if (!removed)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/cart/items
    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool cleared = await _cartItemService.ClearCartAsync(userId);

        if (!cleared)
            return NotFound();

        return NoContent();
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
