using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class ProductSpecificationDto
    {
        public long SpecId { get; set; }
        public long ProductId { get; set; }
        public string SpecName { get; set; } = string.Empty;
        public string SpecValue { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
