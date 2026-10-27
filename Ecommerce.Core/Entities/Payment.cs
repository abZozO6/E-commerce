using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Entities
{
    public class Payment
    {
        public long PaymentId { get; set; }
        public long OrderId { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string? ProviderReference { get; set; } = null;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime? PaidAt { get; set; } = null;
        public DateTime CreatedAt { get; set; }


    }
}
