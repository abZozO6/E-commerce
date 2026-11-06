using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class ProductDetailsDto
    {
        public long ProductId { get; set; }
        public long? BrandId { get; set; }
        public long CategoryId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string? Description { get; set; }

        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int? WarrantyMonths { get; set; }

        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string RowVersion { get; set; } = string.Empty;

        public string CategoryName { get; set; } = string.Empty;
        public string? BrandName { get; set; }
        public string? ImageUrl { get; set; }
    }
}
