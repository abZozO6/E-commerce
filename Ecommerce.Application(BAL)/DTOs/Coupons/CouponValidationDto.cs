using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Coupons
{
    public class CouponValidationDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public long? CouponId { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }
}
