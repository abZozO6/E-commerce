using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Application_BAL_.DTOs.Payments
{
    public class CreatePaymentDto
    {
        [Required]
        [MaxLength(50)]
        public string Provider { get; set; } = string.Empty;
    }
}
