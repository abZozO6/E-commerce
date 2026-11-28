using Ecommerce.Application_BAL_.DTOs.Coupons;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ecommerce.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CouponsController : ControllerBase
{
    private readonly ICouponService _couponService;

    public CouponsController(ICouponService couponService)
    {
        _couponService = couponService;
    }

    // POST: api/Coupons/validate?code=DISCOUNT&orderAmount=100
    [Authorize]
    [EnableRateLimiting("CouponValidation")]
    [HttpPost("validate")]
    public async Task<ActionResult<CouponValidationDto>> ValidateCoupon(
        [FromQuery] string code,
        [FromQuery] decimal orderAmount)
    {
        var result = await _couponService.ValidateCouponAsync(code, orderAmount);
        return Ok(result);
    }

    // GET: api/Coupons
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CouponDto>>> GetAll()
    {
        var coupons = await _couponService.GetAllAsync();
        return Ok(coupons);
    }

    // GET: api/Coupons/5
    [Authorize(Roles = "Admin")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<CouponDto>> GetById(long id)
    {
        var coupon = await _couponService.GetByIdAsync(id);

        if (coupon is null)
            return NotFound();

        return Ok(coupon);
    }

    // GET: api/Coupons/code/DISCOUNT
    [Authorize(Roles = "Admin")]
    [HttpGet("code/{code}")]
    public async Task<ActionResult<CouponDto>> GetByCode(string code)
    {
        var coupon = await _couponService.GetByCodeAsync(code);

        if (coupon is null)
            return NotFound();

        return Ok(coupon);
    }

    // POST: api/Coupons
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CouponDto>> Create(CreateCouponDto dto)
    {
        var couponId = await _couponService.CreateAsync(dto);
        var coupon = await _couponService.GetByIdAsync(couponId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = couponId },
            coupon);
    }

    // PUT: api/Coupons/5
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateCouponDto dto)
    {
        bool updated = await _couponService.UpdateAsync(id, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // DELETE: api/Coupons/5
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        bool deleted = await _couponService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
