using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

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
                SpecValue,
                DisplayOrder
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
                SpecValue,
                DisplayOrder
            FROM dbo.ProductSpecifications
            WHERE ProductId = @ProductId
            ORDER BY DisplayOrder ASC, SpecificationId ASC;";

        return await connection.QueryAsync<ProductSpecification>(
            sql,
            new { ProductId = productId });
    }

    /// <summary>
    /// Inserts a specification or updates the existing specification
    /// with the same name for the product.
    /// </summary>
    public async Task<long> UpsertAsync(ProductSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        NormalizeAndValidate(spec);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction =
            connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
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
                    SET SpecValue = @SpecValue,
                        DisplayOrder = @DisplayOrder
                    WHERE SpecificationId = @SpecificationId;";

                await connection.ExecuteAsync(
                    updateSql,
                    new
                    {
                        SpecificationId = existingSpecId.Value,
                        spec.SpecValue,
                        spec.DisplayOrder
                    },
                    transaction);

                resultId = existingSpecId.Value;
            }
            else
            {
                const string insertSql = @"
                    INSERT INTO dbo.ProductSpecifications
                    (
                        ProductId,
                        SpecName,
                        SpecValue,
                        DisplayOrder
                    )
                    VALUES
                    (
                        @ProductId,
                        @SpecName,
                        @SpecValue,
                        @DisplayOrder
                    );

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
    /// Replaces all specifications belonging to a product.
    /// The operation is atomic.
    /// </summary>
    public async Task<bool> ReplaceProductSpecificationsAsync(long productId,IEnumerable<ProductSpecification> specifications)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentNullException.ThrowIfNull(specifications);

        var specificationList = specifications.ToList();

        foreach (var spec in specificationList)
        {
            ArgumentNullException.ThrowIfNull(spec);

            spec.ProductId = productId;
            NormalizeAndValidate(spec);
        }

        var hasDuplicateNames = specificationList
            .GroupBy(
                spec => spec.SpecName,
                StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (hasDuplicateNames)
        {
            throw new ArgumentException(
                "Specification names must be unique within a product.",
                nameof(specifications));
        }

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction =
            connection.BeginTransaction(IsolationLevel.Serializable);

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
                (
                    ProductId,
                    SpecName,
                    SpecValue,
                    DisplayOrder
                )
                VALUES
                (
                    @ProductId,
                    @SpecName,
                    @SpecValue,
                    @DisplayOrder
                );";

            foreach (var spec in specificationList)
            {
                await connection.ExecuteAsync(
                    insertSql,
                    spec,
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
    /// Updates an existing specification belonging to a product.
    /// </summary>
    public async Task<bool> UpdateAsync(ProductSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            spec.SpecificationId);

        NormalizeAndValidate(spec);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE dbo.ProductSpecifications
            SET SpecName = @SpecName,
                SpecValue = @SpecValue,
                DisplayOrder = @DisplayOrder
            WHERE SpecificationId = @SpecificationId
              AND ProductId = @ProductId;";

        int rowsAffected = await connection.ExecuteAsync(sql, spec);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Deletes a specification only when it belongs to the specified product.
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

    private static void NormalizeAndValidate(ProductSpecification spec)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            spec.ProductId);

        if (string.IsNullOrWhiteSpace(spec.SpecName))
        {
            throw new ArgumentException(
                "Specification name is required.",
                nameof(spec.SpecName));
        }

        if (string.IsNullOrWhiteSpace(spec.SpecValue))
        {
            throw new ArgumentException(
                "Specification value is required.",
                nameof(spec.SpecValue));
        }

        spec.SpecName = spec.SpecName.Trim();
        spec.SpecValue = spec.SpecValue.Trim();

        if (spec.SpecName.Length > 100)
        {
            throw new ArgumentException(
                "Specification name cannot exceed 100 characters.");
        }

        if (spec.SpecValue.Length > 500)
        {
            throw new ArgumentException(
                "Specification value cannot exceed 500 characters.");
        }

        if (spec.DisplayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spec.DisplayOrder),
                "Display order cannot be negative.");
        }
    }
}