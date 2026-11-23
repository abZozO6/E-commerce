using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.Carts;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[Authorize]
[ApiController]
[Route("api/cart")]
[Route("api/[controller]")]
public class CartsController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartsController(ICartService cartService)
    {
        _cartService = cartService;
    }

    // GET: api/cart
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var cart = await _cartService.GetOrCreateAsync(userId);
        return Ok(cart);
    }

    // DELETE: api/cart
    [HttpDelete]
    public async Task<IActionResult> DeleteCart()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool deleted = await _cartService.DeleteAsync(userId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    // POST: api/cart/touch
    [HttpPost("touch")]
    public async Task<IActionResult> TouchCart()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool touched = await _cartService.TouchUpdatedAtAsync(userId);

        if (!touched)
            return NotFound();

        return NoContent();
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
