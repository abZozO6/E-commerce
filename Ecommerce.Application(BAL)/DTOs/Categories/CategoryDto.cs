using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Categories
{
    public class CategoryDto
    {
        public long CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public long? ParentCategoryId { get; set; }
        public bool IsActive { get; set; }
    }
}
