using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class UpdateProductImageDto
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; }
        public int DisplayOrder { get; set; }
    }
}
