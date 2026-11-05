using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AddressRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<Address?> GetByIdAsync(long addressId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(addressId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT AddressId, UserId, Label, City, Street,
                   Phone, BuildingDetails, IsDefault
            FROM dbo.Addresses
            WHERE AddressId = @AddressId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Address>(
            sql, new { AddressId = addressId });
    }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT AddressId, UserId, Label, City, Street,
                   Phone, BuildingDetails, IsDefault
            FROM dbo.Addresses
            WHERE UserId = @UserId
            ORDER BY IsDefault DESC, AddressId DESC;
            """;

        return await connection.QueryAsync<Address>(
            sql, new { UserId = userId });
    }

    public async Task<long> CreateAsync(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            DECLARE @HasAddresses BIT;

            SELECT @HasAddresses = CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.Addresses WITH (UPDLOCK, HOLDLOCK)
                WHERE UserId = @UserId
            )
            THEN 1 ELSE 0 END;

            IF (@IsDefault = 1 OR @HasAddresses = 0)
            BEGIN
                UPDATE dbo.Addresses
                SET IsDefault = 0
                WHERE UserId = @UserId
                  AND IsDefault = 1;
            END;

            INSERT INTO dbo.Addresses
            (
                UserId, Label, City, Street, Phone,
                BuildingDetails, IsDefault
            )
            VALUES
            (
                @UserId, @Label, @City, @Street, @Phone,
                @BuildingDetails,
                CASE WHEN @IsDefault = 1 OR @HasAddresses = 0
                     THEN 1 ELSE 0 END
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<long>(
                    new CommandDefinition(
                        sql,
                        address,
                        transaction: transaction,
                        cancellationToken: cancellationToken)));
    }

    public async Task<bool> UpdateAsync(Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Addresses
            SET Label = @Label,
                City = @City,
                Street = @Street,
                Phone = @Phone,
                BuildingDetails = @BuildingDetails
            WHERE AddressId = @AddressId
              AND UserId = @UserId;
            """;

        int rowsAffected = await connection.ExecuteAsync(sql, address);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(long addressId, long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(addressId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            DECLARE @WasDefault BIT;
            DECLARE @NextAddressId BIGINT;

            SELECT @WasDefault = IsDefault
            FROM dbo.Addresses WITH (UPDLOCK, HOLDLOCK)
            WHERE AddressId = @AddressId
              AND UserId = @UserId;

            IF @WasDefault IS NULL
            BEGIN
                SELECT 0;
                RETURN;
            END;

            DELETE FROM dbo.Addresses
            WHERE AddressId = @AddressId
              AND UserId = @UserId;

            IF @WasDefault = 1
            BEGIN
                SELECT TOP 1 @NextAddressId = AddressId
                FROM dbo.Addresses
                WHERE UserId = @UserId
                ORDER BY AddressId DESC;

                IF @NextAddressId IS NOT NULL
                BEGIN
                    UPDATE dbo.Addresses
                    SET IsDefault = 1
                    WHERE AddressId = @NextAddressId
                      AND UserId = @UserId;
                END;
            END;

            SELECT 1;
            """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sql,
                        new { AddressId = addressId, UserId = userId },
                        transaction: transaction,
                        cancellationToken: cancellationToken))) == 1;
    }

    public async Task<bool> SetDefaultAddressAsync(long addressId,long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(addressId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.Addresses WITH (UPDLOCK, HOLDLOCK)
                WHERE AddressId = @AddressId
                  AND UserId = @UserId
            )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            UPDATE dbo.Addresses
            SET IsDefault = 0
            WHERE UserId = @UserId
              AND IsDefault = 1;

            UPDATE dbo.Addresses
            SET IsDefault = 1
            WHERE AddressId = @AddressId
              AND UserId = @UserId;

            SELECT 1;
            """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            (db, transaction, cancellationToken) =>
                db.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        sql,
                        new { AddressId = addressId, UserId = userId },
                        transaction: transaction,
                        cancellationToken: cancellationToken))) == 1;
    }
}