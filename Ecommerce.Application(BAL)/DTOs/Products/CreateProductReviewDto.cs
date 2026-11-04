using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class CreateProductReviewDto
    {
        public byte Rating { get; set; }
        public string? Comment { get; set; }
    }
}
