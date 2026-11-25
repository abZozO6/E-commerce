using System;
using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Application_BAL_.DTOs.Orders
{
    [Obsolete("Use AdminUpdateOrderStatusDto instead. Order status changes require administrative authorization, payment checks, and audit logging.")]
    public class UpdateOrderStatusDto
    {
        [Required]
        [MaxLength(20)]
        public string NewStatus { get; set; } = string.Empty;
    }
}
