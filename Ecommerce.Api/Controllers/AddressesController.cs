using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.Addresses;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AddressesController : ControllerBase
{
    private readonly IAddressService _addressService;

    public AddressesController(IAddressService addressService)
    {
        _addressService = addressService;
    }

    // GET: api/Addresses
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AddressDto>>> GetAll()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var addresses = await _addressService.GetByUserIdAsync(userId);
        return Ok(addresses);
    }

    // GET: api/Addresses/5
    [HttpGet("{id:long}")]
    public async Task<ActionResult<AddressDto>> GetById(long id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var address = await _addressService.GetByIdAsync(id, userId);

        if (address is null)
            return NotFound();

        return Ok(address);
    }

    // POST: api/Addresses
    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create(CreateAddressDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var address = await _addressService.CreateAsync(userId, dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = address.AddressId },
            address);
    }

    // PUT: api/Addresses/5
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateAddressDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool updated = await _addressService.UpdateAsync(id, userId, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/Addresses/5
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool deleted = await _addressService.DeleteAsync(id, userId);

        if (!deleted)
            return NotFound();

        return NoContent();
    }

    // PATCH: api/Addresses/5/default
    [HttpPatch("{id:long}/default")]
    [HttpPut("{id:long}/default")]
    public async Task<IActionResult> SetDefault(long id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool updated = await _addressService.SetDefaultAddressAsync(id, userId);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
