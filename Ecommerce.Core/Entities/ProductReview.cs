using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Entities
{
    public class ProductReview
    {
        public long ProductReviewId { get; set; }
        public long ProductId { get; set; }
        public long UserId { get; set; }
        public byte Rating { get; set; }
        public string? Comment { get; set; } = null;
        public DateTime CreatedAt { get; set; }


    }
}
