using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Orders
{
    public class CreateOrderDto
    {
        [Required]
        [Range(1, long.MaxValue)]
        public long AddressId { get; set; }

        [MaxLength(50)]
        public string? CouponCode { get; set; }
    }
}
