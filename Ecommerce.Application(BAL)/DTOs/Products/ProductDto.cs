using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class ProductDto
    {
        public long ProductId { get; set; }
        public long CategoryId { get; set; }
        public long? BrandId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int? WarrantyMonths { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        // يُرسل كـ Base64 String للواجهة الأمامية ليتم إرجاعه عند التعديل أو تحديث المخزون
        public string RowVersion { get; set; } = string.Empty;
    }
}
