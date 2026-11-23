using Ecommerce.Application_BAL_.DTOs.Inventory;
using Ecommerce.Application_BAL_.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/inventory-transactions")]
[Route("api/[controller]")]
public class InventoryTransactionsController : ControllerBase
{
    private readonly IInventoryTransactionService _inventoryTransactionService;

    public InventoryTransactionsController(IInventoryTransactionService inventoryTransactionService)
    {
        _inventoryTransactionService = inventoryTransactionService;
    }

    // GET: api/inventory-transactions/5
    [HttpGet("{id:long}")]
    public async Task<ActionResult<InventoryTransactionDto>> GetById(long id)
    {
        var transaction = await _inventoryTransactionService.GetByIdAsync(id);

        if (transaction is null)
            return NotFound();

        return Ok(transaction);
    }

    // GET: api/inventory-transactions/product/10
    [HttpGet("product/{productId:long}")]
    public async Task<ActionResult<IEnumerable<InventoryTransactionDto>>> GetByProductId(long productId)
    {
        var transactions = await _inventoryTransactionService.GetByProductIdAsync(productId);
        return Ok(transactions);
    }

    // GET: api/inventory-transactions/order/20
    [HttpGet("order/{orderId:long}")]
    public async Task<ActionResult<IEnumerable<InventoryTransactionDto>>> GetByOrderId(long orderId)
    {
        var transactions = await _inventoryTransactionService.GetByOrderIdAsync(orderId);
        return Ok(transactions);
    }
}
