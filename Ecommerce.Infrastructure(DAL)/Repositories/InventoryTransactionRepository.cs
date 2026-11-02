
using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class InventoryTransactionRepository : IInventoryTransactionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InventoryTransactionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retrieves an inventory transaction by its ID.
    /// </summary>
    public async Task<InventoryTransaction?> GetByIdAsync(long transactionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT InventoryTransactionId, ProductId, QuantityChange,
                   TransactionType, ReferenceOrderId, CreatedAt, Notes
            FROM dbo.InventoryTransactions
            WHERE InventoryTransactionId = @TransactionId;
            """;

        return await connection.QuerySingleOrDefaultAsync<InventoryTransaction>(
            sql,
            new { TransactionId = transactionId });
    }

    /// <summary>
    /// Retrieves all inventory transactions for a specific product.
    /// </summary>
    public async Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(
        long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT InventoryTransactionId, ProductId, QuantityChange,
                   TransactionType, ReferenceOrderId, CreatedAt, Notes
            FROM dbo.InventoryTransactions
            WHERE ProductId = @ProductId
            ORDER BY CreatedAt DESC, InventoryTransactionId DESC;
            """;

        return await connection.QueryAsync<InventoryTransaction>(
            sql,
            new { ProductId = productId });
    }

    /// <summary>
    /// Retrieves all inventory transactions linked to a specific order.
    /// </summary>
    public async Task<IEnumerable<InventoryTransaction>> GetByOrderIdAsync(
        long orderId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT InventoryTransactionId, ProductId, QuantityChange,
                   TransactionType, ReferenceOrderId, CreatedAt, Notes
            FROM dbo.InventoryTransactions
            WHERE ReferenceOrderId = @OrderId
            ORDER BY CreatedAt DESC, InventoryTransactionId DESC;
            """;

        return await connection.QueryAsync<InventoryTransaction>(
            sql,
            new { OrderId = orderId });
    }

    /// <summary>
    /// Creates a new inventory transaction and returns its generated ID.
    /// </summary>
    public async Task<long> CreateAsync(InventoryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.ProductId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transaction.ProductId));
        }

        if (transaction.QuantityChange == 0)
        {
            throw new ArgumentException(
                "QuantityChange cannot be zero.",
                nameof(transaction));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(transaction.TransactionType);

        var allowedTypes = new HashSet<string>
        {
            "Restock",
            "Sale",
            "Return",
            "Adjustment"
        };

        if (!allowedTypes.Contains(transaction.TransactionType))
        {
            throw new ArgumentException(
                "Invalid transaction type.",
                nameof(transaction));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.InventoryTransactions (
                ProductId,
                QuantityChange,
                TransactionType,
                ReferenceOrderId,
                Notes
            )
            VALUES (
                @ProductId,
                @QuantityChange,
                @TransactionType,
                @ReferenceOrderId,
                @Notes
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, transaction);
    }
}
