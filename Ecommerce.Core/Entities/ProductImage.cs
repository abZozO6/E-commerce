using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Entities
{
    public class ProductImage
    {
        public long ProductImageId { get; set; }
        public long ProductId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? AltText { get; set; } = null;
        public int DisplayOrder { get; set; }
        public bool IsPrimary { get; set; } = false;

    }
}
