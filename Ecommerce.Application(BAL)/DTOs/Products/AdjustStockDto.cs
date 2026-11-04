using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class AdjustStockDto
    {
        public int QuantityChange { get; set; }
        public string TransactionType { get; set; } = "Restock"; // 'Restock' or 'Adjustment'
        public string? Notes { get; set; }
        public string RowVersion { get; set; } = string.Empty;


    }
}
