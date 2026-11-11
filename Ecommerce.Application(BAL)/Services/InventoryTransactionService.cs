using Ecommerce.Application_BAL_.DTOs.Inventory;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class InventoryTransactionService : IInventoryTransactionService
{
    private readonly IInventoryTransactionRepository _repository;

    public InventoryTransactionService(IInventoryTransactionRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Retrieves an inventory transaction by its ID.
    /// </summary>
    public async Task<InventoryTransactionDto?> GetByIdAsync(long transactionId)
    {
        ValidateId(transactionId, nameof(transactionId));

        var transaction = await _repository.GetByIdAsync(transactionId);

        if (transaction is null)
            return null;

        return MapToDto(transaction);
    }

    /// <summary>
    /// Retrieves all inventory transactions for a specific product.
    /// </summary>
    public async Task<IEnumerable<InventoryTransactionDto>> GetByProductIdAsync(long productId)
    {
        ValidateId(productId, nameof(productId));

        var transactions = await _repository.GetByProductIdAsync(productId);

        return transactions.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Retrieves all inventory transactions associated with an order.
    /// </summary>
    public async Task<IEnumerable<InventoryTransactionDto>> GetByOrderIdAsync(long orderId)
    {
        ValidateId(orderId, nameof(orderId));

        var transactions = await _repository.GetByOrderIdAsync(orderId);

        return transactions.Select(MapToDto).ToList();
    }

    private static InventoryTransactionDto MapToDto(Ecommerce.Core.Entities.InventoryTransaction transaction)
    {
        return new InventoryTransactionDto
        {
            InventoryTransactionId = transaction.InventoryTransactionId,
            ProductId = transaction.ProductId,
            QuantityChange = transaction.QuantityChange,
            TransactionType = transaction.TransactionType,
            ReferenceOrderId = transaction.ReferenceOrderId,
            CreatedAt = transaction.CreatedAt,
            Notes = transaction.Notes
        };
    }

    private static void ValidateId(long id, string parameterName)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}
