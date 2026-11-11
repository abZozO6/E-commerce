using Ecommerce.Application_BAL_.DTOs.Coupons;
using Ecommerce.Application_BAL_.Interfaces;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces;

namespace Ecommerce.Application_BAL_.Services;

public class CouponService : ICouponService
{
    private readonly ICouponRepository _couponRepository;

    public CouponService(ICouponRepository couponRepository)
    {
        _couponRepository = couponRepository
            ?? throw new ArgumentNullException(nameof(couponRepository));
    }

    public async Task<IEnumerable<CouponDto>> GetAllAsync()
    {
        var coupons = await _couponRepository.GetAllAsync();

        return coupons.Select(MapToDto);
    }

    public async Task<CouponDto?> GetByIdAsync(long couponId)
    {
        ValidateId(couponId, nameof(couponId));

        var coupon = await _couponRepository.GetByIdAsync(couponId);

        return coupon is null ? null : MapToDto(coupon);
    }

    public async Task<CouponDto?> GetByCodeAsync(string code)
    {
        ValidateCode(code);

        var normalizedCode = code.Trim().ToUpperInvariant();
        var coupon = await _couponRepository.GetByCodeAsync(normalizedCode);

        return coupon is null ? null : MapToDto(coupon);
    }

    public async Task<long> CreateAsync(CreateCouponDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        ValidateCouponData(
            dto.Code,
            dto.DiscountType,
            dto.DiscountValue,
            dto.MinOrderAmount,
            dto.MaxDiscountAmount,
            dto.StartsAt,
            dto.ExpiresAt,
            dto.UsageLimit);

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();

        if (await _couponRepository.ExistsByCodeAsync(normalizedCode))
            throw new InvalidOperationException(
                "A coupon with this code already exists.");

        var coupon = new Coupon
        {
            Code = normalizedCode,
            DiscountType = NormalizeDiscountType(dto.DiscountType),
            DiscountValue = dto.DiscountValue,
            MinOrderAmount = dto.MinOrderAmount,
            MaxDiscountAmount = dto.MaxDiscountAmount,
            StartsAt = dto.StartsAt,
            ExpiresAt = dto.ExpiresAt,
            UsageLimit = dto.UsageLimit,
            IsActive = dto.IsActive
        };

        return await _couponRepository.CreateAsync(coupon);
    }

    public async Task<bool> UpdateAsync(long couponId, UpdateCouponDto dto)
    {
        ValidateId(couponId, nameof(couponId));

        ArgumentNullException.ThrowIfNull(dto);

        ValidateCouponData(
            dto.Code,
            dto.DiscountType,
            dto.DiscountValue,
            dto.MinOrderAmount,
            dto.MaxDiscountAmount,
            dto.StartsAt,
            dto.ExpiresAt,
            dto.UsageLimit);

        var existingCoupon = await _couponRepository.GetByIdAsync(couponId);

        if (existingCoupon is null)
            return false;

        var normalizedCode = dto.Code.Trim().ToUpperInvariant();

        if (await _couponRepository.ExistsByCodeAsync(
            normalizedCode,
            couponId))
        {
            throw new InvalidOperationException(
                "A coupon with this code already exists.");
        }

        existingCoupon.Code = normalizedCode;
        existingCoupon.DiscountType = NormalizeDiscountType(dto.DiscountType);
        existingCoupon.DiscountValue = dto.DiscountValue;
        existingCoupon.MinOrderAmount = dto.MinOrderAmount;
        existingCoupon.MaxDiscountAmount = dto.MaxDiscountAmount;
        existingCoupon.StartsAt = dto.StartsAt;
        existingCoupon.ExpiresAt = dto.ExpiresAt;
        existingCoupon.UsageLimit = dto.UsageLimit;
        existingCoupon.IsActive = dto.IsActive;

        return await _couponRepository.UpdateAsync(existingCoupon);
    }

    public async Task<bool> DeleteAsync(long couponId)
    {
        ValidateId(couponId, nameof(couponId));

        var coupon = await _couponRepository.GetByIdAsync(couponId);

        if (coupon is null)
            return false;

        var usageCount = await _couponRepository.GetUsageCountAsync(couponId);

        if (usageCount > 0)
        {
            throw new InvalidOperationException(
                "This coupon has been used and cannot be deleted. " +
                "Deactivate it instead.");
        }

        return await _couponRepository.DeleteAsync(couponId);
    }



    /// <summary>
    /// Validates a coupon against the order amount and calculates the discount.
    /// </summary>
    public async Task<CouponValidationDto> ValidateCouponAsync(string code,decimal orderAmount)
    {
        ValidateCode(code);

        if (orderAmount <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(orderAmount),
                "Order amount must be greater than zero.");

        var normalizedCode = code.Trim().ToUpperInvariant();
        var coupon = await _couponRepository.GetByCodeAsync(normalizedCode);

        if (coupon is null)
            return InvalidCoupon("Coupon code was not found.");

        if (!coupon.IsActive)
            return InvalidCoupon("This coupon is inactive.");

        var now = DateTime.UtcNow;

        if (now < coupon.StartsAt)
            return InvalidCoupon("This coupon is not active yet.");

        if (now > coupon.ExpiresAt)
            return InvalidCoupon("This coupon has expired.");

        if (orderAmount < coupon.MinOrderAmount)
        {
            return InvalidCoupon(
                $"Minimum order amount is {coupon.MinOrderAmount}.");
        }

        var usageCount = await _couponRepository.GetUsageCountAsync(
            coupon.CouponId);

        if (coupon.UsageLimit.HasValue &&
            usageCount >= coupon.UsageLimit.Value)
        {
            return InvalidCoupon("This coupon has reached its usage limit.");
        }

        var discountAmount = CalculateDiscount(coupon, orderAmount);

        return new CouponValidationDto
        {
            IsValid = true,
            Message = "Coupon applied successfully.",
            CouponId = coupon.CouponId,
            DiscountAmount = discountAmount,
            FinalAmount = orderAmount - discountAmount
        };
    }

    private static decimal CalculateDiscount(Coupon coupon,decimal orderAmount)
    {
        decimal discountAmount;

        if (coupon.DiscountType == "Percentage")
        {
            discountAmount = orderAmount * coupon.DiscountValue / 100m;
        }
        else
        {
            discountAmount = coupon.DiscountValue;
        }

        if (coupon.MaxDiscountAmount.HasValue)
        {
            discountAmount = Math.Min(
                discountAmount,
                coupon.MaxDiscountAmount.Value);
        }

        // A discount must never exceed the order amount.
        return Math.Min(discountAmount, orderAmount);
    }

    private static void ValidateCouponData(
        string code,
        string discountType,
        decimal discountValue,
        decimal minOrderAmount,
        decimal? maxDiscountAmount,
        DateTime startsAt,
        DateTime expiresAt,
        int? usageLimit)
    {
        ValidateCode(code);

        if (code.Trim().Length > 50)
            throw new ArgumentException(
                "Coupon code cannot exceed 50 characters.");

        if (string.IsNullOrWhiteSpace(discountType))
            throw new ArgumentException(
                "Discount type is required.");

        var normalizedType = NormalizeDiscountType(discountType);

        if (normalizedType == "Percentage" && discountValue > 100)
            throw new ArgumentException(
                "Percentage discount cannot exceed 100.");

        if (discountValue <= 0)
            throw new ArgumentException(
                "Discount value must be greater than zero.");

        if (minOrderAmount < 0)
            throw new ArgumentException(
                "Minimum order amount cannot be negative.");

        if (maxDiscountAmount.HasValue &&
            maxDiscountAmount.Value <= 0)
        {
            throw new ArgumentException(
                "Maximum discount amount must be greater than zero.");
        }

        if (expiresAt <= startsAt)
            throw new ArgumentException(
                "Expiration date must be later than the start date.");

        if (usageLimit.HasValue && usageLimit.Value <= 0)
            throw new ArgumentException(
                "Usage limit must be greater than zero.");
    }

    private static string NormalizeDiscountType(string discountType)
    {
        if (discountType.Trim().Equals(
            "Percentage",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Percentage";
        }

        if (discountType.Trim().Equals(
            "Fixed",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Fixed";
        }

        throw new ArgumentException(
            "Discount type must be Percentage or Fixed.");
    }

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Coupon code is required.");

        if (code.Trim().Length > 50)
            throw new ArgumentException(
                "Coupon code cannot exceed 50 characters.");
    }

    private static void ValidateId(long id, string paramName)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(
                paramName,
                $"Invalid {paramName}.");
    }

    private static CouponDto MapToDto(Coupon coupon)
    {
        return new CouponDto
        {
            CouponId = coupon.CouponId,
            Code = coupon.Code,
            DiscountType = coupon.DiscountType,
            DiscountValue = coupon.DiscountValue,
            MinOrderAmount = coupon.MinOrderAmount,
            MaxDiscountAmount = coupon.MaxDiscountAmount,
            StartsAt = coupon.StartsAt,
            ExpiresAt = coupon.ExpiresAt,
            UsageLimit = coupon.UsageLimit,
            IsActive = coupon.IsActive
        };
    }

    private static CouponValidationDto InvalidCoupon(string message)
    {
        return new CouponValidationDto
        {
            IsValid = false,
            Message = message,
            DiscountAmount = 0,
            FinalAmount = 0
        };
    }
}
