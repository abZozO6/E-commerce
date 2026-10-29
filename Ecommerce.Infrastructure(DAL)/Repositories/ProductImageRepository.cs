using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class ProductImageRepository : IProductImageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductImageRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ProductImage?> GetByIdAsync(long imageId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductImageId, ProductId, ImageUrl, AltText, DisplayOrder, IsPrimary
            FROM dbo.ProductImages
            WHERE ProductImageId = @ProductImageId";

        return await connection.QuerySingleOrDefaultAsync<ProductImage>(
            sql,
            new { ProductImageId = imageId });
    }

    public async Task<IEnumerable<ProductImage>> GetByProductIdAsync(long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductImageId, ProductId, ImageUrl, AltText, DisplayOrder, IsPrimary
            FROM dbo.ProductImages
            WHERE ProductId = @ProductId
            ORDER BY IsPrimary DESC, DisplayOrder ASC, ProductImageId ASC";

        return await connection.QueryAsync<ProductImage>(
            sql,
            new { ProductId = productId });
    }

    public async Task<ProductImage?> GetMainImageByProductIdAsync(long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT ProductImageId, ProductId, ImageUrl, AltText, DisplayOrder, IsPrimary
            FROM dbo.ProductImages
            WHERE ProductId = @ProductId AND IsPrimary = 1";

        return await connection.QuerySingleOrDefaultAsync<ProductImage>(
            sql,
            new { ProductId = productId });
    }

    public async Task<long> CreateAsync(ProductImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.ProductId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(image.ProductId),
                "Product ID must be positive.");
        }

        if (string.IsNullOrWhiteSpace(image.ImageUrl))
        {
            throw new ArgumentException(
                "Image URL is required.",
                nameof(image));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string insertSql = @"
            INSERT INTO dbo.ProductImages
                (ProductId, ImageUrl, AltText, DisplayOrder, IsPrimary)
            VALUES
                (@ProductId, @ImageUrl, @AltText, @DisplayOrder, @IsPrimary);

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

        if (!image.IsPrimary)
        {
            return await connection.ExecuteScalarAsync<long>(
                insertSql,
                image);
        }

        using var transaction =
            connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string clearPrimarySql = @"
                UPDATE dbo.ProductImages
                SET IsPrimary = 0
                WHERE ProductId = @ProductId AND IsPrimary = 1;";

            await connection.ExecuteAsync(
                clearPrimarySql,
                new { image.ProductId },
                transaction);

            long imageId = await connection.ExecuteScalarAsync<long>(
                insertSql,
                image,
                transaction);

            transaction.Commit();

            return imageId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> SetMainImageAsync(long imageId,long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        using var transaction =
            connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string verifySql = @"
                SELECT ProductImageId
                FROM dbo.ProductImages WITH (UPDLOCK, HOLDLOCK)
                WHERE ProductImageId = @ProductImageId
                  AND ProductId = @ProductId;";

            var existingImage =
                await connection.QuerySingleOrDefaultAsync<long?>(
                    verifySql,
                    new
                    {
                        ProductImageId = imageId,
                        ProductId = productId
                    },
                    transaction);

            if (existingImage is null)
            {
                transaction.Rollback();
                return false;
            }

            const string clearPrimarySql = @"
                UPDATE dbo.ProductImages
                SET IsPrimary = 0
                WHERE ProductId = @ProductId AND IsPrimary = 1;";

            await connection.ExecuteAsync(
                clearPrimarySql,
                new { ProductId = productId },
                transaction);

            const string setPrimarySql = @"
                UPDATE dbo.ProductImages
                SET IsPrimary = 1
                WHERE ProductImageId = @ProductImageId
                  AND ProductId = @ProductId;";

            int rowsAffected = await connection.ExecuteAsync(
                setPrimarySql,
                new
                {
                    ProductImageId = imageId,
                    ProductId = productId
                },
                transaction);

            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return false;
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

    public async Task<bool> UpdateAsync(ProductImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.ProductImageId <= 0 || image.ProductId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(image),
                "Image ID and product ID must be positive.");
        }

        if (string.IsNullOrWhiteSpace(image.ImageUrl))
        {
            throw new ArgumentException(
                "Image URL is required.",
                nameof(image));
        }

        using var connection = _connectionFactory.CreateConnection();

        const string updateSql = @"
            UPDATE dbo.ProductImages
            SET ImageUrl = @ImageUrl,
                AltText = @AltText,
                DisplayOrder = @DisplayOrder,
                IsPrimary = @IsPrimary
            WHERE ProductImageId = @ProductImageId
              AND ProductId = @ProductId;";

        if (!image.IsPrimary)
        {
            int rowsAffected = await connection.ExecuteAsync(
                updateSql,
                image);

            return rowsAffected > 0;
        }

        using var transaction =
            connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string verifySql = @"
                SELECT ProductImageId
                FROM dbo.ProductImages WITH (UPDLOCK, HOLDLOCK)
                WHERE ProductImageId = @ProductImageId
                  AND ProductId = @ProductId;";

            var existingImage =
                await connection.QuerySingleOrDefaultAsync<long?>(
                    verifySql,
                    new
                    {
                        image.ProductImageId,
                        image.ProductId
                    },
                    transaction);

            if (existingImage is null)
            {
                transaction.Rollback();
                return false;
            }

            const string clearPrimarySql = @"
                UPDATE dbo.ProductImages
                SET IsPrimary = 0
                WHERE ProductId = @ProductId
                  AND IsPrimary = 1
                  AND ProductImageId <> @ProductImageId;";

            await connection.ExecuteAsync(
                clearPrimarySql,
                new
                {
                    image.ProductId,
                    image.ProductImageId
                },
                transaction);

            int rowsAffected = await connection.ExecuteAsync(
                updateSql,
                image,
                transaction);

            if (rowsAffected == 0)
            {
                transaction.Rollback();
                return false;
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

    public async Task<bool> DeleteAsync(long imageId, long productId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            DELETE FROM dbo.ProductImages
            WHERE ProductImageId = @ProductImageId
              AND ProductId = @ProductId;";

        int rowsAffected = await connection.ExecuteAsync(
            sql,
            new
            {
                ProductImageId = imageId,
                ProductId = productId
            });

        return rowsAffected > 0;
    }
}
