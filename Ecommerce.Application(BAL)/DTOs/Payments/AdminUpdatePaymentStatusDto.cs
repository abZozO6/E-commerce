using System.ComponentModel.DataAnnotations;

namespace Ecommerce.Application_BAL_.DTOs.Payments
{
    public class AdminUpdatePaymentStatusDto
    {
        [Required]
        [MaxLength(30)]
        public string NewStatus { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? TransactionReference { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
