using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Payments
{
    public class PaymentDto
    {
        public long PaymentId { get; set; }

        public long OrderId { get; set; }

        public string Provider { get; set; } = string.Empty;

        public string? ProviderReference { get; set; }

        public decimal Amount { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime? PaidAt { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
