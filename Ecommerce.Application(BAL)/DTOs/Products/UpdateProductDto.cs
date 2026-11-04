using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class UpdateProductDto
    {
        public long CategoryId { get; set; }
        public long? BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int? WarrantyMonths { get; set; }
        public bool IsActive { get; set; } = true;
        public string RowVersion { get; set; } = string.Empty;
    }
}
