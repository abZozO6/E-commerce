using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data.Common;

namespace Ecommerce.Infrastructure.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AddressRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Address?> GetByIdAsync(long addressId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT AddressId, UserId, Label, City, Street, Phone, BuildingDetails, IsDefault
            FROM dbo.Addresses
            WHERE AddressId = @AddressId";

        return await connection.QueryFirstOrDefaultAsync<Address>(sql, new { AddressId = addressId });
    }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(long userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT AddressId, UserId, Label, City, Street, Phone, BuildingDetails, IsDefault
            FROM dbo.Addresses
            WHERE UserId = @UserId
            ORDER BY IsDefault DESC, AddressId DESC";

        return await connection.QueryAsync<Address>(sql, new { UserId = userId });
    }

    public async Task<long> CreateAsync(Address address)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        IF (@IsDefault = 1)
        BEGIN
            UPDATE dbo.Addresses
            SET IsDefault = 0
            WHERE UserId = @UserId
              AND IsDefault = 1;
        END;

        INSERT INTO dbo.Addresses
            (UserId, Label, City, Street, Phone,
             BuildingDetails, IsDefault)
        VALUES
            (@UserId, @Label, @City, @Street, @Phone,
             @BuildingDetails, @IsDefault);

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
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        UPDATE dbo.Addresses
        SET Label = @Label,
            City = @City,
            Street = @Street,
            Phone = @Phone,
            BuildingDetails = @BuildingDetails,
            IsDefault = @IsDefault
        WHERE AddressId = @AddressId
          AND UserId = @UserId;

        IF @@ROWCOUNT = 0
            RETURN;

        IF (@IsDefault = 1)
        BEGIN
            UPDATE dbo.Addresses
            SET IsDefault = 0
            WHERE UserId = @UserId
              AND IsDefault = 1
              AND AddressId <> @AddressId;
        END;
        """;

        return await DbTransactionHelper.ExecuteAsync(
            connection,
            async (db, transaction, cancellationToken) =>
            {
                var rowsAffected = await db.ExecuteAsync(
                    new CommandDefinition(
                        sql,
                        address,
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                return rowsAffected > 0;
            });
    }

    public async Task<bool> DeleteAsync(long addressId, long userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM dbo.Addresses WHERE AddressId = @AddressId AND UserId = @UserId";

        int rowsAffected = await connection.ExecuteAsync(sql, new { AddressId = addressId, UserId = userId });
        return rowsAffected > 0;
    }


    public async Task<bool> SetDefaultAddressAsync(long addressId, long userId)
    { // زهقان اعدلها 
        using var connection = _connectionFactory.CreateConnection();

        await ((DbConnection)connection).OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string checkSql = """
            SELECT COUNT(1)
            FROM dbo.Addresses
            WHERE AddressId = @AddressId
              AND UserId = @UserId;
            """;

            var exists = await connection.ExecuteScalarAsync<int>(
                checkSql,
                new { AddressId = addressId, UserId = userId },
                transaction);

            if (exists == 0)
            {
                transaction.Rollback();
                return false;
            }

            const string updateSql = """
            UPDATE dbo.Addresses
            SET IsDefault = 0
            WHERE UserId = @UserId
              AND IsDefault = 1;

            UPDATE dbo.Addresses
            SET IsDefault = 1
            WHERE AddressId = @AddressId
              AND UserId = @UserId;
            """;

            await connection.ExecuteAsync(
                updateSql,
                new { AddressId = addressId, UserId = userId },
                transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

}