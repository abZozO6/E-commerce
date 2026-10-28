using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class CartItemRepository : ICartItemRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CartItemRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CartItem?> GetByIdAsync(long cartItemId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CartItemId, CartId, ProductId, Quantity, AddedAt
            FROM dbo.CartItems
            WHERE CartItemId = @CartItemId";

        return await connection.QueryFirstOrDefaultAsync<CartItem>(sql, new { CartItemId = cartItemId });
    }

    public async Task<CartItem?> GetByCartAndProductAsync(long cartId, long productId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CartItemId, CartId, ProductId, Quantity, AddedAt
            FROM dbo.CartItems
            WHERE CartId = @CartId AND ProductId = @ProductId";

        return await connection.QueryFirstOrDefaultAsync<CartItem>(sql, new { CartId = cartId, ProductId = productId });
    }

    public async Task<IEnumerable<CartItem>> GetByCartIdAsync(long cartId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT CartItemId, CartId, ProductId, Quantity, AddedAt
            FROM dbo.CartItems
            WHERE CartId = @CartId
            ORDER BY AddedAt DESC";

        return await connection.QueryAsync<CartItem>(sql, new { CartId = cartId });
    }

    public async Task<long> AddOrIncrementAsync(CartItem item)
    {
        if (item.Quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(item.Quantity), "Quantity must be greater than zero.");

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        DECLARE @CartItemId BIGINT;

        SELECT @CartItemId = CartItemId
        FROM dbo.CartItems WITH (UPDLOCK, HOLDLOCK)
        WHERE CartId = @CartId AND ProductId = @ProductId;

        IF @CartItemId IS NOT NULL
        BEGIN
            UPDATE dbo.CartItems
            SET Quantity = Quantity + @Quantity
            WHERE CartItemId = @CartItemId;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.CartItems (CartId, ProductId, Quantity)
            VALUES (@CartId, @ProductId, @Quantity);

            SET @CartItemId = CAST(SCOPE_IDENTITY() AS BIGINT);
        END;

        UPDATE dbo.Carts
        SET UpdatedAt = SYSUTCDATETIME()
        WHERE CartId = @CartId;

        SELECT @CartItemId;
        """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<long>(
                    new CommandDefinition(
                        sql,
                        item,
                        transaction: transaction,
                        cancellationToken: cancellationToken)));
    }

    public async Task<bool> UpdateQuantityAsync(long cartItemId,long cartId,int newQuantity)
    {
        if (newQuantity <= 0)
            return false;

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        UPDATE dbo.CartItems
        SET Quantity = @Quantity
        WHERE CartItemId = @CartItemId
          AND CartId = @CartId;

        IF @@ROWCOUNT = 0
            SELECT CAST(0 AS BIT);
        ELSE
        BEGIN
            UPDATE dbo.Carts
            SET UpdatedAt = SYSUTCDATETIME()
            WHERE CartId = @CartId;

            SELECT CAST(1 AS BIT);
        END;
        """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            CartItemId = cartItemId,
                            CartId = cartId,
                            Quantity = newQuantity
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken)));
    }

    public async Task<bool> DeleteAsync(long cartItemId, long cartId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        DELETE FROM dbo.CartItems
        WHERE CartItemId = @CartItemId
          AND CartId = @CartId;

        IF @@ROWCOUNT = 0
            SELECT CAST(0 AS BIT);
        ELSE
        BEGIN
            UPDATE dbo.Carts
            SET UpdatedAt = SYSUTCDATETIME()
            WHERE CartId = @CartId;

            SELECT CAST(1 AS BIT);
        END;
        """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        sql,
                        new { CartItemId = cartItemId, CartId = cartId },
                        transaction: transaction,
                        cancellationToken: cancellationToken)));
    }

    public async Task<bool> ClearCartAsync(long cartId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.Carts
            WHERE CartId = @CartId
        )
        BEGIN
            SELECT CAST(0 AS BIT);
            RETURN;
        END;

        DELETE FROM dbo.CartItems
        WHERE CartId = @CartId;

        UPDATE dbo.Carts
        SET UpdatedAt = SYSUTCDATETIME()
        WHERE CartId = @CartId;

        SELECT CAST(1 AS BIT);
        """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<bool>(
                    new CommandDefinition(
                        sql,
                        new { CartId = cartId },
                        transaction: transaction,
                        cancellationToken: cancellationToken)));
    }

}