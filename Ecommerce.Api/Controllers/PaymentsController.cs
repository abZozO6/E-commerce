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
    public async Task<ActionResult<InitiatePaymentResult>> InitiatePayment(long orderId, CreatePaymentDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var result = await _paymentService.InitiatePaymentAsync(orderId, userId, dto);

        return CreatedAtAction(
            nameof(GetPaymentById),
            new { id = result.PaymentId },
            result);
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
    /// Admin lists all payments across orders with an optional status filter.
    /// </summary>
    // GET: api/Payments
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentDto>>> GetAll([FromQuery] string? status = null)
    {
        var payments = await _paymentService.GetAllAsync(status);
        return Ok(payments);
    }

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

    /// <summary>
    /// Admin initiates a full refund for a Succeeded payment.
    /// Calls provider, transitions payment to Refunded, order to Refunded, and writes an audit log.
    /// </summary>
    // POST: api/Payments/5/refund
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:long}/refund")]
    public async Task<IActionResult> Refund(long id, [FromBody] RefundPaymentDto dto)
    {
        if (!TryGetUserId(out var adminUserId))
            return Unauthorized();

        var result = await _paymentService.RefundAsync(id, adminUserId, dto.Reason);

        return result switch
        {
            PaymentStatusUpdateResult.Updated => NoContent(),
            PaymentStatusUpdateResult.NotFound => NotFound(),
            PaymentStatusUpdateResult.NoChange => Ok(new
            {
                message = "Payment is already marked as refunded."
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
    // Webhook Endpoint (Provider Callbacks)
    // ==========================================

    /// <summary>
    /// Inbound payment provider webhook endpoint.
    /// Requires cryptographic signature header (X-Signature or X-Webhook-Signature).
    /// Anonymous access is allowed because verification is performed via HMAC signature.
    /// </summary>
    // POST: api/Payments/webhook
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> ProcessWebhook()
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Signature"].FirstOrDefault()
            ?? Request.Headers["X-Webhook-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(signature))
        {
            return BadRequest(new
            {
                error = "Webhook signature header (X-Signature or X-Webhook-Signature) is missing."
            });
        }

        var success = await _paymentService.ProcessWebhookAsync(payload, signature);

        return Ok(new
        {
            message = "Webhook processed successfully.",
            updated = success
        });
    }

    // ==========================================
    // Helpers
    // ==========================================

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
