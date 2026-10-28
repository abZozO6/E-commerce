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

    public async Task<InventoryTransaction?> GetByIdAsync(long transactionId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT TransactionId, ProductId, OrderId, ChangeQuantity,
                   TransactionType, Notes, CreatedAt
            FROM dbo.InventoryTransactions
            WHERE TransactionId = @TransactionId;
            """;

        return await connection.QuerySingleOrDefaultAsync<InventoryTransaction>(
            sql,
            new { TransactionId = transactionId });
    }

    public async Task<IEnumerable<InventoryTransaction>> GetByProductIdAsync(long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT TransactionId, ProductId, OrderId, ChangeQuantity,
                   TransactionType, Notes, CreatedAt
            FROM dbo.InventoryTransactions
            WHERE ProductId = @ProductId
            ORDER BY CreatedAt DESC, TransactionId DESC;
            """;

        return await connection.QueryAsync<InventoryTransaction>(
            sql,
            new { ProductId = productId });
    }

    public async Task<IEnumerable<InventoryTransaction>> GetByOrderIdAsync( long orderId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT TransactionId, ProductId, OrderId, ChangeQuantity,
                   TransactionType, Notes, CreatedAt
            FROM dbo.InventoryTransactions
            WHERE OrderId = @OrderId
            ORDER BY CreatedAt DESC, TransactionId DESC;
            """;

        return await connection.QueryAsync<InventoryTransaction>(
            sql,
            new { OrderId = orderId });
    }

    public async Task<long> CreateAsync(InventoryTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (transaction.QuantityChange == 0)
        {
            throw new ArgumentException(
                "QuantityChange cannot be zero.",
                nameof(transaction));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(transaction.TransactionType);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.InventoryTransactions (
                ProductId, OrderId, ChangeQuantity, TransactionType, Notes
            )
            VALUES (
                @ProductId, @OrderId, @ChangeQuantity, @TransactionType, @Notes
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, transaction);
    }
}
