using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class ProductRatingSummaryDto
    {
        public long ProductId { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }
}
