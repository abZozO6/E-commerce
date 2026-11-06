using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ecommerce.Application_BAL_.DTOs.Coupons;

namespace Ecommerce.Application_BAL_.Interfaces;

public interface ICouponService
{
    Task<IEnumerable<CouponDto>> GetAllAsync();

    Task<CouponDto?> GetByIdAsync(long couponId);

    Task<CouponDto?> GetByCodeAsync(string code);

    Task<long> CreateAsync(CreateCouponDto dto);

    Task<bool> UpdateAsync(long couponId, UpdateCouponDto dto);

    Task<bool> DeleteAsync(long couponId);

    Task<CouponValidationDto> ValidateCouponAsync(string code,decimal orderAmount);
}