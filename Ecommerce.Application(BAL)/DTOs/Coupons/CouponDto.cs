using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Coupons
{
    public class CouponDto
    {
        public long CouponId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;
        public decimal DiscountValue { get; set; }
        public decimal MinOrderAmount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int? UsageLimit { get; set; }
        public bool IsActive { get; set; }
    }
}
