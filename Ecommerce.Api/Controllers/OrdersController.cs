using System.Security.Claims;
using Ecommerce.Application_BAL_.DTOs.Orders;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // ==========================================
    // Customer Endpoints (Current Authenticated User)
    // ==========================================

    // GET: api/Orders/my
    [HttpGet("my")]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetMyOrders()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var orders = await _orderService.GetMyOrdersAsync(userId);
        return Ok(orders);
    }

    // GET: api/Orders/my/5
    [HttpGet("my/{id:long}")]
    public async Task<ActionResult<OrderDto>> GetMyOrderById(long id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var order = await _orderService.GetMyOrderByIdAsync(id, userId);

        if (order is null)
            return NotFound();

        return Ok(order);
    }

    // POST: api/Orders
    [HttpPost]
    public async Task<ActionResult<OrderDto>> CreateOrder(CreateOrderDto dto)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var orderId = await _orderService.CreateAsync(userId, dto);
        var order = await _orderService.GetMyOrderByIdAsync(orderId, userId);

        return CreatedAtAction(
            nameof(GetMyOrderById),
            new { id = orderId },
            order);
    }

    // POST: api/Orders/my/5/cancel
    [HttpPost("my/{id:long}/cancel")]
    [HttpPut("my/{id:long}/cancel")]
    public async Task<IActionResult> CancelMyOrder(long id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        bool cancelled = await _orderService.CancelMyOrderAsync(id, userId);

        if (!cancelled)
            return NotFound();

        return NoContent();
    }

    // ==========================================
    // Admin Endpoints
    // ==========================================

    // GET: api/Orders?status=Pending
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAllOrders([FromQuery] string? status = null)
    {
        var orders = await _orderService.GetAllOrdersAsync(status);
        return Ok(orders);
    }

    // GET: api/Orders/5
    [Authorize(Roles = "Admin")]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(long id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);

        if (order is null)
            return NotFound();

        return Ok(order);
    }

    // PUT: api/Orders/5/status
    [Authorize(Roles = "Admin")]
    [HttpPut("{id:long}/status")]
    public async Task<IActionResult> UpdateOrderStatus(long id, UpdateOrderStatusDto dto)
    {
        bool updated = await _orderService.UpdateStatusAsync(id, dto);

        if (!updated)
            return NotFound();

        return NoContent();
    }

    // POST: api/Orders/5/cancel
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:long}/cancel")]
    [HttpPut("{id:long}/cancel")]
    public async Task<IActionResult> CancelOrder(long id)
    {
        bool cancelled = await _orderService.CancelOrderAsync(id);

        if (!cancelled)
            return NotFound();

        return NoContent();
    }

    private bool TryGetUserId(out long userId)
    {
        return long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
