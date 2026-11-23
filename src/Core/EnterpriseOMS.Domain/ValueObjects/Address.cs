using EnterpriseOMS.Domain.Common.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.ValueObjects
{
    /// <summary>
    /// A postal address. A VALUE OBJECT: it has no identity of its own, it is defined entirely by its
    /// values, and two addresses with the same values are equal (records give us that for free).
    /// It is stored inside the owning table's row (an EF Core "owned type"), not in a table of its own.
    /// </summary>
    public sealed record Address
    {
        public const int LineMaxLength = 200;
        public const int CityMaxLength = 100;
        public const int RegionMaxLength = 100;
        public const int PostalCodeMaxLength = 20;

        private static readonly Regex CountryCodePattern = new("^[A-Z]{2}$", RegexOptions.Compiled);

        // Required by EF Core. All other code must go through Create(), which enforces the rules.
        private Address() { }

        public string Line1 { get; private set; } = default!;
        public string? Line2 { get; private set; }
        public string City { get; private set; } = default!;

        /// <summary>State, province or county. Optional because many countries do not use one.</summary>
        public string? Region { get; private set; }

        public string? PostalCode { get; private set; }

        /// <summary>Two-letter ISO 3166-1 country code, always stored in upper case (for example "IN", "NL").</summary>
        public string CountryCode { get; private set; } = default!;

        public static Address Create(
            string line1,
            string? line2,
            string city,
            string? region,
            string? postalCode,
            string countryCode)
        {
            var cleanLine1 = Required(line1, "Address line 1", LineMaxLength);
            var cleanCity = Required(city, "City", CityMaxLength);

            var cleanCountry = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
            if (!CountryCodePattern.IsMatch(cleanCountry))
                throw new DomainException("Country code must be a 2-letter ISO code such as IN or NL.");

            return new Address
            {
                Line1 = cleanLine1,
                Line2 = Optional(line2, "Address line 2", LineMaxLength),
                City = cleanCity,
                Region = Optional(region, "Region", RegionMaxLength),
                PostalCode = Optional(postalCode, "Postal code", PostalCodeMaxLength),
                CountryCode = cleanCountry
            };
        }

        private static string Required(string? value, string label, int maxLength)
        {
            var trimmed = value?.Trim() ?? string.Empty;

            if (trimmed.Length == 0)
                throw new DomainException($"{label} is required.");

            if (trimmed.Length > maxLength)
                throw new DomainException($"{label} must be {maxLength} characters or fewer.");

            return trimmed;
        }

        private static string? Optional(string? value, string label, int maxLength)
        {
            var trimmed = value?.Trim();

            if (string.IsNullOrEmpty(trimmed))
                return null;

            if (trimmed.Length > maxLength)
                throw new DomainException($"{label} must be {maxLength} characters or fewer.");

            return trimmed;
        }
    }
}
