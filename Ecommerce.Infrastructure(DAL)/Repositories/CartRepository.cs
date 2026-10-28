using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data.Common;

namespace Ecommerce.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CartRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Cart?> GetByIdAsync(long cartId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CartId, UserId, CreatedAt, UpdatedAt
            FROM dbo.Carts
            WHERE CartId = @CartId;
            """;

        return await connection.QueryFirstOrDefaultAsync<Cart>(
            sql,
            new { CartId = cartId });
    }

    public async Task<Cart?> GetByUserIdAsync(long userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CartId, UserId, CreatedAt, UpdatedAt
            FROM dbo.Carts
            WHERE UserId = @UserId;
            """;

        return await connection.QueryFirstOrDefaultAsync<Cart>(
            sql,
            new { UserId = userId });
    }

    public async Task<Cart> GetOrCreateByUserIdAsync(long userId)
    {
        using var connection = _connectionFactory.CreateConnection();

    // UPDLOCK prevents conflicting operations while checking whether the cart exists.
   //  HOLDLOCK keeps the locks until the transaction ends, helping prevent duplicate cart creation.
        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.Carts WITH (UPDLOCK, HOLDLOCK)
                WHERE UserId = @UserId
            )
            BEGIN
                INSERT INTO dbo.Carts (UserId)
                VALUES (@UserId);
            END;

            SELECT CartId, UserId, CreatedAt, UpdatedAt
            FROM dbo.Carts
            WHERE UserId = @UserId;
            """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            async (conn, transaction, cancellationToken) =>
            {
                var command = new CommandDefinition(
                    sql,
                    new { UserId = userId },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                return await conn.QuerySingleAsync<Cart>(command);
            });
    }

    public async Task<bool> TouchUpdatedAtAsync(long cartId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Carts
            SET UpdatedAt = SYSUTCDATETIME()
            WHERE CartId = @CartId;
            """;

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new { CartId = cartId });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteByUserIdAsync(long userId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.Carts
                WHERE UserId = @UserId
            )
            BEGIN
                SELECT CAST(0 AS BIT);
                RETURN;
            END;

            DELETE FROM dbo.CartItems
            WHERE CartId IN (
                SELECT CartId
                FROM dbo.Carts
                WHERE UserId = @UserId
            );

            DELETE FROM dbo.Carts
            WHERE UserId = @UserId;

            SELECT CAST(1 AS BIT);
            """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            async (conn, transaction, cancellationToken) =>
            {
                var command = new CommandDefinition(
                    sql,
                    new { UserId = userId },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                return await conn.ExecuteScalarAsync<bool>(command);
            });
    }
}
