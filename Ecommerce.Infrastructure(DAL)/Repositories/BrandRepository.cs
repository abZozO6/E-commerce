using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace Ecommerce.Infrastructure_DAL_.Repositories
{
    public class BrandRepository : IBrandRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public BrandRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<Brand>> GetAllActiveAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            SELECT BrandId, Name, Slug, LogoUrl, IsActive
            FROM dbo.Brands
            WHERE IsActive = 1
            ORDER BY Name ASC";

            return await connection.QueryAsync<Brand>(sql);
        }

        public async Task<IEnumerable<Brand>> GetAllAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            SELECT BrandId, Name, Slug, LogoUrl, IsActive
            FROM dbo.Brands
            ORDER BY Name ASC";

            return await connection.QueryAsync<Brand>(sql);
        }

        public async Task<Brand?> GetByIdAsync(long brandId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            SELECT BrandId, Name, Slug, LogoUrl, IsActive
            FROM dbo.Brands
            WHERE BrandId = @BrandId";

            return await connection.QueryFirstOrDefaultAsync<Brand>(sql, new { BrandId = brandId });
        }

        public async Task<Brand?> GetBySlugAsync(string slug)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            SELECT BrandId, Name, Slug, LogoUrl, IsActive
            FROM dbo.Brands
            WHERE Slug = @Slug AND IsActive = 1";

            return await connection.QueryFirstOrDefaultAsync<Brand>(sql, new { Slug = slug });
        }

        public async Task<bool> ExistsByNameOrSlugAsync(string name, string slug, long? excludeBrandId = null)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.Brands
                WHERE (Name = @Name OR Slug = @Slug)
                  AND (@ExcludeId IS NULL OR BrandId <> @ExcludeId)
            ) THEN 1 ELSE 0 END";

            return await connection.ExecuteScalarAsync<bool>(sql, new
            {
                Name = name,
                Slug = slug,
                ExcludeId = excludeBrandId
            });
        }

        public async Task<long> CreateAsync(Brand brand)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            INSERT INTO dbo.Brands (Name, Slug, LogoUrl, IsActive)
            VALUES (@Name, @Slug, @LogoUrl, @IsActive);
            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";

            return await connection.ExecuteScalarAsync<long>(sql, brand);
        }

        public async Task<bool> UpdateAsync(Brand brand)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"
            UPDATE dbo.Brands
            SET Name = @Name,
                Slug = @Slug,
                LogoUrl = @LogoUrl,
                IsActive = @IsActive
            WHERE BrandId = @BrandId";

            int rowsAffected = await connection.ExecuteAsync(sql, brand);
            return rowsAffected > 0;
        }

        public async Task<bool> DeleteAsync(long brandId)
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = "DELETE FROM dbo.Brands WHERE BrandId = @BrandId";

            int rowsAffected = await connection.ExecuteAsync(sql, new { BrandId = brandId });
            return rowsAffected > 0;
        }
    }
}
