using Dapper;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;
using Ecommerce.Infrastructure_DAL_.Data;

namespace Ecommerce.Infrastructure.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CouponRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Coupon?> GetByIdAsync(long couponId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CouponId, Code, DiscountType, DiscountValue, MinOrderAmount,
                   MaxDiscountAmount, StartsAt, ExpiresAt, UsageLimit, IsActive
            FROM dbo.Coupons
            WHERE CouponId = @CouponId;
            """;

        return await connection.QuerySingleOrDefaultAsync<Coupon>(
            sql,
            new { CouponId = couponId });
    }

    public async Task<Coupon?> GetByCodeAsync(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CouponId, Code, DiscountType, DiscountValue, MinOrderAmount,
                   MaxDiscountAmount, StartsAt, ExpiresAt, UsageLimit, IsActive
            FROM dbo.Coupons
            WHERE Code = @Code;
            """;

        return await connection.QuerySingleOrDefaultAsync<Coupon>(
            sql,
            new { Code = code.Trim() });
    }

    public async Task<IEnumerable<Coupon>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CouponId, Code, DiscountType, DiscountValue, MinOrderAmount,
                   MaxDiscountAmount, StartsAt, ExpiresAt, UsageLimit, IsActive
            FROM dbo.Coupons
            ORDER BY CouponId DESC;
            """;

        return await connection.QueryAsync<Coupon>(sql);
    }

    public async Task<int> GetUsageCountAsync(long couponId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.Orders
            WHERE CouponId = @CouponId
              AND Status <> 'Cancelled';
            """;

        return await connection.ExecuteScalarAsync<int>(
            sql,
            new { CouponId = couponId });
    }

    public async Task<bool> ExistsByCodeAsync(string code,long? excludeCouponId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            SELECT CAST(
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.Coupons
                    WHERE Code = @Code
                      AND (@ExcludeCouponId IS NULL
                           OR CouponId <> @ExcludeCouponId)
                )
                THEN 1 ELSE 0 END
            AS BIT);
            """;

        return await connection.ExecuteScalarAsync<bool>(
            sql,
            new
            {
                Code = code.Trim(),
                ExcludeCouponId = excludeCouponId
            });
    }

    public async Task<long> CreateAsync(Coupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentException.ThrowIfNullOrWhiteSpace(coupon.Code);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            INSERT INTO dbo.Coupons (
                Code, DiscountType, DiscountValue, MinOrderAmount,
                MaxDiscountAmount, StartsAt, ExpiresAt, UsageLimit, IsActive
            )
            VALUES (
                @Code, @DiscountType, @DiscountValue, @MinOrderAmount,
                @MaxDiscountAmount, @StartsAt, @ExpiresAt, @UsageLimit, @IsActive
            );

            SELECT CAST(SCOPE_IDENTITY() AS BIGINT);
            """;

        coupon.Code = coupon.Code.Trim();

        return await connection.ExecuteScalarAsync<long>(sql, coupon);
    }

    public async Task<bool> UpdateAsync(Coupon coupon)
    {
        ArgumentNullException.ThrowIfNull(coupon);
        ArgumentException.ThrowIfNullOrWhiteSpace(coupon.Code);

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            UPDATE dbo.Coupons
            SET Code = @Code,
                DiscountType = @DiscountType,
                DiscountValue = @DiscountValue,
                MinOrderAmount = @MinOrderAmount,
                MaxDiscountAmount = @MaxDiscountAmount,
                StartsAt = @StartsAt,
                ExpiresAt = @ExpiresAt,
                UsageLimit = @UsageLimit,
                IsActive = @IsActive
            WHERE CouponId = @CouponId;
            """;

        coupon.Code = coupon.Code.Trim();

        var rowsAffected = await connection.ExecuteAsync(sql, coupon);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(long couponId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
            DELETE FROM dbo.Coupons
            WHERE CouponId = @CouponId;
            """;

        var rowsAffected = await connection.ExecuteAsync(
            sql,
            new { CouponId = couponId });

        return rowsAffected > 0;
    }
}
