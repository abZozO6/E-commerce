using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class OrderItemRepository : IOrderItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OrderItemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OrderItem?> GetByIdAsync(long orderItemId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderItemId, OrderId, ProductId, ProductName,
                   UnitPrice, Quantity, LineTotal
            FROM dbo.OrderItems
            WHERE OrderItemId = @OrderItemId;
            """;

        return await connection.QuerySingleOrDefaultAsync<OrderItem>(
            sql,
            new { OrderItemId = orderItemId });
    }

    /// <summary>
    /// Retrieves all items belonging to a specific order.
    /// Returns an empty collection if the order has no items.
    /// </summary>
    public async Task<IEnumerable<OrderItem>> GetByOrderIdAsync(long orderId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT OrderItemId, OrderId, ProductId, ProductName,
                   UnitPrice, Quantity, LineTotal
            FROM dbo.OrderItems
            WHERE OrderId = @OrderId
            ORDER BY OrderItemId ASC;
            """;

        return await connection.QueryAsync<OrderItem>(
            sql,
            new { OrderId = orderId });
    }

    /// <summary>
    /// Creates an order item, calculates its line total, and returns its generated ID.
    /// </summary>
    public async Task<long> CreateAsync(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(item.Quantity), "Quantity must be greater than zero.");

        if (item.UnitPrice < 0)
            throw new ArgumentOutOfRangeException(
                nameof(item.UnitPrice), "Unit price cannot be negative.");

        item.LineTotal = item.UnitPrice * item.Quantity;

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        INSERT INTO dbo.OrderItems (
            OrderId, ProductId, ProductName, SKU,
            UnitPrice, Quantity, LineTotal
        )
        VALUES (
            @OrderId, @ProductId, @ProductName, @SKU,
            @UnitPrice, @Quantity, @LineTotal
        );

        SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
        """;

        return await connection.ExecuteScalarAsync<long>(sql, item);
    }

    /// <summary>
    /// Checks whether a user has purchased and received a specific product.
    /// Returns true only if a matching order has Delivered status.
    /// </summary>
    public async Task<bool> HasUserPurchasedProductAsync(long userId,long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CAST(
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.OrderItems oi
                    INNER JOIN dbo.Orders o ON oi.OrderId = o.OrderId
                    WHERE o.UserId = @UserId
                      AND oi.ProductId = @ProductId
                      AND o.Status = 'Delivered'
                )
                THEN 1 ELSE 0 END
            AS BIT);
            """;

        return await connection.ExecuteScalarAsync<bool>(
            sql,
            new { UserId = userId, ProductId = productId });
    }
}
