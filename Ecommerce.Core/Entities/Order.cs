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

        /// <summary>
        /// Normalizes an incoming order status string to its canonical PascalCase representation.
        /// Unrecognized values are returned trimmed for subsequent validation.
        /// </summary>
        public static string NormalizeStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return string.Empty;

            return status.Trim().ToLowerInvariant() switch
            {
                "pending"    => "Pending",
                "confirmed"  => "Confirmed",
                "processing" => "Processing",
                "shipped"    => "Shipped",
                "delivered"  => "Delivered",
                "cancelled"  => "Cancelled",
                "returned"   => "Returned",
                _            => status.Trim()
            };
        }

        /// <summary>
        /// Checks whether a status is one of the supported order statuses.
        /// </summary>
        public static bool IsAllowedStatus(string status) =>
            status is "Pending" or "Confirmed" or "Processing"
                or "Shipped" or "Delivered" or "Cancelled" or "Returned";
    }
}
