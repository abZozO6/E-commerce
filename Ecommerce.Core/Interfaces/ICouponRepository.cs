using System.Collections.Generic;
using System.Threading.Tasks;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces;

public interface ICouponRepository
{
    Task<Coupon?> GetByIdAsync(long couponId);

    Task<Coupon?> GetByCodeAsync(string code);

    Task<IEnumerable<Coupon>> GetAllAsync();

    Task<int> GetUsageCountAsync(long couponId);

    Task<bool> ExistsByCodeAsync(string code, long? excludeCouponId = null);

    Task<long> CreateAsync(Coupon coupon);

    Task<bool> UpdateAsync(Coupon coupon);

    Task<bool> DeleteAsync(long couponId);
}
