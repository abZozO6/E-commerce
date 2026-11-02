
using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure_DAL_.Repositories;

public class BrandRepository : IBrandRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BrandRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>
    /// Retrieves all active, non-deleted brands.
    /// </summary>
    public async Task<IEnumerable<Brand>> GetAllActiveAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT BrandId, Name, Slug, LogoUrl, IsActive, IsDeleted
            FROM dbo.Brands
            WHERE IsActive = 1
              AND IsDeleted = 0
            ORDER BY Name ASC;
            """;

        return await connection.QueryAsync<Brand>(sql);
    }

    /// <summary>
    /// Retrieves all non-deleted brands, including inactive brands.
    /// </summary>
    public async Task<IEnumerable<Brand>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT BrandId, Name, Slug, LogoUrl, IsActive, IsDeleted
            FROM dbo.Brands
            WHERE IsDeleted = 0
            ORDER BY Name ASC;
            """;

        return await connection.QueryAsync<Brand>(sql);
    }

    /// <summary>
    /// Retrieves a non-deleted brand by its identifier.
    /// Returns null if the brand does not exist.
    /// </summary>
    public async Task<Brand?> GetByIdAsync(long brandId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT BrandId, Name, Slug, LogoUrl, IsActive, IsDeleted
            FROM dbo.Brands
            WHERE BrandId = @BrandId
              AND IsDeleted = 0;
            """;

        return await connection.QuerySingleOrDefaultAsync<Brand>(
            sql,
            new { BrandId = brandId });
    }

    /// <summary>
    /// Retrieves an active, non-deleted brand by its slug.
    /// Returns null if the brand does not exist or is inactive.
    /// </summary>
    public async Task<Brand?> GetBySlugAsync(string slug)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT BrandId, Name, Slug, LogoUrl, IsActive, IsDeleted
            FROM dbo.Brands
            WHERE Slug = @Slug
              AND IsActive = 1
              AND IsDeleted = 0;
            """;

        return await connection.QuerySingleOrDefaultAsync<Brand>(
            sql,
            new { Slug = slug });
    }

    /// <summary>
    /// Checks whether a non-deleted brand already uses the specified name or slug.
    /// An optional brand identifier can be excluded during updates.
    /// </summary>
    public async Task<bool> ExistsByNameOrSlugAsync(
        string name,
        string slug,
        long? excludeBrandId = null)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.Brands
                WHERE (Name = @Name OR Slug = @Slug)
                  AND IsDeleted = 0
                  AND (@ExcludeId IS NULL OR BrandId <> @ExcludeId)
            )
            THEN 1 ELSE 0 END;
            """;

        return await connection.ExecuteScalarAsync<bool>(
            sql,
            new
            {
                Name = name,
                Slug = slug,
                ExcludeId = excludeBrandId
            });
    }

    /// <summary>
    /// Creates a new brand and returns its generated identifier.
    /// </summary>
    public async Task<long> CreateAsync(Brand brand)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.Brands
                (Name, Slug, LogoUrl, IsActive)
            VALUES
                (@Name, @Slug, @LogoUrl, @IsActive);

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, brand);
    }

    /// <summary>
    /// Updates an existing non-deleted brand.
    /// Returns true if a row was updated.
    /// </summary>
    public async Task<bool> UpdateAsync(Brand brand)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Brands
            SET Name = @Name,
                Slug = @Slug,
                LogoUrl = @LogoUrl,
                IsActive = @IsActive
            WHERE BrandId = @BrandId
              AND IsDeleted = 0;
            """;

        int rowsAffected = await connection.ExecuteAsync(sql, brand);

        return rowsAffected > 0;
    }

    /// <summary>
    /// Soft deletes a brand by marking it as deleted.
    /// Returns true if the brand was successfully deleted.
    /// </summary>
    public async Task<bool> DeleteAsync(long brandId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Brands
            SET IsDeleted = 1
            WHERE BrandId = @BrandId
              AND IsDeleted = 0;
            """;

        int rowsAffected = await connection.ExecuteAsync(
            sql,
            new { BrandId = brandId });

        return rowsAffected > 0;
    }
}
