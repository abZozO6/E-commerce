using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Application_BAL_.DTOs.Payments;

public class RefundPaymentDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
