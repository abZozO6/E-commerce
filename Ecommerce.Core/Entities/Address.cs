using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Core.Entities
{
    public class Address
    {
        public long AddressId { get; set; }
        public long UserId { get; set; }
        public string? Label { get; set; } = null;
        public string City { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? BuildingDetails { get; set; } = null;
        public bool IsDefault { get; set; } = false;


    }
}
