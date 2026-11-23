using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.Payments;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Enums;
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

    // ==========================================
    // Customer Endpoints
    // ==========================================

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

    // ==========================================
    // Admin Endpoints
    // ==========================================

    /// <summary>
    /// Admin manually updates a payment's status and records an audit log entry.
    /// PUT replaces the old POST /callback endpoint with a clearly named, body-driven operation.
    /// </summary>
    // PUT: api/Payments/5/status
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> AdminUpdateStatus(long id, [FromBody] AdminUpdatePaymentStatusDto dto)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized();

        var result = await _paymentService.AdminUpdateStatusAsync(id, adminUserId, dto);

        return result switch
        {
            PaymentStatusUpdateResult.Updated => NoContent(),
            PaymentStatusUpdateResult.NotFound => NotFound(),
            PaymentStatusUpdateResult.NoChange => Ok(new
            {
                message = "Payment already has the requested status. No changes were made."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    // GET: api/Payments/5/audit-logs
    [Authorize(Roles = "Admin")]
    [HttpGet("{id:long}/audit-logs")]
    public async Task<ActionResult<IEnumerable<PaymentStatusAuditLogDto>>> GetAuditLogs(long id)
    {
        var logs = await _paymentService.GetAuditLogsAsync(id);
        return Ok(logs);
    }

    // ==========================================
    // Helpers
    // ==========================================

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
