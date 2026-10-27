using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Entities
{
    public class Order
    {
        public long OrderId { get; set; }
        public Guid OrderNumber { get; set; }
        public long UserId { get; set; }
        public long? CouponId { get; set; } = null;
        public string Status { get; set; } = "Pending";
        public string PaymentStatus { get; set; } = "Pending";
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal TotalAmount { get; set; }
        public string ShippingFullName { get; set; } = string.Empty;
        public string ShippingPhone { get; set; } = string.Empty;
        public string ShippingCity { get; set; } = string.Empty;
        public string ShippingStreet { get; set; } = string.Empty;
        public string? ShippingBuildingDetails { get; set; } = null;
        public DateTime CreatedAt { get; set; }

    }
}
