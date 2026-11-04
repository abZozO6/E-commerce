using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class CreateProductImageDto
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public bool IsPrimary { get; set; }
        public int DisplayOrder { get; set; }
    }
}
