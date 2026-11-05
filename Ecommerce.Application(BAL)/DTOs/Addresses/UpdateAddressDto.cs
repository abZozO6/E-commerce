using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ecommerce.Application_BAL_.DTOs.Addresses
{
    public class UpdateAddressDto
    {
        public string? Label { get; set; }
        public string City { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? BuildingDetails { get; set; }
    }
}
