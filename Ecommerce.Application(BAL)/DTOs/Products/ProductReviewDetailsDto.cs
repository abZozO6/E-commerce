using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Products
{
    public class ProductReviewDetailsDto
    {
        public long ReviewId { get; set; }
        public long ProductId { get; set; }
        public long UserId { get; set; }

        public byte Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }

        public string UserFullName { get; set; } = string.Empty;
    }
}
