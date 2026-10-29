using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class ProductSpecificationRepository : IProductSpecificationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductSpecificationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory
            ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ProductSpecification?> GetByIdAsync(long specId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(specId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                SpecificationId,
                ProductId,
                SpecName,
                SpecValue
            FROM dbo.ProductSpecifications
            WHERE SpecificationId = @SpecId;";

        return await connection.QuerySingleOrDefaultAsync<ProductSpecification>(
            sql,
            new { SpecId = specId });
    }

    public async Task<IEnumerable<ProductSpecification>> GetByProductIdAsync(long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT
                SpecificationId,
                ProductId,
                SpecName,
                SpecValue
            FROM dbo.ProductSpecifications
            WHERE ProductId = @ProductId
            ORDER BY SpecificationId ASC;";

        return await connection.QueryAsync<ProductSpecification>(
            sql,
            new { ProductId = productId });
    }

    /// <summary>
    /// Inserts a specification if it does not exist for the product,
    /// or updates its value if it already exists.
    /// Returns the identifier of the inserted or updated specification.
    /// </summary>
    public async Task<long> UpsertAsync(ProductSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ValidateSpecification(spec);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction =
            connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            // Lock the matching key range to reduce concurrent insert conflicts.
            const string findSql = @"
                SELECT SpecificationId
                FROM dbo.ProductSpecifications WITH (UPDLOCK, HOLDLOCK)
                WHERE ProductId = @ProductId
                  AND SpecName = @SpecName;";

            long? existingSpecId =
                await connection.QuerySingleOrDefaultAsync<long?>(
                    findSql,
                    new
                    {
                        spec.ProductId,
                        spec.SpecName
                    },
                    transaction);

            long resultId;

            if (existingSpecId.HasValue)
            {
                const string updateSql = @"
                    UPDATE dbo.ProductSpecifications
                    SET SpecValue = @SpecValue
                    WHERE SpecificationId = @SpecificationId;";

                await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        SpecificationId = existingSpecId.Value,
                        spec.SpecValue
                    },
                    transaction);

                resultId = existingSpecId.Value;
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO dbo.ProductSpecifications
                        (ProductId, SpecName, SpecValue)
                    VALUES
                        (@ProductId, @SpecName, @SpecValue);

                    SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

                resultId = await connection.ExecuteScalarAsync<long>(
                    insertSql,
                    spec,
                    transaction);
            }

            transaction.Commit();

            return resultId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Replaces all specifications of a product with the supplied collection.
    /// All deletions and insertions are committed together or rolled back
    /// together if an operation fails.
    /// An empty collection removes all existing specifications.
    /// </summary>
    public async Task<bool> ReplaceProductSpecificationsAsync(
        long productId,
        IEnumerable<ProductSpecification> specifications)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentNullException.ThrowIfNull(specifications);

        var specificationList = specifications.ToList();

        foreach (var spec in specificationList)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ValidateSpecification(spec, validateProductId: false);
        }

        // Reject duplicate names before modifying the database.
        var hasDuplicateNames = specificationList
            .GroupBy(spec => spec.SpecName, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (hasDuplicateNames)
        {
            throw new ArgumentException(
                "Specification names must be unique within a product.",
                nameof(specifications));
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            const string deleteSql = @"
                DELETE FROM dbo.ProductSpecifications
                WHERE ProductId = @ProductId;";

            await connection.ExecuteAsync(
                deleteSql,
                new { ProductId = productId },
                transaction);

            const string insertSql = @"
                INSERT INTO dbo.ProductSpecifications
                    (ProductId, SpecName, SpecValue)
                VALUES
                    (@ProductId, @SpecName, @SpecValue);";

            foreach (var spec in specificationList)
            {
                await connection.ExecuteAsync(
                    insertSql,
                    new
                    {
                        ProductId = productId,
                        spec.SpecName,
                        spec.SpecValue
                    },
                    transaction);
            }

            transaction.Commit();

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Updates the name and value of an existing specification.
    /// The specification must belong to the specified product.
    /// Returns false when no matching record exists.
    /// </summary>
    public async Task<bool> UpdateAsync(ProductSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            spec.SpecificationId);

        ValidateSpecification(spec);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.ProductSpecifications
            SET SpecName = @SpecName,
                SpecValue = @SpecValue
            WHERE SpecificationId = @SpecificationId
              AND ProductId = @ProductId;";

        int rowsAffected = await connection.ExecuteAsync(sql, spec);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Deletes a specification only when its identifier and product identifier
    /// both match. Returns false when no matching record exists.
    /// </summary>
    public async Task<bool> DeleteAsync(long specId, long productId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(specId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM dbo.ProductSpecifications
            WHERE SpecificationId = @SpecId
              AND ProductId = @ProductId;";

        int rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                SpecId = specId,
                ProductId = productId
            });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Validates the required specification fields and their values.
    /// </summary>
    private static void ValidateSpecification(ProductSpecification spec,bool validateProductId = true)
    {
        if (validateProductId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                spec.ProductId);
        }

        if (string.IsNullOrWhiteSpace(spec.SpecName))
        {
            throw new ArgumentException(
                "Specification name cannot be empty.",
                nameof(spec.SpecName));
        }

        if (string.IsNullOrWhiteSpace(spec.SpecValue))
        {
            throw new ArgumentException(
                "Specification value cannot be empty.",
                nameof(spec.SpecValue));
        }
    }
}
