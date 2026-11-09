using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Orders
{
    public class UpdateOrderStatusDto
    {
        [Required]
        [MaxLength(20)]
        public string NewStatus { get; set; } = string.Empty;
    }
}
