using EnterpriseOMS.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Warehouses.DTOs
{
    /// <summary>An address, as sent to and returned from the API (nested as "address" in a warehouse).</summary>
    public class AddressDto
    {
        public string Line1 { get; set; } = default!;
        public string? Line2 { get; set; }
        public string City { get; set; } = default!;
        public string? Region { get; set; }
        public string? PostalCode { get; set; }
        public string CountryCode { get; set; } = default!;

        public static AddressDto From(Address address) => new()
        {
            Line1 = address.Line1,
            Line2 = address.Line2,
            City = address.City,
            Region = address.Region,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode
        };

        /// <summary>Builds the domain value object, which applies the address rules once more.</summary>
        public Address ToAddress() => Address.Create(Line1, Line2, City, Region, PostalCode, CountryCode);
    }
}
