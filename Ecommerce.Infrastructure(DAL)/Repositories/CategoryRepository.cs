using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CategoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<Category>> GetAllActiveAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CategoryId, Name, Slug, ParentCategoryId, IsActive
            FROM dbo.Categories
            WHERE IsActive = 1
            ORDER BY Name ASC;
            """;

        return await connection.QueryAsync<Category>(sql);
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CategoryId, Name, Slug, ParentCategoryId, IsActive
            FROM dbo.Categories
            ORDER BY ParentCategoryId ASC, Name ASC;
            """;

        return await connection.QueryAsync<Category>(sql);
    }

    public async Task<IEnumerable<Category>> GetRootCategoriesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CategoryId, Name, Slug, ParentCategoryId, IsActive
            FROM dbo.Categories
            WHERE ParentCategoryId IS NULL
              AND IsActive = 1
            ORDER BY Name ASC;
            """;

        return await connection.QueryAsync<Category>(sql);
    }

    public async Task<IEnumerable<Category>> GetSubCategoriesAsync(long parentCategoryId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CategoryId, Name, Slug, ParentCategoryId, IsActive
            FROM dbo.Categories
            WHERE ParentCategoryId = @ParentCategoryId
              AND IsActive = 1
            ORDER BY Name ASC;
            """;

        return await connection.QueryAsync<Category>(
            sql,
            new { ParentCategoryId = parentCategoryId });
    }

    public async Task<Category?> GetByIdAsync(long categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CategoryId, Name, Slug, ParentCategoryId, IsActive
            FROM dbo.Categories
            WHERE CategoryId = @CategoryId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Category>(
            sql,
            new { CategoryId = categoryId });
    }

    public async Task<Category?> GetBySlugAsync(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CategoryId, Name, Slug, ParentCategoryId, IsActive
            FROM dbo.Categories
            WHERE Slug = @Slug
              AND IsActive = 1;
            """;

        return await connection.QuerySingleOrDefaultAsync<Category>(
            sql,
            new { Slug = slug });
    }

    public async Task<bool> ExistsBySlugAsync(string slug,long? excludeCategoryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CAST(
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.Categories
                    WHERE Slug = @Slug
                      AND (@ExcludeCategoryId IS NULL
                           OR CategoryId <> @ExcludeCategoryId)
                )
                THEN 1 ELSE 0 END
            AS BIT);
            """;

        return await connection.ExecuteScalarAsync<bool>(
            sql,
            new
            {
                Slug = slug,
                ExcludeCategoryId = excludeCategoryId
            });
    }

    public async Task<long> CreateAsync(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.Slug);

        if (category.ParentCategoryId == category.CategoryId &&
            category.CategoryId != 0)
        {
            throw new InvalidOperationException(
                "Category cannot be its own parent.");
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.Categories
                (Name, Slug, ParentCategoryId, IsActive)
            VALUES
                (@Name, @Slug, @ParentCategoryId, @IsActive);

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        return await connection.ExecuteScalarAsync<long>(sql, category);
    }

    public async Task<bool> UpdateAsync(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(category.Slug);

        if (category.ParentCategoryId == category.CategoryId)
        {
            throw new InvalidOperationException(
                "Category cannot be its own parent.");
        }

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Categories
            SET Name = @Name,
                Slug = @Slug,
                ParentCategoryId = @ParentCategoryId,
                IsActive = @IsActive
            WHERE CategoryId = @CategoryId;
            """;

        var rowsAffected = await connection.ExecuteAsync(sql, category);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(long categoryId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            DELETE FROM dbo.Categories
            WHERE CategoryId = @CategoryId;
            """;

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new { CategoryId = categoryId });

        return rowsAffected > 0;
    }
}
