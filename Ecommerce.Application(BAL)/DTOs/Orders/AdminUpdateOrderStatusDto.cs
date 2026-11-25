using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Application_BAL_.DTOs.Orders;

public class AdminUpdateOrderStatusDto
{
    [Required]
    [MaxLength(60)]
    public string NewStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
