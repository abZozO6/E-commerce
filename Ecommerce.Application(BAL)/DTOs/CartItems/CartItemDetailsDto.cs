using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.CartItems
{
    public class CartItemDetailsDto
    {
        public long CartItemId { get; set; }
        public long CartId { get; set; }
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public DateTime AddedAt { get; set; }

        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string? ImageUrl { get; set; }
    }
}
