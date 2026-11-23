using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.Payments;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    // POST: api/Payments/order/5
    [HttpPost("order/{orderId:long}")]
    public async Task<ActionResult<PaymentDto>> InitiatePayment(long orderId, CreatePaymentDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var paymentId = await _paymentService.InitiatePaymentAsync(orderId, userId, dto);
        var payment = await _paymentService.GetMyPaymentByIdAsync(paymentId, userId);

        return CreatedAtAction(
            nameof(GetPaymentById),
            new { id = paymentId },
            payment);
    }

    // GET: api/Payments/5
    [HttpGet("{id:long}")]
    public async Task<ActionResult<PaymentDto>> GetPaymentById(long id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var payment = await _paymentService.GetMyPaymentByIdAsync(id, userId);

        if (payment is null)
            return NotFound();

        return Ok(payment);
    }

    // GET: api/Payments/order/5
    [HttpGet("order/{orderId:long}")]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPaymentsByOrderId(long orderId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var payments = await _paymentService.GetMyPaymentsByOrderIdAsync(orderId, userId);
        return Ok(payments);
    }

    // POST: api/Payments/5/callback
    // Restricted to Admin: used for verified payment provider webhook or manual callback processing
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:long}/callback")]
    public async Task<IActionResult> HandleCallback(
        long id,
        [FromQuery] string newStatus,
        [FromQuery] string? transactionReference = null)
    {
        bool handled = await _paymentService.HandleVerifiedCallbackAsync(id, newStatus, transactionReference);

        if (!handled)
            return NotFound();

        return NoContent();
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
