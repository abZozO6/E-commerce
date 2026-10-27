using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Entities
{
    public class InventoryTransaction
    {
        public long InventoryTransactionId { get; set; }
        public long ProductId { get; set; }
        public int QuantityChange { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public long? ReferenceOrderId { get; set; } = null;
        public DateTime CreatedAt { get; set; }
        public string? Notes { get; set; } = null;
    }
}
