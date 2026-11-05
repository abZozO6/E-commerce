using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.CartItems
{
    public class AddCartItemDto
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
